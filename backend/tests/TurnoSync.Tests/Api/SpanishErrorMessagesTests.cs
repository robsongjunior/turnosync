using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using TurnoSync.Tests.Support;

namespace TurnoSync.Tests.Api;

/// <summary>
/// ASP.NET Core y System.Text.Json redactan en inglés sus propios errores (cuerpo vacío, JSON mal formado, valores
/// no válidos...). Ninguna respuesta de error de la API debe salir en otro idioma ni filtrar nombres de tipos de .NET.
/// </summary>
public sealed class SpanishErrorMessagesTests : IDisposable
{
    private const string ValidationTitle = "Se han producido uno o más errores de validación.";

    private readonly TurnoSyncFactory _factory = new();
    private readonly HttpClient _client;

    public SpanishErrorMessagesTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<ValidationProblemDetails> ExpectBadRequestAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(ValidationTitle, problem!.Title);
        return problem;
    }

    private async Task<ValidationProblemDetails> PostJsonAsync(string url, string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await ExpectBadRequestAsync(await _client.PostAsync(url, content));
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    public async Task EmptyBody_ShouldReturnASingleSpanishError(string body)
    {
        var problem = await PostJsonAsync("/api/shifts", body);

        // Un solo error: sin el "request" redundante que añadiría el [Required] implícito.
        var error = Assert.Single(problem.Errors);
        Assert.Equal(string.Empty, error.Key);
        Assert.Equal(["El cuerpo de la solicitud es obligatorio."], error.Value);
    }

    [Fact]
    public async Task MalformedJson_ShouldReturnAGenericSpanishMessage()
    {
        var problem = await PostJsonAsync("/api/shifts", "{ not json");

        var error = Assert.Single(problem.Errors);
        Assert.Equal("$", error.Key);
        Assert.Equal(["El cuerpo de la solicitud no tiene un formato JSON válido."], error.Value);
    }

    [Theory]
    [InlineData("/api/shifts", """{"collaboratorId":1,"date":"2026-13-45","startTime":"08:00","endTime":"16:00"}""", "date")]
    [InlineData("/api/shifts", """{"collaboratorId":"abc","date":"2026-10-12","startTime":"08:00","endTime":"16:00"}""", "collaboratorId")]
    [InlineData("/api/collaborators", """{"name":5,"email":"ana@turnosync.dev","countryCode":"ES"}""", "name")]
    public async Task WrongJsonValue_ShouldNameTheFieldInSpanish_WithoutLeakingDotNetTypes(string url, string json, string field)
    {
        var problem = await PostJsonAsync(url, json);

        var error = Assert.Single(problem.Errors);
        Assert.Equal($"$.{field}", error.Key);
        Assert.Equal([$"El valor de '{field}' no es válido."], error.Value);
    }

    [Fact]
    public async Task InvalidTime_ShouldKeepTheMessageOfTheTimeConverter()
    {
        var problem = await PostJsonAsync(
            "/api/shifts", """{"collaboratorId":1,"date":"2026-10-12","startTime":"25:00","endTime":"16:00"}""");

        Assert.Equal(["La hora debe tener el formato HH:mm."], problem.Errors["$.startTime"]);
    }

    [Fact]
    public async Task InvalidQueryValue_ShouldReturnTheSpanishBindingMessage()
    {
        var problem = await ExpectBadRequestAsync(
            await _client.GetAsync("/api/shifts/export?from=yesterday&to=2026-10-12"));

        Assert.Equal(["El valor 'yesterday' no es válido."], problem.Errors["from"]);
    }

    [Fact]
    public async Task UnreadableForm_ShouldReturnASpanishMessage()
    {
        // Un multipart sin "boundary" no se puede leer: MVC lo informa en inglés ("Failed to read the request form...").
        using var content = new StringContent("sin boundary", Encoding.UTF8, "multipart/form-data");

        var problem = await ExpectBadRequestAsync(await _client.PostAsync("/api/shifts/import", content));

        Assert.Equal(
            ["No se pudo leer el formulario de la solicitud: el archivo es demasiado grande o la solicitud está dañada."],
            problem.Errors[""]);
    }

    [Fact]
    public async Task UnsupportedMediaType_ShouldHaveASpanishTitle()
    {
        using var content = new StringContent("hola", Encoding.UTF8, "text/plain");

        var response = await _client.PostAsync("/api/shifts", content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Tipo de medio no admitido", problem!.Title);
    }

    [Fact]
    public async Task TranslatedErrors_ShouldKeepTheTypeAndTraceIdOfTheDefaultResponse()
    {
        using var content = new StringContent("{ not json", Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/shifts", content);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem!.Type);
        Assert.Contains("traceId", problem.Extensions.Keys);
    }
}
