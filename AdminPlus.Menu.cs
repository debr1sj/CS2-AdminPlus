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
using System.Text.Json.Nodes;
using System.Drawing;

namespace AdminPlus;

public partial class AdminPlus
{
    private static readonly List<string> PredefinedMaps = new()
    {
        "de_vertigo", "de_mirage", "de_inferno", "de_anubis", "de_nuke",
        "de_overpass", "de_train", "de_ancient", "de_dust2"
    };
    
    public static void CleanupMenu()
    {
        try
        {
            Console.WriteLine("[AdminPlus] Menu system cleaned up.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminPlus] Error during menu cleanup: {ex.Message}");
        }
    }

    public void RegisterMenuCommands()
    {
        AddCommand("admin", Localizer["Menu.AdminDesc"], AdminMenu);
        AddCommand("css_admin", "Open admin menu from console", AdminMenu);
        AddCommand("css_adminmenu", "Open admin menu from console", AdminMenu);
        AddCommand("banlist", Localizer["BanList.Header"], BanListMenu);
        AddCommand("css_banlist", "Show ban list menu from console", BanListMenu);
    }

    private string GetExecutorNameMenu(CCSPlayerController? caller)
    {
        return (caller == null || !caller.IsValid) ? Localizer["Console"] : caller.PlayerName;
    }

    public void AdminMenu(CCSPlayerController? caller, CommandInfo info)
    {
        bool isConsoleCommand = caller == null;
        
        if (isConsoleCommand)
        {
            Console.WriteLine("[AdminPlus] Admin menu opened from console.");
            return;
        }
        
        if (caller == null || !caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/generic"))
        {
            caller?.Print(Localizer["NoPermission"]);
            return;
        }

        if (!AdminManager.PlayerHasPermissions(caller, "@css/ban"))
        {
            caller.Print(Localizer["NoPermission"]);
            return;
        }

        var menu = CreateMenu(Localizer["Menu.Title"]);
        List<ChatMenuOptionData> options = [];

        if (AdminManager.PlayerHasPermissions(caller, "@css/root"))
            options.Add(new ChatMenuOptionData(Localizer["Menu.Option.AdminManage"], () => ShowAdminManageMenu(caller)));

        options.Add(new ChatMenuOptionData(Localizer["Menu.ServerCommands"], () => ShowServerCommands(caller)));
        options.Add(new ChatMenuOptionData(Localizer["Menu.Option.PlayerCommands"], () => ShowPlayerCommands(caller)));
        options.Add(new ChatMenuOptionData(Localizer["Menu.Fun.Title"], () => ShowFunRootMenu(caller)));

        foreach (var menuOptionData in options)
        {
            var menuName = menuOptionData.Name;
            menu?.AddMenuOption(menuName, (_, _) => { menuOptionData.Action.Invoke(); }, menuOptionData.Disabled);
        }

        if (menu != null) 
        {
            menu.ExitButton = true;
            OpenMenu(caller, menu);
        }
    }

    private void ShowFunRootMenu(CCSPlayerController admin)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/slay"))
        {
            admin.Print(Localizer["NoPermission"]);
            return;
        }

        var m = CreateMenu(Localizer["Menu.Fun.Title"]);
        if (m == null) return;
        
        m.AddMenuOption(Localizer["Menu.Fun.Cat.Teleport"], (p, o) => ShowFunTeleportMenu(admin));
        m.AddMenuOption(Localizer["Menu.Fun.Cat.PlayerFx"], (p, o) => ShowFunPlayerFxMenu(admin));
        m.AddMenuOption(Localizer["Menu.Fun.Cat.Weapons"], (p, o) => ShowFunWeaponsMenu(admin));
        m.AddMenuOption(Localizer["Menu.Fun.Cat.Physics"], (p, o) => ShowFunPhysicsMenu(admin));
        m.AddMenuOption(Localizer["Menu.Fun.Cat.Visual"], (p, o) => ShowFunVisualMenu(admin));
        {
            m.ExitButton = true;
            OpenMenu(admin, m);
        }
    }

    private IEnumerable<CCSPlayerController> GetLivePlayers()
        => Utilities.GetPlayers()!.Where(p => p != null && p.IsValid && !p.IsBot);

    private IEnumerable<CCSPlayerController> GetAllPlayers()
        => Utilities.GetPlayers()!.Where(p => p != null && p.IsValid);

    private void ShowTargetMenu(CCSPlayerController admin, string title, Action<string> onPicked, bool onlyAlive = true, bool onlyDead = false)
    {
        var m = CreateMenu(title);
        if (m == null) return;

        var players = GetAllPlayers();
        if (onlyAlive) players = players.Where(p => p.PawnIsAlive);
        if (onlyDead) players = players.Where(p => !p.PawnIsAlive);

        foreach (var pl in players)
        {
            var botIndicator = pl.IsBot ? " [BOT]" : "";
            var label = $"{SanitizeName(pl.PlayerName)}{botIndicator} [#{pl.UserId}]";
            var token = $"#{pl.UserId}";
            m?.AddMenuOption(label, (p, o) => onPicked(token));
        }

        if (m?.MenuOptions?.Any() != true)
            m?.AddMenuOption(Localizer["Menu.NoPlayers"], (p, o) => { });

        if (m != null)
        {
            m.ExitButton = true;
            OpenMenu(admin, m);
        }
    }

    private void ShowToggleMenu(CCSPlayerController admin, string title, Action<int> onPicked)
    {
        var m = CreateMenu(title);
        if (m == null) return;
        
        m.AddMenuOption(Localizer["On"], (p, o) => onPicked(1));
        m.AddMenuOption(Localizer["Off"], (p, o) => onPicked(0));
        {
            m.ExitButton = true;
            OpenMenu(admin, m);
        }
    }

    private void ShowNumberMenu(CCSPlayerController admin, string title, IEnumerable<string> options, Action<string> onPicked)
    {
        var m = CreateMenu(title);
        if (m == null) return;
        
        foreach (var opt in options)
            m.AddMenuOption(opt, (p, o) => onPicked(opt));
        {
            m.ExitButton = true;
            OpenMenu(admin, m);
        }
    }

    private void RunServerCmd(CCSPlayerController admin, string cmd)
    {
        AdminPlus._menuInvokerName = admin.PlayerName;
        Server.ExecuteCommand(cmd);
        AddTimer(0.1f, () => { AdminPlus._menuInvokerName = null; });
    }

