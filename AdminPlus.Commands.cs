using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.ValveConstants.Protobuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using CounterStrikeSharp.API.Modules.Memory;

namespace AdminPlus;

public partial class AdminPlus
{
    private static readonly Dictionary<string, string> MapAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        { "mirage", "de_mirage" },
        { "vertigo", "de_vertigo" },
        { "inferno", "de_inferno" },
        { "nuke", "de_nuke" },
        { "overpass", "de_overpass" },
        { "ancient", "de_ancient" },
        { "dust2", "de_dust2" },
        { "anubis", "de_anubis" },
        { "train", "de_train" }
    };

    private static HashSet<string> InstalledMaps = new();
    
    public static void CleanupCommands()
    {
        try
        {
            InstalledMaps.Clear();
            Console.WriteLine("[AdminPlus] Commands system cleaned up.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminPlus] Error during commands cleanup: {ex.Message}");
        }
    }

    private void LoadInstalledMaps()
    {
        try
        {
            string mapsDir = Path.Combine(Server.GameDirectory, "csgo", "maps");
            if (Directory.Exists(mapsDir))
            {
                InstalledMaps = Directory.GetFiles(mapsDir, "*.bsp")
                    .Select(f => Path.GetFileNameWithoutExtension(f))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminPlus] Error loading maps: {ex.Message}");
        }
    }

    public void RegisterAdminCommands()
    {
        LoadInstalledMaps();

        AddCommand("kick", Localizer["Kick.Usage"], CmdKick);
        AddCommand("map", Localizer["Map.Usage"], CmdChangeMap);
        AddCommand("wsmap", Localizer["Map.Usage"], CmdChangeWorkshopMap);
        AddCommand("workshop", Localizer["Map.Usage"], CmdChangeWorkshopMap);
        AddCommand("rcon", Localizer["Rcon.Usage"], CmdRcon);
        AddCommand("cvar", Localizer["Cvar.Usage"], CmdCvar);
        AddCommand("who", Localizer["Who.Usage"], CmdWho);
        AddCommand("rr", Localizer["Round.Usage"], CmdRestartRound);

        AddCommand("css_kick", "Kick a player from console", CmdKick);
        AddCommand("css_map", "Change map from console", CmdChangeMap);
        AddCommand("css_wsmap", "Change workshop map from console", CmdChangeWorkshopMap);
        AddCommand("css_workshop", "Change workshop map from console", CmdChangeWorkshopMap);
        AddCommand("css_rcon", "Send RCON command from console", CmdRcon);
        AddCommand("css_cvar", "Get/Set cvar value from console", CmdCvar);
        AddCommand("css_who", "Show player info from console", CmdWho);
        AddCommand("css_rr", "Restart round from console", CmdRestartRound);

        AddCommand("css_players", "List all players in console", CmdPlayers);

        AddCommand("css_slap", "Slap a player", CmdSlap);
        AddCommand("css_slay", "Slay a player", CmdSlay);
        AddCommand("css_rename", "Rename a player", CmdRename);
        AddCommand("css_money", "Set player money", CmdMoney);
        AddCommand("css_armor", "Set player armor", CmdArmor);

        AddCommand("slap", Localizer["Slap.Usage"], CmdSlap);
        AddCommand("slay", Localizer["Slay.Usage"], CmdSlay);
        AddCommand("money", Localizer["Money.Usage"], CmdMoney);
        AddCommand("armor", Localizer["Armor.Usage"], CmdArmor);

        foreach (var alias in MapAliases.Keys)
        {
            AddCommand(alias, $"Change map alias: {alias}", (caller, info) =>
            {
                bool isConsoleCommand = caller == null;
                
                if (isConsoleCommand)
                {
                    Console.WriteLine($"[AdminPlus] Console map change command executed: {alias}");
                }
                else
                {
                    if (caller == null || !caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/generic"))
                    {
                        caller?.Print(Localizer["NoPermission"]);
                        return;
                    }
                }
                ForceChangeMap(caller, MapAliases[alias]);
            });
        }
    }

    private void CmdKick(CCSPlayerController? caller, CommandInfo info)
    {
        bool isConsoleCommand = caller == null;
        
        if (isConsoleCommand)
        {
            Console.WriteLine("[AdminPlus] Console kick command executed.");
        }
        else
        {
            if (caller == null || !caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/generic"))
            {
                caller?.Print(Localizer["NoPermission"]);
                return;
            }
        }

        if (info.ArgCount < 2)
        {
            SendUsageMessage(caller, "Kick.Usage", "Usage: css_kick <target> [reason]");
            return;
        }

        var targetInput = info.GetArg(1);

        var teamPlayers = GetPlayersFromTeamInput(targetInput);
        if (teamPlayers.Count > 0)
        {
            HandleTeamKick(caller, info, teamPlayers, targetInput);
            return;
        }

        var target = GetPlayerTarget(caller, info, 1, out string errorMessage);
        if (target == null)
        {
            SendErrorMessage(caller, "NoMatchingClient", errorMessage);
            return;
        }

        string reason = Localizer["Ban.NoReason"];
        if (info.ArgCount >= 3)
        {
            reason = string.Join(" ", Enumerable.Range(2, info.ArgCount - 2).Select(i => info.GetArg(i)));
        }

        target.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_KICKED);

        string executorName = GetExecutorNameCommand(caller);
        string targetName = target.PlayerName;

        PlayerExtensions.PrintToAll(Localizer["Player.Kick.Success", executorName, EscapeForStringFormat(targetName), reason]);

        if (caller == null)
            Console.WriteLine($"[AdminPlus] Panel player kicked: {targetName} (Reason: {reason})");
    }

    private void CmdSlap(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null && (!caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/slay")))
        { if (caller.IsValid) caller.Print(Localizer["NoPermission"]); return; }

        if (info.ArgCount < 2)
        { SendUsageMessage(caller, "Slap.Usage", "Usage: !slap <target> <damage>"); return; }

        string targetTok = info.GetArg(1);
        int damage = 0;
        if (info.ArgCount >= 3) int.TryParse(info.GetArg(2), NumberStyles.Integer, CultureInfo.InvariantCulture, out damage);

        var targets = ResolveTargetsMultiWithBots(targetTok, onlyAlive: true);
        if (targets.Count == 0)
        { SendErrorMessage(caller, "NoMatchingClient", Localizer["NoMatchingClient"]); return; }

        if (caller != null && caller.IsValid)
        {
            targets = targets.Where(t => !CheckImmunity(caller, t)).ToList();
            if (targets.Count == 0)
            { SendErrorMessage(caller, "Punish.ImmunityBlocked", Localizer["Punish.ImmunityBlocked"]); return; }
        }

        foreach (var t in targets)
        {
            var pawn = t.PlayerPawn?.Value;
            if (pawn == null || !t.IsValid) continue;

            if (damage > 0)
            {
                int newHp = Math.Max(0, pawn.Health - damage);
                pawn.Health = newHp; t.Health = newHp; Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iHealth");
                if (newHp <= 0)
                {
                    try { pawn.TakesDamage = true; pawn.CommitSuicide(false, true); } catch { }
                    continue;
                }
            }

            try
            {
                var vel = pawn.AbsVelocity ?? new Vector(0, 0, 0);
                var rnd = Random.Shared;
                float dx = (float)((rnd.Next(180) + 50) * (rnd.Next(2) == 1 ? -1 : 1));
                float dy = (float)((rnd.Next(180) + 50) * (rnd.Next(2) == 1 ? -1 : 1));
                float dz = (float)(rnd.Next(200) + 100);
                vel = new Vector(vel.X + dx, vel.Y + dy, vel.Z + dz);
                pawn.Teleport(null, null, vel);
                try
                {
                    var painSounds = new[] { "player/damage1.vsnd_c", "player/damage2.vsnd_c", "player/damage3.vsnd_c" };
                    var s = painSounds[Random.Shared.Next(painSounds.Length)];
                    t.ExecuteClientCommand($"play {s}");
                }
                catch { }
            }
            catch { }
        }

        string adminName = GetExecutorNameCommand(caller);
        string label = targets.Count == 1 ? targets[0].PlayerName : Localizer["Team.ALL"];
        string message = targets.Count == 1 
            ? Localizer["css_slap<player>", adminName, EscapeForStringFormat(label), damage]
            : Localizer["css_slap<multiple>", adminName, targets.Count, damage];
        
        foreach (var player in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot))
        {
            player.Print(message);
        }
    }

    private void CmdSlay(CCSPlayerController? caller, CommandInfo info)
    {
        bool isConsoleCommand = caller == null;
        
        if (isConsoleCommand)
        {
            Console.WriteLine("[AdminPlus] Console slay command executed.");
        }
        else
        {
            if (caller == null || !caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/slay"))
            {
                caller?.Print(Localizer["NoPermission"]);
                return;
            }
        }

        if (info.ArgCount < 2)
        { SendUsageMessage(caller, "Slay.Usage", "Usage: !slay <target>"); return; }

        string targetTok = info.GetArg(1);
        var targets = ResolveTargetsMultiWithBots(targetTok, onlyAlive: true);
        if (targets.Count == 0)
        { SendErrorMessage(caller, "OnlyAlive", Localizer["OnlyAlive"]); return; }

        if (caller != null && caller.IsValid)
        {
            targets = targets.Where(t => !CheckImmunity(caller, t)).ToList();
            if (targets.Count == 0)
            { SendErrorMessage(caller, "Punish.ImmunityBlocked", Localizer["Punish.ImmunityBlocked"]); return; }
        }

        foreach (var t in targets)
        {
            try
            {
                var pawn = t.PlayerPawn?.Value; if (pawn == null) continue;
                pawn.TakesDamage = true; pawn.CommitSuicide(false, true);
            }
            catch { }
        }

        string adminName = GetExecutorNameCommand(caller);
        string message = targets.Count == 1 
            ? Localizer["css_slay<player>", adminName, EscapeForStringFormat(targets[0].PlayerName)]
            : Localizer["css_slay<multiple>", adminName, targets.Count];
        
        PlayerExtensions.PrintToAll(message);
    }

    private void CmdRename(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null && (!caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/slay")))
        { if (caller.IsValid) caller.Print(Localizer["NoPermission"]); return; }

        if (info.ArgCount < 3)
        { SendUsageMessage(caller, "Rename.Usage", "Usage: css_rename <target> <new_name>"); return; }

        var targetTok = info.GetArg(1);
        var newName = string.Join(' ', Enumerable.Range(2, info.ArgCount - 2).Select(i => info.GetArg(i)));

        var targets = ResolveTargetsMulti(targetTok, onlyAlive: false);
        if (targets.Count == 0)
        { SendErrorMessage(caller, "NoMatchingClient", Localizer["NoMatchingClient"]); return; }

        var t = targets[0];
        SafeRename(t, newName);
        PlayerExtensions.PrintToAll(Localizer["css_rename", GetExecutorNameCommand(caller), EscapeForStringFormat(t.PlayerName), EscapeForStringFormat(SanitizeName(newName))]);
    }

    private void CmdMoney(CCSPlayerController? caller, CommandInfo info)
    {
        bool isConsoleCommand = caller == null;
        
        if (isConsoleCommand)
        {
            Console.WriteLine("[AdminPlus] Console money command executed.");
        }
        else
        {
            if (caller == null || !caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/slay"))
            {
                caller?.Print(Localizer["NoPermission"]);
                return;
            }
        }

        if (info.ArgCount < 3)
        { SendUsageMessage(caller, "Money.Usage", "Usage: css_money <target> <amount>"); return; }

        var targetTok = info.GetArg(1);
        if (!int.TryParse(info.GetArg(2), NumberStyles.Integer, CultureInfo.InvariantCulture, out int moneyAmount))
        {
            SendErrorMessage(caller, "Money.InvalidAmount", "Invalid money amount!");
            return;
        }

        var teamPlayers = GetPlayersFromTeamInput(targetTok);
        if (teamPlayers.Count > 0)
        {
            HandleTeamMoney(caller, teamPlayers, targetTok, moneyAmount);
            return;
        }

        var targets = ResolveTargetsMultiWithBots(targetTok, onlyAlive: true);
        if (targets.Count == 0)
        { SendErrorMessage(caller, "NoMatchingClient", Localizer["NoMatchingClient"]); return; }

        if (caller != null && caller.IsValid)
        {
            targets = targets.Where(t => !CheckImmunity(caller, t)).ToList();
            if (targets.Count == 0)
            { SendErrorMessage(caller, "Punish.ImmunityBlocked", Localizer["Punish.ImmunityBlocked"]); return; }
        }

        foreach (var target in targets)
        {
            if (target == null || !target.IsValid || !target.PawnIsAlive) continue;
            
            int clampedMoney = Math.Max(0, Math.Min(20000, moneyAmount));
            
            try
            {
                Server.NextFrame(() => {
                    if (target.IsValid && target.PawnIsAlive && target.InGameMoneyServices != null)
                    {
                        target.InGameMoneyServices.Account = clampedMoney;
                        Utilities.SetStateChanged(target, "CCSPlayerController", "m_pInGameMoneyServices");
                        
                    }
                });
            }
            catch { }
        }

        string adminName = GetExecutorNameCommand(caller);
        string message = targets.Count == 1 
            ? Localizer["css_money<player>", adminName, EscapeForStringFormat(targets[0].PlayerName), moneyAmount]
            : Localizer["css_money<multiple>", adminName, targets.Count, moneyAmount];
        
        PlayerExtensions.PrintToAll(message);
    }

    private void CmdArmor(CCSPlayerController? caller, CommandInfo info)
    {
        bool isConsoleCommand = caller == null;
        
        if (isConsoleCommand)
        {
            Console.WriteLine("[AdminPlus] Console armor command executed.");
        }
        else
        {
            if (caller == null || !caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/slay"))
            {
                caller?.Print(Localizer["NoPermission"]);
                return;
            }
        }

        if (info.ArgCount < 3)
        { SendUsageMessage(caller, "Armor.Usage", "Usage: css_armor <target> <amount>"); return; }

        var targetTok = info.GetArg(1);
        if (!int.TryParse(info.GetArg(2), NumberStyles.Integer, CultureInfo.InvariantCulture, out int armorAmount))
        {
            SendErrorMessage(caller, "Armor.InvalidAmount", "Invalid armor amount!");
            return;
        }

        var teamPlayers = GetPlayersFromTeamInput(targetTok);
        if (teamPlayers.Count > 0)
        {
            HandleTeamArmor(caller, teamPlayers, targetTok, armorAmount);
            return;
        }

        var targets = ResolveTargetsMultiWithBots(targetTok, onlyAlive: true);
        if (targets.Count == 0)
        { SendErrorMessage(caller, "NoMatchingClient", Localizer["NoMatchingClient"]); return; }

        if (caller != null && caller.IsValid)
        {
            targets = targets.Where(t => !CheckImmunity(caller, t)).ToList();
            if (targets.Count == 0)
            { SendErrorMessage(caller, "Punish.ImmunityBlocked", Localizer["Punish.ImmunityBlocked"]); return; }
        }

        foreach (var target in targets)
        {
            if (target == null || !target.IsValid || !target.PawnIsAlive) continue;
            
            var pawn = target.PlayerPawn?.Value;
            if (pawn == null) continue;
            
            int clampedArmor = Math.Max(0, Math.Min(500, armorAmount));
            
            try
            {
                Server.NextFrame(() => {
                    if (target.IsValid && target.PawnIsAlive)
                    {
                        var nextFramePawn = target.PlayerPawn?.Value;
                        if (nextFramePawn != null)
                        {
                            nextFramePawn.ArmorValue = clampedArmor;
                            Utilities.SetStateChanged(nextFramePawn, "CCSPlayerPawn", "m_ArmorValue");
                            
                        }
                    }
                });
            }
            catch { }
        }

        string adminName = GetExecutorNameCommand(caller);
        string message = targets.Count == 1 
            ? Localizer["css_armor<player>", adminName, EscapeForStringFormat(targets[0].PlayerName), armorAmount]
            : Localizer["css_armor<multiple>", adminName, targets.Count, armorAmount];
        
        PlayerExtensions.PrintToAll(message);
    }

    private static void SafeRename(CCSPlayerController player, string newname)
    {
        if (player == null || !player.IsValid) return;
        string desired = newname?.Trim() ?? "";
        if (desired.Length == 0) return;
        if (desired.Length > 31) desired = desired.Substring(0, 31);

        try
        {
            player.PlayerName = desired;
            Utilities.SetStateChanged(player, "CBasePlayerController", "m_iszPlayerName");
        }
        catch { }

        try { player.ExecuteClientCommand($"name \"{desired}\""); } catch { }
    }

    private List<CCSPlayerController> ResolveTargetsMulti(string token, bool onlyAlive)
    {
        var players = Utilities.GetPlayers()?.Where(p => p != null && p.IsValid && !p.IsBot).ToList() ?? new();
        if (token.StartsWith("@"))
        {
            var teamList = GetPlayersFromTeamInput(token);
            return onlyAlive ? teamList.Where(p => p.PawnIsAlive).ToList() : teamList;
        }

        if (token.StartsWith("#") && int.TryParse(token.AsSpan(1), out var uid))
        {
            var p = players.FirstOrDefault(x => x.UserId == uid);
            return p != null ? new List<CCSPlayerController> { p } : new();
        }

        var matches = players.Where(p => !string.IsNullOrEmpty(p.PlayerName) && p.PlayerName!.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        return onlyAlive ? matches.Where(p => p.PawnIsAlive).ToList() : matches;
    }

    private List<CCSPlayerController> ResolveTargetsMultiWithBots(string token, bool onlyAlive)
    {
        var players = Utilities.GetPlayers()?.Where(p => p != null && p.IsValid).ToList() ?? new(); // Botlar dahil
        if (token.StartsWith("@"))
        {
            var teamList = GetPlayersFromTeamInput(token);
            return onlyAlive ? teamList.Where(p => p.PawnIsAlive).ToList() : teamList;
        }

        if (token.StartsWith("#") && int.TryParse(token.AsSpan(1), out var uid))
        {
            var p = players.FirstOrDefault(x => x.UserId == uid);
            return p != null ? new List<CCSPlayerController> { p } : new();
        }

        var matches = players.Where(p => !string.IsNullOrEmpty(p.PlayerName) && p.PlayerName!.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        return onlyAlive ? matches.Where(p => p.PawnIsAlive).ToList() : matches;
    }

    private void HandleTeamKick(CCSPlayerController? caller, CommandInfo info, List<CCSPlayerController> teamPlayers, string teamInput)
    {
        if (teamPlayers.Count == 0)
        {
            SendErrorMessage(caller, "Error.NoPlayersInTeam", $"No players found in {GetTeamName(teamInput)} team.");
            return;
        }

        string reason = Localizer["Ban.NoReason"];
        if (info.ArgCount >= 3)
        {
            reason = string.Join(" ", Enumerable.Range(2, info.ArgCount - 2).Select(i => info.GetArg(i)));
        }

        string executorName = GetExecutorNameCommand(caller);
        string teamName = GetTeamName(teamInput);
        int kickedCount = 0;

        foreach (var target in teamPlayers)
        {
            if (target != null && target.IsValid)
            {
                if (caller != null && caller.IsValid && CheckImmunity(caller, target))
                {
                    if (caller.IsValid) caller.Print(Localizer["Kick.ImmunityBlockedPlayer", target.PlayerName]);
                    continue;
                }

                target.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_KICKED);
                kickedCount++;
            }
        }

        if (kickedCount > 0)
        {
            PlayerExtensions.PrintToAll(Localizer["Team.Kick.Success", executorName, teamName, reason]);
            PlayerExtensions.PrintToAll(Localizer["Team.Kick.PlayerCount", kickedCount]);

            if (caller == null)
                Console.WriteLine($"[AdminPlus] Panel {kickedCount} players kicked from {teamName} team: (Reason: {reason})");
        }
    }

    private void HandleTeamMoney(CCSPlayerController? caller, List<CCSPlayerController> teamPlayers, string teamInput, int moneyAmount)
    {
        if (caller == null)
        {
            Console.WriteLine("[AdminPlus] Panel team money command executed.");
        }

        if (teamPlayers.Count == 0)
        {
            SendErrorMessage(caller, "Error.NoPlayersInTeam", $"No players found in {GetTeamName(teamInput)} team.");
            return;
        }

        string executorName = GetExecutorNameCommand(caller);
        string teamName = GetTeamName(teamInput);
        int affectedCount = 0;

        int clampedMoney = Math.Max(0, Math.Min(20000, moneyAmount));

        foreach (var target in teamPlayers)
        {
            if (target != null && target.IsValid && target.PawnIsAlive)
            {
                if (caller != null && caller.IsValid && CheckImmunity(caller, target))
                {
                    if (caller.IsValid) caller.Print(Localizer["Money.ImmunityBlockedPlayer", target.PlayerName]);
                    continue;
                }

                try
                {
                    Server.NextFrame(() => {
                        if (target.IsValid && target.PawnIsAlive && target.InGameMoneyServices != null)
                        {
                            target.InGameMoneyServices.Account = clampedMoney;
                            Utilities.SetStateChanged(target, "CCSPlayerController", "m_pInGameMoneyServices");
                            
                        }
                    });
                    
                    affectedCount++;
                }
                catch { }
            }
        }

        if (affectedCount > 0)
        {
            PlayerExtensions.PrintToAll(Localizer["Team.Money.Success", executorName, teamName, moneyAmount]);
            PlayerExtensions.PrintToAll(Localizer["Team.Money.PlayerCount", affectedCount]);

            if (caller == null)
                Console.WriteLine($"[AdminPlus] Panel {affectedCount} players from {teamName} team received money: {moneyAmount}");
        }
    }

    private void HandleTeamArmor(CCSPlayerController? caller, List<CCSPlayerController> teamPlayers, string teamInput, int armorAmount)
    {
        if (caller == null)
        {
            Console.WriteLine("[AdminPlus] Panel team armor command executed.");
        }

        if (teamPlayers.Count == 0)
        {
            SendErrorMessage(caller, "Error.NoPlayersInTeam", $"No players found in {GetTeamName(teamInput)} team.");
            return;
        }

        string executorName = GetExecutorNameCommand(caller);
        string teamName = GetTeamName(teamInput);
        int affectedCount = 0;

        int clampedArmor = Math.Max(0, Math.Min(500, armorAmount));

        foreach (var target in teamPlayers)
        {
            if (target != null && target.IsValid && target.PawnIsAlive)
            {
                if (caller != null && caller.IsValid && CheckImmunity(caller, target))
                {
                    if (caller.IsValid) caller.Print(Localizer["Armor.ImmunityBlockedPlayer", target.PlayerName]);
                    continue;
                }

                var pawn = target.PlayerPawn?.Value;
                if (pawn != null)
                {
                    try
                    {
                        Server.NextFrame(() => {
                            if (target.IsValid && target.PawnIsAlive)
                            {
                                var nextFramePawn = target.PlayerPawn?.Value;
                                if (nextFramePawn != null)
                                {
                                    nextFramePawn.ArmorValue = clampedArmor;
                                    Utilities.SetStateChanged(nextFramePawn, "CCSPlayerPawn", "m_ArmorValue");
                                    
                                }
                            }
                        });
                        
                        affectedCount++;
                    }
                    catch { }
                }
            }
        }

        if (affectedCount > 0)
        {
            PlayerExtensions.PrintToAll(Localizer["Team.Armor.Success", executorName, teamName, armorAmount]);
            PlayerExtensions.PrintToAll(Localizer["Team.Armor.PlayerCount", affectedCount]);

            if (caller == null)
                Console.WriteLine($"[AdminPlus] Panel {affectedCount} players from {teamName} team received armor: {armorAmount}");
        }
    }

    private void CmdChangeMap(CCSPlayerController? caller, CommandInfo info)
    {
        bool isConsoleCommand = caller == null;
        
        if (isConsoleCommand)
        {
            Console.WriteLine("[AdminPlus] Console map change command executed.");
        }
        else
        {
            if (caller == null || !caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/generic"))
            {
                caller?.Print(Localizer["NoPermission"]);
                return;
            }
        }

        if (info.ArgCount < 2)
        {
            SendUsageMessage(caller, "Map.Usage", "Usage: css_map <map>");
            return;
        }

        var mapArg = info.GetArg(1);
        if (MapAliases.TryGetValue(mapArg, out var aliasMap))
            mapArg = aliasMap;

        ForceChangeMap(caller, mapArg);
    }

    private void CmdChangeWorkshopMap(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null)
        {
            Console.WriteLine("[AdminPlus] Panel workshop map change command executed.");
        }
        else if (!caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/generic"))
        {
            if (caller.IsValid) caller.Print(Localizer["NoPermission"]);
            return;
        }

        if (info.ArgCount < 2)
        {
            SendUsageMessage(caller, "Map.WorkshopUsage", "Usage: css_wsmap <workshop_id>");
            return;
        }

        var workshopId = info.GetArg(1);
        if (!ulong.TryParse(workshopId, out _))
        {
            SendErrorMessage(caller, "Map.InvalidWorkshop", "Invalid Workshop ID!");
            return;
        }

        string executorName = GetExecutorNameCommand(caller);
        PlayerExtensions.PrintToAll(Localizer["Map.WorkshopChanged", executorName, workshopId]);
        Server.ExecuteCommand($"host_workshop_map {workshopId}");

        if (caller == null)
            Console.WriteLine($"[AdminPlus] Panel workshop map changed: {workshopId}");
    }

    private void CmdRcon(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null)
        {
            Console.WriteLine("[AdminPlus] Panel RCON command executed.");
        }
        else if (!caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/root"))
        {
            if (caller.IsValid) caller.Print(Localizer["NoPermission"]);
            return;
        }

        if (info.ArgCount < 2)
        {
            SendUsageMessage(caller, "Rcon.Usage", "Usage: css_rcon <command>");
            return;
        }

        var cmd = string.Join(" ", Enumerable.Range(1, info.ArgCount - 1).Select(i => info.GetArg(i)));
        Server.ExecuteCommand(cmd);

        string executorName = GetExecutorNameCommand(caller);

        if (caller != null && caller.IsValid)
            caller.Print(Localizer["Rcon.Sent", executorName, cmd]);
        else
            Console.WriteLine($"[AdminPlus] Panel RCON command executed: {cmd}");
    }

    private void CmdCvar(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null)
        {
            Console.WriteLine("[AdminPlus] Panel Cvar command executed.");
        }
        else if (!caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/generic"))
        {
            if (caller.IsValid) caller.Print(Localizer["NoPermission"]);
            return;
        }

        if (info.ArgCount < 2)
        {
            SendUsageMessage(caller, "Cvar.Usage", "Usage: css_cvar <cvar> [value]");
            return;
        }

        var cvarName = info.GetArg(1);
        var convar = ConVar.Find(cvarName);

        if (convar == null)
        {
            SendErrorMessage(caller, "Cvar.NotFound", $"Cvar not found: {cvarName}", cvarName);
            return;
        }

        if (info.ArgCount == 2)
        {
            string response = $"{cvarName} = {convar.StringValue}";
            if (caller != null && caller.IsValid)
                caller.Print(response);
            else
                Console.WriteLine(response);
            return;
        }

        var newValue = string.Join(" ", info.ArgRange(2, info.ArgCount - 2));

        if (cvarName.Equals("sv_cheats", StringComparison.OrdinalIgnoreCase))
        {
            SendErrorMessage(caller, "Cvar.CheatsBlocked", "sv_cheats cannot be changed!");
            return;
        }

        var oldValue = convar.StringValue;
        convar.SetValue(newValue);

        string executorName = GetExecutorNameCommand(caller);
        string message = Localizer["Cvar.Changed", executorName, oldValue, newValue];

        if (caller != null && caller.IsValid)
            caller.Print(message);
        else
            Console.WriteLine($"[AdminPlus] {message}");
        
        PlayerExtensions.PrintToAll(message);
    }


    private void CmdWho(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null)
        {
            Console.WriteLine("[AdminPlus] Who komutu çalıştırıldı.");
        }
        else if (!caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/generic"))
        {
            if (caller.IsValid) caller.Print(Localizer["NoPermission"]);
            return;
        }

        var players = Utilities.GetPlayers().Where(p => p != null && p.IsValid && !p.IsBot).ToList();
        if (players.Count == 0)
        {
            SendErrorMessage(caller, "NoMatchingClient", "No matching player found.");
            return;
        }

        foreach (var p in players)
        {
            uint imm = AdminManager.GetPlayerImmunity(p);
            string line = $"[{p.PlayerName}] [{p.SteamID}] [{p.IpAddress ?? "-"}] [Imm:{imm}]";

            if (caller != null && caller.IsValid)
                caller.PrintToConsole(line);
            else
                Console.WriteLine(line);
        }
    }

    private void CmdRestartRound(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null)
        {
            Console.WriteLine("[AdminPlus] Panel round restart command executed.");
        }
        else if (!caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/generic"))
        {
            if (caller.IsValid) caller.Print(Localizer["NoPermission"]);
            return;
        }

        Server.ExecuteCommand("mp_restartgame 1");

        string executorName = GetExecutorNameCommand(caller);
        PlayerExtensions.PrintToAll(Localizer["Round.Restarted", executorName]);

        if (caller == null)
            Console.WriteLine("[AdminPlus] Panel round restarted.");
    }

    private void CmdPlayers(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null && !AdminManager.PlayerHasPermissions(caller, "@css/root"))
            return;

        Console.WriteLine("--------- PLAYER LIST ---------");
        foreach (var p in Utilities.GetPlayers().Where(p => p != null && p.IsValid && !p.IsBot))
        {
            Console.WriteLine($"• [#{p.UserId}] \"{p.PlayerName}\" (IP: \"{p.IpAddress ?? "-"}\" SteamID64: \"{p.SteamID}\")");
        }
        Console.WriteLine("--------- END LIST ---------");
    }

    private void ForceChangeMap(CCSPlayerController? caller, string mapName)
    {
        if (mapName.StartsWith("workshop/", StringComparison.OrdinalIgnoreCase))
        {
            string workshopId = mapName.Replace("workshop/", "");
            Server.ExecuteCommand($"host_workshop_map {workshopId}");

            var executorName = GetExecutorNameCommand(caller);
            PlayerExtensions.PrintToAll(Localizer["Map.WorkshopChanged", executorName, workshopId]);
            return;
        }

        if (InstalledMaps.Count > 0 && !InstalledMaps.Contains(mapName))
        {
            SendErrorMessage(caller, "Map.NotFound", $"Map not found: {mapName}", mapName);
            return;
        }

        var executor = GetExecutorNameCommand(caller);
        var targetMap = mapName;

        PlayerExtensions.PrintToAll(Localizer["Map.Changed", executor, targetMap]);

        AddTimer(4.0f, () =>
        {
            try
            {
                Server.ExecuteCommand($"changelevel {targetMap}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AdminPlus] Map change error: {ex.Message}");
                SendErrorMessage(caller, "Map.ChangeError", $"Map change error: {targetMap}", targetMap);
            }
        });
    }

    private CCSPlayerController? FindPlayerByNameOrId(string input)
    {
        var players = Utilities.GetPlayers();
        if (players == null) return null;

        if (input.StartsWith("#") && int.TryParse(input[1..], out var userId))
        {
            return players.FirstOrDefault(p => p.IsValid && p.UserId == userId);
        }

        var target = players.FirstOrDefault(p => p.IsValid && p.SteamID.ToString() == input);
        if (target != null) return target;

        var matchingPlayers = players
            .Where(p => p.IsValid && !string.IsNullOrEmpty(p.PlayerName))
            .Where(p => p.PlayerName.Contains(input, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matchingPlayers.Count == 0)
            return null;

        if (matchingPlayers.Count == 1)
            return matchingPlayers[0];

        return matchingPlayers.FirstOrDefault();
    }

    private CCSPlayerController? GetPlayerTarget(CCSPlayerController? caller, CommandInfo info, int argIndex, out string errorMessage)
    {
        errorMessage = string.Empty;
        
        if (info.ArgCount <= argIndex)
        {
            errorMessage = "No target specified.";
            return null;
        }

        string targetInput = info.GetArg(argIndex);
        if (string.IsNullOrWhiteSpace(targetInput))
        {
            errorMessage = "Empty target specified.";
            return null;
        }

        var target = FindPlayerByNameOrId(targetInput);
        if (target == null)
        {
            errorMessage = "No matching player found.";
            return null;
        }

        if (caller != null && caller.IsValid && CheckImmunity(caller, target))
        {
            errorMessage = "Target has immunity.";
            return null;
        }

        return target;
    }

    private string GetExecutorNameCommand(CCSPlayerController? caller)
    {
        if (caller != null && caller.IsValid)
            return caller.PlayerName;
        if (!string.IsNullOrEmpty(AdminPlus._menuInvokerName))
            return AdminPlus._menuInvokerName!;
        return Localizer["Console"];
    }

    private void SendUsageMessage(CCSPlayerController? caller, string localeKey, string consoleMessage)
    {
        if (caller != null && caller.IsValid)
        {
            var message = Localizer[localeKey];
            if (message == localeKey)
            {
                caller.Print(consoleMessage);
            }
            else
            {
                caller.Print(message);
            }
        }
        else
        {
            Console.WriteLine(consoleMessage);
        }
    }

    private void SendErrorMessage(CCSPlayerController? caller, string localeKey, string consoleMessage, params object[] args)
    {
        if (caller != null && caller.IsValid)
        {
            if (args.Length > 0)
                caller.Print(string.Format(Localizer[localeKey], args));
            else
                caller.Print(Localizer[localeKey]);
        }
        else
        {
            Console.WriteLine(consoleMessage);
        }
    }

    private static string EscapeForStringFormat(string? s)
    {
        if (string.IsNullOrEmpty(s)) return s ?? string.Empty;
        return s.Replace("{", "{{").Replace("}", "}}");
    }
}

public static class CommandInfoExtensions
{
    public static IEnumerable<string> ArgRange(this CommandInfo info, int startIndex, int count = -1)
    {
        if (count == -1) count = info.ArgCount - startIndex;
        for (int i = startIndex; i < startIndex + count && i < info.ArgCount; i++)
        {
            yield return info.GetArg(i);
        }
    }
}