using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TurnoSync.Api.Common;

/// <summary>
/// Las horas viajan como HH:mm (System.Text.Json usa HH:mm:ss por defecto). Al leer se aceptan los
/// segundos, pero se descartan.
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

        throw new JsonException("La hora debe tener el formato HH:mm.");
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
}
