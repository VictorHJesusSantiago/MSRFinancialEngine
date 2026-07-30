using Microsoft.EntityFrameworkCore;
using MSRFinancialEngine.Infrastructure;
using MSRFinancialEngine.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddFinancialEngineInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Aplica migrations pendentes automaticamente ao iniciar (adequado para MVP/dev;
// em produção considerar pipeline de migration separado).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FinancialEngineDbContext>();
    db.Database.Migrate();
}

app.Run();

public partial class Program { }
