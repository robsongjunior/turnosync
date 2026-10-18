using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using TurnoSync.Application.Shifts;
using TurnoSync.Tests.Support;

namespace TurnoSync.Tests.Api;

public sealed class ShiftSpreadsheetApiTests : IDisposable
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly TurnoSyncFactory _factory = new();
    private readonly HttpClient _client;

    public ShiftSpreadsheetApiTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private static MultipartFormDataContent Upload(byte[] file, string fileName = "shifts.xlsx", string field = "file")
    {
        var fileContent = new ByteArrayContent(file);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(XlsxContentType);

        return new MultipartFormDataContent { { fileContent, field, fileName } };
    }

    private async Task CreateShiftAsync(int collaboratorId, string date, string start = "08:00", string end = "16:00")
    {
        var response = await _client.PostAsJsonAsync(
            "/api/shifts", new { collaboratorId, date, startTime = start, endTime = end });
        response.EnsureSuccessStatusCode();
    }

    private static List<string[]> ReadSheet(byte[] file)
    {
        using var workbook = new XLWorkbook(SheetBuilder.AsStream(file));
        return workbook.Worksheet(1).RowsUsed()
            .Select(r => r.Cells(1, 4).Select(c => c.GetFormattedString()).ToArray())
            .ToList();
    }

    // --- Importación ---

    [Fact]
    public async Task Import_ShouldReturn200WithTheReport_AndPersistTheAcceptedRows()
    {
        var file = SheetBuilder.Standard(
            ["ana@turnosync.dev", "2026-10-12", "08:00", "16:00"],
            ["ana@turnosync.dev", "2026-10-12", "12:00", "20:00"],
            ["joao@turnosync.dev", "2026-10-12", "08:00", "16:00"]);

        var response = await _client.PostAsync("/api/shifts/import", Upload(file));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<ShiftImportReport>(TestJson.Options);
        Assert.Equal([2, 4], report!.Imported.Select(i => i.Row));
        var failed = Assert.Single(report.Failed);
        Assert.Equal(3, failed.Row);
        Assert.Equal(["Se solapa con la fila 2."], failed.Reasons);

        var list = await _client.GetFromJsonAsync<ShiftListResponse>("/api/shifts", TestJson.Options);
        Assert.Equal(2, list!.Items.Count);
    }

    [Fact]
    public async Task Import_ShouldUseTheDocumentedJsonShape()
    {
        var file = SheetBuilder.Standard(
            ["ana@turnosync.dev", "2026-10-12", "08:00", "16:00"],
            ["x@y.com", "2026-10-12", "08:00", "16:00"]);

        var json = await (await _client.PostAsync("/api/shifts/import", Upload(file))).Content.ReadAsStringAsync();

        Assert.Contains("\"imported\":[{\"row\":2,\"email\":\"ana@turnosync.dev\"}]", json);
        Assert.Contains("\"failed\":[{\"row\":3,\"email\":\"x@y.com\",\"reasons\":[", json);
    }

    [Fact]
    public async Task Import_ShouldReturn400_WhenFileIsNotAnXlsx()
    {
        var response = await _client.PostAsync("/api/shifts/import", Upload("a,b,c"u8.ToArray(), "shifts.csv"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(["Solo se admiten archivos .xlsx."], problem!.Errors["file"]);
    }

    [Fact]
    public async Task Import_ShouldReturn400_WhenXlsxHasTheWrongContent()
    {
        var response = await _client.PostAsync("/api/shifts/import", Upload("not excel"u8.ToArray()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Import_ShouldReturn400_WhenColumnsAreMissing()
    {
        var file = SheetBuilder.Build(sheet => SheetBuilder.WriteRow(sheet, 1, ["Email", "Fecha"]));

        var response = await _client.PostAsync("/api/shifts/import", Upload(file));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("HoraInicio", problem!.Errors["file"][0]);
    }

    [Fact]
    public async Task Import_ShouldReturn400_WhenTheFileFieldIsMissing()
    {
        using var form = new MultipartFormDataContent { { new StringContent("x"), "other" } };

        var response = await _client.PostAsync("/api/shifts/import", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(["El archivo .xlsx es obligatorio."], problem!.Errors["file"]);
    }

    [Fact]
    public async Task Import_ShouldReturn400_WhenTheMultipartIsEmpty()
    {
        var response = await _client.PostAsync("/api/shifts/import", new MultipartFormDataContent());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- Exportación ---

    [Fact]
    public async Task Export_ShouldIncludeBothEndsOfThePeriodAndExcludeOutsideAndDeletedShifts()
    {
        await CreateShiftAsync(1, "2026-10-11");
        await CreateShiftAsync(1, "2026-10-12");                      // límite inferior
        await CreateShiftAsync(2, "2026-10-14", "22:00", "06:00");    // límite superior (nocturno, por fecha de inicio)
        await CreateShiftAsync(1, "2026-10-15");
        await CreateShiftAsync(3, "2026-10-13");
        var deleted = await _client.GetFromJsonAsync<ShiftListResponse>("/api/shifts", TestJson.Options);
        await _client.DeleteAsync($"/api/shifts/{deleted!.Items.Single(i => i.CollaboratorId == 3).Id}");

        var response = await _client.GetAsync("/api/shifts/export?from=2026-10-12&to=2026-10-14");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(XlsxContentType, response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("turnos_2026-10-12_2026-10-14.xlsx", response.Content.Headers.ContentDisposition!.FileName);
        Assert.Equal(
            [
                ["Email", "Fecha", "HoraInicio", "HoraFin"],
                ["ana@turnosync.dev", "2026-10-12", "08:00", "16:00"],
                ["joao@turnosync.dev", "2026-10-14", "22:00", "06:00"],
            ],
            ReadSheet(await response.Content.ReadAsByteArrayAsync()));
    }

    [Fact]
    public async Task Export_ShouldUseTheListingOrder()
    {
        await CreateShiftAsync(2, "2026-10-13");
        await CreateShiftAsync(1, "2026-10-13");
        await CreateShiftAsync(1, "2026-10-12");

        var response = await _client.GetAsync("/api/shifts/export?from=2026-10-01&to=2026-10-31");

        Assert.Equal(
            ["ana@turnosync.dev", "ana@turnosync.dev", "joao@turnosync.dev"],
            ReadSheet(await response.Content.ReadAsByteArrayAsync()).Skip(1).Select(r => r[0]));
    }

    [Fact]
    public async Task Export_ShouldReturnOnlyTheHeader_WhenThereAreNoShiftsInThePeriod()
    {
        var response = await _client.GetAsync("/api/shifts/export?from=2026-10-01&to=2026-10-31");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(ReadSheet(await response.Content.ReadAsByteArrayAsync()));
    }

    [Fact]
    public async Task Export_ShouldAcceptASingleDayPeriod()
    {
        await CreateShiftAsync(1, "2026-10-12");

        var response = await _client.GetAsync("/api/shifts/export?from=2026-10-12&to=2026-10-12");

        Assert.Equal(2, ReadSheet(await response.Content.ReadAsByteArrayAsync()).Count);
    }

    [Fact]
    public async Task Export_ShouldReturn400_WhenFromIsAfterTo()
    {
        var response = await _client.GetAsync("/api/shifts/export?from=2026-10-14&to=2026-10-12");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(["from"], problem!.Errors.Keys);
        Assert.Equal(["El parámetro 'from' debe ser anterior o igual a 'to'."], problem.Errors["from"]);
    }

    [Theory]
    [InlineData("", "from,to")]
    [InlineData("?from=2026-10-12", "to")]
    [InlineData("?to=2026-10-12", "from")]
    public async Task Export_ShouldReturn400_WhenAParameterIsMissing(string query, string expectedFields)
    {
        var response = await _client.GetAsync($"/api/shifts/export{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(expectedFields.Split(','), problem!.Errors.Keys.Order());
        Assert.All(
            expectedFields.Split(','),
            field => Assert.Equal([$"El parámetro '{field}' es obligatorio (yyyy-MM-dd)."], problem.Errors[field]));
    }

    [Fact]
    public async Task Export_ShouldReturn400_WhenADateIsMalformed()
    {
        var response = await _client.GetAsync("/api/shifts/export?from=yesterday&to=2026-10-12");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ExportedFile_ShouldBeReimportableIntoAnotherInstallation()
    {
        await CreateShiftAsync(1, "2026-10-12", "22:00", "06:00");
        await CreateShiftAsync(2, "2026-10-13", "00:00", "08:00");
        await CreateShiftAsync(5, "2026-10-14", "07:59", "15:01");
        var exported = await _client.GetByteArrayAsync("/api/shifts/export?from=2026-10-01&to=2026-10-31");

        using var otherFactory = new TurnoSyncFactory();
        using var otherClient = otherFactory.CreateClient();
        var response = await otherClient.PostAsync("/api/shifts/import", Upload(exported));

        var report = await response.Content.ReadFromJsonAsync<ShiftImportReport>(TestJson.Options);
        Assert.Equal(3, report!.Imported.Count);
        Assert.Empty(report.Failed);
        var original = await _client.GetFromJsonAsync<ShiftListResponse>("/api/shifts", TestJson.Options);
        var copy = await otherClient.GetFromJsonAsync<ShiftListResponse>("/api/shifts", TestJson.Options);
        Assert.Equal(
            original!.Items.Select(i => (i.CollaboratorId, i.Date, i.StartTime, i.EndTime)),
            copy!.Items.Select(i => (i.CollaboratorId, i.Date, i.StartTime, i.EndTime)));
    }
}
