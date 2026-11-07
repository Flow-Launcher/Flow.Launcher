using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Reads and writes <see cref="ResultPreview"/> with the registrations declared on the base type,
    /// so previews without one cannot break serialization.
    /// </summary>
    internal sealed class ResultPreviewConverter : JsonConverter<ResultPreview>
    {
        private static readonly HashSet<Type> RegisteredTypes = typeof(ResultPreview)
            .GetCustomAttributes<JsonDerivedTypeAttribute>()
            .Select(attribute => attribute.DerivedType)
            .ToHashSet();

        public override ResultPreview Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            using var document = JsonDocument.ParseValue(ref reader);
            return document.RootElement.Deserialize<ResultPreview>(options);
        }

        public override void Write(Utf8JsonWriter writer, ResultPreview value, JsonSerializerOptions options)
        {
            // A preview without a registration has no JSON form, for example one that holds a WPF control.
            // Dropping it keeps the rest of the result serializable.
            if (value is null || !RegisteredTypes.Contains(value.GetType()))
            {
                writer.WriteNullValue();
                return;
            }

            var preview = JsonSerializer.SerializeToElement(value, typeof(ResultPreview), options);
            preview.WriteTo(writer);
        }
    }
}
