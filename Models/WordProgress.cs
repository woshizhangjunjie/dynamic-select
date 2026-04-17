namespace DailyWords.Models;

public sealed class WordProgress
{
    public bool IsMastered { get; set; }

    public int SeenCount { get; set; }

    public DateTimeOffset? LastSeenAt { get; set; }

    public DateTimeOffset? MasteredAt { get; set; }

    public WordProgress Clone()
    {
        return new WordProgress
        {
            IsMastered = IsMastered,
            SeenCount = SeenCount,
            LastSeenAt = LastSeenAt,
            MasteredAt = MasteredAt,
        };
    }
}