    private void ShowFunTeleportMenu(CCSPlayerController admin)
    {
        var m = CreateMenu(Localizer["Menu.Fun.Cat.Teleport"]);
        if (m == null) return;

        m.AddMenuOption(Localizer["Menu.Fun.Teleport.Goto"], (p, o) =>
        {
            ShowTargetMenu(admin, Localizer["Menu.Fun.Prompt.SelectTarget"], target =>
            {
                RunServerCmd(admin, $"css_goto {target}");
                ShowFunTeleportMenu(admin);
            }, onlyAlive: true);
        });

        m.AddMenuOption(Localizer["Menu.Fun.Teleport.Bring"], (p, o) =>
        {
            ShowTargetMenu(admin, Localizer["Menu.Fun.Prompt.SelectTarget"], target =>
            {
                RunServerCmd(admin, $"css_bring {target}");
                ShowFunTeleportMenu(admin);
            }, onlyAlive: true);
        });



        if (m != null)
        {
            m.ExitButton = true;
            OpenMenu(admin, m);
        }
    }

    private void ShowFunPlayerFxMenu(CCSPlayerController admin)
    {
        var m = CreateMenu(Localizer["Menu.Fun.Cat.PlayerFx"]);
        if (m == null) return;

        m.AddMenuOption(Localizer["Menu.Fun.PlayerFx.Beacon"], (p, o) =>
        {
            ShowTargetMenu(admin, Localizer["Menu.Fun.Prompt.SelectTarget"], target =>
            {
                ShowToggleMenu(admin, Localizer["Menu.Fun.Prompt.Toggle"], val =>
                {
                    RunServerCmd(admin, $"css_beacon {target} {val}");
                    ShowFunPlayerFxMenu(admin);
                });
            }, onlyAlive: true);
        });

        m.AddMenuOption(Localizer["Menu.Fun.PlayerFx.Freeze"], (p, o) =>
        {
            ShowTargetMenu(admin, Localizer["Menu.Fun.Prompt.SelectTarget"], target =>
            {
                ShowNumberMenu(admin, Localizer["Menu.Fun.Prompt.Seconds"],
                    new[] { "3", "5", "10", "15", "30", "60" }, sec =>
                    {
                        RunServerCmd(admin, $"css_freeze {target} {sec}");
                        ShowFunPlayerFxMenu(admin);
                    });
            }, onlyAlive: true);
        });


        m.AddMenuOption(Localizer["Menu.Fun.PlayerFx.Blind"], (p, o) =>
        {
            ShowTargetMenu(admin, Localizer["Menu.Fun.Prompt.SelectTarget"], target =>
            {
                RunServerCmd(admin, $"css_blind {target} 5");
                ShowFunPlayerFxMenu(admin);
            }, onlyAlive: true);
        });


        m.AddMenuOption(Localizer["Menu.Fun.PlayerFx.Drug"], (p, o) =>
        {
            ShowTargetMenu(admin, Localizer["Menu.Fun.Prompt.SelectTarget"], target =>
            {
                RunServerCmd(admin, $"css_drug {target} 5");
                ShowFunPlayerFxMenu(admin);
            }, onlyAlive: true);
        });


        m.AddMenuOption(Localizer["Menu.Fun.PlayerFx.Shake"], (p, o) =>
        {
            ShowTargetMenu(admin, Localizer["Menu.Fun.Prompt.SelectTarget"], target =>
            {
                ShowNumberMenu(admin, Localizer["Menu.Fun.Prompt.Seconds"],
                    new[] { "2", "3", "5", "8", "10" }, sec =>
                    {
                        RunServerCmd(admin, $"css_shake {target} {sec}");
                        ShowFunPlayerFxMenu(admin);
                    });
            }, onlyAlive: true);
        });


        if (m != null)
        {
            m.ExitButton = true;
            OpenMenu(admin, m);
        }
    }

    private void ShowFunWeaponsMenu(CCSPlayerController admin)
    {
        var m = CreateMenu(Localizer["Menu.Fun.Cat.Weapons"]);
        if (m == null) return;

        m.AddMenuOption(Localizer["Menu.Fun.Weapons.Weapon"], (p, o) =>
        {
            ShowTargetMenu(admin, Localizer["Menu.Fun.Prompt.SelectTarget"], target =>
            {
                var weapons = new[] { "ak47", "m4a1", "m4a1_silencer", "awp", "deagle", "usp_silencer", "glock", "famas", "galilar", "mp9", "mp7", "p90" };
                ShowNumberMenu(admin, Localizer["Menu.Fun.Prompt.Weapon"], weapons, wpn =>
                {
                    RunServerCmd(admin, $"css_weapon {target} {wpn}");
                    ShowFunWeaponsMenu(admin);
                });
            }, onlyAlive: true);
        });

        m.AddMenuOption(Localizer["Menu.Fun.Weapons.Strip"], (p, o) =>
        {
            ShowTargetMenu(admin, Localizer["Menu.Fun.Prompt.SelectTarget"], target =>
            {
                var filters = new[] { "Primary Weapon", "Secondary Weapon", "Grenade", "C4", "All" };
                ShowNumberMenu(admin, "Select Weapon Type:", filters, filt =>
                {
                    var filterMap = new Dictionary<string, string>
                    {
                        ["Primary Weapon"] = "primary",
                        ["Secondary Weapon"] = "secondary",
                        ["Grenade"] = "grenade",
                        ["C4"] = "c4",
                        ["All"] = "all"
                    };
                    RunServerCmd(admin, $"css_strip {target} {filterMap[filt]}");
                    ShowFunWeaponsMenu(admin);
                });
            }, onlyAlive: true);
        });

        m.AddMenuOption(Localizer["Menu.Fun.Weapons.Clean"], (p, o) =>
        {
            RunServerCmd(admin, $"css_clean");
        });

        if (m != null)
        {
            m.ExitButton = true;
            OpenMenu(admin, m);
        }
    }

