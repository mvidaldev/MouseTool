using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace MouseTool;

internal sealed class MainForm : Form
{
    private readonly MouseKeeperApplicationContext _context;
    private readonly Label _statusBadge;
    private readonly Label _statusTitle;
    private readonly Label _statusBody;
    private readonly Label _anchorValue;
    private readonly Label _saveStatusLabel;
    private readonly Label _logStatusLabel;
    private readonly Label _gratitudeLabel;
    private readonly Label _supportTitleLabel;
    private readonly LinkLabel _supportEmailLink;
    private readonly Label _heroTitleLabel;
    private readonly Label _heroSubtitleLabel;
    private readonly Label _languageLabel;
    private readonly PictureBox _logoPictureBox;
    private readonly ComboBox _languageComboBox;
    private readonly ComboBox _primaryMonitorComboBox;
    private readonly ComboBox _touchMonitorComboBox;
    private readonly CheckBox _restoreImmediatelyCheckBox;
    private readonly CheckBox _allowMouseOnTouchscreenCheckBox;
    private readonly CheckBox _startWithWindowsCheckBox;
    private readonly Button _applyButton;
    private readonly Button _startButton;
    private readonly Button _stopButton;
    private readonly Button _trayButton;
    private readonly Button _configButton;
    private readonly Button _enableLogButton;
    private readonly Button _disableLogButton;
    private readonly Button _openLogButton;
    private readonly Button _helpButton;
    private readonly Button _coffeeButton;
    private readonly TabPage _overviewTab;
    private readonly TabPage _displaysTab;
    private readonly TabPage _behaviorTab;
    private readonly TabPage _diagnosticsTab;
    private bool _allowCloseWithoutPrompt;
    private bool _hasPendingChanges;

    public MainForm(MouseKeeperApplicationContext context)
    {
        _context = context;

        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1080, 760);
        MinimumSize = Size;
        MaximumSize = Size;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = Color.FromArgb(241, 244, 248);
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        Icon = BrandAssets.AppIcon;

        _statusBadge = new Label();
        _statusTitle = new Label();
        _statusBody = new Label();
        _anchorValue = new Label();
        _saveStatusLabel = new Label();
        _logStatusLabel = new Label();
        _gratitudeLabel = new Label();
        _supportTitleLabel = new Label();
        _supportEmailLink = new LinkLabel();
        _heroTitleLabel = new Label();
        _heroSubtitleLabel = new Label();
        _languageLabel = new Label();
        _logoPictureBox = new PictureBox();
        _languageComboBox = new ComboBox();
        _primaryMonitorComboBox = new ComboBox();
        _touchMonitorComboBox = new ComboBox();
        _restoreImmediatelyCheckBox = new CheckBox();
        _allowMouseOnTouchscreenCheckBox = new CheckBox();
        _startWithWindowsCheckBox = new CheckBox();
        _applyButton = new Button();
        _startButton = new Button();
        _stopButton = new Button();
        _trayButton = new Button();
        _configButton = new Button();
        _enableLogButton = new Button();
        _disableLogButton = new Button();
        _openLogButton = new Button();
        _helpButton = new Button();
        _coffeeButton = new Button();
        _overviewTab = CreateTabPage(string.Empty);
        _displaysTab = CreateTabPage(string.Empty);
        _behaviorTab = CreateTabPage(string.Empty);
        _diagnosticsTab = CreateTabPage(string.Empty);

