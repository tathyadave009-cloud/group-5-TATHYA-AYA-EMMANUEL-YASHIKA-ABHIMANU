
using Finance.Api.Data;
using Finance.Api.Models;
using Finance.Api.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

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

builder.Services.AddSingleton<ExpenseService>(_ =>
    new ExpenseService(
        DepartmentBudgets.StartingBudgets()));

builder.Services.AddSingleton<IBudgetFundingService>(
    provider =>
        provider.GetRequiredService<ExpenseService>());

builder.Services.AddSingleton<FundingRequestService>();

// Register the revenue service for Sprint 2 BF-08.
builder.Services.AddSingleton<RevenueService>();

var app = builder.Build();

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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
