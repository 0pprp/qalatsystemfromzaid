using BE_SalesEmployee.DelegatedManager.Domain;
using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.DelegatedManager.Stores;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Sales.Services;
using BE_SalesEmployee.Services;

namespace BE_SalesEmployee.DelegatedManager.Services;

public sealed class CreateSalesExceptionInput
{
    public string? CityValue { get; init; }
    public string? CityName { get; init; }
    public int? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public string? CustomerPhone { get; init; }
    public int? SalesRequestId { get; init; }
    public string? Reason { get; init; }
    public string? TargetApproverType { get; init; }
}

public enum SalesExceptionOutcome
{
    Success,
    Invalid,
    NotFound,
    Conflict,
    Forbidden
}

public sealed class SalesExceptionResult
{
    public required SalesExceptionOutcome Outcome { get; init; }
    public string? Error { get; init; }
    public SalesExceptionRequest? Request { get; init; }

    public bool Ok => Outcome == SalesExceptionOutcome.Success;

    public static SalesExceptionResult Invalid(string error) =>
        new() { Outcome = SalesExceptionOutcome.Invalid, Error = error };

    public static SalesExceptionResult NotFound() =>
        new() { Outcome = SalesExceptionOutcome.NotFound, Error = "الطلب غير موجود" };

    public static SalesExceptionResult Conflict(string error, SalesExceptionRequest? existing = null) =>
        new() { Outcome = SalesExceptionOutcome.Conflict, Error = error, Request = existing };

    public static SalesExceptionResult Forbidden(string error) =>
        new() { Outcome = SalesExceptionOutcome.Forbidden, Error = error };

    public static SalesExceptionResult Success(SalesExceptionRequest request) =>
        new() { Outcome = SalesExceptionOutcome.Success, Request = request };
}

public sealed class SalesExceptionDetail
{
    public required SalesExceptionRequest Request { get; init; }
    public required IReadOnlyList<SalesExceptionAuditEntry> Audit { get; init; }
    /// <summary>Branch same-province review payload (null when not loaded).</summary>
    public object? Review { get; init; }
    /// <summary>Full customer profile when WrongSale has CustomerId but no SalesRequestId.</summary>
    public object? CustomerProfile { get; init; }
    public string? ReviewError { get; init; }
}

public sealed class SalesExceptionService
{
    private readonly ISalesExceptionStore _store;
    private readonly IBranchNotePoster _notePoster;
    private readonly IBranchExceptionHoldPoster _holdPoster;
    private readonly ISalesManagerBranchAggregator? _aggregator;

    public SalesExceptionService(
        ISalesExceptionStore store,
        IBranchNotePoster notePoster,
        IBranchExceptionHoldPoster? holdPoster = null,
        ISalesManagerBranchAggregator? aggregator = null)
    {
        _store = store;
        _notePoster = notePoster;
        _holdPoster = holdPoster ?? new IntentRecordingBranchExceptionHoldPoster();
        _aggregator = aggregator;
    }

    public async Task<SalesExceptionResult> CreateAsync(
        GatewayUser requester,
        CreateSalesExceptionInput input,
        CancellationToken ct = default)
    {
        if (!SalesRoles.IsSalesManager(requester.UserType))
        {
            return SalesExceptionResult.Forbidden("طلب الاستثناء متاح لمدير المبيعات فقط");
        }

        var reason = input.Reason?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(reason))
        {
            return SalesExceptionResult.Invalid("سبب الاستثناء مطلوب");
        }

