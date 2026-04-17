namespace DailyWords.Services;

public static class AppPaths
{
    public static string RootDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DailyWords");

    public static string DataDirectory => Path.Combine(RootDirectory, "data");

    public static string ConfigFilePath => Path.Combine(RootDirectory, "config.json");

    public static string RuntimeWordBankPath => Path.Combine(DataDirectory, "high_school_words.json");

    public static string BundledWordBankPath => Path.Combine(AppContext.BaseDirectory, "Data", "high_school_words.json");
}
