using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;
using System;
using System.Globalization;
using System.Linq;

namespace AdminPlus;

public partial class AdminPlus
{
    private const string ChatPerm = "@css/chat";

    public void CleanupChat()
    {
        try
        {
            RemoveCommandListener("say", OnChatSayListener, HookMode.Pre);
            RemoveCommandListener("say_team", OnChatSayListener, HookMode.Pre);
            
            Console.WriteLine("[AdminPlus] Chat system cleaned up.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminPlus] Error during chat cleanup: {ex.Message}");
        }
    }

    public void RegisterChatCommands()
    {
        AddCommandListener("say", OnChatSayListener, HookMode.Pre);
        AddCommandListener("say_team", OnChatSayListener, HookMode.Pre);

        AddCommand("asay", Localizer["Asay.Usage"], CmdASay);
        AddCommand("csay", Localizer["Csay.Usage"], CmdCSay);
        AddCommand("hsay", Localizer["Hsay.Usage"], CmdHSay);
        AddCommand("psay", Localizer["Psay.Usage"], CmdPSay);

        AddCommand("css_asay", "Admin say (console)", CmdASay);
        AddCommand("css_csay", "Center say (console)", CmdCSay);
        AddCommand("css_hsay", "HUD say (console)", CmdHSay);
        AddCommand("css_psay", "Private say (console)", CmdPSay);
        AddCommand("css_say", "Say to all (console)", CmdSayAll);
    }

    private static bool HasChatPermission(CCSPlayerController? p)
        => p != null && p.IsValid && AdminManager.PlayerHasPermissions(p, ChatPerm);

