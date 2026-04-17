using System.Net.Http;
using System.Windows;
using DailyWords.Services;
using DailyWords.Views;

namespace DailyWords;

public partial class App : System.Windows.Application
{
    private TrayService? _trayService;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var configService = new ConfigService();
        var config = configService.Load();
        var startupService = new StartupService();
        startupService.SetEnabled(config.StartWithWindows);

        var repository = new WordRepository(
            new WordSourceService(new WordEnrichmentService(new HttpClient())));

        try
        {
            var words = await repository.LoadAsync();
            var rotationService = new WordRotationService(config);
            rotationService.SetWords(words);

            _trayService = new TrayService();
            var mainWindow = new MainWindow(
                config,
                configService,
                repository,
                rotationService,
                _trayService,
                startupService);

            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"启动失败：{exception.Message}",
                "悬浮置顶单词本",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayService?.Dispose();
        base.OnExit(e);
    }
}
