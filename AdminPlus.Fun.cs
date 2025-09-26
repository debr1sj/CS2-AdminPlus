using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Collections.Concurrent;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.UserMessages;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.ValveConstants.Protobuf;

namespace AdminPlus;

public partial class AdminPlus
{
    internal static string? _menuInvokerName;
    private enum FunTimer { Beacon, Freeze, FreezeLoop, Shake, Drug, DrugEnd, Blind }
    private readonly Dictionary<ulong, Dictionary<FunTimer, Timer>> _funTimers = new();
    private readonly Dictionary<ulong, Vector> _lastDeathPosFun = new();
    private readonly Dictionary<ulong, MoveType_t> _prevMoveType = new();
    private readonly Dictionary<ulong, Vector> _freezeAnchor = new();
    private readonly Dictionary<ulong, CEnvShake> _activeShakes = new();
    private readonly Dictionary<ulong, float> _prevGravityScale = new();

    private int _ctDefaultHealth = 100;
    private int _tDefaultHealth = 100;

    [GameEventHandler]
    public HookResult Fun_OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var p = @event.Userid;
        if (p == null || !p.IsValid) return HookResult.Continue;

        RemoveAllFunTimers(p);

        _lastDeathPosFun.Remove(p.SteamID);

        AddTimer(0.05f, () =>
        {
            if (!p.IsValid) return;
            if (p.Team == CsTeam.CounterTerrorist) SetHealth(p, _ctDefaultHealth);
            else if (p.Team == CsTeam.Terrorist) SetHealth(p, _tDefaultHealth);
        });


        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult Fun_OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        var p = @event.Userid;
        if (p == null) return HookResult.Continue;

        RemoveAllFunTimers(p);