    private HookResult OnChatSayListener(CCSPlayerController? caller, CommandInfo info)
    {
        try
        {
            if (caller == null || !caller.IsValid || caller.IsBot)
                return HookResult.Continue;

            string cmd = info.GetArg(0) ?? string.Empty; // say / say_team
            string text = info.ArgCount >= 2 ? (info.GetArg(1) ?? string.Empty).Trim() : string.Empty;
            if (string.IsNullOrEmpty(text)) return HookResult.Continue;

            if (cmd.Equals("say_team", StringComparison.OrdinalIgnoreCase) && text.StartsWith("@"))
            {
                var msg = text.Length >= 2 && text[1] == ' ' ? text[2..] : text[1..];
                if (!string.IsNullOrWhiteSpace(msg))
                    SendASay(caller, msg);
                return HookResult.Handled;
            }

            if (cmd.Equals("say", StringComparison.OrdinalIgnoreCase) && text.StartsWith("@") && HasChatPermission(caller))
            {
                var msg = text.Length >= 2 && text[1] == ' ' ? text[2..] : text[1..];
                if (!string.IsNullOrWhiteSpace(msg))
                    SendSayAll(caller, msg);
                return HookResult.Handled;
            }

            if (text.StartsWith("!asay ", StringComparison.OrdinalIgnoreCase))
            { if (HasChatPermission(caller)) SendASay(caller, text[6..]); return HookResult.Handled; }

            if (text.StartsWith("!csay ", StringComparison.OrdinalIgnoreCase))
            { if (HasChatPermission(caller)) SendCSay(caller, text[6..]); return HookResult.Handled; }

            if (text.StartsWith("!hsay ", StringComparison.OrdinalIgnoreCase))
            { if (HasChatPermission(caller)) SendHSay(caller, text[6..]); return HookResult.Handled; }

            if (text.StartsWith("!psay ", StringComparison.OrdinalIgnoreCase))
            {
                if (!HasChatPermission(caller)) return HookResult.Handled;
                var raw = text[6..];
                var parts = raw.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    caller.Print(Localizer["Psay.Usage"]);
                    return HookResult.Handled;
                }
                var token = parts[0];
                var msg = parts[1];
                var target = FindPlayerByNameOrId(token);
                if (target == null || !target.IsValid)
                {
                    caller.Print(Localizer["NoMatchingClient"]);
                    return HookResult.Handled;
                }
                SendPSay(caller, target, msg);
                return HookResult.Handled;
            }

            if (text.StartsWith("!say ", StringComparison.OrdinalIgnoreCase))
            { if (HasChatPermission(caller)) SendSayAll(caller, text[5..]); return HookResult.Handled; }

            return HookResult.Continue;
        }
        catch
        {
            return HookResult.Continue;
        }
    }

    private void SendSayAll(CCSPlayerController from, string message)
    {
        var line = string.Format(CultureInfo.InvariantCulture, Localizer["css_say"], from.PlayerName ?? from.SteamID.ToString(), message);
        Server.PrintToChatAll(line);
    }

    private void SendASay(CCSPlayerController from, string message)
    {
        var line = string.Format(CultureInfo.InvariantCulture, Localizer["css_asay"], from.PlayerName ?? from.SteamID.ToString(), message);
        from.PrintToChat(line);
        foreach (var a in Utilities.GetPlayers()!.Where(p => p.IsValid && !p.IsBot && AdminManager.PlayerHasPermissions(p, ChatPerm)))
            if (a != from) a.PrintToChat(line);
    }

    private void SendCSay(CCSPlayerController from, string message)
    {
        var text = string.Format(CultureInfo.InvariantCulture, Localizer["css_csay"], from.PlayerName ?? from.SteamID.ToString(), message);
        Server.NextFrame(() =>
        {
            foreach (var pl in Utilities.GetPlayers()!.Where(p => p.IsValid && !p.IsBot))
                pl.PrintToCenter(text);
        });
    }

    private void SendHSay(CCSPlayerController from, string message)
    {
        var text = string.Format(CultureInfo.InvariantCulture, Localizer["css_hsay"], from.PlayerName ?? from.SteamID.ToString(), message);
        Server.NextFrame(() => VirtualFunctions.ClientPrintAll(HudDestination.Alert, text, 0, 0, 0, 0));
    }

    private void SendPSay(CCSPlayerController from, CCSPlayerController to, string message)
    {
        var line = string.Format(CultureInfo.InvariantCulture, Localizer["css_psay"], from.PlayerName ?? from.SteamID.ToString(), to.PlayerName ?? to.SteamID.ToString(), message);
        from.PrintToChat(line);
        if (to != from) to.PrintToChat(line);
    }

    private void CmdASay(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null && (!caller.IsValid || !HasChatPermission(caller))) return;
        if (info.ArgCount < 2) { if (caller != null) caller.Print(Localizer["Asay.Usage"]); else Console.WriteLine(Localizer["Asay.UsageConsole"]); return; }
        var msg = info.ArgString?.Trim() ?? "";
        if (caller == null)
        {
            var line = string.Format(CultureInfo.InvariantCulture, Localizer["css_asay"], Localizer["Console"], msg).ReplaceColorTags();
            foreach (var a in Utilities.GetPlayers()!.Where(p => p.IsValid && !p.IsBot && AdminManager.PlayerHasPermissions(p, ChatPerm)))
                a.Print(line);
            return;
        }
        SendASay(caller, msg);
    }

    private void CmdCSay(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null && (!caller.IsValid || !HasChatPermission(caller))) return;
        if (info.ArgCount < 2) { if (caller != null) caller.Print(Localizer["Csay.Usage"]); else Console.WriteLine(Localizer["Csay.UsageConsole"]); return; }
        var msg = info.ArgString?.Trim() ?? "";
        if (caller == null) { Console.WriteLine("[AdminPlus] csay: " + msg); return; }
        SendCSay(caller, msg);
    }

    private void CmdHSay(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null && (!caller.IsValid || !HasChatPermission(caller))) return;
        if (info.ArgCount < 2) { if (caller != null) caller.Print(Localizer["Hsay.Usage"]); else Console.WriteLine(Localizer["Hsay.UsageConsole"]); return; }
        var msg = info.ArgString?.Trim() ?? "";
        if (caller == null) { Console.WriteLine("[AdminPlus] hsay: " + msg); return; }
        SendHSay(caller, msg);
    }

    private void CmdPSay(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null && (!caller.IsValid || !HasChatPermission(caller))) return;
        if (info.ArgCount < 3)
        {
            if (caller != null) caller.Print(Localizer["Psay.Usage"]);
            else Console.WriteLine(Localizer["Psay.UsageConsole"]);
            return;
        }
        var token = info.GetArg(1);
        var msg = string.Join(' ', Enumerable.Range(2, info.ArgCount - 2).Select(i => info.GetArg(i)));
        var target = FindPlayerByNameOrId(token);
        if (target == null || !target.IsValid)
        { if (caller != null) caller.Print(Localizer["NoMatchingClient"]); else Console.WriteLine(Localizer["NoMatchingClient"]); return; }
        if (caller == null) { Console.WriteLine($"[AdminPlus] psay to {target.PlayerName}: {msg}"); target.Print(msg); return; }
        SendPSay(caller, target, msg);
    }

    private void CmdSayAll(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null && (!caller.IsValid || !HasChatPermission(caller))) return;
        if (info.ArgCount < 2) { if (caller != null) caller.Print(Localizer["Say.Usage"]); else Console.WriteLine(Localizer["Say.UsageConsole"]); return; }
        var msg = info.ArgString?.Trim() ?? "";
        if (caller == null)
        {
            var line = string.Format(CultureInfo.InvariantCulture, Localizer["css_say"], Localizer["Console"], msg).ReplaceColorTags();
            PlayerExtensions.PrintToAll(line);
            return;
        }
        SendSayAll(caller, msg);
    }
}

