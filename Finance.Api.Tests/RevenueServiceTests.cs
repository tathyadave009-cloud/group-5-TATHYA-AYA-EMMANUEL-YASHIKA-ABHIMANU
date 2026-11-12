
using Finance.Api.Services;
using Xunit;

namespace Finance.Api.Tests;

public class RevenueServiceTests
{
    [Fact]
    public void RecordRevenue_ValidInput_ReturnsApproved()
    {
        var service = new RevenueService();

        var result = service.RecordRevenue(
            "FINES", "SEC", 150m, "Parking violation fine");

        Assert.True(result.Approved);
        Assert.NotNull(result.Revenue);
        Assert.Equal("FINES", result.Revenue.Source);
        Assert.Equal("SEC", result.Revenue.DepartmentCode);
        Assert.Equal(150m, result.Revenue.Amount);
    }

    [Fact]
    public void RecordRevenue_InvalidSource_ReturnsRejected()
    {
        var service = new RevenueService();

        var result = service.RecordRevenue(
            "DONATIONS", "SEC", 100m, "Invalid source test");

        Assert.False(result.Approved);
        Assert.Null(result.Revenue);
        Assert.Equal("Invalid revenue source.", result.Message);
    }

    [Fact]
    public void RecordRevenue_ZeroAmount_ReturnsRejected()
    {
        var service = new RevenueService();

        var result = service.RecordRevenue(
            "FINES", "SEC", 0m, "Zero amount test");

        Assert.False(result.Approved);
        Assert.Null(result.Revenue);
        Assert.Equal(
            "Amount must be greater than zero.",
            result.Message);
    }

    [Fact]
    public void RecordRevenue_TooManyDecimals_ReturnsRejected()
    {
        var service = new RevenueService();

        var result = service.RecordRevenue(
            "FINES", "SEC", 150.123m, "Decimal test");

        Assert.False(result.Approved);
        Assert.Null(result.Revenue);
        Assert.Equal(
            "Amount must have at most two decimal places.",
            result.Message);
    }

    [Fact]
    public void RecordRevenue_UnknownDepartment_ReturnsRejected()
    {
        var service = new RevenueService();

        var result = service.RecordRevenue(
            "FINES", "XYZ", 100m, "Unknown department test");

        Assert.False(result.Approved);
        Assert.Null(result.Revenue);
        Assert.Equal("Unknown department code.", result.Message);
    }

    [Fact]
    public void GetRevenueSummary_CalculatesTotalsBySource()
    {
        var service = new RevenueService();

        service.RecordRevenue(
            "FINES", "SEC", 150m, "Parking fine");

        service.RecordRevenue(
            "FINES", "SEC", 50m, "Speeding fine");

        service.RecordRevenue(
            "FARES", "TRANSIT", 75m, "Bus fares");

        var summary = service.GetRevenueSummary();

        Assert.Equal(200m, summary["FINES"]);
        Assert.Equal(75m, summary["FARES"]);
        Assert.Equal(0m, summary["BILLS"]);
        Assert.Equal(0m, summary["FEES"]);
    }
}
