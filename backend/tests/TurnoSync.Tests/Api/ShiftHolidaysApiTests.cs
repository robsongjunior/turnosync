using System.Net.Http.Json;
using TurnoSync.Application.Shifts;
using TurnoSync.Tests.Support;

namespace TurnoSync.Tests.Api;

public sealed class ShiftHolidaysApiTests : IDisposable
{
    private const int Ana = 1;  // ES
    private const int Joao = 2; // BR

    private readonly TurnoSyncFactory _factory = new();
    private readonly HttpClient _client;

    public ShiftHolidaysApiTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task CreateShiftAsync(int collaboratorId, string date, string start = "08:00", string end = "16:00")
    {
        var response = await _client.PostAsJsonAsync(
            "/api/shifts", new { collaboratorId, date, startTime = start, endTime = end });
        response.EnsureSuccessStatusCode();
    }

    private async Task<ShiftListResponse> ListAsync() =>
        (await _client.GetFromJsonAsync<ShiftListResponse>("/api/shifts", TestJson.Options))!;

    [Fact]
    public async Task List_ShouldMarkOnlyShiftsOnTheirOwnCountrysHoliday()
    {
        _factory.Holidays.SetHolidays("ES", 2026, new DateOnly(2026, 10, 12));
        await CreateShiftAsync(Ana, "2026-10-12");
        await CreateShiftAsync(Joao, "2026-10-12");

        var list = await ListAsync();

        Assert.True(list.HolidaysAvailable);
        Assert.Equal(
            [("Ana Torres", (bool?)true), ("João Lima", (bool?)false)],
            list.Items.Select(i => (i.CollaboratorName, i.IsHoliday)));
    }

    [Fact]
    public async Task List_ShouldUseTheStartDate_WhenNightShiftCrossesIntoOrOutOfAHoliday()
    {
        _factory.Holidays.SetHolidays("ES", 2026, new DateOnly(2026, 10, 13));
        await CreateShiftAsync(Ana, "2026-10-12", "22:00", "06:00"); // ends on the holiday, starts before it
        await CreateShiftAsync(Ana, "2026-10-13", "22:00", "06:00"); // starts on the holiday

        var list = await ListAsync();

        Assert.Equal([false, true], list.Items.Select(i => i.IsHoliday));
    }

    [Fact]
    public async Task List_ShouldReturnNullFlagOnlyForTheCountryThatFailed_AndKeepTheOthers()
    {
        _factory.Holidays.SetHolidays("ES", 2026, new DateOnly(2026, 10, 12));
        _factory.Holidays.SetUnavailable("BR", 2026);
        await CreateShiftAsync(Ana, "2026-10-12");
        await CreateShiftAsync(Joao, "2026-10-12");

        var list = await ListAsync();

        Assert.False(list.HolidaysAvailable);
        Assert.Equal(
            [("Ana Torres", (bool?)true), ("João Lima", (bool?)null)],
            list.Items.Select(i => (i.CollaboratorName, i.IsHoliday)));
    }

    [Fact]
    public async Task List_ShouldSerializeUnknownHolidayAsNull_NotFalse()
    {
        _factory.Holidays.SetUnavailable("BR", 2026);
        await CreateShiftAsync(Joao, "2026-10-12");

        var json = await _client.GetStringAsync("/api/shifts");

        Assert.Contains("\"isHoliday\":null", json);
        Assert.Contains("\"holidaysAvailable\":false", json);
    }

    [Fact]
    public async Task List_ShouldQueryEachCountryAndYearOnlyOnce()
    {
        await CreateShiftAsync(Ana, "2026-10-12");
        await CreateShiftAsync(Ana, "2026-10-13");
        await CreateShiftAsync(Ana, "2027-01-05");
        await CreateShiftAsync(Joao, "2026-10-12");

        await ListAsync();

        Assert.Equal(
            [("BR", 2026), ("ES", 2026), ("ES", 2027)],
            _factory.Holidays.Calls.Order().ToArray());
    }

    [Fact]
    public async Task List_ShouldReturnEmptyAndAvailable_WhenThereAreNoShifts()
    {
        var list = await ListAsync();

        Assert.Empty(list.Items);
        Assert.True(list.HolidaysAvailable);
        Assert.Empty(_factory.Holidays.Calls);
    }
}
