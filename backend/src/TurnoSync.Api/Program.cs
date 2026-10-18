using TurnoSync.Api.Common;
using TurnoSync.Application;
using TurnoSync.Infra;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new TimeOnlyJsonConverter()))
    .AddSpanishErrorMessages();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "TurnoSync API", Version = "v1" });

    var xmlDocs = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
    options.IncludeXmlComments(xmlDocs);
});

builder.Services.AddApplicationServices();
builder.Services.AddInfra();

var app = builder.Build();

app.Services.MigrateDatabase();

app.UseExceptionHandler();

// Swagger está habilitado en todos los entornos, incluido el contenedor.
app.UseSwagger();
app.UseSwaggerUI();

// El build de Angular vive en wwwroot y se sirve desde el mismo origen que la API (sin CORS ni nginx).
// Sin fallback de SPA: el frontend no tiene router, así que una ruta /api desconocida sigue siendo un 404.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

app.Run();

// Expuesto para WebApplicationFactory en las pruebas de integración
public partial class Program;