        BuildLayout();
        RefreshView();
    }

    public void RefreshView()
    {
        var running = _context.IsRunning;
        var config = _context.Config;

        Text = _context.T("AppName");
        _heroTitleLabel.Text = _context.T("HeroTitle");
        _heroSubtitleLabel.Text = _context.T("HeroSubtitle");
        _languageLabel.Text = _context.T("LanguageLabel");

        _statusBadge.Text = running ? "ACTIVE" : "PAUSED";
        _statusBadge.BackColor = running ? Color.FromArgb(223, 245, 232) : Color.FromArgb(255, 239, 221);
        _statusBadge.ForeColor = running ? Color.FromArgb(22, 120, 68) : Color.FromArgb(163, 89, 13);

        _statusTitle.Text = running ? _context.T("OverviewStatusActive") : _context.T("OverviewStatusPaused");
        _statusBody.Text = _context.StatusText;
        _anchorValue.Text = string.Format(_context.T("OverviewAnchorValue"), config.LastPrimaryMousePosition.X, config.LastPrimaryMousePosition.Y);
        _supportTitleLabel.Text = _context.T("OverviewSupportTitle");
        _gratitudeLabel.Text = _context.T("OverviewGratitudeMessage");
        _supportEmailLink.Text = _context.T("OverviewSupportEmail");
        _helpButton.Text = _context.T("HelpButton");
        _coffeeButton.Text = _context.T("CoffeeButton");
        _logStatusLabel.Text = _context.LoggingEnabled ? _context.T("DiagnosticsLoggingEnabled") : _context.T("DiagnosticsLoggingDisabled");
        _logStatusLabel.ForeColor = _context.LoggingEnabled ? Color.FromArgb(28, 120, 68) : Color.FromArgb(120, 126, 137);
        _overviewTab.Text = _context.T("TabOverview");
        _displaysTab.Text = _context.T("TabDisplays");
        _behaviorTab.Text = _context.T("TabBehavior");
        _diagnosticsTab.Text = _context.T("TabDiagnostics");

        ResizeActionButton(_helpButton, 250);
        ResizeActionButton(_coffeeButton, 250);

        RefreshDisplayCombos(config);
        RefreshLanguageCombo(config.SelectedLanguage);

        _restoreImmediatelyCheckBox.CheckedChanged -= MarkPendingChanges;
        _restoreImmediatelyCheckBox.Checked = config.RestoreImmediatelyOnTouchRelease;
        _restoreImmediatelyCheckBox.CheckedChanged += MarkPendingChanges;
        _allowMouseOnTouchscreenCheckBox.CheckedChanged -= MarkPendingChanges;
        _allowMouseOnTouchscreenCheckBox.Checked = config.AllowMouseOnTouchscreen;
        _allowMouseOnTouchscreenCheckBox.CheckedChanged += MarkPendingChanges;
        _startWithWindowsCheckBox.CheckedChanged -= MarkPendingChanges;
        _startWithWindowsCheckBox.Checked = config.StartWithWindows;
        _startWithWindowsCheckBox.CheckedChanged += MarkPendingChanges;

        _startButton.Enabled = !running;
        _stopButton.Enabled = running;
        _enableLogButton.Enabled = !_context.LoggingEnabled;
        _disableLogButton.Enabled = _context.LoggingEnabled;
        UpdateSaveState(_hasPendingChanges ? _context.T("SaveStatusPending") : _context.T("SaveStatusCurrent"), _hasPendingChanges);
    }

    public void AllowCloseWithoutPrompt()
    {
        _allowCloseWithoutPrompt = true;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_allowCloseWithoutPrompt)
        {
            base.OnFormClosing(e);
            return;
        }

        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;

            using var dialog = new CloseChoiceForm(_context);
            var result = dialog.ShowDialog(this);
            if (result == DialogResult.Yes)
            {
                _context.MinimizeMainFormToTray();
            }
            else if (result == DialogResult.No)
            {
                _context.StopProtection(showBalloon: false, persistEnabled: false);
                _context.ExitApplication();
            }

            return;
        }

        base.OnFormClosing(e);
    }

    private void BuildLayout()
    {
        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24),
            ColumnCount = 1,
            RowCount = 3
        };
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 170F));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));

        shell.Controls.Add(BuildHeroPanel(), 0, 0);
        shell.Controls.Add(BuildTabs(), 0, 1);
        shell.Controls.Add(BuildFooter(), 0, 2);

        Controls.Add(shell);
    }

    private Control BuildHeroPanel()
    {
        var hero = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(18, 43, 74),
            Padding = new Padding(28)
        };

        _logoPictureBox.Image = BrandAssets.LogoImage;
        _logoPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        _logoPictureBox.Location = new Point(28, 28);
        _logoPictureBox.Size = new Size(72, 72);

        _statusBadge.AutoSize = true;
        _statusBadge.Padding = new Padding(12, 6, 12, 6);
        _statusBadge.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        _statusBadge.Location = new Point(118, 24);

        _heroTitleLabel.AutoSize = true;
        _heroTitleLabel.Font = new Font("Segoe UI Semibold", 22F, FontStyle.Bold);
        _heroTitleLabel.ForeColor = Color.White;
        _heroTitleLabel.Location = new Point(118, 56);

        _heroSubtitleLabel.AutoSize = false;
        _heroSubtitleLabel.Size = new Size(640, 52);
        _heroSubtitleLabel.Location = new Point(120, 96);
        _heroSubtitleLabel.ForeColor = Color.FromArgb(216, 226, 238);
        _heroSubtitleLabel.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);

        _languageLabel.AutoSize = true;
        _languageLabel.ForeColor = Color.FromArgb(216, 226, 238);
        _languageLabel.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        _languageLabel.Location = new Point(800, 34);

        ConfigureLanguageCombo();
        _languageComboBox.Location = new Point(800, 58);
        _languageComboBox.Size = new Size(220, 32);

        hero.Controls.Add(_logoPictureBox);
        hero.Controls.Add(_statusBadge);
        hero.Controls.Add(_heroTitleLabel);
        hero.Controls.Add(_heroSubtitleLabel);
        hero.Controls.Add(_languageLabel);
        hero.Controls.Add(_languageComboBox);
        return hero;
    }

    private Control BuildTabs()
    {
        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Appearance = TabAppearance.Normal,
            Padding = new Point(18, 8),
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold)
        };

        BuildOverviewTab();
        BuildDisplaysTab();
        BuildBehaviorTab();
        BuildDiagnosticsTab();
        tabs.TabPages.Add(_overviewTab);
        tabs.TabPages.Add(_displaysTab);
        tabs.TabPages.Add(_behaviorTab);
        tabs.TabPages.Add(_diagnosticsTab);
        return tabs;
    }

    private void BuildOverviewTab()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 67F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));

        var leftColumn = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 0, 12, 0)
        };

        var leftScroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true
        };

        var contentStack = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 4,
            Margin = new Padding(0)
        };
        contentStack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        var statusCard = CreateOverviewCard(Color.FromArgb(21, 83, 144), 162);
        var supportCard = CreateCardPanel();
        supportCard.Dock = DockStyle.Fill;
        supportCard.BackColor = Color.FromArgb(247, 250, 253);
        supportCard.Padding = new Padding(0);

        var supportContent = new Panel
        {
            Size = new Size(278, 486),
            Anchor = AnchorStyles.None
        };

        _supportTitleLabel.AutoSize = true;
        _supportTitleLabel.Location = new Point(0, 10);
        _supportTitleLabel.Font = new Font("Segoe UI Semibold", 11.5F, FontStyle.Bold);
        _supportTitleLabel.ForeColor = Color.FromArgb(21, 51, 91);
        _supportTitleLabel.Text = _context.T("OverviewSupportTitle");

        _gratitudeLabel.AutoSize = false;
        _gratitudeLabel.Size = new Size(250, 92);
        _gratitudeLabel.Location = new Point(0, 44);
        _gratitudeLabel.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular);
        _gratitudeLabel.ForeColor = Color.FromArgb(88, 100, 118);
        _gratitudeLabel.Text = _context.T("OverviewGratitudeMessage");

        var supportNoteLabel = new Label
        {
            AutoSize = false,
            Location = new Point(0, 140),
            Size = new Size(250, 42),
            Font = new Font("Segoe UI", 9.25F, FontStyle.Regular),
            ForeColor = Color.FromArgb(88, 100, 118),
            Text = _context.T("OverviewSupportNote")
        };

        _supportEmailLink.AutoSize = true;
        _supportEmailLink.Location = new Point(0, 190);
        _supportEmailLink.Font = new Font("Segoe UI Semibold", 9.75F, FontStyle.Bold);
        _supportEmailLink.LinkColor = Color.FromArgb(21, 83, 144);
        _supportEmailLink.ActiveLinkColor = Color.FromArgb(15, 63, 109);
        _supportEmailLink.VisitedLinkColor = Color.FromArgb(21, 83, 144);
        _supportEmailLink.Text = _context.T("OverviewSupportEmail");
        _supportEmailLink.LinkClicked += (_, _) =>
        {
            var supportEmail = _context.T("OverviewSupportEmail");
            Process.Start(new ProcessStartInfo
            {
                FileName = "mailto:" + supportEmail,
                UseShellExecute = true
            });
        };

        _helpButton.Text = _context.T("HelpButton");
        ConfigureActionButton(_helpButton, _context.T("HelpButton"), Color.White, Color.FromArgb(21, 51, 91), new Point(0, 252), 250);
        _helpButton.Size = new Size(250, 42);
        _helpButton.Image = BrandAssets.HelpButtonIcon;
        _helpButton.ImageAlign = ContentAlignment.MiddleLeft;
        _helpButton.TextImageRelation = TextImageRelation.ImageBeforeText;
        _helpButton.Padding = new Padding(12, 0, 12, 0);
        _helpButton.Click += (_, _) => _context.OpenHelpFile();

        var helpHintLabel = new Label
        {
            AutoSize = false,
            Location = new Point(0, 302),
            Size = new Size(250, 38),
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = Color.FromArgb(88, 100, 118),
            Text = _context.T("OverviewHelpHint")
        };

        _coffeeButton.Text = _context.T("CoffeeButton");
        ConfigureActionButton(_coffeeButton, _context.T("CoffeeButton"), Color.FromArgb(255, 224, 179), Color.FromArgb(106, 63, 0), new Point(0, 362), 250);
        _coffeeButton.Size = new Size(250, 42);
        _coffeeButton.Image = BrandAssets.CoffeeButtonIcon;
        _coffeeButton.ImageAlign = ContentAlignment.MiddleLeft;
        _coffeeButton.TextImageRelation = TextImageRelation.ImageBeforeText;
        _coffeeButton.Padding = new Padding(12, 0, 12, 0);
        _coffeeButton.Click += (_, _) => _context.OpenCoffeeLink();

        var coffeeHintLabel = new Label
        {
            AutoSize = false,
            Location = new Point(0, 412),
            Size = new Size(250, 54),
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = Color.FromArgb(88, 100, 118),
            Text = _context.T("OverviewCoffeeHint")
        };

        CenterControlHorizontally(supportContent, _supportTitleLabel);
        CenterControlHorizontally(supportContent, _supportEmailLink);

        supportContent.Controls.Add(_supportTitleLabel);
        supportContent.Controls.Add(_gratitudeLabel);
        supportContent.Controls.Add(supportNoteLabel);
        supportContent.Controls.Add(_supportEmailLink);
        supportContent.Controls.Add(_helpButton);
        supportContent.Controls.Add(helpHintLabel);
        supportContent.Controls.Add(_coffeeButton);
        supportContent.Controls.Add(coffeeHintLabel);
        supportCard.Controls.Add(supportContent);
        supportCard.Resize += (_, _) =>
        {
            supportContent.Left = Math.Max(0, (supportCard.ClientSize.Width - supportContent.Width) / 2);
            supportContent.Top = Math.Max(0, (supportCard.ClientSize.Height - supportContent.Height) / 2);
            CenterControlHorizontally(supportContent, _supportTitleLabel);
            CenterControlHorizontally(supportContent, _supportEmailLink);
        };

        _statusTitle.AutoSize = true;
        _statusTitle.Font = new Font("Segoe UI Semibold", 14F, FontStyle.Bold);
        _statusTitle.ForeColor = Color.FromArgb(33, 45, 61);
        _statusTitle.Location = new Point(24, 22);

        _statusBody.AutoSize = false;
        _statusBody.Size = new Size(560, 44);
        _statusBody.Location = new Point(24, 58);
        _statusBody.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
        _statusBody.ForeColor = Color.FromArgb(88, 100, 118);

        _anchorValue.AutoSize = true;
        _anchorValue.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        _anchorValue.ForeColor = Color.FromArgb(21, 83, 144);
        _anchorValue.Location = new Point(24, 114);

        statusCard.Controls.Add(_statusTitle);
        statusCard.Controls.Add(_statusBody);
        statusCard.Controls.Add(_anchorValue);

        contentStack.Controls.Add(statusCard, 0, 0);
        contentStack.Controls.Add(CreateOverviewTextCard(_context.T("OverviewHowTitle"), _context.T("OverviewHowBody"), Color.FromArgb(21, 83, 144), 176), 0, 1);
        contentStack.Controls.Add(CreateOverviewStepsCard(_context.T("OverviewWorkflowTitle"), SplitLocalizedLines(_context.T("OverviewWorkflowBody")), Color.FromArgb(28, 120, 68)), 0, 2);
        contentStack.Controls.Add(CreateOverviewStepsCard(_context.T("OverviewTipsTitle"), SplitLocalizedLines(_context.T("OverviewTipsBody")), Color.FromArgb(183, 111, 0)), 0, 3);

        leftScroll.Controls.Add(contentStack);
        leftColumn.Controls.Add(leftScroll);
        layout.Controls.Add(leftColumn, 0, 0);
        layout.Controls.Add(supportCard, 1, 0);
        _overviewTab.Controls.Add(layout);
    }

    private void BuildDisplaysTab()
    {
        _displaysTab.AutoScroll = true;
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 250F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var mappingCard = CreateCardPanel();
        mappingCard.Dock = DockStyle.Fill;

        var mappingTitle = CreateSectionTitle(_context.T("DisplaysTitle"), 24, 22);
        var mappingBody = CreateSectionBody(_context.T("DisplaysBody"), 24, 56, 900, 42);

        ConfigureDisplayCombo(_primaryMonitorComboBox);
        ConfigureDisplayCombo(_touchMonitorComboBox);
        _primaryMonitorComboBox.SelectedIndexChanged += MarkPendingChanges;
        _touchMonitorComboBox.SelectedIndexChanged += MarkPendingChanges;

        mappingCard.Controls.Add(mappingTitle);
        mappingCard.Controls.Add(mappingBody);
        mappingCard.Controls.Add(CreateComboField(_context.T("DisplaysPrimary"), _primaryMonitorComboBox, 24, 116));
        mappingCard.Controls.Add(CreateComboField(_context.T("DisplaysTouch"), _touchMonitorComboBox, 24, 184));

        var notesCard = CreateCardPanel();
        notesCard.Dock = DockStyle.Fill;
        notesCard.Controls.Add(CreateSectionTitle(_context.T("DisplaysTipsTitle"), 24, 22));
        notesCard.Controls.Add(CreateSectionBody(_context.T("DisplaysTipsBody1"), 24, 56, 900, 60));
        notesCard.Controls.Add(CreateSectionBody(_context.T("DisplaysTipsBody2"), 24, 126, 900, 60));

        layout.Controls.Add(mappingCard, 0, 0);
        layout.Controls.Add(notesCard, 0, 1);
        _displaysTab.Controls.Add(layout);
    }

    private void BuildBehaviorTab()
    {
        _behaviorTab.AutoScroll = true;
        var card = CreateCardPanel();
        card.Dock = DockStyle.Top;
        card.Height = 470;

        card.Controls.Add(CreateSectionTitle(_context.T("BehaviorTitle"), 24, 22));
        card.Controls.Add(CreateSectionBody(_context.T("BehaviorBody"), 24, 56, 900, 42));

        _restoreImmediatelyCheckBox.Text = _context.T("BehaviorRestoreNow");
        _restoreImmediatelyCheckBox.AutoSize = true;
        _restoreImmediatelyCheckBox.Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold);
        _restoreImmediatelyCheckBox.ForeColor = Color.FromArgb(40, 52, 67);
        _restoreImmediatelyCheckBox.Location = new Point(24, 120);
        _restoreImmediatelyCheckBox.CheckedChanged += MarkPendingChanges;

        var helper = CreateSectionBody(_context.T("BehaviorRestoreHelp"), 46, 150, 860, 46);
        _allowMouseOnTouchscreenCheckBox.Text = _context.T("BehaviorAllowMouseTouchscreen");
        _allowMouseOnTouchscreenCheckBox.AutoSize = true;
        _allowMouseOnTouchscreenCheckBox.Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold);
        _allowMouseOnTouchscreenCheckBox.ForeColor = Color.FromArgb(40, 52, 67);
        _allowMouseOnTouchscreenCheckBox.Location = new Point(24, 220);
        _allowMouseOnTouchscreenCheckBox.CheckedChanged += MarkPendingChanges;

        var allowMouseHelper = CreateSectionBody(_context.T("BehaviorAllowMouseTouchscreenHelp"), 46, 250, 860, 46);
        _startWithWindowsCheckBox.Text = _context.T("BehaviorStartWithWindows");
        _startWithWindowsCheckBox.AutoSize = true;
        _startWithWindowsCheckBox.Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold);
        _startWithWindowsCheckBox.ForeColor = Color.FromArgb(40, 52, 67);
        _startWithWindowsCheckBox.Location = new Point(24, 320);
        _startWithWindowsCheckBox.CheckedChanged += MarkPendingChanges;

        var startupHelper = CreateSectionBody(_context.T("BehaviorStartWithWindowsHelp"), 46, 350, 860, 46);
        card.Controls.Add(_restoreImmediatelyCheckBox);
        card.Controls.Add(helper);
        card.Controls.Add(_allowMouseOnTouchscreenCheckBox);
        card.Controls.Add(allowMouseHelper);
        card.Controls.Add(_startWithWindowsCheckBox);
        card.Controls.Add(startupHelper);
        _behaviorTab.Controls.Add(card);
    }

    private void BuildDiagnosticsTab()
    {
        var card = CreateCardPanel();
        card.Dock = DockStyle.Fill;

        card.Controls.Add(CreateSectionTitle(_context.T("DiagnosticsTitle"), 24, 22));
        card.Controls.Add(CreateSectionBody(_context.T("DiagnosticsBody"), 24, 56, 900, 44));

        ConfigureActionButton(_enableLogButton, _context.T("DiagnosticsEnableLog"), Color.FromArgb(21, 83, 144), Color.White, new Point(24, 122));
        _enableLogButton.Click += (_, _) =>
        {
            _context.EnableLogging();
            RefreshView();
        };

        ConfigureActionButton(_disableLogButton, _context.T("DiagnosticsDisableLog"), Color.FromArgb(232, 236, 241), Color.FromArgb(40, 51, 67), new Point(204, 122));
        _disableLogButton.Click += (_, _) =>
        {
            _context.DisableLogging();
            RefreshView();
        };

        ConfigureActionButton(_openLogButton, _context.T("DiagnosticsOpenLog"), Color.White, Color.FromArgb(21, 51, 91), new Point(384, 122));
        _openLogButton.Click += (_, _) => _context.OpenLogFile();

        _logStatusLabel.AutoSize = true;
        _logStatusLabel.Location = new Point(24, 178);
        _logStatusLabel.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);

        card.Controls.Add(_enableLogButton);
        card.Controls.Add(_disableLogButton);
        card.Controls.Add(_openLogButton);
        card.Controls.Add(_logStatusLabel);
        _diagnosticsTab.Controls.Add(card);
    }

    private Control BuildFooter()
    {
        var footer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(18, 18, 18, 18)
        };

        var buttonsPanel = new FlowLayoutPanel
        {
            Location = new Point(18, 18),
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight
        };

        ConfigureActionButton(_startButton, _context.T("FooterStart"), Color.FromArgb(26, 124, 72), Color.White, Point.Empty, 170);
        _startButton.Click += (_, _) => _context.StartProtection();

        ConfigureActionButton(_stopButton, _context.T("FooterPause"), Color.FromArgb(232, 236, 241), Color.FromArgb(40, 51, 67), Point.Empty, 170);
        _stopButton.Click += (_, _) => _context.StopProtection();

        ConfigureActionButton(_trayButton, _context.T("FooterTray"), Color.White, Color.FromArgb(21, 51, 91), Point.Empty, 180);
        _trayButton.Click += (_, _) => _context.MinimizeMainFormToTray();

        ConfigureActionButton(_configButton, _context.T("FooterConfig"), Color.White, Color.FromArgb(21, 51, 91), Point.Empty, 200);
        _configButton.Click += (_, _) => _context.OpenConfigFolder();

        ConfigureActionButton(_applyButton, _context.T("FooterApply"), Color.FromArgb(21, 83, 144), Color.White, Point.Empty, 180);
        _applyButton.Click += (_, _) => ApplyChanges();

        buttonsPanel.Controls.Add(_startButton);
        buttonsPanel.Controls.Add(_stopButton);
        buttonsPanel.Controls.Add(_trayButton);
        buttonsPanel.Controls.Add(_configButton);
        buttonsPanel.Controls.Add(_applyButton);

        _saveStatusLabel.AutoSize = true;
        _saveStatusLabel.Location = new Point(22, 66);
        _saveStatusLabel.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);

        footer.Controls.Add(buttonsPanel);
        footer.Controls.Add(_saveStatusLabel);
        return footer;
    }

    private void ApplyChanges()
    {
        if (_primaryMonitorComboBox.SelectedItem is not DisplayOption primary ||
            _touchMonitorComboBox.SelectedItem is not DisplayOption touch)
        {
            UpdateSaveState(_context.T("SaveStatusSelectDisplays"), true, true);
            return;
        }

        _context.Config.RestoreImmediatelyOnTouchRelease = _restoreImmediatelyCheckBox.Checked;
        _context.Config.AllowMouseOnTouchscreen = _allowMouseOnTouchscreenCheckBox.Checked;
        _context.Config.StartWithWindows = _startWithWindowsCheckBox.Checked;
        _context.UpdateSelectedDisplays(primary.DeviceName, touch.DeviceName);
        _hasPendingChanges = false;
        UpdateSaveState(_context.T("SaveStatusSaved"), false);
        RefreshView();
    }

    private void MarkPendingChanges(object? sender, EventArgs e)
    {
        _hasPendingChanges = true;
        UpdateSaveState(_context.T("SaveStatusPending"), true);
    }

    private void LanguageChanged(object? sender, EventArgs e)
    {
        if (_languageComboBox.SelectedItem is LanguageOption language)
        {
            if (_context.SetLanguageAndRequiresRestart(language.Code))
        {
            _context.ShowRestartRequiredMessage();
        }
        }
    }

    private void UpdateSaveState(string text, bool hasPendingChanges, bool warning = false)
    {
        _applyButton.Enabled = hasPendingChanges;
        _saveStatusLabel.Text = text;
        _saveStatusLabel.ForeColor = warning
            ? Color.FromArgb(174, 77, 20)
            : hasPendingChanges
                ? Color.FromArgb(174, 77, 20)
                : Color.FromArgb(28, 120, 68);
    }

    private static TabPage CreateTabPage(string title)
    {
        return new TabPage(title)
        {
            BackColor = Color.FromArgb(241, 244, 248),
            Padding = new Padding(14)
        };
    }

    private static Panel CreateCardPanel()
    {
        return new Panel
        {
            BackColor = Color.White,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
    }

    private static Label CreateSectionTitle(string text, int x, int y)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Location = new Point(x, y),
            Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold),
            ForeColor = Color.FromArgb(33, 45, 61)
        };
    }

    private static Label CreateSectionBody(string text, int x, int y, int width, int height)
    {
        return new Label
        {
            Text = text,
            AutoSize = false,
            Location = new Point(x, y),
            Size = new Size(width, height),
            Font = new Font("Segoe UI", 10F, FontStyle.Regular),
            ForeColor = Color.FromArgb(88, 100, 118)
        };
    }

    private static Panel CreateOverviewCard(Color accentColor, int height)
    {
        var panel = CreateCardPanel();
        panel.Dock = DockStyle.Top;
        panel.Height = height;
        panel.Margin = new Padding(0, 0, 0, 16);

        var accent = new Panel
        {
            Dock = DockStyle.Top,
            Height = 4,
            BackColor = accentColor
        };

        panel.Controls.Add(accent);
        return panel;
    }

    private static Panel CreateOverviewTextCard(string title, string body, Color accentColor, int height)
    {
        var card = CreateOverviewCard(accentColor, height);
        card.Controls.Add(CreateSectionTitle(title, 24, 22));
        card.Controls.Add(CreateSectionBody(body, 24, 58, 560, height - 84));
        return card;
    }

    private static Panel CreateOverviewStepsCard(string title, IReadOnlyList<string> steps, Color accentColor)
    {
        var cardHeight = 84 + (steps.Count * 68);
        var card = CreateOverviewCard(accentColor, cardHeight);
        card.Controls.Add(CreateSectionTitle(title, 24, 22));

        var y = 62;
        for (var i = 0; i < steps.Count; i++)
        {
            var badge = new Label
            {
                Text = (i + 1).ToString(),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(24, y + 2),
                Size = new Size(28, 28),
                BackColor = accentColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold)
            };

            var body = new Label
            {
                Text = steps[i],
                AutoSize = false,
                Location = new Point(66, y),
                Size = new Size(520, 40),
                Font = new Font("Segoe UI", 9.75F, FontStyle.Regular),
                ForeColor = Color.FromArgb(88, 100, 118)
            };

            card.Controls.Add(badge);
            card.Controls.Add(body);
            y += 58;
        }

        return card;
    }

    private static IReadOnlyList<string> SplitLocalizedLines(string text)
    {
        return text
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
    }

    private static void CenterControlHorizontally(Control container, Control control)
    {
        control.Left = Math.Max(0, (container.ClientSize.Width - control.Width) / 2);
    }

    private Control CreateComboField(string labelText, ComboBox comboBox, int x, int y)
    {
        var panel = new Panel
        {
            Location = new Point(x, y),
            Size = new Size(900, 54)
        };

        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            Location = new Point(0, 0),
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(40, 52, 67)
        };

        comboBox.Location = new Point(0, 22);
        comboBox.Size = new Size(520, 32);

        panel.Controls.Add(label);
        panel.Controls.Add(comboBox);
        return panel;
    }

    private void ConfigureDisplayCombo(ComboBox comboBox)
    {
        comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        comboBox.FlatStyle = FlatStyle.Flat;
        comboBox.BackColor = Color.White;
        comboBox.ForeColor = Color.FromArgb(38, 48, 63);
        comboBox.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
        comboBox.DisplayMember = nameof(DisplayOption.DisplayName);
        comboBox.ValueMember = nameof(DisplayOption.DeviceName);
    }

    private void ConfigureLanguageCombo()
    {
        _languageComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _languageComboBox.FlatStyle = FlatStyle.Flat;
        _languageComboBox.BackColor = Color.White;
        _languageComboBox.ForeColor = Color.FromArgb(38, 48, 63);
        _languageComboBox.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
        _languageComboBox.DisplayMember = nameof(LanguageOption.DisplayName);
        _languageComboBox.ValueMember = nameof(LanguageOption.Code);
        _languageComboBox.SelectedIndexChanged += LanguageChanged;
    }

    private void RefreshDisplayCombos(MouseKeeperConfig config)
    {
        var options = _context.GetDisplayOptions().ToList();

        _primaryMonitorComboBox.SelectedIndexChanged -= MarkPendingChanges;
        _touchMonitorComboBox.SelectedIndexChanged -= MarkPendingChanges;

        _primaryMonitorComboBox.DataSource = options.ToList();
        _touchMonitorComboBox.DataSource = options.ToList();

        _primaryMonitorComboBox.SelectedItem = options.FirstOrDefault(o => o.DeviceName == config.PrimaryMonitorDeviceName);
        _touchMonitorComboBox.SelectedItem = options.FirstOrDefault(o => o.DeviceName == config.TouchMonitorDeviceName);

        _primaryMonitorComboBox.SelectedIndexChanged += MarkPendingChanges;
        _touchMonitorComboBox.SelectedIndexChanged += MarkPendingChanges;
    }

    private void RefreshLanguageCombo(string selectedLanguage)
    {
        var options = _context.GetLanguageOptions().ToList();
        _languageComboBox.SelectedIndexChanged -= LanguageChanged;
        _languageComboBox.DataSource = options;
        _languageComboBox.SelectedItem = options.FirstOrDefault(o => string.Equals(o.Code, selectedLanguage, StringComparison.OrdinalIgnoreCase))
            ?? options.First();
        _languageComboBox.SelectedIndexChanged += LanguageChanged;
    }

    private static void ConfigureActionButton(Button button, string text, Color backColor, Color foreColor, Point location, int minimumWidth = 160)
    {
        button.Text = text;
        ResizeActionButton(button, minimumWidth);
        button.Height = 40;
        button.Location = location;
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = backColor;
        button.ForeColor = foreColor;
        button.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
    }

    private static void ResizeActionButton(Button button, int minimumWidth)
    {
        var measured = TextRenderer.MeasureText(button.Text, new Font("Segoe UI Semibold", 10F, FontStyle.Bold));
        var width = Math.Max(minimumWidth, measured.Width + 28);
        button.Width = width;
    }
}

