using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Markup.Xaml;
using DailyWords.Services;

namespace DailyWords.Mac;

public partial class App : Application
{
    private WordCardController? _controller;
    private MainWindow? _mainWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;

            var configService = new ConfigService();
            var controller = new WordCardController(
                configService.Load(),
                configService,
                new WordRepository(new WordSourceService(new WordEnrichmentService(new HttpClient()))));

            var mainWindow = new MainWindow(controller);
            _controller = controller;
            _mainWindow = mainWindow;
            DataContext = controller;

            desktop.MainWindow = mainWindow;
            mainWindow.Show();

            try
            {
                await controller.InitializeAsync();
            }
            catch (Exception exception)
            {
                mainWindow.ShowNotification("启动失败", exception.Message, NotificationType.Error);
                desktop.Shutdown();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ShowMenuItem_OnClick(object? sender, EventArgs e)
    {
        _mainWindow?.ShowFromTray();
    }

    private void PreviousMenuItem_OnClick(object? sender, EventArgs e)
    {
        _controller?.HandlePrevious();
    }

    private void PlayMenuItem_OnClick(object? sender, EventArgs e)
    {
        _controller?.HandleTogglePlay();
    }

    private void NextMenuItem_OnClick(object? sender, EventArgs e)
    {
        _controller?.HandleNext();
    }

    private async void SettingsMenuItem_OnClick(object? sender, EventArgs e)
    {
        if (_mainWindow is null)
        {
            return;
        }

        await _mainWindow.OpenSettingsAsync();
    }

    private async void RebuildMenuItem_OnClick(object? sender, EventArgs e)
    {
        if (_controller is null)
        {
            return;
        }

        await _controller.RebuildWordBankAsync();
    }

    private void FilterMenuItem_OnClick(object? sender, EventArgs e)
    {
        _controller?.ToggleOnlyUnmasteredMode();
    }

    private void ExitMenuItem_OnClick(object? sender, EventArgs e)
    {
        _mainWindow?.RequestExitAndShutdown();
    }
}
