using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.ValveConstants.Protobuf;
using System;
using System.Collections.Generic;
using System.Globalization;

using System.IO;
using System.Linq;

namespace AdminPlus;

public partial class AdminPlus
{

    private static IMenu? _currentVoteMenu = null;
    private static Dictionary<string, int> _voteResults = new();
    private static Dictionary<CCSPlayerController, string> _playerVotes = new();
    private static Timer? _voteTimer = null;
    private static Timer? _countdownTimer = null;
    private static string _currentVoteQuestion = "";
    private static string _voteInitiator = "";
    private static bool _isVoteInProgress = false;
    private static int _voteTimeLimit = 30;
    private static int _remainingTime = 30;
    private static VoteType _currentVoteType = VoteType.General;
    private static CCSPlayerController? _targetPlayer = null;
    private static string _voteReason = "";
    private static int _voteDuration = 0;
    private static string _voteMapName = "";
    private static Dictionary<CCSPlayerController, int> _playerMenuPositions = new();
    private static HashSet<CCSPlayerController> _playersWhoClosedMenu = new();

    public enum VoteType
    {
        General,
        Map,
        Kick,
        Ban,
        Gag,
        Mute,
        Silence
    }

    public void RegisterVoteCommands()
    {
        AddCommand("vote", Localizer["Vote.Usage"], CmdVote);
        AddCommand("votemap", Localizer["VoteMap.Usage"], CmdVoteMap);
        AddCommand("rvote", Localizer["Rvote.Usage"], CmdRevote);
        AddCommand("cancelvote", Localizer["CancelVote.Usage"], CmdCancelVote);

        AddCommand("votekick", Localizer["VoteKick.Usage"], CmdVoteKick);
        AddCommand("voteban", Localizer["VoteBan.Usage"], CmdVoteBan);
        AddCommand("votegag", Localizer["VoteGag.Usage"], CmdVoteGag);
        AddCommand("votemute", Localizer["VoteMute.Usage"], CmdVoteMute);
        AddCommand("votesilence", Localizer["VoteSilence.Usage"], CmdVoteSilence);

        AddCommand("css_vote", "Create a vote from console", CmdVote);
        AddCommand("css_votemap", "Create a map vote from console", CmdVoteMap);
        AddCommand("css_rvote", "Revote from console", CmdRevote);
        AddCommand("css_cancelvote", "Cancel vote from console", CmdCancelVote);
        AddCommand("css_votekick", "Create a kick vote from console", CmdVoteKick);
        AddCommand("css_voteban", "Create a ban vote from console", CmdVoteBan);
        AddCommand("css_votegag", "Create a gag vote from console", CmdVoteGag);
        AddCommand("css_votemute", "Create a mute vote from console", CmdVoteMute);
        AddCommand("css_votesilence", "Create a silence vote from console", CmdVoteSilence);
    }

