using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DailyWords.Models;

public sealed class WordItem
{
    public string Word { get; set; } = string.Empty;

    public string WordKey { get; set; } = string.Empty;

    public string? Phonetic { get; set; }

    public string? Meaning { get; set; }

    public string? Example { get; set; }

    public string? ExampleTranslation { get; set; }

    public string Level { get; set; } = "high-school";

    public string Source { get; set; } = "curriculum-standard";

    public string? WordSource { get; set; }

    public string? MeaningSource { get; set; }

    public int SortOrder { get; set; }

    [JsonIgnore]
    public string ProgressKey => NormalizeKey(string.IsNullOrWhiteSpace(WordKey) ? Word : WordKey);

    [JsonIgnore]
    public string DisplayExample
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Example))
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(ExampleTranslation))
            {
                return Example!;
            }

            return $"{Example}{Environment.NewLine}{ExampleTranslation}";
        }
    }

    public WordItem Clone()
    {
        return new WordItem
        {
            Word = Word,
            WordKey = WordKey,
            Phonetic = Phonetic,
            Meaning = Meaning,
            Example = Example,
            ExampleTranslation = ExampleTranslation,
            Level = Level,
            Source = Source,
            WordSource = WordSource,
            MeaningSource = MeaningSource,
            SortOrder = SortOrder,
        };
    }

    public static string NormalizeKey(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text.Trim()
            .Replace('’', '\'')
            .Replace('‘', '\'')
            .Replace('（', '(')
            .Replace('）', ')')
            .ToLowerInvariant();

        return Regex.Replace(normalized, "\\s+", " ").Trim();
    }
}
