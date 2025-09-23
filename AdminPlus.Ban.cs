using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.ValveConstants.Protobuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AdminPlus;

public partial class AdminPlus
{
    private Dictionary<ulong, int> adminImmunity = new();
    private Dictionary<ulong, (DateTime seenAt, string ip)> recentSeen = new();
    
    public void CleanupBanSystem()
    {
        try
        {
            adminImmunity.Clear();
            recentSeen.Clear();
            Console.WriteLine("[AdminPlus] Ban system cleaned up.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminPlus] Error during ban system cleanup: {ex.Message}");
        }
    }

    public void RegisterBanCommands()
    {
        AddCommand("ban", Localizer["Ban.Usage"], CmdBan);
        AddCommand("ipban", Localizer["IpBan.Usage"], CmdIpBan);
        AddCommand("unban", Localizer["Unban.Usage"], CmdUnban);
        AddCommand("lastban", Localizer["LastBan.Header"], CmdLastBan);
        AddCommand("baninfo", Localizer["BanInfo.Usage"], CmdBanInfo);

        AddCommand("css_ban", "Ban a player from console", CmdBan);
        AddCommand("css_ipban", "IP ban a player from console", CmdIpBan);
        AddCommand("css_unban", "Unban a player from console", CmdUnban);
        AddCommand("css_lastban", "Show last disconnected players", CmdLastBan);
        AddCommand("css_baninfo", "Show ban info", CmdBanInfo);
        
        AddCommand("css_cleanbans", "Clean all bans from console", CmdCleanBans);
        AddCommand("css_cleanipbans", "Clean all IP bans from console", CmdCleanIpBans);
        AddCommand("css_cleansteambans", "Clean all SteamID bans from console", CmdCleanSteamBans);
    }

    private void CmdBan(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null)
        {
            Console.WriteLine("[AdminPlus] Panel ban command executed.");
        }
        else if (!caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/ban"))
        {
            if (caller.IsValid) caller.Print(Localizer["NoPermission"]);
            return;
        }

        if (info.ArgCount < 2)
        {
            if (caller != null && caller.IsValid) caller.Print(Localizer["Ban.Usage"]);
            else Console.WriteLine(Localizer["Ban.UsageConsole"]);
            return;
        }

        var targetInput = info.GetArg(1);
        
        var teamPlayers = GetPlayersFromTeamInput(targetInput);
        if (teamPlayers.Count > 0)
        {
            HandleTeamBan(caller, info, teamPlayers, targetInput);
            return;
        }

        var target = (caller == null) ? GetPlayerFromInput(targetInput, true) : GetPlayerFromInput(targetInput);

        if (target == null && caller != null && caller.IsValid)
        {
            caller.Print(Localizer["Ban.TargetNotFound", targetInput]);
            return;
        }

        if (target == null && caller == null)
        {
            Console.WriteLine(Localizer["Ban.TargetNotFoundConsole", targetInput]);
            return;
        }

        if (caller != null && caller.IsValid && target != null && CheckImmunity(caller, target))
        {
            caller.Print(Localizer["Ban.ImmunityBlocked"]);
            return;
        }

        HandleIndividualBan(caller, info, target, targetInput);
    }

