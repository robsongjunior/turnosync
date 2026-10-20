using Microsoft.AspNetCore.Mvc;
using TurnoSync.Api.Common;
using TurnoSync.Application.Employees;

namespace TurnoSync.Api.Controllers;

[Route(RouteConsts.Employees)]
public sealed class EmployeesController(EmployeeService service) : BaseController
{
    /// <summary>Lista los colaboradores.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<EmployeeResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await service.ListAsync(ct));

    /// <summary>Obtiene un colaborador.</summary>
    [HttpGet(RouteConsts.ById)]
    [ProducesResponseType<EmployeeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        HandleResult(await service.GetAsync(id, ct));

    /// <summary>Crea un colaborador. El email debe ser único entre los colaboradores activos.</summary>
    [HttpPost]
    [ProducesResponseType<EmployeeResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(EmployeeRequest request, CancellationToken ct) =>
        HandleResult(
            await service.CreateAsync(request, ct),
            created => CreatedAtAction(nameof(Get), new { id = created.Id }, created));

    /// <summary>Actualiza un colaborador.</summary>
    [HttpPut(RouteConsts.ById)]
    [ProducesResponseType<EmployeeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, EmployeeRequest request, CancellationToken ct) =>
        HandleResult(await service.UpdateAsync(id, request, ct));

    /// <summary>Borra lógicamente al colaborador y todos sus turnos.</summary>
    [HttpDelete(RouteConsts.ById)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        HandleResult(await service.DeleteAsync(id, ct), NoContent);
}