        var cityValue = Trimmed(input.CityValue) ?? Trimmed(requester.CityValue);
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return SalesExceptionResult.Invalid("المحافظة مطلوبة");
        }

        var target = Trimmed(input.TargetApproverType) ?? TargetApproverTypes.DelegatedManager;
        if (!string.Equals(target, TargetApproverTypes.DelegatedManager, StringComparison.Ordinal)
            && !string.Equals(target, TargetApproverTypes.BranchManager, StringComparison.Ordinal))
        {
            return SalesExceptionResult.Invalid("نوع الجهة المعتمدة غير صحيح");
        }

        if (input.SalesRequestId is int salesRequestId and > 0)
        {
            var active = await _store.FindActiveBySalesRequestIdAsync(salesRequestId, ct);
            if (active is not null)
            {
                return SalesExceptionResult.Conflict("يوجد طلب استثناء نشط لهذا الطلب", active);
            }
        }

        // WrongSale from first-collection / follower auth: idempotent on customer (retry-safe).
        var isWrongSaleIntake = reason.Contains("مبيعات خاطئة", StringComparison.Ordinal)
                                || reason.Contains("تقييم المتجر", StringComparison.Ordinal);
        if (isWrongSaleIntake && input.CustomerId is int wrongCustomerId and > 0
            && (input.SalesRequestId is null or <= 0))
        {
            var existingWrong = await _store.FindPendingWrongSaleByCustomerIdAsync(wrongCustomerId, ct);
            if (existingWrong is not null)
            {
                return SalesExceptionResult.Success(existingWrong);
            }
        }

        var now = DateTime.UtcNow;
        var displayName = Trimmed(requester.UserName) ?? Trimmed(requester.UserID) ?? "مدير المبيعات";
        var request = new SalesExceptionRequest
        {
            Id = Guid.NewGuid(),
            CityValue = cityValue,
            CityName = Trimmed(input.CityName) ?? Trimmed(requester.CityName),
            CustomerId = input.CustomerId,
            CustomerName = Trimmed(input.CustomerName),
            CustomerPhone = Trimmed(input.CustomerPhone),
            SalesRequestId = input.SalesRequestId,
            RequestingManagerUserName = Trimmed(requester.UserName) ?? Trimmed(requester.UserID) ?? "",
            RequestingManagerDisplayName = displayName,
            Reason = reason,
            TargetApproverType = target,
            Status = ExceptionStatuses.Pending,
            RequestedAtUtc = now
        };

        var audit = new SalesExceptionAuditEntry
        {
            ActorUserName = request.RequestingManagerUserName,
            ActorDisplayName = displayName,
            ActorRole = SalesRoles.SalesManager,
            PreviousStatus = null,
            NewStatus = ExceptionStatuses.Pending,
            CityValue = cityValue,
            CreatedAtUtc = now
        };

        var created = await _store.CreateAsync(request, audit, ct);
        // Hide the SalesRequest from «غير مسند» until DM decides / assign consumes.
        await _holdPoster.SyncHoldAsync(created, ExceptionStatuses.Pending, ct);
        return SalesExceptionResult.Success(created);
    }

    public Task<PagedResult<SalesExceptionRequest>> ListAsync(SalesExceptionQuery query, CancellationToken ct = default)
    {
        var (page, pageSize) = PagingDefaults.Normalize(query.Page, query.PageSize);
        return _store.QueryAsync(new SalesExceptionQuery
        {
            Page = page,
            PageSize = pageSize,
            Status = query.Status,
            CityValue = query.CityValue,
            TargetApproverType = query.TargetApproverType,
            RequestingManagerUserName = query.RequestingManagerUserName
        }, ct);
    }

    public async Task<SalesExceptionDetail?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var request = await _store.GetAsync(id, ct);
        if (request is null)
        {
            return null;
        }

        return new SalesExceptionDetail
        {
            Request = request,
            Audit = await _store.GetAuditAsync(id, ct)
        };
    }

    /// <summary>
    /// DM detail with same-province review context from the branch identified by CityValue.
    /// On success, <see cref="SalesExceptionDetail.Review"/> is populated.
    /// On missing SalesRequest / branch failure, returns Invalid with Arabic error (Request still set when known).
    /// </summary>
    public async Task<(SalesExceptionOutcome Outcome, SalesExceptionDetail? Detail, string? Error)> GetEnrichedDetailAsync(
        Guid id,
        CancellationToken ct = default)
    {
        var request = await _store.GetAsync(id, ct);
        if (request is null)
        {
            return (SalesExceptionOutcome.NotFound, null, "الطلب غير موجود");
        }

        var audit = await _store.GetAuditAsync(id, ct);
        var isWrongSale = IsWrongSaleReason(request.Reason);

        // WrongSale (أو أي استثناء بلا SalesRequestId) يجب أن يُفتح دائماً —
        // بروفايل + مطابقات الاسم/الهاتف من الفرع.
        if (request.SalesRequestId is null or <= 0 || isWrongSale)
        {
            object? customerProfile = null;
            object? review = null;
            string? profileError = null;
            if (request.CustomerId is > 0
                && !string.IsNullOrWhiteSpace(request.CityValue)
                && _aggregator is not null)
            {
                (customerProfile, profileError) = await TryLoadCustomerProfileAsync(
                    request, request.CustomerId.Value, ct);
                (review, var matchErr) = await TryLoadCustomerMatchReviewAsync(
                    request, request.CustomerId.Value, ct);
                profileError ??= matchErr;
            }
            else if (isWrongSale && request.CustomerId is null or <= 0)
            {
                profileError = "لا يوجد معرف زبون مرتبط بهذا الإشعار.";
            }

            return (SalesExceptionOutcome.Success, new SalesExceptionDetail
            {
                Request = request,
                Audit = audit,
                Review = review,
                CustomerProfile = customerProfile,
                ReviewError = profileError
            }, null);
        }

        if (string.IsNullOrWhiteSpace(request.CityValue))
        {
            return (SalesExceptionOutcome.Success, new SalesExceptionDetail
            {
                Request = request,
                Audit = audit,
                ReviewError = "محافظة الطلب غير متوفرة."
            }, null);
        }

        if (_aggregator is null)
        {
            return (SalesExceptionOutcome.Success, new SalesExceptionDetail
            {
                Request = request,
                Audit = audit,
                Review = null,
                ReviewError = "مراجع الفرع غير متاح في هذا السياق."
            }, null);
        }

        var proxyUser = BuildProxyUser(request);

        try
        {
            var path = $"sales-manager/sales-requests/{request.SalesRequestId.Value}/exception-review-context";
            var (status, body) = await _aggregator.GetOneAsync(proxyUser, request.CityValue, path, ct);
            object? customerProfile = null;
            string? reviewError = null;
            object? review = null;

            if (status == 404)
            {
                reviewError = "لا يمكن تحميل تفاصيل طلب البيع الأصلي.";
            }
            else if (status is < 200 or >= 300)
            {
                reviewError = ExtractBranchMessage(body) ?? "تعذر تحميل بيانات المراجعة من الفرع.";
            }
            else
            {
                review = UnwrapReviewBody(body);
            }

            // Always attach profile for WrongSale so DM sees shop evals / payments.
            if (isWrongSale && request.CustomerId is > 0)
            {
                (customerProfile, var profileErr) = await TryLoadCustomerProfileAsync(
                    request, request.CustomerId.Value, ct);
                reviewError ??= profileErr;
            }

            return (SalesExceptionOutcome.Success, new SalesExceptionDetail
            {
                Request = request,
                Audit = audit,
                Review = review,
                CustomerProfile = customerProfile,
                ReviewError = reviewError
            }, null);
        }
        catch
        {
            object? customerProfile = null;
            if (request.CustomerId is > 0)
            {
                (customerProfile, _) = await TryLoadCustomerProfileAsync(
                    request, request.CustomerId.Value, ct);
            }

            return (SalesExceptionOutcome.Success, new SalesExceptionDetail
            {
                Request = request,
                Audit = audit,
                CustomerProfile = customerProfile,
                ReviewError = "تعذر تحميل بيانات المراجعة من الفرع."
            }, null);
        }
    }

    private static bool IsWrongSaleReason(string? reason) =>
        !string.IsNullOrWhiteSpace(reason)
        && (reason.Contains("مبيعات خاطئة", StringComparison.Ordinal)
            || reason.Contains("تقييم المتجر", StringComparison.Ordinal));

    private static GatewayUser BuildProxyUser(SalesExceptionRequest request) => new()
    {
        UserID = "0",
        UserName = request.RequestingManagerUserName ?? "delegated-manager",
        UserType = SalesRoles.UserTypeSalesManager,
        IsCentral = true,
        CityValue = request.CityValue,
        CityName = request.CityName ?? ""
    };

    private async Task<(object? Profile, string? Error)> TryLoadCustomerProfileAsync(
        SalesExceptionRequest request,
        int customerId,
        CancellationToken ct)
    {
        if (_aggregator is null || string.IsNullOrWhiteSpace(request.CityValue))
        {
            return (null, "مراجع الفرع غير متاح.");
        }

        try
        {
            var path = $"sales-manager/customers/{customerId}/profile";
            var (status, body) = await _aggregator.GetOneAsync(
                BuildProxyUser(request), request.CityValue, path, ct);
            if (status is >= 200 and < 300)
            {
                return (body, null);
            }

            return (null, ExtractBranchMessage(body) ?? "تعذر تحميل بروفايل الزبون من الفرع.");
        }
        catch
        {
            return (null, "تعذر تحميل بروفايل الزبون من الفرع.");
        }
    }

    /// <summary>Same-province name/phone matches (reuses waiver-review-context).</summary>
    private async Task<(object? Review, string? Error)> TryLoadCustomerMatchReviewAsync(
        SalesExceptionRequest request,
        int customerId,
        CancellationToken ct)
    {
        if (_aggregator is null || string.IsNullOrWhiteSpace(request.CityValue))
        {
            return (null, null);
        }

        try
        {
            var path = $"sales-manager/customers/{customerId}/waiver-review-context";
            var (status, body) = await _aggregator.GetOneAsync(
                BuildProxyUser(request), request.CityValue, path, ct);
            if (status is >= 200 and < 300)
            {
                // WrongSale / DM: never show the same customer as a "similar" match.
                return (ExcludeSelfFromMatchReview(UnwrapReviewBody(body), customerId), null);
            }

            return (null, null);
        }
        catch
        {
            return (null, null);
        }
    }

    /// <summary>
    /// Waiver context includes the target row as «الزبون الحالي»; WrongSale inbox
    /// only wants other province accounts (prior name/phone matches).
    /// </summary>
    private static object? ExcludeSelfFromMatchReview(object? review, int customerId)
    {
        if (review is null || customerId <= 0)
        {
            return review;
        }

        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(review);
            var node = System.Text.Json.Nodes.JsonNode.Parse(json);
            if (node is null)
            {
                return review;
            }

            var matches = node["matchingCustomers"] ?? node["MatchingCustomers"];
            if (matches is not System.Text.Json.Nodes.JsonArray arr)
            {
                return review;
            }

            var filtered = new System.Text.Json.Nodes.JsonArray();
            foreach (var item in arr)
            {
                if (item is null)
                {
                    continue;
                }

                var idNode = item["customerId"] ?? item["CustomerId"];
                var id = idNode?.GetValue<int?>() ?? 0;
                var reasons = item["matchReasons"] ?? item["MatchReasons"];
                var isSelf = id == customerId;
                if (!isSelf && reasons is System.Text.Json.Nodes.JsonArray reasonArr)
                {
                    foreach (var r in reasonArr)
                    {
                        var text = r?.ToString() ?? "";
                        if (text.Contains("الزبون الحالي", StringComparison.Ordinal))
                        {
                            isSelf = true;
                            break;
                        }
                    }
                }

                if (!isSelf)
                {
                    filtered.Add(item.DeepClone());
                }
            }

            if (node["matchingCustomers"] is not null)
            {
                node["matchingCustomers"] = filtered;
            }
            else
            {
                node["MatchingCustomers"] = filtered;
            }

            var classification = node["customerClassification"] ?? node["CustomerClassification"];
            if (classification is not null)
            {
                if (classification["matchCount"] is not null)
                {
                    classification["matchCount"] = filtered.Count;
                }

                if (classification["MatchCount"] is not null)
                {
                    classification["MatchCount"] = filtered.Count;
                }
            }

            return node;
        }
        catch
        {
            return review;
        }
    }

    public Task<SalesExceptionRequest?> MarkReadAsync(Guid id, CancellationToken ct = default) =>
        _store.MarkReadAsync(id, DateTime.UtcNow, ct);

    private static string? ExtractBranchMessage(object? body)
    {
        if (body is null)
        {
            return null;
        }

        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(body);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
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

    /// <summary>
    /// Aggregator Stamp may wrap payloads; prefer inner review-shaped object when present.
    /// </summary>
    private static object? UnwrapReviewBody(object? body) => body;

    public Task<IReadOnlyDictionary<string, int>> DashboardCountsAsync(string? cityValue, CancellationToken ct = default) =>
        _store.CountByStatusAsync(cityValue, ct);

    /// <summary>Hold-only status counts for dashboard (excludes WrongSale).</summary>
    public Task<IReadOnlyDictionary<string, int>> HoldDashboardCountsAsync(
        string? cityValue,
        CancellationToken ct = default) =>
        _store.CountHoldByStatusAsync(cityValue, ct);

    public Task<int> UnreadWrongSalesCountAsync(string? cityValue, CancellationToken ct = default) =>
        _store.CountUnreadWrongSalesAsync(cityValue, ct);

    /// <summary>
    /// Applies Approve/Reject/Cancel. Anything already decided comes back as Conflict so the API can answer 409.
    /// </summary>
    public async Task<SalesExceptionResult> DecideAsync(
        GatewayUser actor,
        Guid id,
        string? decision,
        string? note = null,
        CancellationToken ct = default)
    {
        var newStatus = NormalizeDecision(decision);
        if (newStatus is null)
        {
            return SalesExceptionResult.Invalid("القرار غير صحيح");
        }

        var actorRole = SalesRoles.ToModuleRole(actor.UserType);
        if (!CanDecide(actorRole, newStatus))
        {
            return SalesExceptionResult.Forbidden("غير مخوّل لاتخاذ هذا القرار");
        }

        var existing = await _store.GetAsync(id, ct);
        if (existing is null)
        {
            return SalesExceptionResult.NotFound();
        }

        if (IsWrongSaleReason(existing.Reason))
        {
            return SalesExceptionResult.Forbidden(
                "المبيعات الخاطئة إشعار اطلاع فقط — لا موافقة ولا رفض.");
        }

        if (string.Equals(actorRole, SalesRoles.SalesManager, StringComparison.Ordinal)
            && !string.Equals(existing.RequestingManagerUserName, actor.UserName?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return SalesExceptionResult.Forbidden("يمكن إلغاء الطلبات الخاصة بك فقط");
        }

        if (!ExceptionStateMachine.CanTransition(existing.Status, newStatus))
        {
            return SalesExceptionResult.Conflict($"لا يمكن تغيير الحالة من {existing.Status} إلى {newStatus}");
        }

        var now = DateTime.UtcNow;
        var displayName = Trimmed(actor.UserName) ?? Trimmed(actor.UserID) ?? "";
        var decided = await _store.TryDecideAsync(
            id,
            ExceptionStatuses.Pending,
            new SalesExceptionDecision
            {
                NewStatus = newStatus,
                DecisionMakerUserName = Trimmed(actor.UserName) ?? "",
                DecisionMakerDisplayName = displayName,
                DecisionNote = Trimmed(note),
                DecidedAtUtc = now
            },
            new SalesExceptionAuditEntry
            {
                ActorUserName = Trimmed(actor.UserName) ?? "",
                ActorDisplayName = displayName,
                ActorRole = actorRole ?? Trimmed(actor.UserType) ?? "Unknown",
                PreviousStatus = existing.Status,
                NewStatus = newStatus,
                DecisionNote = Trimmed(note),
                CityValue = existing.CityValue,
                CreatedAtUtc = now
            },
            ct);

        if (decided is null)
        {
            // Lost the race: another decision landed between the read and the guarded update.
            return SalesExceptionResult.Conflict("تمت معالجة الطلب مسبقاً");
        }

        if (string.Equals(newStatus, ExceptionStatuses.Approved, StringComparison.Ordinal)
            && await _notePoster.PostApprovalNoteAsync(decided, ct))
        {
            await _store.MarkBranchNotePostedAsync(decided.Id, ct);
            decided.BranchCustomerNotePosted = true;
        }

        // Branch denormalized hold: Approved/Rejected keep hold; Cancelled clears it.
        string? holdStatus = newStatus switch
        {
            ExceptionStatuses.Approved => ExceptionStatuses.Approved,
            ExceptionStatuses.Rejected => ExceptionStatuses.Rejected,
            ExceptionStatuses.Cancelled => null,
            _ => ExceptionStatuses.Pending
        };
        await _holdPoster.SyncHoldAsync(decided, holdStatus, ct);

        return SalesExceptionResult.Success(decided);
    }

    /// <summary>
    /// Race-safe consume of an Approved exception after branch AssignAsync succeeds.
    /// </summary>
    public async Task<SalesExceptionResult> TryConsumeAssignmentAsync(
        GatewayUser manager,
        Guid id,
        int employeeId,
        string? employeeName,
        CancellationToken ct = default)
    {
        if (employeeId <= 0)
        {
            return SalesExceptionResult.Invalid("يجب اختيار موظف المبيعات");
        }

        var existing = await _store.GetAsync(id, ct);
        if (existing is null)
        {
            return SalesExceptionResult.NotFound();
        }

        if (!string.Equals(existing.Status, ExceptionStatuses.Approved, StringComparison.Ordinal)
            || existing.AssignmentConsumed)
        {
            return SalesExceptionResult.Conflict("لا يمكن إسناد هذا الاستثناء");
        }

        var now = DateTime.UtcNow;
        var managerName = Trimmed(manager.UserName) ?? Trimmed(manager.UserID) ?? "";
        var consumed = await _store.TryConsumeAsync(
            id,
            employeeId,
            Trimmed(employeeName),
            managerName,
            now,
            new SalesExceptionAuditEntry
            {
                ActorUserName = managerName,
                ActorDisplayName = managerName,
                ActorRole = SalesRoles.SalesManager,
                PreviousStatus = existing.Status,
                NewStatus = existing.Status,
                DecisionNote = $"AssignmentConsumed -> employee {employeeId}",
                CityValue = existing.CityValue,
                CreatedAtUtc = now
            },
            ct);

        if (consumed is null)
        {
            return SalesExceptionResult.Conflict("تم إسناد الاستثناء مسبقاً");
        }

        return SalesExceptionResult.Success(consumed);
    }

    private static bool CanDecide(string? actorRole, string newStatus) => actorRole switch
    {
        SalesRoles.DelegatedManager => true,
        SalesRoles.SalesManager => string.Equals(newStatus, ExceptionStatuses.Cancelled, StringComparison.Ordinal),
        _ => false
    };

    private static string? NormalizeDecision(string? decision)
    {
        if (string.IsNullOrWhiteSpace(decision))
        {
            return null;
        }

        return decision.Trim().ToLowerInvariant() switch
        {
            "approve" or "approved" => ExceptionStatuses.Approved,
            "reject" or "rejected" => ExceptionStatuses.Rejected,
            "cancel" or "cancelled" or "canceled" => ExceptionStatuses.Cancelled,
            _ => null
        };
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
