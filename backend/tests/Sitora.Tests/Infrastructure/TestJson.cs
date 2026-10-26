using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sitora.Tests.Infrastructure;

public static class TestJson
{
    // API отдаёт перечисления строками.
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
