using Microsoft.AspNetCore.Mvc;
using TurnoSync.Api.Common;
using TurnoSync.Application.Shifts;

namespace TurnoSync.Api.Controllers;

[Route(RouteConsts.Shifts)]
public sealed class ShiftsController(ShiftService service) : BaseController
{
    /// <summary>Lista todos os turnos, ordenados pelo início e depois pelo nome do colaborador.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ShiftResponse>>(StatusCodes.Status200OK)]
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
}
