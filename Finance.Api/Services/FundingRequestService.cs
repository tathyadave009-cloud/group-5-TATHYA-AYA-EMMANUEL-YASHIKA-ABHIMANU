using Finance.Api.Data;

namespace Finance.Api.Services;

public enum FundingRequestStatus
{
    Pending,
    Approved,
    Rejected
}

public enum FundingDecisionOutcome
{
    Success,
    NotFound,
    AlreadyDecided
}

public record FundingRequest(
    Guid Id,
    string DepartmentCode,
    decimal Amount,
    string Reason,
    bool Urgent,
    DateTime CreatedAtUtc,
    FundingRequestStatus Status,
    DateTime? DecidedAtUtc);

public record FundingDecisionResult(
    FundingDecisionOutcome Outcome,
    string Message,
    FundingRequest? Request);

public interface IBudgetFundingService
{
    void AddFunds(string departmentCode, decimal amount);
}

public class FundingRequestService
{
    private readonly IBudgetFundingService _budgetFundingService;
    private readonly Func<DateTime> _utcNow;
    private readonly List<FundingRequest> _requests = new();
    private readonly object _lock = new();

    public FundingRequestService(
        IBudgetFundingService budgetFundingService,
        Func<DateTime>? utcNow = null)
    {
        _budgetFundingService = budgetFundingService;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    public FundingRequest Create(
        string? departmentCode,
        decimal amount,
        string? reason,
        bool urgent)
    {
        if (string.IsNullOrWhiteSpace(departmentCode))
        {
            throw new ArgumentException("Department code is required.");
        }

        string code = departmentCode.Trim().ToUpperInvariant();

        if (DepartmentBudgets.Find(code) is null)
        {
            throw new ArgumentException("Unknown department code.");
        }

        if (amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero.");
        }

        if (decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentException(
                "Amount must have at most two decimal places.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Reason is required.");
        }

        var request = new FundingRequest(
            Guid.NewGuid(),
            code,
            amount,
            reason.Trim(),
            urgent,
            _utcNow(),
            FundingRequestStatus.Pending,
            null);

        lock (_lock)
        {
            _requests.Add(request);
        }

        return request;
    }

    public FundingRequest[] GetAll()
    {
        lock (_lock)
        {
            return _requests
                .OrderBy(GetPriority)
                .ThenBy(request => request.CreatedAtUtc)
                .ToArray();
        }
    }

    public FundingDecisionResult Decide(Guid id, bool approved)
    {
        lock (_lock)
        {
            int index = _requests.FindIndex(request => request.Id == id);

            if (index < 0)
            {
                return new FundingDecisionResult(
                    FundingDecisionOutcome.NotFound,
                    "Funding request was not found.",
                    null);
            }

            FundingRequest current = _requests[index];

            if (current.Status != FundingRequestStatus.Pending)
            {
                return new FundingDecisionResult(
                    FundingDecisionOutcome.AlreadyDecided,
                    "Funding request has already been decided.",
                    current);
            }

            if (approved)
            {
                _budgetFundingService.AddFunds(
                    current.DepartmentCode,
                    current.Amount);
            }

            FundingRequest decided = current with
            {
                Status = approved
                    ? FundingRequestStatus.Approved
                    : FundingRequestStatus.Rejected,
                DecidedAtUtc = _utcNow()
            };

            _requests[index] = decided;

            return new FundingDecisionResult(
                FundingDecisionOutcome.Success,
                approved
                    ? "Funding request approved."
                    : "Funding request rejected.",
                decided);
        }
    }

    private static int GetPriority(FundingRequest request)
    {
        if (request.Urgent &&
            request.DepartmentCode.Equals(
                "EMS",
                StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return request.Urgent ? 1 : 2;
    }
}