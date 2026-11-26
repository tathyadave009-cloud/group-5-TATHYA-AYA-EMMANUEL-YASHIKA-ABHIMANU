using Finance.Api.Data;
using Finance.Api.Models;
using Finance.Api.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Invalid input." : e.ErrorMessage)
                .ToList();

            string message = errors.Count > 0
                ? string.Join("; ", errors)
                : "Invalid or malformed request payload.";

            var apiError = new ApiError(
                "bad_request",
                message,
                context.HttpContext.TraceIdentifier);

            return new BadRequestObjectResult(apiError);
        };
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod()));

// Database: SQL Server when a connection string is set (Azure).
// In Development with no connection string, use an in-memory database
// (local runs and GitHub tests have no SQL Server).
var connectionString = builder.Configuration.GetConnectionString("Default");
var inMemoryDbName = $"finance-{Guid.NewGuid()}";

builder.Services.AddDbContextFactory<AppDbContext>(options =>
{
    if (builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(connectionString))
        options.UseInMemoryDatabase(inMemoryDbName);
    else
        options.UseSqlServer(connectionString);
});

builder.Services.AddSingleton<ExpenseService>(sp =>
    new ExpenseService(
        sp.GetRequiredService<IDbContextFactory<AppDbContext>>(),
        DepartmentBudgets.StartingBudgets()));

builder.Services.AddSingleton<IBudgetFundingService>(
    provider =>
        provider.GetRequiredService<ExpenseService>());

builder.Services.AddSingleton<FundingRequestService>();

// Register the revenue service for Sprint 2 BF-08.
builder.Services.AddSingleton<RevenueService>();

// BF-20: Security notification client.
builder.Services.AddHttpClient<SecurityNotificationService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
});

// BF-29: HR funding decision notification client.
builder.Services.AddHttpClient<HrClient>(client =>
{
    var baseUrl = builder.Configuration["Hr:BaseUrl"];

    if (Uri.TryCreate(baseUrl?.Trim().TrimEnd('/') + "/",
        UriKind.Absolute, out var address) &&
        (address.Scheme == Uri.UriSchemeHttps ||
         (address.Scheme == Uri.UriSchemeHttp && address.IsLoopback)))
    {
        client.BaseAddress = address;
    }
});

var app = builder.Build();

// Create the ExpenseService now so the database is checked at startup.
app.Services.GetRequiredService<ExpenseService>();

app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        var exceptionHandlerPathFeature =
            context.Features.Get<IExceptionHandlerPathFeature>();
        var exception = exceptionHandlerPathFeature?.Error;

        var apiError = new ApiError(
            "internal_error",
            exception?.Message ?? "An unexpected server error occurred.",
            context.TraceIdentifier);

        await context.Response.WriteAsJsonAsync(apiError);
    });
});

app.UseCors();

// Swagger stays on in every environment so other groups can see our API on Azure (BF-18).
app.UseSwagger();
app.UseSwaggerUI();

// The main link opens the Swagger page instead of a 404.
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }