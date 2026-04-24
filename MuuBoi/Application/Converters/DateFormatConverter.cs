using System.Text.Json;
using System.Text.Json.Serialization;

namespace MuuBoi.Converters
{
    public class DateFormatConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => DateTime.Parse(reader.GetString()!);

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString("dd/MM/yyyy"));
    }

    public class NullableDateFormatConverter : JsonConverter<DateTime?>
    {
        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => reader.TokenType == JsonTokenType.Null ? null : DateTime.Parse(reader.GetString()!);

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value == null) writer.WriteNullValue();
            else writer.WriteStringValue(value.Value.ToString("dd/MM/yyyy"));
        }
    }
}
