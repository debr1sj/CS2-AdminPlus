// FILE: AdminPlus.Help.cs
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Commands;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace AdminPlus;

public partial class AdminPlus
{
    public static void CleanupHelp()
    {
        try
        {
            Console.WriteLine("[AdminPlus] Help system cleaned up.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminPlus] Error during help cleanup: {ex.Message}");
        }
    }

    public void RegisterHelpCommands()
    {
        AddCommand("css_adminhelp", "Detailed help for admin commands", CmdAdminHelp);
    }

    private void CmdAdminHelp(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null && (!caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/generic")))
        {
            if (caller.IsValid) caller.PrintToChat(Localizer["NoPermission"]);
            return;
        }

        if (caller != null && caller.IsValid)
        {
            caller.PrintToChat(Localizer["Help.Chat.ConsoleOutput"]);
        }
        
        ShowAdminHelpConsole(caller);
    }

    private void ShowAdminHelpConsole(CCSPlayerController? caller)
    {
        if (caller != null && caller.IsValid)
        {
            caller.PrintToConsole(Localizer["Help.ConsoleTitle"]);
            caller.PrintToConsole("");
            
            caller.PrintToConsole(Localizer["Help.Console.BanTitle"]);
            caller.PrintToConsole("");
            caller.PrintToConsole(Localizer["Help.Console.Ban.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Ipban.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Unban.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Lastban.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Baninfo.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.CleanBans.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.CleanIpBans.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.CleanSteamBans.Usage"]);
            caller.PrintToConsole("");
            
            caller.PrintToConsole(Localizer["Help.Console.MuteTitle"]);
            caller.PrintToConsole("");
            caller.PrintToConsole(Localizer["Help.Console.Mute.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Unmute.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Gag.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Ungag.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Silence.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Unsilence.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Mutelist.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Gaglist.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.CleanAll.Usage"]);
            caller.PrintToConsole("");
            
            caller.PrintToConsole(Localizer["Help.Console.TeleTitle"]);
            caller.PrintToConsole("");
            caller.PrintToConsole(Localizer["Help.Console.Bring.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Send.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Goto.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Noclip.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Hrespawn.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Respawn.Usage"]);
            caller.PrintToConsole("");
            
            caller.PrintToConsole(Localizer["Help.Console.GameTitle"]);
            caller.PrintToConsole("");
            caller.PrintToConsole(Localizer["Help.Console.Slay.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Freeze.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Unfreeze.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.God.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Ungod.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Hp.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Money.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Armor.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Speed.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Unspeed.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Team.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Swap.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Weapon.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Strip.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Sethp.Usage"]);
            caller.PrintToConsole("");
            
            caller.PrintToConsole(Localizer["Help.Console.FunTitle"]);
            caller.PrintToConsole("");
            caller.PrintToConsole(Localizer["Help.Console.Beacon.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Shake.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Unshake.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Blind.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Unblind.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Drug.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Undrug.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Glow.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Color.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Bury.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Unbury.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Gravity.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Clean.Usage"]);
            caller.PrintToConsole("");
            
            caller.PrintToConsole(Localizer["Help.Console.ChatTitle"]);
            caller.PrintToConsole("");
            caller.PrintToConsole(Localizer["Help.Console.Asay.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Csay.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Hsay.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Psay.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Say.Usage"]);
            caller.PrintToConsole("");
            
            caller.PrintToConsole(Localizer["Help.Console.AdminManageTitle"]);
            caller.PrintToConsole("");
            caller.PrintToConsole(Localizer["Help.Console.Addadmin.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Removeadmin.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Adminlist.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Admins.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.AdminMenu.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.BanlistMenu.Usage"]);
            caller.PrintToConsole("");
            
            caller.PrintToConsole(Localizer["Help.Console.VoteTitle"]);
            caller.PrintToConsole("");
            caller.PrintToConsole(Localizer["Help.Console.Voteban.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Votekick.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Votemute.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Votemap.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Vote.Usage"]);
            caller.PrintToConsole("");
            
            caller.PrintToConsole(Localizer["Help.Console.ServerTitle"]);
            caller.PrintToConsole("");
            caller.PrintToConsole(Localizer["Help.Console.Map.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Restart.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Shutdown.Usage"]);
            caller.PrintToConsole(Localizer["Help.Console.Rcon.Usage"]);
            caller.PrintToConsole("");
            
            caller.PrintToConsole(Localizer["Help.Console.HelpTitle"]);
            caller.PrintToConsole("");
            caller.PrintToConsole(Localizer["Help.Console.AdminHelp.Usage"]);
            caller.PrintToConsole("");
            
            caller.PrintToConsole(Localizer["Help.Console.Tip1"]);
            caller.PrintToConsole(Localizer["Help.Console.Tip2"]);
            caller.PrintToConsole(Localizer["Help.Console.Tip3"]);
        }
        else
        {
            Console.WriteLine(Localizer["Help.ConsoleTitle"]);
            Console.WriteLine("");
            
            Console.WriteLine(Localizer["Help.Console.BanTitle"]);
            Console.WriteLine("");
            Console.WriteLine(Localizer["Help.Console.Ban.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Ipban.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Unban.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Lastban.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Baninfo.Usage"]);
            Console.WriteLine(Localizer["Help.Console.CleanBans.Usage"]);
            Console.WriteLine(Localizer["Help.Console.CleanIpBans.Usage"]);
            Console.WriteLine(Localizer["Help.Console.CleanSteamBans.Usage"]);
            Console.WriteLine("");
            
            Console.WriteLine(Localizer["Help.Console.MuteTitle"]);
            Console.WriteLine("");
            Console.WriteLine(Localizer["Help.Console.Mute.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Unmute.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Gag.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Ungag.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Silence.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Unsilence.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Mutelist.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Gaglist.Usage"]);
            Console.WriteLine(Localizer["Help.Console.CleanAll.Usage"]);
            Console.WriteLine("");
            
            Console.WriteLine(Localizer["Help.Console.TeleTitle"]);
            Console.WriteLine("");
            Console.WriteLine(Localizer["Help.Console.Bring.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Send.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Goto.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Noclip.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Hrespawn.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Respawn.Usage"]);
            Console.WriteLine("");
            
            Console.WriteLine(Localizer["Help.Console.GameTitle"]);
            Console.WriteLine("");
            Console.WriteLine(Localizer["Help.Console.Slay.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Freeze.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Unfreeze.Usage"]);
            Console.WriteLine(Localizer["Help.Console.God.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Ungod.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Hp.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Money.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Armor.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Speed.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Unspeed.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Team.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Swap.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Weapon.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Strip.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Sethp.Usage"]);
            Console.WriteLine("");
            
            Console.WriteLine(Localizer["Help.Console.FunTitle"]);
            Console.WriteLine("");
            Console.WriteLine(Localizer["Help.Console.Beacon.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Shake.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Unshake.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Blind.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Unblind.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Drug.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Undrug.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Glow.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Color.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Bury.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Unbury.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Gravity.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Clean.Usage"]);
            Console.WriteLine("");
            
            Console.WriteLine(Localizer["Help.Console.ChatTitle"]);
            Console.WriteLine("");
            Console.WriteLine(Localizer["Help.Console.Asay.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Csay.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Hsay.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Psay.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Say.Usage"]);
            Console.WriteLine("");
            
            Console.WriteLine(Localizer["Help.Console.AdminManageTitle"]);
            Console.WriteLine("");
            Console.WriteLine(Localizer["Help.Console.Addadmin.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Removeadmin.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Adminlist.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Admins.Usage"]);
            Console.WriteLine(Localizer["Help.Console.AdminMenu.Usage"]);
            Console.WriteLine(Localizer["Help.Console.BanlistMenu.Usage"]);
            Console.WriteLine("");
            
            Console.WriteLine(Localizer["Help.Console.VoteTitle"]);
            Console.WriteLine("");
            Console.WriteLine(Localizer["Help.Console.Voteban.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Votekick.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Votemute.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Votemap.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Vote.Usage"]);
            Console.WriteLine("");
            
            Console.WriteLine(Localizer["Help.Console.ServerTitle"]);
            Console.WriteLine("");
            Console.WriteLine(Localizer["Help.Console.Map.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Restart.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Shutdown.Usage"]);
            Console.WriteLine(Localizer["Help.Console.Rcon.Usage"]);
            Console.WriteLine("");
            
            Console.WriteLine(Localizer["Help.Console.HelpTitle"]);
            Console.WriteLine("");
            Console.WriteLine(Localizer["Help.Console.AdminHelp.Usage"]);
            Console.WriteLine("");
            
            Console.WriteLine(Localizer["Help.Console.Tip1"]);
            Console.WriteLine(Localizer["Help.Console.Tip2"]);
            Console.WriteLine(Localizer["Help.Console.Tip3"]);
        }
    }
}