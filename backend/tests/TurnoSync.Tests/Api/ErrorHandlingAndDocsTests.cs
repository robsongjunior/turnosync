using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TurnoSync.Application.Collaborators;
using TurnoSync.Tests.Support;

namespace TurnoSync.Tests.Api;

public sealed class ErrorHandlingAndDocsTests : IDisposable
{
    private readonly TurnoSyncFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task UnexpectedException_ShouldReturn500ProblemDetailsWithoutLeakingDetails()
    {
        var failing = Substitute.For<ICollaboratorRepository>();
        failing.ListAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("secret db detail"));
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped<ICollaboratorRepository>(_ => failing)));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/collaborators");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("secret db detail", body);
        Assert.DoesNotContain("InvalidOperationException", body);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(500, problem!.Status);
    }

    [Fact]
    public async Task EmptyBody_ShouldReturn400_NotAServerError()
    {
        using var client = _factory.CreateClient();
        using var content = new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/collaborators", content);

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
    public async Task Swagger_ShouldDocumentCollaboratorAndShiftEndpoints()
    {
        using var client = _factory.CreateClient();

        var json = await client.GetStringAsync("/swagger/v1/swagger.json");

        Assert.Contains("\"/api/collaborators\"", json);
        Assert.Contains("\"/api/collaborators/{id}\"", json);
        Assert.Contains("\"/api/shifts\"", json);
        Assert.Contains("\"/api/shifts/{id}\"", json);
    }

    [Fact]
    public async Task SwaggerUi_ShouldBeServed()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