    private void ShowFunPhysicsMenu(CCSPlayerController admin)
    {
        var m = CreateMenu(Localizer["Menu.Fun.Cat.Physics"]);
        if (m == null) return;

        m.AddMenuOption(Localizer["Menu.Fun.Physics.Noclip"], (p, o) =>
        {
            ShowTargetMenu(admin, Localizer["Menu.Fun.Prompt.SelectTarget"], target =>
            {
                ShowToggleMenu(admin, Localizer["Menu.Fun.Prompt.Toggle"], val =>
                {
                    RunServerCmd(admin, $"css_noclip {target} {val}");
                    ShowFunPhysicsMenu(admin);
                });
            }, onlyAlive: true);
        });

        m.AddMenuOption(Localizer["Menu.Fun.Physics.Speed"], (p, o) =>
        {
            ShowTargetMenu(admin, Localizer["Menu.Fun.Prompt.SelectTarget"], target =>
            {
                var speeds = new[] { "0.5", "0.8", "1.0", "1.2", "1.5", "2.0" };
                ShowNumberMenu(admin, Localizer["Menu.Fun.Prompt.Speed"], speeds, sv =>
                {
                    RunServerCmd(admin, $"css_speed {target} {sv}");
                    ShowFunPhysicsMenu(admin);
                });
            }, onlyAlive: true);
        });


        m.AddMenuOption(Localizer["Menu.Fun.Physics.God"], (p, o) =>
        {
            ShowTargetMenu(admin, Localizer["Menu.Fun.Prompt.SelectTarget"], target =>
            {
                ShowToggleMenu(admin, Localizer["Menu.Fun.Prompt.Toggle"], val =>
                {
                    RunServerCmd(admin, $"css_god {target} {val}");
                    ShowFunPhysicsMenu(admin);
                });
            }, onlyAlive: true);
        });

        m.AddMenuOption(Localizer["Menu.Fun.Physics.Hp"], (p, o) =>
        {
            ShowTargetMenu(admin, Localizer["Menu.Fun.Prompt.SelectTarget"], target =>
            {
                var hps = new[] { "1", "50", "100", "150", "200", "255" };
                ShowNumberMenu(admin, Localizer["Menu.Fun.Prompt.Hp"], hps, hp =>
                {
                    RunServerCmd(admin, $"css_hp {target} {hp}");
                    ShowFunPhysicsMenu(admin);
                });
            }, onlyAlive: true);
        });

        m.AddMenuOption(Localizer["Menu.Fun.Physics.SetHp"], (p, o) =>
        {
            var tm = CreateMenu(Localizer["Menu.Fun.Prompt.SetHpTeam"]);
            if (tm == null) return;
            
            tm.AddMenuOption("Terrorist Team", (pp, oo) => ShowNumberMenu(admin, Localizer["Menu.Fun.Prompt.Hp"], new[] { "50", "100", "150", "200" }, hp => RunServerCmd(admin, $"css_sethp t {hp}")));
            tm.AddMenuOption("CT Team", (pp, oo) => ShowNumberMenu(admin, Localizer["Menu.Fun.Prompt.Hp"], new[] { "50", "100", "150", "200" }, hp => RunServerCmd(admin, $"css_sethp ct {hp}")));
            tm.ExitButton = true;
            OpenMenu(admin, tm);
        });

        if (m != null)
        {
            m.ExitButton = true;
            OpenMenu(admin, m);
        }
    }

    private void ShowFunVisualMenu(CCSPlayerController admin)
    {
        var m = CreateMenu(Localizer["Menu.Fun.Cat.Visual"]);
        if (m == null) return;

        m.AddMenuOption(Localizer["Menu.Fun.Visual.Glow"], (p, o) =>
        {
            ShowTargetMenu(admin, Localizer["Menu.Fun.Prompt.SelectTarget"], target =>
            {
                var colors = new[] { "Red", "Blue", "Green", "Yellow" };
                ShowNumberMenu(admin, "Select Color:", colors, col =>
                {
                    RunServerCmd(admin, $"css_glow {target} {col.ToLower()}");
                    ShowFunVisualMenu(admin);
                });
            }, onlyAlive: true);
        });


        m.AddMenuOption(Localizer["Menu.Fun.Visual.GlowOff"], (p, o) =>
        {
            ShowTargetMenu(admin, Localizer["Menu.Fun.Prompt.SelectTarget"], target =>
            {
                RunServerCmd(admin, $"css_glow {target} off");
                ShowFunVisualMenu(admin);
            }, onlyAlive: true);
        });

        if (m != null)
        {
            m.ExitButton = true;
            OpenMenu(admin, m);
        }
    }

    private void ShowFunTeamOpsMenu(CCSPlayerController admin)
    {
        var m = CreateMenu(Localizer["Menu.Fun.Cat.TeamOps"]);
        if (m == null) return;

        m.AddMenuOption(Localizer["Menu.Fun.TeamOps.Team"], (p, o) =>
        {
            ShowTargetMenu(admin, Localizer["Menu.Fun.Prompt.SelectTarget"], target =>
            {
                var tm = CreateMenu(Localizer["Menu.Fun.Prompt.Team"]);
                if (tm == null) return;
                
                tm.AddMenuOption("Terrorist Team", (pp, oo) => RunServerCmd(admin, $"css_team {target} t"));
                tm.AddMenuOption("CT Team", (pp, oo) => RunServerCmd(admin, $"css_team {target} ct"));
                tm.AddMenuOption("Spectator", (pp, oo) => RunServerCmd(admin, $"css_team {target} spec"));
                tm.ExitButton = true;
                OpenMenu(admin, tm);
            }, onlyAlive: true);
        });

        m.AddMenuOption(Localizer["Menu.Fun.TeamOps.Swap"], (p, o) =>
        {
            ShowTargetMenu(admin, Localizer["Menu.Fun.Prompt.SelectTarget"], target =>
            {
                RunServerCmd(admin, $"css_swap {target}");
            }, onlyAlive: true);
        });

        if (m != null)
        {
            m.ExitButton = true;
            OpenMenu(admin, m);
        }
    }

    private void ShowFunCleanupMenu(CCSPlayerController admin)
    {
        var m = CreateMenu(Localizer["Menu.Fun.Cat.Cleanup"]);
        if (m == null) return;
        
        m.AddMenuOption(Localizer["Menu.Fun.Cleanup.Clean"], (p, o) => RunServerCmd(admin, "css_clean"));
        {
            m.ExitButton = true;
            OpenMenu(admin, m);
        }
    }

