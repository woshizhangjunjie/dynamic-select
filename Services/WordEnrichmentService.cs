using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using DailyWords.Models;

namespace DailyWords.Services;

public sealed class WordEnrichmentService
{
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _lookupLock = new(1, 1);
    private Dictionary<string, WordItem>? _bundledLookup;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public WordEnrichmentService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<WordItem>> EnrichAsync(IReadOnlyList<string> displayWords, CancellationToken cancellationToken = default)
    {
        var lookup = await LoadBundledLookupAsync(cancellationToken);
        var result = new List<WordItem>(displayWords.Count);

        foreach (var displayWord in displayWords)
        {
            result.Add(await EnrichSingleAsync(displayWord, lookup, cancellationToken));
        }

        return result;
    }

    private async Task<WordItem> EnrichSingleAsync(
        string displayWord,
        IReadOnlyDictionary<string, WordItem> lookup,
        CancellationToken cancellationToken)
    {
        foreach (var candidate in BuildCandidates(displayWord))
        {
            if (lookup.TryGetValue(candidate, out var wordItem))
            {
                var clone = wordItem.Clone();
                clone.Word = StripStars(displayWord);
                clone.WordKey = wordItem.WordKey;
                clone.Source = "curriculum-standard";
                clone.WordSource = "cpsenglish-2017-3000";
                return clone;
            }
        }

        var fallback = await TryLoadFromDictionaryApiAsync(displayWord, cancellationToken);
        return fallback ?? CreateBareWordItem(displayWord);
    }

    private async Task<Dictionary<string, WordItem>> LoadBundledLookupAsync(CancellationToken cancellationToken)
    {
        if (_bundledLookup is not null)
        {
            return _bundledLookup;
        }

        await _lookupLock.WaitAsync(cancellationToken);
        try
        {
            if (_bundledLookup is not null)
            {
                return _bundledLookup;
            }

            using var stream = BundledWordBankResourceService.OpenRead();
            var items = await JsonSerializer.DeserializeAsync<List<WordItem>>(stream, JsonOptions, cancellationToken) ?? [];
            _bundledLookup = items
                .GroupBy(item => item.ProgressKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            return _bundledLookup;
        }
        finally
        {
            _lookupLock.Release();
        }
    }

    private async Task<WordItem?> TryLoadFromDictionaryApiAsync(string displayWord, CancellationToken cancellationToken)
    {
        var requestWord = BuildCandidates(displayWord).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(requestWord) || requestWord.Contains(' ') || requestWord.Contains('/'))
        {
            return null;
        }

        try
        {
            using var response = await _httpClient.GetAsync(
                $"https://api.dictionaryapi.dev/api/v2/entries/en/{Uri.EscapeDataString(requestWord)}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
            {
                return null;
            }

            var entry = root[0];
            var phonetic = string.Empty;
            if (entry.TryGetProperty("phonetic", out var phoneticNode))
            {
                phonetic = phoneticNode.GetString() ?? string.Empty;
            }

            var example = string.Empty;
            if (entry.TryGetProperty("meanings", out var meaningsNode) && meaningsNode.ValueKind == JsonValueKind.Array)
            {
                foreach (var meaningNode in meaningsNode.EnumerateArray())
                {
                    if (!meaningNode.TryGetProperty("definitions", out var definitionsNode) || definitionsNode.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var definitionNode in definitionsNode.EnumerateArray())
                    {
                        if (definitionNode.TryGetProperty("example", out var exampleNode))
                        {
                            example = exampleNode.GetString() ?? string.Empty;
                        }

                        if (!string.IsNullOrWhiteSpace(example))
                        {
                            break;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(example))
                    {
                        break;
                    }
                }
            }

            return new WordItem
            {
                Word = StripStars(displayWord),
                WordKey = requestWord,
                Phonetic = string.IsNullOrWhiteSpace(phonetic) ? null : phonetic,
                Meaning = string.Empty,
                Example = string.IsNullOrWhiteSpace(example) ? null : example,
                ExampleTranslation = null,
                Level = "high-school",
                Source = "curriculum-standard",
                WordSource = "cpsenglish-2017-3000",
                MeaningSource = "dictionaryapi",
            };
        }
        catch
        {
            return null;
        }
    }

    private static WordItem CreateBareWordItem(string displayWord)
    {
        var word = StripStars(displayWord);
        var key = BuildCandidates(displayWord).FirstOrDefault() ?? WordItem.NormalizeKey(word);
        return new WordItem
        {
            Word = word,
            WordKey = key,
            Meaning = string.Empty,
            Level = "high-school",
            Source = "curriculum-standard",
            WordSource = "cpsenglish-2017-3000",
            MeaningSource = "none",
        };
    }

    private static IEnumerable<string> BuildCandidates(string displayWord)
    {
        var clean = StripStars(displayWord)
            .Replace("（", "(")
            .Replace("）", ")")
            .Replace("’", "'")
            .Trim();

        var candidates = new List<string>();
        if (string.Equals(clean, "a/an", StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add("a");
            candidates.Add("an");
        }

        var primary = Regex.Replace(clean, "\\([^)]*\\)", string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(primary))
        {
            candidates.Add(primary);
        }

        foreach (Match match in Regex.Matches(clean, "\\(([^)]*)\\)"))
        {
            foreach (var part in match.Groups[1].Value.Split([',', '/', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Regex.IsMatch(part, "^[A-Za-z.\\- ']+$"))
                {
                    candidates.Add(part);
                }
            }
        }

        var expanded = new List<string>();
        foreach (var item in candidates)
        {
            expanded.Add(item);
            if (item.EndsWith("ise", StringComparison.OrdinalIgnoreCase))
            {
                expanded.Add(item[..^3] + "ize");
            }

            if (item.EndsWith("yse", StringComparison.OrdinalIgnoreCase))
            {
                expanded.Add(item[..^3] + "yze");
            }

            if (item.EndsWith("our", StringComparison.OrdinalIgnoreCase))
            {
                expanded.Add(item[..^3] + "or");
            }

            if (string.Equals(item, "licence", StringComparison.OrdinalIgnoreCase))
            {
                expanded.Add("license");
            }

            if (string.Equals(item, "café", StringComparison.OrdinalIgnoreCase))
            {
                expanded.Add("cafe");
            }

            if (string.Equals(item, "o’clock", StringComparison.OrdinalIgnoreCase))
            {
                expanded.Add("o'clock");
            }

            if (item.StartsWith("criterion", StringComparison.OrdinalIgnoreCase))
            {
                expanded.Add("criterion");
            }
        }

        return expanded
            .Select(WordItem.NormalizeKey)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static string StripStars(string displayWord)
    {
        return Regex.Replace(displayWord, "\\*+$", string.Empty).Trim();
    }
}
