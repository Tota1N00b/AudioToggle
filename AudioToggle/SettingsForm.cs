using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace AudioToggle;

internal sealed class SettingsForm : Form
{
    private const int ScrollbarWidth = 6;
    private const int ScrollbarEdgeInset = 1;
    private const int ScrollbarListGap = 12;
    private const int CardSafetyInset = 6;
    private const int ActionRightInset = ScrollbarWidth + ScrollbarListGap + ScrollbarEdgeInset;

    private readonly Label _titleLabel = new();
    private readonly Label _statusLabel = new();
    private readonly Label _subtitleLabel = new();
    private readonly FlowLayoutPanel _selectedDevicesPanel = new();
    private readonly Panel _selectedDevicesHost = new();
    private readonly Panel _statusLeftHost = new();
    private readonly DoubleBufferedFlowLayoutPanel _deviceListPanel = new();
    private readonly Panel _deviceListHost = new();
    private readonly SlimScrollBar _deviceScrollBar = new();
    private readonly FluentButton _refreshButton = new();
    private readonly FluentButton _clearButton = new();
    private readonly FluentButton _toggleButton = new();
    private readonly CheckBox _openOnStartupCheckBox = new();
    private readonly TableLayoutPanel _rootLayout = new();

    private ThemePalette _theme;
    private bool _isBindingStartupState;

    public SettingsForm(ThemePalette theme)
    {
        _theme = theme;
        Text = "Audio Toggle";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 520);
        ClientSize = new Size(920, 640);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        BackColor = _theme.WindowBackground;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

        _rootLayout.Dock = DockStyle.Fill;
        _rootLayout.ColumnCount = 1;
        _rootLayout.RowCount = 3;
        _rootLayout.Padding = new Padding(24);
        _rootLayout.BackColor = BackColor;
        _rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _rootLayout.Controls.Add(BuildHeader(), 0, 0);
        _rootLayout.Controls.Add(BuildListPanel(), 0, 1);
        _rootLayout.Controls.Add(BuildFooter(), 0, 2);

        Controls.Add(_rootLayout);

        SizeChanged += (_, _) =>
        {
            ResizeCards();
            UpdateSelectedTagLayout();
        };
        _deviceListHost.Resize += (_, _) => ResizeCards();
        _statusLeftHost.Resize += (_, _) => UpdateSelectedTagLayout();
        HandleCreated += (_, _) => ThemeInterop.ApplyWindowTheme(this, _theme);

