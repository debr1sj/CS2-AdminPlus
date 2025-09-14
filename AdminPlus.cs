using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.ValveConstants.Protobuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;

namespace AdminPlus;

[MinimumApiVersion(78)]
public partial class AdminPlus : BasePlugin
{
    public override string ModuleName => "AdminPlus";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "AdminPlus";

    internal static string BannedUserPath = string.Empty;
    internal static string BannedIpPath = string.Empty;
    internal static Dictionary<string, (long expiry, string line, string nick, string ip)> SteamBans = new();
    internal static Dictionary<string, (long expiry, string line, string nick)> IpBans = new();
    internal static object _lock = new();
    internal static Dictionary<ulong, (string name, string ip)> DisconnectedPlayers = new();

    private Timer? cleanupTimer;
    private static AdminPlus? _instance;
    private bool _communicationInitialized = false;
    
    public static string GetPrefix()
    {
        return "{green}[AdminPlus]{default}";
    }

    public AdminPlus()
    {
        _instance = this;
    }

    public override void Load(bool hotReload)
    {
        base.Load(hotReload);
        _instance = this;

        BannedUserPath = Path.Combine(Server.GameDirectory, "csgo/cfg/banned_user.cfg");
        BannedIpPath = Path.Combine(Server.GameDirectory, "csgo/cfg/banned_ip.cfg");

        LoadBans();
        StartCleanup();

        RegisterCommunicationCommands();
        RegisterMenuCommands();
        RegisterBanCommands();
        RegisterAdminManageCommands();
        RegisterAdminCommands();

        RegisterFunCommands();

        RegisterChatCommands();

        RegisterVoteCommands();

        RegisterHelpCommands();

        InitializeReservationSystem();
        AddCommand("admins", Localizer["Admins.Usage"], CmdAdmins);
        AddCommand("css_admins", "List online admins in console", CmdAdmins);
        RegisterListener<Listeners.OnClientAuthorized>((slot, id) => EnforceBan(slot));


        RegisterListener<Listeners.OnClientDisconnect>((slot) =>
        {
            var player = Utilities.GetPlayerFromSlot(slot);
            if (player != null && player.IsValid && !player.IsBot)
            {
                if (DisconnectedPlayers.Count >= 50)
                    DisconnectedPlayers.Remove(DisconnectedPlayers.Keys.First());

                DisconnectedPlayers[player.SteamID] = (player.PlayerName, player.IpAddress ?? "-");
                OnPlayerDisconnectVote(player);
            }
        });

        RegisterListener<Listeners.OnMapStart>((mapName) =>
        {
            DisconnectedPlayers.Clear();
            if (!_communicationInitialized)
            {
                InitializeCommunication();
                _communicationInitialized = true;
            }
        });

        RegisterEventHandler<EventPlayerDeath>((@event, info) =>
        {
            var victim = @event.Userid; // CCSPlayerController?
            victim?.CopyLastCoord();
            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerSpawn>((@event, info) =>
        {
            var player = @event.Userid; // CCSPlayerController?
            player?.RemoveLastCoord();
            return HookResult.Continue;
        });

        Console.WriteLine("[AdminPlus] Plugin loaded successfully with console support!");
    }

    public override void Unload(bool hotReload)
    {
        try
        {
            cleanupTimer?.Kill();
            cleanupTimer = null;
            
            CleanupCommunication();
            CleanupAllFunTimers();
            CleanupBanSystem();
            CleanupCommands();
            CleanupMenu();
            CleanupVoteSystem();
            CleanupHelp();
            CleanupChat();
            LastCoordExtensions.CleanupLastCoords();
            CleanupReservationSystem();
            
            lock (_lock)
            {
                SteamBans.Clear();
                IpBans.Clear();
                DisconnectedPlayers.Clear();
            }
            
            _instance = null;
            _communicationInitialized = false;
            
            Console.WriteLine("Plugin unloaded and all memory cleaned successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during plugin unload: {ex.Message}");
        }
    }

    private void EnforceBan(int playerSlot)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player == null || !player.IsValid || player.IsBot) return;

        var steamId = player.SteamID.ToString();
        var ip = player.IpAddress ?? "";

        lock (_lock)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (IpBans.TryGetValue(ip, out var ipBan) && (ipBan.expiry == 0 || now < ipBan.expiry))
            {
                player.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_STEAM_BANNED);
                Console.WriteLine($"[AdminPlus] Blocked banned IP: {ip} (Player: {player.PlayerName})");
                return;
            }

