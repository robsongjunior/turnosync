using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TurnoSync.Application.Employees;
using TurnoSync.Tests.Support;

namespace TurnoSync.Tests.Api;

public sealed class ErrorHandlingAndDocsTests : IDisposable
{
    private readonly TurnoSyncFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task UnexpectedException_ShouldReturn500ProblemDetailsWithoutLeakingDetails()
    {
        var failing = Substitute.For<IEmployeeRepository>();
        failing.ListAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("secret db detail"));
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped<IEmployeeRepository>(_ => failing)));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/employees");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("secret db detail", body);
        Assert.DoesNotContain("InvalidOperationException", body);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(500, problem!.Status);
        Assert.Equal("Se ha producido un error inesperado.", problem.Title);
    }

    [Fact]
    public async Task ClientRequestFailure_ShouldKeepItsStatusAndReturnASpanishTitle()
    {
        var failing = Substitute.For<IEmployeeRepository>();
        failing.ListAsync(Arg.Any<CancellationToken>()).ThrowsAsync(
            new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge));
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped<IEmployeeRepository>(_ => failing)));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/employees");

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("No se pudo procesar la solicitud.", problem!.Title);
    }

    [Fact]
    public async Task EmptyBody_ShouldReturn400_NotAServerError()
    {
        using var client = _factory.CreateClient();
        using var content = new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/employees", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MalformedJson_ShouldReturn400()
    {
        using var client = _factory.CreateClient();
        using var content = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/shifts", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnknownApiRoute_ShouldReturn404()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_ShouldDocumentEmployeeAndShiftEndpoints()
    {
        using var client = _factory.CreateClient();

        var json = await client.GetStringAsync("/swagger/v1/swagger.json");

        Assert.Contains("\"/api/employees\"", json);
        Assert.Contains("\"/api/employees/{id}\"", json);
        Assert.Contains("\"/api/shifts\"", json);
        Assert.Contains("\"/api/shifts/{id}\"", json);
        Assert.Contains("\"/api/shifts/import\"", json);
        Assert.Contains("\"/api/shifts/export\"", json);
        Assert.Contains("multipart/form-data", json);
    }

    [Fact]
    public async Task Swagger_ShouldDescribeTheEndpointsInSpanish()
    {
        using var client = _factory.CreateClient();

        var json = await client.GetStringAsync("/swagger/v1/swagger.json");

        Assert.Contains("\"summary\": \"Lista los colaboradores.\"", json);
        Assert.Contains("\"summary\": \"Obtiene un turno.\"", json);
        Assert.Contains("La hoja de cálculo .xlsx.", json);
    }

    [Fact]
    public async Task SwaggerUi_ShouldBeServed()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
