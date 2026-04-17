using System.Windows.Threading;
using DailyWords.Models;

namespace DailyWords.Services;

public sealed class WordRotationService : IDisposable
{
    private readonly AppConfig _config;
    private readonly DispatcherTimer _timer;
    private readonly List<WordItem> _allWords = [];

    public event EventHandler<WordItem?>? CurrentWordChanged;

    public event EventHandler? StateChanged;

    public WordRotationService(AppConfig config)
    {
        _config = config;
        _timer = new DispatcherTimer();
        _timer.Tick += (_, _) => Move(1);
    }

    public WordItem? CurrentWord { get; private set; }

    public bool IsPlaying => _config.AutoPlay;

    public int TotalCount => _allWords.Count;

    public int VisibleCount => GetVisibleWords().Count;

    public int CurrentVisibleIndex => GetCurrentVisibleIndex();

    public int MasteredCount => _config.ProgressMap.Values.Count(item => item.IsMastered);

    public bool IsCurrentWordMastered =>
        CurrentWord is not null && GetProgress(CurrentWord.ProgressKey).IsMastered;

    public void SetWords(IReadOnlyList<WordItem> words)
    {
        _allWords.Clear();
        _allWords.AddRange(words.OrderBy(item => item.SortOrder));
        ApplyConfig();
    }

    public void ApplyConfig()
    {
        _timer.Interval = TimeSpan.FromSeconds(_config.RotationIntervalSeconds);
        EnsureCurrentWord();

        if (_config.AutoPlay && VisibleCount > 1)
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }

        RaiseStateChanged();
    }

    public void Pause()
    {
        _config.AutoPlay = false;
        _timer.Stop();
        RaiseStateChanged();
    }

    public void Resume()
    {
        _config.AutoPlay = true;
        if (VisibleCount > 1)
        {
            _timer.Start();
        }

        RaiseStateChanged();
    }

    public void TogglePlayState()
    {
        if (_config.AutoPlay)
        {
            Pause();
        }
        else
        {
            Resume();
        }
    }

    public void Next()
    {
        Move(1);
    }

    public void Previous()
    {
        Move(-1);
    }

    public void ToggleCurrentWordMastered()
    {
        if (CurrentWord is null)
        {
            return;
        }

        var progress = GetProgress(CurrentWord.ProgressKey);
        progress.IsMastered = !progress.IsMastered;
        progress.MasteredAt = progress.IsMastered ? DateTimeOffset.Now : null;

        if (_config.ShowOnlyUnmastered && progress.IsMastered)
        {
            Move(1, true);
            return;
        }

        RaiseStateChanged();
        RaiseCurrentWordChanged();
    }

    public void EnsureCurrentWord()
    {
        if (_allWords.Count == 0)
        {
            CurrentWord = null;
            RaiseCurrentWordChanged();
            return;
        }

        var visibleWords = GetVisibleWords();
        if (visibleWords.Count == 0)
        {
            CurrentWord = null;
            RaiseCurrentWordChanged();
            return;
        }

        if (!string.IsNullOrWhiteSpace(_config.CurrentWordKey))
        {
            var matchedWord = visibleWords.FirstOrDefault(item =>
                string.Equals(item.ProgressKey, WordItem.NormalizeKey(_config.CurrentWordKey), StringComparison.OrdinalIgnoreCase));

            if (matchedWord is not null)
            {
                SetCurrentWord(matchedWord, false, false);
                return;
            }
        }

        var index = Math.Clamp(_config.CurrentIndex, 0, _allWords.Count - 1);
        var current = _allWords[index];
        if (_config.ShowOnlyUnmastered && GetProgress(current.ProgressKey).IsMastered)
        {
            current = visibleWords[0];
        }

        SetCurrentWord(current, false, false);
    }

    public void Dispose()
    {
        _timer.Stop();
    }

    private void Move(int offset, bool fromMasteredChange = false)
    {
        var visibleWords = GetVisibleWords();
        if (visibleWords.Count == 0)
        {
            CurrentWord = null;
            RaiseCurrentWordChanged();
            RaiseStateChanged();
            return;
        }

        if (CurrentWord is null)
        {
            SetCurrentWord(visibleWords[0], true, true);
            return;
        }

        var currentIndex = visibleWords.FindIndex(item => item.ProgressKey == CurrentWord.ProgressKey);
        if (currentIndex < 0)
        {
            SetCurrentWord(visibleWords[0], true, true);
            return;
        }

        var nextIndex = currentIndex + offset;
        if (nextIndex >= visibleWords.Count)
        {
            if (_config.LoopPlayback)
            {
                nextIndex = 0;
            }
            else
            {
                nextIndex = visibleWords.Count - 1;
                if (!fromMasteredChange)
                {
                    Pause();
                }
            }
        }
        else if (nextIndex < 0)
        {
            nextIndex = _config.LoopPlayback ? visibleWords.Count - 1 : 0;
        }

        SetCurrentWord(visibleWords[nextIndex], true, true);
    }

    private void SetCurrentWord(WordItem wordItem, bool raiseEvent, bool markSeen)
    {
        CurrentWord = wordItem;
        _config.CurrentWordKey = wordItem.ProgressKey;
        _config.CurrentIndex = Math.Max(0, _allWords.FindIndex(item => item.ProgressKey == wordItem.ProgressKey));

        if (markSeen)
        {
            var progress = GetProgress(wordItem.ProgressKey);
            progress.SeenCount += 1;
            progress.LastSeenAt = DateTimeOffset.Now;
        }

        if (raiseEvent)
        {
            RaiseCurrentWordChanged();
        }

        RaiseStateChanged();
    }

    private List<WordItem> GetVisibleWords()
    {
        if (!_config.ShowOnlyUnmastered)
        {
            return [.. _allWords];
        }

        return _allWords
            .Where(item => !GetProgress(item.ProgressKey).IsMastered)
            .ToList();
    }

    private int GetCurrentVisibleIndex()
    {
        if (CurrentWord is null)
        {
            return -1;
        }

        var visibleWords = GetVisibleWords();
        return visibleWords.FindIndex(item => item.ProgressKey == CurrentWord.ProgressKey);
    }

    private WordProgress GetProgress(string key)
    {
        if (!_config.ProgressMap.TryGetValue(key, out var progress))
        {
            progress = new WordProgress();
            _config.ProgressMap[key] = progress;
        }

        return progress;
    }

    private void RaiseCurrentWordChanged()
    {
        CurrentWordChanged?.Invoke(this, CurrentWord);
    }

    private void RaiseStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
