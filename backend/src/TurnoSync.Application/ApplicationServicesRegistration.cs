using System.Globalization;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TurnoSync.Application.Collaborators;
using TurnoSync.Application.Shifts;

namespace TurnoSync.Application;

public static class ApplicationServicesRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // API error messages are always in English, regardless of the machine culture.
        ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("en-US");

        // Validators are stateless, so a singleton is enough.
        services.AddSingleton<IValidator<CollaboratorRequest>, CollaboratorRequestValidator>();
        services.AddSingleton<IValidator<ShiftRequest>, ShiftRequestValidator>();

        services.AddScoped<CollaboratorService>();
        services.AddScoped<ShiftService>();
        services.AddScoped<ShiftImportService>();
        services.AddScoped<ShiftExportService>();

        return services;
    }
}
