using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using System;
using System.Collections.Generic;

namespace AdminPlus;

public partial class AdminPlus
{
    private static bool _hideAdminsInList;

    internal static bool AreAdminsHiddenFromList() => _hideAdminsInList;

    private static readonly HashSet<string> _pluginChatCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "admins", "hideadmin", "ban", "ipban", "unban", "lastban", "baninfo", "banlist",
        "kick", "map", "wsmap", "workshop", "rcon", "cvar", "who", "rr", "slap", "slay", "money", "armor",
        "mute", "gag", "unmute", "ungag", "mutelist", "gaglist", "silence", "unsilence",
        "asay", "csay", "hsay", "psay", "say",
        "addadmin", "removeadmin", "adminlist", "adminreload", "admin_reload",
        "vote", "votemap", "rvote", "cancelvote", "votekick", "voteban", "votegag", "votemute", "votesilence",
        "report", "calladmin", "version", "help", "adminhelp",
        "css_admin", "css_admins", "css_hideadmin", "css_ban", "css_ipban", "css_unban", "css_lastban",
        "css_baninfo", "css_banlist", "css_kick", "css_map", "css_wsmap", "css_workshop", "css_rcon", "css_cvar",
        "css_who", "css_rr", "css_slap", "css_slay", "css_money", "css_armor", "css_mute", "css_gag",
        "css_unmute", "css_ungag", "css_mutelist", "css_gaglist", "css_silence", "css_unsilence",
        "css_asay", "css_csay", "css_hsay", "css_psay", "css_say", "css_addadmin", "css_removeadmin",
        "css_adminlist", "css_adminreload", "css_admin_reload", "css_adminmenu", "css_vote", "css_votemap",
        "css_rvote", "css_cancelvote", "css_votekick", "css_voteban", "css_votegag", "css_votemute",
        "css_votesilence", "css_report", "css_calladmin", "css_version", "css_adminhelp", "css_players",
        "css_cleanall", "css_cleanmute", "css_cleangag", "css_cleanbans", "css_cleanipbans", "css_cleansteambans",
        "css_discord_status", "css_rename", "css_team", "css_swap", "css_noclip", "css_god", "css_hp", "css_speed",
        "css_unspeed", "css_give", "css_strip", "css_freeze", "css_unfreeze", "css_blind", "css_unblind",
        "css_bury", "css_unbury", "css_beacon", "css_shake", "css_unshake", "css_glow", "css_color", "css_teleport"
    };

    private void RegisterHideAdminCommands()
    {
        AddCommand("hideadmin", Localizer["HideAdmin.Usage"], CmdHideAdmin);
        AddCommand("css_hideadmin", "Toggle hiding all admins from !admins list", CmdHideAdmin);
    }

    private void CmdHideAdmin(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null && caller.IsValid)
        {
            bool hasAdminPerm =
                HasEffectivePermission(caller, "@css/root") ||
                HasEffectivePermission(caller, "@css/ban") ||
                HasEffectivePermission(caller, "@css/generic");

            if (!hasAdminPerm)
            {
                caller.Print(Localizer["NoPermission"]);
                return;
            }
        }

        ToggleHideAdminsInList(caller);
    }

    private void ToggleHideAdminsInList(CCSPlayerController? caller)
    {
        _hideAdminsInList = !_hideAdminsInList;

        if (caller != null && caller.IsValid)
        {
            var msg = Localizer[_hideAdminsInList ? "HideAdmin.Hidden" : "HideAdmin.Visible"];
            caller.PrintToChat($"{Localizer["Prefix"]} {msg}");
            return;
        }

        Console.WriteLine(_hideAdminsInList
            ? "[AdminPlus] All admins hidden from !admins."
            : "[AdminPlus] Admin visibility in !admins restored.");
    }

    /// <summary>
    /// Hides plugin chat commands from public chat and runs them on the next frame.
    /// Returning Handled alone would block CounterStrikeSharp from dispatching the command.
    /// </summary>
    internal bool HandleSuppressedPluginChatCommand(CCSPlayerController player, string rawMessage)
    {
        if (!TryParsePluginChatCommand(rawMessage, out var commandToken, out var argumentLine))
            return false;

        Server.NextFrame(() =>
        {
            if (!player.IsValid)
                return;
            DispatchPluginChatCommand(player, commandToken, argumentLine);
        });

        return true;
    }

    private static bool TryParsePluginChatCommand(string rawMessage, out string commandToken, out string argumentLine)
    {
        commandToken = string.Empty;
        argumentLine = string.Empty;

        var text = (rawMessage ?? string.Empty).Trim();
        if (text.Length == 0 || (text[0] != '!' && text[0] != '/'))
            return false;

        var body = text[1..].TrimStart();
        if (body.Length == 0)
            return false;

        var spaceIndex = body.IndexOf(' ');
        commandToken = spaceIndex < 0 ? body : body[..spaceIndex];
        argumentLine = spaceIndex < 0 ? string.Empty : body[(spaceIndex + 1)..].Trim();

        if (commandToken.StartsWith("css_", StringComparison.OrdinalIgnoreCase))
            commandToken = commandToken[4..];

        return _pluginChatCommands.Contains(commandToken);
    }

    private void DispatchPluginChatCommand(CCSPlayerController player, string commandToken, string argumentLine)
    {
        switch (commandToken.ToLowerInvariant())
        {
            case "admin":
            case "adminmenu":
                AdminMenu(player, null!);
                return;
            case "admins":
                CmdAdmins(player, null!);
                return;
            case "hideadmin":
                CmdHideAdmin(player, null!);
                return;
            case "banlist":
                BanListMenu(player, null!);
                return;
            case "version":
                CmdPluginVersion(player, null!);
                return;
        }

        var cssCommand = $"css_{commandToken}";
        var commandLine = string.IsNullOrWhiteSpace(argumentLine) ? cssCommand : $"{cssCommand} {argumentLine}";
        player.ExecuteClientCommandFromServer(commandLine);
    }

    private static string ExtractChatCommandToken(string message)
    {
        int i = 0;
        while (i < message.Length && (message[i] == '!' || message[i] == '/' || message[i] == ' '))
            i++;
        int start = i;
        while (i < message.Length && !char.IsWhiteSpace(message[i]))
            i++;
        return message.Substring(start, i - start);
    }
}