        ApplyTheme(_theme);
    }

    public event EventHandler? RefreshRequested;

    public event EventHandler? ToggleNowRequested;

    public event EventHandler? ClearSelectionRequested;

    public event EventHandler<string>? DeviceSelectionToggled;

    public event EventHandler<bool>? StartupPreferenceChanged;

    public void ApplyTheme(ThemePalette theme)
    {
        _theme = theme;
        BackColor = _theme.WindowBackground;
        ForeColor = _theme.TextPrimary;
        ApplyContainerBackColor(this);

        _titleLabel.ForeColor = _theme.TextPrimary;
        _subtitleLabel.ForeColor = _theme.TextSecondary;
        _statusLabel.ForeColor = ResolveStatusColor(_statusLabel.Text);

        _openOnStartupCheckBox.BackColor = _theme.WindowBackground;
        _openOnStartupCheckBox.ForeColor = _theme.TextPrimary;

        _refreshButton.Theme = _theme;
        _clearButton.Theme = _theme;
        _toggleButton.Theme = _theme;
        _toggleButton.IsPrimary = true;
        _deviceScrollBar.Theme = _theme;

        foreach (var tag in _selectedDevicesPanel.Controls.OfType<StatusTagLabel>())
        {
            tag.Theme = _theme;
        }

        foreach (var card in _deviceListPanel.Controls.OfType<DeviceCardControl>())
        {
            card.Theme = _theme;
        }

        ThemeInterop.ApplyWindowTheme(this, _theme);
        Invalidate(true);
    }

    public void BindDevices(IEnumerable<AudioDeviceInfo> devices, IReadOnlyList<string> selectedDeviceIds, string statusMessage, bool startupEnabled)
    {
        var selectedSet = selectedDeviceIds.ToHashSet(StringComparer.Ordinal);
        var selectedIdsInOrder = selectedDeviceIds.ToList();
        var selectionLocked = selectedDeviceIds.Count >= 2;
        var cards = new List<DeviceCardModel>();
        var selectedDeviceNames = new List<string>();

        foreach (var device in devices)
        {
            var (title, subtitle) = DeviceNameParser.Split(device.FriendlyName);
            if (selectedSet.Contains(device.Id))
            {
                selectedDeviceNames.Add(device.FriendlyName);
            }

            cards.Add(new DeviceCardModel
            {
                Id = device.Id,
                Title = title,
                Subtitle = subtitle,
                IsSelected = selectedSet.Contains(device.Id),
                IsCurrentOutput = device.IsCurrentOutput,
                IsMissing = false,
                IsDisabled = selectionLocked && !selectedSet.Contains(device.Id)
            });
        }

        foreach (var missingId in selectedIdsInOrder.Where(id => cards.All(card => !string.Equals(card.Id, id, StringComparison.Ordinal))))
        {
            selectedDeviceNames.Add($"Missing: {ShortenId(missingId)}");
            cards.Add(new DeviceCardModel
            {
                Id = missingId,
                Title = "Saved device unavailable",
                Subtitle = ShortenId(missingId),
                IsSelected = true,
                IsCurrentOutput = false,
                IsMissing = true,
                IsDisabled = false
            });
        }

        _deviceListPanel.SuspendLayout();
        _deviceListPanel.Controls.Clear();
        foreach (var card in cards)
        {
            var control = new DeviceCardControl(card, _theme);
            control.DeviceInvoked += (_, id) => DeviceSelectionToggled?.Invoke(this, id);
            _deviceListPanel.Controls.Add(control);
        }

        _deviceListPanel.ResumeLayout();
        ResizeCards();

        _isBindingStartupState = true;
        _openOnStartupCheckBox.Checked = startupEnabled;
        _isBindingStartupState = false;

        SetStatus(statusMessage);
        BindSelectedDeviceTags(selectedDeviceNames);
        _clearButton.Enabled = selectedDeviceIds.Count > 0;
    }

    public void SetStatus(string message)
    {
        _statusLabel.Text = message;
        _statusLabel.ForeColor = ResolveStatusColor(message);
    }

    private Control BuildHeader()
    {
        _subtitleLabel.Text = "Choose up to 2 output devices to switch between from the tray icon.";
        _subtitleLabel.AutoSize = true;
        _subtitleLabel.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        _subtitleLabel.ForeColor = _theme.TextSecondary;
        _subtitleLabel.Margin = new Padding(0, 4, 0, 0);

        _statusLabel.AutoSize = true;
        _statusLabel.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold, GraphicsUnit.Point);
        _statusLabel.Margin = new Padding(0);

        _selectedDevicesPanel.AutoSize = true;
        _selectedDevicesPanel.WrapContents = true;
        _selectedDevicesPanel.FlowDirection = FlowDirection.LeftToRight;
        _selectedDevicesPanel.Margin = new Padding(0);
        _selectedDevicesPanel.Padding = new Padding(0);
        _selectedDevicesPanel.BackColor = _theme.WindowBackground;

        _selectedDevicesHost.AutoSize = true;
        _selectedDevicesHost.Dock = DockStyle.Top;
        _selectedDevicesHost.Margin = new Padding(0, 14, 0, 0);
        _selectedDevicesHost.Padding = new Padding(0);
        _selectedDevicesHost.BackColor = _theme.WindowBackground;
        _selectedDevicesHost.Controls.Add(_selectedDevicesPanel);

        _titleLabel.Text = "Audio Toggle";
        _titleLabel.AutoSize = true;
        _titleLabel.Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold, GraphicsUnit.Point);
        _titleLabel.ForeColor = _theme.TextPrimary;
        _titleLabel.Margin = new Padding(0);

        _refreshButton.Text = "Refresh";
        _refreshButton.Theme = _theme;
        _refreshButton.Size = new Size(176, 40);
        _refreshButton.MinimumSize = _refreshButton.Size;
        _refreshButton.Margin = new Padding(0);
        _refreshButton.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);

        _clearButton.Text = "Clear selection";
        _clearButton.Theme = _theme;
        _clearButton.Size = new Size(176, 40);
        _clearButton.MinimumSize = _clearButton.Size;
        _clearButton.Margin = new Padding(0, 12, 0, 0);
        _clearButton.Click += (_, _) => ClearSelectionRequested?.Invoke(this, EventArgs.Empty);

        var leftStatusStack = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = _theme.WindowBackground
        };
        leftStatusStack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        leftStatusStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        leftStatusStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        leftStatusStack.Controls.Add(_statusLabel, 0, 0);
        leftStatusStack.Controls.Add(_selectedDevicesHost, 0, 1);

        _statusLeftHost.Dock = DockStyle.Fill;
        _statusLeftHost.Margin = new Padding(0);
        _statusLeftHost.Padding = new Padding(0);
        _statusLeftHost.BackColor = _theme.WindowBackground;
        _statusLeftHost.Controls.Clear();
        _statusLeftHost.Controls.Add(leftStatusStack);

        var rightActionsHost = new Panel
        {
            Dock = DockStyle.Fill,
            Width = 176 + ActionRightInset,
            MinimumSize = new Size(176 + ActionRightInset, 92),
            Margin = new Padding(24, 0, 0, 0),
            Padding = new Padding(0),
            BackColor = _theme.WindowBackground
        };

        _refreshButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _clearButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        rightActionsHost.Controls.Add(_refreshButton);
        rightActionsHost.Controls.Add(_clearButton);
        void LayoutRightButtons()
        {
            _refreshButton.Location = new Point(
                Math.Max(0, rightActionsHost.ClientSize.Width - _refreshButton.Width - ActionRightInset),
                0);
            _clearButton.Location = new Point(
                Math.Max(0, rightActionsHost.ClientSize.Width - _clearButton.Width - ActionRightInset),
                _refreshButton.Bottom + 12);
        }
        rightActionsHost.SizeChanged += (_, _) => LayoutRightButtons();
        LayoutRightButtons();

        var statusRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize = true,
            Margin = new Padding(0, 26, 0, 0),
            Padding = new Padding(0),
            BackColor = _theme.WindowBackground
        };
        statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 232F));
        statusRow.Controls.Add(_statusLeftHost, 0, 0);
        statusRow.Controls.Add(rightActionsHost, 1, 0);

        var headerTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 3,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 16),
            Padding = new Padding(0),
            BackColor = _theme.WindowBackground
        };
        headerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        headerTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        headerTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        headerTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        headerTable.Controls.Add(_titleLabel, 0, 0);
        headerTable.Controls.Add(_subtitleLabel, 0, 1);
        headerTable.Controls.Add(statusRow, 0, 2);

        return headerTable;
    }

    private Control BuildListPanel()
    {
        _deviceListHost.Dock = DockStyle.Fill;
        _deviceListHost.Padding = new Padding(0);
        _deviceListHost.Margin = new Padding(0, 0, 0, 16);
        _deviceListHost.BackColor = _theme.WindowBackground;

        _deviceScrollBar.Width = ScrollbarWidth;
        _deviceScrollBar.Margin = new Padding(0);
        _deviceScrollBar.Visible = false;
        _deviceScrollBar.Theme = _theme;
        _deviceScrollBar.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right;
        _deviceScrollBar.Scroll += (_, e) => _deviceListPanel.SetScrollOffset(e.NewValue);

        _deviceListPanel.Dock = DockStyle.Fill;
        _deviceListPanel.AutoScroll = false;
        _deviceListPanel.Padding = new Padding(0);
        _deviceListPanel.BackColor = _theme.WindowBackground;
        _deviceListPanel.ScrollStateChanged += (_, _) => SyncScrollBar();

        _deviceListHost.Controls.Add(_deviceListPanel);
        _deviceListHost.Controls.Add(_deviceScrollBar);
        _deviceListHost.Resize += (_, _) => LayoutScrollBar();
        LayoutScrollBar();
        return _deviceListHost;
    }

    private Control BuildFooter()
    {
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
            Margin = new Padding(0),
            Padding = new Padding(0, 16, 0, 0),
            BackColor = _theme.WindowBackground
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var checkboxRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
            BackColor = _theme.WindowBackground,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };

        _openOnStartupCheckBox.Text = "Open on startup";
        _openOnStartupCheckBox.AutoSize = true;
        _openOnStartupCheckBox.BackColor = _theme.WindowBackground;
        _openOnStartupCheckBox.ForeColor = _theme.TextPrimary;
        _openOnStartupCheckBox.Margin = new Padding(0);
        _openOnStartupCheckBox.CheckedChanged += (_, _) =>
        {
            if (!_isBindingStartupState)
            {
                StartupPreferenceChanged?.Invoke(this, _openOnStartupCheckBox.Checked);
            }
        };
        checkboxRow.Controls.Add(_openOnStartupCheckBox);

        _toggleButton.Text = "Toggle Now";
        _toggleButton.Theme = _theme;
        _toggleButton.IsPrimary = true;
        _toggleButton.Size = new Size(170, 46);
        _toggleButton.MinimumSize = _toggleButton.Size;
        _toggleButton.Margin = new Padding(0);
        _toggleButton.Click += (_, _) => ToggleNowRequested?.Invoke(this, EventArgs.Empty);

        var toggleHost = new Panel
        {
            Dock = DockStyle.Fill,
            Width = _toggleButton.Width + ActionRightInset,
            Height = _toggleButton.Height,
            Margin = new Padding(16, 0, 0, 0),
            BackColor = _theme.WindowBackground
        };
        void LayoutToggleButton()
        {
            _toggleButton.Location = new Point(
                Math.Max(0, toggleHost.ClientSize.Width - _toggleButton.Width - ActionRightInset),
                0);
        }
        toggleHost.SizeChanged += (_, _) => LayoutToggleButton();
        toggleHost.Controls.Add(_toggleButton);
        LayoutToggleButton();

        footer.Controls.Add(checkboxRow, 0, 0);
        footer.Controls.Add(toggleHost, 1, 0);
        return footer;
    }

    private void ResizeCards()
    {
        var availableWidth = GetCardWidth(_deviceScrollBar.Visible);
        _deviceListPanel.SuspendLayout();
        foreach (Control control in _deviceListPanel.Controls)
        {
            control.Width = availableWidth;
            control.Margin = new Padding(0, 0, 0, 12);
            control.Invalidate();
            control.Update();
        }

        _deviceListPanel.ResumeLayout();
        _deviceListPanel.Invalidate(true);
        _deviceListHost.Invalidate(true);
        SyncScrollBar();
    }

    private void UpdateSelectedTagLayout()
    {
        if (_statusLeftHost.ClientSize.Width <= 0)
        {
            return;
        }

        var maxWidth = Math.Max(180, _statusLeftHost.ClientSize.Width - 8);
        _selectedDevicesPanel.MaximumSize = new Size(maxWidth, 0);
        _selectedDevicesPanel.Width = maxWidth;
        _selectedDevicesPanel.PerformLayout();
        _selectedDevicesHost.Height = _selectedDevicesPanel.PreferredSize.Height;
    }

    private static string ShortenId(string deviceId)
    {
        return deviceId.Length <= 42 ? deviceId : $"{deviceId[..18]}...{deviceId[^16..]}";
    }

    private void BindSelectedDeviceTags(IEnumerable<string> selectedDeviceNames)
    {
        _selectedDevicesPanel.SuspendLayout();
        _selectedDevicesPanel.Controls.Clear();

        foreach (var name in selectedDeviceNames)
        {
            _selectedDevicesPanel.Controls.Add(new StatusTagLabel(name, _theme));
        }

        _selectedDevicesPanel.Visible = _selectedDevicesPanel.Controls.Count > 0;
        _selectedDevicesPanel.ResumeLayout();
        UpdateSelectedTagLayout();
    }

    private void SyncScrollBar()
    {
        var info = _deviceListPanel.GetVerticalScrollInfo();
        _deviceScrollBar.UpdateMetrics(info);
        LayoutScrollBar();
        ResizeCardsForScrollbarState(info.Visible);
    }

    private void LayoutScrollBar()
    {
        if (_deviceListHost.ClientSize.Width <= 0 || _deviceListHost.ClientSize.Height <= 0)
        {
            return;
        }

        _deviceScrollBar.Location = new Point(
            Math.Max(0, _deviceListHost.ClientSize.Width - _deviceScrollBar.Width - ScrollbarEdgeInset),
            0);
        _deviceScrollBar.Height = _deviceListHost.ClientSize.Height;
        _deviceScrollBar.BringToFront();
    }

    private void ResizeCardsForScrollbarState(bool scrollbarVisible)
    {
        var targetWidth = GetCardWidth(scrollbarVisible);

        var requiresUpdate = false;
        foreach (Control control in _deviceListPanel.Controls)
        {
            if (control.Width != targetWidth)
            {
                requiresUpdate = true;
                break;
            }
        }

        if (!requiresUpdate)
        {
            return;
        }

        _deviceListPanel.SuspendLayout();
        foreach (Control control in _deviceListPanel.Controls)
        {
            control.Width = targetWidth;
        }

        _deviceListPanel.ResumeLayout();
        _deviceListPanel.Invalidate();
    }

    private int GetCardWidth(bool scrollbarVisible)
    {
        var rightInset = scrollbarVisible
            ? _deviceScrollBar.Width + ScrollbarListGap + ScrollbarEdgeInset
            : CardSafetyInset;

        return Math.Max(280, _deviceListHost.ClientSize.Width - rightInset);
    }

    private Color ResolveStatusColor(string message)
    {
        if (message.Contains("Ready to toggle", StringComparison.OrdinalIgnoreCase))
        {
            return _theme.SuccessText;
        }

        if (message.Contains("Deselect one device first", StringComparison.OrdinalIgnoreCase)
            || message.Contains("missing", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Unable", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Could not", StringComparison.OrdinalIgnoreCase))
        {
            return _theme.WarningText;
        }

        return _theme.TextSecondary;
    }

    private void ApplyContainerBackColor(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is Panel or TableLayoutPanel or FlowLayoutPanel)
            {
                child.BackColor = _theme.WindowBackground;
            }

            if (child is CheckBox checkBox)
            {
                checkBox.BackColor = _theme.WindowBackground;
                checkBox.ForeColor = _theme.TextPrimary;
            }

            if (child.HasChildren)
            {
                ApplyContainerBackColor(child);
            }
        }
    }
}

