using System.Globalization;
using System.Text.Json;
using BE_SalesEmployee.DelegatedManager.Domain;
using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.DelegatedManager.Stores;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Sales.Services;
using BE_SalesEmployee.Services;

namespace BE_SalesEmployee.DelegatedManager.Services;

public sealed class CreateCustomerWaiverInput
{
    public string? CityValue { get; init; }
    public string? CityName { get; init; }
    public int CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public string? CustomerPhone { get; init; }
    public string? Note { get; init; }
}

public enum CustomerWaiverOutcome
{
    Success,
    Invalid,
    NotFound,
    Conflict,
    Forbidden
}

public sealed class CustomerWaiverResult
{
    public required CustomerWaiverOutcome Outcome { get; init; }
    public string? Error { get; init; }
    public CustomerWaiverRequest? Request { get; init; }

    public bool Ok => Outcome == CustomerWaiverOutcome.Success;

    public static CustomerWaiverResult Invalid(string error) =>
        new() { Outcome = CustomerWaiverOutcome.Invalid, Error = error };

    public static CustomerWaiverResult NotFound() =>
        new() { Outcome = CustomerWaiverOutcome.NotFound, Error = "الطلب غير موجود" };

    public static CustomerWaiverResult Conflict(string error) =>
        new() { Outcome = CustomerWaiverOutcome.Conflict, Error = error };

    public static CustomerWaiverResult Forbidden(string error) =>
        new() { Outcome = CustomerWaiverOutcome.Forbidden, Error = error };

    public static CustomerWaiverResult Success(CustomerWaiverRequest request) =>
        new() { Outcome = CustomerWaiverOutcome.Success, Request = request };
}

public sealed class CustomerWaiverDetail
{
    public required CustomerWaiverRequest Request { get; init; }
    public required IReadOnlyList<CustomerWaiverAuditEntry> Audit { get; init; }
    public object? Review { get; init; }
    public string? ReviewError { get; init; }
}

public interface IBranchWaiverApplyPoster
{
    Task<(bool Ok, string? Error)> ApplyAsync(CustomerWaiverRequest request, double amount, CancellationToken ct = default);
}

public sealed class IntentRecordingBranchWaiverApplyPoster : IBranchWaiverApplyPoster
{
    public Task<(bool Ok, string? Error)> ApplyAsync(
        CustomerWaiverRequest request,
        double amount,
        CancellationToken ct = default) =>
        Task.FromResult<(bool, string?)>((true, null));
}

public sealed class GatewayWaiverApplyPoster : IBranchWaiverApplyPoster
{
    private readonly ISalesManagerBranchAggregator _aggregator;

    public GatewayWaiverApplyPoster(ISalesManagerBranchAggregator aggregator)
    {
        _aggregator = aggregator;
    }

    public async Task<(bool Ok, string? Error)> ApplyAsync(
        CustomerWaiverRequest request,
        double amount,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CityValue) || request.CustomerId <= 0 || amount <= 0)
        {
            return (false, "بيانات التطبيق غير مكتملة");
        }

        var user = new GatewayUser
        {
            UserID = "0",
            UserName = request.DecisionMakerUserName
                       ?? request.BranchManagerUserName
                       ?? "delegated-manager",
            UserType = SalesRoles.UserTypeDelegatedManager,
            IsCentral = true,
            IsDelegatedManager = true,
            CityValue = request.CityValue,
            CityName = request.CityName ?? ""
        };

        var customerName = (request.CustomerName ?? "").Trim();
        var amountText = amount.ToString("#,##0.##", CultureInfo.InvariantCulture);
        var note = $"تم التنازل عن المبلغ {amountText} من حساب الزبون {customerName}";

        var json = JsonSerializer.Serialize(new
        {
            waiverId = request.Id,
            customerId = request.CustomerId,
            customerName,
            amount,
            note,
            decidedByUserName = request.DecisionMakerUserName,
            decidedByDisplayName = request.DecisionMakerDisplayName
        });

        try
        {
            var (status, body) = await _aggregator.PostAsync(
                user, request.CityValue, "sales-manager/waivers/apply", json, ct);
            if (status is >= 200 and < 300)
            {
                return (true, null);
            }

            return (false, ExtractMessage(body) ?? "تعذر تسجيل التسديد في الفرع");
        }
        catch
        {
            return (false, "تعذر الاتصال بفرع الزبون لتسجيل التسديد");
        }
    }

    private static string? ExtractMessage(object? body)
    {
        if (body is null) return null;
        try
        {
            var json = JsonSerializer.Serialize(body);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("message", out var msg))
            {
                return msg.GetString();
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }
}