internal sealed class CloseChoiceForm : Form
{
    public CloseChoiceForm(MouseKeeperApplicationContext context)
    {
        Text = context.T("CloseDialogTitle");
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(460, 220);
        BackColor = Color.White;
        Font = new Font("Segoe UI", 10F, FontStyle.Regular);

        var title = new Label
        {
            Text = context.T("CloseDialogQuestion"),
            Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(24, 22)
        };

        var body = new Label
        {
            Text = context.IsRunning ? context.T("CloseDialogBodyRunning") : context.T("CloseDialogBodyPaused"),
            AutoSize = false,
            Size = new Size(406, 54),
            Location = new Point(24, 58),
            ForeColor = Color.FromArgb(87, 96, 112)
        };

        var trayButton = new Button
        {
            Text = context.T("CloseDialogTray"),
            DialogResult = DialogResult.Yes,
            Size = new Size(132, 40),
            Location = new Point(24, 146),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(21, 51, 91),
            ForeColor = Color.White
        };

        var exitButton = new Button
        {
            Text = context.T("CloseDialogExit"),
            DialogResult = DialogResult.No,
            Size = new Size(132, 40),
            Location = new Point(168, 146),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(236, 239, 244),
            ForeColor = Color.FromArgb(37, 47, 63)
        };

        var cancelButton = new Button
        {
            Text = context.T("CloseDialogCancel"),
            DialogResult = DialogResult.Cancel,
            Size = new Size(100, 40),
            Location = new Point(312, 146),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(37, 47, 63)
        };

        Controls.Add(title);
        Controls.Add(body);
        Controls.Add(trayButton);
        Controls.Add(exitButton);
        Controls.Add(cancelButton);

        AcceptButton = trayButton;
        CancelButton = cancelButton;
    }
}



