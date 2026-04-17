using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls.Notifications;
using DailyWords.Models;
using DailyWords.Services;

namespace DailyWords.Mac;

public sealed class WordCardController : INotifyPropertyChanged, IDisposable
{
    private readonly ConfigService _configService;
    private readonly WordRepository _wordRepository;
    private readonly WordRotationService _wordRotationService;

    private string _currentWordText = "加载中...";
    private string _currentPhonetic = string.Empty;
    private string _currentMeaning = string.Empty;
    private string _currentExample = string.Empty;
    private string _statusText = string.Empty;
    private string _secondaryStatusText = string.Empty;
    private string _windowTipText = string.Empty;

    public WordCardController(
        AppConfig config,
        ConfigService configService,
        WordRepository wordRepository)
    {
        Config = config;
        _configService = configService;
        _wordRepository = wordRepository;
        _wordRotationService = new WordRotationService(Config);

        _wordRotationService.CurrentWordChanged += (_, _) => RefreshView();
        _wordRotationService.StateChanged += (_, _) => RefreshView();
        RefreshView();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler<NotificationRequest>? NotificationRequested;

    public AppConfig Config { get; }

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

    public bool CanShowPhonetic => Config.ShowPhonetic && !string.IsNullOrWhiteSpace(CurrentPhonetic);

    public bool CanShowMeaning => Config.ShowMeaning && !string.IsNullOrWhiteSpace(CurrentMeaning);

    public bool CanShowExample => Config.ShowExample && !string.IsNullOrWhiteSpace(CurrentExample);

    public bool CanToggleMastered => _wordRotationService.CurrentWord is not null;

    public bool ShowOnlyUnmastered => Config.ShowOnlyUnmastered;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var words = await _wordRepository.LoadAsync(cancellationToken);
        _wordRotationService.SetWords(words);
        RefreshView();
    }

    public void HandlePrevious()
    {
        _wordRotationService.Previous();
        SaveConfig();
    }

    public void HandleTogglePlay()
    {
        _wordRotationService.TogglePlayState();
        SaveConfig();
    }

    public void HandleNext()
    {
        _wordRotationService.Next();
        SaveConfig();
    }

    public void ToggleCurrentWordMastered()
    {
        _wordRotationService.ToggleCurrentWordMastered();
        SaveConfig();
    }

    public async Task RebuildWordBankAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var words = await _wordRepository.RebuildAsync(cancellationToken);
            _wordRotationService.SetWords(words);
            _wordRotationService.ApplyConfig();
            SaveConfig();
            RefreshView();
            RaiseNotification("词库更新完成", "词库已经重新构建完成。", NotificationType.Success);
        }
        catch (Exception exception)
        {
            RaiseNotification("重建词库失败", exception.Message, NotificationType.Warning);
        }
    }

    public void ToggleOnlyUnmasteredMode()
    {
        Config.ShowOnlyUnmastered = !Config.ShowOnlyUnmastered;
        _wordRotationService.ApplyConfig();
        SaveConfig();
        RefreshView();
    }

    public void ApplyConfig()
    {
        _wordRotationService.ApplyConfig();
        SaveConfig();
        RefreshView();
    }

    public void UpdateWindowBounds(double left, double top, double width, double height)
    {
        Config.WindowLeft = left;
        Config.WindowTop = top;
        Config.WindowWidth = width;
        Config.WindowHeight = height;
        SaveConfig();
    }

    public void Dispose()
    {
        _wordRotationService.Dispose();
    }

    private void SaveConfig()
    {
        _configService.Save(Config);
    }

    private void RefreshView()
    {
        var currentWord = _wordRotationService.CurrentWord;
        if (currentWord is null)
        {
            CurrentWordText = "当前没有可显示的单词";
            CurrentPhonetic = string.Empty;
            CurrentMeaning = Config.ShowOnlyUnmastered
                ? "当前筛选下没有未掌握单词，可以在设置里关闭“仅显示未掌握单词”。"
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
            StatusText = "当前没有可轮播单词";
        }

        SecondaryStatusText = $"已记住 {_wordRotationService.MasteredCount}/{_wordRotationService.TotalCount} 个 | 模式：{(Config.ShowOnlyUnmastered ? "仅未掌握" : "全部单词")}";
        WindowTipText = Config.LockWindowPosition ? "位置已锁定" : "可拖动移动";

        OnPropertyChanged(nameof(CanShowPhonetic));
        OnPropertyChanged(nameof(CanShowMeaning));
        OnPropertyChanged(nameof(CanShowExample));
        OnPropertyChanged(nameof(PlaybackButtonText));
        OnPropertyChanged(nameof(MarkButtonText));
        OnPropertyChanged(nameof(CanToggleMastered));
        OnPropertyChanged(nameof(ShowOnlyUnmastered));
    }

    private void RaiseNotification(string title, string message, NotificationType type)
    {
        NotificationRequested?.Invoke(this, new NotificationRequest(title, message, type));
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
