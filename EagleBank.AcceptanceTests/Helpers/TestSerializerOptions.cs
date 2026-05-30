using System.Text.Json;
using System.Text.Json.Serialization;

namespace EagleBank.AcceptanceTests.Helpers;

public static class TestSerializerOptions
{
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
