using Finance.Api.Data;
using Finance.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod()));

builder.Services.AddSingleton<ExpenseService>(_ =>
    new ExpenseService(
        DepartmentBudgets.StartingBudgets()));

builder.Services.AddSingleton<IBudgetFundingService>(
    provider =>
        provider.GetRequiredService<ExpenseService>());

builder.Services.AddSingleton<FundingRequestService>();

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