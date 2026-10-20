using System.Globalization;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TurnoSync.Application.Employees;
using TurnoSync.Application.Shifts;

namespace TurnoSync.Application;

public static class ApplicationServicesRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Cada regla lleva su mensaje en español; además se fija la cultura global de FluentValidation para que
        // cualquier regla sin mensaje propio salga también en español, sea cual sea la cultura de la máquina.
        ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("es");

        // Los validadores no guardan estado, así que basta con un singleton.
        services.AddSingleton<IValidator<EmployeeRequest>, EmployeeRequestValidator>();
        services.AddSingleton<IValidator<ShiftRequest>, ShiftRequestValidator>();

        services.AddScoped<EmployeeService>();
        services.AddScoped<ShiftService>();
        services.AddScoped<ShiftImportService>();
        services.AddScoped<ShiftExportService>();

        return services;
    }
}