    private void HandleTeamBan(CCSPlayerController? caller, CommandInfo info, List<CCSPlayerController> teamPlayers, string teamInput)
    {
        if (teamPlayers.Count == 0)
        {
            SendTeamBanErrorMessage(caller, GetTeamName(teamInput));
            return;
        }

        int minutes = 0;
        if (info.ArgCount >= 3 && int.TryParse(info.GetArg(2), out var parsed))
            minutes = parsed;

        string reason = Localizer["Ban.NoReason"];
        if (info.ArgCount >= 4)
        {
            reason = string.Join(" ", Enumerable.Range(3, info.ArgCount - 3).Select(i => info.GetArg(i)));
        }

        string executorName = GetExecutorName(caller);
        string teamName = GetTeamName(teamInput);
        int bannedCount = 0;

        foreach (var target in teamPlayers)
        {
            if (target == null || !target.IsValid) continue;
            
            if (caller != null && caller.IsValid && CheckImmunity(caller, target))
            {
                if (caller.IsValid) caller.Print(Localizer["Ban.ImmunityBlockedPlayer", target.PlayerName]);
                continue;
            }

            string steamId = target.SteamID.ToString();
            string playerName = target.PlayerName;
            string ip = target.IpAddress ?? "-";

            var safeName = SanitizeName(playerName);
            var expiry = minutes == 0 ? 0 : DateTimeOffset.UtcNow.ToUnixTimeSeconds() + minutes * 60;

            var safeReason = SanitizeForCfg(reason);
            var safeTeam = SanitizeForCfg(teamInput);
            var line = $"banid \"{steamId}\" \"{safeName}\" ip:{ip} expiry:{expiry} // {safeReason} (Team: {safeTeam})";

            lock (_lock)
            {
                SteamBans[steamId] = (expiry, line, safeName, ip);
            }

            target.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_STEAM_BANNED);
            bannedCount++;
        }

