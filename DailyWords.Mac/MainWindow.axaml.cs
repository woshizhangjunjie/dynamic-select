using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Input;

namespace DailyWords.Mac;

public partial class MainWindow : Window
{
    private readonly WordCardController _controller;
    private readonly WindowNotificationManager _notificationManager;
    private bool _allowClose;
    private bool _windowLoaded;

    public MainWindow(WordCardController controller)
    {
        InitializeComponent();
        DataContext = controller;
        _controller = controller;
        _notificationManager = new WindowNotificationManager(this)
        {
            Position = NotificationPosition.TopRight,
            MaxItems = 3,
        };
        _controller.NotificationRequested += Controller_OnNotificationRequested;
    }

    public async Task OpenSettingsAsync()
    {
        var settingsWindow = new SettingsWindow(_controller.Config.Clone())
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        var result = await settingsWindow.ShowDialog<bool?>(this);
        if (result != true)
        {
            return;
        }

        _controller.Config.CopyFrom(settingsWindow.ResultConfig);
        ApplyConfigToWindow();

        if (settingsWindow.ResetPositionRequested)
        {
            ResetToDefaultPosition();
            ShowNotification("设置", "窗口位置已恢复到默认位置。", NotificationType.Information);
        }

        _controller.ApplyConfig();

        if (settingsWindow.RebuildWordBankRequested)
        {
            await _controller.RebuildWordBankAsync();
        }
    }

    public void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    public void RequestExitAndShutdown()
    {
        _allowClose = true;
        Close();
        if (Application.Current?.ApplicationLifetime is IControlledApplicationLifetime lifetime)
        {
            lifetime.Shutdown();
        }
    }

    public void ShowNotification(string title, string message, NotificationType type)
    {
        _notificationManager.Show(new Notification(title, message, type));
    }

    private void MainWindow_OnOpened(object? sender, EventArgs e)
    {
        _windowLoaded = true;
        ApplyConfigToWindow();
        RestoreWindowPosition();
    }

    private void MainWindow_OnPositionChanged(object? sender, PixelPointEventArgs e)
    {
        if (!_windowLoaded || _controller.Config.LockWindowPosition)
        {
            return;
        }

        SaveWindowState();
    }

    private void MainWindow_OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (!_allowClose)
        {
            Hide();
            e.Cancel = true;
            return;
        }

        SaveWindowState();
        _controller.Dispose();
    }

    private void WordCardBorder_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_controller.Config.LockWindowPosition)
        {
            return;
        }

        if (e.Source is Button)
        {
            return;
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
            SaveWindowState();
        }
    }

    private void PreviousButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _controller.HandlePrevious();
    }

    private void PlayButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _controller.HandleTogglePlay();
    }

    private void NextButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _controller.HandleNext();
    }

    private void MasteredButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _controller.ToggleCurrentWordMastered();
    }

    private async void SettingsButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await OpenSettingsAsync();
    }

    private void ApplyConfigToWindow()
    {
        Opacity = _controller.Config.WindowOpacity;
        Width = _controller.Config.WindowWidth;
        Height = _controller.Config.WindowHeight;
    }

    private void RestoreWindowPosition()
    {
        var workingArea = Screens.Primary?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);
        var hasStoredPosition = !double.IsNaN(_controller.Config.WindowLeft) && !double.IsNaN(_controller.Config.WindowTop);
        if (hasStoredPosition && IsInsideWorkArea(_controller.Config.WindowLeft, _controller.Config.WindowTop, workingArea))
        {
            Position = new PixelPoint((int)Math.Round(_controller.Config.WindowLeft), (int)Math.Round(_controller.Config.WindowTop));
            return;
        }

        ResetToDefaultPosition();
    }

    private void ResetToDefaultPosition()
    {
        var workingArea = Screens.Primary?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);
        Position = new PixelPoint(workingArea.X + 20, workingArea.Bottom - (int)Math.Round(Height) - 20);
        SaveWindowState();
    }

    private void SaveWindowState()
    {
        _controller.UpdateWindowBounds(Position.X, Position.Y, Width, Height);
    }

    private void Controller_OnNotificationRequested(object? sender, NotificationRequest e)
    {
        ShowNotification(e.Title, e.Message, e.Type);
    }

    private static bool IsInsideWorkArea(double left, double top, PixelRect workArea)
    {
        return left >= workArea.X - 40
               && top >= workArea.Y - 40
               && left <= workArea.Right - 120
               && top <= workArea.Bottom - 120;
    }
}
