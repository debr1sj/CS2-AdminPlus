using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AdminPlus;

internal enum AdminPlusOptionType
{
    Button,
    Bool,
    Text,
    Input,
    Slider
}

internal sealed class AdminPlusMenuOption
{
    public string Text { get; set; } = string.Empty;
    public AdminPlusOptionType Type { get; set; } = AdminPlusOptionType.Button;
    public bool Disabled { get; set; }
    public object? Value { get; set; }
    public List<object>? SliderValues { get; set; }
    public int SliderDisplayItems { get; set; } = 3;
    public Action<CCSPlayerController, object?> Callback { get; set; } = (_, _) => { };
    public Action<CCSPlayerController, AdminPlusMenuOption, int>? OnSlide { get; set; }
    public Action<CCSPlayerController, AdminPlusMenuOption, string>? OnInputSubmit { get; set; }
}

internal sealed class AdminPlusMenu
{
    public string Title { get; set; } = string.Empty;
    public bool ExitButton { get; set; } = true;
    public bool IsSubMenu { get; set; }
    public bool SuppressHistoryPush { get; set; }
    public Action<CCSPlayerController>? CustomBackAction { get; set; }
    public bool IsExitable { get; set; } = true;
    public bool FreezePlayer { get; set; } = true;
    public bool HasSound { get; set; } = true;
    public List<AdminPlusMenuOption> MenuOptions { get; } = [];
    public Dictionary<string, string> ButtonOverrides { get; } = new();
    public Dictionary<string, string> ControlInfoOverrides { get; } = new();

    public AdminPlusMenu(string title)
    {
        Title = title;
    }

    public void AddMenuOption(string text, Action<CCSPlayerController, object?> callback, bool disabled = false)
    {
        MenuOptions.Add(new AdminPlusMenuOption
        {
            Text = text,
            Type = AdminPlusOptionType.Button,
            Callback = callback,
            Disabled = disabled
        });
    }

    public void AddTextOption(string text, bool selectable = false)
    {
        MenuOptions.Add(new AdminPlusMenuOption
        {
            Text = text,
            Type = AdminPlusOptionType.Text,
            Disabled = !selectable
        });
    }

    public void AddBoolOption(string text, bool defaultValue = false, Action<CCSPlayerController, AdminPlusMenuOption>? onToggle = null)
    {
        string state = defaultValue ? "✔" : "❌";
        MenuOptions.Add(new AdminPlusMenuOption
        {
            Text = $"{text}: [{state}]",
            Type = AdminPlusOptionType.Bool,
            Value = defaultValue,
            Callback = (player, _) =>
            {
                var opt = MenuOptions.Last();
                bool next = !(opt.Value as bool? ?? false);
                opt.Value = next;
                opt.Text = $"{text}: [{(next ? "✔" : "❌")}]";
                onToggle?.Invoke(player, opt);
            }
        });
    }

    public void AddInputOption(string text, string placeHolderText = "", Action<CCSPlayerController, AdminPlusMenuOption, string>? onInputSubmit = null, string? inputPromptMessage = null)
    {
        MenuOptions.Add(new AdminPlusMenuOption
        {
            Text = $"{text}: [{placeHolderText}]",
            Type = AdminPlusOptionType.Input,
            Value = placeHolderText,
            OnInputSubmit = onInputSubmit,
            Callback = (player, _) =>
            {
                var state = AdminPlus.GetOrCreateMenuState(player);
                state.InputMode = true;
                state.InputOption = state.ActiveMenu?.MenuOptions.ElementAtOrDefault(state.SelectedIndex);
                if (!string.IsNullOrWhiteSpace(inputPromptMessage))
                    player.PrintToChat(inputPromptMessage);
            }
        });
    }

    public void AddSliderOption(string text, List<object> values, object? defaultValue = null, int displayItems = 3, Action<CCSPlayerController, AdminPlusMenuOption, int>? onSlide = null)
    {
        if (values.Count == 0)
            values.Add("N/A");
        defaultValue ??= values[0];
        MenuOptions.Add(new AdminPlusMenuOption
        {
            Text = text,
            Type = AdminPlusOptionType.Slider,
            SliderValues = values,
            SliderDisplayItems = Math.Max(1, Math.Min(displayItems, values.Count)),
            Value = defaultValue,
            OnSlide = onSlide,
            Callback = (player, _) =>
            {
                var opt = MenuOptions.Last();
                int idx = GetSliderIndex(opt);
                onSlide?.Invoke(player, opt, idx);
            }
        });
    }