        var pos = p.PlayerPawn?.Value?.AbsOrigin;
        if (pos != null)
        {
            _lastDeathPosFun[p.SteamID] = pos;
            p.CopyLastCoord();
        }

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult Fun_OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var p = @event.Userid;
        if (p == null) return HookResult.Continue;
        RemoveAllFunTimers(p);
        _lastDeathPosFun.Remove(p.SteamID);
        p.RemoveLastCoord();
        return HookResult.Continue;
    }

    public void RegisterFunCommands()
    {
        AddPermCmd("@css/slay", "css_freeze", "<#userid|name|all @ commands> [time]", CmdFreeze);
        AddPermCmd("@css/slay", "css_unfreeze", "<#userid|name|all @ commands>", CmdUnfreeze);
        AddPermCmd("@css/slay", "css_gravity", "<gravity>", CmdGravity);
        AddPermCmd("@css/slay", "css_bury", "<#userid|name|all @ commands>", CmdBury);
        AddPermCmd("@css/slay", "css_unbury", "<#userid|name|all @ commands>", CmdUnBury);
        AddPermCmd("@css/slay", "css_beacon", "<#userid|name|all @ commands> <0|1>", CmdBeacon);
        AddPermCmd("@css/slay", "css_shake", "<#userid|name|all @ commands> <time>", CmdShake);
        AddPermCmd("@css/slay", "css_unshake", "<#userid|name|all @ commands>", CmdUnShake);
        AddPermCmd("@css/slay", "css_blind", "<#userid|name|all @ commands> <time>", CmdBlind);
        AddPermCmd("@css/slay", "css_unblind", "<#userid|name|all @ commands>", CmdUnBlind);
        AddPermCmd("@css/slay", "css_clean", "- Clean weapons on the ground", CmdClean);
        AddPermCmd("@css/slay", "css_goto", "<#userid|name> - Teleport to player", CmdGoto);
        AddPermCmd("@css/slay", "css_bring", "<#userid|name|all @ commands> - Bring", CmdBring);
        AddPermCmd("@css/slay", "css_hrespawn", "<#userid|name> - Respawn at last pos", CmdHRespawn);
        AddPermCmd("@css/slay", "css_1up", "<#userid|name> - Respawn at last pos", CmdHRespawn);
        AddPermCmd("@css/slay", "css_drug", "<#userid|name|all @ commands> <time>", CmdDrug);
        AddPermCmd("@css/slay", "css_undrug", "<#userid|name|all @ commands>", CmdUnDrug);
        AddPermCmd("@css/slay", "css_glow", "<#userid|name|all @ commands> <color>", CmdGlow);
        AddPermCmd("@css/slay", "css_color", "<#userid|name|all @ commands> <color>", CmdColor);

        AddPermCmd("@css/cheats", "css_revive", "<#userid|name|all @ commands>", CmdRespawn);
        AddPermCmd("@css/cheats", "css_respawn", "<#userid|name|all @ commands>", CmdRespawn);
        AddPermCmd("@css/cheats", "css_noclip", "<#userid|name|all @ commands> <0|1>", CmdNoclip);
        AddPermCmd("@css/cheats", "css_weapon", "<#userid|name|all @ commands> <weapon>", CmdWeapon);
        AddPermCmd("@css/cheats", "css_strip", "<#userid|name|all @ commands> [filter]", CmdStrip);
        AddPermCmd("@css/cheats", "css_sethp", "<team> <health>", CmdSetTeamHp);
        AddPermCmd("@css/cheats", "css_hp", "<#userid|name|all @ commands> <health>", CmdHp);
        AddPermCmd("@css/cheats", "css_speed", "<#userid|name|all @ commands> <value>", CmdSpeed);
        AddPermCmd("@css/cheats", "css_unspeed", "<#userid|name|all @ commands>", CmdUnSpeed);
        AddPermCmd("@css/cheats", "css_god", "<#userid|name|all @ commands> <0|1>", CmdGod);

        AddPermCmd("@css/kick", "css_team", "<#userid|name|all @ commands> <t|ct|spec>", CmdTeam);
        AddPermCmd("@css/kick", "css_swap", "<#userid|name>", CmdSwap);
    }

    private void AddPermCmd(string perm, string name, string usage, Action<CCSPlayerController?, CommandInfo> handler)
    {
        AddCommand(name, usage, (caller, info) =>
        {
            if (caller != null && caller.IsValid && !AdminManager.PlayerHasPermissions(caller, perm))
            { caller.Print(Localizer["NoPermission"]); return; }
            handler(caller, info);
        });
    }

    private enum TargetScope { Single, TeamT, TeamCT, TeamSpec, All }

    private bool TryParseTeamSelector(string token, out CsTeam team, out TargetScope scope)
    {
        team = CsTeam.None; scope = TargetScope.Single;
        switch (token.Trim().ToLowerInvariant())
        {
            case "@t":
            case "@terrorist": team = CsTeam.Terrorist; scope = TargetScope.TeamT; return true;
            case "@ct":
            case "@counterterrorist": team = CsTeam.CounterTerrorist; scope = TargetScope.TeamCT; return true;
            case "@spec":
            case "@spectator": team = CsTeam.Spectator; scope = TargetScope.TeamSpec; return true;
            case "t":
            case "terrorist": team = CsTeam.Terrorist; scope = TargetScope.TeamT; return true;
            case "ct":
            case "counter":
            case "counterterrorist": team = CsTeam.CounterTerrorist; scope = TargetScope.TeamCT; return true;
            case "spec":
            case "spectator": team = CsTeam.Spectator; scope = TargetScope.TeamSpec; return true;
            case "@all":
            case "all":
            case "@*": scope = TargetScope.All; return true;
            default: return false;
        }
    }

    private string TeamDisplay(TargetScope s) => s switch
    {
        TargetScope.TeamT => Localizer["Team.T"],
        TargetScope.TeamCT => Localizer["Team.CT"],
        TargetScope.TeamSpec => Localizer["Team.SPEC"],
        TargetScope.All => Localizer["Team.ALL"],
        _ => ""
    };

    private bool ResolveTargets(
        CCSPlayerController? invoker, string token,
        bool checkImmunity, bool onlyAlive, bool onlyDead,
        out List<CCSPlayerController> targets, out string adminName, out string targetLabel, out TargetScope scope,
        bool allowBots = false)
    {
        targets = new();
        var fallback = _menuInvokerName;
        adminName = (invoker == null || !invoker.IsValid) ? (string.IsNullOrEmpty(fallback) ? Localizer["Console"] : fallback) : invoker.PlayerName;
        targetLabel = ""; scope = TargetScope.Single;
        if (string.IsNullOrWhiteSpace(token)) return false;

        if (TryParseTeamSelector(token, out var selTeam, out scope))
        {
            var q = Utilities.GetPlayers().Where(p => p != null && p.IsValid);
            if (!allowBots) q = q.Where(p => !p.IsBot);
            if (scope == TargetScope.All)
            {
                targets = q.ToList()!;
                targetLabel = Localizer["Team.ALL"];
            }
            else
            {
                targets = q.Where(p => p.Team == selTeam).ToList()!;
                targetLabel = TeamDisplay(scope);
            }
        }
        else if (token.StartsWith("#") && int.TryParse(token.AsSpan(1), out var uid))
        {
            var p = Utilities.GetPlayers().FirstOrDefault(x => (x.UserId ?? 0) == uid);
            if (p == null || (!allowBots && p.IsBot)) { SendErrorMessage(invoker, "NoMatchingClient", Localizer["NoMatchingClient"]); return false; }
            targets.Add(p);
            targetLabel = p.PlayerName ?? p.SteamID.ToString();
        }
        else
        {
            var q = Utilities.GetPlayers().Where(p => p != null && p.IsValid && !string.IsNullOrWhiteSpace(p.PlayerName));
            if (!allowBots) q = q.Where(p => !p.IsBot);

            var list = q.Where(p => p.PlayerName!.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0).ToList()!;
            if (list.Count == 0) { SendErrorMessage(invoker, "NoMatchingClient", Localizer["NoMatchingClient"]); return false; }
            if (list.Count > 1) { SendErrorMessage(invoker, "MoreThanOneClient", Localizer["MoreThanOneClient"]); return false; }
            targets.Add(list[0]);
            targetLabel = list[0].PlayerName!;
        }

        if (onlyAlive) targets = targets.Where(p => p.PawnIsAlive).ToList();
        if (onlyDead) targets = targets.Where(p => !p.PawnIsAlive).ToList();

        if (checkImmunity && invoker != null && invoker.IsValid)
            targets = targets.Where(t => !CheckImmunity(invoker, t)).ToList();

        return targets.Count > 0;
    }

    private void Announce(string key1, string keyN, string admin, string label, params object[] extra)
    {
        var args = new object[] { admin, label }.Concat(extra).ToArray();
        string msg = Localizer[keyN, args];
        if (!string.IsNullOrEmpty(label) && !label.Equals(Localizer["Team.ALL"], StringComparison.OrdinalIgnoreCase) && extra.Length > 0)
            msg = Localizer[key1, args];

        foreach (var pl in Utilities.GetPlayers())
            if (pl.IsValid) pl.Print(msg);
    }

    private void RememberTimer(CCSPlayerController p, FunTimer key, Timer t)
    {
        if (!_funTimers.TryGetValue(p.SteamID, out var map)) { map = new(); _funTimers[p.SteamID] = map; }
        map[key] = t;
    }
    private void StopTimer(CCSPlayerController p, FunTimer key)
    {
        if (_funTimers.TryGetValue(p.SteamID, out var map) && map.TryGetValue(key, out var t))
        { try { t.Kill(); } catch { } map.Remove(key); if (map.Count == 0) _funTimers.Remove(p.SteamID); }
    }
    private void RemoveAllFunTimers(CCSPlayerController p)
    {
        if (_funTimers.TryGetValue(p.SteamID, out var map))
        { 
            foreach (var t in map.Values) 
            { 
                try { t.Kill(); } 
                catch { } 
            } 
            _funTimers.Remove(p.SteamID); 
        }
    }
    
    private void CleanupAllFunTimers()
    {
        try
        {
            foreach (var playerTimers in _funTimers.Values)
            {
                foreach (var timer in playerTimers.Values)
                {
                    try { timer.Kill(); } catch { }
                }
            }
            _funTimers.Clear();
            _lastDeathPosFun.Clear();
            _prevMoveType.Clear();
            _freezeAnchor.Clear();
            _activeShakes.Clear();
            _prevGravityScale.Clear();
            
            Console.WriteLine("[AdminPlus] All fun timers and data cleaned up.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminPlus] Error during fun cleanup: {ex.Message}");
        }
    }

    private static void SetHealth(CCSPlayerController target, int hp)
    {
        var pawn = target.PlayerPawn?.Value; if (pawn == null) return;
        target.Health = hp; pawn.Health = hp;
        if (hp > 100) { target.MaxHealth = hp; pawn.MaxHealth = hp; }
        Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iHealth");
    }
    private static void SetSpeed(CCSPlayerController target, float mult)
    {
        var pawn = target.PlayerPawn?.Value; if (pawn == null) return;
        pawn.VelocityModifier = mult;
        Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_flVelocityModifier");
    }
    private static void SetGod(CCSPlayerController target, bool enabled)
    {
        var pawn = target.PlayerPawn?.Value; if (pawn == null) return;
        pawn.TakesDamage = !enabled;
    }
    private static void SetNoclip(CCSPlayerController target, bool on)
    {
        var pawn = target.PlayerPawn?.Value; if (pawn == null) return;
        try { pawn.MoveType = on ? MoveType_t.MOVETYPE_NOCLIP : MoveType_t.MOVETYPE_WALK; }
        catch { pawn.VelocityModifier = on ? 2.5f : 1.0f; }
        Utilities.SetStateChanged(pawn, "CBaseEntity", "m_MoveType");
    }
    
    private static bool HasActiveTimer(CCSPlayerController player, FunTimer timerType)
    {
        if (_instance?._funTimers == null) return false;
        if (!_instance._funTimers.TryGetValue(player.SteamID, out var timers)) return false;
        if (timers == null) return false;
        return timers.TryGetValue(timerType, out var timer) && timer != null;
    }
    
    private static bool IsGodModeActive(CCSPlayerController player)
    {
        var pawn = player.PlayerPawn?.Value;
        return pawn != null && !pawn.TakesDamage;
    }
    private static void BuryPawn(CCSPlayerController target, float dz)
    {
        var pawn = target.PlayerPawn?.Value; if (pawn?.AbsOrigin == null) return;
        var o = pawn.AbsOrigin!;
        var v = new Vector(o.X, o.Y, o.Z + dz);
        pawn.Teleport(v, pawn.AbsRotation, new Vector(0, 0, 0));
    }
    private static void GlowPawn(CCSPlayerController target, Color color)
    {
        var pawn = target.PlayerPawn?.Value; if (pawn == null) return;
        pawn.RenderMode = RenderMode_t.kRenderTransColor;
        pawn.Render = color;
        Utilities.SetStateChanged(pawn, "CBaseModelEntity", "m_clrRender");
    }
    private static bool TryParseColor(string s, out Color color)
    {
        color = Color.White;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim();
        try
        {
            if (s.StartsWith("#"))
            {
                var hex = s[1..];
                if (hex.Length == 6) { color = ColorTranslator.FromHtml("#" + hex); return true; }
                if (hex.Length == 8) { color = Color.FromArgb(int.Parse(hex, NumberStyles.HexNumber)); return true; }
            }
            else
            {
                color = Color.FromName(s);
                if (color.A == 0 && color.R == 0 && color.G == 0 && color.B == 0 && !string.Equals(s, "black", StringComparison.OrdinalIgnoreCase))
                    return false;
                return true;
            }
        }
        catch { }
        return false;
    }

    private static void ColorScreen(CCSPlayerController player, Color color, float hold, float fade, bool stayout, bool purge)
    {
        try
        {
            var msg = UserMessage.FromPartialName("Fade");
            msg.SetInt("duration", Convert.ToInt32(fade * 512));
            msg.SetInt("hold_time", Convert.ToInt32(hold * 512));
            int flags = 0x0001; // FADE_IN
            if (hold <= 0) flags = 0x0002; // FADE_OUT
            if (stayout) flags |= 0x0008; // STAYOUT
            if (purge) flags |= 0x0010; // PURGE
            msg.SetInt("flags", flags);
            msg.SetInt("color", color.R | (color.G << 8) | (color.B << 16) | (color.A << 24));
            msg.Send(player);
        }
        catch { }
    }

    private static void SetActualMoveType(CBaseEntity pawn, int value)
    {
        try { Schema.SetSchemaValue(pawn.Handle, "CBaseEntity", "m_nActualMoveType", value); } catch { }
    }

    private static void ZeroVelocity(CCSPlayerController t)
    {
        var pawn = t.PlayerPawn?.Value;
        if (pawn == null || pawn.AbsOrigin == null || pawn.AbsRotation == null) return;

        pawn.Teleport(pawn.AbsOrigin, pawn.AbsRotation, new Vector(0, 0, 0));
        pawn.VelocityModifier = 0f;
        Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_flVelocityModifier");
    }

    private static void TinyViewPunch(CCSPlayerController t, float strength = 1.8f)
    {
        var pawn = t.PlayerPawn?.Value; if (pawn?.AbsRotation == null) return;
        var ang = pawn.AbsRotation!;
        float pitch = (float)(Random.Shared.NextDouble() * 2 - 1) * strength;
        float yaw = (float)(Random.Shared.NextDouble() * 2 - 1) * strength;

        var newAng = new QAngle(ClampAngle(ang.X + pitch), ClampAngle(ang.Y + yaw), 0);
        pawn.Teleport(pawn.AbsOrigin, newAng, pawn.AbsVelocity);
    }

    private static float ClampAngle(float a)
    {
        while (a > 89f) a -= 180f;
        while (a < -89f) a += 180f;
        return a;
    }

    private static bool TryFlashBlind(CCSPlayerController t, float seconds)
    {
        try
        {
            var pawn = t.PlayerPawn?.Value;
            if (pawn == null) return false;
            pawn.FlashMaxAlpha = 255.0f; Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_flFlashMaxAlpha");
            pawn.FlashDuration = seconds; Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_flFlashDuration");
            return true;
        }
        catch { return false; }
    }

    private static void OverlayBlindOn(CCSPlayerController t)
    {
        try { t.ExecuteClientCommand("r_screenoverlay effects/hud_white"); } catch { }
    }
    private static void OverlayBlindOff(CCSPlayerController t)
    {
        try { t.ExecuteClientCommand("r_screenoverlay \"\""); } catch { }
    }

    private void CmdBeacon(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2) { SendUsageMessage(caller, "Fun.Beacon.Usage", "Usage: css_beacon <target> [0|1]"); return; }
        if (!ResolveTargets(caller, info.GetArg(1), false, true, false, out var players, out var admin, out var label, out _, allowBots: true))
        { SendErrorMessage(caller, "OnlyAlive", Localizer["OnlyAlive"]); return; }
        
        int val = -1; // -1 = toggle, 0 = off, 1 = on
        if (info.ArgCount >= 3)
        {
            if (!int.TryParse(info.GetArg(2), out val)) { SendErrorMessage(caller, "MustBeInteger", Localizer["MustBeInteger"]); return; }
        }

        foreach (var t in players)
        {
            bool wasBeaconActive = HasActiveTimer(t, FunTimer.Beacon);
            
            bool shouldActivate = val switch
            {
                -1 => !wasBeaconActive, 
                0 => false,            
                1 => true,             
                _ => !wasBeaconActive  
            };
            
            string action = shouldActivate ? "applied" : "removed";
            
            if (shouldActivate)
            {
                StopTimer(t, FunTimer.Beacon);
                var beaconTimer = AddTimer(3.0f, () =>
                {
                    try { t.ExecuteClientCommand("play sounds/tools/sfm/beep.vsnd_c"); } catch { }
                    var pawn = t.PlayerPawn?.Value; var origin = pawn?.AbsOrigin; if (origin == null) return;
                    const int lines = 20; const float initialRadius = 20f; const float step = (float)(2 * Math.PI) / lines;
                    float angle = 0f; var color = t.Team == CsTeam.Terrorist ? Color.Red : Color.Blue;
                    var beams = new List<CBeam>();
                    for (int i = 0; i < lines; i++)
                    {
                        var start = new Vector((float)(origin.X + initialRadius * Math.Cos(angle)), (float)(origin.Y + initialRadius * Math.Sin(angle)), origin.Z + 6f);
                        angle += step;
                        var end = new Vector((float)(origin.X + initialRadius * Math.Cos(angle)), (float)(origin.Y + initialRadius * Math.Sin(angle)), origin.Z + 6f);
                        var beam = Utilities.CreateEntityByName<CBeam>("beam");
                        if (beam == null) continue; beam.Render = color; beam.Width = 2.0f; beam.Teleport(start, new QAngle(), new Vector());
                        beam.EndPos.X = end.X; beam.EndPos.Y = end.Y; beam.EndPos.Z = end.Z; beam.DispatchSpawn();
                        AddTimer(1.0f, () => { if (beam != null && beam.IsValid) beam.Remove(); });
                        beams.Add(beam);
                    }
                }, TimerFlags.REPEAT);
                RememberTimer(t, FunTimer.Beacon, beaconTimer);
            }
            else StopTimer(t, FunTimer.Beacon);
            
            Announce("css_beacon<player>", "css_beacon<multiple>", admin, label, action);
            AdminPlus.LogAction($"{admin} {action} beacon to {label}");
            
        }
        
        AddTimer(0.1f, () => {
            foreach (var target in players)
            {
                bool wasBeaconActive = HasActiveTimer(target, FunTimer.Beacon);
                bool shouldActivate = val switch
                {
                    -1 => !wasBeaconActive,
                    0 => false,
                    1 => true,
                    _ => !wasBeaconActive
                };
                string action = shouldActivate ? "applied" : "removed";
                _ = Discord.SendAdminActionLog("beacon", target.PlayerName, target.SteamID, admin, caller?.SteamID ?? 0UL, action, this);
            }
        });
    }

    private void CmdFreeze(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2) { SendUsageMessage(caller, "Fun.Freeze.Usage", "Usage: css_freeze <target> [seconds]"); return; }
        if (!ResolveTargets(caller, info.GetArg(1), true, true, false, out var players, out var admin, out var label, out _, true))
        { SendErrorMessage(caller, "OnlyAlive", Localizer["OnlyAlive"]); return; }

        float seconds = -1f;
        if (info.ArgCount >= 3 && float.TryParse(info.GetArg(2), NumberStyles.Float, CultureInfo.InvariantCulture, out var s) && s > 0) seconds = s;

        foreach (var t in players)
        {
            var pawn = t.PlayerPawn?.Value; if (pawn == null) continue;

            if (!_prevMoveType.ContainsKey(t.SteamID)) _prevMoveType[t.SteamID] = pawn.MoveType;
            if (pawn.AbsOrigin != null) _freezeAnchor[t.SteamID] = pawn.AbsOrigin;

            try { pawn.MoveType = MoveType_t.MOVETYPE_OBSOLETE; } catch { }
            SetActualMoveType(pawn, (int)MoveType_t.MOVETYPE_OBSOLETE);
            pawn.VelocityModifier = 0f; Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_flVelocityModifier");
            try { if (!_prevGravityScale.ContainsKey(t.SteamID)) _prevGravityScale[t.SteamID] = pawn.GravityScale; pawn.GravityScale = 0f; Utilities.SetStateChanged(pawn, "CBaseEntity", "m_flGravityScale"); } catch { }
            if (_freezeAnchor.TryGetValue(t.SteamID, out var anchor))
                pawn.Teleport(anchor, pawn.AbsRotation, new Vector(0, 0, 0));
            Utilities.SetStateChanged(pawn, "CBaseEntity", "m_MoveType");

            try { t.ExecuteClientCommand("play sounds/tools/sfm/beep.vsnd_c"); } catch { }

            StopTimer(t, FunTimer.FreezeLoop);
            RememberTimer(t, FunTimer.FreezeLoop, AddTimer(0.02f, () =>
            {
                if (!t.IsValid) { StopTimer(t, FunTimer.FreezeLoop); return; }
                var p = t.PlayerPawn?.Value; if (p == null) return;
                try { p.MoveType = MoveType_t.MOVETYPE_OBSOLETE; } catch { }
                SetActualMoveType(p, (int)MoveType_t.MOVETYPE_OBSOLETE);
                if (_freezeAnchor.TryGetValue(t.SteamID, out var a)) p.Teleport(a, null, new Vector(0, 0, 0));
            }, TimerFlags.REPEAT));

            if (seconds > 0)
            {
                StopTimer(t, FunTimer.Freeze);
                RememberTimer(t, FunTimer.Freeze, AddTimer(seconds, () =>
                {
                    if (!t.IsValid) { StopTimer(t, FunTimer.FreezeLoop); StopTimer(t, FunTimer.Freeze); return; }
                    var p = t.PlayerPawn?.Value; if (p == null) return;
                    StopTimer(t, FunTimer.FreezeLoop);
                    if (_prevMoveType.TryGetValue(t.SteamID, out var prev)) { p.MoveType = prev; SetActualMoveType(p, (int)prev); _prevMoveType.Remove(t.SteamID); }
                    else { p.MoveType = MoveType_t.MOVETYPE_WALK; SetActualMoveType(p, (int)MoveType_t.MOVETYPE_WALK); }
                    p.VelocityModifier = 1f; Utilities.SetStateChanged(p, "CCSPlayerPawn", "m_flVelocityModifier");
                    AddTimer(0.05f, () => { if (p != null && p.IsValid) { try { p.MoveType = MoveType_t.MOVETYPE_WALK; } catch { } SetActualMoveType(p, 2); Utilities.SetStateChanged(p, "CBaseEntity", "m_MoveType"); } });
                    AddTimer(0.10f, () => { if (p != null && p.IsValid) { p.VelocityModifier = 1f; Utilities.SetStateChanged(p, "CCSPlayerPawn", "m_flVelocityModifier"); } });
                    try { if (p.AbsOrigin != null && p.AbsRotation != null) p.Teleport(p.AbsOrigin, p.AbsRotation, new Vector(0, 0, 0)); } catch { }
                    try { if (_prevGravityScale.TryGetValue(t.SteamID, out var g)) { p.GravityScale = g; _prevGravityScale.Remove(t.SteamID); Utilities.SetStateChanged(p, "CBaseEntity", "m_flGravityScale"); } } catch { }
                    try { t.ExecuteClientCommand("play sounds/tools/sfm/beep.vsnd_c"); } catch { }
                }));
            }
        }
        string duration = seconds > 0 ? $"{seconds}" : "permanent";
        Announce("css_freeze<player>", "css_freeze<multiple>", admin, label, duration);
        
        AdminPlus.LogAction($"{admin} froze {label} for {duration}");
        
        AddTimer(0.1f, () => {
            foreach (var target in players)
            {
                _ = Discord.SendAdminActionLog("freeze", target.PlayerName, target.SteamID, admin, caller?.SteamID ?? 0UL, duration, this);
            }
        });
    }

    private void CmdUnfreeze(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2) { SendUsageMessage(caller, "Fun.Unfreeze.Usage", "Usage: css_unfreeze <target>"); return; }
        if (!ResolveTargets(caller, info.GetArg(1), true, true, false, out var players, out var admin, out var label, out _, true))
        { SendErrorMessage(caller, "OnlyAlive", Localizer["OnlyAlive"]); return; }

        foreach (var t in players)
        {
            var pawn = t.PlayerPawn?.Value; if (pawn == null) continue;
            StopTimer(t, FunTimer.FreezeLoop);
            StopTimer(t, FunTimer.Freeze);
            if (_prevMoveType.TryGetValue(t.SteamID, out var prev)) { pawn.MoveType = prev; SetActualMoveType(pawn, prev == MoveType_t.MOVETYPE_NOCLIP ? 8 : 2); _prevMoveType.Remove(t.SteamID); }
            else { try { pawn.MoveType = MoveType_t.MOVETYPE_WALK; } catch { } SetActualMoveType(pawn, 2); }
            pawn.VelocityModifier = 1f; Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_flVelocityModifier");
            AddTimer(0.05f, () => { if (pawn != null && pawn.IsValid) { try { pawn.MoveType = MoveType_t.MOVETYPE_WALK; } catch { } SetActualMoveType(pawn, 2); Utilities.SetStateChanged(pawn, "CBaseEntity", "m_MoveType"); } });
            AddTimer(0.10f, () => { if (pawn != null && pawn.IsValid) { pawn.VelocityModifier = 1f; Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_flVelocityModifier"); } });
            try { if (pawn.AbsOrigin != null && pawn.AbsRotation != null) pawn.Teleport(pawn.AbsOrigin, pawn.AbsRotation, new Vector(0, 0, 0)); } catch { }
            _freezeAnchor.Remove(t.SteamID);
        }
        Announce("css_unfreeze<player>", "css_unfreeze<multiple>", admin, label);
        
        AdminPlus.LogAction($"{admin} unfroze {label}");
        
        AddTimer(0.1f, () => {
            foreach (var target in players)
            {
                _ = Discord.SendAdminActionLog("unfreeze", target.PlayerName, target.SteamID, admin, caller?.SteamID ?? 0UL, "", this);
            }
        });
    }

    private void CmdGravity(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2 || !int.TryParse(info.GetArg(1), out var value))
        { SendErrorMessage(caller, "MustBeInteger", Localizer["MustBeInteger"]); return; }

        var cvar = ConVar.Find("sv_gravity");
        if (cvar == null) { SendErrorMessage(caller, "CvarNotFound", Localizer["CvarNotFound", "sv_gravity"]); return; }

        var oldValue = cvar.GetPrimitiveValue<float>();
        Server.ExecuteCommand($"sv_gravity {value}");
        var admin = (caller == null || !caller.IsValid) ? Localizer["Console"] : caller.PlayerName;
        string message = Localizer["Cvar.Changed", admin, oldValue.ToString(), value.ToString()];
        
        PlayerExtensions.PrintToAll(message);
        
        AdminPlus.LogAction($"{admin} changed gravity from {oldValue} to {value}");
        
        AddTimer(0.1f, () => {
            _ = Discord.SendAdminActionLog("gravity", "Server", 0UL, admin, caller?.SteamID ?? 0UL, $"{oldValue} → {value}", this);
        });
    }

    private void CmdRespawn(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2) { SendUsageMessage(caller, "Fun.Respawn.Usage", "Usage: css_respawn <target>"); return; }
        if (!ResolveTargets(caller, info.GetArg(1), false, false, true, out var deadPlayers, out var admin, out var label, out _))
        { SendErrorMessage(caller, "OnlyDead", Localizer["OnlyDead"]); return; }

        foreach (var t in deadPlayers) SafeRespawn(t);
        Announce("css_respawn<player>", "css_respawn<multiple>", admin, label);
        
        AdminPlus.LogAction($"{admin} respawned {label}");
        
        AddTimer(0.1f, () => {
            foreach (var target in deadPlayers)
            {
                _ = Discord.SendAdminActionLog("respawn", target.PlayerName, target.SteamID, admin, caller?.SteamID ?? 0UL, "", this);
            }
        });
    }

    private void SafeRespawn(CCSPlayerController t)
    {
        try
        {
            if (t == null || !t.IsValid || t.PawnIsAlive) return;
            t.Respawn();
        }
        catch { }
    }

    private void CmdNoclip(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2) { SendUsageMessage(caller, "Fun.Noclip.Usage", "Usage: css_noclip <target> [0|1]"); return; }
        if (!ResolveTargets(caller, info.GetArg(1), true, false, false, out var players, out var admin, out var label, out _, true))
        { SendErrorMessage(caller, "NoMatchingClient", Localizer["NoMatchingClient"]); return; }
        
        int on = -1; // -1 = toggle, 0 = off, 1 = on
        if (info.ArgCount >= 3)
        {
            if (!int.TryParse(info.GetArg(2), out on)) { SendErrorMessage(caller, "MustBeInteger", Localizer["MustBeInteger"]); return; }
        }
        
        foreach (var t in players)
        {
            var pawn = t.PlayerPawn?.Value; if (pawn == null) continue;
            bool isNoclipActive = pawn.MoveType == MoveType_t.MOVETYPE_NOCLIP;
            
            bool enable = on switch
            {
                -1 => !isNoclipActive, 
                0 => false,            
                1 => true,             
                _ => !isNoclipActive   
            };
            
            try { pawn.MoveType = enable ? MoveType_t.MOVETYPE_NOCLIP : MoveType_t.MOVETYPE_WALK; } catch { }
            SetActualMoveType(pawn, enable ? 8 : 2);
            Utilities.SetStateChanged(pawn, "CBaseEntity", "m_MoveType");
            AddTimer(0.05f, () => { if (pawn != null && pawn.IsValid) { try { pawn.MoveType = enable ? MoveType_t.MOVETYPE_NOCLIP : MoveType_t.MOVETYPE_WALK; } catch { } SetActualMoveType(pawn, enable ? 8 : 2); Utilities.SetStateChanged(pawn, "CBaseEntity", "m_MoveType"); } });
            AddTimer(0.15f, () => { if (pawn != null && pawn.IsValid) { try { pawn.MoveType = enable ? MoveType_t.MOVETYPE_NOCLIP : MoveType_t.MOVETYPE_WALK; } catch { } SetActualMoveType(pawn, enable ? 8 : 2); Utilities.SetStateChanged(pawn, "CBaseEntity", "m_MoveType"); } });
            
            Announce("css_noclip<player>", "css_noclip<multiple>", admin, label, enable ? Localizer["On"] : Localizer["Off"]);
            
        }
        
        AddTimer(0.1f, () => {
            foreach (var target in players)
            {
                var pawn = target.PlayerPawn?.Value; if (pawn == null) continue;
                bool isNoclipActive = pawn.MoveType == MoveType_t.MOVETYPE_NOCLIP;
                bool enable = on switch
                {
                    -1 => !isNoclipActive,
                    0 => false,
                    1 => true,
                    _ => !isNoclipActive
                };
                _ = Discord.SendAdminActionLog("noclip", target.PlayerName, target.SteamID, admin, caller?.SteamID ?? 0UL, enable ? Localizer["On"] : Localizer["Off"], this);
            }
        });
    }

    private void CmdWeapon(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 3) { SendUsageMessage(caller, "Fun.Weapon.Usage", "Usage: css_weapon <target> <weapon>"); return; }
        if (!ResolveTargets(caller, info.GetArg(1), true, true, false, out var players, out var admin, out var label, out _, true))
        { SendErrorMessage(caller, "OnlyAlive", Localizer["OnlyAlive"]); return; }
        var wep = info.GetArg(2);

        foreach (var t in players)
        {
            try { t.GiveNamedItem(wep.StartsWith("weapon_") ? wep : $"weapon_{wep}"); }
            catch { SendErrorMessage(caller, "WeaponNotFound", Localizer["WeaponNotFound"]); }
        }
        Announce("css_weapon<player>", "css_weapon<multiple>", admin, label, wep);
        
        AdminPlus.LogAction($"{admin} gave weapon {wep} to {label}");
        
        AddTimer(0.1f, () => {
            foreach (var target in players)
            {
                _ = Discord.SendAdminActionLog("weapon", target.PlayerName, target.SteamID, admin, caller?.SteamID ?? 0UL, wep, this);
            }
        });
    }

    private void CmdStrip(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2) { SendUsageMessage(caller, "Fun.Strip.Usage", "Usage: css_strip <target> [primary|secondary|grenade|c4|all]"); return; }
        if (!ResolveTargets(caller, info.GetArg(1), true, false, false, out var players, out var admin, out var label, out _, true))
        { SendErrorMessage(caller, "NoMatchingClient", Localizer["NoMatchingClient"]); return; }

        var filter = info.ArgCount >= 3 ? info.GetArg(2).ToLowerInvariant() : "all";

        foreach (var t in players)
        {
            var pawn = t.PlayerPawn?.Value;
            var weps = pawn?.WeaponServices?.MyWeapons?.Select(h => h.Value).Where(w => w?.IsValid == true).ToList();
            if (weps == null) continue;

            foreach (var w in weps)
            {
                try
                {
                    var baseW = w!.As<CCSWeaponBase>();
                    var cls = w!.DesignerName ?? "";
                    var slot = baseW?.VData?.GearSlot ?? gear_slot_t.GEAR_SLOT_INVALID;

                    bool isKnife = cls.Contains("knife", StringComparison.OrdinalIgnoreCase);
                    bool remove = filter switch
                    {
                        "primary" => slot == gear_slot_t.GEAR_SLOT_RIFLE,
                        "secondary" => slot == gear_slot_t.GEAR_SLOT_PISTOL,
                        "grenade" => slot == gear_slot_t.GEAR_SLOT_GRENADES,
                        "c4" => slot == gear_slot_t.GEAR_SLOT_C4,
                        "all" => !isKnife,
                        _ => !isKnife
                    };

                    if (remove) w.AcceptInput("Kill", w, w);
                }
                catch { }
            }
        }
        Announce("css_strip<player>", "css_strip<multiple>", admin, label, filter);
        
        AdminPlus.LogAction($"{admin} stripped {filter} weapons from {label}");
        
        AddTimer(0.1f, () => {
            foreach (var target in players)
            {
                _ = Discord.SendAdminActionLog("strip", target.PlayerName, target.SteamID, admin, caller?.SteamID ?? 0UL, "", this);
            }
        });
    }

    private void CmdSetTeamHp(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 3) { SendUsageMessage(caller, "Fun.SetHp.Usage", "Usage: css_sethp <t|ct> <hp>"); return; }
        var teamTok = info.GetArg(1).ToLowerInvariant();
        if (!int.TryParse(info.GetArg(2), out var hp)) { SendErrorMessage(caller, "MustBeInteger", Localizer["MustBeInteger"]); return; }

        if (teamTok is "ct" or "cts" or "counter" or "counterterrorist")
        {
            _ctDefaultHealth = hp;
            PlayerExtensions.PrintToAll(Localizer["css_sethp", Localizer["Team.CT"], hp]);
        }
        else if (teamTok is "t" or "ts" or "terrorist")
        {
            _tDefaultHealth = hp;
            PlayerExtensions.PrintToAll(Localizer["css_sethp", Localizer["Team.T"], hp]);
        }
        else { SendErrorMessage(caller, "NoTeam", Localizer["NoTeam"]); }
    }

    private void CmdHp(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 3 || !int.TryParse(info.GetArg(2), out var hp))
        { SendUsageMessage(caller, "Fun.Hp.Usage", "Usage: css_hp <target> <hp>"); return; }

        if (!ResolveTargets(caller, info.GetArg(1), true, false, false, out var players, out var admin, out var label, out _, true))
        { SendErrorMessage(caller, "NoMatchingClient", Localizer["NoMatchingClient"]); return; }
        foreach (var t in players) SetHealth(t, hp);
        Announce("css_hp<player>", "css_hp<multiple>", admin, label, hp);
        
        AddTimer(0.1f, () => {
            foreach (var target in players)
            {
                _ = Discord.SendAdminActionLog("hp", target.PlayerName, target.SteamID, admin, caller?.SteamID ?? 0UL, hp.ToString(), this);
            }
        });
    }

    private void CmdSpeed(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 3 || !float.TryParse(info.GetArg(2), NumberStyles.Float, CultureInfo.InvariantCulture, out var mult))
        { SendUsageMessage(caller, "Fun.Speed.Usage", "Usage: css_speed <target> <multiplier>"); return; }
        if (!ResolveTargets(caller, info.GetArg(1), true, false, false, out var players, out var admin, out var label, out _, true))
        { SendErrorMessage(caller, "NoMatchingClient", Localizer["NoMatchingClient"]); return; }
        foreach (var t in players) SetSpeed(t, mult);
        Announce("css_speed<player>", "css_speed<multiple>", admin, label, mult.ToString("0.##"));
        
        AddTimer(0.1f, () => {
            foreach (var target in players)
            {
                _ = Discord.SendAdminActionLog("speed", target.PlayerName, target.SteamID, admin, caller?.SteamID ?? 0UL, mult.ToString("0.##"), this);
            }
        });
    }

    private void CmdUnSpeed(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2) { SendUsageMessage(caller, "Fun.Speed.Usage", "Usage: css_unspeed <target>"); return; }
        if (!ResolveTargets(caller, info.GetArg(1), true, false, false, out var players, out var admin, out var label, out _, true))
        { SendErrorMessage(caller, "NoMatchingClient", Localizer["NoMatchingClient"]); return; }
        foreach (var t in players) SetSpeed(t, 1.0f);
        Announce("css_speed<player>", "css_speed<multiple>", admin, label, "1.0");
        
        AddTimer(0.1f, () => {
            foreach (var target in players)
            {
                _ = Discord.SendAdminActionLog("unspeed", target.PlayerName, target.SteamID, admin, caller?.SteamID ?? 0UL, "", this);
            }
        });
    }

    private void CmdGod(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2) { SendUsageMessage(caller, "Fun.God.Usage", "Usage: css_god <target> [0|1]"); return; }
        if (!ResolveTargets(caller, info.GetArg(1), true, false, false, out var players, out var admin, out var label, out _, true))
        { SendErrorMessage(caller, "NoMatchingClient", Localizer["NoMatchingClient"]); return; }
        
        int on = -1; // -1 = toggle, 0 = off, 1 = on
        if (info.ArgCount >= 3)
        {
            if (!int.TryParse(info.GetArg(2), out on)) { SendErrorMessage(caller, "MustBeInteger", Localizer["MustBeInteger"]); return; }
        }
        
        foreach (var t in players)
        {
            bool isGodActive = IsGodModeActive(t);
            
            bool shouldActivate = on switch
            {
                -1 => !isGodActive, 
                0 => false,         
                1 => true,          
                _ => !isGodActive   
            };
            
            SetGod(t, shouldActivate);
            Announce("css_god<player>", "css_god<multiple>", admin, label, shouldActivate ? Localizer["On"] : Localizer["Off"]);
            
        }
        
        AddTimer(0.1f, () => {
            foreach (var target in players)
            {
                bool isGodActive = IsGodModeActive(target);
                bool shouldActivate = on switch
                {
                    -1 => !isGodActive,
                    0 => false,
                    1 => true,
                    _ => !isGodActive
                };
                _ = Discord.SendAdminActionLog("god", target.PlayerName, target.SteamID, admin, caller?.SteamID ?? 0UL, shouldActivate ? Localizer["On"] : Localizer["Off"], this);
            }
        });
    }

    private void CmdTeam(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 3) { SendUsageMessage(caller, "Fun.Team.Usage", "Usage: css_team <target> <t|ct|spec>"); return; }
        if (!ResolveTargets(caller, info.GetArg(1), true, false, false, out var players, out var admin, out var label, out _, true))
        { SendErrorMessage(caller, "NoMatchingClient", Localizer["NoMatchingClient"]); return; }
        var tok = info.GetArg(2).ToLowerInvariant();
        var team = tok switch
        {
            "t" or "ts" or "terrorist" => CsTeam.Terrorist,
            "ct" or "cts" or "counter" or "counterterrorist" => CsTeam.CounterTerrorist,
            "spec" or "spectator" => CsTeam.Spectator,
            _ => CsTeam.None
        };
        if (team == CsTeam.None) { SendErrorMessage(caller, "NoTeam", Localizer["NoTeam"]); return; }
        foreach (var t in players) t.ChangeTeam(team);
        PlayerExtensions.PrintToAll(Localizer["css_team", admin, label, TeamDisplay(team == CsTeam.Terrorist ? TargetScope.TeamT : team == CsTeam.CounterTerrorist ? TargetScope.TeamCT : TargetScope.TeamSpec)]);
        
        AdminPlus.LogAction($"{admin} changed team for {label} to {TeamDisplay(team == CsTeam.Terrorist ? TargetScope.TeamT : team == CsTeam.CounterTerrorist ? TargetScope.TeamCT : TargetScope.TeamSpec)}");
    }

    private void CmdSwap(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2) { SendUsageMessage(caller, "Fun.Swap.Usage", "Usage: css_swap <#userid|name>"); return; }
        if (!ResolveTargets(caller, info.GetArg(1), true, false, false, out var players, out var admin, out var label, out _, true))
        { SendErrorMessage(caller, "NoMatchingClient", Localizer["NoMatchingClient"]); return; }
        var t = players[0];
        var newTeam = t.Team == CsTeam.CounterTerrorist ? CsTeam.Terrorist : CsTeam.CounterTerrorist;
        t.ChangeTeam(newTeam);
        PlayerExtensions.PrintToAll(Localizer["css_swap", admin, label]);
        
        AdminPlus.LogAction($"{admin} swapped {label} to {TeamDisplay(newTeam == CsTeam.Terrorist ? TargetScope.TeamT : TargetScope.TeamCT)}");
    }

    private void CmdBury(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2) { SendUsageMessage(caller, "Fun.Bury.Usage", "Usage: css_bury <target>"); return; }
        if (!ResolveTargets(caller, info.GetArg(1), true, true, false, out var players, out var admin, out var label, out _, true))
        { SendErrorMessage(caller, "OnlyAlive", Localizer["OnlyAlive"]); return; }
        foreach (var t in players) BuryPawn(t, -40f);
        Announce("css_bury<player>", "css_bury<multiple>", admin, label);
        
        AdminPlus.LogAction($"{admin} buried {label}");
        
        AddTimer(0.1f, () => {
            foreach (var target in players)
            {
                _ = Discord.SendAdminActionLog("bury", target.PlayerName, target.SteamID, admin, caller?.SteamID ?? 0UL, "", this);
            }
        });
    }
    private void CmdUnBury(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2) { SendUsageMessage(caller, "Fun.UnBury.Usage", "Usage: css_unbury <target>"); return; }
        if (!ResolveTargets(caller, info.GetArg(1), true, true, false, out var players, out var admin, out var label, out _, true))
        { SendErrorMessage(caller, "OnlyAlive", Localizer["OnlyAlive"]); return; }
        foreach (var t in players) BuryPawn(t, +40f);
        Announce("css_unbury<player>", "css_unbury<multiple>", admin, label);
        
        AdminPlus.LogAction($"{admin} unburied {label}");
        
        AddTimer(0.1f, () => {
            foreach (var target in players)
            {
                _ = Discord.SendAdminActionLog("unbury", target.PlayerName, target.SteamID, admin, caller?.SteamID ?? 0UL, "", this);
            }
        });
    }

    private void CmdClean(CCSPlayerController? caller, CommandInfo info)
    {
        try
        {
            var worldWeapons = Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("weapon_");
            foreach (var w in worldWeapons)
            {
                try
                {
                    if (!w.IsValid) continue;
                    if (w.OwnerEntity?.Value == null)
                        w.Remove();
                }
                catch { }
            }
            var worldItems = Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("item_");
            foreach (var it in worldItems)
            {
                try
                {
                    if (!it.IsValid) continue;
                    if (it.OwnerEntity?.Value == null)
                        it.Remove();
                }
                catch { }
            }
        }
        catch { }

        var fallback = _menuInvokerName;
        var admin = (caller == null || !caller.IsValid) ? (string.IsNullOrEmpty(fallback) ? Localizer["Console"] : fallback) : caller.PlayerName;
        PlayerExtensions.PrintToAll(Localizer["css_clean", admin]);
        
        AddTimer(0.1f, () => {
            _ = Discord.SendAdminActionLog("clean", "Server", 0UL, admin, caller?.SteamID ?? 0UL, "", this);
        });
    }

    private void CmdGoto(CCSPlayerController? caller, CommandInfo info)
    {
        var effectiveInvoker = caller;
        if (effectiveInvoker == null || !effectiveInvoker.IsValid)
        {
            if (!string.IsNullOrEmpty(AdminPlus._menuInvokerName))
            {
                effectiveInvoker = Utilities.GetPlayers()
                    .FirstOrDefault(p => p != null && p.IsValid && !p.IsBot && string.Equals(p.PlayerName, AdminPlus._menuInvokerName, StringComparison.Ordinal));
            }
            if (effectiveInvoker == null || !effectiveInvoker.IsValid) return;
        }
        if (info.ArgCount < 2) { SendUsageMessage(effectiveInvoker, "Fun.Goto.Usage", "Usage: css_goto <#userid|name>"); return; }

        if (!ResolveTargets(effectiveInvoker, info.GetArg(1), false, false, false, out var players, out _, out _, out _, allowBots: true))
        { SendErrorMessage(effectiveInvoker, "NoMatchingClient", Localizer["NoMatchingClient"]); return; }

        var target = players[0];
        
        if (effectiveInvoker.SteamID == target.SteamID)
        { SendErrorMessage(effectiveInvoker, "CannotTargetSelf", Localizer["CannotTargetSelf"]); return; }
        var adminPawn = effectiveInvoker.PlayerPawn?.Value; var targetPawn = target.PlayerPawn?.Value;
        if (adminPawn?.AbsOrigin == null || adminPawn.AbsRotation == null || targetPawn?.AbsOrigin == null || targetPawn.AbsRotation == null) return;

        float yaw = (float)(Math.PI / 180.0) * adminPawn.AbsRotation.Y;
        var pos = targetPawn.AbsOrigin;
        var forward = new Vector((float)Math.Cos(yaw), (float)Math.Sin(yaw), 0f);
        var dest = new Vector(pos.X + forward.X * 80f, pos.Y + forward.Y * 80f, pos.Z + 5f);
        adminPawn.Teleport(dest, adminPawn.AbsRotation, new Vector(0, 0, 0));
        effectiveInvoker.Print(Localizer["css_goto", effectiveInvoker.PlayerName ?? effectiveInvoker.SteamID.ToString(), target.PlayerName ?? target.SteamID.ToString()]);
        
        AddTimer(0.1f, () => {
            _ = Discord.SendAdminActionLog("goto", target.PlayerName ?? "Unknown", target.SteamID, effectiveInvoker.PlayerName ?? "Unknown", effectiveInvoker.SteamID, "", this);
        });
    }

    private void CmdBring(CCSPlayerController? caller, CommandInfo info)
    {
        var effectiveInvoker = caller;
        if (effectiveInvoker == null || !effectiveInvoker.IsValid)
        {
            if (!string.IsNullOrEmpty(AdminPlus._menuInvokerName))
            {
                effectiveInvoker = Utilities.GetPlayers()
                    .FirstOrDefault(p => p != null && p.IsValid && !p.IsBot && string.Equals(p.PlayerName, AdminPlus._menuInvokerName, StringComparison.Ordinal));
            }
            if (effectiveInvoker == null || !effectiveInvoker.IsValid) return;
        }
        if (info.ArgCount < 2) { SendUsageMessage(effectiveInvoker, "Fun.Bring.Usage", "Usage: css_bring <target>"); return; }

        if (!ResolveTargets(effectiveInvoker, info.GetArg(1), true, false, false, out var players, out var admin, out var label, out _, allowBots: true))
        { SendErrorMessage(effectiveInvoker, "NoMatchingClient", Localizer["NoMatchingClient"]); return; }

        if (players.Any(p => p.SteamID == effectiveInvoker.SteamID))
        { SendErrorMessage(effectiveInvoker, "CannotTargetSelf", Localizer["CannotTargetSelf"]); return; }

        var adminPawn = effectiveInvoker.PlayerPawn?.Value; if (adminPawn?.AbsOrigin == null || adminPawn.AbsRotation == null) return;
        float yaw = (float)(Math.PI / 180.0) * adminPawn.AbsRotation.Y;
        var basePos = adminPawn.AbsOrigin;
        var fwd = new Vector((float)Math.Cos(yaw), (float)Math.Sin(yaw), 0f);
        var bringPos = new Vector(basePos.X + fwd.X * 80f, basePos.Y + fwd.Y * 80f, basePos.Z + 5f);
        foreach (var t in players)
        {
            var pawn = t.PlayerPawn?.Value; if (pawn?.AbsOrigin == null) continue;
            pawn.Teleport(bringPos, pawn.AbsRotation, new Vector(0, 0, 0));
        }
        Announce("css_bring<player>", "css_bring<multiple>", admin, label);
        
        AddTimer(0.1f, () => {
            foreach (var target in players)
            {
                _ = Discord.SendAdminActionLog("bring", target.PlayerName ?? "Unknown", target.SteamID, admin ?? "Unknown", effectiveInvoker?.SteamID ?? 0UL, "", this);
            }
        });
    }

    private void CmdHRespawn(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2) { SendUsageMessage(caller, "Fun.HRespawn.Usage", "Usage: css_hrespawn <#userid|name>"); return; }
        if (!ResolveTargets(caller, info.GetArg(1), false, false, true, out var players, out var admin, out var label, out _, true))
        { SendErrorMessage(caller, "OnlyDead", Localizer["OnlyDead"]); return; }

        var t = players[0];
        if (_lastDeathPosFun.TryGetValue(t.SteamID, out var v) || t.TryGetLastCoord(out v))
        {
            var adjustedPos = new Vector(v.X, v.Y, v.Z + 10);
            
            SafeRespawn(t);
            
            void tp() 
            { 
                var pawn = t.PlayerPawn?.Value; 
                if (pawn != null && pawn.IsValid && t.IsValid) 
                {
                    pawn.Teleport(adjustedPos, pawn.AbsRotation, new Vector(0, 0, 0));
                }
            }
            
            AddTimer(0.2f, tp);
            AddTimer(0.5f, tp);
            AddTimer(1.0f, tp);
            AddTimer(1.5f, tp);

            PlayerExtensions.PrintToAll(Localizer["css_hrespawn", admin, label]);
            
            AddTimer(0.1f, () => {
                _ = Discord.SendAdminActionLog("hrespawn", t.PlayerName, t.SteamID, admin, caller?.SteamID ?? 0UL, "", this);
            });
        }
        else SendErrorMessage(caller, "NoDeathPos", Localizer["NoDeathPos"]);
    }

    private void CmdGlow(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 3) { SendUsageMessage(caller, "Fun.Glow.Usage", "Usage: css_glow <target> <color>"); return; }
        if (!ResolveTargets(caller, info.GetArg(1), true, false, false, out var players, out var admin, out var label, out _, true))
        { SendErrorMessage(caller, "NoMatchingClient", Localizer["NoMatchingClient"]); return; }

        var colorArg = info.GetArg(2).ToLowerInvariant();
        if (colorArg == "off")
        {
            foreach (var t in players) GlowPawn(t, Color.White);
            PlayerExtensions.PrintToAll(Localizer["css_glow_off", admin, label]);
            
        }
        else
        {
            if (!TryParseColor(colorArg, out var color)) { SendErrorMessage(caller, "ColorNotFound", Localizer["ColorNotFound"]); return; }
            foreach (var t in players) GlowPawn(t, color);
            PlayerExtensions.PrintToAll(Localizer["css_glow", admin, label, colorArg]);
            
        }
        
        AddTimer(0.1f, () => {
            foreach (var target in players)
            {
                _ = Discord.SendAdminActionLog("glow", target.PlayerName, target.SteamID, admin, caller?.SteamID ?? 0UL, colorArg, this);
            }
        });
    }

    private void CmdColor(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 3) { SendUsageMessage(caller, "Fun.Color.Usage", "Usage: css_color <target> <color>"); return; }
        if (!ResolveTargets(caller, info.GetArg(1), true, false, false, out var players, out var admin, out var label, out _, true))
        { SendErrorMessage(caller, "NoMatchingClient", Localizer["NoMatchingClient"]); return; }

        var colorArg = info.GetArg(2).ToLowerInvariant();
        if (colorArg == "off")
        {
            foreach (var t in players) GlowPawn(t, Color.White);
            PlayerExtensions.PrintToAll(Localizer["css_color_off", admin, label]);
        }
        else
        {
            if (!TryParseColor(colorArg, out var color)) { SendErrorMessage(caller, "ColorNotFound", Localizer["ColorNotFound"]); return; }
            foreach (var t in players) GlowPawn(t, color);
            PlayerExtensions.PrintToAll(Localizer["css_color", admin, label, colorArg]);
        }
    }

    private void CmdShake(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 3 || !float.TryParse(info.GetArg(2), NumberStyles.Float, CultureInfo.InvariantCulture, out var sec) || sec <= 0)
        { SendUsageMessage(caller, "Fun.Shake.Usage", "Usage: css_shake <target> <seconds>"); return; }
        if (!ResolveTargets(caller, info.GetArg(1), true, true, false, out var players, out var admin, out var label, out _, true))
        { SendErrorMessage(caller, "OnlyAlive", Localizer["OnlyAlive"]); return; }

        foreach (var t in players)
        {
            StopTimer(t, FunTimer.Shake);
            var pawn = t.PlayerPawn?.Value; if (pawn?.AbsOrigin == null) continue;
            var shake = Utilities.CreateEntityByName<CEnvShake>("env_shake");
            if (shake == null) continue;
            shake.Amplitude = 18; shake.Frequency = 255; shake.Duration = sec; shake.Radius = 80;
            shake.Teleport(new Vector(pawn.AbsOrigin.X, pawn.AbsOrigin.Y, pawn.AbsOrigin.Z + 72), QAngle.Zero, Vector.Zero);
            shake.DispatchSpawn();
            shake.AcceptInput("SetParent", pawn, pawn, "!activator");
            shake.AcceptInput("StartShake");
            _activeShakes[t.SteamID] = shake;
            RememberTimer(t, FunTimer.Shake, AddTimer(sec, () =>
            {
                if (_activeShakes.TryGetValue(t.SteamID, out var sh) && sh != null && sh.IsValid)
                { try { sh.AcceptInput("StopShake"); sh.Remove(); } catch { } _activeShakes.Remove(t.SteamID); }
                StopTimer(t, FunTimer.Shake);
            }));
        }
        Announce("css_shake<player>", "css_shake<multiple>", admin, label, sec);
        
        AdminPlus.LogAction($"{admin} shook {label} for {sec} seconds");
        
        AddTimer(0.1f, () => {
            foreach (var target in players)
            {
                _ = Discord.SendAdminActionLog("shake", target.PlayerName, target.SteamID, admin, caller?.SteamID ?? 0UL, sec.ToString(), this);
            }
        });
    }

    private void CmdUnShake(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2) { SendUsageMessage(caller, "Fun.UnShake.Usage", "Usage: css_unshake <target>"); return; }
        if (!ResolveTargets(caller, info.GetArg(1), true, true, false, out var players, out var admin, out var label, out _, true))
        { SendErrorMessage(caller, "OnlyAlive", Localizer["OnlyAlive"]); return; }
        foreach (var t in players)
        {
            StopTimer(t, FunTimer.Shake);
            if (_activeShakes.TryGetValue(t.SteamID, out var sh) && sh != null && sh.IsValid)
            { try { sh.AcceptInput("StopShake"); sh.Remove(); } catch { } _activeShakes.Remove(t.SteamID); }
        }
        PlayerExtensions.PrintToAll(Localizer["css_unshake", admin, label]);
        
        AdminPlus.LogAction($"{admin} unshook {label}");
    }

    private void CmdBlind(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 3 || !float.TryParse(info.GetArg(2), NumberStyles.Float, CultureInfo.InvariantCulture, out var sec) || sec <= 0)
        { SendUsageMessage(caller, "Fun.Blind.Usage", "Usage: css_blind <target> <seconds>"); return; }
        if (!ResolveTargets(caller, info.GetArg(1), true, true, false, out var players, out var admin, out var label, out _, true))
        { SendErrorMessage(caller, "OnlyAlive", Localizer["OnlyAlive"]); return; }

        foreach (var t in players)
        {
            StopTimer(t, FunTimer.Blind);
            try { t.ExecuteClientCommand("play weapons/flashbang/flashbang_explode2.wav"); } catch { }
            float hold = Math.Max(0.1f, sec - 0.4f);
            ColorScreen(t, Color.White, hold, 0.02f, stayout: true, purge: true);
            RememberTimer(t, FunTimer.Blind, AddTimer(hold, () =>
            {
                ColorScreen(t, Color.White, 0, 0.4f, stayout: false, purge: true);
                OverlayBlindOff(t);
                StopTimer(t, FunTimer.Blind);
            }));
        }
        Announce("css_blind<player>", "css_blind<multiple>", admin, label, sec);
        
        AdminPlus.LogAction($"{admin} blinded {label} for {sec} seconds");
        
        AddTimer(0.1f, () => {
            foreach (var target in players)
            {
                _ = Discord.SendAdminActionLog("blind", target.PlayerName, target.SteamID, admin, caller?.SteamID ?? 0UL, sec.ToString(), this);
            }
        });
    }

    private void CmdUnBlind(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2) { SendUsageMessage(caller, "Fun.UnBlind.Usage", "Usage: css_unblind <target>"); return; }
        if (!ResolveTargets(caller, info.GetArg(1), true, true, false, out var players, out var admin, out var label, out _, true))
        { SendErrorMessage(caller, "OnlyAlive", Localizer["OnlyAlive"]); return; }
        foreach (var t in players) { OverlayBlindOff(t); StopTimer(t, FunTimer.Blind); }
        PlayerExtensions.PrintToAll(Localizer["css_unblind", admin, label]);
        
        AdminPlus.LogAction($"{admin} unblinded {label}");
    }

    private void CmdDrug(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 3 || !float.TryParse(info.GetArg(2), NumberStyles.Float, CultureInfo.InvariantCulture, out var sec) || sec <= 0)
        { SendUsageMessage(caller, "Fun.Drug.Usage", "Usage: css_drug <target> <seconds>"); return; }
        if (!ResolveTargets(caller, info.GetArg(1), true, true, false, out var players, out var admin, out var label, out _, true))
        { SendErrorMessage(caller, "OnlyAlive", Localizer["OnlyAlive"]); return; }

        foreach (var t in players)
        {
            StopTimer(t, FunTimer.Drug);
            StopTimer(t, FunTimer.DrugEnd);

            var rnd = new Random();
            float time = 0f;
            RememberTimer(t, FunTimer.Drug, AddTimer(0.033f, () =>
            {
                time += 0.033f;
                var pawn = t.PlayerPawn?.Value; if (pawn?.AbsRotation == null) return;
                var a = pawn.AbsRotation;
                float roll = (float)(Math.Sin(time * 2.0f) * 20.0f);
                a.Z = roll;
                pawn.Teleport(null, a, null);
                ColorScreen(t, Color.FromArgb(rnd.Next(256), rnd.Next(256), rnd.Next(256), 100), 0.12f, 0.04f, stayout: true, purge: false);
            }, TimerFlags.REPEAT));

            RememberTimer(t, FunTimer.DrugEnd, AddTimer(sec, () =>
            {
                StopTimer(t, FunTimer.Drug);
                var pawn = t.PlayerPawn?.Value; if (pawn?.AbsRotation != null) { var a = pawn.AbsRotation; a.Z = 0f; pawn.Teleport(null, a, null); }
                ColorScreen(t, Color.FromArgb(0, 0, 0, 0), 0, 0.10f, stayout: false, purge: true);
                StopTimer(t, FunTimer.DrugEnd);
            }));
        }
        Announce("css_drug<player>", "css_drug<multiple>", admin, label, sec);
        
        AdminPlus.LogAction($"{admin} drugged {label} for {sec} seconds");
        
        AddTimer(0.1f, () => {
            foreach (var target in players)
            {
                _ = Discord.SendAdminActionLog("drug", target.PlayerName, target.SteamID, admin, caller?.SteamID ?? 0UL, sec.ToString(), this);
            }
        });
    }

    private void CmdUnDrug(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2) { SendUsageMessage(caller, "Fun.Drug.Usage", "Usage: css_undrug <target>"); return; }
        if (!ResolveTargets(caller, info.GetArg(1), true, true, false, out var players, out var admin, out var label, out _, true))
        { SendErrorMessage(caller, "OnlyAlive", Localizer["OnlyAlive"]); return; }

        foreach (var t in players)
        {
            StopTimer(t, FunTimer.Drug);
            StopTimer(t, FunTimer.DrugEnd);
            var pawn = t.PlayerPawn?.Value;
            if (pawn?.AbsRotation != null)
            {
                var a = pawn.AbsRotation; a.Z = 0f; pawn.Teleport(null, a, null);
            }
            ColorScreen(t, Color.FromArgb(0, 0, 0, 0), 0, 0.1f, stayout: false, purge: true);
        }
        
        AdminPlus.LogAction($"{admin} undrugged {label}");
    }
}

public static class LastCoordExtensions
{
    private static readonly ConcurrentDictionary<ulong, Vector> _lastCoords = new();

    public static void CopyLastCoord(this CCSPlayerController player)
    {
        var pos = player?.PlayerPawn?.Value?.AbsOrigin;
        if (player != null && player.IsValid && pos != null)
            _lastCoords[player.SteamID] = pos;
    }

    public static void RemoveLastCoord(this CCSPlayerController player)
    {
        if (player == null) return;
        _lastCoords.TryRemove(player.SteamID, out _);
    }

    public static bool TryGetLastCoord(this CCSPlayerController player, out Vector pos)
    {
        if (player != null && _lastCoords.TryGetValue(player.SteamID, out var v))
        { pos = v; return true; }
        pos = new Vector(0, 0, 0);
        return false;
    }
    
    public static void CleanupLastCoords()
    {
        try
        {
            _lastCoords.Clear();
            Console.WriteLine("[AdminPlus] LastCoords cleaned up.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminPlus] Error during LastCoords cleanup: {ex.Message}");
        }
    }
}
