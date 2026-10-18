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
        // Mensagens de erro da API sempre em inglês, independente da cultura da máquina.
        ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("en-US");

        // Validators não guardam estado, então um singleton basta.
        services.AddSingleton<IValidator<CollaboratorRequest>, CollaboratorRequestValidator>();
        services.AddSingleton<IValidator<ShiftRequest>, ShiftRequestValidator>();

        services.AddScoped<CollaboratorService>();
        services.AddScoped<ShiftService>();
        services.AddScoped<ShiftImportService>();
        services.AddScoped<ShiftExportService>();

        return services;
    }
}