public sealed class CustomerWaiverService
{
    private readonly ICustomerWaiverStore _store;
    private readonly IBranchWaiverApplyPoster _applyPoster;
    private readonly ISalesManagerBranchAggregator? _aggregator;

    public CustomerWaiverService(
        ICustomerWaiverStore store,
        IBranchWaiverApplyPoster? applyPoster = null,
        ISalesManagerBranchAggregator? aggregator = null)
    {
        _store = store;
        _applyPoster = applyPoster ?? new IntentRecordingBranchWaiverApplyPoster();
        _aggregator = aggregator;
    }

    public async Task<CustomerWaiverResult> CreateAsync(
        GatewayUser requester,
        CreateCustomerWaiverInput input,
        CancellationToken ct = default)
    {
        // Branch BE already authenticated مدير فرع; internal route trusts that assertion.
        if (!IsTrustedBranchManager(requester))
        {
            return CustomerWaiverResult.Forbidden("طلب التنازل متاح لمدير الفرع فقط");
        }

        var note = input.Note?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(note))
        {
            return CustomerWaiverResult.Invalid("ملاحظة مدير الفرع إجبارية");
        }

        if (note.Length > 2000)
        {
            return CustomerWaiverResult.Invalid("الحد الأقصى للملاحظة 2000 حرف");
        }

        if (input.CustomerId <= 0)
        {
            return CustomerWaiverResult.Invalid("معرّف الزبون مطلوب");
        }