    private void MenuClearDrug(CCSPlayerController admin, string targetToken)
    {
        IEnumerable<CCSPlayerController> targets = ResolveMenuTargets(targetToken);
        foreach (var t in targets)
        {
            StopTimer(t, FunTimer.Drug);
            var pawn = t.PlayerPawn?.Value;
            if (pawn != null)
            {
                pawn.Render = Color.White;
                Utilities.SetStateChanged(pawn, "CBaseModelEntity", "m_clrRender");
            }
        }
        admin.Print($"{{green}}Drug effect cleared for {targets.Count()} player(s).");
    }

    private IEnumerable<CCSPlayerController> ResolveMenuTargets(string token)
    {
        token = token?.Trim() ?? "";
        var all = GetLivePlayers();

        switch (token.ToLowerInvariant())
        {
            case "@t": return all.Where(p => p.Team == CsTeam.Terrorist);
            case "@ct": return all.Where(p => p.Team == CsTeam.CounterTerrorist);
            case "@spec": return all.Where(p => p.Team == CsTeam.Spectator);
            case "@all": return all;
        }

        if (token.StartsWith("#") && int.TryParse(token.AsSpan(1), out var uid))
        {
            return all.Where(p => p.UserId == uid);
        }

        return all.Where(p => (p.PlayerName ?? "").Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private void ShowAdminManageMenu(CCSPlayerController admin)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/root"))
        {
            admin.Print(Localizer["NoPermission"]);
            return;
        }

        var menu = CreateMenu(Localizer["Menu.AdminManage"]);
        if (menu == null) return;

        menu.AddMenuOption(Localizer["Menu.Option.AddAdmin"], (ply, opt) => ShowAddAdminPlayerMenu(admin));
        menu.AddMenuOption(Localizer["Menu.Option.RemoveAdmin"], (ply, opt) => OpenRemoveAdminMenu(admin));
        menu.AddMenuOption(Localizer["Menu.Option.ListAdmins"], (ply, opt) => ShowAdminListMenu(admin));

        menu.ExitButton = true;
        OpenMenu(admin, menu);
    }

    private void ShowAddAdminPlayerMenu(CCSPlayerController admin)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/root"))
        {
            admin.Print(Localizer["NoPermission"]);
            return;
        }

        var menu = CreateMenu(Localizer["Menu.AdminAdd.ChoosePlayer"]);
        if (menu == null) return;

        foreach (var p in Utilities.GetPlayers()!)
        {
            if (p == null || !p.IsValid || p.IsBot) continue;
            menu.AddMenuOption($"{SanitizeName(p.PlayerName)} [{p.SteamID}]", (ply, opt) =>
            {
                ShowAddAdminGroupMenu(admin, p.SteamID.ToString(), SanitizeName(p.PlayerName));
            });
        }

        if (!menu.MenuOptions.Any())
            menu.AddMenuOption(Localizer["Menu.NoPlayers"], (ply, opt) => { });

        menu.ExitButton = true;
        OpenMenu(admin, menu);
    }

    private void ShowAddAdminGroupMenu(CCSPlayerController admin, string steamId, string playerName)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/root"))
        {
            admin.Print(Localizer["NoPermission"]);
            return;
        }

        var menu = CreateMenu(Localizer["Menu.AdminAdd.ChooseGroup"]);
        if (menu == null) return;

        var groups = new[] { "#css/root", "#css/admin", "#css/mod", "#css/vip" };
        foreach (var group in groups)
        {
            menu.AddMenuOption(group, (ply, opt) =>
            {
                ShowAddAdminImmunityMenu(admin, steamId, playerName, group);
            });
        }