internal static class DeviceNameParser
{
    public static (string Title, string? Subtitle) Split(string friendlyName)
    {
        if (string.IsNullOrWhiteSpace(friendlyName))
        {
            return ("Unknown device", null);
        }

        var openParen = friendlyName.LastIndexOf('(');
        var closeParen = friendlyName.LastIndexOf(')');
        if (openParen > 0 && closeParen == friendlyName.Length - 1 && openParen < closeParen)
        {
            var title = friendlyName[..openParen].Trim();
            var subtitle = friendlyName[(openParen + 1)..closeParen].Trim();
            if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(subtitle))
            {
                return (title, subtitle);
            }
        }

        return (friendlyName, null);
    }
}

internal sealed class DeviceCardModel
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    public bool IsSelected { get; init; }
    public bool IsCurrentOutput { get; init; }
    public bool IsMissing { get; init; }
    public bool IsDisabled { get; init; }
}

internal sealed class DeviceCardControl : Control
{
    private readonly DeviceCardModel _model;
    private ThemePalette _theme;
    private bool _isHovered;

    public DeviceCardControl(DeviceCardModel model, ThemePalette theme)
    {
        _model = model;
        _theme = theme;
        DoubleBuffered = true;
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        Cursor = _model.IsDisabled ? Cursors.Default : Cursors.Hand;
        Height = string.IsNullOrWhiteSpace(_model.Subtitle) ? 98 : 112;
        TabStop = false;
        Margin = new Padding(0, 0, 0, 12);

        MouseEnter += (_, _) =>
        {
            if (_model.IsDisabled)
            {
                return;
            }

            _isHovered = true;
            Invalidate();
        };
        MouseLeave += (_, _) =>
        {
            if (_model.IsDisabled)
            {
                return;
            }

            _isHovered = false;
            Invalidate();
        };
        Click += (_, _) =>
        {
            if (!_model.IsDisabled)
            {
                DeviceInvoked?.Invoke(this, _model.Id);
            }
        };
    }

