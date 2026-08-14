namespace ArchipelaWoW.QuestExtractor.Services;

public static class Env
{
    public static string GetString(string key)
    {
        string value = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public static int? GetInt(string key)
    {
        string value = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(value) ? null : int.Parse(value);
    }
}
