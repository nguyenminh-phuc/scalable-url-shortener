namespace Shortener.Shared.Utils;

public static class RandomUtils
{
    private static readonly Random s_random = new();

    public static string String(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, length).Select(s => s[s_random.Next(s.Length)]).ToArray());
    }
}