    public event EventHandler<string>? DeviceInvoked;

    public ThemePalette Theme
    {
        get => _theme;
        set
        {
            _theme = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var accent = _theme.Accent;
        var background = ResolveBackground(accent);
        var border = ResolveBorder(accent);
        var textColor = ResolveTextColor();
        var subtitleColor = ResolveSubtitleColor();

        var rect = new Rectangle(1, 1, Width - 3, Height - 3);
        using var path = CreateRoundRectangle(rect, 20);
        using var backgroundBrush = new SolidBrush(background);
        using var borderPen = new Pen(border, 1.1F);

        e.Graphics.FillPath(backgroundBrush, path);
        e.Graphics.DrawPath(borderPen, path);

        DrawSelectionIndicator(e.Graphics, new Rectangle(18, (Height - 22) / 2, 22, 22), accent);

        var textLeft = 56;
        var titleTop = 14;
        var contentWidth = Width - textLeft - 150;
        TextRenderer.DrawText(
            e.Graphics,
            _model.Title,
            new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold, GraphicsUnit.Point),
            new Rectangle(textLeft, titleTop, Math.Max(120, contentWidth), 34),
            textColor,
            TextFormatFlags.EndEllipsis);

        if (!string.IsNullOrWhiteSpace(_model.Subtitle))
        {
            TextRenderer.DrawText(
                e.Graphics,
                _model.Subtitle,
                new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
                new Rectangle(textLeft, titleTop + 40, Math.Max(120, contentWidth), 24),
                subtitleColor,
                TextFormatFlags.EndEllipsis);
        }

        DrawBadges(e.Graphics, accent);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        Invalidate();
        Parent?.Invalidate();
    }

