using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoCenter.Models;
using AutoCenter.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.System;
using WinRT.Interop;

namespace AutoCenter;

public sealed partial class MainWindow : Window
{
    private readonly SettingsStore _settingsStore = new();
    private readonly MonitorService _monitorService = new();
    private AppSettings _settings;
    private readonly WindowPlacementService _placementService;
    private readonly HotkeyService _hotkeyService;
    private readonly TrayIconService _trayIconService;
    private bool _loading;
    private readonly Dictionary<UIElement, int> _feedbackTokens = new();
    private readonly ObservableCollection<string> _excludedProcesses = new();

    public MainWindow()
    {
        _loading = true;

        InitializeComponent();
        ConfigureTitleBar();
        ApplyMicaBackdrop();

        _settings = _settingsStore.Load();
        _placementService = new WindowPlacementService(_settings);
        _hotkeyService = new HotkeyService(this);
        _trayIconService = new TrayIconService(this);

        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        _trayIconService.TrayIconClicked += OnTrayIconClicked;
        _trayIconService.ExitRequested += OnExitRequested;
        _trayIconService.Initialize();

        Closed += OnClosed;
        AppWindow.Closing += OnAppWindowClosing;

        try
        {
            ConfigureNumericControls();
            ResizeWindow();
            RefreshMonitors();
            ExcludedProcessesList.ItemsSource = _excludedProcesses;
            LoadSettingsIntoControls();
            PopulateAboutInfo();
            ConfigureElevationCard();
            ApplyTrayIconVisibility();
        }
        finally
        {
            _loading = false;
        }

        ApplySettings();
    }

    private void ConfigureElevationCard()
    {
        if (ElevationService.IsElevated)
        {
            ElevateButtonLabel.Text = "Already running as admin";
            ElevateButton.IsEnabled = false;
            ElevationCard.Description = "AutoCenter is currently running with administrator rights and can move elevated app windows.";
        }
    }

    private void ApplyTrayIconVisibility()
    {
        _trayIconService.SetIconVisible(!_settings.HideTrayIcon);
    }

    private void ApplyMicaBackdrop()
    {
        SystemBackdrop = new MicaBackdrop { Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt };
    }

    private void ConfigureTitleBar()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        appWindow.TitleBar.ButtonHoverBackgroundColor = Colors.Transparent;
        appWindow.TitleBar.ButtonPressedBackgroundColor = Colors.Transparent;

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AutoCenter.ico");
        if (File.Exists(iconPath))
        {
            appWindow.SetIcon(iconPath);
        }
    }

    private void ConfigureNumericControls()
    {
        WidthPercentSlider.Minimum = 30;
        WidthPercentSlider.Maximum = 100;
        WidthPercentSlider.StepFrequency = 1;
        HeightPercentSlider.Minimum = 30;
        HeightPercentSlider.Maximum = 100;
        HeightPercentSlider.StepFrequency = 1;

        WidthPixelsBox.Minimum = 120;
        WidthPixelsBox.Maximum = 7680;
        WidthPixelsBox.SmallChange = 20;
        WidthPixelsBox.LargeChange = 100;
        HeightPixelsBox.Minimum = 90;
        HeightPixelsBox.Maximum = 4320;
        HeightPixelsBox.SmallChange = 20;
        HeightPixelsBox.LargeChange = 100;

        OffsetXBox.Minimum = -3000;
        OffsetXBox.Maximum = 3000;
        OffsetXBox.SmallChange = 10;
        OffsetXBox.LargeChange = 100;
        OffsetYBox.Minimum = -3000;
        OffsetYBox.Maximum = 3000;
        OffsetYBox.SmallChange = 10;
        OffsetYBox.LargeChange = 100;

        UniformMarginBox.Minimum = 0;
        UniformMarginBox.Maximum = 2000;
        UniformMarginBox.SmallChange = 8;
        UniformMarginBox.LargeChange = 40;

        MarginXBox.Minimum = 0;
        MarginXBox.Maximum = 2000;
        MarginXBox.SmallChange = 8;
        MarginXBox.LargeChange = 40;
        MarginYBox.Minimum = 0;
        MarginYBox.Maximum = 2000;
        MarginYBox.SmallChange = 8;
        MarginYBox.LargeChange = 40;

        PollIntervalBox.Minimum = 75;
        PollIntervalBox.Maximum = 1500;
        PollIntervalBox.SmallChange = 25;
        PollIntervalBox.LargeChange = 100;
        ActivationDelayBox.Minimum = 0;
        ActivationDelayBox.Maximum = 2000;
        ActivationDelayBox.SmallChange = 25;
        ActivationDelayBox.LargeChange = 100;

        SequenceCountBox.Minimum = 2;
        SequenceCountBox.Maximum = 5;
        SequenceCountBox.SmallChange = 1;
        SequenceTimeoutBox.Minimum = 200;
        SequenceTimeoutBox.Maximum = 3000;
        SequenceTimeoutBox.SmallChange = 50;
        SequenceTimeoutBox.LargeChange = 100;
    }

    private async void ShowFeedback(UIElement target, int durationMs = 1800)
    {
        var token = (_feedbackTokens.TryGetValue(target, out var existing) ? existing : 0) + 1;
        _feedbackTokens[target] = token;
        target.Visibility = Visibility.Visible;
        await Task.Delay(durationMs);
        if (_feedbackTokens.TryGetValue(target, out var current) && current == token)
        {
            target.Visibility = Visibility.Collapsed;
        }
    }

    private void PopulateAboutInfo()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        var versionText = version is null ? "1.0" : $"{version.Major}.{version.Minor}.{version.Build}";
        HomeVersionText.Text = $"v{versionText}";
