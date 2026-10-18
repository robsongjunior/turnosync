using Microsoft.AspNetCore.Mvc;
using TurnoSync.Api.Common;
using TurnoSync.Application.Shifts;

namespace TurnoSync.Api.Controllers;

[Route(RouteConsts.Shifts)]
public sealed class ShiftsController(
    ShiftService service,
    ShiftImportService importService,
    ShiftExportService exportService) : BaseController
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>
    /// Lists all shifts ordered by start and then by collaborator name, flagging the ones that start on a
    /// national public holiday of the collaborator's country (Nager.Date). If the holiday lookup fails,
    /// <c>isHoliday</c> is null for the affected country's shifts and <c>holidaysAvailable</c> is false; the listing never fails.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<ShiftListResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await service.ListAsync(ct));

    /// <summary>Gets a shift.</summary>
    [HttpGet(RouteConsts.ById)]
    [ProducesResponseType<ShiftResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        HandleResult(await service.GetAsync(id, ct));

    /// <summary>
    /// Creates a shift. If the end time is earlier than the start time, the shift crosses midnight.
    /// Returns 409 if it overlaps another shift of the same collaborator.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<ShiftResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(ShiftRequest request, CancellationToken ct) =>
        HandleResult(
            await service.CreateAsync(request, ct),
            created => CreatedAtAction(nameof(Get), new { id = created.Id }, created));

    /// <summary>Updates a shift. Returns 409 if it would overlap another shift of the same collaborator.</summary>
    [HttpPut(RouteConsts.ById)]
    [ProducesResponseType<ShiftResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, ShiftRequest request, CancellationToken ct) =>
        HandleResult(await service.UpdateAsync(id, request, ct));

    /// <summary>Soft-deletes a shift.</summary>
    [HttpDelete(RouteConsts.ById)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        HandleResult(await service.DeleteAsync(id, ct), NoContent);

    /// <summary>
    /// Imports shifts from an .xlsx with the columns Email, Fecha, HoraInicio and HoraFin (first sheet, headers
    /// matched by name, in any order). Each row is validated on its own: valid rows are imported and invalid ones
    /// are returned in the report with the reason. Dates and times can be Excel cells or text (yyyy-MM-dd, HH:mm).
    /// </summary>
    /// <param name="file">The .xlsx spreadsheet.</param>
    [HttpPost(RouteConsts.Import)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<ShiftImportReport>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Import(IFormFile file, CancellationToken ct)
    {
        await using var content = file.OpenReadStream();

        return HandleResult(await importService.ImportAsync(file.FileName, content, ct));
    }

    /// <summary>
    /// Exports to .xlsx the shifts whose start date is between <c>from</c> and <c>to</c> (both inclusive and
    /// required, yyyy-MM-dd). Same columns as the import.
    /// </summary>
    /// <param name="from">First date (inclusive).</param>
    /// <param name="to">Last date (inclusive).</param>
    [HttpGet(RouteConsts.Export)]
    [Produces(XlsxContentType)]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Export(DateOnly? from, DateOnly? to, CancellationToken ct) =>
        HandleResult(
            await exportService.ExportAsync(from, to, ct),
            file => File(file.Content, XlsxContentType, file.FileName));
}
