using System.Text.Json;
using DailyWords.Models;

namespace DailyWords.Services;

public sealed class WordRepository
{
    private readonly WordSourceService _wordSourceService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public WordRepository(WordSourceService wordSourceService)
    {
        _wordSourceService = wordSourceService;
    }

    public async Task<IReadOnlyList<WordItem>> LoadAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);

        if (!File.Exists(AppPaths.RuntimeWordBankPath))
        {
            var builtWords = await _wordSourceService.BuildWordBankAsync(cancellationToken);
            await SaveWordBankAsync(builtWords, cancellationToken);
            return builtWords;
        }

        return await ReadWordBankAsync(AppPaths.RuntimeWordBankPath, cancellationToken);
    }

    public async Task<IReadOnlyList<WordItem>> RebuildAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);
        var builtWords = await _wordSourceService.BuildWordBankAsync(cancellationToken);
        await SaveWordBankAsync(builtWords, cancellationToken);
        return builtWords;
    }

    private async Task<IReadOnlyList<WordItem>> ReadWordBankAsync(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<List<WordItem>>(stream, JsonOptions, cancellationToken) ?? [];
    }

    private async Task SaveWordBankAsync(IReadOnlyList<WordItem> items, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.RuntimeWordBankPath)!);
        await using var stream = File.Create(AppPaths.RuntimeWordBankPath);
        await JsonSerializer.SerializeAsync(stream, items, JsonOptions, cancellationToken);
    }
}
