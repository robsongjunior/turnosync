using Microsoft.AspNetCore.Mvc;
using TurnoSync.Api.Common;
using TurnoSync.Application.Collaborators;

namespace TurnoSync.Api.Controllers;

[Route(RouteConsts.Collaborators)]
public sealed class CollaboratorsController(CollaboratorService service) : BaseController
{
    /// <summary>Lista os colaboradores.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CollaboratorResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await service.ListAsync(ct));

    /// <summary>Detalha um colaborador.</summary>
    [HttpGet(RouteConsts.ById)]
    [ProducesResponseType<CollaboratorResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        HandleResult(await service.GetAsync(id, ct));

    /// <summary>Cria um colaborador. O e-mail deve ser único entre os colaboradores ativos.</summary>
    [HttpPost]
    [ProducesResponseType<CollaboratorResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CollaboratorRequest request, CancellationToken ct) =>
        HandleResult(
            await service.CreateAsync(request, ct),
            created => CreatedAtAction(nameof(Get), new { id = created.Id }, created));

    /// <summary>Atualiza um colaborador.</summary>
    [HttpPut(RouteConsts.ById)]
    [ProducesResponseType<CollaboratorResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, CollaboratorRequest request, CancellationToken ct) =>
        HandleResult(await service.UpdateAsync(id, request, ct));

    /// <summary>Exclui (soft delete) o colaborador e todos os seus turnos.</summary>
    [HttpDelete(RouteConsts.ById)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        HandleResult(await service.DeleteAsync(id, ct), NoContent);
}