    private void CmdVote(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null && (!caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/generic")))
        {
            if (caller.IsValid) caller.Print(Localizer["NoPermission"]);
            return;
        }

        if (_isVoteInProgress)
        {
            string message = Localizer["Vote.AlreadyInProgress"];
            if (caller != null && caller.IsValid)
                caller.Print(message);
            else
                Console.WriteLine(message);
            return;
        }

        if (info.ArgCount < 4)
        {
            string usage = Localizer["Vote.Usage"];
            if (caller != null && caller.IsValid)
                caller.Print(usage);
            else
                Console.WriteLine(usage);
            return;
        }

        string question = info.GetArg(1);
        List<string> options = new();

        for (int i = 2; i < info.ArgCount; i++)
        {
            options.Add(info.GetArg(i));
        }

        if (options.Count < 2)
        {
            string message = Localizer["Vote.NotEnoughOptions"];
            if (caller != null && caller.IsValid)
                caller.Print(message);
            else
                Console.WriteLine(message);
            return;
        }

        StartVote(question, options, caller);
    }

    private void CmdVoteMap(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null && (!caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/generic")))
        {
            if (caller.IsValid) caller.Print(Localizer["NoPermission"]);
            return;
        }

        if (_isVoteInProgress)
        {
            string message = Localizer["Vote.AlreadyInProgress"];
            if (caller != null && caller.IsValid)
                caller.Print(message);
            else
                Console.WriteLine(message);
            return;
        }

        if (info.ArgCount < 2)
        {
            string usage = Localizer["VoteMap.Usage"];
            if (caller != null && caller.IsValid)
                caller.Print(usage);
            else
                Console.WriteLine(usage);
            return;
        }

        string mapName = info.GetArg(1);
        if (MapAliases.TryGetValue(mapName, out var aliasMap))
            mapName = aliasMap;

        _voteMapName = mapName;
        string question = Localizer["VoteMap.Question", mapName];
        List<string> options = new() { Localizer["Vote.Yes"], Localizer["Vote.No"] };

        StartVote(question, options, caller, VoteType.Map);
    }

    private void CmdRevote(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null || !caller.IsValid)
        {
            Console.WriteLine(Localizer["Vote.PlayerOnly"]);
            return;
        }

        if (!_isVoteInProgress)
        {
            caller.Print(Localizer["Vote.NotInProgress"]);
            return;
        }

        if (_remainingTime <= 0)
        {
            caller.Print(Localizer["Vote.AlreadyEnded"]);
            return;
        }

        if (_currentVoteMenu != null && caller.IsValid)
        {
            CloseMenu(caller);
            _currentVoteMenu.Open(caller);
            caller.Print(Localizer["Vote.MenuReopened"]);
        }

        if (_playerVotes.ContainsKey(caller))
        {
            string previousVote = _playerVotes[caller];
            _voteResults[previousVote]--;
            _playerVotes.Remove(caller);
            caller.Print(Localizer["Vote.Revoted"]);
        }

        _playersWhoClosedMenu.Remove(caller);

        _playersWhoClosedMenu.Remove(caller);

        _playersWhoClosedMenu.Remove(caller);

        _playersWhoClosedMenu.Remove(caller);
    }

    private void CmdCancelVote(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null && (!caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/generic")))
        {
            if (caller.IsValid) caller.Print(Localizer["NoPermission"]);
            return;
        }

        if (!_isVoteInProgress)
        {
            string message = Localizer["Vote.NotInProgress"];
            if (caller != null && caller.IsValid)
                caller.Print(message);
            else
                Console.WriteLine(message);
            return;
        }

        CancelVote(caller);
    }

    private void StartVote(string question, List<string> options, CCSPlayerController? initiator, VoteType voteType = VoteType.General, CCSPlayerController? targetPlayer = null, string reason = "", int duration = 0)
    {
        _isVoteInProgress = true;
        _currentVoteQuestion = question;
        _voteInitiator = initiator?.PlayerName ?? "Konsol";
        _currentVoteType = voteType;
        _targetPlayer = targetPlayer;
        _voteReason = reason;
        _voteDuration = duration;
        _remainingTime = _voteTimeLimit;
        _voteResults.Clear();
        _playerVotes.Clear();

        foreach (string option in options)
        {
            _voteResults[option] = 0;
        }

        CreateVoteMenuWithCountdown(question, options);

        var players = Utilities.GetPlayers().Where(p => p != null && p.IsValid && !p.IsBot);
        foreach (var player in players)
        {
            if (player.IsValid)
                _currentVoteMenu?.Open(player);
        }

        string startMessage = Localizer["Vote.Started", _voteInitiator, question];
        PlayerExtensions.PrintToAll(startMessage);

        _voteTimer = AddTimer(_voteTimeLimit, () => EndVote());
        
        Console.WriteLine($"[AdminPlus] Starting vote countdown: {_remainingTime} seconds");
        _countdownTimer = AddTimer(1.0f, () => UpdateCountdown(), TimerFlags.REPEAT);
    }

    private void UpdateCountdown()
    {
        if (!_isVoteInProgress)
        {
            _countdownTimer?.Kill();
            _countdownTimer = null;
            return;
        }

        _remainingTime--;

        if (_remainingTime > 0 && _currentVoteMenu != null)
        {
            UpdateVoteMenuForNonVoters();
        }

        if (_remainingTime > 0 && (_remainingTime % 5 == 0))
        {
            string baseMessage = Localizer["Vote.CountdownWithRevote"];
            string countdownMessage = baseMessage.Replace("{0}", _remainingTime.ToString());
            PlayerExtensions.PrintToAll(countdownMessage);
        }

        if (_remainingTime <= 0)
        {
            _countdownTimer?.Kill();
            _countdownTimer = null;
            _isVoteInProgress = false;
            EndVote();
        }
    }

    private void RecreateVoteMenuWithCountdown()
    {
        if (_currentVoteMenu == null) return;

        string titleWithCountdown = Localizer["Vote.MenuTitleWithTime", _currentVoteQuestion, _remainingTime];
        
        var newMenu = CreateMenu(titleWithCountdown);
        if (newMenu == null) return;
        
        foreach (var option in _voteResults.Keys)
        {
            string menuText = Localizer["Vote.OptionText", option, _voteResults[option]];
            newMenu.AddMenuOption(menuText, (player, optionClicked) => HandleVote(player, option));
        }
        
        newMenu.ExitButton = true;
        
        var players = Utilities.GetPlayers().Where(p => p != null && p.IsValid && !p.IsBot);
        foreach (var player in players)
        {
            if (player.IsValid)
            {
                CloseMenu(player);
                newMenu.Open(player);
            }
        }
        
        _currentVoteMenu = newMenu;
    }

    private void UpdateVoteMenuWithCountdown()
    {
        if (_currentVoteMenu == null) return;

        string titleWithCountdown = Localizer["Vote.MenuTitleWithTime", _currentVoteQuestion, _remainingTime];
        
        var newMenu = CreateMenu(titleWithCountdown);
        if (newMenu == null) return;
        
        foreach (var option in _voteResults.Keys)
        {
            string menuText = Localizer["Vote.OptionText", option, _voteResults[option]];
            newMenu.AddMenuOption(menuText, (player, optionClicked) => HandleVote(player, option));
        }
        
        newMenu.ExitButton = true;
        
        var players = Utilities.GetPlayers().Where(p => p != null && p.IsValid && !p.IsBot && !_playerVotes.ContainsKey(p));
        foreach (var player in players)
        {
            if (player.IsValid)
            {
                CloseMenu(player);
                newMenu.Open(player);
            }
        }
        
        _currentVoteMenu = newMenu;
    }

    private void UpdateVoteMenuForNonVoters()
    {
        if (_currentVoteMenu == null || !_isVoteInProgress) return;

        string titleWithCountdown = Localizer["Vote.MenuTitleWithTime", _currentVoteQuestion, _remainingTime];
        
        var newMenu = CreateMenu(titleWithCountdown);
        if (newMenu == null) return;
        
        foreach (var option in _voteResults.Keys)
        {
            string menuText = Localizer["Vote.OptionText", option, _voteResults[option]];
            newMenu.AddMenuOption(menuText, (player, optionClicked) => HandleVote(player, option));
        }
        
        newMenu.ExitButton = true;
        
        var nonVoters = Utilities.GetPlayers().Where(p => p != null && p.IsValid && !p.IsBot && !_playerVotes.ContainsKey(p) && !_playersWhoClosedMenu.Contains(p));
        foreach (var player in nonVoters)
        {
            if (player.IsValid)
            {
                CloseMenu(player);
                newMenu.Open(player);
            }
        }
        
        _currentVoteMenu = newMenu;
    }

    private void CreateVoteMenuWithCountdown(string question, List<string> options)
    {
        string titleWithCountdown = Localizer["Vote.MenuTitleWithTime", question, _remainingTime];
        _currentVoteMenu = CreateMenu(titleWithCountdown);
        if (_currentVoteMenu == null) return;

        foreach (string option in options)
        {
            string menuText = Localizer["Vote.OptionText", option, _voteResults[option]];
            _currentVoteMenu.AddMenuOption(menuText, (player, optionClicked) => HandleVote(player, option));
        }

        _currentVoteMenu.ExitButton = true;
    }

    private void CreateVoteMenu(string question, List<string> options)
    {
        _currentVoteMenu = new CenterHtmlMenu(Localizer["Vote.MenuTitle", question], this);

        foreach (string option in options)
        {
            string menuText = Localizer["Vote.OptionText", option, _voteResults[option]];
            _currentVoteMenu.AddMenuOption(menuText, (player, optionClicked) => HandleVote(player, option));
        }

        _currentVoteMenu.ExitButton = true;
    }

    private void HandleVote(CCSPlayerController player, string option)
    {
        if (!_isVoteInProgress || !player.IsValid)
            return;

        if (_playerVotes.ContainsKey(player))
        {
            string previousVote = _playerVotes[player];
            _voteResults[previousVote]--;
        }

        _playerVotes[player] = option;
        _voteResults[option]++;

        player.Print(Localizer["Vote.Voted", option]);

        CloseMenu(player);

        UpdateVoteMenuForNonVoters();
    }

    private void UpdateVoteMenu()
    {
        if (_currentVoteMenu == null) return;

        _currentVoteMenu.MenuOptions.Clear();

        foreach (var option in _voteResults.Keys)
        {
            string menuText = Localizer["Vote.OptionText", option, _voteResults[option]];
            _currentVoteMenu.AddMenuOption(menuText, (player, optionClicked) => HandleVote(player, option));
        }

        var players = Utilities.GetPlayers().Where(p => p != null && p.IsValid && !p.IsBot);
        foreach (var player in players)
        {
            if (player.IsValid)
            {
                CloseMenu(player);
                _currentVoteMenu.Open(player);
            }
        }
    }

    private void UpdateVoteMenuForOthers(CCSPlayerController excludePlayer)
    {
        if (_currentVoteMenu == null) return;

        _currentVoteMenu.MenuOptions.Clear();

        foreach (var option in _voteResults.Keys)
        {
            string menuText = Localizer["Vote.OptionText", option, _voteResults[option]];
            _currentVoteMenu.AddMenuOption(menuText, (player, optionClicked) => HandleVote(player, option));
        }

        var players = Utilities.GetPlayers().Where(p => p != null && p.IsValid && !p.IsBot && p != excludePlayer);
        foreach (var player in players)
        {
            if (player.IsValid)
            {
                CloseMenu(player);
                _currentVoteMenu.Open(player);
            }
        }
    }

    private void EndVote()
    {
        if (!_isVoteInProgress) return;

        _isVoteInProgress = false;

        CloseAllVoteMenus();

        PlayerExtensions.PrintToAll(Localizer["Vote.Ended"]);

        var winner = _voteResults.OrderByDescending(x => x.Value).FirstOrDefault();
        int totalVotes = _voteResults.Values.Sum();

        ShowModernVoteResults(winner, totalVotes);

        ProcessVoteResult(winner);

        CleanupVote();
    }

    private void ShowModernVoteResults(KeyValuePair<string, int> winner, int totalVotes)
    {
        var maxVotes = _voteResults.Values.Max();
        var winners = _voteResults.Where(x => x.Value == maxVotes).ToList();
        
        if (winners.Count > 1)
        {
            string voteDetails = string.Join(" - ", winners.Select(w => $"{ChatColors.Blue}{w.Key}{ChatColors.Default} [{ChatColors.Green}{w.Value} VOTES{ChatColors.Default}]"));
            string tieMessage = string.Format(CultureInfo.InvariantCulture, Localizer["Vote.TieWithVotes"], voteDetails);
            PlayerExtensions.PrintToAll(tieMessage);
        }
        else
        {
            string winnerMessage = string.Format(CultureInfo.InvariantCulture, Localizer["Vote.WinnerWithVotes"], winner.Key, winner.Value);
            PlayerExtensions.PrintToAll(winnerMessage);
        }
    }

    private void ProcessVoteResult(KeyValuePair<string, int> winner)
    {
        switch (_currentVoteType)
        {
            case VoteType.Map:
                ProcessMapVoteResult();
                break;
            case VoteType.Kick:
            case VoteType.Ban:
            case VoteType.Gag:
            case VoteType.Mute:
            case VoteType.Silence:
                if (winner.Key == Localizer["Vote.Yes"])
                {
                    switch (_currentVoteType)
                    {
                        case VoteType.Kick:
                            ProcessKickVoteResult();
                            break;
                        case VoteType.Ban:
                            ProcessBanVoteResult();
                            break;
                        case VoteType.Gag:
                            ProcessGagVoteResult();
                            break;
                        case VoteType.Mute:
                            ProcessMuteVoteResult();
                            break;
                        case VoteType.Silence:
                            ProcessSilenceVoteResult();
                            break;
                    }
                }
                break;
        }
    }

    private void ProcessMapVoteResult()
    {
        var winner = _voteResults.OrderByDescending(x => x.Value).FirstOrDefault();
        var maxVotes = _voteResults.Values.Max();
        var winners = _voteResults.Where(x => x.Value == maxVotes).ToList();
        
        
        if (winners.Count == 1 && winner.Key == Localizer["Vote.Yes"])
        {
            if (!string.IsNullOrEmpty(_voteMapName))
            {
                string mapToChange = _voteMapName;
                PlayerExtensions.PrintToAll(Localizer["VoteMap.Changed", mapToChange]);
                
                AddTimer(4.0f, () =>
                {
                    try
                    {
                        Server.ExecuteCommand($"changelevel {mapToChange}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[AdminPlus] Map change error: {ex.Message}");
                    }
                });
            }
        }
    }

    private void ProcessKickVoteResult()
    {
        if (_targetPlayer != null && _targetPlayer.IsValid)
        {
            string reason = Localizer["Reason.VoteResult"];
            Server.ExecuteCommand($"kickid {_targetPlayer.UserId} \"{reason}\"");
            PlayerExtensions.PrintToAll(Localizer["VoteKick.Success", _targetPlayer.PlayerName]);
        }
    }

    private void ProcessBanVoteResult()
    {
        if (_targetPlayer != null && _targetPlayer.IsValid)
        {
            string reason = Localizer["Reason.VoteResult"];
            string steamId = _targetPlayer.SteamID.ToString();
            string playerName = SanitizeName(_targetPlayer.PlayerName);
            string ip = _targetPlayer.IpAddress ?? "-";
            int minutes = 30;

            var expiry = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + minutes * 60;
            var line = $"banid \"{steamId}\" \"{playerName}\" ip:{ip} expiry:{expiry} // {reason}";

            lock (_lock)
            {
                SteamBans[steamId] = (expiry, line, playerName, ip);
                File.WriteAllLines(BannedUserPath, SteamBans.Values.Select(x => x.line));
            }

            _targetPlayer.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_STEAM_BANNED);
            PlayerExtensions.PrintToAll(Localizer["VoteBan.Success", _targetPlayer.PlayerName]);
        }
    }

    private void ProcessGagVoteResult()
    {
        if (_targetPlayer != null && _targetPlayer.IsValid)
        {
            ApplyPunishment(_targetPlayer, "GAG", 30, Localizer["Reason.VoteResult"], null);
            PlayerExtensions.PrintToAll(Localizer["VoteGag.Success", _targetPlayer.PlayerName]);
        }
    }

    private void ProcessMuteVoteResult()
    {
        if (_targetPlayer != null && _targetPlayer.IsValid)
        {
            ApplyPunishment(_targetPlayer, "MUTE", 30, Localizer["Reason.VoteResult"], null);
            PlayerExtensions.PrintToAll(Localizer["VoteMute.Success", _targetPlayer.PlayerName]);
        }
    }

    private void ProcessSilenceVoteResult()
    {
        if (_targetPlayer != null && _targetPlayer.IsValid)
        {
            ApplyPunishment(_targetPlayer, "MUTE", 30, Localizer["Reason.VoteResult"], null);
            ApplyPunishment(_targetPlayer, "GAG", 30, Localizer["Reason.VoteResult"], null);
            PlayerExtensions.PrintToAll(Localizer["VoteSilence.Success", _targetPlayer.PlayerName]);
        }
    }

    private void CleanupVote()
    {
        try
        {
            CloseAllVoteMenus();
            
            _currentVoteMenu = null;
            _voteResults.Clear();
            _playerVotes.Clear();
            
            _voteTimer?.Kill();
            _voteTimer = null;
            _countdownTimer?.Kill();
            _countdownTimer = null;
            
            _currentVoteQuestion = "";
            _voteInitiator = "";
            _currentVoteType = VoteType.General;
            _targetPlayer = null;
            _voteReason = "";
            _voteDuration = 0;
            _voteMapName = "";
            _remainingTime = 30;
            _isVoteInProgress = false;
            _playersWhoClosedMenu.Clear();
            _playersWhoClosedMenu.Clear();
            _playersWhoClosedMenu.Clear();
            _playersWhoClosedMenu.Clear();
            
            Console.WriteLine("[AdminPlus] Vote system cleaned up.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminPlus] Error during vote cleanup: {ex.Message}");
        }
    }
    
    public void CleanupVoteSystem()
    {
        CleanupVote();
    }

    private void CancelVote(CCSPlayerController? caller)
    {
        _isVoteInProgress = false;

        string cancelMessage = Localizer["Vote.Cancelled", _voteInitiator];
        PlayerExtensions.PrintToAll(cancelMessage);

        CloseAllVoteMenus();

        CleanupVote();
    }

    private void CloseAllVoteMenus()
    {
        var players = Utilities.GetPlayers().Where(p => p != null && p.IsValid && !p.IsBot);
        foreach (var player in players)
        {
            if (player.IsValid)
            {
                try
                {
                    CloseMenu(player);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AdminPlus] Error closing menu for {player.PlayerName}: {ex.Message}");
                }
            }
        }
        
        _currentVoteMenu = null;
    }

