using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Flow.Launcher.Core.Resource
{
    public class JsonObjectConverter : JsonConverter<object>
    {
        public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.True:
                    return true;
                case JsonTokenType.False:
                    return false;
                case JsonTokenType.Number when reader.TryGetInt32(out var i):
                    return i;
                case JsonTokenType.Number when reader.TryGetInt64(out var l):
                    return l;
                case JsonTokenType.Number:
                    return reader.GetDouble();
                case JsonTokenType.String when reader.TryGetDateTime(out DateTime datetime):
                    return datetime;
                case JsonTokenType.String:
                    return reader.GetString();
                default:
                    // Use JsonElement as fallback.
                    // Newtonsoft uses JArray or JObject.
                    using (var document = JsonDocument.ParseValue(ref reader))
                    {
                        return document.RootElement.Clone();
                    }
            }
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            throw new InvalidOperationException("Should not get here.");
        }
    }
}