    private void DrawSelectionIndicator(Graphics graphics, Rectangle rect, Color accent)
    {
        var fill = _model.IsSelected
            ? accent
            : _theme.IsDark
                ? ColorBlendHelper.Blend(_theme.SurfaceBackground, Color.White, 0.08F)
                : Color.White;
        var border = _model.IsSelected
            ? accent
            : _model.IsDisabled
                ? ColorBlendHelper.Blend(_theme.BorderColor, _theme.WindowBackground, _theme.IsDark ? 0.24F : 0.10F)
                : _theme.BorderColor;

        if (_model.IsDisabled)
        {
            fill = _theme.SurfaceDisabledBackground;
        }

        using var fillBrush = new SolidBrush(fill);
        using var borderPen = new Pen(border, 1.5F);
        graphics.FillEllipse(fillBrush, rect);
        graphics.DrawEllipse(borderPen, rect);

        if (_model.IsSelected)
        {
            using var checkPen = new Pen(Color.White, 2F)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            var a = new Point(rect.Left + 5, rect.Top + 11);
            var b = new Point(rect.Left + 9, rect.Top + 15);
            var c = new Point(rect.Right - 5, rect.Top + 7);
            graphics.DrawLines(checkPen, new[] { a, b, c });
        }
    }

    private void DrawBadges(Graphics graphics, Color accent)
    {
        var badges = new List<(string Text, Color Back, Color Fore)>();

        if (_model.IsCurrentOutput)
        {
            badges.Add(("Current output", ColorBlendHelper.Blend(_theme.SurfaceBackground, accent, _theme.IsDark ? 0.28F : 0.18F), ResolveAccentBadgeText(accent)));
        }

        if (_model.IsSelected)
        {
            badges.Add(("Selected", ColorBlendHelper.Blend(_theme.SurfaceBackground, accent, _theme.IsDark ? 0.22F : 0.14F), ResolveAccentBadgeText(accent)));
        }

        if (_model.IsMissing)
        {
            badges.Add(("Missing", _theme.IsDark ? Color.FromArgb(82, 47, 45) : Color.FromArgb(245, 231, 231), _theme.WarningText));
        }

        if (_model.IsDisabled)
        {
            badges.Add(("Unavailable", _theme.IsDark ? Color.FromArgb(57, 60, 66) : Color.FromArgb(241, 243, 245), _theme.TextMuted));
        }

        var x = Width - 18;
        for (var index = badges.Count - 1; index >= 0; index--)
        {
            var badge = badges[index];
            var size = TextRenderer.MeasureText(badge.Text, new Font("Segoe UI Semibold", 8F, FontStyle.Bold, GraphicsUnit.Point));
            var badgeWidth = size.Width + 18;
            var badgeRect = new Rectangle(x - badgeWidth, 18, badgeWidth, 24);
            using var path = CreateRoundRectangle(badgeRect, 12);
            using var badgeBrush = new SolidBrush(badge.Back);
            graphics.FillPath(badgeBrush, path);
            TextRenderer.DrawText(
                graphics,
                badge.Text,
                new Font("Segoe UI Semibold", 8F, FontStyle.Bold, GraphicsUnit.Point),
                badgeRect,
                badge.Fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            x -= badgeWidth + 8;
        }
    }

    private Color ResolveBackground(Color accent)
    {
        var baseColor = _theme.SurfaceBackground;
        if (_model.IsDisabled)
        {
            return _theme.SurfaceDisabledBackground;
        }

        if (_model.IsSelected)
        {
            return ColorBlendHelper.Blend(baseColor, accent, _theme.IsDark ? 0.18F : 0.09F);
        }

        if (_isHovered)
        {
            return ColorBlendHelper.Blend(baseColor, accent, _theme.IsDark ? 0.07F : 0.035F);
        }

        return _model.IsMissing
            ? _theme.IsDark ? Color.FromArgb(42, 44, 50) : Color.FromArgb(248, 248, 249)
            : baseColor;
    }

    private Color ResolveBorder(Color accent)
    {
        if (_model.IsDisabled)
        {
            return ColorBlendHelper.Blend(_theme.BorderColor, _theme.WindowBackground, _theme.IsDark ? 0.20F : 0.05F);
        }

        if (_model.IsSelected)
        {
            return ColorBlendHelper.Blend(_theme.BorderColor, accent, _theme.IsDark ? 0.58F : 0.45F);
        }

        if (_isHovered)
        {
            return ColorBlendHelper.Blend(_theme.BorderColor, accent, _theme.IsDark ? 0.30F : 0.22F);
        }

        return _model.IsMissing
            ? ColorBlendHelper.Blend(_theme.BorderColor, _theme.WarningText, _theme.IsDark ? 0.15F : 0.08F)
            : _theme.BorderColor;
    }

    private Color ResolveTextColor()
    {
        if (_model.IsDisabled)
        {
            return _theme.TextMuted;
        }

        return _model.IsMissing
            ? ColorBlendHelper.Blend(_theme.TextPrimary, _theme.TextMuted, 0.35F)
            : _theme.TextPrimary;
    }

    private Color ResolveSubtitleColor()
    {
        if (_model.IsDisabled)
        {
            return _theme.TextMuted;
        }

        return _model.IsMissing
            ? ColorBlendHelper.Blend(_theme.TextSecondary, _theme.TextMuted, 0.30F)
            : _theme.TextSecondary;
    }

    private Color ResolveAccentBadgeText(Color accent)
    {
        return _theme.IsDark
            ? ColorBlendHelper.Blend(Color.White, accent, 0.22F)
            : accent;
    }

    private static GraphicsPath CreateRoundRectangle(Rectangle rectangle, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class FluentButton : Button
{
    private ThemePalette _theme = ThemePalette.Light;
    private bool _isHovered;
    private bool _isPressed;

    public FluentButton()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        AutoSize = false;
        MinimumSize = new Size(116, 38);
        Size = MinimumSize;
        Margin = new Padding(0, 0, 10, 0);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        FlatAppearance.MouseDownBackColor = BackColor;
        FlatAppearance.MouseOverBackColor = BackColor;
        Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold, GraphicsUnit.Point);
        Padding = new Padding(14, 0, 14, 0);
        UseVisualStyleBackColor = false;
    }

    public ThemePalette Theme
    {
        get => _theme;
        set
        {
            _theme = value;
            Invalidate();
        }
    }

    public bool IsPrimary { get; set; }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        pevent.Graphics.Clear(ResolveSurfaceColor(Parent));

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = new GraphicsPath();
        path.AddArc(rect.Left, rect.Top, 18, 18, 180, 90);
        path.AddArc(rect.Right - 18, rect.Top, 18, 18, 270, 90);
        path.AddArc(rect.Right - 18, rect.Bottom - 18, 18, 18, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - 18, 18, 18, 90, 90);
        path.CloseFigure();

        var background = ResolveBackgroundColor();
        var border = ResolveBorderColor();
        var foreground = ResolveForegroundColor();
        using var backgroundBrush = new SolidBrush(background);
        using var borderPen = new Pen(border);
        pevent.Graphics.FillPath(backgroundBrush, path);
        pevent.Graphics.DrawPath(borderPen, path);
        TextRenderer.DrawText(
            pevent.Graphics,
            Text,
            Font,
            rect,
            foreground,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        _isPressed = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        base.OnMouseDown(mevent);
        _isPressed = true;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        base.OnMouseUp(mevent);
        _isPressed = false;
        Invalidate();
    }

    private Color ResolveBackgroundColor()
    {
        if (!Enabled)
        {
            return _theme.IsDark ? Color.FromArgb(61, 64, 70) : Color.FromArgb(243, 244, 246);
        }

        if (IsPrimary)
        {
            if (_isPressed)
            {
                return _theme.PrimaryButtonPressedBackground;
            }

            if (_isHovered)
            {
                return _theme.PrimaryButtonHoverBackground;
            }

            return _theme.PrimaryButtonBackground;
        }

        if (_isPressed)
        {
            return _theme.ButtonPressedBackground;
        }

        if (_isHovered)
        {
            return _theme.ButtonHoverBackground;
        }

        return _theme.ButtonBackground;
    }

    private Color ResolveBorderColor()
    {
        if (!Enabled)
        {
            return _theme.IsDark ? Color.FromArgb(79, 83, 91) : Color.FromArgb(225, 228, 233);
        }

        if (IsPrimary)
        {
            return _theme.PrimaryButtonBorder;
        }

        return _isHovered ? _theme.ButtonHoverBorder : _theme.ButtonBorder;
    }

    private Color ResolveForegroundColor()
    {
        if (!Enabled)
        {
            return _theme.TextMuted;
        }

        if (IsPrimary)
        {
            return _theme.PrimaryButtonText;
        }

        return _theme.ButtonText;
    }

    private static Color ResolveSurfaceColor(Control? control)
    {
        while (control is not null)
        {
            if (control.BackColor.A == 255)
            {
                return control.BackColor;
            }

            control = control.Parent;
        }

        return SystemColors.Control;
    }
}

internal sealed class DoubleBufferedFlowLayoutPanel : Panel
{
    private int _contentHeight;
    private int _lastScrollValue = -1;
    private int _scrollOffset;

    public DoubleBufferedFlowLayoutPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        AutoScroll = false;
        TabStop = true;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UpdateLayoutAndState();
        PublishScrollState();
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        UpdateLayoutAndState();
        PublishScrollState();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        Focus();
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        UpdateLayoutAndState();
        PublishScrollState();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        var step = Math.Max(48, SystemInformation.MouseWheelScrollLines * 24);
        SetScrollOffset(_scrollOffset + (e.Delta > 0 ? -step : step));
        base.OnMouseWheel(e);
    }

    public event EventHandler? ScrollStateChanged;

    public void SetScrollOffset(int value)
    {
        var maximum = GetMaximumScrollOffset();
        var clamped = Math.Max(0, Math.Min(value, maximum));
        if (_scrollOffset == clamped)
        {
            return;
        }

        _scrollOffset = clamped;
        PerformLayout();
        PublishScrollState();
    }

    public ScrollMetrics GetVerticalScrollInfo()
    {
        var maximum = GetMaximumScrollOffset();
        return new ScrollMetrics
        {
            Visible = maximum > 0,
            Minimum = 0,
            Maximum = maximum,
            Value = _scrollOffset,
            LargeChange = Math.Max(1, ClientSize.Height)
        };
    }

    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        if (e.Control is not null)
        {
            e.Control.SizeChanged += ChildControlOnSizeChanged;
            e.Control.VisibleChanged += ChildControlOnVisibleChanged;
        }

