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

// Swagger fica habilitado em qualquer ambiente, inclusive no container.
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();

// Exposto para o WebApplicationFactory nos testes de integração
public partial class Program;
