using Finance.Api.Data;
using Finance.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Real department budgets (Aya, BF-02) shared with Emmanuel's expense service.
builder.Services.AddSingleton<ExpenseService>(_ =>
    new ExpenseService(DepartmentBudgets.StartingBudgets()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "budget-finance"
}));

app.Run();