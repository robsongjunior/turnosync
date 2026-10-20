using TurnoSync.Application.Common;
using TurnoSync.Domain.Employees;

namespace TurnoSync.Application.Employees;

public interface IEmployeeRepository : IRepository<Employee>
{
    /// <summary>¿Existe otro colaborador (no borrado) con este email? El email debe venir ya normalizado.</summary>
    Task<bool> EmailExistsAsync(string email, int? excludeId, CancellationToken ct = default);

    /// <summary>Busca colaboradores por email (lo usa la importación). Los emails deben venir ya normalizados.</summary>
    Task<IReadOnlyList<Employee>> GetByEmailsAsync(IEnumerable<string> emails, CancellationToken ct = default);
}
