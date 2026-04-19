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
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MenuManager;

namespace AdminPlus;


[MinimumApiVersion(78)]
public partial class AdminPlus : BasePlugin
{
    public override string ModuleName => "AdminPlus";
    public override string ModuleVersion => "1.0.5";
    public override string ModuleAuthor => "debr1sj";

    internal static string BannedUserPath = string.Empty;
    internal static string BannedIpPath = string.Empty;
    internal static Dictionary<string, (long expiry, string line, string nick, string ip)> SteamBans = new();
    internal static Dictionary<string, (long expiry, string line, string nick)> IpBans = new();
    internal static object _lock = new();
    internal static Dictionary<ulong, (string name, string ip)> DisconnectedPlayers = new();
    private const int MAX_DISCONNECTED_PLAYERS = 50;
    
    private static readonly HashSet<ulong> _loggedPlayers = new();
    private DateTime _lastBanCacheRefreshUtc = DateTime.MinValue;
    private DateTime _lastUserBanWriteUtc = DateTime.MinValue;
    private DateTime _lastIpBanWriteUtc = DateTime.MinValue;
    

    private Timer? cleanupTimer;
    internal static AdminPlus? _instance;
    private bool _communicationInitialized = false;
    
    internal static IMenuApi? MenuApi;
    private static readonly PluginCapability<IMenuApi> MenuCapability = new("menu:nfcore");
    
    
    public static string GetPrefix()
    {
        return _instance?.Localizer?["Prefix"] ?? "";
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

        EnsureAdminConfigFiles();
        EnsurePluginDataFiles();

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

    private void EnsurePluginDataFiles()
    {
        try
        {
            Directory.CreateDirectory(ModuleDirectory);

            var communicationDataPath = Path.Combine(ModuleDirectory, "communication_data.json");
            if (!File.Exists(communicationDataPath))
            {
                File.WriteAllText(communicationDataPath, "[]" + Environment.NewLine);
                Console.WriteLine($"[AdminPlus] Created missing communication data file: {communicationDataPath}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminPlus] ERROR: Failed to bootstrap plugin data files: {ex.Message}");
        }
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
        AddCommand("version", "Print AdminPlus name and version to console", CmdPluginVersion);
        AddCommand("css_version", "Print AdminPlus name and version to console", CmdPluginVersion);
        
        RegisterListener<Listeners.OnClientAuthorized>((slot, id) =>
        {
            EnforceBan(slot);
            ScheduleBanRechecks(slot);
        });


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
            var victim = @event.Userid;
            victim?.CopyLastCoord();
            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerSpawn>((@event, info) =>
        {
            var player = @event.Userid;
            player?.RemoveLastCoord();

            if (player != null && player.IsValid)
                EnforceBan(player.Slot);
            
            if (player != null && player.IsValid && !player.IsBot)
            {
                var steamId = player.SteamID;
                if (!_loggedPlayers.Contains(steamId))
                {
                    _loggedPlayers.Add(steamId);
                    _ = Discord.SendConnectionLog(player.PlayerName, player.SteamID.ToString(), "Connect", _instance!);
                }
            }
            
            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerConnectFull>((@event, info) =>
        {
            var player = @event.Userid;
            if (player != null && player.IsValid)
            {
                EnforceBan(player.Slot);
                ScheduleBanRechecks(player.Slot);
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

                _ = Discord.SendConnectionLog(player.PlayerName, player.SteamID.ToString(), "Disconnect", this);
                
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
        TryRefreshBanCacheIfChanged();

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
                NotifyBlockedBan(player, "IP", ipWithoutPort);
                return;
            }

            if (SteamBans.TryGetValue(steamId, out var steamBan) && (steamBan.expiry == 0 || now < steamBan.expiry))
            {
                player.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_STEAM_BANNED);
                NotifyBlockedBan(player, "SteamID", steamId);
            }
        }
    }

    private void TryRefreshBanCacheIfChanged()
    {
        try
        {
            var now = DateTime.UtcNow;
            if ((now - _lastBanCacheRefreshUtc).TotalMilliseconds < 800)
                return;

            _lastBanCacheRefreshUtc = now;

            var userWrite = File.Exists(BannedUserPath) ? File.GetLastWriteTimeUtc(BannedUserPath) : DateTime.MinValue;
            var ipWrite = File.Exists(BannedIpPath) ? File.GetLastWriteTimeUtc(BannedIpPath) : DateTime.MinValue;

            if (userWrite != _lastUserBanWriteUtc || ipWrite != _lastIpBanWriteUtc)
            {
                LoadBans();
                _lastUserBanWriteUtc = userWrite;
                _lastIpBanWriteUtc = ipWrite;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminPlus] Ban cache refresh check failed: {ex.Message}");
        }
    }

    private void NotifyBlockedBan(CCSPlayerController player, string banType, string banKey)
    {
        var safeName = SanitizeName(player.PlayerName);
        var steamId = player.SteamID.ToString();
        var ip = player.IpAddress ?? "-";

        PlayerExtensions.PrintToAll(Localizer["Ban.Blocked.Connect", safeName, steamId, banType]);
        Console.WriteLine(Localizer["Ban.Blocked.Console", safeName, steamId, ip, banType, banKey]);
    }

    private void ScheduleBanRechecks(int playerSlot)
    {
        AddTimer(0.10f, () => EnforceBan(playerSlot));
        AddTimer(0.30f, () => EnforceBan(playerSlot));
        AddTimer(0.5f, () => EnforceBan(playerSlot));
        AddTimer(1.0f, () => EnforceBan(playerSlot));
        AddTimer(1.5f, () => EnforceBan(playerSlot));
        AddTimer(3.0f, () => EnforceBan(playerSlot));
        AddTimer(6.0f, () => EnforceBan(playerSlot));
        AddTimer(10.0f, () => EnforceBan(playerSlot));
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
            var match = Regex.Match(line, @"^\s*banid\s+""([^""]+)""\s+""([^""]+)""\s+ip:([^\s]+)\s+expiry:(\d+)");
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
            var match = Regex.Match(line, @"^\s*addip\s+\""(.*?)\""\s+expiry:(\d+)(?:\s*//\s*(.+))?");
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

    private void PrintPluginVersionToConsole(CCSPlayerController? caller)
    {
        var line = $"{ModuleName} plugin version {ModuleVersion}";
        if (caller != null && caller.IsValid && !caller.IsBot)
            caller.PrintToConsole($"[AdminPlus] {line}");
        else
            Console.WriteLine($"[AdminPlus] {line}");
    }

    private bool TryHandlePublicVersionChatCommand(CCSPlayerController? player, string rawMessage)
    {
        if (player == null || !player.IsValid || player.IsBot)
            return false;

        var t = (rawMessage ?? string.Empty).Trim();
        if (t.Length == 0)
            return false;

        if (t.Equals("!version", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("/version", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("!css_version", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("/css_version", StringComparison.OrdinalIgnoreCase))
        {
            PrintPluginVersionToConsole(player);
            return true;
        }

        return false;
    }

    private void CmdPluginVersion(CCSPlayerController? caller, CommandInfo info)
    {
        try
        {
            PrintPluginVersionToConsole(caller);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminPlus] version command error: {ex.Message}");
        }
    }

    private void CmdAdmins(CCSPlayerController? caller, CommandInfo info)
    {
        try
        {
            var players = Utilities.GetPlayers()
                .Where(p => p != null && p.IsValid && !p.IsBot)
                .ToList();

            var onlineAdmins = new List<(string name, int imm)>();

            foreach (var p in players)
            {
                bool hasAdminPerm =
                    HasEffectivePermission(p, "@css/root") ||
                    HasEffectivePermission(p, "@css/ban") ||
                    HasEffectivePermission(p, "@css/generic");

                if (!hasAdminPerm)
                    continue;

                adminImmunity.TryGetValue(p.SteamID, out var imm);
                string name = SanitizeName(p.PlayerName);
                onlineAdmins.Add((name, imm));
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

    internal bool HasEffectivePermission(CCSPlayerController? player, string permission)
    {
        if (player == null || !player.IsValid)
            return false;

        if (AdminManager.PlayerHasPermissions(player, permission) || AdminManager.PlayerHasPermissions(player, "@css/root"))
            return true;

        try
        {
            if (!ReadAdminsFile(out var adminsRoot))
                return false;

            var steamKey = player.SteamID.ToString();
            if (!adminsRoot.TryGetPropertyValue(steamKey, out var adminNode) || adminNode is not JsonObject adminObj)
                return false;

            if (HasPermissionInFlagsArray(adminObj["flags"] as JsonArray, permission))
                return true;

            if (adminObj["groups"] is not JsonArray groups || groups.Count == 0)
                return false;

            var groupsFile = Path.Combine(Server.GameDirectory, "csgo", "addons", "counterstrikesharp", "configs", "admin_groups.json");
            if (!File.Exists(groupsFile))
                return false;

            var groupsText = File.ReadAllText(groupsFile);
            if (string.IsNullOrWhiteSpace(groupsText))
                return false;

            var groupRoot = JsonNode.Parse(groupsText) as JsonObject;
            if (groupRoot == null)
                return false;

            foreach (var groupNode in groups)
            {
                var groupName = groupNode?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(groupName))
                    continue;

                if (!groupRoot.TryGetPropertyValue(groupName, out var gNode) || gNode is not JsonObject groupObj)
                    continue;

                if (HasPermissionInFlagsArray(groupObj["flags"] as JsonArray, permission))
                    return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminPlus] Effective permission fallback error: {ex.Message}");
        }

        return false;
    }

    internal static string GetServerAddress()
    {
        string ip = "0.0.0.0";
        string port = "27015";

        try
        {
            var ipConVar = ConVar.Find("ip");
            if (ipConVar != null && !string.IsNullOrWhiteSpace(ipConVar.StringValue))
            {
                ip = ipConVar.StringValue;
            }
        }
        catch { }

        try
        {
            var hostPortConVar = ConVar.Find("hostport");
            if (hostPortConVar != null)
            {
                var portValue = hostPortConVar.GetPrimitiveValue<int>();
                if (portValue > 0)
                    port = portValue.ToString();
            }
        }
        catch { }

        return $"{ip}:{port}";
    }

    private static bool HasPermissionInFlagsArray(JsonArray? flags, string permission)
    {
        if (flags == null) return false;

        foreach (var flagNode in flags)
        {
            var flag = flagNode?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(flag))
                continue;

            if (string.Equals(flag, permission, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(flag, "@css/root", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
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

            var reportCutoff = DateTime.Now.AddMinutes(-10);
            var oldGlobalCooldowns = _lastReportTime.Where(kv => kv.Value < reportCutoff).Select(kv => kv.Key).ToList();
            foreach (var key in oldGlobalCooldowns)
                _lastReportTime.Remove(key);

            var oldPairCooldowns = _playerReportingCooldowns.Where(kv => kv.Value < reportCutoff).Select(kv => kv.Key).ToList();
            foreach (var key in oldPairCooldowns)
                _playerReportingCooldowns.Remove(key);
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
            caller.PrintToChat($"{Localizer["Prefix"]} {Localizer["Report.GlobalCooldown"]}");
            return;
        }

        var players = Utilities.GetPlayers().Where(x => x.IsValid && !x.IsBot && x.Connected == PlayerConnectedState.PlayerConnected);
        
        var reportMenu = CreateMenu(Localizer["Report.Menu.SelectPlayer"]);
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
    private readonly Dictionary<string, DateTime> _playerReportingCooldowns = new();

    private bool CheckReportCooldown(string playerId)
    {
        if (_lastReportTime.TryGetValue(ulong.Parse(playerId), out DateTime lastReportTime))
        {
            var secondsSinceLastReport = (DateTime.Now - lastReportTime).TotalSeconds;
            return secondsSinceLastReport >= 120;
        }
        return true;
    }

    private bool CheckPlayerToPlayerReportCooldown(CCSPlayerController reporter, CCSPlayerController targetPlayer)
    {
        string key = $"{reporter.SteamID}_{targetPlayer.SteamID}";
        if (_playerReportingCooldowns.TryGetValue(key, out DateTime lastReport))
        {
            var timeSinceLastReport = DateTime.Now - lastReport;
            return timeSinceLastReport.TotalMinutes >= 3.0;
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
            controller.PrintToChat($"{Localizer["Prefix"]} {Localizer["Report.PlayerNotFound"]}");
            return;
        }

        if (!CheckPlayerToPlayerReportCooldown(controller, targetPlayer))
        {
            controller.PrintToChat($"{Localizer["Prefix"]} {Localizer["Report.CooldownWarning"]}");
            return;
        }

        var reasons = new[]
        {
            Localizer["Report.Reason.Cheating"].Value,
            Localizer["Report.Reason.Toxic"].Value,
            Localizer["Report.Reason.Griefing"].Value,
            Localizer["Report.Reason.Spam"].Value,
            Localizer["Report.Reason.Other"].Value
        };

        var reasonMenu = CreateMenu(Localizer["Report.Menu.SelectReason"]);
        if (reasonMenu == null) return;

        var customReasonData = new ChatMenuOptionData(Localizer["Report.Reason.Custom"], () => HandleCustomReasonSimple(controller, targetPlayer));
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
        controller.PrintToChat($"{Localizer["Prefix"]} {Localizer["Report.CustomReasonPrompt"]}");
        
        _selectedReportTarget = targetPlayer;
        
        AddTimer(20.0f, () =>
        {
            if (_selectedReportTarget != null)
            {
                controller.PrintToChat($"{Localizer["Prefix"]} {Localizer["Report.TimedOut"]}");
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
            reporter.PrintToChat($"{Localizer["Prefix"]} {Localizer["Report.CooldownWarning"]}");
            _selectedReportTarget = null;
            _activeReportMenu = null;
            return;
        }

        try
        {
            var serverIp = GetServerAddress();

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
            reporter.PrintToChat($"{Localizer["Prefix"]} {message}");
        }
        catch (Exception ex)
        {
            reporter.PrintToChat($"{Localizer["Prefix"]} {Localizer["Report.FailedToSend"]}");
            Console.WriteLine($"[AdminPlus] Report error: {ex.Message}");
        }
    }


    private bool HandleCustomReportReason(CCSPlayerController player, string message)
    {
        if (_selectedReportTarget == null) return false;

        if (!CheckPlayerToPlayerReportCooldown(player, _selectedReportTarget))
        {
            player.PrintToChat($"{Localizer["Prefix"]} {Localizer["Report.CooldownWarning"]}");
            _selectedReportTarget = null;
            return true;
        }

        if (message.ToLower().Contains("cancel"))
        {
            player.PrintToChat($"{Localizer["Prefix"]} {Localizer["Report.Cancelled"]}");
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
        var prefix = AdminPlus._instance?.Localizer?["Prefix"] ?? "";
        if (!string.IsNullOrEmpty(prefix))
            controller.PrintToChat($"{prefix} {message}");
        else
            controller.PrintToChat(message);
    }
    
        public static void PrintToAll(string message)
        {
            try
            {
                var prefix = AdminPlus._instance?.Localizer?["Prefix"] ?? "";
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
