using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using TurnoSync.Application.Shifts;
using TurnoSync.Tests.Support;

namespace TurnoSync.Tests.Api;

public sealed class ShiftsApiTests : IDisposable
{
    private readonly TurnoSyncFactory _factory = new();
    private readonly HttpClient _client;

    public ShiftsApiTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private Task<HttpResponseMessage> PostShiftAsync(int collaboratorId, string date, string start, string end) =>
        _client.PostAsJsonAsync("/api/shifts", new { collaboratorId, date, startTime = start, endTime = end });

    private async Task<ShiftResponse> CreateShiftAsync(int collaboratorId, string date, string start, string end)
    {
        var response = await PostShiftAsync(collaboratorId, date, start, end);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ShiftResponse>(TestJson.Options))!;
    }

    [Fact]
    public async Task Create_ShouldReturn201WithLocation_WhenValid()
    {
        var response = await PostShiftAsync(1, "2026-10-12", "08:00", "16:00");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ShiftResponse>(TestJson.Options);
        Assert.Equal(
            new ShiftResponse(created!.Id, 1, "Ana Torres", new DateOnly(2026, 10, 12), new TimeOnly(8, 0), new TimeOnly(16, 0)),
            created);
        Assert.EndsWith($"/api/shifts/{created.Id}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Create_ShouldSerializeTimesAsHoursAndMinutes()
    {
        await CreateShiftAsync(1, "2026-10-12", "08:00", "16:00");

        var json = await _client.GetStringAsync("/api/shifts/1");

        Assert.Contains("\"startTime\":\"08:00\"", json);
        Assert.Contains("\"endTime\":\"16:00\"", json);
    }

    [Theory]
    [InlineData("12:00", "20:00")] // solapamiento parcial al final
    [InlineData("06:00", "10:00")] // solapamiento parcial al inicio
    [InlineData("10:00", "12:00")] // contenido
    [InlineData("06:00", "18:00")] // contiene
    [InlineData("08:00", "16:00")] // idéntico
    public async Task Create_ShouldReturn409_WhenOverlapsAnExistingShiftOfTheSameCollaborator(string start, string end)
    {
        await CreateShiftAsync(1, "2026-10-12", "08:00", "16:00");

        var response = await PostShiftAsync(1, "2026-10-12", start, end);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Conflicto", problem!.Title);
        Assert.Equal(
            "El turno se solapa con un turno existente de Ana Torres (2026-10-12 08:00-16:00).",
            problem.Detail);
    }

    [Fact]
    public async Task Create_ShouldReturn201_WhenShiftsOnlyTouch()
    {
        await CreateShiftAsync(1, "2026-10-12", "08:00", "12:00");

        var response = await PostShiftAsync(1, "2026-10-12", "12:00", "16:00");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_ShouldReturn201_WhenSameTimeForDifferentCollaborators()
    {
        await CreateShiftAsync(1, "2026-10-12", "08:00", "16:00");

        var response = await PostShiftAsync(2, "2026-10-12", "08:00", "16:00");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_ShouldReturn409_WhenNightShiftReachesIntoNextDayShift()
    {
        await CreateShiftAsync(1, "2026-10-12", "22:00", "06:00");

        var response = await PostShiftAsync(1, "2026-10-13", "05:00", "09:00");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_ShouldReturn409_WhenNextDayNightShiftReachesBackIntoPreviousDayShift()
    {
        await CreateShiftAsync(1, "2026-10-13", "05:00", "09:00");

        var response = await PostShiftAsync(1, "2026-10-12", "22:00", "06:00");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_ShouldReturn201_WhenNightShiftEndsExactlyWhenNextDayShiftStarts()
    {
        await CreateShiftAsync(1, "2026-10-12", "22:00", "06:00");

        var response = await PostShiftAsync(1, "2026-10-13", "06:00", "10:00");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_ShouldReturn400OnEndTime_WhenStartEqualsEnd()
    {
        var response = await PostShiftAsync(1, "2026-10-12", "08:00", "08:00");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(["endTime"], problem!.Errors.Keys);
        Assert.Equal(["La hora de fin debe ser distinta de la hora de inicio."], problem.Errors["endTime"]);
    }

    [Fact]
    public async Task Create_ShouldReturn400ForEveryField_WhenBodyIsEmptyObject()
    {
        var response = await _client.PostAsJsonAsync("/api/shifts", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(["collaboratorId", "date", "endTime", "startTime"], problem!.Errors.Keys.Order());
    }

    [Theory]
    [InlineData("8am")]
    [InlineData("25:00")]
    [InlineData("")]
    public async Task Create_ShouldReturn400_WhenTimeFormatIsInvalid(string start)
    {
        var response = await PostShiftAsync(1, "2026-10-12", start, "16:00");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_ShouldAcceptStartAtMidnight()
    {
        var response = await PostShiftAsync(1, "2026-10-12", "00:00", "08:00");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_ShouldReturn404_WhenCollaboratorDoesNotExist()
    {
        var response = await PostShiftAsync(99, "2026-10-12", "08:00", "16:00");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("No se encontró el colaborador 99.", problem!.Detail);
    }

    [Fact]
    public async Task Get_ShouldReturn404_WhenShiftDoesNotExist()
    {
        var response = await _client.GetAsync("/api/shifts/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("No encontrado", problem!.Title);
        Assert.Equal("No se encontró el turno 999.", problem.Detail);
    }

    [Fact]
    public async Task List_ShouldBeOrderedByStartThenCollaboratorName()
    {
        await CreateShiftAsync(2, "2026-10-13", "08:00", "16:00");
        await CreateShiftAsync(3, "2026-10-12", "08:00", "16:00");
        await CreateShiftAsync(1, "2026-10-12", "08:00", "16:00");

        var list = await _client.GetFromJsonAsync<ShiftListResponse>("/api/shifts", TestJson.Options);

        Assert.Equal(
            ["Ana Torres", "Lucía Fernández", "João Lima"],
            list!.Items.Select(s => s.CollaboratorName));
    }

    [Fact]
    public async Task Update_ShouldReturn200_WhenNarrowingItsOwnTimeRange()
    {
        var shift = await CreateShiftAsync(1, "2026-10-12", "08:00", "16:00");

        var response = await _client.PutAsJsonAsync(
            $"/api/shifts/{shift.Id}", new { collaboratorId = 1, date = "2026-10-12", startTime = "09:00", endTime = "15:00" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ShiftResponse>(TestJson.Options);
        Assert.Equal(new TimeOnly(9, 0), updated!.StartTime);
        Assert.Equal(new TimeOnly(15, 0), updated.EndTime);
    }

    [Fact]
    public async Task Update_ShouldReturn409_WhenMovedOntoAnotherShift()
    {
        await CreateShiftAsync(1, "2026-10-12", "08:00", "12:00");
        var second = await CreateShiftAsync(1, "2026-10-12", "13:00", "17:00");

        var response = await _client.PutAsJsonAsync(
            $"/api/shifts/{second.Id}", new { collaboratorId = 1, date = "2026-10-12", startTime = "11:00", endTime = "17:00" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var unchanged = await _client.GetFromJsonAsync<ShiftResponse>($"/api/shifts/{second.Id}", TestJson.Options);
        Assert.Equal(new TimeOnly(13, 0), unchanged!.StartTime);
    }

    [Fact]
    public async Task Update_ShouldReturn409_WhenReassignedToACollaboratorWhoIsBusy()
    {
        await CreateShiftAsync(2, "2026-10-12", "08:00", "16:00");
        var shift = await CreateShiftAsync(1, "2026-10-12", "10:00", "14:00");

        var response = await _client.PutAsJsonAsync(
            $"/api/shifts/{shift.Id}", new { collaboratorId = 2, date = "2026-10-12", startTime = "10:00", endTime = "14:00" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_ShouldReturn400_WhenStartEqualsEnd()
    {
        var shift = await CreateShiftAsync(1, "2026-10-12", "08:00", "16:00");

        var response = await _client.PutAsJsonAsync(
            $"/api/shifts/{shift.Id}", new { collaboratorId = 1, date = "2026-10-12", startTime = "08:00", endTime = "08:00" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_ShouldReturn404_WhenShiftDoesNotExist()
    {
        var response = await _client.PutAsJsonAsync(
            "/api/shifts/999", new { collaboratorId = 1, date = "2026-10-12", startTime = "08:00", endTime = "16:00" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ShouldSoftDeleteAndFreeTheSlot()
    {
        var shift = await CreateShiftAsync(1, "2026-10-12", "08:00", "16:00");

        var deleted = await _client.DeleteAsync($"/api/shifts/{shift.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/shifts/{shift.Id}")).StatusCode);
        Assert.Empty((await _client.GetFromJsonAsync<ShiftListResponse>("/api/shifts", TestJson.Options))!.Items);
        Assert.Equal(HttpStatusCode.Created, (await PostShiftAsync(1, "2026-10-12", "08:00", "16:00")).StatusCode);
    }

    [Fact]
    public async Task Delete_ShouldReturn404_WhenShiftDoesNotExist()
    {
        var response = await _client.DeleteAsync("/api/shifts/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
