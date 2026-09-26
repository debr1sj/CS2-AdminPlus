using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AdminPlus;

public partial class AdminPlus
{
    private static string AdminsFile =>
        Path.Combine(Server.GameDirectory, "csgo", "addons", "counterstrikesharp", "configs", "admins.json");
    private static string AdminGroupsFile =>
        Path.Combine(Server.GameDirectory, "csgo", "addons", "counterstrikesharp", "configs", "admin_groups.json");

    internal static void EnsureAdminConfigFiles()
    {
        try
        {
            var configDir = Path.GetDirectoryName(AdminsFile)!;
            Directory.CreateDirectory(configDir);

            if (!File.Exists(AdminsFile))
            {
                File.WriteAllText(AdminsFile, "{}" + Environment.NewLine, Encoding.UTF8);
                Console.WriteLine($"[AdminPlus] Created missing admin config: {AdminsFile}");
            }

            if (!File.Exists(AdminGroupsFile))
            {
                var defaultGroups = new JsonObject
                {
                    ["#css/admin"] = new JsonObject
                    {
                        ["flags"] = new JsonArray(
                            "@css/reservation",
                            "@css/generic",
                            "@css/kick",
                            "@css/ban",
                            "@css/unban",
                            "@css/vip",
                            "@css/slay",
                            "@css/changemap",
                            "@css/cvar",
                            "@css/config",
                            "@css/chat",
                            "@css/vote",
                            "@css/password",
                            "@css/rcon",
                            "@css/cheats",
                            "@css/root"
                        ),
                        ["immunity"] = 100
                    }
                };

                var json = defaultGroups.ToJsonString(new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });

                File.WriteAllText(AdminGroupsFile, json + Environment.NewLine, Encoding.UTF8);
                Console.WriteLine($"[AdminPlus] Created missing admin groups config: {AdminGroupsFile}");
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to bootstrap admin config files: {ex.Message}");
        }
    }

    private static bool ReadAdminsFile(out JsonObject root)
    {
        root = new JsonObject();
        try
        {
            if (!File.Exists(AdminsFile)) 
            {
                LogError($"Admin file not found: {AdminsFile}");
                return false;
            }
            Console.WriteLine($"[AdminPlus] Loading admin data from: {AdminsFile}");
            var text = File.ReadAllText(AdminsFile, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(text))
            {
                root = new JsonObject();
                return true;
            }
            var node = JsonNode.Parse(text) as JsonObject;
            if (node != null) 
            { 
                root = node; 
                return true; 
            }
            return false;
        }
        catch (Exception ex)
        {
            LogError($"Failed to load admin data from {AdminsFile}");
            LogError($"JSON parse error: {ex.Message}");
            return false;
        }
    }

    private static void WriteAdminsFile(JsonObject root)
    {
        var dir = Path.GetDirectoryName(AdminsFile)!;
        Directory.CreateDirectory(dir);

        var tmp = AdminsFile + ".tmp";
        var json = root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        Console.WriteLine($"[AdminPlus] Saving admin data to: {AdminsFile}");
        File.WriteAllText(tmp, json, Encoding.UTF8);
        try
        {
            if (File.Exists(AdminsFile)) File.Replace(tmp, AdminsFile, null);
            else File.Move(tmp, AdminsFile);
        }
        catch (Exception ex)
        {
            LogError($"Failed to save admin data to {AdminsFile}");
            LogError($"{ex.Message}");
            if (File.Exists(AdminsFile)) File.Delete(AdminsFile);
            File.Move(tmp, AdminsFile);
        }
    }

    private static bool TryParseSteam64(string input, out ulong steam64)
    {
        steam64 = 0;
        return ulong.TryParse(input, out steam64) && steam64 > 0;
    }

    private static string ConvertToSteamID3(ulong steam64)
    {
        try
        {
            var steamId3 = (steam64 - 76561197960265728) & 0xFFFFFFFF;
            return $"U:1:{steamId3}";
        }
        catch
        {
            return steam64.ToString();
        }
    }

