namespace AllRelicsBecomeOneRelic;

internal static class ModLog
{
    internal static void Info(string message)
    {
        Console.WriteLine($"[{ModEntry.ModFileStem}] {message}");
    }

    internal static void Warn(string message)
    {
        Console.WriteLine($"[{ModEntry.ModFileStem}] WARN: {message}");
    }
}
