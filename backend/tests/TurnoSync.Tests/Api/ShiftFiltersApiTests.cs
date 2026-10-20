using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using TurnoSync.Application.Shifts;
using TurnoSync.Tests.Support;

namespace TurnoSync.Tests.Api;

public sealed class ShiftFiltersApiTests : IDisposable
{
    private const int Ana = 1;  // ES
    private const int Joao = 2; // BR

    private readonly TurnoSyncFactory _factory = new();
    private readonly HttpClient _client;

    public ShiftFiltersApiTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task CreateShiftAsync(int employeeId, string date, string start = "08:00", string end = "16:00")
    {
        var response = await _client.PostAsJsonAsync(
            "/api/shifts", new { employeeId, date, startTime = start, endTime = end });
        response.EnsureSuccessStatusCode();
    }

    private async Task<ShiftListResponse> ListAsync(string query) =>
        (await _client.GetFromJsonAsync<ShiftListResponse>($"/api/shifts{query}", TestJson.Options))!;

    private static IEnumerable<(int, string)> Keys(ShiftListResponse list) =>
        list.Items.Select(i => (i.EmployeeId, i.Date.ToString("yyyy-MM-dd")));

    [Fact]
    public async Task List_ShouldReturnEverything_WhenThereAreNoFilters()
    {
        await CreateShiftAsync(Ana, "2026-10-12");
        await CreateShiftAsync(Joao, "2026-10-13");

        var list = await ListAsync("");

        Assert.Equal(2, list.Items.Count);
    }

    [Fact]
    public async Task List_ShouldFilterByEmployee()
    {
        await CreateShiftAsync(Ana, "2026-10-12");
        await CreateShiftAsync(Joao, "2026-10-12");
        await CreateShiftAsync(Ana, "2026-10-13");

        var list = await ListAsync($"?employeeId={Ana}");

        Assert.Equal([(Ana, "2026-10-12"), (Ana, "2026-10-13")], Keys(list));
    }

    [Fact]
    public async Task List_ShouldReturnAnEmptyList_WhenTheEmployeeDoesNotExist()
    {
        await CreateShiftAsync(Ana, "2026-10-12");

        var list = await ListAsync("?employeeId=999");

        Assert.Empty(list.Items);
    }

    [Fact]
    public async Task List_ShouldFilterByStartDate_InclusiveOnBothEnds()
    {
        await CreateShiftAsync(Ana, "2026-10-11");
        await CreateShiftAsync(Ana, "2026-10-12");                   // límite inferior
        await CreateShiftAsync(Ana, "2026-10-14", "22:00", "06:00"); // límite superior (termina el 15)
        await CreateShiftAsync(Ana, "2026-10-15");

        var list = await ListAsync("?from=2026-10-12&to=2026-10-14");

        Assert.Equal([(Ana, "2026-10-12"), (Ana, "2026-10-14")], Keys(list));
    }

    [Theory]
    [InlineData("?from=2026-10-13", new[] { "2026-10-13", "2026-10-14" })]
    [InlineData("?to=2026-10-13", new[] { "2026-10-12", "2026-10-13" })]
    public async Task List_ShouldLeaveThePeriodOpen_WhenOnlyOneDateIsGiven(string query, string[] expectedDates)
    {
        await CreateShiftAsync(Ana, "2026-10-12");
        await CreateShiftAsync(Ana, "2026-10-13");
        await CreateShiftAsync(Ana, "2026-10-14");

        var list = await ListAsync(query);

        Assert.Equal(expectedDates, list.Items.Select(i => i.Date.ToString("yyyy-MM-dd")));
    }

    [Fact]
    public async Task List_ShouldReturn400_WhenFromIsAfterTo()
    {
        var response = await _client.GetAsync("/api/shifts?from=2026-10-14&to=2026-10-12");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(["El parámetro 'from' debe ser anterior o igual a 'to'."], problem!.Errors["from"]);
    }

    [Theory]
    [InlineData("?from=ayer")]
    [InlineData("?holiday=quizas")]
    [InlineData("?employeeId=ana")]
    public async Task List_ShouldReturn400_WhenAFilterIsMalformed(string query)
    {
        var response = await _client.GetAsync($"/api/shifts{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(true, Ana)]
    [InlineData(false, Joao)]
    public async Task List_ShouldFilterByHoliday(bool holiday, int expectedEmployee)
    {
        _factory.Holidays.SetHolidays("ES", 2026, new DateOnly(2026, 10, 12));
        await CreateShiftAsync(Ana, "2026-10-12");
        await CreateShiftAsync(Joao, "2026-10-12");

        var list = await ListAsync($"?holiday={holiday}");

        Assert.Equal([(expectedEmployee, "2026-10-12")], Keys(list));
        Assert.True(list.HolidaysAvailable);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task List_ShouldLeaveUnknownHolidaysOutOfBothFilters_AndWarn(bool holiday)
    {
        _factory.Holidays.SetHolidays("ES", 2026, new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 13));
        _factory.Holidays.SetUnavailable("BR", 2026);
        await CreateShiftAsync(Ana, "2026-10-12");
        await CreateShiftAsync(Ana, "2026-10-14");
        await CreateShiftAsync(Joao, "2026-10-12");

        var list = await ListAsync($"?holiday={holiday}");

        Assert.DoesNotContain(list.Items, i => i.EmployeeId == Joao);
        Assert.Single(list.Items);
        Assert.False(list.HolidaysAvailable);
    }

    [Fact]
    public async Task List_ShouldCombineAllFilters()
    {
        _factory.Holidays.SetHolidays("ES", 2026, new DateOnly(2026, 10, 12), new DateOnly(2026, 11, 1));
        _factory.Holidays.SetHolidays("BR", 2026, new DateOnly(2026, 10, 12));
        await CreateShiftAsync(Ana, "2026-10-12");  // cumple todo
        await CreateShiftAsync(Ana, "2026-10-13");  // no es festivo
        await CreateShiftAsync(Ana, "2026-11-01");  // fuera del período
        await CreateShiftAsync(Joao, "2026-10-12"); // otro colaborador

        var list = await ListAsync($"?employeeId={Ana}&from=2026-10-01&to=2026-10-31&holiday=true");

        Assert.Equal([(Ana, "2026-10-12")], Keys(list));
    }

    [Fact]
    public async Task List_ShouldOnlyQueryHolidaysForTheCountriesAndYearsLeftAfterFiltering()
    {
        await CreateShiftAsync(Ana, "2026-10-12");
        await CreateShiftAsync(Joao, "2027-01-04");

        await ListAsync($"?employeeId={Ana}");

        Assert.Equal([("ES", 2026)], _factory.Holidays.Calls);
    }

    [Fact]
    public async Task Export_ShouldFilterByEmployee_WhenGiven()
    {
        await CreateShiftAsync(Ana, "2026-10-12");
        await CreateShiftAsync(Joao, "2026-10-12");

        var file = await _client.GetByteArrayAsync($"/api/shifts/export?from=2026-10-01&to=2026-10-31&employeeId={Joao}");

        using var workbook = new XLWorkbook(SheetBuilder.AsStream(file));
        Assert.Equal(
            ["Email", "joao@turnosync.dev"],
            workbook.Worksheet(1).RowsUsed().Select(r => r.Cell(1).GetFormattedString()));
    }
}