    private static string NormalizeAdminGroup(string input)
    {
        var group = (input ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(group))
            return "#css/admin";

        if (group.StartsWith("@"))
            group = group[1..];
        if (group.StartsWith("#"))
            group = group[1..];

        if (!group.StartsWith("css/", StringComparison.OrdinalIgnoreCase))
            group = "css/" + group;

        return "#" + group;
    }

    private static string NormalizeAdminFlag(string input)
    {
        var flag = (input ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(flag))
            return "@css/generic";

        if (flag.StartsWith("#"))
            flag = flag[1..];
        if (flag.StartsWith("@"))
            flag = flag[1..];

        if (!flag.StartsWith("css/", StringComparison.OrdinalIgnoreCase))
            flag = "css/" + flag;

        return "@" + flag;
    }

    private static string? ResolveGroupForFlag(string flag)
    {
        try
        {
            if (!File.Exists(AdminGroupsFile))
                return null;

            var text = File.ReadAllText(AdminGroupsFile, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var root = JsonNode.Parse(text) as JsonObject;
            if (root == null)
                return null;

            foreach (var kv in root)
            {
                if (kv.Value is not JsonObject groupObj)
                    continue;

                if (groupObj["flags"] is not JsonArray flags)
                    continue;

                foreach (var f in flags)
                {
                    var existing = f?.GetValue<string>()?.Trim();
                    if (string.Equals(existing, flag, StringComparison.OrdinalIgnoreCase))
                        return kv.Key;
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to resolve group for flag {flag}: {ex.Message}");
        }

        return null;
    }

    private static IReadOnlyList<string> ReadDefinedAdminGroups()
    {
        try
        {
            if (!File.Exists(AdminGroupsFile))
                return Array.Empty<string>();

            var node = JsonNode.Parse(File.ReadAllText(AdminGroupsFile, Encoding.UTF8)) as JsonObject;
            if (node == null)
                return Array.Empty<string>();

            return node
                .Where(kv => kv.Value is JsonObject && kv.Key.StartsWith("#", StringComparison.Ordinal))
                .Select(kv => kv.Key)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex)
        {
            LogError($"Failed to read admin groups: {ex.Message}");
            return Array.Empty<string>();
        }
    }

    private static bool IsDefinedAdminGroup(string group)
        => ReadDefinedAdminGroups().Contains(group, StringComparer.OrdinalIgnoreCase);

    private static void ApplyRuntimeAdminData(CCSPlayerController? target, string assignment, int immunity)
    {
        if (target == null || !target.IsValid)
            return;

        try
        {
            AdminManager.RemovePlayerAdminData(target);

            if (assignment.StartsWith("@", StringComparison.Ordinal))
                AdminManager.AddPlayerPermissions(target, assignment);
            else
                AdminManager.AddPlayerToGroup(target, assignment);

            AdminManager.SetPlayerImmunity(target, (uint)Math.Max(0, immunity));
        }
        catch (Exception ex)
        {
            LogError($"Failed to apply runtime permissions for {target.SteamID}: {ex.Message}");
        }
    }

    private static string NormalizeAdminAssignment(string input, out bool isFlag)
    {
        isFlag = input.TrimStart().StartsWith("@", StringComparison.Ordinal);
        return isFlag ? NormalizeAdminFlag(input) : NormalizeAdminGroup(input);
    }

    private string GetExecutorNameAdmin(CCSPlayerController? caller)
    {
        return (caller == null || !caller.IsValid) ? Localizer["Console"] : caller.PlayerName;
    }

    private void SendUsageMessageAdmin(CCSPlayerController? caller, string localeKey, string consoleMessage)
    {
        if (caller != null && caller.IsValid)
            caller.Print(Localizer[localeKey]);
        else
            Console.WriteLine(consoleMessage);
    }

    private void SendErrorMessageAdmin(CCSPlayerController? caller, string localeKey, string consoleMessage, params object[] args)
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

    private void CmdAddAdmin(CCSPlayerController? caller, CommandInfo info)
    {
        bool isConsoleCommand = caller == null;
        
        if (isConsoleCommand)
        {
            Console.WriteLine("[AdminPlus] Console admin add command executed.");
        }
        else
        {
            if (caller == null || !caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/root"))
            {
                caller?.Print(Localizer["NoPermission"]);
                return;
            }
        }

        if (info.ArgCount < 4)
        {
            SendUsageMessageAdmin(caller, "Admin.Add.Usage", "Usage: css_addadmin <steamid64> <group> <immunity>");
            return;
        }

        var idRaw = info.GetArg(1);
        var groupArg = info.GetArg(2);
        var assignment = NormalizeAdminAssignment(groupArg, out var isFlagInput);
        if (!isFlagInput && !IsDefinedAdminGroup(assignment))
        {
            SendErrorMessageAdmin(caller, "Admin.GroupNotFound", $"Admin group not found: {assignment}", assignment);
            return;
        }
        if (!int.TryParse(info.GetArg(3), out var immunity)) immunity = 0;

        if (!TryParseSteam64(idRaw, out var s64) || s64 == 0)
        {
            if (caller != null && caller.IsValid)
                caller.Print(Localizer["InvalidSteamId"]);
            else
                Console.WriteLine("[AdminPlus] Invalid SteamID!");
            return;
        }

        var key = s64.ToString();
        if (!ReadAdminsFile(out var root)) root = new JsonObject();

        string playerName = "Unknown";
        var target = Utilities.GetPlayers().FirstOrDefault(p => p != null && p.IsValid && p.SteamID == s64);
        if (target != null) playerName = SanitizeName(target.PlayerName);

        var steamId3 = ConvertToSteamID3(s64);

        var updated = root[key] is JsonObject;
        var obj = root[key] as JsonObject ?? new JsonObject();
        obj["identity"] = key;
        if (target != null)
            obj["name"] = playerName;
        else if (obj["name"] == null)
            obj["name"] = playerName;
        obj["immunity"] = immunity;
        obj.Remove("groups");
        obj.Remove("flags");
        if (isFlagInput)
            obj["flags"] = new JsonArray(assignment);
        else
            obj["groups"] = new JsonArray(assignment);

        root[key] = obj;
        WriteAdminsFile(root);
        LoadImmunity();
        ApplyRuntimeAdminData(target, assignment, immunity);

        string executorName = GetExecutorNameAdmin(caller);
        var displayAssignment = assignment;
        if (caller != null && caller.IsValid)
            caller.Print(Localizer[updated ? "Admin.Updated" : "Admin.Added", playerName, steamId3, displayAssignment, immunity]);
        else
            Console.WriteLine(Localizer[updated ? "Admin.Update.Console" : "Admin.Add.Console", playerName, steamId3, displayAssignment, immunity]);
    }

    private void CmdRemoveAdmin(CCSPlayerController? caller, CommandInfo info)
    {
        bool isConsoleCommand = caller == null;
        
        if (isConsoleCommand)
        {
            Console.WriteLine("[AdminPlus] Console admin remove command executed.");
        }
        else
        {
            if (caller == null || !caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/root"))
            {
                caller?.Print(Localizer["NoPermission"]);
                return;
            }
        }

        if (info.ArgCount < 2)
        {
            SendUsageMessageAdmin(caller, "Admin.Remove.Usage", "Usage: css_removeadmin <steamid64>");
            return;
        }

        var idArg = info.GetArg(1);
        if (!TryParseSteam64(idArg, out var s64) || s64 == 0)
        {
            if (caller != null && caller.IsValid)
                caller.Print(Localizer["InvalidSteamId"]);
            else
                Console.WriteLine("[AdminPlus] Invalid SteamID!");
            return;
        }

        var key = s64.ToString();
        if (!ReadAdminsFile(out var root) || !root.ContainsKey(key))
        {
            if (caller != null && caller.IsValid)
                caller.Print(Localizer["Admin.NotFound", key]);
            else
                Console.WriteLine($"[AdminPlus] Admin not found: {key}");
            return;
        }

        string name = root[key]?["name"]?.GetValue<string>() ?? key;
        var steamId3 = ConvertToSteamID3(s64);

        root.Remove(key);
        WriteAdminsFile(root);
        LoadImmunity();

        string executorName = GetExecutorNameAdmin(caller);
        if (caller != null && caller.IsValid)
            caller.Print(Localizer["Admin.Removed", $"{name} {steamId3}"]);
        else
            Console.WriteLine(Localizer["Admin.Remove.Console", $"{name} {steamId3}"]);
    }

    private void CmdAdminList(CCSPlayerController? caller, CommandInfo info)
    {
        bool isConsoleCommand = caller == null;
        
        if (isConsoleCommand)
        {
            Console.WriteLine("[AdminPlus] Console admin list command executed.");
        }
        else
        {
            if (caller == null || !caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/root"))
            {
                caller?.Print(Localizer["NoPermission"]);
                return;
            }
        }

        if (!ReadAdminsFile(out var root) || root.Count == 0)
        {
            SendErrorMessageAdmin(caller, "Admin.List.Empty", "Admin list is empty.");
            return;
        }

        if (caller == null)
        {
            Console.WriteLine("--------- ADMIN LIST ---------");
        }
        else
        {
            caller.PrintToConsole("--------- ADMIN LIST ---------");
        }

        foreach (var kv in root)
        {
            if (kv.Value is JsonObject obj)
            {
                var imm = obj["immunity"]?.GetValue<int?>() ?? 0;
                var groups = obj["groups"] is JsonArray ga && ga.Count > 0
                    ? string.Join(",", ga.Select(x => x?.GetValue<string>()))
                    : "-";
                var name = obj["name"]?.GetValue<string>() ?? kv.Key;
                
                if (ulong.TryParse(kv.Key, out var steam64))
                {
                    var steamId3 = ConvertToSteamID3(steam64);
                    
                    if (caller == null)
                    {
                        Console.WriteLine($"• {name} {steamId3} (Immunity: {imm}, Groups: {groups})");
                    }
                    else
                    {
                        var line = Localizer["Admin.List.Row", $"{name} {steamId3}", imm, groups];
                        caller.PrintToConsole(line);
                    }
                }
                else
                {
                    if (caller == null)
                    {
                        Console.WriteLine($"• {name} (SteamID: {kv.Key}, Immunity: {imm}, Groups: {groups})");
                    }
                    else
                    {
                        var line = Localizer["Admin.List.Row", name, imm, groups];
                        caller.PrintToConsole(line);
                    }
                }
            }
        }

        if (caller == null)
        {
            Console.WriteLine("--------- END ADMIN LIST ---------");
        }
        else
        {
            caller.PrintToConsole("--------- END ADMIN LIST ---------");
            caller.Print(Localizer["Admin.List.Printed"]);
        }
    }

    private void CmdAdminReload(CCSPlayerController? caller, CommandInfo info)
    {
        bool isConsoleCommand = caller == null;
        if (!isConsoleCommand && (caller == null || !caller.IsValid || !HasEffectivePermission(caller, "@css/root")))
        {
            caller?.Print(Localizer["NoPermission"]);
            return;
        }

        EnsureAdminConfigFiles();
        LoadImmunity();

        if (caller != null && caller.IsValid)
            caller.Print(Localizer["Admin.Reload.Success"]);
        else
            Console.WriteLine(Localizer["Admin.Reload.Console"]);
    }

    public void RegisterAdminManageCommands()
    {
        AddCommand("addadmin", Localizer["Admin.Add.Usage"], CmdAddAdmin);
        AddCommand("removeadmin", Localizer["Admin.Remove.Usage"], CmdRemoveAdmin);
        AddCommand("adminlist", Localizer["Admin.List.Header"], CmdAdminList);
        AddCommand("adminreload", Localizer["Admin.Reload.Usage"], CmdAdminReload);

        AddCommand("css_addadmin", "Add admin from console", CmdAddAdmin);
        AddCommand("css_removeadmin", "Remove admin from console", CmdRemoveAdmin);
        AddCommand("css_adminlist", "List admins from console", CmdAdminList);
        AddCommand("css_adminreload", "Reload admin cache from config files", CmdAdminReload);
        AddCommand("css_admin_reload", "Reload admin cache from config files", CmdAdminReload);
    }
}
