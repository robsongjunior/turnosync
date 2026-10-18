using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace TurnoSync.Api.Common;

/// <summary>
/// ASP.NET Core redacta en inglés sus propios mensajes de error (cuerpo vacío, valores no válidos, JSON mal
/// formado, títulos de ProblemDetails). Aquí se sustituyen por mensajes en español para que ninguna respuesta
/// de error de la API salga en otro idioma. Los mensajes propios (validadores, servicios) ya nacen en español.
/// </summary>
public static class SpanishErrorMessages
{
    public const string ValidationTitle = "Se han producido uno o más errores de validación.";

    public const string UnexpectedErrorTitle = "Se ha producido un error inesperado.";

    // System.Text.Json termina sus mensajes con "Path: $.campo | LineNumber: n | BytePositionInLine: m." y nombra
    // tipos internos de .NET: no son mensajes para el cliente. Los de nuestros propios conversores no llevan esa cola.
    private const string JsonPositionMarker = "| LineNumber:";

    // Cuando MVC no puede leer el formulario (archivo que supera el límite de Kestrel, multipart dañado, cliente que
    // se desconecta) el mensaje empieza así y sigue con el detalle técnico, también en inglés.
    private const string FormReadFailureMarker = "Failed to read the request form.";

    public static IMvcBuilder AddSpanishErrorMessages(this IMvcBuilder builder) =>
        builder
            .AddMvcOptions(options =>
            {
                // BodyModelBinder ya rechaza un cuerpo vacío o "null"; el [Required] implícito solo añadiría un
                // error redundante sobre el parámetro ("request") a cada fallo de lectura del JSON.
                options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;

                LocalizeBindingMessages(options.ModelBindingMessageProvider);
            })
            .ConfigureApiBehaviorOptions(options =>
            {
                options.ClientErrorMapping[StatusCodes.Status400BadRequest].Title = "Solicitud incorrecta";
                options.ClientErrorMapping[StatusCodes.Status406NotAcceptable].Title = "No aceptable";
                options.ClientErrorMapping[StatusCodes.Status415UnsupportedMediaType].Title = "Tipo de medio no admitido";

                // Se parte de la respuesta por defecto (conserva type, traceId y content types) y solo se traduce.
                var defaultFactory = options.InvalidModelStateResponseFactory;
                options.InvalidModelStateResponseFactory = context =>
                {
                    var result = defaultFactory(context);
                    if (result is ObjectResult { Value: ValidationProblemDetails problem })
                    {
                        Localize(problem);
                    }

                    return result;
                };
            });

    private static void LocalizeBindingMessages(DefaultModelBindingMessageProvider messages)
    {
        messages.SetMissingBindRequiredValueAccessor(name => $"No se ha proporcionado ningún valor para '{name}'.");
        messages.SetMissingKeyOrValueAccessor(() => "Se requiere un valor.");
        messages.SetMissingRequestBodyRequiredValueAccessor(() => "El cuerpo de la solicitud es obligatorio.");
        messages.SetValueMustNotBeNullAccessor(value => $"El valor '{value}' no es válido.");
        messages.SetAttemptedValueIsInvalidAccessor((value, field) => $"El valor '{value}' no es válido para {field}.");
        messages.SetNonPropertyAttemptedValueIsInvalidAccessor(value => $"El valor '{value}' no es válido.");
        messages.SetUnknownValueIsInvalidAccessor(field => $"El valor proporcionado no es válido para {field}.");
        messages.SetNonPropertyUnknownValueIsInvalidAccessor(() => "El valor proporcionado no es válido.");
        messages.SetValueIsInvalidAccessor(value => $"El valor '{value}' no es válido.");
        messages.SetValueMustBeANumberAccessor(field => $"El campo {field} debe ser un número.");
        messages.SetNonPropertyValueMustBeANumberAccessor(() => "El campo debe ser un número.");
    }

    private static void Localize(ValidationProblemDetails problem)
    {
        problem.Title = ValidationTitle;

        foreach (var (field, messages) in problem.Errors.ToList())
        {
            problem.Errors[field] = messages.Select(message => Localize(field, message)).Distinct().ToArray();
        }
    }

    private static string Localize(string field, string message)
    {
        if (message.Contains(JsonPositionMarker, StringComparison.Ordinal))
        {
            return DescribeJsonError(field);
        }

        return message.StartsWith(FormReadFailureMarker, StringComparison.Ordinal)
            ? "No se pudo leer el formulario de la solicitud: el archivo es demasiado grande o la solicitud está dañada."
            : message;
    }

    /// <summary>El campo de un error de JSON es su ruta: "$" es el cuerpo entero y "$.date" una propiedad.</summary>
    private static string DescribeJsonError(string jsonPath) =>
        jsonPath.StartsWith("$.", StringComparison.Ordinal)
            ? $"El valor de '{jsonPath[2..]}' no es válido."
            : "El cuerpo de la solicitud no tiene un formato JSON válido.";
}