        menu.ExitButton = true;
        OpenMenu(admin, menu);
    }

    private void ShowAddAdminImmunityMenu(CCSPlayerController admin, string steamId, string playerName, string group)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/root"))
        {
            admin.Print(Localizer["NoPermission"]);
            return;
        }

        var menu = CreateMenu(Localizer["Menu.AdminAdd.ChooseImmunity"]);
        if (menu == null) return;

        var immLevels = new Dictionary<string, int>
        {
            { "10 (" + Localizer["Menu.Immunity.Low"] + ")", 10 },
            { "50 (" + Localizer["Menu.Immunity.Mid"] + ")", 50 },
            { "90 (" + Localizer["Menu.Immunity.High"] + ")", 90 },
            { "100 (" + Localizer["Menu.Immunity.Root"] + ")", 100 }
        };

        foreach (var entry in immLevels)
        {
            menu.AddMenuOption(entry.Key, (ply, opt) =>
            {
                ShowAddAdminConfirmMenu(admin, steamId, playerName, group, entry.Value);
            });
        }

        menu.ExitButton = true;
        OpenMenu(admin, menu);
    }

    private void ShowAddAdminConfirmMenu(CCSPlayerController admin, string steamId, string playerName, string group, int immunity)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/root"))
        {
            admin.Print(Localizer["NoPermission"]);
            return;
        }

        var menu = CreateMenu(Localizer["Menu.AdminAdd.Confirm"]);
        if (menu == null) return;

        string info = $"{playerName} [{steamId}]<br/>Grup: {group}<br/>Immunity: {immunity}";
        menu.AddMenuOption(Localizer["Menu.ConfirmYes"] + " → " + info, (ply, opt) =>
        {
            if (!AdminManager.PlayerHasPermissions(admin, "@css/root"))
            {
                admin.Print(Localizer["NoPermission"]);
                return;
            }

            if (!ReadAdminsFile(out var root)) root = new JsonObject();
            if (root.ContainsKey(steamId))
            {
                if (ulong.TryParse(steamId, out var steam64))
                {
                    var steamId3 = ConvertToSteamID3(steam64);
                    admin.Print(Localizer["Admin.Exists", $"{playerName} {steamId3}"]);
                }
                else
                {
                    admin.Print(Localizer["Admin.Exists", $"{playerName} {steamId}"]);
                }
                return;
            }

            var obj = new JsonObject
            {
                ["identity"] = steamId,
                ["name"] = playerName,
                ["immunity"] = immunity,
                ["groups"] = new JsonArray(group)
            };

            root[steamId] = obj;
            WriteAdminsFile(root);
            LoadImmunity();

            admin.Print(Localizer["Admin.Added", playerName, group, immunity]);
        });

        menu.AddMenuOption(Localizer["Menu.ConfirmNo"], (ply, opt) => ShowAdminManageMenu(admin));
        menu.ExitButton = true;
        OpenMenu(admin, menu);
    }

    private void OpenRemoveAdminMenu(CCSPlayerController admin)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/root"))
        {
            admin.Print(Localizer["NoPermission"]);
            return;
        }

        var menu = CreateMenu(Localizer["Menu.RemoveAdmin"]);
        if (menu == null) return;

        if (!ReadAdminsFile(out var root) || root.Count == 0)
        {
            menu.AddMenuOption(Localizer["Admin.List.Empty"], (ply, opt) => { });
        }
        else
        {
            var ordered = root.Select(kv =>
            {
                int imm = 0;
                string name = kv.Key;
                if (kv.Value is JsonObject obj)
                {
                    imm = obj["immunity"]?.GetValue<int?>() ?? 0;
                    name = obj["name"]?.GetValue<string>() ?? kv.Key;
                }
                return new { SteamId = kv.Key, Name = name, Immunity = imm };
            }).OrderByDescending(x => x.Immunity).ToList();

            foreach (var entry in ordered)
                menu.AddMenuOption($"{entry.Name} [{entry.SteamId}] (Imm:{entry.Immunity})", (ply, opt) =>
                {
                    ShowRemoveAdminConfirmMenu(admin, entry.SteamId, entry.Name, entry.Immunity);
                });
        }

        menu.ExitButton = true;
        OpenMenu(admin, menu);
    }

    private void ShowRemoveAdminConfirmMenu(CCSPlayerController admin, string steamId, string name, int immunity)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/root"))
        {
            admin.Print(Localizer["NoPermission"]);
            return;
        }

        var menu = CreateMenu(Localizer["Menu.RemoveAdmin"]);
        if (menu == null) return;

        string info = $"{name} [{steamId}] (Imm:{immunity})";
        menu.AddMenuOption(Localizer["Menu.ConfirmYes"] + " → " + info, (ply, opt) =>
        {
            if (!AdminManager.PlayerHasPermissions(admin, "@css/root"))
            {
                admin.Print(Localizer["NoPermission"]);
                return;
            }

            if (ReadAdminsFile(out var root) && root.ContainsKey(steamId))
            {
                root.Remove(steamId);
                WriteAdminsFile(root);
                LoadImmunity();
                admin.Print(Localizer["Admin.Removed", name]);
            }
            else admin.Print(Localizer["Admin.NotFound", steamId]);
        });

        menu.AddMenuOption(Localizer["Menu.ConfirmNo"], (ply, opt) => ShowAdminManageMenu(admin));
        menu.ExitButton = true;
        OpenMenu(admin, menu);
    }

    private void ShowAdminListMenu(CCSPlayerController admin)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/root"))
        {
            admin.Print(Localizer["NoPermission"]);
            return;
        }

        var menu = CreateMenu(Localizer["Admin.List.Header"]);
        if (menu == null) return;

        if (!ReadAdminsFile(out var root) || root.Count == 0)
            menu.AddMenuOption(Localizer["Admin.List.Empty"], (ply, opt) => { });
        else
        {
            var ordered = root.Select(kv =>
            {
                int imm = 0;
                string name = kv.Key;
                if (kv.Value is JsonObject obj)
                {
                    imm = obj["immunity"]?.GetValue<int?>() ?? 0;
                    name = obj["name"]?.GetValue<string>() ?? kv.Key;
                }
                return new { Name = name, Immunity = imm };
            }).OrderByDescending(x => x.Immunity).ToList();

            foreach (var entry in ordered)
                menu.AddMenuOption(Localizer["Admin.List.RowSimple", entry.Name, entry.Immunity], (ply, opt) => { });
        }

        menu.ExitButton = true;
        OpenMenu(admin, menu);
    }

    private void ShowPlayerCommands(CCSPlayerController admin)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/ban"))
        {
            admin.Print(Localizer["NoPermission"]);
            return;
        }

        var menu = CreateMenu(Localizer["Menu.PlayerCommands"]);
        List<ChatMenuOptionData> options = [];

        options.Add(new ChatMenuOptionData(Localizer["Menu.Option.Ban"], () => ShowPlayerList(admin)));
        options.Add(new ChatMenuOptionData(Localizer["Menu.Option.Kick"], () => ShowKickPlayerMenu(admin)));
        options.Add(new ChatMenuOptionData(Localizer["Menu.Option.Slay"], () => ShowSlayMenu(admin)));
        options.Add(new ChatMenuOptionData(Localizer["Menu.Option.Slap"], () => ShowSlapMenu(admin)));
        options.Add(new ChatMenuOptionData(Localizer["Menu.Option.Mute"], () => ShowMutePlayerMenu(admin)));
        options.Add(new ChatMenuOptionData(Localizer["Menu.Option.Gag"], () => ShowGagPlayerMenu(admin)));
        options.Add(new ChatMenuOptionData(Localizer["Menu.Option.Silence"], () => ShowSilencePlayerMenu(admin)));
        options.Add(new ChatMenuOptionData(Localizer["Menu.Fun.Teleport.Respawn"], () => ShowRespawnMenu(admin)));
        options.Add(new ChatMenuOptionData(Localizer["Menu.Option.Money"], () => ShowMoneyMenu(admin)));
        options.Add(new ChatMenuOptionData(Localizer["Menu.Option.Armor"], () => ShowArmorMenu(admin)));
        options.Add(new ChatMenuOptionData(Localizer["Menu.Fun.Cat.TeamOps"], () => ShowFunTeamOpsMenu(admin)));

        foreach (var menuOptionData in options)
        {
            var menuName = menuOptionData.Name;
            menu?.AddMenuOption(menuName, (_, _) => { menuOptionData.Action.Invoke(); }, menuOptionData.Disabled);
        }

        if (menu != null)
        {
            menu.ExitButton = true;
            OpenMenu(admin, menu);
        }
    }

    private void ShowSlapMenu(CCSPlayerController admin)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/slay"))
        { admin.Print(Localizer["NoPermission"]); return; }

        ShowTargetMenu(admin, Localizer["Menu.ChoosePlayer"], target =>
        {
            var damages = new[] { "0", "5", "10", "25", "50" };
            ShowNumberMenu(admin, Localizer["Menu.Fun.Prompt.Hp"], damages, dm =>
            {
                RunServerCmd(admin, $"css_slap {target} {dm}");
                ShowSlapMenu(admin);
            });
        }, onlyAlive: true);
    }

    private void ShowSlayMenu(CCSPlayerController admin)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/slay"))
        { admin.Print(Localizer["NoPermission"]); return; }

        ShowTargetMenu(admin, Localizer["Menu.ChoosePlayer"], target =>
        {
            RunServerCmd(admin, $"css_slay {target}");
            ShowSlayMenu(admin);
        }, onlyAlive: true);
    }

    private void ShowRespawnMenu(CCSPlayerController admin)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/slay"))
        { admin.Print(Localizer["NoPermission"]); return; }

        ShowTargetMenu(admin, Localizer["Menu.Fun.Prompt.SelectTarget"], target =>
        {
            RunServerCmd(admin, $"css_respawn {target}");
            ShowRespawnMenu(admin);
        }, onlyAlive: false, onlyDead: true);
    }

    private void ShowKickPlayerMenu(CCSPlayerController admin)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/generic"))
        {
            admin.Print(Localizer["NoPermission"]);
            return;
        }

        var menu = CreateMenu(Localizer["Menu.KickPlayer"]);
        if (menu == null) return;

        foreach (var p in Utilities.GetPlayers()!)
        {
            if (p == null || !p.IsValid || p.IsBot) continue;

            menu.AddMenuOption(SanitizeName(p.PlayerName), (ply, opt) =>
            {
                if (AdminManager.PlayerHasPermissions(admin, "@css/generic"))
                {
                    p.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_KICKED);
                    string reason = Localizer["Ban.NoReason"];
                    PlayerExtensions.PrintToAll(Localizer["Player.Kick.Success", admin.PlayerName, SanitizeName(p.PlayerName), reason]);
                    LogAction($"{admin.PlayerName} kicked {SanitizeName(p.PlayerName)}. Reason: {reason}");
                }
                else
                {
                    admin.Print(Localizer["NoPermission"]);
                }
            });
        }

        if (!menu.MenuOptions.Any())
            menu.AddMenuOption(Localizer["Menu.NoPlayers"], (ply, opt) => { });

        menu.ExitButton = true;
        OpenMenu(admin, menu);
    }

    private void ShowMutePlayerMenu(CCSPlayerController admin)
    {
        var menu = CreateMenu(Localizer["Menu.MutePlayer"]);
        if (menu == null) return;

        foreach (var p in Utilities.GetPlayers()!)
        {
            if (p == null || !p.IsValid || p.IsBot) continue;

            menu.AddMenuOption(SanitizeName(p.PlayerName), (ply, opt) =>
            {
                if (AdminManager.PlayerHasPermissions(admin, "@css/chat"))
                {
                    ShowCommDurationMenu(admin, p, "MUTE");
                }
                else
                {
                    admin.Print(Localizer["NoPermission"]);
                }
            });
        }

        if (!menu.MenuOptions.Any())
            menu.AddMenuOption(Localizer["Menu.NoPlayers"], (ply, opt) => { });

        menu.ExitButton = true;
        OpenMenu(admin, menu);
    }

    private void ShowGagPlayerMenu(CCSPlayerController admin)
    {
        var menu = CreateMenu(Localizer["Menu.GagPlayer"]);
        if (menu == null) return;

        foreach (var p in Utilities.GetPlayers()!)
        {
            if (p == null || !p.IsValid || p.IsBot) continue;

            menu.AddMenuOption(SanitizeName(p.PlayerName), (ply, opt) =>
            {
                if (AdminManager.PlayerHasPermissions(admin, "@css/chat"))
                {
                    ShowCommDurationMenu(admin, p, "GAG");
                }
                else
                {
                    admin.Print(Localizer["NoPermission"]);
                }
            });
        }

        if (!menu.MenuOptions.Any())
            menu.AddMenuOption(Localizer["Menu.NoPlayers"], (ply, opt) => { });

        menu.ExitButton = true;
        OpenMenu(admin, menu);
    }

    private void ShowSilencePlayerMenu(CCSPlayerController admin)
    {
        var menu = CreateMenu(Localizer["Menu.SilencePlayer"]);
        if (menu == null) return;

        foreach (var p in Utilities.GetPlayers()!)
        {
            if (p == null || !p.IsValid || p.IsBot) continue;

            menu.AddMenuOption(SanitizeName(p.PlayerName), (ply, opt) =>
            {
                if (AdminManager.PlayerHasPermissions(admin, "@css/chat"))
                {
                    ShowCommDurationMenu(admin, p, "SILENCE");
                }
                else
                {
                    admin.Print(Localizer["NoPermission"]);
                }
            });
        }

        if (!menu.MenuOptions.Any())
            menu.AddMenuOption(Localizer["Menu.NoPlayers"], (ply, opt) => { });

        menu.ExitButton = true;
        OpenMenu(admin, menu);
    }

    private void ShowCommDurationMenu(CCSPlayerController admin, CCSPlayerController target, string type)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/chat"))
        {
            admin.Print(Localizer["NoPermission"]);
            return;
        }

        var menu = CreateMenu(Localizer["Menu.ChooseDuration"]);
        if (menu == null) return;

        var durations = new Dictionary<string, int>
        {
            { "10 " + Localizer["Duration.Minute"], 10 },
            { "30 " + Localizer["Duration.Minute"], 30 },
            { "1 " + Localizer["Duration.Hour"], 60 },
            { "6 " + Localizer["Duration.Hour"], 360 },
            { "1 " + Localizer["Duration.Day"], 1440 },
            { "7 " + Localizer["Duration.Day"], 10080 },
            { Localizer["Duration.Forever"], 0 }
        };

        foreach (var entry in durations)
            menu.AddMenuOption(entry.Key, (ply, opt) => ApplyCommunicationPunishment(admin, target, type, entry.Value));

        menu.ExitButton = true;
        OpenMenu(admin, menu);
    }

    private void ApplyCommunicationPunishment(CCSPlayerController admin, CCSPlayerController target, string type, int duration)
    {
        string executorName = admin.PlayerName;
        string targetName = SanitizeName(target.PlayerName);

        if (type == "SILENCE")
        {
            ApplyPunishment(target, "MUTE", duration, "", admin);
            ApplyPunishment(target, "GAG", duration, "", admin);

            if (duration == 0)
                PlayerExtensions.PrintToAll(Localizer["PermaSILENCE", executorName, targetName]);
            else
                PlayerExtensions.PrintToAll(Localizer["SILENCE", executorName, targetName, duration]);

            LogAction($"{executorName} silenced {targetName} ({target.SteamID}) for {duration} minutes.");
        }
        else
        {
            ApplyPunishment(target, type, duration, "", admin);

            if (duration == 0)
                PlayerExtensions.PrintToAll(Localizer[$"Perma{type}", executorName, targetName]);
            else
                PlayerExtensions.PrintToAll(Localizer[$"{type}", executorName, targetName, duration]);

            LogAction($"{executorName} {type.ToLower()}ed {targetName} ({target.SteamID}) for {duration} minutes.");
        }
    }


    private void ShowServerCommands(CCSPlayerController admin)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/generic"))
        {
            admin.Print(Localizer["NoPermission"]);
            return;
        }

        var menu = CreateMenu(Localizer["Menu.ServerCommands"]);
        List<ChatMenuOptionData> options = [];

        options.Add(new ChatMenuOptionData(Localizer["Menu.Option.ChangeMap"], () => ShowMapSelectionMenu(admin)));
        options.Add(new ChatMenuOptionData(Localizer["Menu.Fun.Cat.Cleanup"], () => ShowFunCleanupMenu(admin)));
        options.Add(new ChatMenuOptionData(Localizer["Menu.Option.RoundRestart"], () =>
        {
            if (AdminManager.PlayerHasPermissions(admin, "@css/generic"))
            {
                Server.ExecuteCommand("mp_restartgame 1");
                PlayerExtensions.PrintToAll(Localizer["Round.Restarted", admin.PlayerName]);
            }
            else admin.Print(Localizer["NoPermission"]);
        }));

        foreach (var menuOptionData in options)
        {
            var menuName = menuOptionData.Name;
            menu?.AddMenuOption(menuName, (_, _) => { menuOptionData.Action.Invoke(); }, menuOptionData.Disabled);
        }

        if (menu != null)
        {
            menu.ExitButton = true;
            OpenMenu(admin, menu);
        }
    }

    private void ShowMapSelectionMenu(CCSPlayerController admin)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/generic"))
        {
            admin.Print(Localizer["NoPermission"]);
            return;
        }

        var menu = CreateMenu(Localizer["Menu.ChangeMap"]);
        if (menu == null) return;

        foreach (var map in PredefinedMaps)
            menu.AddMenuOption(map, (ply, opt) => ShowConfirmChangeMapMenu(admin, map));

        menu.ExitButton = true;
        OpenMenu(admin, menu);
    }

    private void ShowConfirmChangeMapMenu(CCSPlayerController admin, string map)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/generic"))
        {
            admin.Print(Localizer["NoPermission"]);
            return;
        }

        var menu = CreateMenu(Localizer["Menu.ChangeMapConfirm"]);
        if (menu == null) return;

        menu.AddMenuOption(Localizer["Menu.ConfirmYes"], (ply, opt) =>
        {
            if (AdminManager.PlayerHasPermissions(admin, "@css/generic"))
            {
                var currentMap = Server.MapName;
                if (currentMap == map)
                {
                    admin.Print($"{{green}}[AdminPlus]{{default}} You are already on {{yellow}}{map}{{default}} map!");
                    return;
                }

                PlayerExtensions.PrintToAll(Localizer["Map.Changed", admin.PlayerName, map]);
                AddTimer(2.0f, () =>
                {
                    try { Server.ExecuteCommand($"changelevel {map}"); }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[AdminPlus] Map change error: {ex.Message}");
                        admin.Print(Localizer["Map.NotFound", map]);
                    }
                });
            }
            else admin.Print(Localizer["NoPermission"]);
        });

        menu.AddMenuOption(Localizer["Menu.ConfirmNo"], (ply, opt) => ShowServerCommands(admin));
        menu.ExitButton = true;
        OpenMenu(admin, menu);
    }

    private void ShowPlayerList(CCSPlayerController admin)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/ban"))
        {
            admin.Print(Localizer["NoPermission"]);
            return;
        }

        var menu = CreateMenu(Localizer["Menu.ChoosePlayer"]);
        if (menu == null) return;

        foreach (var p in Utilities.GetPlayers()!)
        {
            if (p == null || !p.IsValid || p.IsBot) continue;
            menu.AddMenuOption(SanitizeName(p.PlayerName), (ply, opt) => ShowBanTypeMenu(admin, p));
        }

        if (!menu.MenuOptions.Any())
            menu.AddMenuOption(Localizer["Menu.NoPlayers"], (ply, opt) => { });

        menu.ExitButton = true;
        OpenMenu(admin, menu);
    }

    private void ShowBanTypeMenu(CCSPlayerController admin, CCSPlayerController target)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/ban"))
        {
            admin.Print(Localizer["NoPermission"]);
            return;
        }

        var menu = CreateMenu(Localizer["Menu.ChooseBanType"]);
        if (menu == null) return;
        
        menu.AddMenuOption(Localizer["Menu.Option.SteamIdBan"], (ply, opt) => ShowDurationMenu(admin, target));
        menu.AddMenuOption(Localizer["Menu.Option.IpBan"], (ply, opt) => ShowReasonMenu(admin, target, 0, true));
        menu.ExitButton = true;
        OpenMenu(admin, menu);
    }

    private void ShowDurationMenu(CCSPlayerController admin, CCSPlayerController target)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/ban"))
        {
            admin.Print(Localizer["NoPermission"]);
            return;
        }

        var menu = CreateMenu(Localizer["Menu.ChooseDuration"]);
        if (menu == null) return;

        var durations = new Dictionary<string, int>
        {
            { "5 " + Localizer["Duration.Minute"], 5 },
            { "30 " + Localizer["Duration.Minute"], 30 },
            { "1 " + Localizer["Duration.Hour"], 60 },
            { Localizer["Duration.Forever"], 0 }
        };

        foreach (var entry in durations)
            menu.AddMenuOption(entry.Key, (ply, opt) => ShowReasonMenu(admin, target, entry.Value, false));

        menu.ExitButton = true;
        OpenMenu(admin, menu);
    }

    private void ShowReasonMenu(CCSPlayerController admin, CCSPlayerController target, int minutes, bool isIpBan)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/ban"))
        {
            admin.Print(Localizer["NoPermission"]);
            return;
        }

        var menu = CreateMenu(Localizer["Menu.ChooseReason"]);
        if (menu == null) return;

        var reasons = new[]
        {
            Localizer["Reason.Cheat"], Localizer["Reason.Insult"], Localizer["Reason.Advertise"],
            Localizer["Reason.Troll"], Localizer["Reason.Other"], Localizer["Ban.NoReason"]
        };

        foreach (var reason in reasons)
        {
            menu.AddMenuOption(reason, (ply, opt) =>
            {
                if (!AdminManager.PlayerHasPermissions(admin, "@css/ban"))
                {
                    admin.Print(Localizer["NoPermission"]);
                    return;
                }

                var safeName = SanitizeName(target.PlayerName);

                if (isIpBan)
                {
                    string ip = target.IpAddress ?? "-";
                    var line = $"addip \"{ip}\" expiry:0 // {reason}";

                    lock (_lock)
                    {
                        IpBans[ip] = (0, line, safeName);
                        File.WriteAllLines(BannedIpPath, IpBans.Values.Select(x => x.line));
                    }

                    target.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_STEAM_BANNED);
                    PlayerExtensions.PrintToAll(Localizer["IpBan.AddedNick", admin.PlayerName, safeName, reason]);
                    LogAction($"{admin.PlayerName} ip-banned {safeName} ({ip}). Reason: {reason}");
                }
                else
                {
                    var steamId = target.SteamID.ToString();
                    var ip = target.IpAddress ?? "-";
                    var expiry = minutes == 0 ? 0 : DateTimeOffset.UtcNow.ToUnixTimeSeconds() + minutes * 60;
                    var line = $"banid \"{steamId}\" \"{safeName}\" ip:{ip} expiry:{expiry} // {reason}";

                    lock (_lock)
                    {
                        SteamBans[steamId] = (expiry, line, safeName, ip);
                        File.WriteAllLines(BannedUserPath, SteamBans.Values.Select(x => x.line));
                    }

                    target.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_STEAM_BANNED);
                    if (minutes == 0)
                        PlayerExtensions.PrintToAll(Localizer["PermabannedReason", admin.PlayerName, safeName, reason]);
                    else
                        PlayerExtensions.PrintToAll(Localizer["BannedReason", admin.PlayerName, safeName, minutes, reason]);

                    LogAction($"{admin.PlayerName} banned {safeName} ({steamId}) [IP:{ip}] for {minutes} minutes. Reason: {reason}");
                }
            });
        }

        menu.ExitButton = true;
        OpenMenu(admin, menu);
    }

    private void BanListMenu(CCSPlayerController? caller, CommandInfo info)
    {
        bool isConsoleCommand = caller == null;
        
        if (isConsoleCommand)
        {
            Console.WriteLine("[AdminPlus] Console ban list menu opened.");
            return;
        }
        
        if (caller == null || !caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/ban"))
        {
            caller?.Print(Localizer["NoPermission"]);
            return;
        }

        var root = CreateMenu(Localizer["Menu.BanList"]);
        if (root == null) return;
        
        root.AddMenuOption(Localizer["Menu.BanList.Steam"], (ply, opt) => ShowSteamBanListMenu(caller));
        root.AddMenuOption(Localizer["Menu.BanList.IP"], (ply, opt) => ShowIpBanListMenu(caller));
        root.ExitButton = true;
        OpenMenu(caller, root);
    }

    private void ShowSteamBanListMenu(CCSPlayerController caller)
    {
        if (!caller.IsValid) return;
        var menu = CreateMenu(Localizer["Menu.BanList.Steam"]);
        if (menu == null) return;

        if (SteamBans.Count == 0)
        {
            menu.AddMenuOption(Localizer["BanList.Steam.Empty"], (ply, opt) => { });
        }
        else
        {
            foreach (var kv in SteamBans)
                menu.AddMenuOption($"{SanitizeName(kv.Value.nick)} [{kv.Key}]", (ply, opt) => { });
        }

        if (menu != null)
        {
            menu.ExitButton = true;
            OpenMenu(caller, menu);
        }
    }

    private void ShowIpBanListMenu(CCSPlayerController caller)
    {
        if (!caller.IsValid) return;
        var menu = CreateMenu(Localizer["Menu.BanList.IP"]);
        if (menu == null) return;

        if (IpBans.Count == 0)
        {
            menu.AddMenuOption(Localizer["BanList.IP.Empty"], (ply, opt) => { });
        }
        else
        {
            foreach (var kv in IpBans)
                menu.AddMenuOption($"{SanitizeName(kv.Value.nick)} (IP) [{kv.Key}]", (ply, opt) => { });
        }

        if (menu != null)
        {
            menu.ExitButton = true;
            OpenMenu(caller, menu);
        }
    }

    private void ShowMoneyMenu(CCSPlayerController admin)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/slay"))
        {
            admin.Print(Localizer["NoPermission"]);
            return;
        }

        ShowTargetMenu(admin, Localizer["Menu.ChoosePlayerMoney"], target =>
        {
            var amounts = new[] { "500", "1000", "2000", "5000", "10000", "15000", "20000" };
            ShowNumberMenu(admin, Localizer["Menu.Fun.Prompt.Money"], amounts, amount =>
            {
                RunServerCmd(admin, $"css_money {target} {amount}");
                ShowMoneyMenu(admin);
            });
        }, onlyAlive: true);
    }

    private void ShowArmorMenu(CCSPlayerController admin)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/slay"))
        {
            admin.Print(Localizer["NoPermission"]);
            return;
        }

        ShowTargetMenu(admin, Localizer["Menu.ChoosePlayerArmor"], target =>
        {
            var amounts = new[] { "50", "100", "150", "200", "300", "400", "500" };
            ShowNumberMenu(admin, Localizer["Menu.Fun.Prompt.Armor"], amounts, amount =>
            {
                RunServerCmd(admin, $"css_armor {target} {amount}");
                ShowArmorMenu(admin);
            });
        }, onlyAlive: true);
    }
}

public class ChatMenuOptionData(string name, Action action, bool disabled = false)
{
    public readonly string Name = name;
    public readonly Action Action = action;
    public readonly bool Disabled = disabled;
}
