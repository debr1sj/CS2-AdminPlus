using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.ValveConstants.Protobuf;
using AdminPlus;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MenuManager;

namespace AdminPlus;


[MinimumApiVersion(78)]
public partial class AdminPlus : BasePlugin
{
    public override string ModuleName => "AdminPlus";
    public override string ModuleVersion => "1.0.2";
    public override string ModuleAuthor => "debr1sj";

    internal static string BannedUserPath = string.Empty;
    internal static string BannedIpPath = string.Empty;
    internal static Dictionary<string, (long expiry, string line, string nick, string ip)> SteamBans = new();
    internal static Dictionary<string, (long expiry, string line, string nick)> IpBans = new();
    internal static object _lock = new();
    internal static Dictionary<ulong, (string name, string ip)> DisconnectedPlayers = new();
    private const int MAX_DISCONNECTED_PLAYERS = 50;
    
    private static readonly HashSet<ulong> _loggedPlayers = new();
    

    private Timer? cleanupTimer;
    internal static AdminPlus? _instance;
    private bool _communicationInitialized = false;
    
    internal static IMenuApi? MenuApi;
    private static readonly PluginCapability<IMenuApi> MenuCapability = new("menu:nfcore");
    
    
    public static string GetPrefix()
    {
        return _instance?.Localizer?["ap_prefix"] ?? "";
    }
    
