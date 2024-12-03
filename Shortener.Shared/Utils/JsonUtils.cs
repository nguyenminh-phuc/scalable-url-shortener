using System.Text.Json;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace Shortener.Shared.Utils;

public static class JsonUtils
{
    // https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1869
    public static JsonSerializerOptions SerializerOptions { get; } =
        new JsonSerializerOptions().ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
}
