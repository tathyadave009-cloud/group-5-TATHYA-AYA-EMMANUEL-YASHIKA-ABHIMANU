using Finance.Api.Services;

namespace Finance.Api.Tests;

public class FundingRequestServiceTests
{
    [Fact]
    public void Create_ValidRequest_NormalizesAndStoresRequest()
    {
        var fundingService = new FakeBudgetFundingService();
        DateTime now = new(
            2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

        var service = new FundingRequestService(
            fundingService,
            () => now);

        FundingRequest result = service.Create(
            " ems ",
            1500.50m,
            "Emergency equipment",
            true);

        Assert.Equal("EMS", result.DepartmentCode);
        Assert.Equal(1500.50m, result.Amount);
        Assert.Equal("Emergency equipment", result.Reason);
        Assert.True(result.Urgent);
        Assert.Equal(now, result.CreatedAtUtc);
        Assert.Equal(FundingRequestStatus.Pending, result.Status);
        Assert.Null(result.DecidedAtUtc);
        Assert.Single(service.GetAll());
    }

    [Fact]
    public void Create_RejectsUnknownDepartment()
    {
        var service = CreateService();

        Assert.Throws<ArgumentException>(() =>
            service.Create(
                "UNKNOWN",
                100m,
                "Invalid department",
                false));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(10.123)]
    public void Create_RejectsInvalidAmounts(double invalidAmount)
    {
        var service = CreateService();

        Assert.Throws<ArgumentException>(() =>
            service.Create(
                "EMS",
                Convert.ToDecimal(invalidAmount),
                "Invalid amount",
                false));
    }

    [Fact]
    public void Create_RejectsMissingReason()
    {
        var service = CreateService();

        Assert.Throws<ArgumentException>(() =>
            service.Create(
                "EMS",
                100m,
                " ",
                false));
    }

    [Fact]
    public void GetAll_ReturnsRequestsInRequiredPriorityOrder()
    {
        var times = new Queue<DateTime>(new[]
        {
            new DateTime(2026, 11, 11, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 11, 10, 1, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 11, 10, 2, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 11, 10, 3, 0, DateTimeKind.Utc)
        });

        var service = new FundingRequestService(
            new FakeBudgetFundingService(),
            () => times.Dequeue());

        service.Create("HR", 100m, "Old normal request", false);
        service.Create("EMS", 200m, "Urgent EMS request", true);
        service.Create("FIN", 300m, "Other urgent request", true);
        service.Create("RES", 400m, "New normal request", false);

        FundingRequest[] results = service.GetAll();

        Assert.Equal(
            new[] { "EMS", "FIN", "HR", "RES" },
            results.Select(request => request.DepartmentCode));
    }

    [Fact]
    public void Decide_Approval_AddsFundsAndMarksRequestApproved()
    {
        var fundingService = new FakeBudgetFundingService();
        var service = new FundingRequestService(fundingService);

        FundingRequest request = service.Create(
            "EMS",
            500m,
            "Additional emergency funds",
            true);

        FundingDecisionResult result =
            service.Decide(request.Id, true);

        Assert.Equal(FundingDecisionOutcome.Success, result.Outcome);
        Assert.NotNull(result.Request);
        Assert.Equal(
            FundingRequestStatus.Approved,
            result.Request.Status);
        Assert.NotNull(result.Request.DecidedAtUtc);
        Assert.Equal(500m, fundingService.GetAddedFunds("EMS"));
    }

    [Fact]
    public void Decide_Rejection_DoesNotAddFunds()
    {
        var fundingService = new FakeBudgetFundingService();
        var service = new FundingRequestService(fundingService);

        FundingRequest request = service.Create(
            "HR",
            250m,
            "Optional training",
            false);

        FundingDecisionResult result =
            service.Decide(request.Id, false);

        Assert.Equal(FundingDecisionOutcome.Success, result.Outcome);
        Assert.NotNull(result.Request);
        Assert.Equal(
            FundingRequestStatus.Rejected,
            result.Request.Status);
        Assert.Equal(0m, fundingService.GetAddedFunds("HR"));
    }

    [Fact]
    public void Decide_ReturnsNotFoundAndRejectsDuplicateDecision()
    {
        var service = CreateService();

        FundingDecisionResult missing =
            service.Decide(Guid.NewGuid(), true);

        Assert.Equal(
            FundingDecisionOutcome.NotFound,
            missing.Outcome);

        FundingRequest request = service.Create(
            "FIN",
            700m,
            "Finance system upgrade",
            false);

        FundingDecisionResult first =
            service.Decide(request.Id, false);

        FundingDecisionResult second =
            service.Decide(request.Id, true);

        Assert.Equal(
            FundingDecisionOutcome.Success,
            first.Outcome);

        Assert.Equal(
            FundingDecisionOutcome.AlreadyDecided,
            second.Outcome);
    }

    private static FundingRequestService CreateService()
    {
        return new FundingRequestService(
            new FakeBudgetFundingService());
    }

    private sealed class FakeBudgetFundingService
        : IBudgetFundingService
    {
        private readonly Dictionary<string, decimal> _addedFunds =
            new(StringComparer.OrdinalIgnoreCase);

        public void AddFunds(
            string departmentCode,
            decimal amount)
        {
            _addedFunds[departmentCode] =
                GetAddedFunds(departmentCode) + amount;
        }

        public decimal GetAddedFunds(string departmentCode)
        {
            return _addedFunds.TryGetValue(
                departmentCode,
                out decimal amount)
                ? amount
                : 0m;
        }
    }
}