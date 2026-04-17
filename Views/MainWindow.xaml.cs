using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using DailyWords.Models;
using DailyWords.Services;

namespace DailyWords.Views;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly AppConfig _config;
    private readonly ConfigService _configService;
    private readonly WordRepository _wordRepository;
    private readonly WordRotationService _wordRotationService;
    private readonly TrayService _trayService;
    private readonly StartupService _startupService;

    private bool _allowClose;
    private bool _windowLoaded;
    private string _currentWordText = "加载中...";
    private string _currentPhonetic = string.Empty;
    private string _currentMeaning = string.Empty;
    private string _currentExample = string.Empty;
    private string _statusText = string.Empty;
    private string _secondaryStatusText = string.Empty;
    private string _windowTipText = string.Empty;

    public MainWindow(
        AppConfig config,
        ConfigService configService,
        WordRepository wordRepository,
        WordRotationService wordRotationService,
        TrayService trayService,
        StartupService startupService)
    {
        InitializeComponent();
        DataContext = this;

        _config = config;
        _configService = configService;
        _wordRepository = wordRepository;
        _wordRotationService = wordRotationService;
        _trayService = trayService;
        _startupService = startupService;

        Width = _config.WindowWidth;
        Height = _config.WindowHeight;

        _wordRotationService.CurrentWordChanged += (_, _) => Dispatcher.Invoke(() => RefreshView(true));
        _wordRotationService.StateChanged += (_, _) => Dispatcher.Invoke(() => RefreshView(false));

        _trayService.ShowRequested += (_, _) => Dispatcher.Invoke(ShowFromTray);
        _trayService.PreviousRequested += (_, _) => Dispatcher.Invoke(HandlePrevious);
        _trayService.TogglePlayRequested += (_, _) => Dispatcher.Invoke(HandleTogglePlay);
        _trayService.NextRequested += (_, _) => Dispatcher.Invoke(HandleNext);
        _trayService.SettingsRequested += async (_, _) => await OpenSettingsAsync();
        _trayService.RebuildRequested += async (_, _) => await RebuildWordBankAsync();
        _trayService.ToggleFilterRequested += (_, _) => Dispatcher.Invoke(ToggleOnlyUnmasteredMode);
        _trayService.ExitRequested += (_, _) => Dispatcher.Invoke(RequestExit);

        Closing += MainWindow_Closing;
        RefreshView(false);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string CurrentWordText
    {
        get => _currentWordText;
        private set => SetField(ref _currentWordText, value);
    }

    public string CurrentPhonetic
    {
        get => _currentPhonetic;
        private set => SetField(ref _currentPhonetic, value);
    }

    public string CurrentMeaning
    {
        get => _currentMeaning;
        private set => SetField(ref _currentMeaning, value);
    }

    public string CurrentExample
    {
        get => _currentExample;
        private set => SetField(ref _currentExample, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public string SecondaryStatusText
    {
        get => _secondaryStatusText;
        private set => SetField(ref _secondaryStatusText, value);
    }

    public string WindowTipText
    {
        get => _windowTipText;
        private set => SetField(ref _windowTipText, value);
    }

    public string PlaybackButtonText => _wordRotationService.IsPlaying ? "暂停" : "继续";

    public string MarkButtonText => _wordRotationService.IsCurrentWordMastered ? "取消记住" : "记住";

    public bool CanShowPhonetic => _config.ShowPhonetic && !string.IsNullOrWhiteSpace(CurrentPhonetic);

    public bool CanShowMeaning => _config.ShowMeaning && !string.IsNullOrWhiteSpace(CurrentMeaning);

    public bool CanShowExample => _config.ShowExample && !string.IsNullOrWhiteSpace(CurrentExample);

    public bool CanToggleMastered => _wordRotationService.CurrentWord is not null;

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _windowLoaded = true;
        ApplyConfigToWindow();
        RestoreWindowPosition();
        RefreshView(false);
    }

    private void Window_LocationChanged(object? sender, EventArgs e)
    {
        if (!_windowLoaded || _config.LockWindowPosition)
        {
            return;
        }

        _config.WindowLeft = Left;
        _config.WindowTop = Top;
        SaveConfig();
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        SaveWindowState();
        _trayService.Dispose();
    }

    private void WordCardBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_config.LockWindowPosition || IsButtonSource(e.OriginalSource as DependencyObject))
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch
        {
        }

        SaveWindowState();
    }

    private void PreviousButton_Click(object sender, RoutedEventArgs e)
    {
        HandlePrevious();
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        HandleTogglePlay();
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        HandleNext();
    }

    private void MasteredButton_Click(object sender, RoutedEventArgs e)
    {
        _wordRotationService.ToggleCurrentWordMastered();
        SaveConfig();
    }

    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        await OpenSettingsAsync();
    }

    private void HandlePrevious()
    {
        _wordRotationService.Previous();
        SaveConfig();
    }

    private void HandleTogglePlay()
    {
        _wordRotationService.TogglePlayState();
        SaveConfig();
    }

    private void HandleNext()
    {
        _wordRotationService.Next();
        SaveConfig();
    }

    private async Task OpenSettingsAsync()
    {
        var settingsWindow = new SettingsWindow(_config.Clone())
        {
            Owner = this,
        };

        if (settingsWindow.ShowDialog() != true)
        {
            return;
        }

        _config.CopyFrom(settingsWindow.ResultConfig);
        _startupService.SetEnabled(_config.StartWithWindows);
        ApplyConfigToWindow();

        if (settingsWindow.ResetPositionRequested)
        {
            ResetToDefaultPosition();
        }

        _wordRotationService.ApplyConfig();
        SaveConfig();
        RefreshView(false);

        if (settingsWindow.RebuildWordBankRequested)
        {
            await RebuildWordBankAsync();
        }
    }

    private async Task RebuildWordBankAsync()
    {
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            var words = await _wordRepository.RebuildAsync();
            _wordRotationService.SetWords(words);
            _wordRotationService.ApplyConfig();
            SaveConfig();
            RefreshView(false);
            MessageBox.Show("词库已更新完成。", "悬浮置顶单词本", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"重建词库失败：{exception.Message}",
                "悬浮置顶单词本",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void ToggleOnlyUnmasteredMode()
    {
        _config.ShowOnlyUnmastered = !_config.ShowOnlyUnmastered;
        _wordRotationService.ApplyConfig();
        SaveConfig();
        RefreshView(false);
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void RequestExit()
    {
        _allowClose = true;
        Close();
        Application.Current.Shutdown();
    }

    private void ApplyConfigToWindow()
    {
        Opacity = _config.WindowOpacity;
        Topmost = true;
        Width = _config.WindowWidth;
        Height = _config.WindowHeight;
    }

    private void RestoreWindowPosition()
    {
        var workArea = SystemParameters.WorkArea;
        var hasStoredPosition = !double.IsNaN(_config.WindowLeft) && !double.IsNaN(_config.WindowTop);
        if (hasStoredPosition && IsInsideWorkArea(_config.WindowLeft, _config.WindowTop, workArea))
        {
            Left = _config.WindowLeft;
            Top = _config.WindowTop;
            return;
        }

        ResetToDefaultPosition();
    }

    private void ResetToDefaultPosition()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + 20;
        Top = workArea.Bottom - Height - 20;
        SaveWindowState();
    }

    private void SaveWindowState()
    {
        _config.WindowLeft = Left;
        _config.WindowTop = Top;
        _config.WindowWidth = Width;
        _config.WindowHeight = Height;
        SaveConfig();
    }

    private void SaveConfig()
    {
        _configService.Save(_config);
    }

    private void RefreshView(bool animateCard)
    {
        var currentWord = _wordRotationService.CurrentWord;
        if (currentWord is null)
        {
            CurrentWordText = "当前没有可显示的单词";
            CurrentPhonetic = string.Empty;
            CurrentMeaning = _config.ShowOnlyUnmastered
                ? "当前筛选下没有未掌握单词，可以在设置中关闭“仅显示未掌握单词”。"
                : "未找到可用词条。";
            CurrentExample = string.Empty;
        }
        else
        {
            CurrentWordText = currentWord.Word;
            CurrentPhonetic = currentWord.Phonetic ?? string.Empty;
            CurrentMeaning = currentWord.Meaning ?? string.Empty;
            CurrentExample = currentWord.DisplayExample;
        }

        if (_wordRotationService.VisibleCount > 0 && _wordRotationService.CurrentVisibleIndex >= 0)
        {
            StatusText = $"第 {_wordRotationService.CurrentVisibleIndex + 1}/{_wordRotationService.VisibleCount} 个";
        }
        else
        {
            StatusText = "当前无可轮播单词";
        }

        SecondaryStatusText = $"已记住 {_wordRotationService.MasteredCount}/{_wordRotationService.TotalCount} 个 | 模式：{(_config.ShowOnlyUnmastered ? "仅未掌握" : "全部单词")}";
        WindowTipText = _config.LockWindowPosition ? "位置已锁定" : "可拖拽移动";

        OnPropertyChanged(nameof(CanShowPhonetic));
        OnPropertyChanged(nameof(CanShowMeaning));
        OnPropertyChanged(nameof(CanShowExample));
        OnPropertyChanged(nameof(PlaybackButtonText));
        OnPropertyChanged(nameof(MarkButtonText));
        OnPropertyChanged(nameof(CanToggleMastered));

        _trayService.UpdateState(_wordRotationService.IsPlaying, _config.ShowOnlyUnmastered);

        if (animateCard)
        {
            AnimateCard();
        }
    }

    private void AnimateCard()
    {
        var animation = new DoubleAnimation
        {
            From = 0.45,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(220),
        };
        WordCardBorder.BeginAnimation(OpacityProperty, animation);
    }

    private static bool IsButtonSource(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is Button)
            {
                return true;
            }

            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private static bool IsInsideWorkArea(double left, double top, Rect workArea)
    {
        return left >= workArea.Left - 40
               && top >= workArea.Top - 40
               && left <= workArea.Right - 120
               && top <= workArea.Bottom - 120;
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
