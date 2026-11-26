using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Finance.Api.Tests;

// BF-10: login and roles from Registration (JWT). BF-22: 401/403 in the class error format.
public class AuthTests
{
    private const string Key = "test-signing-key-for-finance-api-at-least-32-bytes!";
    private const string Issuer = "cm-registration-test";
    private const string Audience = "cm-services-test";

    private static WebApplicationFactory<Program> SecuredApi() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Jwt:Key", Key);
            builder.UseSetting("Jwt:Issuer", Issuer);
            builder.UseSetting("Jwt:Audience", Audience);
        });

    private static string Token(string role, string user = "tester")
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: new[]
            {
                new Claim(ClaimTypes.Name, user),
                new Claim(ClaimTypes.Role, role)
            },
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static HttpRequestMessage PutBudget(string? token)
    {
        var message = new HttpRequestMessage(HttpMethod.Put, "/api/budget/v1/budgets/EMS")
        {
            Content = JsonContent.Create(new { allocated = 5_000_000m })
        };
        if (token != null)
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return message;
    }

    [Fact]
    public async Task ChangeBudget_NoToken_Returns401InClassFormat()
    {
        using var factory = SecuredApi();
        var response = await factory.CreateClient().SendAsync(PutBudget(null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("unauthorized", doc.RootElement.GetProperty("error").GetString());
        Assert.False(string.IsNullOrWhiteSpace(doc.RootElement.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task ChangeBudget_UserRole_Returns403InClassFormat()
    {
        using var factory = SecuredApi();
        var response = await factory.CreateClient().SendAsync(PutBudget(Token("User")));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("forbidden", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ChangeBudget_ManagerRole_Returns200()
    {
        using var factory = SecuredApi();
        var response = await factory.CreateClient().SendAsync(PutBudget(Token("Manager")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ChangeBudget_TokenSignedWithWrongKey_Returns401()
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes("a-completely-different-key-that-is-32-bytes-long!!")),
            SecurityAlgorithms.HmacSha256);
        var fake = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            Issuer, Audience, new[] { new Claim(ClaimTypes.Role, "Manager") },
            expires: DateTime.UtcNow.AddMinutes(10), signingCredentials: credentials));

        using var factory = SecuredApi();
        var response = await factory.CreateClient().SendAsync(PutBudget(fake));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DecideFunding_UserRole_Returns403()
    {
        using var factory = SecuredApi();
        var client = factory.CreateClient();
        var message = new HttpRequestMessage(HttpMethod.Put, $"/api/budget/v1/funding-requests/{Guid.NewGuid()}/decision")
        {
            Content = JsonContent.Create(new { approved = true })
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token("User"));

        var response = await client.SendAsync(message);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PartnerCalls_StayOpen_WhenLoginIsOn()
    {
        // Other groups (Transit, Emergency, HR) call us service-to-service without a user token.
        using var factory = SecuredApi();
        var client = factory.CreateClient();

        var budgets = await client.GetAsync("/api/budget/v1/allocations/EMS");
        Assert.Equal(HttpStatusCode.OK, budgets.StatusCode);

        var expense = await client.PostAsJsonAsync("/api/budget/v1/expenses",
            new { departmentCode = "TRANSIT", amount = 100m, description = "Bus repair" });
        Assert.Equal(HttpStatusCode.Created, expense.StatusCode);

        var funding = await client.PostAsJsonAsync("/api/budget/v1/funding-requests",
            new { departmentCode = "UTIL", amount = 5000m, reason = "Water main", urgent = false });
        Assert.Equal(HttpStatusCode.Created, funding.StatusCode);
    }

    [Fact]
    public async Task ChangeBudget_LoginOff_StillWorksWithoutToken()
    {
        // Local runs and CI have no Jwt:Key, so nothing is blocked.
        using var factory = new WebApplicationFactory<Program>();
        var response = await factory.CreateClient().SendAsync(PutBudget(null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