    private void CmdVoteKick(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null && (!caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/generic")))
        {
            if (caller.IsValid) caller.Print(Localizer["NoPermission"]);
            return;
        }

        if (_isVoteInProgress)
        {
            string message = Localizer["Vote.AlreadyInProgress"];
            if (caller != null && caller.IsValid)
                caller.Print(message);
            else
                Console.WriteLine(message);
            return;
        }

        ShowPlayerSelectionMenu(caller, VoteType.Kick, Localizer["VoteKick.Question"]);
    }

    private void CmdVoteBan(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null && (!caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/generic")))
        {
            if (caller.IsValid) caller.Print(Localizer["NoPermission"]);
            return;
        }

        if (_isVoteInProgress)
        {
            string message = Localizer["Vote.AlreadyInProgress"];
            if (caller != null && caller.IsValid)
                caller.Print(message);
            else
                Console.WriteLine(message);
            return;
        }

        ShowPlayerSelectionMenu(caller, VoteType.Ban, Localizer["VoteBan.Question"]);
    }

    private void CmdVoteGag(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null && (!caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/generic")))
        {
            if (caller.IsValid) caller.Print(Localizer["NoPermission"]);
            return;
        }

        if (_isVoteInProgress)
        {
            string message = Localizer["Vote.AlreadyInProgress"];
            if (caller != null && caller.IsValid)
                caller.Print(message);
            else
                Console.WriteLine(message);
            return;
        }

        ShowPlayerSelectionMenu(caller, VoteType.Gag, Localizer["VoteGag.Question"]);
    }

    private void CmdVoteMute(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null && (!caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/generic")))
        {
            if (caller.IsValid) caller.Print(Localizer["NoPermission"]);
            return;
        }

        if (_isVoteInProgress)
        {
            string message = Localizer["Vote.AlreadyInProgress"];
            if (caller != null && caller.IsValid)
                caller.Print(message);
            else
                Console.WriteLine(message);
            return;
        }

        ShowPlayerSelectionMenu(caller, VoteType.Mute, Localizer["VoteMute.Question"]);
    }

    private void CmdVoteSilence(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null && (!caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/generic")))
        {
            if (caller.IsValid) caller.Print(Localizer["NoPermission"]);
            return;
        }

        if (_isVoteInProgress)
        {
            string message = Localizer["Vote.AlreadyInProgress"];
            if (caller != null && caller.IsValid)
                caller.Print(message);
            else
                Console.WriteLine(message);
            return;
        }

        ShowPlayerSelectionMenu(caller, VoteType.Silence, Localizer["VoteSilence.Question"]);
    }

    private void ShowPlayerSelectionMenu(CCSPlayerController? caller, VoteType voteType, string question)
    {
        var players = Utilities.GetPlayers().Where(p => p != null && p.IsValid && !p.IsBot).ToList();
        
        if (players.Count == 0)
        {
            if (caller != null && caller.IsValid)
                caller.Print(Localizer["Menu.NoPlayers"]);
            else
                Console.WriteLine(Localizer["Menu.NoPlayers"]);
            return;
        }

        string menuTitle = voteType switch
        {
            VoteType.Kick => Localizer["Menu.ChoosePlayerKick"],
            VoteType.Gag => Localizer["Menu.ChoosePlayerGag"],
            VoteType.Mute => Localizer["Menu.ChoosePlayerMute"],
            VoteType.Silence => Localizer["Menu.ChoosePlayerSilence"],
            _ => Localizer["Menu.ChoosePlayer"]
        };

        var playerMenu = CreateMenu(menuTitle);
        
        foreach (var player in players)
        {
            string playerText = $"{player.PlayerName} (#{player.UserId})";
            playerMenu?.AddMenuOption(playerText, (menuCaller, option) => StartPlayerVote(menuCaller, player, voteType, question));
        }

        if (playerMenu != null)
        {
            playerMenu.ExitButton = true;
            
            if (caller != null && caller.IsValid)
                playerMenu.Open(caller);
        }
    }

    private void StartPlayerVote(CCSPlayerController caller, CCSPlayerController targetPlayer, VoteType voteType, string baseQuestion)
    {
        string question = Localizer["Vote.PlayerVoteQuestion", baseQuestion, targetPlayer.PlayerName];
        List<string> options = new() { Localizer["Vote.Yes"], Localizer["Vote.No"] };
        
        StartVote(question, options, caller, voteType, targetPlayer, Localizer["Vote.NoReason"]);
    }

    internal void OnPlayerDisconnectVote(CCSPlayerController player)
    {
        if (_playerVotes.ContainsKey(player))
        {
            string vote = _playerVotes[player];
            _voteResults[vote]--;
            _playerVotes.Remove(player);
            UpdateVoteMenu();
        }
    }

    internal void OnPlayerConnectVote(CCSPlayerController player)
    {
        if (_isVoteInProgress && _currentVoteMenu != null && player.IsValid)
        {
            _currentVoteMenu.Open(player);
        }
    }
}