#if GITHUB_RELEASE
        AboutVersionText.Text = $"Version {versionText} • GitHub";
#else
        AboutVersionText.Text = $"Version {versionText} • Microsoft Store";
        StoreCard.Visibility = Visibility.Collapsed;
#endif
    }

    private void ResizeWindow()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new SizeInt32(780, 620));
    }

    private void RefreshMonitors()
    {
        var monitors = _monitorService.GetMonitors();
        MonitorListView.ItemsSource = monitors;
        SpecificMonitorComboBox.ItemsSource = monitors;

        var selected = monitors.FirstOrDefault(monitor => monitor.DeviceName == _settings.SpecificMonitorDeviceName)
            ?? monitors.FirstOrDefault(monitor => monitor.IsPrimary)
            ?? monitors.FirstOrDefault();

        SpecificMonitorComboBox.SelectedItem = selected;
        if (selected is not null)
        {
            _settings.SpecificMonitorDeviceName = selected.DeviceName;
        }
    }

    private void LoadSettingsIntoControls()
    {
        var wasLoading = _loading;
        _loading = true;

        AutoCenterToggle.IsOn = _settings.AutoCenterNewWindows;
        HotkeyToggle.IsOn = _settings.CenterWithHotkey;
        OnlyOnceCheckBox.IsChecked = _settings.OnlyOncePerWindow;
        SmartFilterCheckBox.IsChecked = _settings.SmartFilter;
        SkipMaximizedCheckBox.IsChecked = _settings.SkipMaximizedWindows;
        IncludeFixedSizeCheckBox.IsChecked = _settings.IncludeFixedSizeWindows;
        StartupToggle.IsOn = _settings.LaunchAtStartup;
        WorkingAreaCheckBox.IsChecked = _settings.UseWorkingArea;

        MonitorModeComboBox.SelectedIndex = (int)_settings.TargetMonitorMode;
        ResizeModeComboBox.SelectedIndex = (int)_settings.ResizeMode;
        AlignmentComboBox.SelectedIndex = (int)_settings.Alignment;

        WidthPercentSlider.Value = _settings.WidthPercent;
        HeightPercentSlider.Value = _settings.HeightPercent;
        WidthPixelsBox.Value = _settings.WidthPixels;
        HeightPixelsBox.Value = _settings.HeightPixels;
        UniformMarginBox.Value = _settings.UniformMargin;
        MarginXBox.Value = _settings.MarginX;
        MarginYBox.Value = _settings.MarginY;
        OffsetXBox.Value = _settings.OffsetX;
        OffsetYBox.Value = _settings.OffsetY;
        PollIntervalBox.Value = _settings.PollIntervalMs;
        ActivationDelayBox.Value = _settings.ActivationDelayMs;

        HotkeyAltCheckBox.IsChecked = (_settings.HotkeyModifiers & 0x0001) != 0;
        HotkeyControlCheckBox.IsChecked = (_settings.HotkeyModifiers & 0x0002) != 0;
        HotkeyShiftCheckBox.IsChecked = (_settings.HotkeyModifiers & 0x0004) != 0;
        HotkeyWinCheckBox.IsChecked = (_settings.HotkeyModifiers & 0x0008) != 0;
        SelectHotkeyKey(_settings.HotkeyVirtualKey);
        HotkeyBypassFixedSizeCheckBox.IsChecked = _settings.HotkeyBypassFixedSize;
        HotkeyBypassMaximizedCheckBox.IsChecked = _settings.HotkeyBypassMaximized;
        HotkeySequenceCheckBox.IsChecked = _settings.UseHotkeySequence;
        SelectSequenceKey(_settings.HotkeySequenceKey);
        SequenceCountBox.Value = _settings.HotkeySequenceCount;
        SequenceTimeoutBox.Value = _settings.HotkeySequenceTimeoutMs;
        HideTrayIconToggle.IsOn = _settings.HideTrayIcon;
        SyncExcludedProcessesCollection();
        ThemeComboBox.SelectedIndex = Math.Clamp(_settings.Theme, 0, 2);
        ApplyTheme();

        UpdateDynamicControlState();
        _loading = wasLoading;
    }

    private void CaptureSettingsFromControls()
    {
        if (_loading)
        {
            return;
        }

        _settings.AutoCenterNewWindows = AutoCenterToggle.IsOn;
        _settings.CenterWithHotkey = HotkeyToggle.IsOn;
        _settings.OnlyOncePerWindow = OnlyOnceCheckBox.IsChecked == true;
        _settings.SmartFilter = SmartFilterCheckBox.IsChecked == true;
        _settings.SkipMaximizedWindows = SkipMaximizedCheckBox.IsChecked == true;
        _settings.IncludeFixedSizeWindows = IncludeFixedSizeCheckBox.IsChecked == true;
        _settings.UseWorkingArea = WorkingAreaCheckBox.IsChecked == true;
        _settings.TargetMonitorMode = (TargetMonitorMode)Math.Max(0, MonitorModeComboBox.SelectedIndex);
        _settings.ResizeMode = (ResizeMode)Math.Max(0, ResizeModeComboBox.SelectedIndex);
        _settings.Alignment = (PlacementAlignment)Math.Max(0, AlignmentComboBox.SelectedIndex);
        _settings.WidthPercent = (int)Math.Round(WidthPercentSlider.Value);
        _settings.HeightPercent = (int)Math.Round(HeightPercentSlider.Value);
        _settings.WidthPixels = SanitizeNumber(WidthPixelsBox.Value, _settings.WidthPixels);
        _settings.HeightPixels = SanitizeNumber(HeightPixelsBox.Value, _settings.HeightPixels);
        _settings.UniformMargin = SanitizeNumber(UniformMarginBox.Value, _settings.UniformMargin);
        _settings.MarginX = SanitizeNumber(MarginXBox.Value, _settings.MarginX);
        _settings.MarginY = SanitizeNumber(MarginYBox.Value, _settings.MarginY);
        _settings.OffsetX = SanitizeNumber(OffsetXBox.Value, _settings.OffsetX);
        _settings.OffsetY = SanitizeNumber(OffsetYBox.Value, _settings.OffsetY);
        _settings.PollIntervalMs = SanitizeNumber(PollIntervalBox.Value, _settings.PollIntervalMs);
        _settings.ActivationDelayMs = SanitizeNumber(ActivationDelayBox.Value, _settings.ActivationDelayMs);
        _settings.LaunchAtStartup = StartupToggle.IsOn;
        _settings.HotkeyModifiers = GetHotkeyModifiers();
        _settings.HotkeyVirtualKey = GetSelectedHotkeyVirtualKey();
        _settings.HotkeyBypassFixedSize = HotkeyBypassFixedSizeCheckBox.IsChecked == true;
        _settings.HotkeyBypassMaximized = HotkeyBypassMaximizedCheckBox.IsChecked == true;
        _settings.UseHotkeySequence = HotkeySequenceCheckBox.IsChecked == true;
        _settings.HotkeySequenceKey = GetSelectedSequenceKey();
        _settings.HotkeySequenceCount = Math.Clamp((int)Math.Round(SequenceCountBox.Value), 2, 5);
        _settings.HotkeySequenceTimeoutMs = SanitizeNumber(SequenceTimeoutBox.Value, _settings.HotkeySequenceTimeoutMs);
        _settings.ExcludedProcessNames = _excludedProcesses.ToList();
        _settings.HideTrayIcon = HideTrayIconToggle.IsOn;
        _settings.Theme = ThemeComboBox.SelectedIndex < 0 ? 0 : ThemeComboBox.SelectedIndex;

        if (SpecificMonitorComboBox.SelectedItem is MonitorInfo monitor)
        {
            _settings.SpecificMonitorDeviceName = monitor.DeviceName;
        }
    }

    private int GetHotkeyModifiers()
    {
        var modifiers = 0;
        if (HotkeyAltCheckBox.IsChecked == true)
        {
            modifiers |= 0x0001;
        }

        if (HotkeyControlCheckBox.IsChecked == true)
        {
            modifiers |= 0x0002;
        }

        if (HotkeyShiftCheckBox.IsChecked == true)
        {
            modifiers |= 0x0004;
        }

        if (HotkeyWinCheckBox.IsChecked == true)
        {
            modifiers |= 0x0008;
        }

        return modifiers == 0 ? 0x0002 | 0x0004 : modifiers;
    }

    private int GetSelectedHotkeyVirtualKey()
    {
        if (HotkeyKeyComboBox.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var key))
        {
            return key;
        }

        return 0x43;
    }

    private void SelectHotkeyKey(int virtualKey)
    {
        foreach (var item in HotkeyKeyComboBox.Items.OfType<ComboBoxItem>())
        {
            if (int.TryParse(item.Tag?.ToString(), out var key) && key == virtualKey)
            {
                HotkeyKeyComboBox.SelectedItem = item;
                return;
            }
        }

        HotkeyKeyComboBox.SelectedIndex = 0;
    }

    private void SelectSequenceKey(int virtualKey)
    {
        foreach (var item in SequenceKeyComboBox.Items.OfType<ComboBoxItem>())
        {
            if (int.TryParse(item.Tag?.ToString(), out var key) && key == virtualKey)
            {
                SequenceKeyComboBox.SelectedItem = item;
                return;
            }
        }

        SequenceKeyComboBox.SelectedIndex = 0;
    }

    private int GetSelectedSequenceKey()
    {
        if (SequenceKeyComboBox.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var key))
        {
            return key;
        }

        return 0x10;
    }

    private static int SanitizeNumber(double value, int fallback)
    {
        return double.IsNaN(value) ? fallback : (int)Math.Round(value);
    }

    private void ApplySettings()
    {
        if (_loading)
        {
            return;
        }

        CaptureSettingsFromControls();
        UpdateDynamicControlState();
        _settingsStore.Save(_settings);
        _placementService.UpdateSettings(_settings);
        _hotkeyService.UpdateSettings(_settings);
    }

    private void ApplyTheme()
    {
        RootGrid.RequestedTheme = ThemeComboBox.SelectedIndex switch
        {
            1 => ElementTheme.Light,
            2 => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }

    private void UpdateDynamicControlState()
    {
        WidthPercentLabel.Text = $"{(int)Math.Round(WidthPercentSlider.Value)}%";
        HeightPercentLabel.Text = $"{(int)Math.Round(HeightPercentSlider.Value)}%";

        SpecificMonitorCard.Visibility = MonitorModeComboBox.SelectedIndex == (int)TargetMonitorMode.Specific
            ? Visibility.Visible : Visibility.Collapsed;

        var resizeMode = (ResizeMode)Math.Max(0, ResizeModeComboBox.SelectedIndex);
        WidthPercentCard.Visibility = resizeMode == ResizeMode.PercentOfMonitor ? Visibility.Visible : Visibility.Collapsed;
        HeightPercentCard.Visibility = resizeMode == ResizeMode.PercentOfMonitor ? Visibility.Visible : Visibility.Collapsed;
        WidthPixelsCard.Visibility = resizeMode == ResizeMode.FixedPixels ? Visibility.Visible : Visibility.Collapsed;
        HeightPixelsCard.Visibility = resizeMode == ResizeMode.FixedPixels ? Visibility.Visible : Visibility.Collapsed;
        UniformMarginCard.Visibility = resizeMode == ResizeMode.UniformMargin ? Visibility.Visible : Visibility.Collapsed;
        MarginXCard.Visibility = resizeMode == ResizeMode.SeparateMargins ? Visibility.Visible : Visibility.Collapsed;
        MarginYCard.Visibility = resizeMode == ResizeMode.SeparateMargins ? Visibility.Visible : Visibility.Collapsed;
        PresetsCard.Visibility = resizeMode == ResizeMode.PercentOfMonitor ? Visibility.Visible : Visibility.Collapsed;

        var useSequence = HotkeySequenceCheckBox.IsChecked == true;
        ModifiersCard.Visibility = useSequence ? Visibility.Collapsed : Visibility.Visible;
        HotkeyKeyCard.Visibility = useSequence ? Visibility.Collapsed : Visibility.Visible;
        SequenceKeyCard.Visibility = useSequence ? Visibility.Visible : Visibility.Collapsed;
        SequenceCountCard.Visibility = useSequence ? Visibility.Visible : Visibility.Collapsed;
        SequenceTimeoutCard.Visibility = useSequence ? Visibility.Visible : Visibility.Collapsed;

        RefreshHotkeyChips();
    }

    private void RefreshHotkeyChips()
    {
        var chips = new List<string>();
        if (HotkeySequenceCheckBox.IsChecked == true)
        {
            var name = SequenceKeyName(GetSelectedSequenceKey());
            var count = double.IsNaN(SequenceCountBox.Value) ? 2 : (int)Math.Round(SequenceCountBox.Value);
            count = Math.Clamp(count, 2, 5);
            for (var i = 0; i < count; i++)
            {
                chips.Add(name);
            }
        }
        else
        {
            if (HotkeyWinCheckBox.IsChecked == true) chips.Add("Win");
            if (HotkeyControlCheckBox.IsChecked == true) chips.Add("Ctrl");
            if (HotkeyAltCheckBox.IsChecked == true) chips.Add("Alt");
            if (HotkeyShiftCheckBox.IsChecked == true) chips.Add("Shift");
            chips.Add(HotkeyKeyName(GetSelectedHotkeyVirtualKey()));
        }
        HotkeyChipsHost.ItemsSource = chips;
    }

    private static string SequenceKeyName(int virtualKey) => virtualKey switch
    {
        16 => "Shift",
        17 => "Ctrl",
        18 => "Alt",
        _ => "?"
    };

    private static string HotkeyKeyName(int virtualKey) => virtualKey switch
    {
        67 => "C",
        32 => "Space",
        13 => "Enter",
        119 => "F8",
        120 => "F9",
        121 => "F10",
        _ => "?"
    };

    private void OnShellNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ShowPage("settings");
        }
        else if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            ShowPage(tag);
        }
    }

    private void ShowPage(string tag)
    {
        HomePage.Visibility = tag == "home" ? Visibility.Visible : Visibility.Collapsed;
        GeneralPage.Visibility = tag == "general" ? Visibility.Visible : Visibility.Collapsed;
        PlacementPage.Visibility = tag == "placement" ? Visibility.Visible : Visibility.Collapsed;
        DisplaysPage.Visibility = tag == "displays" ? Visibility.Visible : Visibility.Collapsed;
        HotkeyPage.Visibility = tag == "hotkey" ? Visibility.Visible : Visibility.Collapsed;
        AdvancedPage.Visibility = tag == "advanced" ? Visibility.Visible : Visibility.Collapsed;
        AboutPage.Visibility = tag == "about" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = tag == "settings" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnDashboardCardClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string tag)
        {
            NavigateTo(tag);
        }
    }

    private void NavigateTo(string tag)
    {
        if (tag == "settings")
        {
            ShellNavigation.SelectedItem = ShellNavigation.SettingsItem;
            return;
        }

        var allItems = ShellNavigation.MenuItems
            .OfType<NavigationViewItem>()
            .Concat(ShellNavigation.FooterMenuItems.OfType<NavigationViewItem>());

        foreach (var item in allItems)
        {
            if (item.Tag is string itemTag && itemTag == tag)
            {
                ShellNavigation.SelectedItem = item;
                return;
            }
        }
    }

    private async void OnHomeHeroClicked(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new Uri("https://github.com/jihedkdiss/AutoCenter/releases/latest"));
    }

    private async void OnOpenStoreClick(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new Uri("ms-windows-store://pdp/?productid=9NJBD74TXX5B"));
    }

    private async void OnViewLogsClick(object sender, RoutedEventArgs e)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AutoCenter");
        Directory.CreateDirectory(folder);
        await Launcher.LaunchFolderPathAsync(folder);
    }

    private async void OnReportBugClick(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new Uri("https://github.com/jihedkdiss/AutoCenter/issues/new?template=bug_report.yaml"));
    }

    private async void OnSponsorClick(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new Uri("https://github.com/sponsors/jihedkdiss"));
    }

    private async void OnGitHubRepoClick(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new Uri("https://github.com/jihedkdiss/AutoCenter"));
    }

    private void OnCenterAllClick(object sender, RoutedEventArgs e)
    {
        var selfHwnd = WindowNative.GetWindowHandle(this);
        var count = _placementService.CenterAllWindows(selfHwnd);
        CenterAllFeedback.Text = count == 0
            ? "No eligible windows."
            : $"Centered {count} window{(count == 1 ? string.Empty : "s")}.";
        ShowFeedback(CenterAllFeedback);
    }

    private void OnHideTrayIconToggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _trayIconService.SetIconVisible(!HideTrayIconToggle.IsOn);
        ApplySettings();
    }

    private void OnElevateClick(object sender, RoutedEventArgs e)
    {
        if (ElevationService.IsElevated)
        {
            return;
        }

        if (ElevationService.RelaunchAsAdmin())
        {
            // Save current settings before exiting so the elevated instance picks them up.
            _settingsStore.Save(_settings);
            Application.Current.Exit();
        }
    }

    private void OnAddExcludedProcessClick(object sender, RoutedEventArgs e)
    {
        AddExcludedProcessFromInput();
    }

    private void OnNewExcludedProcessKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            AddExcludedProcessFromInput();
            e.Handled = true;
        }
    }

    private void AddExcludedProcessFromInput()
    {
        var name = NewExcludedProcessBox.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        // Strip optional .exe so matching against Process.ProcessName works.
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }

        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        if (_excludedProcesses.Any(p => string.Equals(p, name, StringComparison.OrdinalIgnoreCase)))
        {
            NewExcludedProcessBox.Text = string.Empty;
            return;
        }

        _excludedProcesses.Add(name);
        NewExcludedProcessBox.Text = string.Empty;
        UpdateExcludedProcessesEmptyState();
        ApplySettings();
    }

    private void OnRemoveExcludedProcessClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string name)
        {
            for (var i = _excludedProcesses.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_excludedProcesses[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    _excludedProcesses.RemoveAt(i);
                    break;
                }
            }
            UpdateExcludedProcessesEmptyState();
            ApplySettings();
        }
    }

    private void SyncExcludedProcessesCollection()
    {
        _excludedProcesses.Clear();
        foreach (var name in _settings.ExcludedProcessNames)
        {
            _excludedProcesses.Add(name);
        }
        UpdateExcludedProcessesEmptyState();
    }

    private void UpdateExcludedProcessesEmptyState()
    {
        ExcludedProcessesEmptyText.Visibility = _excludedProcesses.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnSettingChanged(object sender, RoutedEventArgs e)
    {
        ApplySettings();
    }

    private void OnSettingChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        ApplySettings();
    }

    private void OnNumberSettingChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        ApplySettings();
    }

    private void OnResizeModeChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplySettings();
    }

    private void OnAlignmentChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplySettings();
    }

    private void OnTargetModeChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplySettings();
    }

    private void OnSpecificMonitorChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplySettings();
    }

    private void OnHotkeyKeyChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplySettings();
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplySettings();
        ApplyTheme();
    }

    private void OnRefreshMonitorsClick(object sender, RoutedEventArgs e)
    {
        RefreshMonitors();
        ApplySettings();
        ShowFeedback(RefreshFeedback);
    }

    private void OnCenterNowClick(object sender, RoutedEventArgs e)
    {
        _placementService.CenterForegroundWindow();
    }

    private void OnResetAllClick(object sender, RoutedEventArgs e)
    {
        _settings = new AppSettings();
        _placementService.ClearHistory();

        _loading = true;
        try
        {
            RefreshMonitors();
            LoadSettingsIntoControls();
        }
        finally
        {
            _loading = false;
        }

        ApplySettings();
        ApplyTheme();
        ShowFeedback(ResetFeedback);
    }

    private void OnCompactPresetClick(object sender, RoutedEventArgs e)
    {
        ApplyPercentPreset(50, 55, "Compact preset applied");
    }

    private void OnComfortPresetClick(object sender, RoutedEventArgs e)
    {
        ApplyPercentPreset(70, 75, "Comfort preset applied");
    }

    private void OnFocusPresetClick(object sender, RoutedEventArgs e)
    {
        ApplyPercentPreset(90, 90, "Focus preset applied");
    }

    private void OnPreservePresetClick(object sender, RoutedEventArgs e)
    {
        ResizeModeComboBox.SelectedIndex = (int)ResizeMode.PreserveCurrentSize;
        ApplySettings();
    }

    private void ApplyPercentPreset(int width, int height, string feedbackMessage)
    {
        ResizeModeComboBox.SelectedIndex = (int)ResizeMode.PercentOfMonitor;
        WidthPercentSlider.Value = width;
        HeightPercentSlider.Value = height;
        ApplySettings();
        PresetFeedbackText.Text = feedbackMessage;
        ShowFeedback(PresetFeedback);
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() => _placementService.CenterForegroundWindow());
    }

    private async void OnStartupToggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        ApplySettings();
        await StartupService.SetStartupAsync(_settings.LaunchAtStartup);
    }

    private void OnTrayIconClicked(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            AppWindow.Show();
            _hotkeyService.Register(_settings.HotkeyModifiers, _settings.HotkeyVirtualKey);
        });
    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            Closed -= OnClosed;
            AppWindow.Closing -= OnAppWindowClosing;
            _loading = false;
            CaptureSettingsFromControls();
            _settingsStore.Save(_settings);
            _hotkeyService.Dispose();
            _trayIconService.Dispose();
            _placementService.Dispose();
            Close();
        });
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        args.Cancel = true;
        sender.Hide();
    }

    private void OnKillExitClick(object sender, RoutedEventArgs e)
    {
        Closed -= OnClosed;
        AppWindow.Closing -= OnAppWindowClosing;
        _loading = false;
        CaptureSettingsFromControls();
        _settingsStore.Save(_settings);
        _hotkeyService.Dispose();
        _trayIconService.Dispose();
        _placementService.Dispose();
        Close();
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _loading = false;
        CaptureSettingsFromControls();
        _settingsStore.Save(_settings);
        _hotkeyService.Dispose();
        _trayIconService.Dispose();
        _placementService.Dispose();
    }
}
