using Finance.Api.Data;
using Finance.Api.Services;

namespace Finance.Api.Tests;

public class FundingRequestIntegrationTests
{
    [Fact]
    public void ApprovingRequest_IncreasesActualDepartmentBudget()
    {
        var expenseService =
            new ExpenseService(DepartmentBudgets.StartingBudgets());

        var fundingRequestService =
            new FundingRequestService(expenseService);

        decimal before =
            expenseService.GetRemainingBudget("EMS")!.Value;

        FundingRequest request =
            fundingRequestService.Create(
                "EMS",
                1250m,
                "Emergency response equipment",
                true);

        FundingDecisionResult result =
            fundingRequestService.Decide(request.Id, true);

        decimal after =
            expenseService.GetRemainingBudget("EMS")!.Value;

        Assert.Equal(
            FundingDecisionOutcome.Success,
            result.Outcome);

        Assert.Equal(before + 1250m, after);
    }
}