using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sitora.Infrastructure.Data.Configurations;

// Значение-объект в столбце jsonb (контур блока, уступы, план участка, ступени цены): сериализуется System.Text.Json в
// camelCase; изменения отслеживаются по JSON целиком (значение заменяется всегда целиком).
internal static class JsonColumns
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static PropertyBuilder<T?> HasJson<T>(this PropertyBuilder<T?> property) where T : class =>
        property.HasColumnType("jsonb").HasConversion(
            v => JsonSerializer.Serialize(v, Options),
            s => JsonSerializer.Deserialize<T>(s, Options),
            new ValueComparer<T?>(
                (a, b) => JsonSerializer.Serialize(a, Options) == JsonSerializer.Serialize(b, Options),
                v => StringComparer.Ordinal.GetHashCode(JsonSerializer.Serialize(v, Options)),
                v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, Options), Options)));
}
