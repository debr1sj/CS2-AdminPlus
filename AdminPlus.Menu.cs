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
        AddCommand("banlist", Localizer["BanList.Header"], BanListMenu);
    }

    private string GetExecutorNameMenu(CCSPlayerController? caller)
    {
        return (caller == null || !caller.IsValid) ? Localizer["Console"] : caller.PlayerName;
    }

    public void AdminMenu(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null || !caller.IsValid) return;

        if (!AdminManager.PlayerHasPermissions(caller, "@css/ban"))
        {
            caller.PrintToChat(Localizer["NoPermission"]);
            return;
        }

        var menu = new CenterHtmlMenu(Localizer["Menu.Title"], this);

        if (AdminManager.PlayerHasPermissions(caller, "@css/root"))
            menu.AddMenuOption(Localizer["Menu.Option.AdminManage"], (ply, opt) => ShowAdminManageMenu(caller));

        menu.AddMenuOption(Localizer["Menu.ServerCommands"], (ply, opt) => ShowServerCommands(caller));
        menu.AddMenuOption(Localizer["Menu.Option.PlayerCommands"], (ply, opt) => ShowPlayerCommands(ply));

        menu.AddMenuOption(Localizer["Menu.Fun.Title"], (ply, opt) => ShowFunRootMenu(ply));

        menu.ExitButton = true;
        menu.Open(caller);
    }

    private void ShowFunRootMenu(CCSPlayerController admin)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/slay"))
        {
            admin.PrintToChat(Localizer["NoPermission"]);
            return;
        }

        var m = new CenterHtmlMenu(Localizer["Menu.Fun.Title"], this);
        m.AddMenuOption(Localizer["Menu.Fun.Cat.Teleport"], (p, o) => ShowFunTeleportMenu(admin));
        m.AddMenuOption(Localizer["Menu.Fun.Cat.PlayerFx"], (p, o) => ShowFunPlayerFxMenu(admin));
        m.AddMenuOption(Localizer["Menu.Fun.Cat.Weapons"], (p, o) => ShowFunWeaponsMenu(admin));
        m.AddMenuOption(Localizer["Menu.Fun.Cat.Physics"], (p, o) => ShowFunPhysicsMenu(admin));
        m.AddMenuOption(Localizer["Menu.Fun.Cat.Visual"], (p, o) => ShowFunVisualMenu(admin));
        m.ExitButton = true;
        m.Open(admin);
    }

    private IEnumerable<CCSPlayerController> GetLivePlayers()
        => Utilities.GetPlayers()!.Where(p => p != null && p.IsValid && !p.IsBot);

    private IEnumerable<CCSPlayerController> GetAllPlayers()
        => Utilities.GetPlayers()!.Where(p => p != null && p.IsValid);

    private void ShowTargetMenu(CCSPlayerController admin, string title, Action<string> onPicked, bool onlyAlive = true, bool onlyDead = false)
    {
        var m = new CenterHtmlMenu(title, this);

        var players = GetAllPlayers();
        if (onlyAlive) players = players.Where(p => p.PawnIsAlive);
        if (onlyDead) players = players.Where(p => !p.PawnIsAlive);

        foreach (var pl in players)
        {
            var botIndicator = pl.IsBot ? " [BOT]" : "";
            var label = $"{SanitizeName(pl.PlayerName)}{botIndicator} [#{pl.UserId}]";
            var token = $"#{pl.UserId}";
            m.AddMenuOption(label, (p, o) => onPicked(token));
        }

        if (!m.MenuOptions.Any())
            m.AddMenuOption(Localizer["Menu.NoPlayers"], (p, o) => { });

        m.ExitButton = true;
        m.Open(admin);
    }

    private void ShowToggleMenu(CCSPlayerController admin, string title, Action<int> onPicked)
    {
        var m = new CenterHtmlMenu(title, this);
        m.AddMenuOption(Localizer["On"], (p, o) => onPicked(1));
        m.AddMenuOption(Localizer["Off"], (p, o) => onPicked(0));
        m.ExitButton = true;
        m.Open(admin);
    }

    private void ShowNumberMenu(CCSPlayerController admin, string title, IEnumerable<string> options, Action<string> onPicked)
    {
        var m = new CenterHtmlMenu(title, this);
        foreach (var opt in options)
            m.AddMenuOption(opt, (p, o) => onPicked(opt));
        m.ExitButton = true;
        m.Open(admin);
    }

    private void RunServerCmd(CCSPlayerController admin, string cmd)
    {
        AdminPlus._menuInvokerName = admin.PlayerName;
        Server.ExecuteCommand(cmd);
        AddTimer(0.1f, () => { AdminPlus._menuInvokerName = null; });
    }

    private void ShowFunTeleportMenu(CCSPlayerController admin)
    {
        var m = new CenterHtmlMenu(Localizer["Menu.Fun.Cat.Teleport"], this);

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



        m.ExitButton = true;
        m.Open(admin);
    }

    private void ShowFunPlayerFxMenu(CCSPlayerController admin)
    {
        var m = new CenterHtmlMenu(Localizer["Menu.Fun.Cat.PlayerFx"], this);

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


        m.ExitButton = true;
        m.Open(admin);
    }

    private void ShowFunWeaponsMenu(CCSPlayerController admin)
    {
        var m = new CenterHtmlMenu(Localizer["Menu.Fun.Cat.Weapons"], this);

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

        m.ExitButton = true;
        m.Open(admin);
    }

    private void ShowFunPhysicsMenu(CCSPlayerController admin)
    {
        var m = new CenterHtmlMenu(Localizer["Menu.Fun.Cat.Physics"], this);

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
            var tm = new CenterHtmlMenu(Localizer["Menu.Fun.Prompt.SetHpTeam"], this);
            tm.AddMenuOption("Terrorist Team", (pp, oo) => ShowNumberMenu(admin, Localizer["Menu.Fun.Prompt.Hp"], new[] { "50", "100", "150", "200" }, hp => RunServerCmd(admin, $"css_sethp t {hp}")));
            tm.AddMenuOption("CT Team", (pp, oo) => ShowNumberMenu(admin, Localizer["Menu.Fun.Prompt.Hp"], new[] { "50", "100", "150", "200" }, hp => RunServerCmd(admin, $"css_sethp ct {hp}")));
            tm.ExitButton = true;
            tm.Open(admin);
        });

        m.ExitButton = true;
        m.Open(admin);
    }

    private void ShowFunVisualMenu(CCSPlayerController admin)
    {
        var m = new CenterHtmlMenu(Localizer["Menu.Fun.Cat.Visual"], this);

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

        m.ExitButton = true;
        m.Open(admin);
    }

    private void ShowFunTeamOpsMenu(CCSPlayerController admin)
    {
        var m = new CenterHtmlMenu(Localizer["Menu.Fun.Cat.TeamOps"], this);

        m.AddMenuOption(Localizer["Menu.Fun.TeamOps.Team"], (p, o) =>
        {
            ShowTargetMenu(admin, Localizer["Menu.Fun.Prompt.SelectTarget"], target =>
            {
                var tm = new CenterHtmlMenu(Localizer["Menu.Fun.Prompt.Team"], this);
                tm.AddMenuOption("Terrorist Team", (pp, oo) => RunServerCmd(admin, $"css_team {target} t"));
                tm.AddMenuOption("CT Team", (pp, oo) => RunServerCmd(admin, $"css_team {target} ct"));
                tm.AddMenuOption("Spectator", (pp, oo) => RunServerCmd(admin, $"css_team {target} spec"));
                tm.ExitButton = true;
                tm.Open(admin);
            }, onlyAlive: true);
        });

        m.AddMenuOption(Localizer["Menu.Fun.TeamOps.Swap"], (p, o) =>
        {
            ShowTargetMenu(admin, Localizer["Menu.Fun.Prompt.SelectTarget"], target =>
            {
                RunServerCmd(admin, $"css_swap {target}");
            }, onlyAlive: true);
        });

        m.ExitButton = true;
        m.Open(admin);
    }

    private void ShowFunCleanupMenu(CCSPlayerController admin)
    {
        var m = new CenterHtmlMenu(Localizer["Menu.Fun.Cat.Cleanup"], this);
        m.AddMenuOption(Localizer["Menu.Fun.Cleanup.Clean"], (p, o) => RunServerCmd(admin, "css_clean"));
        m.ExitButton = true;
        m.Open(admin);
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
        admin.PrintToChat($"{{green}}Drug effect cleared for {targets.Count()} player(s).");
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
            admin.PrintToChat(Localizer["NoPermission"]);
            return;
        }

        var menu = new CenterHtmlMenu(Localizer["Menu.AdminManage"], this);

        menu.AddMenuOption(Localizer["Menu.Option.AddAdmin"], (ply, opt) => ShowAddAdminPlayerMenu(admin));
        menu.AddMenuOption(Localizer["Menu.Option.RemoveAdmin"], (ply, opt) => OpenRemoveAdminMenu(admin));
        menu.AddMenuOption(Localizer["Menu.Option.ListAdmins"], (ply, opt) => ShowAdminListMenu(admin));

        menu.ExitButton = true;
        menu.Open(admin);
    }

    private void ShowAddAdminPlayerMenu(CCSPlayerController admin)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/root"))
        {
            admin.PrintToChat(Localizer["NoPermission"]);
            return;
        }

        var menu = new CenterHtmlMenu(Localizer["Menu.AdminAdd.ChoosePlayer"], this);

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
        menu.Open(admin);
    }

    private void ShowAddAdminGroupMenu(CCSPlayerController admin, string steamId, string playerName)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/root"))
        {
            admin.PrintToChat(Localizer["NoPermission"]);
            return;
        }

        var menu = new CenterHtmlMenu(Localizer["Menu.AdminAdd.ChooseGroup"], this);

        var groups = new[] { "#css/root", "#css/admin", "#css/mod", "#css/vip" };
        foreach (var group in groups)
        {
            menu.AddMenuOption(group, (ply, opt) =>
            {
                ShowAddAdminImmunityMenu(admin, steamId, playerName, group);
            });
        }

        menu.ExitButton = true;
        menu.Open(admin);
    }

    private void ShowAddAdminImmunityMenu(CCSPlayerController admin, string steamId, string playerName, string group)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/root"))
        {
            admin.PrintToChat(Localizer["NoPermission"]);
            return;
        }

        var menu = new CenterHtmlMenu(Localizer["Menu.AdminAdd.ChooseImmunity"], this);

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
        menu.Open(admin);
    }

    private void ShowAddAdminConfirmMenu(CCSPlayerController admin, string steamId, string playerName, string group, int immunity)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/root"))
        {
            admin.PrintToChat(Localizer["NoPermission"]);
            return;
        }

        var menu = new CenterHtmlMenu(Localizer["Menu.AdminAdd.Confirm"], this);

        string info = $"{playerName} [{steamId}]<br/>Grup: {group}<br/>Immunity: {immunity}";
        menu.AddMenuOption(Localizer["Menu.ConfirmYes"] + " → " + info, (ply, opt) =>
        {
            if (!AdminManager.PlayerHasPermissions(admin, "@css/root"))
            {
                admin.PrintToChat(Localizer["NoPermission"]);
                return;
            }

            if (!ReadAdminsFile(out var root)) root = new JsonObject();
            if (root.ContainsKey(steamId))
            {
                // SteamID64'ü SteamID3'e dönüştür
                if (ulong.TryParse(steamId, out var steam64))
                {
                    var steamId3 = ConvertToSteamID3(steam64);
                    admin.PrintToChat(Localizer["Admin.Exists", $"{playerName} {steamId3}"]);
                }
                else
                {
                    admin.PrintToChat(Localizer["Admin.Exists", $"{playerName} {steamId}"]);
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

            admin.PrintToChat(Localizer["Admin.Added", playerName, group, immunity]);
        });

        menu.AddMenuOption(Localizer["Menu.ConfirmNo"], (ply, opt) => ShowAdminManageMenu(admin));
        menu.ExitButton = true;
        menu.Open(admin);
    }

    private void OpenRemoveAdminMenu(CCSPlayerController admin)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/root"))
        {
            admin.PrintToChat(Localizer["NoPermission"]);
            return;
        }

        var menu = new CenterHtmlMenu(Localizer["Menu.RemoveAdmin"], this);

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
        menu.Open(admin);
    }

    private void ShowRemoveAdminConfirmMenu(CCSPlayerController admin, string steamId, string name, int immunity)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/root"))
        {
            admin.PrintToChat(Localizer["NoPermission"]);
            return;
        }

        var menu = new CenterHtmlMenu(Localizer["Menu.RemoveAdmin"], this);

        string info = $"{name} [{steamId}] (Imm:{immunity})";
        menu.AddMenuOption(Localizer["Menu.ConfirmYes"] + " → " + info, (ply, opt) =>
        {
            if (!AdminManager.PlayerHasPermissions(admin, "@css/root"))
            {
                admin.PrintToChat(Localizer["NoPermission"]);
                return;
            }

            if (ReadAdminsFile(out var root) && root.ContainsKey(steamId))
            {
                root.Remove(steamId);
                WriteAdminsFile(root);
                LoadImmunity();
                admin.PrintToChat(Localizer["Admin.Removed", name]);
            }
            else admin.PrintToChat(Localizer["Admin.NotFound", steamId]);
        });

        menu.AddMenuOption(Localizer["Menu.ConfirmNo"], (ply, opt) => ShowAdminManageMenu(admin));
        menu.ExitButton = true;
        menu.Open(admin);
    }

    private void ShowAdminListMenu(CCSPlayerController admin)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/root"))
        {
            admin.PrintToChat(Localizer["NoPermission"]);
            return;
        }

        var menu = new CenterHtmlMenu(Localizer["Admin.List.Header"], this);

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
        menu.Open(admin);
    }

    private void ShowPlayerCommands(CCSPlayerController admin)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/ban"))
        {
            admin.PrintToChat(Localizer["NoPermission"]);
            return;
        }

        var menu = new CenterHtmlMenu(Localizer["Menu.PlayerCommands"], this);

        menu.AddMenuOption(Localizer["Menu.Option.Ban"], (ply, opt) => ShowPlayerList(admin));
        menu.AddMenuOption(Localizer["Menu.Option.Kick"], (ply, opt) => ShowKickPlayerMenu(admin));
        menu.AddMenuOption(Localizer["Menu.Option.Slay"], (ply, opt) => ShowSlayMenu(admin));
        menu.AddMenuOption(Localizer["Menu.Option.Slap"], (ply, opt) => ShowSlapMenu(admin));
        menu.AddMenuOption(Localizer["Menu.Option.Mute"], (ply, opt) => ShowMutePlayerMenu(admin));
        menu.AddMenuOption(Localizer["Menu.Option.Gag"], (ply, opt) => ShowGagPlayerMenu(admin));
        menu.AddMenuOption(Localizer["Menu.Option.Silence"], (ply, opt) => ShowSilencePlayerMenu(admin));
        menu.AddMenuOption(Localizer["Menu.Fun.Teleport.Respawn"], (ply, opt) => ShowRespawnMenu(admin));
        menu.AddMenuOption(Localizer["Menu.Option.Money"], (ply, opt) => ShowMoneyMenu(admin));
        menu.AddMenuOption(Localizer["Menu.Option.Armor"], (ply, opt) => ShowArmorMenu(admin));
        menu.AddMenuOption(Localizer["Menu.Fun.Cat.TeamOps"], (ply, opt) => ShowFunTeamOpsMenu(admin));

        menu.ExitButton = true;
        menu.Open(admin);
    }

    private void ShowSlapMenu(CCSPlayerController admin)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/slay"))
        { admin.PrintToChat(Localizer["NoPermission"]); return; }

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
        { admin.PrintToChat(Localizer["NoPermission"]); return; }

        ShowTargetMenu(admin, Localizer["Menu.ChoosePlayer"], target =>
        {
            RunServerCmd(admin, $"css_slay {target}");
            ShowSlayMenu(admin);
        }, onlyAlive: true);
    }

    private void ShowRespawnMenu(CCSPlayerController admin)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/slay"))
        { admin.PrintToChat(Localizer["NoPermission"]); return; }

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
            admin.PrintToChat(Localizer["NoPermission"]);
            return;
        }

        var menu = new CenterHtmlMenu(Localizer["Menu.KickPlayer"], this);

        foreach (var p in Utilities.GetPlayers()!)
        {
            if (p == null || !p.IsValid || p.IsBot) continue;

            menu.AddMenuOption(SanitizeName(p.PlayerName), (ply, opt) =>
            {
                if (AdminManager.PlayerHasPermissions(admin, "@css/generic"))
                {
                    p.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_KICKED);
                    string reason = Localizer["Ban.NoReason"];
                    Server.PrintToChatAll(Localizer["Player.Kick.Success", admin.PlayerName, SanitizeName(p.PlayerName), reason]);
                    LogAction($"{admin.PlayerName} kicked {SanitizeName(p.PlayerName)}. Reason: {reason}");
                }
                else
                {
                    admin.PrintToChat(Localizer["NoPermission"]);
                }
            });
        }

        if (!menu.MenuOptions.Any())
            menu.AddMenuOption(Localizer["Menu.NoPlayers"], (ply, opt) => { });

        menu.ExitButton = true;
        menu.Open(admin);
    }

    private void ShowMutePlayerMenu(CCSPlayerController admin)
    {
        var menu = new CenterHtmlMenu(Localizer["Menu.MutePlayer"], this);

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
                    admin.PrintToChat(Localizer["NoPermission"]);
                }
            });
        }

        if (!menu.MenuOptions.Any())
            menu.AddMenuOption(Localizer["Menu.NoPlayers"], (ply, opt) => { });

        menu.ExitButton = true;
        menu.Open(admin);
    }

    private void ShowGagPlayerMenu(CCSPlayerController admin)
    {
        var menu = new CenterHtmlMenu(Localizer["Menu.GagPlayer"], this);

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
                    admin.PrintToChat(Localizer["NoPermission"]);
                }
            });
        }

        if (!menu.MenuOptions.Any())
            menu.AddMenuOption(Localizer["Menu.NoPlayers"], (ply, opt) => { });

        menu.ExitButton = true;
        menu.Open(admin);
    }

    private void ShowSilencePlayerMenu(CCSPlayerController admin)
    {
        var menu = new CenterHtmlMenu(Localizer["Menu.SilencePlayer"], this);

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
                    admin.PrintToChat(Localizer["NoPermission"]);
                }
            });
        }

        if (!menu.MenuOptions.Any())
            menu.AddMenuOption(Localizer["Menu.NoPlayers"], (ply, opt) => { });

        menu.ExitButton = true;
        menu.Open(admin);
    }

    private void ShowCommDurationMenu(CCSPlayerController admin, CCSPlayerController target, string type)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/chat"))
        {
            admin.PrintToChat(Localizer["NoPermission"]);
            return;
        }

        var menu = new CenterHtmlMenu(Localizer["Menu.ChooseDuration"], this);

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
        menu.Open(admin);
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
                Server.PrintToChatAll(Localizer["PermaSILENCE", executorName, targetName]);
            else
                Server.PrintToChatAll(Localizer["SILENCE", executorName, targetName, duration]);

            LogAction($"{executorName} silenced {targetName} ({target.SteamID}) for {duration} minutes.");
        }
        else
        {
            ApplyPunishment(target, type, duration, "", admin);

            if (duration == 0)
                Server.PrintToChatAll(Localizer[$"Perma{type}", executorName, targetName]);
            else
                Server.PrintToChatAll(Localizer[$"{type}", executorName, targetName, duration]);

            LogAction($"{executorName} {type.ToLower()}ed {targetName} ({target.SteamID}) for {duration} minutes.");
        }
    }


    private void ShowServerCommands(CCSPlayerController admin)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/generic"))
        {
            admin.PrintToChat(Localizer["NoPermission"]);
            return;
        }

        var menu = new CenterHtmlMenu(Localizer["Menu.ServerCommands"], this);

        menu.AddMenuOption(Localizer["Menu.Option.ChangeMap"], (ply, opt) => ShowMapSelectionMenu(admin));
        menu.AddMenuOption(Localizer["Menu.Fun.Cat.Cleanup"], (ply, opt) => ShowFunCleanupMenu(admin));
        menu.AddMenuOption(Localizer["Menu.Option.RoundRestart"], (ply, opt) =>
        {
            if (AdminManager.PlayerHasPermissions(admin, "@css/generic"))
            {
                Server.ExecuteCommand("mp_restartgame 1");
                Server.PrintToChatAll(Localizer["Round.Restarted", admin.PlayerName]);
            }
            else admin.PrintToChat(Localizer["NoPermission"]);
        });

        menu.ExitButton = true;
        menu.Open(admin);
    }

    private void ShowMapSelectionMenu(CCSPlayerController admin)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/generic"))
        {
            admin.PrintToChat(Localizer["NoPermission"]);
            return;
        }

        var menu = new CenterHtmlMenu(Localizer["Menu.ChangeMap"], this);

        foreach (var map in PredefinedMaps)
            menu.AddMenuOption(map, (ply, opt) => ShowConfirmChangeMapMenu(admin, map));

        menu.ExitButton = true;
        menu.Open(admin);
    }

    private void ShowConfirmChangeMapMenu(CCSPlayerController admin, string map)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/generic"))
        {
            admin.PrintToChat(Localizer["NoPermission"]);
            return;
        }

        var menu = new CenterHtmlMenu(Localizer["Menu.ChangeMapConfirm"], this);

        menu.AddMenuOption(Localizer["Menu.ConfirmYes"], (ply, opt) =>
        {
            if (AdminManager.PlayerHasPermissions(admin, "@css/generic"))
            {
                var currentMap = Server.MapName;
                if (currentMap == map)
                {
                    admin.PrintToChat($"{{green}}[AdminPlus]{{default}} You are already on {{yellow}}{map}{{default}} map!");
                    return;
                }

                Server.PrintToChatAll(Localizer["Map.Changed", admin.PlayerName, map]);
                AddTimer(2.0f, () =>
                {
                    try { Server.ExecuteCommand($"changelevel {map}"); }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[AdminPlus] Map change error: {ex.Message}");
                        admin.PrintToChat(Localizer["Map.NotFound", map]);
                    }
                });
            }
            else admin.PrintToChat(Localizer["NoPermission"]);
        });

        menu.AddMenuOption(Localizer["Menu.ConfirmNo"], (ply, opt) => ShowServerCommands(admin));
        menu.ExitButton = true;
        menu.Open(admin);
    }

    private void ShowPlayerList(CCSPlayerController admin)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/ban"))
        {
            admin.PrintToChat(Localizer["NoPermission"]);
            return;
        }

        var menu = new CenterHtmlMenu(Localizer["Menu.ChoosePlayer"], this);

        foreach (var p in Utilities.GetPlayers()!)
        {
            if (p == null || !p.IsValid || p.IsBot) continue;
            menu.AddMenuOption(SanitizeName(p.PlayerName), (ply, opt) => ShowBanTypeMenu(admin, p));
        }

        if (!menu.MenuOptions.Any())
            menu.AddMenuOption(Localizer["Menu.NoPlayers"], (ply, opt) => { });

        menu.ExitButton = true;
        menu.Open(admin);
    }

    private void ShowBanTypeMenu(CCSPlayerController admin, CCSPlayerController target)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/ban"))
        {
            admin.PrintToChat(Localizer["NoPermission"]);
            return;
        }

        var menu = new CenterHtmlMenu(Localizer["Menu.ChooseBanType"], this);
        menu.AddMenuOption(Localizer["Menu.Option.SteamIdBan"], (ply, opt) => ShowDurationMenu(admin, target));
        menu.AddMenuOption(Localizer["Menu.Option.IpBan"], (ply, opt) => ShowReasonMenu(admin, target, 0, true));
        menu.ExitButton = true;
        menu.Open(admin);
    }

    private void ShowDurationMenu(CCSPlayerController admin, CCSPlayerController target)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/ban"))
        {
            admin.PrintToChat(Localizer["NoPermission"]);
            return;
        }

        var menu = new CenterHtmlMenu(Localizer["Menu.ChooseDuration"], this);

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
        menu.Open(admin);
    }

    private void ShowReasonMenu(CCSPlayerController admin, CCSPlayerController target, int minutes, bool isIpBan)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/ban"))
        {
            admin.PrintToChat(Localizer["NoPermission"]);
            return;
        }

        var menu = new CenterHtmlMenu(Localizer["Menu.ChooseReason"], this);

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
                    admin.PrintToChat(Localizer["NoPermission"]);
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
                    Server.PrintToChatAll(Localizer["IpBan.AddedNick", admin.PlayerName, safeName, reason]);
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
                        Server.PrintToChatAll(Localizer["PermabannedReason", admin.PlayerName, safeName, reason]);
                    else
                        Server.PrintToChatAll(Localizer["BannedReason", admin.PlayerName, safeName, minutes, reason]);

                    LogAction($"{admin.PlayerName} banned {safeName} ({steamId}) [IP:{ip}] for {minutes} minutes. Reason: {reason}");
                }
            });
        }

        menu.ExitButton = true;
        menu.Open(admin);
    }

    private void BanListMenu(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null || !caller.IsValid) return;

        if (!AdminManager.PlayerHasPermissions(caller, "@css/ban"))
        {
            caller.PrintToChat(Localizer["NoPermission"]);
            return;
        }

        var root = new CenterHtmlMenu(Localizer["Menu.BanList"], this);
        root.AddMenuOption(Localizer["Menu.BanList.Steam"], (ply, opt) => ShowSteamBanListMenu(caller));
        root.AddMenuOption(Localizer["Menu.BanList.IP"], (ply, opt) => ShowIpBanListMenu(caller));
        root.ExitButton = true;
        root.Open(caller);
    }

    private void ShowSteamBanListMenu(CCSPlayerController caller)
    {
        if (!caller.IsValid) return;
        var menu = new CenterHtmlMenu(Localizer["Menu.BanList.Steam"], this);

        if (SteamBans.Count == 0)
        {
            menu.AddMenuOption(Localizer["BanList.Steam.Empty"], (ply, opt) => { });
        }
        else
        {
            foreach (var kv in SteamBans)
                menu.AddMenuOption($"{SanitizeName(kv.Value.nick)} [{kv.Key}]", (ply, opt) => { });
        }

        menu.ExitButton = true;
        menu.Open(caller);
    }

    private void ShowIpBanListMenu(CCSPlayerController caller)
    {
        if (!caller.IsValid) return;
        var menu = new CenterHtmlMenu(Localizer["Menu.BanList.IP"], this);

        if (IpBans.Count == 0)
        {
            menu.AddMenuOption(Localizer["BanList.IP.Empty"], (ply, opt) => { });
        }
        else
        {
            foreach (var kv in IpBans)
                menu.AddMenuOption($"{SanitizeName(kv.Value.nick)} (IP) [{kv.Key}]", (ply, opt) => { });
        }

        menu.ExitButton = true;
        menu.Open(caller);
    }

    private void ShowMoneyMenu(CCSPlayerController admin)
    {
        if (!AdminManager.PlayerHasPermissions(admin, "@css/slay"))
        {
            admin.PrintToChat(Localizer["NoPermission"]);
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
            admin.PrintToChat(Localizer["NoPermission"]);
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