            if (SteamBans.TryGetValue(steamId, out var steamBan) && (steamBan.expiry == 0 || now < steamBan.expiry))
            {
                player.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_STEAM_BANNED);
                Console.WriteLine($"[AdminPlus] Blocked banned SteamID: {steamId} (Player: {player.PlayerName})");
            }
        }
    }

    internal void LoadBans()
    {
        lock (_lock)
        {
            SteamBans.Clear();
            IpBans.Clear();

            try
            {
                int steamCount = 0, ipCount = 0;

                if (File.Exists(BannedUserPath))
                {
                    foreach (var line in File.ReadAllLines(BannedUserPath))
                    {
                        if (TryParseSteamBanLine(line, out var key, out var expiry, out var nick, out var ip))
                        {
                            SteamBans[key] = (expiry, line, nick, ip);
                            steamCount++;
                        }
                    }
                }

                if (File.Exists(BannedIpPath))
                {
                    foreach (var line in File.ReadAllLines(BannedIpPath))
                    {
                        if (TryParseIpBanLine(line, out var key, out var expiry, out var nick))
                        {
                            IpBans[key] = (expiry, line, nick);
                            ipCount++;
                        }
                    }
                }

                Console.WriteLine($"[AdminPlus] Loaded {steamCount} Steam bans and {ipCount} IP bans");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AdminPlus] Failed to load ban files: {ex.Message}");
            }
        }
    }

    private bool TryParseSteamBanLine(string line, out string key, out long expiry, out string nick, out string ip)
    {
        key = ""; expiry = 0; nick = "Unknown"; ip = "-";

        try
        {
            var match = Regex.Match(line, @"^banid\s+""([^""]+)""\s+""([^""]+)""\s+ip:([^\s]+)\s+expiry:(\d+)");
            if (match.Success)
            {
                key = match.Groups[1].Value;
                nick = match.Groups[2].Value;
                ip = match.Groups[3].Value;
                expiry = long.Parse(match.Groups[4].Value);
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminPlus] SteamBan parse error: {ex.Message}");
        }
        return false;
    }

    private bool TryParseIpBanLine(string line, out string key, out long expiry, out string nick)
    {
        key = ""; expiry = 0; nick = "Unknown";

        try
        {
            var match = Regex.Match(line, @"^addip\s+\""(.*?)\""\s+expiry:(\d+)(?:\s*//\s*(.+))?");
            if (match.Success)
            {
                key = match.Groups[1].Value;
                expiry = long.Parse(match.Groups[2].Value);
                nick = match.Groups[3].Success ? match.Groups[3].Value : "Unknown";
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminPlus] IpBan parse error: {ex.Message}");
        }
        return false;
    }

    private void CmdAdmins(CCSPlayerController? caller, CommandInfo info)
    {
        try
        {
            JsonObject root;
            bool ok = false;
            try { ok = ReadAdminsFile(out root); }
            catch { root = new JsonObject(); }

            var players = Utilities.GetPlayers()
                .Where(p => p != null && p.IsValid && !p.IsBot)
                .ToList();

            var onlineAdmins = new List<(string name, int imm)>();

            if (ok && root.Count > 0)
            {
                foreach (var p in players)
                {
                    var key = p.SteamID.ToString();
                    if (root.ContainsKey(key) && root[key] is JsonObject obj)
                    {
                        int imm = obj["immunity"]?.GetValue<int?>() ?? 0;
                        if (imm > 0)
                        {
                            string name = SanitizeName(p.PlayerName);
                            onlineAdmins.Add((name, imm));
                        }
                    }
                }
            }

            if (caller != null && caller.IsValid)
            {
                if (onlineAdmins.Count == 0)
                {
                    caller.PrintToChat(Localizer["Admins.None"]);
                    return;
                }

                foreach (var a in onlineAdmins.OrderByDescending(x => x.imm))
                    caller.PrintToChat(Localizer["Admins.Item", a.name, a.imm]);
                caller.PrintToChat(Localizer["Admins.Total", onlineAdmins.Count]);
            }
            else
            {
                if (onlineAdmins.Count == 0)
                {
                    Console.WriteLine("[AdminPlus] No online admins currently.");
                    return;
                }
                
                foreach (var a in onlineAdmins.OrderByDescending(x => x.imm))
                    Console.WriteLine($"[AdminPlus] Online Admin: {a.name} [{a.imm}]");
                Console.WriteLine($"[AdminPlus] Total {onlineAdmins.Count} admins online!");
            }
        }
        catch (Exception ex)
        {
            if (caller != null && caller.IsValid)
                caller.PrintToChat($"{{green}}[AdminPlus]{{default}} Failed to get admin list: {ex.Message}");
            else
                Console.WriteLine($"[AdminPlus] Admins command error: {ex.Message}");
        }
    }

    private void StartCleanup()
    {
        cleanupTimer = AddTimer(60f, () =>
        {
            lock (_lock)
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var steamToRemove = SteamBans.Where(kv => kv.Value.expiry != 0 && kv.Value.expiry <= now).Select(kv => kv.Key).ToList();
                var ipToRemove = IpBans.Where(kv => kv.Value.expiry != 0 && kv.Value.expiry <= now).Select(kv => kv.Key).ToList();

                steamToRemove.ForEach(key => SteamBans.Remove(key));
                ipToRemove.ForEach(key => IpBans.Remove(key));

                if (steamToRemove.Count > 0 || ipToRemove.Count > 0)
                {
                    try
                    {
                        File.WriteAllLines(BannedUserPath, SteamBans.Values.Select(v => v.line));
                        File.WriteAllLines(BannedIpPath, IpBans.Values.Select(v => v.line));
                        Console.WriteLine($"[AdminPlus] Cleaned up {steamToRemove.Count} expired Steam bans and {ipToRemove.Count} expired IP bans");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[AdminPlus] Cleanup write error: {ex.Message}");
                    }
                }
            }
        }, TimerFlags.REPEAT);
    }

    internal static void LogAction(string message)
    {
        try
        {
            var logPath = Path.Combine(Server.GameDirectory, "csgo/addons/counterstrikesharp/logs/adminplus.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminPlus] Log write error: {ex.Message}");
        }
    }

    internal string GetExecutorName(CCSPlayerController? caller) => caller?.PlayerName ?? "Console";

    internal List<CCSPlayerController> GetPlayersFromTeamInput(string input)
    {
        try
        {
            var players = Utilities.GetPlayers();
            if (players == null)
                return new List<CCSPlayerController>();

            return input.ToLower() switch
            {
                "@t" or "@terrorist" or "@terorist" => players.Where(p => p.IsValid && p.TeamNum == (int)CsTeam.Terrorist).ToList(),
                "@ct" or "@counter" or "@counterterrorist" => players.Where(p => p.IsValid && p.TeamNum == (int)CsTeam.CounterTerrorist).ToList(),
                "@spec" or "@spectator" => players.Where(p => p.IsValid && p.TeamNum == (int)CsTeam.Spectator).ToList(),
                "@all" => players.Where(p => p.IsValid && !p.IsBot).ToList(),
                _ => new List<CCSPlayerController>()
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminPlus] GetPlayersFromTeamInput error: {ex.Message}");
            return new List<CCSPlayerController>();
        }
    }

    internal string GetTeamName(string teamInput)
    {
        return teamInput.ToLower() switch
        {
            "@t" or "@terrorist" or "@terorist" => "Terrorist",
            "@ct" or "@counter" or "@counterterrorist" => "Counter-Terrorist",
            "@spec" or "@spectator" => "Spectator",
            "@all" => "All Players",
            _ => teamInput
        };
    }

    internal string GetTeamNameFromEnum(CsTeam team) => team switch
    {
        CsTeam.Terrorist => "Terrorist",
        CsTeam.CounterTerrorist => "Counter-Terrorist",
        CsTeam.Spectator => "Spectator",
        _ => "All Players"
    };

    private static string Localize(string key, params object[] args)
    {
        return _instance?.Localizer[key, args] ?? key;
    }
    
    private static string GetPrefixedMessage(string key, params object[] args)
    {
        var message = _instance?.Localizer[key, args] ?? key;
        var prefix = _instance?.Localizer["Prefix"] ?? "{green}[AdminPlus]{default}";
        return message.Replace("{Prefix}", prefix);
    }
    
    private static void PrintPrefixedMessage(CCSPlayerController? player, string key, params object[] args)
    {
        if (player?.IsValid == true)
        {
            var message = GetPrefixedMessage(key, args);
            player.PrintToChat(message);
        }
    }
    
    private static void PrintPrefixedConsole(string key, params object[] args)
    {
        var message = GetPrefixedMessage(key, args);
        Console.WriteLine(message);
    }
    

}