    public void OverrideButton(string button, string newButton) => ButtonOverrides[button] = newButton;
    public void OverrideControlInfo(string control, string newControlInfo) => ControlInfoOverrides[control] = newControlInfo;
    public void Open(CCSPlayerController player) => AdminPlus.OpenMenu(player, this);

    internal static int GetSliderIndex(AdminPlusMenuOption option)
    {
        if (option.SliderValues == null || option.SliderValues.Count == 0 || option.Value == null) return 0;
        int idx = option.SliderValues.FindIndex(x => Equals(x, option.Value));
        return idx < 0 ? 0 : idx;
    }
}

internal sealed class AdminPlusMenuState
{
    public AdminPlusMenu? ActiveMenu { get; set; }
    public Stack<AdminPlusMenu> History { get; } = new();
    public int SelectedIndex { get; set; }
    public DateTime LastInputUtc { get; set; } = DateTime.MinValue;
    public DateTime OpenedAtUtc { get; set; } = DateTime.MinValue;
    public string PreviousButtonsSnapshot { get; set; } = string.Empty;
    public bool InputMode { get; set; }
    public AdminPlusMenuOption? InputOption { get; set; }
}

internal sealed class AdminPlusMenuRuntimeConfig
{
    public string Move = "[W/S]";
    public string Select = "[E]";
    public string Back = "[A]";
    public string Exit = "[R]";
    public string ScrollUpButton = "W";
    public string ScrollDownButton = "S";
    public string SelectButton = "E";
    public string BackButton = "A";
    public string SlideLeftButton = "A";
    public string SlideRightButton = "D";
    public string ExitButton = "R";
}

public partial class AdminPlus
{
    private const int MenuOpenGraceMs = 400;
    private static readonly TimeSpan MenuInputDebounce = TimeSpan.FromMilliseconds(120);

