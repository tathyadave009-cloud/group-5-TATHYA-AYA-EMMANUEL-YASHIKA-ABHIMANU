using Finance.Api.Controllers;
using Finance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Finance.Api.Tests;

public class FundingRequestsControllerTests
{
    [Fact]
    public void Create_ValidRequest_Returns201()
    {
        var controller = CreateController();

        var request = new CreateFundingRequestRequest(
            "EMS",
            500m,
            "Emergency supplies",
            true);

        ActionResult<FundingRequest> response =
            controller.Create(request);

        ObjectResult result =
            Assert.IsType<ObjectResult>(response.Result);

        Assert.Equal(201, result.StatusCode);
        Assert.IsType<FundingRequest>(result.Value);
    }

    [Fact]
    public void Create_InvalidRequest_Returns400()
    {
        var controller = CreateController();

        var request = new CreateFundingRequestRequest(
            "UNKNOWN",
            500m,
            "Invalid department",
            false);

        ActionResult<FundingRequest> response =
            controller.Create(request);

        Assert.IsType<BadRequestObjectResult>(
            response.Result);
    }

    [Fact]
    public void Get_ReturnsCreatedFundingRequests()
    {
        var service = CreateService();
        var controller =
            new FundingRequestsController(service);

        service.Create(
            "HR",
            250m,
            "Staff training",
            false);

        ActionResult<FundingRequest[]> response =
            controller.Get();

        OkObjectResult result =
            Assert.IsType<OkObjectResult>(response.Result);

        FundingRequest[] requests =
            Assert.IsType<FundingRequest[]>(result.Value);

        Assert.Single(requests);
        Assert.Equal("HR", requests[0].DepartmentCode);
    }

    [Fact]
    public void Decide_UnknownRequest_Returns404()
    {
        var controller = CreateController();

        ActionResult<FundingRequest> response =
            controller.Decide(
                Guid.NewGuid(),
                new FundingDecisionRequest(true));

        Assert.IsType<NotFoundObjectResult>(
            response.Result);
    }

    [Fact]
    public void Decide_AlreadyDecidedRequest_Returns409()
    {
        var service = CreateService();
        var controller =
            new FundingRequestsController(service);

        FundingRequest request = service.Create(
            "FIN",
            750m,
            "System upgrade",
            false);

        ActionResult<FundingRequest> firstResponse =
            controller.Decide(
                request.Id,
                new FundingDecisionRequest(false));

        Assert.IsType<OkObjectResult>(
            firstResponse.Result);

        ActionResult<FundingRequest> secondResponse =
            controller.Decide(
                request.Id,
                new FundingDecisionRequest(true));

        Assert.IsType<ConflictObjectResult>(
            secondResponse.Result);
    }

    private static FundingRequestsController
        CreateController()
    {
        return new FundingRequestsController(
            CreateService());
    }

    private static FundingRequestService CreateService()
    {
        return new FundingRequestService(
            new FakeBudgetFundingService());
    }

    private sealed class FakeBudgetFundingService
        : IBudgetFundingService
    {
        public void AddFunds(
            string departmentCode,
            decimal amount)
        {
        }
    }
}