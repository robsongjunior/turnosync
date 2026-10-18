using System.ComponentModel.DataAnnotations;
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
    /// Lista todos los turnos ordenados por inicio y luego por nombre del colaborador, y marca los que empiezan
    /// en un festivo nacional del país del colaborador (Nager.Date). Si falla la consulta de festivos,
    /// <c>isHoliday</c> es null en los turnos del país afectado y <c>holidaysAvailable</c> es false; el listado nunca falla.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<ShiftListResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await service.ListAsync(ct));

    /// <summary>Obtiene un turno.</summary>
    [HttpGet(RouteConsts.ById)]
    [ProducesResponseType<ShiftResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        HandleResult(await service.GetAsync(id, ct));

    /// <summary>
    /// Crea un turno. Si la hora de fin es anterior a la de inicio, el turno cruza la medianoche.
    /// Devuelve 409 si se solapa con otro turno del mismo colaborador.
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

    /// <summary>Actualiza un turno. Devuelve 409 si se solapara con otro turno del mismo colaborador.</summary>
    [HttpPut(RouteConsts.ById)]
    [ProducesResponseType<ShiftResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, ShiftRequest request, CancellationToken ct) =>
        HandleResult(await service.UpdateAsync(id, request, ct));

    /// <summary>Borra lógicamente un turno.</summary>
    [HttpDelete(RouteConsts.ById)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        HandleResult(await service.DeleteAsync(id, ct), NoContent);

    /// <summary>
    /// Importa turnos desde un .xlsx con las columnas Email, Fecha, HoraInicio y HoraFin (primera hoja, encabezados
    /// localizados por nombre, en cualquier orden). Cada fila se valida por separado: las válidas se importan y las
    /// inválidas se devuelven en el informe con su motivo. Las fechas y horas pueden ser celdas de Excel o texto
    /// (yyyy-MM-dd, HH:mm).
    /// </summary>
    /// <param name="file">La hoja de cálculo .xlsx.</param>
    [HttpPost(RouteConsts.Import)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<ShiftImportReport>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Import(
        [Required(ErrorMessage = "El archivo .xlsx es obligatorio.")] IFormFile file, CancellationToken ct)
    {
        await using var content = file.OpenReadStream();

        return HandleResult(await importService.ImportAsync(file.FileName, content, ct));
    }

    /// <summary>
    /// Exporta a .xlsx los turnos cuya fecha de inicio está entre <c>from</c> y <c>to</c> (ambas inclusivas y
    /// obligatorias, yyyy-MM-dd). Mismas columnas que la importación.
    /// </summary>
    /// <param name="from">Primera fecha (inclusive).</param>
    /// <param name="to">Última fecha (inclusive).</param>
    [HttpGet(RouteConsts.Export)]
    [Produces(XlsxContentType)]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Export(DateOnly? from, DateOnly? to, CancellationToken ct) =>
        HandleResult(
            await exportService.ExportAsync(from, to, ct),
            file => File(file.Content, XlsxContentType, file.FileName));
}