    private static readonly Dictionary<int, AdminPlusMenuState> _menuStates = new();
    private static readonly Dictionary<int, Dictionary<uint, FrozenPawnState>> _savedMenuFreeze = new();
    private static readonly Dictionary<int, int> _possessedBotSlot = new();
    private static readonly HashSet<int> _frozenSlots = [];
    private static readonly AdminPlusMenuRuntimeConfig _menuConfig = new();
    private static CCSPlayerController? _menuSelectionCaller;
    private static readonly Dictionary<string, PlayerButtons> _buttonMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["W"] = PlayerButtons.Forward,
        ["S"] = PlayerButtons.Back,
        ["E"] = PlayerButtons.Use,
        ["A"] = PlayerButtons.Moveleft,
        ["D"] = PlayerButtons.Moveright,
        ["Shift"] = PlayerButtons.Speed,
        ["R"] = PlayerButtons.Reload,
        ["Tab"] = PlayerButtons.Scoreboard
    };

    internal static void ApplyMenuConfig(AdminPlusMenuConfigFile config)
    {
        static string N(string? v, string fallback) => string.IsNullOrWhiteSpace(v) ? fallback : v.Trim();
        static string FirstKey(string? v, string fallback)
        {
            var key = N(v, fallback);
            var comma = key.IndexOf(',');
            return comma >= 0 ? key[..comma].Trim() : key;
        }

        _menuConfig.ScrollUpButton = N(config.MoveUp, "W");
        _menuConfig.ScrollDownButton = N(config.MoveDown, "S");
        _menuConfig.SelectButton = N(config.Select, "E");
        _menuConfig.BackButton = FirstKey(config.Back, "A");
        _menuConfig.ExitButton = FirstKey(config.Exit, "R");
        _menuConfig.SlideLeftButton = N(config.SliderLeft, "A");
        _menuConfig.SlideRightButton = N(config.SliderRight, "D");

        static string Label(string keys) =>
            "[" + string.Join(" / ", keys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) + "]";

        _menuConfig.Move = Label($"{_menuConfig.ScrollUpButton}/{_menuConfig.ScrollDownButton}");
        _menuConfig.Select = Label(_menuConfig.SelectButton);
        _menuConfig.Back = Label(_menuConfig.BackButton);
        _menuConfig.Exit = Label(_menuConfig.ExitButton);
    }

    internal static AdminPlusMenuState GetOrCreateMenuState(CCSPlayerController player)
    {
        if (!_menuStates.TryGetValue(player.Slot, out var state))
        {
            state = new AdminPlusMenuState();
            _menuStates[player.Slot] = state;
        }
        return state;
    }

    internal static AdminPlusMenu? CreateMenu(string title, Action<CCSPlayerController>? backAction = null)
    {
        var menu = new AdminPlusMenu(title);
        if (backAction != null)
            menu.CustomBackAction = backAction;
        return menu;
    }

    internal static AdminPlusMenu? CreateMenuForcedType(string title, object? menuType = null, Action<CCSPlayerController>? backAction = null)
    {
        var menu = new AdminPlusMenu(title);
        if (backAction != null)
            menu.CustomBackAction = backAction;
        return menu;
    }

    internal static void OpenMenu(CCSPlayerController player, AdminPlusMenu? menu)
    {
        if (menu == null || !player.IsValid) return;
        var state = GetOrCreateMenuState(player);
        var currentMenu = state.ActiveMenu;
        bool replacingActiveMenu = currentMenu != null;
        if (replacingActiveMenu && currentMenu != null && !menu.SuppressHistoryPush)
            state.History.Push(currentMenu);
        state.ActiveMenu = menu;
        state.SelectedIndex = ClampToSelectableIndex(state, state.SelectedIndex);
        state.PreviousButtonsSnapshot = player.Buttons.ToString();
        if (!replacingActiveMenu)
        {
            var now = DateTime.UtcNow;
            state.LastInputUtc = now;
            state.OpenedAtUtc = now;
        }
        SetFrozen(player, menu.FreezePlayer);
        RenderMenu(player, state);
    }

    internal static void CleanupMenuForSlot(int slot)
    {
        _menuStates.Remove(slot);
        _frozenSlots.Remove(slot);
        _savedMenuFreeze.Remove(slot);
        _possessedBotSlot.Remove(slot);
        foreach (var pair in _possessedBotSlot.Where(p => p.Value == slot).Select(p => p.Key).ToList())
            _possessedBotSlot.Remove(pair);
    }

    private HookResult OnInternalMenuRoundPrestart(EventRoundPrestart ev, GameEventInfo info)
    {
        CloseAllMenus();
        return HookResult.Continue;
    }

    private HookResult OnInternalMenuPlayerSpawn(EventPlayerSpawn ev, GameEventInfo info)
    {
        var player = ev.Userid;
        if (player is { IsValid: true })
            CloseMenu(player);

        return HookResult.Continue;
    }

    private static void CloseAllMenus()
    {
        foreach (var player in Utilities.GetPlayers())
        {
            if (player is { IsValid: true })
                CloseMenu(player);
        }
    }

    internal static void CloseMenu(CCSPlayerController player)
    {
        if (!player.IsValid) return;
        if (_menuStates.TryGetValue(player.Slot, out var state))
        {
            state.ActiveMenu = null;
            state.History.Clear();
            state.SelectedIndex = 0;
            state.InputMode = false;
            state.InputOption = null;
            player.PrintToCenter(" ");
        }
        SetFrozen(player, false);
    }

    private void OnInternalMenuTick()
    {
        foreach (var pl in Utilities.GetPlayers())
        {
            if (!pl.IsValid) continue;
            if (_menuStates.TryGetValue(pl.Slot, out var state) && state.ActiveMenu != null)
            {
                if (pl.Pawn?.Value?.MovementServices is null)
                {
                    CloseMenu(pl);
                    continue;
                }

                PlayerButtons buttons;
                try
                {
                    buttons = pl.Buttons;
                }
                catch
                {
                    CloseMenu(pl);
                    continue;
                }

                ProcessMenuInput(pl, state, buttons);
                RenderMenu(pl, state);
            }
            if (_frozenSlots.Contains(pl.Slot))
                SetFrozen(pl, true, stripWish: true);
        }
    }

    private void OnInternalMenuPreEntityThink()
    {
        foreach (var pl in Utilities.GetPlayers())
        {
            if (!pl.IsValid || !_frozenSlots.Contains(pl.Slot))
                continue;

            SetFrozen(pl, true, stripWish: false);
        }
    }

    private HookResult OnInternalMenuBotTakeover(EventBotTakeover ev, GameEventInfo info)
    {
        var human = ev.Userid;
        var bot = ev.Botid;
        if (human is not { IsValid: true } || bot is not { IsValid: true })
            return HookResult.Continue;

        _possessedBotSlot[human.Slot] = bot.Slot;
        return HookResult.Continue;
    }

    /// <summary>
    /// Button handling aligned with MenuManagerCS2: react on button snapshot changes,
    /// use HasFlag (not edge detection), and treat exit as its own check (not else-if).
    /// </summary>
    private static void ProcessMenuInput(CCSPlayerController player, AdminPlusMenuState state, PlayerButtons buttons)
    {
        if (!player.IsValid || state.ActiveMenu == null || state.InputMode)
            return;

        var buttonsSnapshot = buttons.ToString();
        if (state.PreviousButtonsSnapshot == buttonsSnapshot)
            return;

        state.PreviousButtonsSnapshot = buttonsSnapshot;
        var menu = state.ActiveMenu;

        if (menu.ExitButton && HasMenuButton(menu, "ExitButton", _menuConfig.ExitButton, buttons))
        {
            CloseMenu(player);
            return;
        }

        if (DateTime.UtcNow - state.OpenedAtUtc < TimeSpan.FromMilliseconds(MenuOpenGraceMs))
            return;
        if (DateTime.UtcNow - state.LastInputUtc < MenuInputDebounce)
            return;

        bool handled = false;
        if (HasMenuButton(menu, "ScrollUpButton", _menuConfig.ScrollUpButton, buttons))
        {
            MoveSelection(state, -1);
            handled = true;
        }
        else if (HasMenuButton(menu, "ScrollDownButton", _menuConfig.ScrollDownButton, buttons))
        {
            MoveSelection(state, 1);
            handled = true;
        }
        else if (HasMenuButton(menu, "SlideLeftButton", _menuConfig.SlideLeftButton, buttons))
        {
            if (IsSliderSelected(state))
                SlideSelection(player, state, -1);
            else
                NavigateBack(player, state);
            handled = true;
        }
        else if (HasMenuButton(menu, "SlideRightButton", _menuConfig.SlideRightButton, buttons))
        {
            SlideSelection(player, state, 1);
            handled = true;
        }
        else if (HasMenuButton(menu, "SelectButton", _menuConfig.SelectButton, buttons))
        {
            SelectCurrentOption(player, state);
            handled = true;
        }

        if (handled)
            state.LastInputUtc = DateTime.UtcNow;
    }

    private HookResult OnInternalMenuSay(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid) return HookResult.Continue;
        if (!_menuStates.TryGetValue(player.Slot, out var state) || !state.InputMode || state.InputOption == null)
            return HookResult.Continue;

        string input = (command.ArgString ?? string.Empty).Replace("\"", "").Trim();
        if (input.Length == 0) return HookResult.Handled;
        if (input.Equals("cancel", StringComparison.OrdinalIgnoreCase))
        {
            state.InputMode = false;
            state.InputOption = null;
            RenderMenu(player, state);
            return HookResult.Handled;
        }

        var opt = state.InputOption;
        string prefix = opt.Text.Split(':')[0];
        opt.Value = input;
        opt.Text = $"{prefix}: [{input}]";
        opt.OnInputSubmit?.Invoke(player, opt, input);
        state.InputMode = false;
        state.InputOption = null;
        RenderMenu(player, state);
        return HookResult.Handled;
    }

    private static bool HasMenuButton(AdminPlusMenu menu, string overrideKey, string configButton, PlayerButtons buttons)
    {
        var buttonName = menu.ButtonOverrides.TryGetValue(overrideKey, out var overrideButton)
            ? overrideButton
            : configButton;

        return _buttonMap.TryGetValue(buttonName, out var btn) && buttons.HasFlag(btn);
    }

    private static bool IsSliderSelected(AdminPlusMenuState state)
    {
        var menu = state.ActiveMenu;
        if (menu == null || menu.MenuOptions.Count == 0) return false;
        var option = menu.MenuOptions[Math.Clamp(state.SelectedIndex, 0, menu.MenuOptions.Count - 1)];
        return option.Type == AdminPlusOptionType.Slider;
    }

    private static void MoveSelection(AdminPlusMenuState state, int direction)
    {
        var menu = state.ActiveMenu;
        if (menu == null || menu.MenuOptions.Count == 0) return;
        int count = menu.MenuOptions.Count;
        int idx = state.SelectedIndex;
        for (int i = 0; i < count; i++)
        {
            idx = (idx + direction + count) % count;
            if (!menu.MenuOptions[idx].Disabled)
            {
                state.SelectedIndex = idx;
                return;
            }
        }
    }

    private static void SlideSelection(CCSPlayerController player, AdminPlusMenuState state, int direction)
    {
        var menu = state.ActiveMenu;
        if (menu == null || menu.MenuOptions.Count == 0) return;
        var option = menu.MenuOptions[Math.Clamp(state.SelectedIndex, 0, menu.MenuOptions.Count - 1)];
        if (option.Type != AdminPlusOptionType.Slider || option.SliderValues == null || option.SliderValues.Count == 0) return;

        int idx = AdminPlusMenu.GetSliderIndex(option) + direction;
        if (idx < 0 || idx >= option.SliderValues.Count) return;
        option.Value = option.SliderValues[idx];
        option.OnSlide?.Invoke(player, option, idx);
    }

    private static void SelectCurrentOption(CCSPlayerController player, AdminPlusMenuState state)
    {
        var menu = state.ActiveMenu;
        if (menu == null || menu.MenuOptions.Count == 0) return;
        var option = menu.MenuOptions[Math.Clamp(state.SelectedIndex, 0, menu.MenuOptions.Count - 1)];
        if (option.Disabled) return;

        _menuSelectionCaller = player;
        try { option.Callback(player, null); }
        finally { _menuSelectionCaller = null; }
    }

    private static bool NavigateBack(CCSPlayerController player, AdminPlusMenuState state)
    {
        var current = state.ActiveMenu;
        if (current?.CustomBackAction != null)
        {
            current.CustomBackAction.Invoke(player);
            return true;
        }

        if (state.History.Count == 0)
        {
            CloseMenu(player);
            return true;
        }

        state.ActiveMenu = state.History.Pop();
        state.SelectedIndex = ClampToSelectableIndex(state, state.SelectedIndex);
        return true;
    }

    private static int ClampToSelectableIndex(AdminPlusMenuState state, int fallback)
    {
        var menu = state.ActiveMenu;
        if (menu == null || menu.MenuOptions.Count == 0) return 0;
        int idx = Math.Clamp(fallback, 0, menu.MenuOptions.Count - 1);
        if (!menu.MenuOptions[idx].Disabled) return idx;
        int first = menu.MenuOptions.FindIndex(x => !x.Disabled);
        return first >= 0 ? first : 0;
    }

    private static void RenderMenu(CCSPlayerController player, AdminPlusMenuState state)
    {
        var menu = state.ActiveMenu;
        if (menu == null) return;

        const int visible = 5;
        int total = menu.MenuOptions.Count;
        int selected = Math.Clamp(state.SelectedIndex, 0, Math.Max(0, total - 1));
        int start = Math.Max(0, selected - (visible / 2));
        if (start + visible > total) start = Math.Max(0, total - visible);
        int end = Math.Min(total, start + visible);

        var sb = new StringBuilder();
        sb.Append($"<b><font color='red' class='fontSize-m'>{menu.Title}</font></b> <font color='yellow' class='fontSize-sm'>{selected + 1}</font>/<font color='orange' class='fontSize-sm'>{total}</font><br>");

        for (int i = start; i < end; i++)
        {
            var opt = menu.MenuOptions[i];
            string text = BuildOptionText(opt);
            bool isSelected = i == selected;
            string color = opt.Disabled ? "grey" : (isSelected ? "#9acd32" : "white");
            if (isSelected && !opt.Disabled)
                sb.Append($"<b><font color='yellow'>►[</font> <font color='{color}' class='fontSize-m'>{text}</font> <font color='yellow'>]◄</font></b><br>");
            else
                sb.Append($"<font color='{color}' class='fontSize-m'>{text}</font><br>");
        }

        string move = menu.ControlInfoOverrides.TryGetValue("Move", out var m) ? m : _menuConfig.Move;
        string select = menu.ControlInfoOverrides.TryGetValue("Select", out var s) ? s : _menuConfig.Select;
        string exit = menu.ControlInfoOverrides.TryGetValue("Exit", out var e) ? e : _menuConfig.Exit;
        bool canGoBack = state.History.Count > 0 || menu.CustomBackAction != null;
        if (canGoBack)
        {
            string back = menu.ControlInfoOverrides.TryGetValue("Back", out var b) ? b : _menuConfig.Back;
            sb.Append($"<font color='#ff3333' class='fontSize-sm'>Move: <font color='#f5a142'>{move}</font> | <font color='#ff3333'>Select: <font color='#f5a142'>{select}</font> | <font color='#ff3333'>Back: <font color='#f5a142'>{back}</font> | <font color='#ff3333'>Exit: <font color='#f5a142'>{exit}</font></font>");
        }
        else
        {
            sb.Append($"<font color='#ff3333' class='fontSize-sm'>Move: <font color='#f5a142'>{move}</font> | <font color='#ff3333'>Select: <font color='#f5a142'>{select}</font> | <font color='#ff3333'>Exit: <font color='#f5a142'>{exit}</font></font>");
        }
        player.PrintToCenterHtml(sb.ToString());
    }

    private static string BuildOptionText(AdminPlusMenuOption option)
    {
        if (option.Type == AdminPlusOptionType.Slider && option.SliderValues != null)
        {
            int idx = AdminPlusMenu.GetSliderIndex(option);
            int window = option.SliderDisplayItems;
            int start = Math.Max(0, idx - (window / 2));
            if (start + window > option.SliderValues.Count) start = Math.Max(0, option.SliderValues.Count - window);
            int end = Math.Min(option.SliderValues.Count - 1, start + window - 1);
            var items = new List<string>();
            for (int i = start; i <= end; i++)
            {
                string v = option.SliderValues[i]?.ToString() ?? "-";
                items.Add(i == idx ? $"<font color='#9acd32'>{v}</font>" : $"<font color='silver'>{v}</font>");
            }
            string left = idx > 0 ? "‹" : "<font color='#888888'>‹</font>";
            string right = idx < option.SliderValues.Count - 1 ? "›" : "<font color='#888888'>›</font>";
            return $"{option.Text}: {left} {string.Join(" ", items)} {right}";
        }
        return option.Text;
    }

    private static void SetFrozen(CCSPlayerController player, bool frozen, bool stripWish = false)
    {
        // Takeover applies the occupant's usercmd to Pawn before OnTick, so
        // writing VelocityModifier there never sees the walk. PreEntityThink
        // sets MOVETYPE_NONE and pins origin. Do not clear buttons in
        // PreEntityThink: the menu reads W/S on OnTick.
        if (frozen)
            _frozenSlots.Add(player.Slot);
        else
            _frozenSlots.Remove(player.Slot);

        try
        {
            var pawns = OccupiedPawns(player);
            if (frozen)
            {
                if (!_savedMenuFreeze.TryGetValue(player.Slot, out var saved))
                {
                    saved = new Dictionary<uint, FrozenPawnState>();
                    _savedMenuFreeze[player.Slot] = saved;
                }

                foreach (var pawn in pawns)
                {
                    if (!saved.TryGetValue(pawn.Index, out var state))
                    {
                        state = SnapshotPawn(pawn);
                        saved[pawn.Index] = state;
                    }

                    ImmobilizePawn(pawn, state);
                    if (stripWish)
                        StripWish(pawn);
                }

                return;
            }

            if (_savedMenuFreeze.TryGetValue(player.Slot, out var restore))
            {
                foreach (var pawn in pawns)
                {
                    if (restore.TryGetValue(pawn.Index, out var state))
                        RestorePawn(pawn, state);
                }

                _savedMenuFreeze.Remove(player.Slot);
            }
        }
        catch
        {
            // Occupied pawn can vanish mid-takeover; do not take down OnTick.
        }
    }

    private static List<CBasePlayerPawn> OccupiedPawns(CCSPlayerController player)
    {
        var pawns = new List<CBasePlayerPawn>(4);
        AddPawn(pawns, player.Pawn?.Value);
        AddPawn(pawns, player.PlayerPawn?.Value);
        try
        {
            AddPawn(pawns, player.ObserverPawn?.Value);
        }
        catch
        {
            // Observer pawn is optional.
        }

        try
        {
            var original = player.OriginalControllerOfCurrentPawn.Value;
            if (original != null && original.IsValid && original.Slot != player.Slot)
            {
                AddPawn(pawns, original.Pawn?.Value);
                AddPawn(pawns, original.PlayerPawn?.Value);
            }
        }
        catch
        {
            // Occupancy handle can be unreadable during inverted takeover.
        }

        if (_possessedBotSlot.TryGetValue(player.Slot, out var botSlot))
        {
            var bot = Utilities.GetPlayerFromSlot(botSlot);
            if (bot != null && bot.IsValid)
            {
                AddPawn(pawns, bot.Pawn?.Value);
                AddPawn(pawns, bot.PlayerPawn?.Value);
            }
        }

        foreach (var other in Utilities.GetPlayers())
        {
            if (other == null || !other.IsValid || other.Slot == player.Slot)
                continue;

            try
            {
                var body = other.PlayerPawn?.Value;
                if (body == null || !body.IsValid)
                    continue;

                if (body.Controller?.Value is CCSPlayerController occupier
                    && occupier.IsValid
                    && occupier.Slot == player.Slot)
                {
                    AddPawn(pawns, body);
                }
            }
            catch
            {
                // Skip this candidate; other sources still apply.
            }
        }

        return pawns;
    }

    private static void AddPawn(List<CBasePlayerPawn> pawns, CBasePlayerPawn? pawn)
    {
        if (pawn == null || !pawn.IsValid)
            return;

        foreach (var existing in pawns)
        {
            if (existing.Index == pawn.Index)
                return;
        }

        pawns.Add(pawn);
    }

    private static FrozenPawnState SnapshotPawn(CBasePlayerPawn pawn)
    {
        var origin = pawn.AbsOrigin;
        return new FrozenPawnState
        {
            MoveType = pawn.MoveType,
            ActualMoveType = pawn.ActualMoveType,
            OriginX = origin?.X ?? 0f,
            OriginY = origin?.Y ?? 0f,
            OriginZ = origin?.Z ?? 0f,
        };
    }

    private static void ImmobilizePawn(CBasePlayerPawn pawn, FrozenPawnState state)
    {
        pawn.MoveType = MoveType_t.MOVETYPE_NONE;
        pawn.ActualMoveType = MoveType_t.MOVETYPE_NONE;
        try
        {
            Utilities.SetStateChanged(pawn, "CBaseEntity", "m_MoveType");
            Utilities.SetStateChanged(pawn, "CBaseEntity", "m_nActualMoveType");
        }
        catch
        {
            // Schema mark is best-effort; MOVETYPE_NONE still applies server-side.
        }

        try
        {
            // Null angles: rewriting AbsRotation every think overwrites mouse look.
            pawn.Teleport(
                new System.Numerics.Vector3(state.OriginX, state.OriginY, state.OriginZ),
                null,
                new System.Numerics.Vector3(0f, 0f, 0f));
        }
        catch
        {
            // Pawn can be mid-swap during takeover.
        }
    }

    private static void StripWish(CBasePlayerPawn pawn)
    {
        try
        {
            if (pawn.MovementServices is not CPlayer_MovementServices movement)
                return;

            var states = movement.Buttons.ButtonStates;
            if (states.Length > 0)
                states[0] &= ~MovementButtonMask;

            movement.ForwardMove = 0f;
            movement.LeftMove = 0f;
            movement.UpMove = 0f;
            movement.CmdForwardMove = 0f;
            movement.CmdLeftMove = 0f;
            movement.CmdUpMove = 0f;
        }
        catch
        {
            // Menu already consumed this tick's buttons.
        }
    }

    private static void RestorePawn(CBasePlayerPawn pawn, FrozenPawnState state)
    {
        pawn.MoveType = state.MoveType;
        pawn.ActualMoveType = state.ActualMoveType;
        try
        {
            Utilities.SetStateChanged(pawn, "CBaseEntity", "m_MoveType");
            Utilities.SetStateChanged(pawn, "CBaseEntity", "m_nActualMoveType");
        }
        catch
        {
            // Restore write still happened even if the mark failed.
        }
    }

    private static readonly ulong MovementButtonMask =
        (ulong)(PlayerButtons.Forward | PlayerButtons.Back | PlayerButtons.Moveleft
            | PlayerButtons.Moveright | PlayerButtons.Jump | PlayerButtons.Duck | PlayerButtons.Speed);

    private readonly struct FrozenPawnState
    {
        public MoveType_t MoveType { get; init; }
        public MoveType_t ActualMoveType { get; init; }
        public float OriginX { get; init; }
        public float OriginY { get; init; }
        public float OriginZ { get; init; }
    }
}