        UpdateLayoutAndState();
        PublishScrollState();
    }

    protected override void OnControlRemoved(ControlEventArgs e)
    {
        if (e.Control is not null)
        {
            e.Control.SizeChanged -= ChildControlOnSizeChanged;
            e.Control.VisibleChanged -= ChildControlOnVisibleChanged;
        }

        base.OnControlRemoved(e);
        UpdateLayoutAndState();
        PublishScrollState();
    }

    private void ChildControlOnSizeChanged(object? sender, EventArgs e)
    {
        UpdateLayoutAndState();
        PublishScrollState();
    }

    private void ChildControlOnVisibleChanged(object? sender, EventArgs e)
    {
        UpdateLayoutAndState();
        PublishScrollState();
    }

    private void UpdateLayoutAndState()
    {
        _contentHeight = CalculateContentHeight();
        var maximum = GetMaximumScrollOffset();
        if (_scrollOffset > maximum)
        {
            _scrollOffset = maximum;
        }

        var y = Padding.Top - _scrollOffset;
        foreach (Control control in Controls)
        {
            if (!control.Visible)
            {
                continue;
            }

            y += control.Margin.Top;
            var targetLocation = new Point(Padding.Left + control.Margin.Left, y);
            if (control.Location != targetLocation)
            {
                control.Location = targetLocation;
            }

            y += control.Height + control.Margin.Bottom;
        }
    }

    private void PublishScrollState()
    {
        var currentValue = _scrollOffset;
        if (_lastScrollValue != currentValue)
        {
            _lastScrollValue = currentValue;
        }

        ScrollStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private int CalculateContentHeight()
    {
        var height = Padding.Top;
        foreach (Control control in Controls)
        {
            if (!control.Visible)
            {
                continue;
            }

            height += control.Margin.Top + control.Height + control.Margin.Bottom;
        }

        return height + Padding.Bottom;
    }

    private int GetMaximumScrollOffset()
    {
        return Math.Max(0, _contentHeight - ClientSize.Height);
    }
}

internal sealed class SlimScrollBar : Control
{
    private ScrollMetrics _metrics = new() { LargeChange = 1 };
    private ThemePalette _theme = ThemePalette.Light;
    private bool _dragging;
    private int _dragOffset;

