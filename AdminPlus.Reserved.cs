using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.ValveConstants.Protobuf;
using static CounterStrikeSharp.API.Core.Listeners;
using static CounterStrikeSharp.API.Modules.Admin.AdminManager;

namespace AdminPlus;

public partial class AdminPlus
{
    private const string PlayerDesignerName = "cs_player_controller";
    private const int MaxAdminReservations = 3;
    
    private bool _reservationEnabled = true;
    private bool _debugMode = false;
    
    private readonly ConcurrentDictionary<CCSPlayerController, double> _playerJoinTime = new();
    private readonly ConcurrentBag<CCSPlayerController> _reservedAdmins = new();
    private readonly object _reservationLock = new object();
    
    private ConVar? _svVisibleMaxPlayers;
    
    private enum ReservationType
    {
        Normal = 0,
        AdminPriority = 1,
        AdminLimit = 2
    }
    
    private enum KickType
    {
        HighestPing = 0,
        LongestTime = 1,
        Random = 2
    }
    
    public void InitializeReservationSystem()
    {
        try
        {
            _svVisibleMaxPlayers = ConVar.Find("sv_visiblemaxplayers");
            
            RegisterListener<OnMapStart>(OnMapStartReservation);
        }
        catch (Exception)
        {
        }
    }
    
    public void OnMapStartReservation(string mapName)
    {
        try
        {
            UpdateReservationSettings();
        }
        catch (Exception)
        {
        }
    }
    
    [GameEventHandler]
    public HookResult OnPlayerConnectReservation(EventPlayerConnectFull @event, GameEventInfo info)
    {
        try
        {
            var player = @event.Userid;
            if (player == null || !player.IsValid) return HookResult.Continue;
            
            _playerJoinTime.TryAdd(player, Server.CurrentTime);
            
            if (_reservationEnabled)
            {
                return HandleReservationLogic(player);
            }
            
            return HookResult.Continue;
        }
        catch (Exception)
        {
            return HookResult.Continue;
        }
    }
    
    [GameEventHandler]
    public HookResult OnPlayerDisconnectReservation(EventPlayerDisconnect @event, GameEventInfo info)
    {
        try
        {
            var player = @event.Userid;
            if (player == null) return HookResult.Continue;
            
            _playerJoinTime.TryRemove(player, out _);
            
            lock (_reservationLock)
            {
                var adminsToRemove = _reservedAdmins.Where(p => p == player).ToList();
                foreach (var admin in adminsToRemove)
                {
                    _reservedAdmins.TryTake(out _);
                }
            }
            
            UpdateReservationSettings();
            
            return HookResult.Continue;
        }
        catch (Exception)
        {
            return HookResult.Continue;
        }
    }
    
    private HookResult HandleReservationLogic(CCSPlayerController player)
    {
        try
        {
            int currentPlayers = GetCurrentPlayerCount();
            int maxPlayers = Server.MaxPlayers;
            
            if (player.IsBot) return HookResult.Continue;
            
            bool isAdmin = AdminManager.PlayerHasPermissions(player, "@css/admin") || 
                          AdminManager.PlayerHasPermissions(player, "@css/reservation");
            
            if (!isAdmin)
            {
                if (currentPlayers < maxPlayers)
                {
                    return HookResult.Continue;
                }
                else
                {
                    player.PrintToChat(Localizer["Reservation.PlayerKicked"]);
                    AddTimer(0.1f, () => KickPlayerForReservation(player));
                    return HookResult.Continue;
                }
            }
            
            if (isAdmin)
            {
                lock (_reservationLock)
                {
                    if (_reservedAdmins.Count >= MaxAdminReservations)
                    {
                        player.PrintToChat(Localizer["Reservation.AdminLimitReached", MaxAdminReservations]);
                        AddTimer(0.1f, () => KickPlayerForReservation(player));
                        return HookResult.Continue;
                    }
                    
                    if (currentPlayers < maxPlayers)
                    {
                        _reservedAdmins.Add(player);
                        return HookResult.Continue;
                    }
                    else
                    {
                        var targetToKick = SelectPlayerToKick();
                        if (targetToKick != null)
                        {
                            _reservedAdmins.Add(player);
                            player.PrintToChat(Localizer["Reservation.AdminKicking", targetToKick.PlayerName]);
                            
                            AddTimer(0.1f, () => KickPlayerForReservation(targetToKick));
                            return HookResult.Continue;
                        }
                        else
                        {
                            player.PrintToChat(Localizer["Reservation.AdminCannotEnter"]);
                            AddTimer(0.1f, () => KickPlayerForReservation(player));
                            return HookResult.Continue;
                        }
                    }
                }
            }
            
            return HookResult.Continue;
        }
        catch (Exception)
        {
            return HookResult.Continue;
        }
    }
    
    private CCSPlayerController? SelectPlayerToKick()
    {
        try
        {
            var allPlayers = Utilities.GetPlayers();
            if (allPlayers == null || allPlayers.Count == 0) return null;
            
            var candidates = new List<CCSPlayerController>();
            
            foreach (var player in allPlayers)
            {
                if (!IsPlayerValid(player))
                {
                    continue;
                }
                
                bool isAdminPlayer = false;
                lock (_reservationLock)
                {
                    isAdminPlayer = _reservedAdmins.Contains(player);
                }
                
                if (isAdminPlayer || 
                    AdminManager.PlayerHasPermissions(player, "@css/admin") || 
                    AdminManager.PlayerHasPermissions(player, "@css/reservation"))
                {
                    continue;
                }
                
                candidates.Add(player);
            }
            
            if (candidates.Count == 0) return null;
            
            return SelectPlayerByCriteria(candidates);
        }
        catch (Exception)
        {
            return null;
        }
    }
    
