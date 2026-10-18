using TurnoSync.Api.Common;
using TurnoSync.Application;
using TurnoSync.Infra;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new TimeOnlyJsonConverter()));

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

// Swagger is enabled in every environment, including the container.
app.UseSwagger();
app.UseSwaggerUI();

// The Angular build lives in wwwroot and is served from the API's own origin (no CORS, no nginx).
// No SPA fallback: the frontend has no router, so an unknown /api route stays a 404.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

app.Run();

// Exposed for WebApplicationFactory in the integration tests
public partial class Program;
