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
    /// Lista todos os turnos, ordenados pelo início e depois pelo nome do colaborador, marcando os que
    /// começam em feriado nacional do país do colaborador (Nager.Date). Se a consulta de feriados falhar,
    /// <c>isHoliday</c> vem nulo nos turnos do país afetado e <c>holidaysAvailable</c> vem falso; a listagem não falha.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<ShiftListResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await service.ListAsync(ct));

    /// <summary>Detalha um turno.</summary>
    [HttpGet(RouteConsts.ById)]
    [ProducesResponseType<ShiftResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        HandleResult(await service.GetAsync(id, ct));

    /// <summary>
    /// Cria um turno. Se a hora de fim for menor que a de início, o turno atravessa a meia-noite.
    /// Devolve 409 se sobrepuser outro turno do mesmo colaborador.
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

    /// <summary>Atualiza um turno. Devolve 409 se passar a sobrepor outro turno do mesmo colaborador.</summary>
    [HttpPut(RouteConsts.ById)]
    [ProducesResponseType<ShiftResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, ShiftRequest request, CancellationToken ct) =>
        HandleResult(await service.UpdateAsync(id, request, ct));

    /// <summary>Exclui (soft delete) um turno.</summary>
    [HttpDelete(RouteConsts.ById)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        HandleResult(await service.DeleteAsync(id, ct), NoContent);

    /// <summary>
    /// Importa turnos de um .xlsx com as colunas Email, Fecha, HoraInicio e HoraFin (primeira aba, cabeçalho
    /// por nome, em qualquer ordem). Cada linha é validada à parte: as válidas entram e as inválidas voltam
    /// no informe com o motivo. Datas e horas podem ser células do Excel ou texto (yyyy-MM-dd, HH:mm).
    /// </summary>
    /// <param name="file">Planilha .xlsx.</param>
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
    /// Exporta para .xlsx os turnos cuja data de início está entre <c>from</c> e <c>to</c> (inclusivos, ambos
    /// obrigatórios, formato yyyy-MM-dd). Mesmas colunas da importação.
    /// </summary>
    /// <param name="from">Primeira data (inclusiva).</param>
    /// <param name="to">Última data (inclusiva).</param>
    [HttpGet(RouteConsts.Export)]
    [Produces(XlsxContentType)]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Export(DateOnly? from, DateOnly? to, CancellationToken ct) =>
        HandleResult(
            await exportService.ExportAsync(from, to, ct),
            file => File(file.Content, XlsxContentType, file.FileName));
}
