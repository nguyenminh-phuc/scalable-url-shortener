using System.Text;

namespace Shortener.Shared.Utils;

public static class Base64Utils
{
    public static string Encode(string plainText)
    {
        byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);
        return Convert.ToBase64String(plainTextBytes);
    }

    public static string Decode(string encodedData)
    {
        byte[] encodedBytes = Convert.FromBase64String(encodedData);
        return Encoding.UTF8.GetString(encodedBytes);
    }
}
