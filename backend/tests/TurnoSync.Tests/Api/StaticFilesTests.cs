using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using TurnoSync.Tests.Support;

namespace TurnoSync.Tests.Api;

/// <summary>La API sirve el build de Angular (wwwroot) desde su propio origen, sin fallback de SPA.</summary>
public sealed class StaticFilesTests : IDisposable
{
    private readonly string _webRoot = Directory.CreateTempSubdirectory("turnosync-wwwroot-").FullName;
    private readonly TurnoSyncFactory _factory = new();

    public StaticFilesTests() =>
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<html><body><app-root></app-root></body></html>");

    public void Dispose()
    {
        _factory.Dispose();
        Directory.Delete(_webRoot, recursive: true);
    }

    private HttpClient CreateClient() =>
        _factory.WithWebHostBuilder(builder => builder.UseWebRoot(_webRoot)).CreateClient();

    [Fact]
    public async Task Root_ShouldServeTheFrontendIndex()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<app-root>", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UnknownApiRoute_ShouldStayNotFound_InsteadOfFallingBackToTheFrontend()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Api_ShouldKeepWorking_WhenTheFrontendIsServed()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/api/collaborators");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