    private bool IsPlayerValid(CCSPlayerController? player)
    {
        try
        {
            if (player == null) return false;
            
            if (!player.IsValid) return false;
            
            if (player.IsBot) return false;
            
            if (player.DesignerName != PlayerDesignerName) return false;
            
            if (player.Connected != PlayerConnectedState.PlayerConnected) return false;
            
            if (string.IsNullOrEmpty(player.PlayerName)) return false;
            
            if (player.UserId == null) return false;
            
            if (player.UserId < 1 || player.UserId > 65535) return false;
            if (player.PlayerName.Length > 32) return false;
            if (player.PlayerName.Contains("\\") || player.PlayerName.Contains("\"")) return false;
            
            if (player.Ping > 2000) return false;
            
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
    
    private CCSPlayerController SelectPlayerByCriteria(List<CCSPlayerController> candidates)
    {
        try
        {
            var validCandidates = candidates.Where(p => IsPlayerValid(p)).ToList();
            if (validCandidates.Count == 0) return candidates[0];
            
            var pingCandidates = validCandidates
                .Where(p => p.Ping > 0 && p.Ping < 1000)
                .ToList();
            
            if (pingCandidates.Count > 0)
            {
                var highestPingPlayer = pingCandidates
                    .OrderByDescending(p => p.Ping)
                    .FirstOrDefault();
                
                if (highestPingPlayer != null)
                {
                    return highestPingPlayer;
                }
            }
            
            var timeCandidates = validCandidates
                .Where(p => _playerJoinTime.ContainsKey(p))
                .OrderBy(p => _playerJoinTime[p])
                .ToList();
            
            if (timeCandidates.Count > 0)
            {
                return timeCandidates.First();
            }
            
            var userIdCandidates = validCandidates
                .Where(p => p.UserId != null)
                .OrderByDescending(p => p.UserId)
                .ToList();
            
            if (userIdCandidates.Count > 0)
            {
                return userIdCandidates.First();
            }
            
            return validCandidates[Random.Shared.Next(validCandidates.Count)];
        }
        catch (Exception)
        {
            return candidates.FirstOrDefault() ?? candidates[0];
        }
    }
    
    private void KickPlayerForReservation(CCSPlayerController player)
    {
        try
        {
            if (player?.IsValid == true)
            {
                player.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_REJECT_RESERVED_FOR_LOBBY);
            }
        }
        catch (Exception)
        {
        }
    }
    
    private void UpdateVisibleSlots()
    {
        try
        {
        }
        catch (Exception)
        {
        }
    }
    
    private void UpdateReservationSettings()
    {
        try
        {
            UpdateVisibleSlots();
        }
        catch (Exception)
        {
        }
    }
    
    private int GetCurrentPlayerCount()
    {
        try
        {
            var allPlayers = Utilities.GetPlayers();
            if (allPlayers == null) return 0;
            
            int count = 0;
            foreach (var player in allPlayers)
            {
                if (IsPlayerValid(player))
                {
                    count++;
                }
            }
            
            return count;
        }
        catch (Exception)
        {
            return 0;
        }
    }
    
    public void CleanupReservationSystem()
    {
        try
        {
            RemoveListener<OnMapStart>(OnMapStartReservation);
            
            _playerJoinTime.Clear();
            
            lock (_reservationLock)
            {
                while (_reservedAdmins.TryTake(out _)) { }
            }
            
            if (_svVisibleMaxPlayers != null)
            {
                _svVisibleMaxPlayers.SetValue(-1);
            }
        }
        catch (Exception)
        {
        }
    }
    
    public void PeriodicCleanup()
    {
        try
        {
            var currentTime = Server.CurrentTime;
            var expiredPlayers = _playerJoinTime
                .Where(kvp => currentTime - kvp.Value > 3600)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var player in expiredPlayers)
            {
                _playerJoinTime.TryRemove(player, out _);
            }
            
            lock (_reservationLock)
            {
                var invalidAdmins = _reservedAdmins
                    .Where(admin => !IsPlayerValid(admin))
                    .ToList();
                
                foreach (var admin in invalidAdmins)
                {
                    _reservedAdmins.TryTake(out _);
                }
            }
        }
        catch (Exception)
        {
        }
    }
    
    private void LogDebug(string message, params object[] args)
    {
        if (_debugMode)
        {
            Console.WriteLine($"[AdminPlus.Reservation] {string.Format(message, args)}");
        }
    }
    
    private void LogError(string message, Exception? ex = null)
    {
        Console.WriteLine($"[AdminPlus.Reservation.ERROR] {message}");
        if (ex != null)
        {
            Console.WriteLine($"[AdminPlus.Reservation.ERROR] Exception: {ex.Message}");
        }
    }
    
    private void LogInfo(string message, params object[] args)
    {
        Console.WriteLine($"[AdminPlus.Reservation.INFO] {string.Format(message, args)}");
    }
    
}
