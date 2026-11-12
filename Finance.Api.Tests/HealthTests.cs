using Finance.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Finance.Api.Tests;

public class HealthTests
{
    [Fact]
    public void GetHealth_ReturnsVersion020AndHealthyStatus()
    {
        var controller = new HealthController();

        var actionResult = controller.GetHealth();
        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var health = Assert.IsType<HealthResponse>(ok.Value);

        Assert.Equal("Healthy", health.Status);
        Assert.Equal("BudgetFinance", health.Service);
        Assert.Equal("0.2.0", health.Version);
    }
}
