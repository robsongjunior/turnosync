using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TurnoSync.Api.Common;

/// <summary>
/// Horários trafegam como HH:mm (o padrão do System.Text.Json é HH:mm:ss). Na leitura, segundos são
/// aceitos mas descartados.
/// </summary>
public sealed class TimeOnlyJsonConverter : JsonConverter<TimeOnly>
{
    private const string Format = "HH:mm";

    private static readonly string[] AcceptedFormats = [Format, "HH:mm:ss"];

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString();

        if (text is not null
            && TimeOnly.TryParseExact(text, AcceptedFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            return new TimeOnly(time.Hour, time.Minute);
        }

        throw new JsonException("The time must use the HH:mm format.");
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
}