        var cityValue = Trimmed(input.CityValue) ?? Trimmed(requester.CityValue);
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return CustomerWaiverResult.Invalid("محافظة الفرع مطلوبة");
        }

        var display = Trimmed(requester.UserName) ?? "مدير فرع";
        var row = new CustomerWaiverRequest
        {
            CityValue = cityValue,
            CityName = Trimmed(input.CityName) ?? Trimmed(requester.CityName),
            CustomerId = input.CustomerId,
            CustomerName = Trimmed(input.CustomerName),
            CustomerPhone = Trimmed(input.CustomerPhone),
            BranchManagerUserName = display,
            BranchManagerDisplayName = display,
            BranchManagerUserId = int.TryParse(requester.UserID, out var uid) ? uid : null,
            Note = note,
            Status = ExceptionStatuses.Pending,
            RequestedAtUtc = DateTime.UtcNow
        };

        var created = await _store.CreateAsync(row, new CustomerWaiverAuditEntry
        {
            Action = "Created",
            ActorUserName = row.BranchManagerUserName,
            ActorDisplayName = row.BranchManagerDisplayName,
            DetailJson = JsonSerializer.Serialize(new { customerId = row.CustomerId })
        }, ct);

        return CustomerWaiverResult.Success(created);
    }

    public Task<PagedResult<CustomerWaiverRequest>> ListAsync(CustomerWaiverQuery query, CancellationToken ct = default) =>
        _store.QueryAsync(query, ct);

    public Task<IReadOnlyDictionary<string, int>> DashboardCountsAsync(
        string? cityValue,
        string? branchManagerUserName,
        CancellationToken ct = default) =>
        _store.CountByStatusAsync(cityValue, branchManagerUserName, ct);

    public async Task<(CustomerWaiverOutcome Outcome, CustomerWaiverDetail? Detail, string? Error)> GetDetailAsync(
        Guid id,
        CancellationToken ct = default)
    {
        var request = await _store.GetAsync(id, ct);
        if (request is null)
        {
            return (CustomerWaiverOutcome.NotFound, null, "الطلب غير موجود");
        }

        var audit = await _store.GetAuditAsync(id, ct);
        if (request.CustomerId <= 0 || string.IsNullOrWhiteSpace(request.CityValue) || _aggregator is null)
        {
            return (CustomerWaiverOutcome.Success, new CustomerWaiverDetail
            {
                Request = request,
                Audit = audit,
                ReviewError = _aggregator is null ? "مراجع الفرع غير متاح" : null
            }, null);
        }

        var proxyUser = new GatewayUser
        {
            UserID = "0",
            UserName = request.BranchManagerUserName ?? "delegated-manager",
            UserType = SalesRoles.UserTypeSalesManager,
            IsCentral = true,
            CityValue = request.CityValue,
            CityName = request.CityName ?? ""
        };

        try
        {
            var path = $"sales-manager/customers/{request.CustomerId}/waiver-review-context";
            var (status, body) = await _aggregator.GetOneAsync(proxyUser, request.CityValue, path, ct);
            if (status is < 200 or >= 300)
            {
                return (CustomerWaiverOutcome.Success, new CustomerWaiverDetail
                {
                    Request = request,
                    Audit = audit,
                    ReviewError = "تعذر تحميل تفاصيل الزبون من الفرع"
                }, null);
            }

            return (CustomerWaiverOutcome.Success, new CustomerWaiverDetail
            {
                Request = request,
                Audit = audit,
                Review = body
            }, null);
        }
        catch
        {
            return (CustomerWaiverOutcome.Success, new CustomerWaiverDetail
            {
                Request = request,
                Audit = audit,
                ReviewError = "تعذر تحميل تفاصيل الزبون من الفرع"
            }, null);
        }
    }

    public async Task<CustomerWaiverResult> DecideAsync(
        GatewayUser actor,
        Guid id,
        string? decision,
        string? note,
        double? approvedAmount,
        CancellationToken ct = default)
    {
        if (!SalesRoles.IsDelegatedManager(actor.UserType))
        {
            return CustomerWaiverResult.Forbidden("قرار التنازل متاح للمدير المفوض فقط");
        }

        var newStatus = NormalizeDecision(decision);
        if (newStatus is null)
        {
            return CustomerWaiverResult.Invalid("القرار غير صحيح");
        }

        if (string.Equals(newStatus, ExceptionStatuses.Approved, StringComparison.Ordinal))
        {
            if (approvedAmount is null or <= 0)
            {
                return CustomerWaiverResult.Invalid("مبلغ التنازل إجباري عند الموافقة");
            }
        }

        var existing = await _store.GetAsync(id, ct);
        if (existing is null)
        {
            return CustomerWaiverResult.NotFound();
        }

        if (!ExceptionStateMachine.CanTransition(existing.Status, newStatus))
        {
            return CustomerWaiverResult.Conflict($"لا يمكن تغيير الحالة من {existing.Status} إلى {newStatus}");
        }

        // Approve: post payment + notify first, then lock the decision row.
        if (string.Equals(newStatus, ExceptionStatuses.Approved, StringComparison.Ordinal)
            && approvedAmount is > 0)
        {
            var preview = existing.Clone();
            preview.DecisionMakerUserName = Trimmed(actor.UserName) ?? "المدير المفوض";
            preview.DecisionMakerDisplayName = Trimmed(actor.UserName) ?? "المدير المفوض";
            preview.ApprovedAmount = approvedAmount;
            var (ok, error) = await _applyPoster.ApplyAsync(preview, approvedAmount.Value, ct);
            if (!ok)
            {
                return CustomerWaiverResult.Invalid(error ?? "تعذر تسجيل التسديد بعد الموافقة");
            }
        }

        var decided = await _store.TryDecideAsync(
            id,
            ExceptionStatuses.Pending,
            new CustomerWaiverDecision
            {
                NewStatus = newStatus,
                DecisionMakerUserName = Trimmed(actor.UserName) ?? "المدير المفوض",
                DecisionMakerDisplayName = Trimmed(actor.UserName) ?? "المدير المفوض",
                DecisionNote = Trimmed(note),
                ApprovedAmount = string.Equals(newStatus, ExceptionStatuses.Approved, StringComparison.Ordinal)
                    ? approvedAmount
                    : null,
                DecidedAtUtc = DateTime.UtcNow
            },
            new CustomerWaiverAuditEntry
            {
                Action = newStatus,
                ActorUserName = Trimmed(actor.UserName),
                ActorDisplayName = Trimmed(actor.UserName),
                DetailJson = JsonSerializer.Serialize(new { approvedAmount, note })
            },
            ct);

        if (decided is null)
        {
            return CustomerWaiverResult.Conflict("تم اتخاذ قرار مسبقاً على هذا الطلب");
        }

        if (string.Equals(newStatus, ExceptionStatuses.Approved, StringComparison.Ordinal))
        {
            await _store.MarkPaymentPostedAsync(decided.Id, ct);
            await _store.MarkAccountantNotifiedAsync(decided.Id, ct);
            decided.PaymentPosted = true;
            decided.AccountantNotified = true;
        }

        return CustomerWaiverResult.Success(decided);
    }

    private static bool IsTrustedBranchManager(GatewayUser requester)
    {
        var type = requester.UserType ?? "";
        return type.Contains("فرع", StringComparison.Ordinal)
               || type.Contains("Branch", StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeDecision(string? decision)
    {
        var value = decision?.Trim() ?? "";
        if (string.Equals(value, ExceptionStatuses.Approved, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "Approve", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "موافقة", StringComparison.Ordinal))
        {
            return ExceptionStatuses.Approved;
        }

        if (string.Equals(value, ExceptionStatuses.Rejected, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "Reject", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "رفض", StringComparison.Ordinal))
        {
            return ExceptionStatuses.Rejected;
        }

        return null;
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
