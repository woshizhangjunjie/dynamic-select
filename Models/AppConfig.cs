namespace DailyWords.Models;

public sealed class AppConfig
{
    public int RotationIntervalSeconds { get; set; } = 30;

    public bool AutoPlay { get; set; } = true;

    public bool LoopPlayback { get; set; } = true;

    public bool ShowPhonetic { get; set; } = true;

    public bool ShowMeaning { get; set; } = true;

    public bool ShowExample { get; set; } = true;

    public bool LockWindowPosition { get; set; }

    public bool StartWithWindows { get; set; }

    public bool ShowOnlyUnmastered { get; set; }

    public double WindowOpacity { get; set; } = 0.95;

    public double WindowLeft { get; set; } = double.NaN;

    public double WindowTop { get; set; } = double.NaN;

    public double WindowWidth { get; set; } = 380;

    public double WindowHeight { get; set; } = 250;

    public int CurrentIndex { get; set; }

    public string? CurrentWordKey { get; set; }

    public Dictionary<string, WordProgress> ProgressMap { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public void Normalize()
    {
        RotationIntervalSeconds = Math.Clamp(RotationIntervalSeconds, 3, 3600);
        WindowOpacity = Math.Clamp(WindowOpacity, 0.35, 1.00);
        WindowWidth = Math.Clamp(WindowWidth, 320, 420);
        WindowHeight = Math.Clamp(WindowHeight, 220, 320);
        CurrentIndex = Math.Max(0, CurrentIndex);
        ProgressMap = ProgressMap is null
            ? new Dictionary<string, WordProgress>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, WordProgress>(ProgressMap, StringComparer.OrdinalIgnoreCase);
    }

    public AppConfig Clone()
    {
        var clone = new AppConfig
        {
            RotationIntervalSeconds = RotationIntervalSeconds,
            AutoPlay = AutoPlay,
            LoopPlayback = LoopPlayback,
            ShowPhonetic = ShowPhonetic,
            ShowMeaning = ShowMeaning,
            ShowExample = ShowExample,
            LockWindowPosition = LockWindowPosition,
            StartWithWindows = StartWithWindows,
            ShowOnlyUnmastered = ShowOnlyUnmastered,
            WindowOpacity = WindowOpacity,
            WindowLeft = WindowLeft,
            WindowTop = WindowTop,
            WindowWidth = WindowWidth,
            WindowHeight = WindowHeight,
            CurrentIndex = CurrentIndex,
            CurrentWordKey = CurrentWordKey,
            ProgressMap = new Dictionary<string, WordProgress>(StringComparer.OrdinalIgnoreCase),
        };

        foreach (var pair in ProgressMap)
        {
            clone.ProgressMap[pair.Key] = pair.Value.Clone();
        }

        return clone;
    }

    public void CopyFrom(AppConfig source)
    {
        RotationIntervalSeconds = source.RotationIntervalSeconds;
        AutoPlay = source.AutoPlay;
        LoopPlayback = source.LoopPlayback;
        ShowPhonetic = source.ShowPhonetic;
        ShowMeaning = source.ShowMeaning;
        ShowExample = source.ShowExample;
        LockWindowPosition = source.LockWindowPosition;
        StartWithWindows = source.StartWithWindows;
        ShowOnlyUnmastered = source.ShowOnlyUnmastered;
        WindowOpacity = source.WindowOpacity;
        WindowLeft = source.WindowLeft;
        WindowTop = source.WindowTop;
        WindowWidth = source.WindowWidth;
        WindowHeight = source.WindowHeight;
        CurrentIndex = source.CurrentIndex;
        CurrentWordKey = source.CurrentWordKey;
        ProgressMap = new Dictionary<string, WordProgress>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in source.ProgressMap)
        {
            ProgressMap[pair.Key] = pair.Value.Clone();
        }

        Normalize();
    }
}
