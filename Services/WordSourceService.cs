using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using DailyWords.Models;

namespace DailyWords.Services;

public sealed class WordSourceService
{
    private const string CurriculumListUrl = "https://www.cpsenglish.com/article/506";

    private readonly HttpClient _httpClient;
    private readonly WordEnrichmentService _wordEnrichmentService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public WordSourceService(WordEnrichmentService wordEnrichmentService)
    {
        _wordEnrichmentService = wordEnrichmentService;
        _httpClient = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        });
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DailyWords/1.0");
    }

    public async Task<IReadOnlyList<WordItem>> BuildWordBankAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var words = await FetchCurriculumWordListAsync(cancellationToken);
            return await _wordEnrichmentService.EnrichAsync(words, cancellationToken);
        }
        catch
        {
            return await LoadBundledFallbackAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<WordItem>> LoadBundledFallbackAsync(CancellationToken cancellationToken = default)
    {
        using var stream = BundledWordBankResourceService.OpenRead();
        return await JsonSerializer.DeserializeAsync<List<WordItem>>(stream, JsonOptions, cancellationToken) ?? [];
    }

    private async Task<IReadOnlyList<string>> FetchCurriculumWordListAsync(CancellationToken cancellationToken)
    {
        var html = await _httpClient.GetStringAsync(CurriculumListUrl, cancellationToken);
        var containerMatch = Regex.Match(html, "<div class=\"text-fmt\">(.*?)<div class=\"post-opt", RegexOptions.Singleline);
        if (!containerMatch.Success)
        {
            throw new InvalidOperationException("未找到在线词表内容。");
        }

        var paragraphs = Regex.Matches(containerMatch.Groups[1].Value, "<p[^>]*>(.*?)</p>", RegexOptions.Singleline);
        var words = new List<string>();

        foreach (Match paragraph in paragraphs)
        {
            var text = StripTags(paragraph.Groups[1].Value);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (text.StartsWith("2017 ", StringComparison.Ordinal))
            {
                continue;
            }

            if (text.StartsWith("1.", StringComparison.Ordinal) || text.StartsWith("2.", StringComparison.Ordinal))
            {
                continue;
            }

            if (text.TrimStart().StartsWith("附", StringComparison.Ordinal))
            {
                break;
            }

            if (Regex.IsMatch(text, "^[▲ ]*[A-Z][▲ ]*$"))
            {
                continue;
            }

            text = Regex.Replace(text, "\\s+([*])", "$1");
            text = Regex.Replace(text, "([A-Za-z.\\-/)])\\s+([*]{1,2})", "$1$2");
            text = Regex.Replace(text, "\\s+", " ").Trim();

            foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!part.Contains("版课标", StringComparison.Ordinal) && !part.Contains("英语词汇", StringComparison.Ordinal))
                {
                    words.Add(part);
                }
            }
        }

        if (words.Count != 3000)
        {
            throw new InvalidOperationException($"在线词表数量异常，当前获取到 {words.Count} 条。");
        }

        return words;
    }

    private static string StripTags(string fragment)
    {
        var text = Regex.Replace(fragment, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "</p>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        return Regex.Replace(text, "\\s+", " ").Trim();
    }
}
