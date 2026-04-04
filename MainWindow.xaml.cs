using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace MouseTool;

internal partial class MainWindow : Window
{
    private readonly MouseKeeperApplicationContext? _context;
    private bool _allowCloseWithoutPrompt;
    private bool _hasPendingChanges;
    private bool _isRefreshing;

    internal MainWindow()
    {
        InitializeComponent();
        Icon = BrandAssetInterop.CreateBitmapSource(BrandAssets.AppIcon);
        LogoImage.Source = BrandAssetInterop.CreateBitmapSource(BrandAssets.LogoImage);
        ApplyDesignTimePreview();
    }

    internal MainWindow(MouseKeeperApplicationContext context)
        : this()
    {
        _context = context;
        RefreshView();
    }

    public void RefreshView()
    {
        if (_context is null)
        {
            ApplyDesignTimePreview();
            return;
        }

        _isRefreshing = true;

        var running = _context.IsRunning;
        var config = _context.Config;

        Title = T("AppName");
        HeroTitleText.Text = T("HeroTitle");
        HeroSubtitleText.Text = T("HeroSubtitle");
        LanguageLabelText.Text = T("LanguageLabel");

        SetBadgeState(running);
        StatusTitleText.Text = running ? T("OverviewStatusActive") : T("OverviewStatusPaused");
        StatusBodyText.Text = _context.StatusText;
        AnchorValueText.Text = string.Format(T("OverviewAnchorValue"), config.LastPrimaryMousePosition.X, config.LastPrimaryMousePosition.Y);
        HowTitleText.Text = T("OverviewHowTitle");
        HowBodyText.Text = NormalizeParagraphText(T("OverviewHowBody"));
        WorkflowTitleText.Text = T("OverviewWorkflowTitle");
        WorkflowBodyText.Text = NormalizeParagraphText(T("OverviewWorkflowBody"));
        TipsTitleText.Text = T("OverviewTipsTitle");
        TipsBodyText.Text = NormalizeParagraphText(T("OverviewTipsBody"));

        SupportTitleText.Text = T("OverviewSupportTitle");
        GratitudeText.Text = T("OverviewGratitudeMessage");
        SupportNoteText.Text = T("OverviewSupportNote");
        SupportEmailRun.Text = T("OverviewSupportEmail");

        SetIconButtonContent(HelpButton, "?", T("HelpButton"));
        SetIconButtonContent(CoffeeButton, "?", T("CoffeeButton"));
        HelpHintText.Text = T("OverviewHelpHint");
        CoffeeHintText.Text = T("OverviewCoffeeHint");

        OverviewTab.Header = T("TabOverview");
        DisplaysTab.Header = T("TabDisplays");
        BehaviorTab.Header = T("TabBehavior");
        DiagnosticsTab.Header = T("TabDiagnostics");

        DisplaysTitleText.Text = T("DisplaysTitle");
        DisplaysBodyText.Text = T("DisplaysBody");
        DisplaysPrimaryLabel.Text = T("DisplaysPrimary");
        DisplaysTouchLabel.Text = T("DisplaysTouch");
        DisplaysTipsTitleText.Text = T("DisplaysTipsTitle");
        DisplaysTipsBody1Text.Text = T("DisplaysTipsBody1");
        DisplaysTipsBody2Text.Text = T("DisplaysTipsBody2");

        BehaviorTitleText.Text = T("BehaviorTitle");
        BehaviorBodyText.Text = T("BehaviorBody");
        RestoreImmediatelyCheckBox.Content = T("BehaviorRestoreNow");
        RestoreHelpText.Text = T("BehaviorRestoreHelp");
        AllowMouseOnTouchscreenCheckBox.Content = T("BehaviorAllowMouseTouchscreen");
        AllowMouseOnTouchscreenHelpText.Text = T("BehaviorAllowMouseTouchscreenHelp");
        StartWithWindowsCheckBox.Content = T("BehaviorStartWithWindows");
        StartWithWindowsHelpText.Text = T("BehaviorStartWithWindowsHelp");

        DiagnosticsTitleText.Text = T("DiagnosticsTitle");
        DiagnosticsBodyText.Text = T("DiagnosticsBody");
        EnableLogButton.Content = T("DiagnosticsEnableLog");
        DisableLogButton.Content = T("DiagnosticsDisableLog");
        OpenLogButton.Content = T("DiagnosticsOpenLog");

        StartButton.Content = T("FooterStart");
        StopButton.Content = T("FooterPause");
        TrayButton.Content = T("FooterTray");
        ConfigButton.Content = T("FooterConfig");
        ApplyButton.Content = T("FooterApply");

        LogStatusText.Text = _context.LoggingEnabled ? T("DiagnosticsLoggingEnabled") : T("DiagnosticsLoggingDisabled");
        LogStatusText.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_context.LoggingEnabled ? "#1C7844" : "#586476"));

        RefreshDisplayCombos(config);
        RefreshLanguageCombo(config.SelectedLanguage);

        RestoreImmediatelyCheckBox.IsChecked = config.RestoreImmediatelyOnTouchRelease;
        AllowMouseOnTouchscreenCheckBox.IsChecked = config.AllowMouseOnTouchscreen;
        StartWithWindowsCheckBox.IsChecked = config.StartWithWindows;

        StartButton.IsEnabled = !running;
        StopButton.IsEnabled = running;
        EnableLogButton.IsEnabled = !_context.LoggingEnabled;
        DisableLogButton.IsEnabled = _context.LoggingEnabled;
        UpdateSaveState(_hasPendingChanges ? T("SaveStatusPending") : T("SaveStatusCurrent"), _hasPendingChanges);

        _isRefreshing = false;
    }

    public void AllowCloseWithoutPrompt()
    {
        _allowCloseWithoutPrompt = true;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_allowCloseWithoutPrompt || _context is null)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        var dialog = new CloseChoiceWindow(_context) { Owner = this };
        dialog.ShowDialog();

        switch (dialog.SelectedAction)
        {
            case CloseChoiceAction.MinimizeToTray:
                _context.MinimizeMainFormToTray();
                break;
            case CloseChoiceAction.StopAndExit:
                _context.StopProtection(showBalloon: false, persistEnabled: false);
                _context.ExitApplication();
                break;
        }
    }

    private void ApplyDesignTimePreview()
    {
        Title = "MouseTool";
        HeroTitleText.Text = "MouseTool Control Center";
        HeroSubtitleText.Text = "Design-time preview for the new WPF dashboard.";
        LanguageLabelText.Text = "Language";
        SetBadgeState(true);
        StatusTitleText.Text = "Protection is active";
        StatusBodyText.Text = "Preview mode is showing the WPF layout for future manual editing.";
        AnchorValueText.Text = "Saved pointer anchor: X 1280  |  Y 720";
        HowTitleText.Text = "How it works";
        HowBodyText.Text = "1. Use the physical mouse on the main display.\n2. Use touch on the secondary display.\n3. Move the physical mouse again to restore the anchor.";
        WorkflowTitleText.Text = "Mini tutorial";
        WorkflowBodyText.Text = "1. Leave the mouse where you want to continue.\n2. Use the touchscreen for quick controls.\n3. Move the mouse again to resume work from the saved position.";
        TipsTitleText.Text = "Best practices";
        TipsBodyText.Text = "Keep the main display for regular work and the touchscreen for quick actions.\nUse diagnostics only when you need troubleshooting data.";
        SupportTitleText.Text = "Support and suggestions";
        GratitudeText.Text = "Thank you for using MouseTool.";
        SupportNoteText.Text = "Suggestions and day-to-day feedback help improve the project.";
        SupportEmailRun.Text = "mvidaldev@outlook.com";
        SetIconButtonContent(HelpButton, "?", "Open Help");
        SetIconButtonContent(CoffeeButton, "?", "Send a Coffee");
        HelpHintText.Text = "Open the help guide for setup and usage.";
        CoffeeHintText.Text = "Support the project if it helped your workflow.";
        OverviewTab.Header = "Overview";
        DisplaysTab.Header = "Displays";
        BehaviorTab.Header = "Behavior";
        DiagnosticsTab.Header = "Diagnostics";
        DisplaysTitleText.Text = "Display Mapping";
        DisplaysBodyText.Text = "Choose the main monitor and the touchscreen monitor.";
        DisplaysPrimaryLabel.Text = "Primary display";
        DisplaysTouchLabel.Text = "Touchscreen display";
        DisplaysTipsTitleText.Text = "Selection tips";
        DisplaysTipsBody1Text.Text = "Use the readable display names to identify the correct monitor.";
        DisplaysTipsBody2Text.Text = "If behavior looks wrong, check the touchscreen selection first.";
        BehaviorTitleText.Text = "Return behavior";
        BehaviorBodyText.Text = "Adjust how the pointer returns after touchscreen activity.";
        RestoreImmediatelyCheckBox.Content = "Restore the mouse immediately when touch ends";
        RestoreHelpText.Text = "Enable this option for immediate return.";
        AllowMouseOnTouchscreenCheckBox.Content = "Allow the mouse to enter the touchscreen area";
        AllowMouseOnTouchscreenHelpText.Text = "Enable this option to let the physical mouse move there.";
        StartWithWindowsCheckBox.Content = "Start MouseTool with Windows";
        StartWithWindowsHelpText.Text = "Enable this option to launch automatically.";
        DiagnosticsTitleText.Text = "Logging tools";
        DiagnosticsBodyText.Text = "Use these controls only when you need diagnostics.";
        EnableLogButton.Content = "Enable log";
        DisableLogButton.Content = "Disable log";
        OpenLogButton.Content = "Open log file";
        LogStatusText.Text = "Logging is disabled";
        StartButton.Content = "Start protection";
        StopButton.Content = "Pause protection";
        TrayButton.Content = "Minimize to tray";
        ConfigButton.Content = "Open config folder";
        ApplyButton.Content = "Apply changes";
        UpdateSaveState("Designer preview", false);
    }

    private void RefreshDisplayCombos(MouseKeeperConfig config)
    {
        var displays = _context?.GetDisplayOptions() ?? [];
        PrimaryDisplayComboBox.ItemsSource = displays;
        TouchDisplayComboBox.ItemsSource = displays;
        PrimaryDisplayComboBox.SelectedItem = displays.FirstOrDefault(item => item.DeviceName == config.PrimaryMonitorDeviceName);
        TouchDisplayComboBox.SelectedItem = displays.FirstOrDefault(item => item.DeviceName == config.TouchMonitorDeviceName);
    }

    private void RefreshLanguageCombo(string selectedLanguage)
    {
        var options = _context?.GetLanguageOptions() ?? [];
        LanguageComboBox.ItemsSource = options;
        LanguageComboBox.SelectedItem = options.FirstOrDefault(item => string.Equals(item.Code, selectedLanguage, StringComparison.OrdinalIgnoreCase))
            ?? options.FirstOrDefault(item => string.IsNullOrWhiteSpace(item.Code));
    }

    private string T(string key) => _context?.T(key) ?? key;

    private void SetBadgeState(bool running)
    {
        StatusBadgeText.Text = running ? "ACTIVE" : "PAUSED";
        StatusBadge.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(running ? "#DFF5E8" : "#FFEFD8"));
        StatusBadgeText.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(running ? "#167844" : "#A3590D"));
    }

    private static string NormalizeParagraphText(string value) => value.Replace("\r\n", "\n");

    private static void SetIconButtonContent(System.Windows.Controls.Button button, string iconText, string labelText)
    {
        var stack = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        stack.Children.Add(new TextBlock
        {
            Text = iconText,
            Margin = new Thickness(0, 0, 10, 0),
            FontSize = 16,
            VerticalAlignment = VerticalAlignment.Center
        });
        stack.Children.Add(new TextBlock
        {
            Text = labelText,
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeights.SemiBold
        });
        button.Content = stack;
    }

    private void UpdateSaveState(string text, bool hasPendingChanges, bool warning = false)
    {
        ApplyButton.IsEnabled = hasPendingChanges;
        SaveStatusText.Text = text;
        var color = warning || hasPendingChanges ? "#AE4D14" : "#1C7844";
        SaveStatusText.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
    }

    private void MarkPendingChanges(object sender, RoutedEventArgs e)
    {
        if (_isRefreshing)
        {
            return;
        }

        _hasPendingChanges = true;
        UpdateSaveState(T("SaveStatusPending"), true);
    }

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshing || _context is null || LanguageComboBox.SelectedItem is not LanguageOption language)
        {
            return;
        }

        if (_context.SetLanguageAndRequiresRestart(language.Code))
        {
            _context.ShowRestartRequiredMessage();
        }
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_context is null)
        {
            return;
        }

        if (PrimaryDisplayComboBox.SelectedItem is not DisplayOption primary || TouchDisplayComboBox.SelectedItem is not DisplayOption touch)
        {
            UpdateSaveState(T("SaveStatusSelectDisplays"), true, true);
            return;
        }

        _context.Config.RestoreImmediatelyOnTouchRelease = RestoreImmediatelyCheckBox.IsChecked == true;
        _context.Config.AllowMouseOnTouchscreen = AllowMouseOnTouchscreenCheckBox.IsChecked == true;
        _context.Config.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
        _context.UpdateSelectedDisplays(primary.DeviceName, touch.DeviceName);
        _hasPendingChanges = false;
        UpdateSaveState(T("SaveStatusSaved"), false);
        RefreshView();
    }

    private void StartButton_Click(object sender, RoutedEventArgs e) => _context?.StartProtection();
    private void StopButton_Click(object sender, RoutedEventArgs e) => _context?.StopProtection();
    private void TrayButton_Click(object sender, RoutedEventArgs e) => _context?.MinimizeMainFormToTray();
    private void ConfigButton_Click(object sender, RoutedEventArgs e) => _context?.OpenConfigFolder();
    private void EnableLogButton_Click(object sender, RoutedEventArgs e) => _context?.EnableLogging();
    private void DisableLogButton_Click(object sender, RoutedEventArgs e) => _context?.DisableLogging();
    private void OpenLogButton_Click(object sender, RoutedEventArgs e) => _context?.OpenLogFile();
    private void HelpButton_Click(object sender, RoutedEventArgs e) => _context?.OpenHelpFile();
    private void CoffeeButton_Click(object sender, RoutedEventArgs e) => _context?.OpenCoffeeLink();

    private void SupportEmailLink_Click(object sender, RoutedEventArgs e)
    {
        if (_context is null)
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "mailto:" + T("OverviewSupportEmail"),
            UseShellExecute = true
        });
    }
}