    public SlimScrollBar()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        BackColor = SystemColors.Control;
        Cursor = Cursors.Hand;
    }

    public event EventHandler<ScrollChangedEventArgs>? Scroll;

    public ThemePalette Theme
    {
        get => _theme;
        set
        {
            _theme = value;
            Invalidate();
        }
    }

    public void UpdateMetrics(ScrollMetrics metrics)
    {
        _metrics = metrics;
        Visible = metrics.Visible;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(ResolveSurfaceColor(Parent));

        if (!_metrics.Visible)
        {
            return;
        }

        var thumbRect = GetThumbRectangle();
        using var brush = new SolidBrush(_theme.ScrollThumb);
        e.Graphics.FillRoundedRectangle(brush, thumbRect, 4);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!_metrics.Visible)
        {
            return;
        }

        var thumb = GetThumbRectangle();
        if (thumb.Contains(e.Location))
        {
            _dragging = true;
            _dragOffset = e.Y - thumb.Y;
            Capture = true;
            return;
        }

        SetValueFromThumbCenter(e.Y - (thumb.Height / 2));
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging)
        {
            SetValueFromThumbTop(e.Y - _dragOffset);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragging = false;
        Capture = false;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_metrics.Visible)
        {
            var delta = e.Delta > 0 ? -SystemInformation.MouseWheelScrollLines * 24 : SystemInformation.MouseWheelScrollLines * 24;
            EmitScroll(_metrics.Value + delta);
        }
    }

    private Rectangle GetThumbRectangle()
    {
        var trackHeight = Math.Max(24, Height - 8);
        var range = Math.Max(1, _metrics.Maximum - _metrics.Minimum);
        var thumbHeight = Math.Max(36, (int)Math.Round(trackHeight * (_metrics.LargeChange / (double)(range + _metrics.LargeChange))));
        thumbHeight = Math.Min(trackHeight, thumbHeight);

        var movableRange = Math.Max(1, trackHeight - thumbHeight);
        var top = 4;
        if (range > 0)
        {
            top += (int)Math.Round((_metrics.Value - _metrics.Minimum) / (double)range * movableRange);
        }

        var thumbWidth = Math.Max(4, Width - 4);
        var left = Math.Max(0, Width - thumbWidth - 1);
        return new Rectangle(left, top, thumbWidth, thumbHeight);
    }

    private void SetValueFromThumbCenter(int centeredTop)
    {
        SetValueFromThumbTop(centeredTop);
    }

    private void SetValueFromThumbTop(int thumbTop)
    {
        var trackHeight = Math.Max(24, Height - 8);
        var thumb = GetThumbRectangle();
        var movableRange = Math.Max(1, trackHeight - thumb.Height);
        var clampedTop = Math.Max(4, Math.Min(4 + movableRange, thumbTop));
        var ratio = (clampedTop - 4) / (double)movableRange;
        var next = _metrics.Minimum + (int)Math.Round((_metrics.Maximum - _metrics.Minimum) * ratio);
        EmitScroll(next);
    }

    private void EmitScroll(int value)
    {
        var clamped = Math.Max(_metrics.Minimum, Math.Min(_metrics.Maximum, value));
        Scroll?.Invoke(this, new ScrollChangedEventArgs(clamped));
    }

    private static Color ResolveSurfaceColor(Control? control)
    {
        while (control is not null)
        {
            if (control.BackColor.A == 255)
            {
                return control.BackColor;
            }

            control = control.Parent;
        }

        return SystemColors.Control;
    }
}

internal sealed class ScrollChangedEventArgs(int newValue) : EventArgs
{
    public int NewValue { get; } = newValue;
}

internal sealed class ScrollMetrics
{
    public bool Visible { get; init; }
    public int Minimum { get; init; }
    public int Maximum { get; init; }
    public int Value { get; init; }
    public int LargeChange { get; init; }
}

internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics graphics, Brush brush, Rectangle rectangle, int radius)
    {
        using var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        graphics.FillPath(brush, path);
    }
}

internal sealed class StatusTagLabel : Control
{
    private readonly string _text;
    private ThemePalette _theme;

    public StatusTagLabel(string text, ThemePalette theme)
    {
        _text = text;
        _theme = theme;
        AutoSize = false;
        Height = 32;
        Margin = new Padding(0, 0, 8, 8);
        Width = TextRenderer.MeasureText(_text, new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold, GraphicsUnit.Point)).Width + 26;
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
    }

    public ThemePalette Theme
    {
        get => _theme;
        set
        {
            _theme = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(ResolveSurfaceColor(Parent));

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = new GraphicsPath();
        path.AddArc(rect.Left, rect.Top, 16, 16, 180, 90);
        path.AddArc(rect.Right - 16, rect.Top, 16, 16, 270, 90);
        path.AddArc(rect.Right - 16, rect.Bottom - 16, 16, 16, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - 16, 16, 16, 90, 90);
        path.CloseFigure();

        using var backBrush = new SolidBrush(_theme.SelectedTagBackground);
        using var borderPen = new Pen(_theme.SelectedTagBorder);
        e.Graphics.FillPath(backBrush, path);
        e.Graphics.DrawPath(borderPen, path);
        TextRenderer.DrawText(
            e.Graphics,
            _text,
            new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold, GraphicsUnit.Point),
            rect,
            _theme.SelectedTagText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }

    private static Color ResolveSurfaceColor(Control? control)
    {
        while (control is not null)
        {
            if (control.BackColor.A == 255)
            {
                return control.BackColor;
            }

            control = control.Parent;
        }

        return SystemColors.Control;
    }
}
