namespace TurnoSync.Application.Employees;

// Los campos de la solicitud son anulables para que un campo ausente llegue al validador (la única fuente
// de mensajes de error) en lugar de fallar antes, en el enlace de modelo.
public sealed record EmployeeRequest(string? Name, string? Email, string? CountryCode);

public sealed record EmployeeResponse(int Id, string Name, string Email, string CountryCode);
