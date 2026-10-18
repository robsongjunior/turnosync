using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using TurnoSync.Application.Collaborators;
using TurnoSync.Application.Shifts;
using TurnoSync.Tests.Support;

namespace TurnoSync.Tests.Api;

public sealed class CollaboratorsApiTests : IDisposable
{
    private readonly TurnoSyncFactory _factory = new();
    private readonly HttpClient _client;

    public CollaboratorsApiTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private Task<HttpResponseMessage> PostAsync(string name, string email, string country) =>
        _client.PostAsJsonAsync("/api/collaborators", new { name, email, countryCode = country });

    [Fact]
    public async Task List_ShouldReturnSeededCollaborators()
    {
        var collaborators = await _client.GetFromJsonAsync<List<CollaboratorResponse>>(
            "/api/collaborators", TestJson.Options);

        Assert.Equal(["ES", "BR", "AR", "MX", "PT"], collaborators!.Select(c => c.CountryCode));
    }

    [Fact]
    public async Task Create_ShouldReturn201WithNormalizedData_WhenValid()
    {
        var response = await PostAsync("  Marta Ruiz ", "Marta.Ruiz@TurnoSync.dev", "es");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CollaboratorResponse>(TestJson.Options);
        Assert.Equal(new CollaboratorResponse(created!.Id, "Marta Ruiz", "marta.ruiz@turnosync.dev", "ES"), created);
        Assert.EndsWith($"/api/collaborators/{created.Id}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Create_ShouldReturn409_WhenEmailAlreadyExistsIgnoringCase()
    {
        var response = await PostAsync("Another Ana", "ANA@turnosync.dev", "ES");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Conflicto", problem!.Title);
        Assert.Equal("Ya existe un colaborador con el email 'ana@turnosync.dev'.", problem.Detail);
    }

    [Fact]
    public async Task Create_ShouldReturn400WithFieldErrors_WhenDataIsInvalid()
    {
        var response = await PostAsync("", "not-an-email", "ZZ");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(["countryCode", "email", "name"], problem!.Errors.Keys.Order());
    }

    [Fact]
    public async Task Create_ShouldReturnErrorsInSpanish()
    {
        var response = await PostAsync("", "ana@turnosync.dev", "ES");

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal("Se han producido uno o más errores de validación.", problem!.Title);
        Assert.Equal(["El nombre es obligatorio."], problem.Errors["name"]);
    }

    [Fact]
    public async Task Get_ShouldReturn404_WhenCollaboratorDoesNotExist()
    {
        var response = await _client.GetAsync("/api/collaborators/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("No encontrado", problem!.Title);
        Assert.Equal("No se encontró el colaborador 999.", problem.Detail);
    }

    [Fact]
    public async Task Update_ShouldReturn200_WhenKeepingItsOwnEmail()
    {
        var response = await _client.PutAsJsonAsync(
            "/api/collaborators/1", new { name = "Ana T.", email = "ana@turnosync.dev", countryCode = "PT" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<CollaboratorResponse>(TestJson.Options);
        Assert.Equal(new CollaboratorResponse(1, "Ana T.", "ana@turnosync.dev", "PT"), updated);
    }

    [Fact]
    public async Task Update_ShouldReturn409_WhenEmailBelongsToAnotherCollaborator()
    {
        var response = await _client.PutAsJsonAsync(
            "/api/collaborators/1", new { name = "Ana", email = "joao@turnosync.dev", countryCode = "ES" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_ShouldReturn404_WhenCollaboratorDoesNotExist()
    {
        var response = await _client.PutAsJsonAsync(
            "/api/collaborators/999", new { name = "Ghost", email = "ghost@turnosync.dev", countryCode = "ES" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ShouldSoftDeleteCollaboratorAndItsShiftsAndFreeTheEmail()
    {
        await _client.PostAsJsonAsync("/api/shifts", new { collaboratorId = 1, date = "2026-10-12", startTime = "08:00", endTime = "16:00" });
        await _client.PostAsJsonAsync("/api/shifts", new { collaboratorId = 2, date = "2026-10-12", startTime = "08:00", endTime = "16:00" });

        var deleted = await _client.DeleteAsync("/api/collaborators/1");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/collaborators/1")).StatusCode);
        var list = await _client.GetFromJsonAsync<ShiftListResponse>("/api/shifts", TestJson.Options);
        Assert.Equal([2], list!.Items.Select(s => s.CollaboratorId));
        Assert.Equal(HttpStatusCode.Created, (await PostAsync("New Ana", "ana@turnosync.dev", "ES")).StatusCode);
    }

    [Fact]
    public async Task Delete_ShouldReturn404_WhenCollaboratorDoesNotExist()
    {
        var response = await _client.DeleteAsync("/api/collaborators/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