        if (bannedCount > 0)
        {
            File.WriteAllLines(BannedUserPath, SteamBans.Values.Select(x => x.line));
            
            string durationText = minutes == 0 ? Localizer["Duration.Forever"] : Localizer["Duration.Temporary", minutes];
            PlayerExtensions.PrintToAll(Localizer["Team.Ban.Success", executorName, teamName, durationText, reason]);
            PlayerExtensions.PrintToAll(Localizer["Team.Ban.PlayerCount", bannedCount]);

            LogAction($"{executorName} banned {bannedCount} players from {teamName} for {minutes} minutes. Reason: {reason}");
        }
        else
        {
            SendTeamBanErrorMessage(caller, teamName);
        }
    }

    private void SendTeamBanErrorMessage(CCSPlayerController? caller, string teamName)
    {
        if (caller != null && caller.IsValid)
            caller.Print(Localizer["Error.NoPlayersInTeam", teamName]);
        else
            Console.WriteLine(Localizer["Error.NoPlayersInTeam", teamName]);
    }

    private void HandleIndividualBan(CCSPlayerController? caller, CommandInfo info, CCSPlayerController? target, string targetInput)
    {
        int minutes = 0;
        if (info.ArgCount >= 3 && int.TryParse(info.GetArg(2), out var parsed))
            minutes = parsed;

        string reason = Localizer["Ban.NoReason"];
        if (info.ArgCount >= 4)
        {
            reason = string.Join(" ", Enumerable.Range(3, info.ArgCount - 3).Select(i => info.GetArg(i)));
        }

        string steamId;
        string playerName;
        string ip;

        if (target != null)
        {
            steamId = target.SteamID.ToString();
            playerName = target.PlayerName;
            ip = target.IpAddress ?? "-";
        }
        else
        {
            steamId = targetInput.Contains("STEAM_") ? targetInput : "";
            playerName = "ConsoleBanned";
            ip = targetInput.Contains(".") ? targetInput : "-";

            if (string.IsNullOrEmpty(steamId) && !targetInput.Contains("."))
            {
                Console.WriteLine(Localizer["Ban.ConsoleSteamIdOrIp"]);
                return;
            }
        }

        var safeName = SanitizeName(playerName);
        var expiry = minutes == 0 ? 0 : DateTimeOffset.UtcNow.ToUnixTimeSeconds() + minutes * 60;

        var safeReason = SanitizeForCfg(reason);
        var line = $"banid \"{steamId}\" \"{safeName}\" ip:{ip} expiry:{expiry} // {safeReason}";

        lock (_lock)
        {
            SteamBans[steamId] = (expiry, line, safeName, ip);
            File.WriteAllLines(BannedUserPath, SteamBans.Values.Select(x => x.line));
        }

        if (target != null)
            target.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_STEAM_BANNED);

        string executorName = GetExecutorName(caller);

        if (minutes == 0)
            PlayerExtensions.PrintToAll(Localizer["Player.Ban.Success", executorName, safeName, Localizer["Duration.Forever"], reason]);
        else
            PlayerExtensions.PrintToAll(Localizer["Player.Ban.Success", executorName, safeName, Localizer["Duration.Temporary", minutes], reason]);

        LogAction($"{executorName} banned {safeName} ({steamId}) [IP:{ip}] for {minutes} minutes. Reason: {reason}");
    }

    private void CmdIpBan(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null)
        {
            Console.WriteLine("[AdminPlus] Panel IP ban command executed.");
        }
        else if (!caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/ban"))
        {
            if (caller.IsValid) caller.Print(Localizer["NoPermission"]);
            return;
        }

        if (info.ArgCount < 2)
        {
            if (caller != null && caller.IsValid) caller.Print(Localizer["IpBan.Usage"]);
            else Console.WriteLine(Localizer["IpBan.UsageConsole"]);
            return;
        }

        string input = info.GetArg(1);
        var target = GetPlayerFromInput(input, caller == null);

        string ip = input;
        string displayName = ip;

        if (target != null && !string.IsNullOrEmpty(target.IpAddress))
        {
            ip = target.IpAddress;
            displayName = SanitizeName(target.PlayerName);
        }

        if (string.IsNullOrWhiteSpace(ip) || !Regex.IsMatch(ip, @"^\d{1,3}(\.\d{1,3}){3}$"))
        {
            if (caller != null && caller.IsValid) caller.Print(Localizer["IpBan.Invalid"]);
            else Console.WriteLine(Localizer["IpBan.InvalidConsole"]);
            return;
        }

        string reason = Localizer["Ban.NoReason"];
        if (info.ArgCount >= 3)
        {
            reason = string.Join(" ", Enumerable.Range(2, info.ArgCount - 2).Select(i => info.GetArg(i)));
        }

        var safeReason = SanitizeForCfg(reason);
        var line = $"addip \"{ip}\" expiry:0 // {safeReason}";

        lock (_lock)
        {
            IpBans[ip] = (0, line, displayName);
            File.WriteAllLines(BannedIpPath, IpBans.Values.Select(x => x.line));
        }

        foreach (var p in Utilities.GetPlayers()!.Where(p => p.IsValid && p.IpAddress == ip))
            p.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_STEAM_BANNED);

        string executorName = GetExecutorName(caller);

        if (target != null)
            PlayerExtensions.PrintToAll(Localizer["IpBan.AddedNick", executorName, displayName, reason]);
        else
            PlayerExtensions.PrintToAll(Localizer["IpBan.AddedIp", executorName, ip, reason]);

        LogAction($"{executorName} IP banned {displayName} ({ip}). Reason: {reason}");
    }

    private void CmdUnban(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null)
        {
            Console.WriteLine("[AdminPlus] Panel unban command executed.");
        }
        else if (!caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/unban"))
        {
            if (caller.IsValid) caller.Print(Localizer["NoPermission"]);
            return;
        }

        if (info.ArgCount < 2)
        {
            if (caller != null && caller.IsValid) caller.Print(Localizer["Unban.Usage"]);
            else Console.WriteLine(Localizer["Unban.UsageConsole"]);
            return;
        }

        string key = info.GetArg(1);
        bool removed = false;

        lock (_lock)
        {
            if (SteamBans.Remove(key))
            {
                File.WriteAllLines(BannedUserPath, SteamBans.Values.Select(x => x.line));
                removed = true;
            }
            if (IpBans.Remove(key))
            {
                File.WriteAllLines(BannedIpPath, IpBans.Values.Select(x => x.line));
                removed = true;
            }
        }

        string executorName = GetExecutorName(caller);

        if (removed)
        {
            PlayerExtensions.PrintToAll(Localizer["Unban.Success", executorName, key]);
            LogAction($"{executorName} unbanned {key}");
        }
        else
        {
            if (caller != null && caller.IsValid) caller.Print(Localizer["Unban.NotFound", key]);
            else Console.WriteLine(Localizer["Unban.NotFoundConsole", key]);
        }
    }

    private void CmdLastBan(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null)
        {
            Console.WriteLine(Localizer["LastBan.HeaderConsole"]);
            if (!DisconnectedPlayers.Any())
            {
                Console.WriteLine(Localizer["LastBan.EmptyConsole"]);
            }
            else
            {
                foreach (var kv in DisconnectedPlayers.TakeLast(5))
                {
                    Console.WriteLine(Localizer["LastBan.ListItemConsole", kv.Value.name, kv.Key, kv.Value.ip]);
                }
            }
            Console.WriteLine(Localizer["LastBan.EndListConsole"]);
            return;
        }

        if (!caller.IsValid) return;

        var menu = CreateMenu(Localizer["LastBan.Header"]);

        if (!DisconnectedPlayers.Any())
        {
            menu?.AddMenuOption(Localizer["LastBan.Empty"], (ply, opt) => { });
        }
        else
        {
            foreach (var kv in DisconnectedPlayers.TakeLast(5))
            {
                string playerName = kv.Value.name;
                string ip = kv.Value.ip;
                ulong steamId = kv.Key;

                menu?.AddMenuOption($"{playerName}", (ply, opt) => ShowLastBanOptions(caller, playerName, steamId, ip));
            }
        }

        if (menu != null)
        {
            menu.ExitButton = true;
            OpenMenu(caller, menu);
        }
    }

    private void ShowLastBanOptions(CCSPlayerController admin, string playerName, ulong steamId, string ip)
    {
        if (!admin.IsValid) return;

        var menu = CreateMenu(playerName);

        menu?.AddMenuOption(Localizer["Menu.Option.SteamIdBan"], (ply, opt) => ShowDurationMenuLastBan(admin, steamId, playerName, ip));
        menu?.AddMenuOption(Localizer["Menu.Option.IpBan"], (ply, opt) => ShowReasonMenuLastBan(admin, playerName, steamId, ip, 0, true));

        if (menu != null)
        {
            menu.ExitButton = true;
            OpenMenu(admin, menu);
        }
    }

    private void ShowDurationMenuLastBan(CCSPlayerController admin, ulong steamId, string playerName, string ip)
    {
        if (!admin.IsValid) return;

        var menu = CreateMenu(Localizer["Menu.ChooseDuration"]);

        var durations = new Dictionary<string, int>
        {
            { "5 " + Localizer["Duration.Minute"], 5 },
            { "30 " + Localizer["Duration.Minute"], 30 },
            { "1 " + Localizer["Duration.Hour"], 60 },
            { Localizer["Duration.Forever"], 0 }
        };

        foreach (var entry in durations)
            menu?.AddMenuOption(entry.Key, (ply, opt) => ShowReasonMenuLastBan(admin, playerName, steamId, ip, entry.Value, false));

        if (menu != null)
        {
            menu.ExitButton = true;
            OpenMenu(admin, menu);
        }
    }

    private void ShowReasonMenuLastBan(CCSPlayerController admin, string playerName, ulong steamId, string ip, int minutes, bool isIpBan)
    {
        if (!admin.IsValid) return;

        var menu = CreateMenu(Localizer["Menu.ChooseReason"]);

        var reasons = new[] {
            Localizer["Reason.Cheat"],
            Localizer["Reason.Insult"],
            Localizer["Reason.Advertise"],
            Localizer["Reason.Troll"],
            Localizer["Reason.Other"],
            Localizer["Ban.NoReason"]
        };

        foreach (var reason in reasons)
        {
            menu?.AddMenuOption(reason, (ply, opt) =>
            {
                if (!admin.IsValid) return;

                var safeName = SanitizeName(playerName);

                if (isIpBan)
                {
                    var safeReason = SanitizeForCfg(reason);
                    var line = $"addip \"{ip}\" expiry:0 // {safeReason}";

                    lock (_lock)
                    {
                        IpBans[ip] = (0, line, safeName);
                        File.WriteAllLines(BannedIpPath, IpBans.Values.Select(x => x.line));
                    }

                    PlayerExtensions.PrintToAll(Localizer["IpBan.AddedIp", admin.PlayerName, ip, reason]);
                    LogAction($"{admin.PlayerName} ip-banned {safeName} ({ip}). Reason: {reason}");
                }
                else
                {
                    var expiry = minutes == 0 ? 0 : DateTimeOffset.UtcNow.ToUnixTimeSeconds() + minutes * 60;
                    var safeReason = SanitizeForCfg(reason);
                    var line = $"banid \"{steamId}\" \"{safeName}\" ip:{ip} expiry:{expiry} // {safeReason}";

                    lock (_lock)
                    {
                        SteamBans[steamId.ToString()] = (expiry, line, safeName, ip);
                        File.WriteAllLines(BannedUserPath, SteamBans.Values.Select(x => x.line));
                    }

                    if (minutes == 0)
                        PlayerExtensions.PrintToAll(Localizer["Player.Ban.Success", admin.PlayerName, safeName, Localizer["Duration.Forever"], reason]);
                    else
                        PlayerExtensions.PrintToAll(Localizer["Player.Ban.Success", admin.PlayerName, safeName, Localizer["Duration.Temporary", minutes], reason]);

                    LogAction($"{admin.PlayerName} banned {safeName} ({steamId}) [IP:{ip}] for {minutes} minutes. Reason: {reason}");
                }
            });
        }

        if (menu != null)
        {
            menu.ExitButton = true;
            OpenMenu(admin, menu);
        }
    }

    private void CmdBanInfo(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null)
        {
            Console.WriteLine(Localizer["BanInfo.ConsoleExecuted"]);
        }

        if (info.ArgCount < 2)
        {
            if (caller != null && caller.IsValid) caller.Print(Localizer["BanInfo.Usage"]);
            else Console.WriteLine(Localizer["BanInfo.UsageConsole"]);
            return;
        }

        string key = info.GetArg(1);

        if (SteamBans.TryGetValue(key, out var steamBan))
        {
            long remaining = steamBan.expiry == 0 ? 0 : steamBan.expiry - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string remainStr = steamBan.expiry == 0 ? Localizer["Duration.Forever"] :
                $"{remaining / 60} {Localizer["Duration.Minute"]}";

            string cleanLine = Regex.Replace(steamBan.line, @"\s*expiry:\d+", "");

            if (caller != null && caller.IsValid)
                caller.Print(Localizer["BanInfo.Steam", key, remainStr, cleanLine]);
            else
                Console.WriteLine(Localizer["BanInfo.SteamConsole", key, remainStr, cleanLine]);
            return;
        }

        if (IpBans.TryGetValue(key, out var ipBan))
        {
            string cleanLine = Regex.Replace(ipBan.line, @"\s*expiry:\d+", "");

            if (caller != null && caller.IsValid)
                caller.Print(Localizer["BanInfo.Ip", key, cleanLine]);
            else
                Console.WriteLine(Localizer["BanInfo.IpConsole", key, cleanLine]);
            return;
        }

        if (caller != null && caller.IsValid)
            caller.Print(Localizer["BanInfo.NotFound", key]);
        else
            Console.WriteLine(Localizer["BanInfo.NotFoundConsole", key]);
    }

    private void LoadImmunity()
    {
        try
        {
            var path = Path.Combine(Server.GameDirectory, "csgo/addons/counterstrikesharp/configs/admins.json");
            if (!File.Exists(path)) return;

            var json = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var admin in json.RootElement.EnumerateObject())
            {
                var obj = admin.Value;
                if (obj.TryGetProperty("immunity", out var immVal) && immVal.TryGetInt32(out var imm))
                {
                    if (TryParseSteamId(admin.Name, out var steamId))
                        adminImmunity[steamId] = imm;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(Localizer["Immunity.LoadError", ex.Message]);
        }
    }

    private bool TryParseSteamId(string input, out ulong steamId)
    {
        steamId = 0;
        return ulong.TryParse(input, out steamId) && steamId.ToString().Length >= 17;
    }

    private bool CheckImmunity(CCSPlayerController caller, CCSPlayerController target)
    {
        if (caller == null || target == null) return false;
        if (caller.SteamID == target.SteamID)
            return false;

        adminImmunity.TryGetValue(caller.SteamID, out var callerImm);
        adminImmunity.TryGetValue(target.SteamID, out var targetImm);

        return callerImm < targetImm;
    }

    private void SaveRecentSeen(CCSPlayerController player)
    {
        if (player != null && player.IsValid)
            recentSeen[player.SteamID] = (DateTime.UtcNow, player.IpAddress ?? "-");
    }

    private void CheckAltAccountWarning(CCSPlayerController player)
    {
        if (player == null || !player.IsValid) return;

        var ip = player.IpAddress ?? "-";

        var others = SteamBans
            .Where(x => x.Value.ip == ip)
            .Take(1)
            .ToList();

        if (others.Count == 0)
            return;

        var lastSteamId = others[0].Key;
        string reason = others[0].Value.line.Split("//").LastOrDefault()?.Trim() ?? Localizer["Ban.NoReason"];
        string nick = others[0].Value.nick;

        AddTimer(5.0f, () =>
        {
            for (int i = 0; i < 3; i++)
            {
                foreach (var admin in Utilities.GetPlayers()!
                         .Where(p => p.IsValid && !p.IsBot && AdminManager.PlayerHasPermissions(p, "@css/ban")))
                {
                    admin.PrintToChat(Localizer["AltAccount.Warning1", player.PlayerName, ip]);
                    admin.PrintToChat(Localizer["AltAccount.Warning2", lastSteamId, nick, reason]);
                }
            }
        });
    }

    private CCSPlayerController? GetPlayerFromInput(string input, bool forConsole = false)
    {
        var players = Utilities.GetPlayers();
        if (players == null) return null;

        if (input.StartsWith("#") && int.TryParse(input[1..], out var userId))
        {
            return players.FirstOrDefault(p => p.IsValid && p.UserId == userId);
        }

        var target = players.FirstOrDefault(p => p.IsValid && p.SteamID.ToString() == input);
        if (target != null) return target;

        if (forConsole && Regex.IsMatch(input, @"^\d{1,3}(\.\d{1,3}){3}$"))
        {
            return players.FirstOrDefault(p => p.IsValid && p.IpAddress == input);
        }

        var matchingPlayers = players
            .Where(p => p.IsValid && !string.IsNullOrEmpty(p.PlayerName))
            .Where(p => p.PlayerName.Contains(input, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matchingPlayers.Count == 0)
            return null;

        if (forConsole && matchingPlayers.Count > 1)
        {
            var bestMatch = matchingPlayers
                .OrderByDescending(p => StringSimilarity(p.PlayerName, input))
                .FirstOrDefault();

            Console.WriteLine($"[AdminPlus] Multiple matches found for '{input}': {string.Join(", ", matchingPlayers.Select(p => p.PlayerName))}");
            Console.WriteLine($"[AdminPlus] Using best match: {bestMatch?.PlayerName}");
            return bestMatch;
        }

        if (forConsole && matchingPlayers.Count == 1)
            return matchingPlayers[0];

        return matchingPlayers.FirstOrDefault();
    }

    private double StringSimilarity(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2))
            return 0;

        s1 = s1.ToLower();
        s2 = s2.ToLower();

        if (s1 == s2)
            return 1.0;

        int[,] distance = new int[s1.Length + 1, s2.Length + 1];

        for (int i = 0; i <= s1.Length; i++)
            distance[i, 0] = i;

        for (int j = 0; j <= s2.Length; j++)
            distance[0, j] = j;

        for (int i = 1; i <= s1.Length; i++)
        {
            for (int j = 1; j <= s2.Length; j++)
            {
                int cost = (s1[i - 1] == s2[j - 1]) ? 0 : 1;

                distance[i, j] = Math.Min(
                    Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1),
                    distance[i - 1, j - 1] + cost);
            }
        }

        int maxLength = Math.Max(s1.Length, s2.Length);
        if (maxLength == 0)
            return 1.0;

        return 1.0 - (double)distance[s1.Length, s2.Length] / maxLength;
    }

    internal string SanitizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Localizer["UnknownPlayer"];

        string cleaned = Regex.Replace(name, @"[^\w\s]", "");
        
        if (cleaned.Length > 32)
            cleaned = cleaned.Substring(0, 32);
            
        cleaned = cleaned.Replace(" ", "_");
        
        return cleaned.Length > 0 ? cleaned : Localizer["UnknownPlayer"];
    }

    private string SanitizeForCfg(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "Unknown";

        string cleaned = Regex.Replace(input, @"[""\\\n\r\t;]", "");
        
        if (cleaned.Length > 64)
            cleaned = cleaned.Substring(0, 64);
            
        return cleaned.Length > 0 ? cleaned : "Unknown";
    }

    private void CmdCleanBans(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null && (!caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/root")))
        {
            if (caller.IsValid) caller.Print(Localizer["NoPermission"]);
            return;
        }

        int steamBanCount = 0;
        int ipBanCount = 0;

        lock (_lock)
        {
            steamBanCount = SteamBans.Count;
            ipBanCount = IpBans.Count;
            
            SteamBans.Clear();
            IpBans.Clear();
            
            File.WriteAllText(BannedUserPath, "");
            File.WriteAllText(BannedIpPath, "");
        }

        if (caller != null && caller.IsValid)
            caller.Print(Localizer["CleanBans.Success", steamBanCount, ipBanCount]);
        else
            Console.WriteLine(Localizer["CleanBans.Console", steamBanCount, ipBanCount]);
            
        LogAction($"All bans cleared by {GetExecutorName(caller)}. SteamID: {steamBanCount}, IP: {ipBanCount}");
    }

    private void CmdCleanIpBans(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null && (!caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/root")))
        {
            if (caller.IsValid) caller.Print(Localizer["NoPermission"]);
            return;
        }

        int ipBanCount = 0;

        lock (_lock)
        {
            ipBanCount = IpBans.Count;
            IpBans.Clear();
            File.WriteAllText(BannedIpPath, "");
        }

        if (caller != null && caller.IsValid)
            caller.Print(Localizer["CleanIpBans.Success", ipBanCount]);
        else
            Console.WriteLine(Localizer["CleanIpBans.Console", ipBanCount]);
            
        LogAction($"All IP bans cleared by {GetExecutorName(caller)}. Count: {ipBanCount}");
    }

    private void CmdCleanSteamBans(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null && (!caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/root")))
        {
            if (caller.IsValid) caller.Print(Localizer["NoPermission"]);
            return;
        }

        int steamBanCount = 0;

        lock (_lock)
        {
            steamBanCount = SteamBans.Count;
            SteamBans.Clear();
            File.WriteAllText(BannedUserPath, "");
        }

        if (caller != null && caller.IsValid)
            caller.Print(Localizer["CleanSteamBans.Success", steamBanCount]);
        else
            Console.WriteLine(Localizer["CleanSteamBans.Console", steamBanCount]);
            
        LogAction($"All SteamID bans cleared by {GetExecutorName(caller)}. Count: {steamBanCount}");
    }
}