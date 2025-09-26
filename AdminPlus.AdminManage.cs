using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using System;
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

    private static bool ReadAdminsFile(out JsonObject root)
    {
        root = new JsonObject();
        try
        {
            if (!File.Exists(AdminsFile)) 
            {
                Console.WriteLine($"[AdminPlus] ERROR: Admin file not found: {AdminsFile}");
                return false;
            }
            Console.WriteLine($"[AdminPlus] Loading admin data from: {AdminsFile}");
            var text = File.ReadAllText(AdminsFile, Encoding.UTF8);
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
            Console.WriteLine($"[AdminPlus] ERROR: Failed to load admin data from {AdminsFile}");
            Console.WriteLine($"[AdminPlus] ERROR: JSON parse error: {ex.Message}");
            Console.WriteLine($"[AdminPlus] ERROR: Stack trace: {ex.StackTrace}");
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
            Console.WriteLine($"[AdminPlus] ERROR: Failed to save admin data to {AdminsFile}");
            Console.WriteLine($"[AdminPlus] ERROR: {ex.Message}");
            Console.WriteLine($"[AdminPlus] ERROR: Stack trace: {ex.StackTrace}");
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
        var group = info.GetArg(2);
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

        if (root.ContainsKey(key))
        {
            if (caller != null && caller.IsValid)
                caller.Print(Localizer["Admin.Exists", $"{playerName} {steamId3}"]);
            else
                Console.WriteLine($"[AdminPlus] This admin already exists: {playerName} {steamId3}");
            return;
        }

        var obj = new JsonObject
        {
            ["identity"] = key,
            ["name"] = playerName,
            ["immunity"] = immunity,
            ["groups"] = new JsonArray(group.StartsWith("#") ? group : "#" + group)
        };

        root[key] = obj;
        WriteAdminsFile(root);
        LoadImmunity();

        string executorName = GetExecutorNameAdmin(caller);
        
        if (caller != null && caller.IsValid)
            caller.Print(Localizer["Admin.Added", playerName, steamId3, group, immunity]);
        else
            Console.WriteLine(Localizer["Admin.Add.Console", playerName, steamId3, group, immunity]);
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

    public void RegisterAdminManageCommands()
    {
        AddCommand("addadmin", Localizer["Admin.Add.Usage"], CmdAddAdmin);
        AddCommand("removeadmin", Localizer["Admin.Remove.Usage"], CmdRemoveAdmin);
        AddCommand("adminlist", Localizer["Admin.List.Header"], CmdAdminList);

        AddCommand("css_addadmin", "Add admin from console", CmdAddAdmin);
        AddCommand("css_removeadmin", "Remove admin from console", CmdRemoveAdmin);
        AddCommand("css_adminlist", "List admins from console", CmdAdminList);
    }
}