    public static void LogAction(string action)
    {
        try
        {
            var now = DateTime.Now;
            var timestamp = now.ToString("yyyy-MM-dd HH:mm:ss");
            var logEntry = $"[{timestamp}] {action}";
            
            var dailyLogPath = GetDailyLogPath(now);
            
            File.AppendAllText(dailyLogPath, logEntry + Environment.NewLine);
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminPlus] ERROR: Failed to log action to daily log file");
            Console.WriteLine($"[AdminPlus] ERROR: Log error: {ex.Message}");
            Console.WriteLine($"[AdminPlus] ERROR: Stack trace: {ex.StackTrace}");
        }
    }
    
    private static string GetDailyLogPath(DateTime date)
    {
        var logDirectory = Path.Combine(Server.GameDirectory, "csgo/addons/counterstrikesharp/logs");
        
        if (!Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
            Console.WriteLine($"[AdminPlus] Created log directory: {logDirectory}");
        }
        
        var fileName = $"log-AdminPlus-{date:dd-MM-yyyy}.log";
        return Path.Combine(logDirectory, fileName);
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
        
        Discord.LoadConfig();
        Discord.StartStatusTimer(this);

        RegisterCommunicationCommands();
        RegisterMenuCommands();
        RegisterBanCommands();
        RegisterAdminManageCommands();
        Discord.RegisterDiscordCommands(this);
        RegisterAdminCommands();

        RegisterFunCommands();

        RegisterChatCommands();

        RegisterVoteCommands();

        RegisterHelpCommands();
        
        RegisterReportCommands();

        InitializeReservationSystem();
    }

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        base.OnAllPluginsLoaded(hotReload);
        
        try
        {
            MenuApi = MenuCapability.Get();
            if (MenuApi != null)
            {
            }
            else
            {
                Console.WriteLine("[AdminPlus] MenuManager API not found, using fallback CenterHtmlMenu");
            }
        }
        catch (KeyNotFoundException)
        {
            Console.WriteLine("[AdminPlus] MenuManager plugin not found, using fallback CenterHtmlMenu");
            MenuApi = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminPlus] Error loading MenuManager API: {ex.Message}, using fallback CenterHtmlMenu");
            MenuApi = null;
        }
        AddCommand("admins", Localizer["Admins.Usage"], CmdAdmins);
        AddCommand("css_admins", "List online admins in console", CmdAdmins);
        
        RegisterListener<Listeners.OnClientAuthorized>((slot, id) => EnforceBan(slot));


        RegisterListener<Listeners.OnClientDisconnect>((slot) =>
        {
            var player = Utilities.GetPlayerFromSlot(slot);
            if (player != null && player.IsValid && !player.IsBot)
            {
                if (DisconnectedPlayers.Count >= MAX_DISCONNECTED_PLAYERS)
                    DisconnectedPlayers.Remove(DisconnectedPlayers.Keys.First());

                DisconnectedPlayers[player.SteamID] = (player.PlayerName, player.IpAddress ?? "-");
                OnPlayerDisconnectVote(player);
            }
        });

        RegisterEventHandler<EventPlayerChat>((@event, info) =>
        {
            try
            {
                var player = Utilities.GetPlayerFromUserid(@event.Userid);
                if (player == null || !player.IsValid || player.IsBot)
                    return HookResult.Continue;

                var message = @event.Text;
                if (string.IsNullOrWhiteSpace(message))
                    return HookResult.Continue;


                if (_selectedReportTarget != null && HandleCustomReportReason(player, message))
                    return HookResult.Handled;

                string channelType;
                string cleanMessage;

                if (@event.Teamonly)
                {
                    channelType = "Team Chat";
                    cleanMessage = message; 
                }
                else
                {
                    channelType = "All Chat";
                    cleanMessage = message;
                }

                _ = Discord.SendChatLog(player.PlayerName, player.SteamID.ToString(), cleanMessage, channelType, this);

                return HookResult.Continue;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AdminPlus] Chat event error: {ex.Message}");
                return HookResult.Continue;
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
            
            if (player != null && player.IsValid && !player.IsBot)
            {
                var steamId = player.SteamID;
                if (!_loggedPlayers.Contains(steamId))
                {
                    _loggedPlayers.Add(steamId);
                    _ = Discord.SendConnectionLog(player.PlayerName, player.SteamID.ToString(), player.IpAddress ?? "Unknown", "Connect", _instance!);
                }
            }
            
            return HookResult.Continue;
        });


        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            try
            {
                var player = @event.Userid;
                if (player == null || !player.IsValid || player.IsBot)
                    return HookResult.Continue;

                _ = Discord.SendConnectionLog(player.PlayerName, player.SteamID.ToString(), player.IpAddress ?? "Unknown", "Disconnect", this);
                
                _loggedPlayers.Remove(player.SteamID);

                return HookResult.Continue;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AdminPlus] Player disconnect event error: {ex.Message}");
                return HookResult.Continue;
            }
        });

    }



    public override void Unload(bool hotReload)
    {
        try
        {
            cleanupTimer?.Kill();
            cleanupTimer = null;
            
            Discord.StopStatusTimer();
            
            CleanupCommunication();
            CleanupAllFunTimers();
            
            Discord.Dispose();
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
        
        string ipWithoutPort = ip;
        if (ip.Contains(":"))
        {
            ipWithoutPort = ip.Split(':')[0];
        }

        lock (_lock)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (IpBans.TryGetValue(ipWithoutPort, out var ipBan) && (ipBan.expiry == 0 || now < ipBan.expiry))
            {
                player.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_STEAM_BANNED);
                Console.WriteLine($"[AdminPlus] Blocked banned IP: {ipWithoutPort} (Player: {player.PlayerName})");
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
                    Console.WriteLine($"[AdminPlus] Loading SteamID bans from: {BannedUserPath}");
                    foreach (var line in File.ReadAllLines(BannedUserPath))
                    {
                        if (TryParseSteamBanLine(line, out var key, out var expiry, out var nick, out var ip))
                        {
                            SteamBans[key] = (expiry, line, nick, ip);
                            steamCount++;
                        }
                    }
                }
                else
                {
                    CreateEmptyBanFile(BannedUserPath, "SteamID");
                }

                if (File.Exists(BannedIpPath))
                {
                    Console.WriteLine($"[AdminPlus] Loading IP bans from: {BannedIpPath}");
                    foreach (var line in File.ReadAllLines(BannedIpPath))
                    {
                        if (TryParseIpBanLine(line, out var key, out var expiry, out var nick))
                        {
                            IpBans[key] = (expiry, line, nick);
                            ipCount++;
                        }
                    }
                }
                else
                {
                    CreateEmptyBanFile(BannedIpPath, "IP");
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AdminPlus] ERROR: Failed to load ban files");
                Console.WriteLine($"[AdminPlus] ERROR: SteamID bans path: {BannedUserPath}");
                Console.WriteLine($"[AdminPlus] ERROR: IP bans path: {BannedIpPath}");
                Console.WriteLine($"[AdminPlus] ERROR: {ex.Message}");
                Console.WriteLine($"[AdminPlus] ERROR: Stack trace: {ex.StackTrace}");
            }
        }
    }

    private void CreateEmptyBanFile(string filePath, string banType)
    {
        try
        {
            if (File.Exists(filePath))
            {
                Console.WriteLine($"[AdminPlus] {banType.ToLower()} ban file already exists, skipping creation.");
                return;
            }

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string header;
            if (banType == "SteamID")
            {
                header = $"// {banType} ban list - AdminPlus\n" +
                        $"// Format: banid \"STEAM_ID\" \"PLAYER_NAME\" ip:IP_ADDRESS expiry:EXPIRY_TIME // REASON\n" +
                        $"// Example: banid \"STEAM_1:0:123456789\" \"PlayerName\" ip:192.168.1.1 expiry:0 // Cheating\n\n";
            }
            else
            {
                header = $"// {banType} ban list - AdminPlus\n" +
                        $"// Format: addip \"IP_ADDRESS\" expiry:0 // REASON\n" +
                        $"// Example: addip \"192.168.1.1\" expiry:0 // Cheating\n\n";
            }

            File.WriteAllText(filePath, header);
            Console.WriteLine($"[AdminPlus] Created empty {banType.ToLower()} ban file: {filePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminPlus] Failed to create {banType.ToLower()} ban file: {ex.Message}");
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
                    caller.Print(Localizer["Admins.None"]);
                    return;
                }

                foreach (var a in onlineAdmins.OrderByDescending(x => x.imm))
                    caller.Print(Localizer["Admins.Item", a.name, a.imm]);
                caller.Print(Localizer["Admins.Total", onlineAdmins.Count]);
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
    
    internal static IMenu? CreateMenu(string title, Action<CCSPlayerController>? backAction = null)
    {
        if (MenuApi == null)
        {
            Console.WriteLine("[AdminPlus] MenuManager API not available, falling back to CenterHtmlMenu");
            return _instance != null ? new CenterHtmlMenu(title, _instance) : null;
        }

        return MenuApi?.GetMenu(title);
    }

    internal static IMenu? CreateMenuForcedType(string title, MenuType menuType, Action<CCSPlayerController>? backAction = null)
    {
        if (MenuApi == null)
        {
            Console.WriteLine("[AdminPlus] MenuManager API not available, falling back to CenterHtmlMenu");
            return _instance != null ? new CenterHtmlMenu(title, _instance) : null;
        }

        return MenuApi?.GetMenuForcetype(title, menuType);
    }

    internal static void OpenMenu(CCSPlayerController player, IMenu? menu)
    {
        if (menu == null) return;
        menu.Open(player);
    }

    internal static void CloseMenu(CCSPlayerController player)
    {
        MenuApi?.CloseMenu(player);
    }


    private string GetServerUptime()
    {
        try
        {
            var uptime = DateTime.Now - DateTime.FromFileTime(Environment.TickCount64);
            return $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m";
        }
        catch
        {
            return "Unknown";
        }
    }

    private string GetMapTimeLeft()
    {
        try
        {
            return "29:42";
        }
        catch
        {
            return "Unknown";
        }
    }

    private void RegisterReportCommands()
    {
        AddCommand("css_report", "Report a player", OnReportCommand);
        AddCommand("css_calladmin", "Call an admin", OnReportCommand);
    }


    [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_ONLY)]
    private void OnReportCommand(CCSPlayerController? caller, CommandInfo? commandInfo)
    {
        if (caller == null || !caller.IsValid) return;

        string playerId = caller.SteamID.ToString();
        
        if (!CheckReportCooldown(playerId))
        {
            caller.PrintToChat($"{Localizer["ap_prefix"]} You must wait 2 minutes between reports.");
            return;
        }

        var players = Utilities.GetPlayers().Where(x => x.IsValid && !x.IsBot && x.Connected == PlayerConnectedState.PlayerConnected);
        
        var reportMenu = CreateMenu("Select player to report:");
        if (reportMenu == null) return;
        
        foreach (var player in players)
        {
            if (player.Team == CsTeam.None) continue;

            var playerName = SanitizeName(player.PlayerName);
            var menuOptionData = new ChatMenuOptionData($"{playerName} [#{player.Index}]", () => HandleReportMenuSimple(caller, player));
            reportMenu.AddMenuOption(menuOptionData.Name, (controller, option) => { menuOptionData.Action.Invoke(); }, menuOptionData.Disabled);
        }
        
        if (reportMenu != null)
        {
            reportMenu.ExitButton = true;
            OpenMenu(caller, reportMenu);
        }
        
        _lastReportTime[caller.SteamID] = DateTime.Now;
    }


    private readonly Dictionary<ulong, DateTime> _lastReportTime = new();
    private readonly Dictionary<string, DateTime> _playerReportingCooldowns = new(); // reporter_steamid:target_steamid -> DateTime

    private bool CheckReportCooldown(string playerId)
    {
        if (_lastReportTime.TryGetValue(ulong.Parse(playerId), out DateTime lastReportTime))
        {
            var secondsSinceLastReport = (DateTime.Now - lastReportTime).TotalSeconds;
            return secondsSinceLastReport >= 120; // 2 minutes cooldown
        }
        return true;
    }

    private bool CheckPlayerToPlayerReportCooldown(CCSPlayerController reporter, CCSPlayerController targetPlayer)
    {
        string key = $"{reporter.SteamID}_{targetPlayer.SteamID}";
        if (_playerReportingCooldowns.TryGetValue(key, out DateTime lastReport))
        {
            var timeSinceLastReport = DateTime.Now - lastReport;
            return timeSinceLastReport.TotalMinutes >= 3.0; // 3 dakika cooldown
        }
        return true;
    }

    private void SetPlayerToPlayerReportCooldown(CCSPlayerController reporter, CCSPlayerController targetPlayer)
    {
        string key = $"{reporter.SteamID}_{targetPlayer.SteamID}";
        _playerReportingCooldowns[key] = DateTime.Now;
    }

    private void HandleReportMenuSimple(CCSPlayerController controller, CCSPlayerController targetPlayer)
    {
        if (targetPlayer == null)
        {
            controller.PrintToChat($"{Localizer["ap_prefix"]} Player not found.");
            return;
        }

        if (!CheckPlayerToPlayerReportCooldown(controller, targetPlayer))
        {
            controller.PrintToChat($"{Localizer["ap_prefix"]} {Localizer["Report.CooldownWarning"]}");
            return;
        }

        var reasons = new[]
        {
            "Hacking/Cheating",
            "Toxic behavior",
            "Griefing", 
            "Spamming",
            "Other"
        };

        var reasonMenu = CreateMenu("Select report reason:");
        if (reasonMenu == null) return;

        var customReasonData = new ChatMenuOptionData("Custom Reason", () => HandleCustomReasonSimple(controller, targetPlayer));
        reasonMenu.AddMenuOption(customReasonData.Name, (ctrl, opt) => { customReasonData.Action.Invoke(); }, customReasonData.Disabled);
        
        foreach (var reason in reasons)
        {
            var reasonData = new ChatMenuOptionData(reason, () => ProcessReport(controller, targetPlayer, reason));
            reasonMenu.AddMenuOption(reasonData.Name, (ctrl, opt) => { reasonData.Action.Invoke(); }, reasonData.Disabled);
        }
        
        if (reasonMenu != null)
        {
            reasonMenu.ExitButton = true;
            _activeReportMenu = reasonMenu;
            OpenMenu(controller, reasonMenu);
        }
    }

    private void HandleCustomReasonSimple(CCSPlayerController controller, CCSPlayerController targetPlayer)
    {
        controller.PrintToChat($"{Localizer["ap_prefix"]} Please type your custom reason in chat. Type 'cancel' to cancel.");
        
        _selectedReportTarget = targetPlayer;
        
        AddTimer(20.0f, () =>
        {
            if (_selectedReportTarget != null)
            {
                controller.PrintToChat($"{Localizer["ap_prefix"]} Report timed out.");
                _selectedReportTarget = null;
            }
        });
    }

    private CCSPlayerController? _selectedReportTarget = null;
    private IMenu? _activeReportMenu = null;


    private void ProcessReport(CCSPlayerController reporter, CCSPlayerController reported, string reason)
    {
        if (!CheckPlayerToPlayerReportCooldown(reporter, reported))
        {
            reporter.PrintToChat($"{Localizer["ap_prefix"]} {Localizer["Report.CooldownWarning"]}");
            _selectedReportTarget = null;
            _activeReportMenu = null;
            return;
        }

        try
        {
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
            catch { }

            _ = Discord.SendPlayerReport(
                reporter.PlayerName, 
                reporter.SteamID.ToString(), 
                reported.PlayerName, 
                reported.SteamID.ToString(), 
                reason,
                serverIp,
                this
            );

            SetPlayerToPlayerReportCooldown(reporter, reported);
            
            _selectedReportTarget = null;
            _activeReportMenu = null;
            
            var message = Localizer["Report.SentSuccessfullyFor"].ToString().Replace("{player}", reported.PlayerName);
            reporter.PrintToChat($"{Localizer["ap_prefix"]} {message}");
        }
        catch (Exception ex)
        {
            reporter.PrintToChat($"{Localizer["ap_prefix"]} {Localizer["Report.FailedToSend"]}");
            Console.WriteLine($"[AdminPlus] Report error: {ex.Message}");
        }
    }


    private bool HandleCustomReportReason(CCSPlayerController player, string message)
    {
        if (_selectedReportTarget == null) return false;

        if (!CheckPlayerToPlayerReportCooldown(player, _selectedReportTarget))
        {
            player.PrintToChat($"{Localizer["ap_prefix"]} {Localizer["Report.CooldownWarning"]}");
            _selectedReportTarget = null;
            return true;
        }

        if (message.ToLower().Contains("cancel"))
        {
            player.PrintToChat($"{Localizer["ap_prefix"]} Report cancelled.");
            _selectedReportTarget = null;
            return true;
        }

        ProcessReport(player, _selectedReportTarget, message.Trim());
        _selectedReportTarget = null;
        return true;
    }
}

public static class PlayerExtensions
{
    public static void Print(this CCSPlayerController controller, string message = "")
    {
        var prefix = AdminPlus._instance?.Localizer?["ap_prefix"] ?? "";
        if (!string.IsNullOrEmpty(prefix))
            controller.PrintToChat($"{prefix} {message}");
        else
            controller.PrintToChat(message);
    }
    
        public static void PrintToAll(string message)
        {
            try
            {
                var prefix = AdminPlus._instance?.Localizer?["ap_prefix"] ?? "";
                string fullMessage = !string.IsNullOrEmpty(prefix) ? $"{prefix} {message}" : message;
                
                if (Server.MaxPlayers <= 0)
                {
                    Console.WriteLine($"[AdminPlus] {fullMessage}");
                    return;
                }
                
                foreach (var player in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot))
                {
                    player.PrintToChat(fullMessage);
                }
            }
            catch (Exception ex)
            {
            Console.WriteLine($"[AdminPlus] PrintToAll Error: {ex.Message}");
            Console.WriteLine($"[AdminPlus] {message}");
        }
    }
    
}
