using System.Text;
using System.Text.Json;
using System.Net.Http;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Timers;

namespace AdminPlus;

public static class Discord
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };
    
    public static void Dispose()
    {
        try
        {
            _httpClient?.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminPlus] Discord HttpClient dispose error: {ex.Message}");
        }
    }

    public static void StartStatusTimer(AdminPlus plugin)
    {
        if (string.IsNullOrWhiteSpace(ServerStatusWebhook))
        {
            return;
        }
        
        _statusTimer = plugin.AddTimer(180f, () =>
        {
            try
            {
                var playerCount = Utilities.GetPlayers().Count(p => p.IsValid && !p.IsBot);
                var maxPlayers = Server.MaxPlayers;
                var currentMap = Server.MapName;
                
                var serverIp = "0.0.0.0:27015";
                try
                {
                    var ipConVar = ConVar.Find("ip");
                    if (ipConVar != null && !string.IsNullOrEmpty(ipConVar.StringValue))
                    {
                        var ip = ipConVar.StringValue;
                        if (ip != "0.0.0.0")
                        {
                            serverIp = $"{ip}:27015";
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AdminPlus] IP detection error: {ex.Message}");
                }
                
                var uptime = GetServerUptime();
                var timeLeft = GetMapTimeLeft();

                _ = SendServerStatus(plugin, "Online", playerCount, maxPlayers, currentMap, uptime, serverIp, timeLeft);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AdminPlus] Discord status timer error: {ex.Message}");
            }
        }, TimerFlags.REPEAT);
    }

    public static void StopStatusTimer()
    {
        try
        {
            _statusTimer?.Kill();
            _statusTimer = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminPlus] Discord status timer stop error: {ex.Message}");
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static string CloudflareWorkerUrl = "https://adminplus-discord.ed1xby.workers.dev";
    private static string BanWebhook = "";
    private static string AdminActionsWebhook = "";
    private static string CommunicationWebhook = "";
    private static string ServerStatusWebhook = "";
    private static string ConnectionLogsWebhook = "";
    private static string ChatLogsWebhook = "";
    private static string ReportAndCalladminWebhook = "";
    private static string ReportAndCalladminWebhookMentionUserId = "";
    private static Timer? _statusTimer;

    public static void LoadConfig()
    {
        try
        {
            var configFile = Path.Combine(AdminPlus._instance?.ModuleDirectory ?? "", "adminplus-discord.json");
            
            if (!File.Exists(configFile))
            {
                CreateDefaultConfig(configFile);
            }
            
            if (File.Exists(configFile))
            {
                var json = File.ReadAllText(configFile);
                var config = JsonSerializer.Deserialize<DiscordConfig>(json, JsonOptions);
                
                if (config?.DiscordWebhooks != null)
                {
                    BanWebhook = config.DiscordWebhooks.BanWebhook ?? "";
                    AdminActionsWebhook = config.DiscordWebhooks.AdminActionsWebhook ?? "";
                    CommunicationWebhook = config.DiscordWebhooks.CommunicationWebhook ?? "";
                    ServerStatusWebhook = config.DiscordWebhooks.ServerStatusWebhook ?? "";
                    ConnectionLogsWebhook = config.DiscordWebhooks.ConnectionLogsWebhook ?? "";
                    ChatLogsWebhook = config.DiscordWebhooks.ChatLogsWebhook ?? "";
                    ReportAndCalladminWebhook = config.DiscordWebhooks.ReportAndCalladminWebhook ?? "";
                    ReportAndCalladminWebhookMentionUserId = config.DiscordWebhooks.ReportAndCalladminWebhookMentionUserId ?? "@everyone";
                    
                }
                else
                {
                    Console.WriteLine($"[AdminPlus] DiscordWebhooks config is null!");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminPlus] Config load error: {ex.Message}");
        }
    }

    private static void CreateDefaultConfig(string configFile)
    {
        try
        {
            var defaultConfig = new
            {
                DiscordWebhooks = new
                {
                    BanWebhook = "",
                    AdminActionsWebhook = "",
                    CommunicationWebhook = "",
                    ServerStatusWebhook = "",
                    ConnectionLogsWebhook = "",
                    ChatLogsWebhook = "",
                    ReportAndCalladminWebhook = "",
                    ReportAndCalladminWebhookMentionUserId = "@everyone"
                }
            };
            
            var json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            File.WriteAllText(configFile, json);
            Console.WriteLine($"[AdminPlus] Created Discord config file: {configFile}");
            Console.WriteLine($"[AdminPlus] Config file created successfully!");
            Console.WriteLine($"[AdminPlus] Please edit the config file and add your Discord webhook URLs");
        }
        catch (Exception)
        {
        }
    }

    public static async Task SendCommunicationLog(string playerName, ulong playerSteamId, string adminName, ulong adminSteamId, string reason, int duration, string actionType, bool isApplied, AdminPlus plugin)
    {
        if (string.IsNullOrWhiteSpace(CommunicationWebhook))
        {
            return;
        }

        if (plugin == null) return;

        string emoji = actionType.ToLower() switch
        {
            "mute" => "🔇",
            "gag" => "🤐", 
            "silence" => "🔕",
            _ => "💬"
        };

        string actionText = actionType.ToLower() switch
        {
            "mute" => isApplied ? plugin.Localizer["Discord.CommunicationLog.Muted"].Value : plugin.Localizer["Discord.CommunicationLog.Unmuted"].Value,
            "gag" => isApplied ? plugin.Localizer["Discord.CommunicationLog.Gagged"].Value : plugin.Localizer["Discord.CommunicationLog.Ungagged"].Value,
            "silence" => isApplied ? plugin.Localizer["Discord.CommunicationLog.Silenced"].Value : plugin.Localizer["Discord.CommunicationLog.Unsilenced"].Value,
            _ => actionType
        };

        int color = isApplied ? 16711680 : 65280; 

        string steamId3 = ConvertToSteamId3(playerSteamId.ToString());

        var fields = new List<object>
        {
            new { name = plugin.Localizer["Discord.CommunicationLog.PlayerName"].Value, value = $"▶ {playerName}", inline = true },
            new { name = plugin.Localizer["Discord.CommunicationLog.SteamID"].Value, value = $"▶ `{steamId3}`", inline = true },
            new { name = plugin.Localizer["Discord.CommunicationLog.AdminName"].Value, value = $"▶ {adminName}", inline = true },
            new { name = plugin.Localizer["Discord.CommunicationLog.Reason"].Value, value = $"▶ {reason}", inline = true },
            new { name = plugin.Localizer["Discord.CommunicationLog.ActionType"].Value, value = $"▶ {actionType}", inline = true }
        };

        if (isApplied)
        {
            string durationText;
            if (duration > 0)
            {
                durationText = plugin.Localizer["Duration.Temporary", duration].Value;
            }
            else
            {
                durationText = plugin.Localizer["Duration.Forever"].Value;
            }
            fields.Add(new { name = plugin.Localizer["Discord.CommunicationLog.Duration"].Value, value = $"▶ {durationText}", inline = true });
        }

        var embedObject = new
        {
            embeds = new[]
            {
                new
                {
                    title = $"{emoji} {plugin.Localizer["Discord.CommunicationLog.Title"].Value}",
                    description = $"**{playerName}** {actionText.ToLower()}",
                    color = color,
                    fields = fields.ToArray(),
                    footer = new { text = plugin.Localizer["Discord.CommunicationLog.Footer"].Value },
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                }
            }
        };

        await SendMessageWithFallback("communication", embedObject);
    }

    public static async Task SendAdminActionLog(string action, string targetName, ulong targetSteamId, string adminName, ulong adminSteamId, string reason, AdminPlus plugin)
    {
        if (string.IsNullOrWhiteSpace(AdminActionsWebhook))
        {
            return;
        }

        if (plugin == null) return;

        string emoji = action.ToLower() switch
        {
            "kick" => "👢",
            "slay" => "💀",
            "slap" => "👋",
            "noclip" => "👻",
            "god" => "🛡️",
            "respawn" => "🔄",
            "teleport" => "📡",
            "speed" => "🏃",
            "gravity" => "⬆️",
            "hp" => "❤️",
            "armor" => "🛡️",
            "money" => "💰",
            "weapon" => "🔫",
            "freeze" => "❄️",
            "unfreeze" => "🔥",
            "glow" => "✨",
            "drug" => "💊",
            "shake" => "📳",
            "rcon" => "🎮",
            "team" => "👥",
            "rename" => "🏷️",
            "swap" => "🔄",
            "beacon" => "📡",
            "blind" => "👁️",
            "color" => "🎨",
            "bury" => "⛏️",
            "clean" => "🧹",
            "goto" => "➡️",
            "bring" => "⬅️",
            "hrespawn" => "🔄",
            "sethp" => "❤️",
            "map" => "🗺️",
            "wsmap" => "🗺️",
            "who" => "👤",
            "rr" => "🔄",
            "players" => "👥",
            "vote" => "🗳️",
            "votemap" => "🗳️",
            "votekick" => "🗳️",
            "rvote" => "🗳️",
            "cancelvote" => "❌",
            "admin" => "👑",
            "strip" => "🚫",
            _ => "⚡"
        };

        string steamId3 = ConvertToSteamId3(targetSteamId.ToString());

        var fields = new List<object>
        {
            new { name = plugin.Localizer["Discord.AdminActionLog.Action"].Value, value = $"▶ {action}", inline = true },
            new { name = plugin.Localizer["Discord.AdminActionLog.Target"].Value, value = $"▶ {targetName}", inline = true },
            new { name = plugin.Localizer["Discord.AdminActionLog.SteamID"].Value, value = $"▶ `{steamId3}`", inline = true },
            new { name = plugin.Localizer["Discord.AdminActionLog.Admin"].Value, value = $"▶ {adminName}", inline = true }
        };

        if (!string.IsNullOrWhiteSpace(reason))
        {
            string fieldName = action.ToLower() switch
            {
                "slap" => plugin.Localizer["Discord.AdminActionLog.Damage"].Value,
                "money" => plugin.Localizer["Discord.AdminActionLog.Amount"].Value,
                "armor" => plugin.Localizer["Discord.AdminActionLog.Amount"].Value,
                "beacon" => plugin.Localizer["Discord.AdminActionLog.Beacon"].Value,
                "freeze" => plugin.Localizer["Discord.AdminActionLog.Freeze"].Value,
                "gravity" => plugin.Localizer["Discord.AdminActionLog.Gravity"].Value,
                "noclip" => plugin.Localizer["Discord.AdminActionLog.Noclip"].Value,
                "weapon" => plugin.Localizer["Discord.AdminActionLog.Weapon"].Value,
                "hp" => plugin.Localizer["Discord.AdminActionLog.HP"].Value,
                "speed" => plugin.Localizer["Discord.AdminActionLog.Speed"].Value,
                "god" => plugin.Localizer["Discord.AdminActionLog.God"].Value,
                "glow" => plugin.Localizer["Discord.AdminActionLog.Glow"].Value,
                "shake" => plugin.Localizer["Discord.AdminActionLog.Shake"].Value,
                "blind" => plugin.Localizer["Discord.AdminActionLog.Blind"].Value,
                "drug" => plugin.Localizer["Discord.AdminActionLog.Drug"].Value,
                _ => plugin.Localizer["Discord.AdminActionLog.Reason"].Value
            };
            
            string displayValue = action.ToLower() switch
            {
                "beacon" when reason == "applied" => plugin.Localizer["Discord.AdminActionLog.Applied"].Value,
                "beacon" when reason == "removed" => plugin.Localizer["Discord.AdminActionLog.Removed"].Value,
                "freeze" when reason != "permanent" => $"{reason} {plugin.Localizer["Discord.AdminActionLog.Seconds"].Value}",
                "gravity" => reason,
                "shake" => $"{reason} {plugin.Localizer["Discord.AdminActionLog.Seconds"].Value}",
                "blind" => $"{reason} {plugin.Localizer["Discord.AdminActionLog.Seconds"].Value}",
                "drug" => $"{reason} {plugin.Localizer["Discord.AdminActionLog.Seconds"].Value}",
                _ => reason
            };
            
            fields.Add(new { name = fieldName, value = $"▶ {displayValue}", inline = true });
        }

        var embedObject = new
        {
            embeds = new[]
            {
                new
                {
                    title = $"{emoji} {plugin.Localizer["Discord.AdminActionLog.Title"].Value}",
                    description = $"**{adminName}** {action.ToLower()}ed **{targetName}**",
                    color = 16776960,
                    fields = fields.ToArray(),
                    footer = new { text = plugin.Localizer["Discord.AdminActionLog.Footer"].Value },
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                }
            }
        };

        await SendMessageWithFallback("adminActions", embedObject);
    }

    public static async Task SendBanMessage(string playerName, ulong playerSteamId, string adminName, ulong adminSteamId, string reason, int duration, bool isBan)
    {
        if (string.IsNullOrWhiteSpace(BanWebhook))
        {
            return;
        }

        int color = isBan ? (duration == 0 ? 16711680 : 16738740) : 3447003;
        string title = isBan ? "🔨 Player Banned" : "✅ Player Unbanned";
        
        string adminValue = adminSteamId == 0 ?
            $"**{adminName}**" :
            $"**[{adminName}](https://steamcommunity.com/profiles/{adminSteamId}) [{adminSteamId}]**";

        List<object> fields = new()
        {
            new { name = "👤 Admin", value = adminValue, inline = true },
            new { name = "🎯 Player", value = $"**[{playerName}](https://steamcommunity.com/profiles/{playerSteamId}) [{playerSteamId}]**", inline = true }
        };

        if (isBan)
        {
            fields.Add(new { name = '\u200b', value = '\u200b', inline = true });

            if (duration == 0)
            {
                fields.Add(new { name = "⏰ Duration", value = "`Permanent`", inline = true });
            }
            else
            {
                fields.Add(new { name = "⏰ Duration", value = $"`{duration} minutes`", inline = true });
                fields.Add(new { name = "📅 End Time", value = $"`{DateTime.Now.AddMinutes(duration):dd-MM-yyyy HH:mm:ss}`", inline = true });
            }
        }

        if (!string.IsNullOrWhiteSpace(reason))
        {
            fields.Add(new { name = "📝 Reason", value = $"`{reason}`", inline = true });
        }

        if (fields.Count < 6)
        {
            fields.Add(new { name = '\u200b', value = '\u200b', inline = true });
        }

        var embedObject = new
        {
            embeds = new[]
            {
                new
                {
                    title,
                    color,
                    fields,
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    footer = new { text = "AdminPlus Bot" }
                }
            }
        };

        string jsonString = JsonSerializer.Serialize(embedObject, JsonOptions);
        using StringContent stringContent = new(jsonString, Encoding.UTF8, "application/json");

        try
        {
            using HttpResponseMessage response = await _httpClient.PostAsync(BanWebhook, stringContent).ConfigureAwait(false);
            string responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
            }
            else
            {
            }
        }
        catch (HttpRequestException)
        {
        }
        catch (Exception)
        {
        }
    }


    public static async Task SendServerStatus(AdminPlus plugin, string status, int playerCount, int maxPlayers, string currentMap, string uptime, string serverIp = "", string timeLeft = "")
    {
        if (string.IsNullOrWhiteSpace(ServerStatusWebhook))
        {
            return;
        }

        var ctPlayers = new List<string>();
        var tPlayers = new List<string>();
        
        try
        {
            foreach (var player in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot && p.TeamNum == 3))
            {
                ctPlayers.Add(player.PlayerName);
            }
            
            foreach (var player in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot && p.TeamNum == 2))
            {
                tPlayers.Add(player.PlayerName);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminPlus] Error getting player lists: {ex.Message}");
        }

        var onlineAdmins = 0;
        try
        {
            foreach (var player in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot))
            {
                if (player.AuthorizedSteamID != null)
                {
                    onlineAdmins++;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminPlus] Error counting admins: {ex.Message}");
        }

        var ctNames = ctPlayers.Count > 0 ? string.Join("\n", ctPlayers.Select(p => $"```ansi\n\u001b[0;34m🔵 {p}\u001b[0m\n```")) : $"```ansi\n\u001b[0;34m🔵 {plugin.Localizer["Discord.ServerStatus.Empty"].Value}\u001b[0m\n```";
        var tNames = tPlayers.Count > 0 ? string.Join("\n", tPlayers.Select(p => $"```ansi\n\u001b[0;33m🟡 {p}\u001b[0m\n```")) : $"```ansi\n\u001b[0;33m🟡 {plugin.Localizer["Discord.ServerStatus.Empty"].Value}\u001b[0m\n```";

        var mapImageUrl = GetMapImageUrl(currentMap);
        
        var serverHostname = "CS2 Server";
        try
        {
            var hostnameConVar = ConVar.Find("hostname");
            if (hostnameConVar != null && !string.IsNullOrEmpty(hostnameConVar.StringValue))
            {
                serverHostname = hostnameConVar.StringValue;
                Console.WriteLine($"[AdminPlus] Hostname found: {serverHostname}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminPlus] Hostname detection error: {ex.Message}");
        }
        
        var embedObject = new
        {
            embeds = new[]
            {
                new
                {
                    title = $"🎮 {serverHostname}",
                    description = plugin.Localizer["Discord.ServerStatus.Description"].Value,
                    color = 0x00ff88,
                    image = !string.IsNullOrEmpty(mapImageUrl) ? new { url = mapImageUrl } : null,
                    fields = new object[]
                    {
                        new { name = $"🗺️ {plugin.Localizer["Discord.ServerStatus.Map"].Value}", value = $"```ansi\n\u001b[2;31m{currentMap}\u001b[0m\n```", inline = true },
                        new { name = plugin.Localizer["Discord.ServerStatus.Players"].Value, value = $"```ansi\n\u001b[2;32m{playerCount}\u001b[0m/\u001b[2;37m{maxPlayers}\u001b[0m\n```", inline = true },
                        new { name = plugin.Localizer["Discord.ServerStatus.ServerIP"].Value, value = $"```ansi\n\u001b[2;36m{serverIp}\u001b[0m\n```", inline = false },
                        new { name = plugin.Localizer["Discord.ServerStatus.CTTeam"].Value, value = ctNames, inline = true },
                        new { name = plugin.Localizer["Discord.ServerStatus.TTeam"].Value, value = tNames, inline = true },
                        new { name = plugin.Localizer["Discord.ServerStatus.OnlineAdmin"].Value, value = $"```ansi\n\u001b[2;35m👑 {onlineAdmins} {plugin.Localizer["Discord.ServerStatus.AdminCount"].Value}\u001b[0m\n```", inline = true },
                    },
                    footer = new { text = plugin.Localizer["Discord.ServerStatus.Footer"].Value },
                    timestamp = DateTime.UtcNow
                }
            },
        };

        try
        {
            await SendMessageWithFallback("serverStatus", embedObject);
        }
        catch (Exception)
        {
        }
    }

    private static string GetWebhookUrl(string webhookType)
    {
        return webhookType switch
        {
            "ban" => BanWebhook,
            "banLogs" => BanWebhook,
            "adminActions" => AdminActionsWebhook,
            "communication" => CommunicationWebhook,
            "serverStatus" => ServerStatusWebhook,
            "connectionLogs" => ConnectionLogsWebhook,
            "chatLogs" => ChatLogsWebhook,
            "reports" => ReportAndCalladminWebhook,
            _ => ""
        };
    }

    public static async Task SendConnectionLog(string playerName, string steamId, string ip, string action, AdminPlus plugin)
    {
        if (string.IsNullOrWhiteSpace(ConnectionLogsWebhook))
            return;

        try
        {
            var steamId3 = ConvertToSteamId3(steamId);
            
            string emoji = action == "Connect" ? "🟢" : "🔴";
            int color = action == "Connect" ? 0x00ff00 : 0xff0000;
            string actionText = action == "Connect" ? plugin.Localizer["Discord.ConnectionLog.Connected"].Value : plugin.Localizer["Discord.ConnectionLog.Disconnected"].Value;

            var embedObject = new
            {
                embeds = new[]
                {
                    new
                    {
                        title = $"{emoji} {actionText}",
                        description = $"**{playerName}** {actionText.ToLower()}",
                        color = color,
                        fields = new[]
                        {
                            new { name = plugin.Localizer["Discord.ConnectionLog.PlayerName"].Value, value = $"▶ {playerName}", inline = true },
                            new { name = plugin.Localizer["Discord.ConnectionLog.SteamID"].Value, value = $"▶ `{steamId3}`", inline = true },
                            new { name = plugin.Localizer["Discord.ConnectionLog.IPAddress"].Value, value = $"▶ `{ip}`", inline = true },
                            new { name = "⏰ Time", value = $"<t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:R>", inline = true }
                        },
                        footer = new
                        {
                            text = plugin.Localizer["Discord.ConnectionLog.Footer"].Value
                        },
                        timestamp = DateTime.UtcNow
                    }
                }
            };

            await SendMessageWithFallback("connectionLogs", embedObject);
        }
        catch (Exception)
        {
        }
    }

    public static async Task SendBanLog(string playerName, string steamId, string ip, string adminName, string reason, string duration, bool isUnban, AdminPlus plugin)
    {
        if (string.IsNullOrWhiteSpace(BanWebhook))
            return;

        try
        {
            var steamId3 = ConvertToSteamId3(steamId);
            
            string emoji = isUnban ? "✅" : "🔨";
            int color = isUnban ? 0x00ff00 : 0xff0000;
            string actionText = isUnban ? plugin.Localizer["Discord.BanLog.Unbanned"].Value : plugin.Localizer["Discord.BanLog.Banned"].Value;

            var embedObject = new
            {
                embeds = new[]
                {
                    new
                    {
                        title = $"{emoji} {actionText}",
                        description = $"**{playerName}** {actionText.ToLower()}",
                        color = color,
                        fields = isUnban ? new[]
                        {
                            new { name = plugin.Localizer["Discord.BanLog.PlayerName"].Value, value = $"▶ {playerName}", inline = true },
                            new { name = plugin.Localizer["Discord.BanLog.SteamID"].Value, value = $"▶ `{steamId3}`", inline = true },
                            new { name = plugin.Localizer["Discord.BanLog.IPAddress"].Value, value = $"▶ `{ip}`", inline = true },
                            new { name = plugin.Localizer["Discord.BanLog.AdminName"].Value, value = $"▶ {adminName}", inline = true },
                            new { name = plugin.Localizer["Discord.BanLog.Reason"].Value, value = $"▶ {reason}", inline = true }
                        } : new[]
                        {
                            new { name = plugin.Localizer["Discord.BanLog.PlayerName"].Value, value = $"▶ {playerName}", inline = true },
                            new { name = plugin.Localizer["Discord.BanLog.SteamID"].Value, value = $"▶ `{steamId3}`", inline = true },
                            new { name = plugin.Localizer["Discord.BanLog.IPAddress"].Value, value = $"▶ `{ip}`", inline = true },
                            new { name = plugin.Localizer["Discord.BanLog.AdminName"].Value, value = $"▶ {adminName}", inline = true },
                            new { name = plugin.Localizer["Discord.BanLog.Reason"].Value, value = $"▶ {reason}", inline = true },
                            new { name = plugin.Localizer["Discord.BanLog.Duration"].Value, value = $"▶ {duration}", inline = true }
                        },
                        footer = new
                        {
                            text = plugin.Localizer["Discord.BanLog.Footer"].Value
                        },
                        timestamp = DateTime.UtcNow
                    }
                }
            };

            await SendMessageWithFallback("banLogs", embedObject);
        }
        catch (Exception)
        {
        }
    }


    public static async Task SendChatLog(string playerName, string steamId, string message, string channel, AdminPlus plugin)
    {
        if (string.IsNullOrWhiteSpace(ChatLogsWebhook))
            return;

        try
        {
            var steamId3 = ConvertToSteamId3(steamId);
            
            var embedObject = new
            {
                embeds = new[]
                {
                    new
                    {
                        title = plugin.Localizer["Discord.ChatLog.Title", playerName].Value,
                        description = $"**{plugin.Localizer["Discord.ChatLog.Message"].Value}**\n```{message}```",
                        color = 0x00ff88,
                        fields = new[]
                        {
                            new { name = plugin.Localizer["Discord.ChatLog.SteamID"].Value, value = $"`{steamId3}`", inline = true },
                            new { name = plugin.Localizer["Discord.ChatLog.Channel"].Value, value = $"`{channel}`", inline = true },
                            new { name = plugin.Localizer["Discord.ChatLog.Time"].Value, value = $"<t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:R>", inline = true }
                        },
                        footer = new
                        {
                            text = plugin.Localizer["Discord.ChatLog.Footer"].Value
                        },
                        timestamp = DateTime.UtcNow
                    }
                }
            };

            await SendMessageWithFallback("chatLogs", embedObject);
        }
        catch (Exception)
        {
        }
    }

    public static async Task SendPlayerReport(string reporterName, string reporterSteamId, string reportedName, string reportedSteamId, string reason, string serverIp, AdminPlus plugin)
    {
        if (string.IsNullOrWhiteSpace(ReportAndCalladminWebhook))
            return;

        try
        {
            var reporterSteamId3 = ConvertToSteamId3(reporterSteamId);
            var reportedSteamId3 = ConvertToSteamId3(reportedSteamId);
            
            var serverHostname = "CS2 Server";
            try
            {
                var hostnameConVar = ConVar.Find("hostname");
                if (hostnameConVar != null && !string.IsNullOrEmpty(hostnameConVar.StringValue))
                {
                    serverHostname = hostnameConVar.StringValue;
                }
            }
            catch { }

            var embedObject = new
            {
                content = GetReportMentionContent(),
                embeds = new[]
                {
                    new
                    {
                        title = $"🚨 {plugin.Localizer["Discord.Report.Title"].Value}",
                        description = plugin.Localizer["Discord.Report.Description"].Value,
                        color = 0xeb4034,
                        fields = new[]
                        {
                            new
                            {
                                name = plugin.Localizer["Discord.Report.Reporter"].Value,
                                value = $"**{plugin.Localizer["Discord.Report.PlayerName"].Value}:** {reporterName}\n**SteamID:** {reporterSteamId3}\n**Steam:** [{plugin.Localizer["Discord.Report.SteamProfile"].Value}](https://steamcommunity.com/profiles/{reporterSteamId}/)",
                                inline = false
                            },
                            new
                            {
                                name = plugin.Localizer["Discord.Report.Reported"].Value,
                                value = $"**{plugin.Localizer["Discord.Report.PlayerName"].Value}:** {reportedName}\n**SteamID:** {reportedSteamId3}\n**Steam:** [{plugin.Localizer["Discord.Report.SteamProfile"].Value}](https://steamcommunity.com/profiles/{reportedSteamId}/)",
                                inline = false
                            },
                            new
                            {
                                name = plugin.Localizer["Discord.Report.Reason"].Value,
                                value = reason,
                                inline = false
                            },
                            new
                            {
                                name = plugin.Localizer["Discord.Report.DirectConnect"].Value,
                                value = $"**[`connect {serverIp}`]** - {plugin.Localizer["Discord.Report.ClickToConnect"].Value}",
                                inline = false
                            }
                        },
                        footer = new { text = plugin.Localizer["Discord.Report.Footer"].Value },
                        timestamp = DateTime.UtcNow
                    }
                }
            };

            await SendMessageWithFallback("reports", embedObject);
        }
        catch (Exception)
        {
        }
    }

    private static string ConvertToSteamId3(string steamId)
    {
        try
        {
            if (string.IsNullOrEmpty(steamId))
                return steamId;

            if (ulong.TryParse(steamId, out ulong steamId64))
            {
                var accountId = (steamId64 - 76561197960265728) & 0xFFFFFFFF;
                return $"U:1:{accountId}";
            }
            
            return steamId;
        }
        catch
        {
            return steamId; 
        }
    }

    private static string GetReportMentionRole()
    {
        return ReportAndCalladminWebhookMentionUserId; 
    }

    private static string GetReportMentionContent()
    {
        var mention = ReportAndCalladminWebhookMentionUserId;
        
        if (string.IsNullOrWhiteSpace(mention) || mention == "none" || mention == "")
        {
            return ""; // Bildirim yok
        }
        else if (mention == "@everyone")
        {
            return "@everyone";
        }
        else if (mention == "@here")
        {
            return "@here";
        }
        else if (mention.StartsWith("@&"))
        {
            return mention;
        }
        else if (mention.StartsWith("<@") && mention.EndsWith(">"))
        {
            return mention;
        }
        else
        {
            return $"<@&{mention}>";
        }
    }

    private static string GetMapImageUrl(string mapName)
    {
        var baseUrl = "https://raw.githubusercontent.com/ghostcap-gaming/cs2-map-images/main/cs2/";
        return $"{baseUrl}{mapName.ToLower()}.png";
    }

    private static async Task<bool> SendDirectToDiscord(string webhookUrl, object embedData)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl))
            return false;

        try
        {
            string jsonString = JsonSerializer.Serialize(embedData, JsonOptions);
            using StringContent stringContent = new(jsonString, Encoding.UTF8, "application/json");

            using HttpResponseMessage response = await _httpClient.PostAsync(webhookUrl, stringContent).ConfigureAwait(false);
            string responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static async Task SendMessageWithFallback(string webhookType, object embedData)
    {
        var webhookUrl = GetWebhookUrl(webhookType);
        
        bool directSuccess = await SendDirectToDiscord(webhookUrl, embedData);
        
        if (!directSuccess)
        {
            Console.WriteLine($"[AdminPlus] Direct webhook failed, trying Cloudflare Worker...");
            await SendViaCloudflareWorker(webhookType, embedData);
        }
    }

    private static async Task SendViaCloudflareWorker(string webhookType, object embedData)
    {
        if (string.IsNullOrWhiteSpace(CloudflareWorkerUrl))
        {
            Console.WriteLine($"[AdminPlus] Cloudflare Worker URL not configured");
            return;
        }

        try
        {
            var payload = new
            {
                webhookUrl = GetWebhookUrl(webhookType),
                webhookType = webhookType,
                embedData = embedData
            };

            string jsonString = JsonSerializer.Serialize(payload, JsonOptions);
            Console.WriteLine($"[AdminPlus] Sending payload to Cloudflare Worker: {jsonString}");
            using StringContent stringContent = new(jsonString, Encoding.UTF8, "application/json");

            using HttpResponseMessage response = await _httpClient.PostAsync(CloudflareWorkerUrl, stringContent).ConfigureAwait(false);
            string responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[AdminPlus] Cloudflare Worker HTTP error: {response.StatusCode} - {responseContent}");
            }
            else
            {
                Console.WriteLine($"[AdminPlus] Message sent via Cloudflare Worker successfully");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminPlus] Cloudflare Worker error: {ex.Message}");
        }
    }

    public static void RegisterDiscordCommands(AdminPlus plugin)
    {
        plugin.AddCommand("css_discord_status", "Send server status to Discord", (player, commandInfo) => OnDiscordStatusCommand(plugin, player, commandInfo));
    }

    [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public static void OnDiscordStatusCommand(AdminPlus plugin, CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player != null && !player.IsValid)
            return;

        try
        {
            var playerCount = Utilities.GetPlayers().Count(p => p.IsValid && !p.IsBot);
            var maxPlayers = Server.MaxPlayers;
            var currentMap = Server.MapName;
            
            var serverIp = "0.0.0.0:27015";
            try
            {
                var ipConVar = ConVar.Find("ip");
                if (ipConVar != null && !string.IsNullOrEmpty(ipConVar.StringValue))
                {
                    var ip = ipConVar.StringValue;
                    if (ip != "0.0.0.0")
                    {
                        serverIp = $"{ip}:27015";
                        Console.WriteLine($"[AdminPlus] IP found via ConVar: {serverIp}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AdminPlus] IP detection error: {ex.Message}");
            }
            
            var uptime = GetServerUptime();
            var timeLeft = GetMapTimeLeft();

            _ = SendServerStatus(plugin, "Online", playerCount, maxPlayers, currentMap, uptime, serverIp, timeLeft);

            var message = "Server status sent to Discord!";
            if (player != null)
            {
                player.PrintToChat($" {ChatColors.Green}[AdminPlus] {message}");
            }
            else
            {
                Console.WriteLine($"[AdminPlus] {message}");
            }
        }
        catch (Exception)
        {
        }
    }

    private static string GetServerUptime()
    {
        try
        {
            var uptime = DateTime.Now - Process.GetCurrentProcess().StartTime;
            return $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m";
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string GetMapTimeLeft()
    {
        try
        {
            var mpTimelimit = ConVar.Find("mp_timelimit");
            if (mpTimelimit != null)
            {
                var timeLimit = mpTimelimit.GetPrimitiveValue<int>();
                if (timeLimit > 0)
                {
                    return $"{timeLimit} minutes";
                }
            }
            return "Unlimited";
        }
        catch
        {
            return "Unknown";
        }
    }
}

public class DiscordConfig
{
    public DiscordWebhooks? DiscordWebhooks { get; set; }
}

public class DiscordWebhooks
{
    public string? BanWebhook { get; set; }
    public string? AdminActionsWebhook { get; set; }
    public string? CommunicationWebhook { get; set; }
    public string? ServerStatusWebhook { get; set; }
    public string? ConnectionLogsWebhook { get; set; }
    public string? ChatLogsWebhook { get; set; }
    public string? ReportAndCalladminWebhook { get; set; }
    public string? ReportAndCalladminWebhookMentionUserId { get; set; }
}
