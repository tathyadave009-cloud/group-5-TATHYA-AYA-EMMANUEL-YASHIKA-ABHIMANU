using Finance.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// Temporary budgets for testing Emmanuel's expense endpoint.
builder.Services.AddSingleton<ExpenseService>(_ =>
    new ExpenseService(new Dictionary<string, decimal>
    {
        ["TRANSIT"] = 10000m,
        ["UTIL"] = 10000m,
        ["RES"] = 10000m,
        ["SEC"] = 10000m,
        ["FIN"] = 10000m,
        ["HR"] = 10000m,
        ["EMS"] = 10000m
    }));

var app = builder.Build();

app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();