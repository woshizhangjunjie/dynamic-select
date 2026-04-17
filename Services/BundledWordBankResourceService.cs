using System.Reflection;

namespace DailyWords.Services;

public static class BundledWordBankResourceService
{
    private const string ResourceName = "DailyWords.Data.high_school_words.json";

    public static Stream OpenRead()
    {
        if (File.Exists(AppPaths.BundledWordBankPath))
        {
            return File.OpenRead(AppPaths.BundledWordBankPath);
        }

        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
        if (stream is not null)
        {
            return stream;
        }

        throw new FileNotFoundException("未找到内置词库资源。");
    }
}
