var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.Run();

// Exposto para o WebApplicationFactory nos testes de integracao
public partial class Program;
