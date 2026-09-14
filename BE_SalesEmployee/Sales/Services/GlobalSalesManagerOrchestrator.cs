using System.Text.Json;
using BE_SalesEmployee.Services;
using Microsoft.Extensions.Logging;

namespace BE_SalesEmployee.Sales.Services;

public enum GlobalOrchestrationStatus
{
    Success = 0,
    PartialFailure = 1,
    Conflict = 2,
    Forbidden = 3,
    BadRequest = 4,
    BranchUnavailable = 5
}

public sealed record BranchOpResult
{
    public string CityValue { get; init; } = "";
    public string CityName { get; init; } = "";
    public string Database { get; init; } = "";
    public bool Ok { get; init; }
    public string Code { get; init; } = "";
    public string? Message { get; init; }
    public int? HttpStatus { get; init; }
    public Guid? ObservedGlobalAccountId { get; init; }
}

public sealed class GlobalOrchestrationResult
{
    public GlobalOrchestrationStatus Status { get; init; }
    public Guid? GlobalAccountId { get; init; }
    public Guid OperationId { get; init; }
    public IReadOnlyList<BranchOpResult> SucceededBranches { get; init; } = [];
    public IReadOnlyList<BranchOpResult> FailedBranches { get; init; } = [];
    public string? Message { get; init; }
}

public interface IGlobalSalesManagerOrchestrator
{
    Task<GlobalOrchestrationResult> CreateAsync(GlobalManagerClientRequest request, CancellationToken ct);
    Task<GlobalOrchestrationResult> UpdateAsync(Guid globalAccountId, GlobalManagerClientRequest request, CancellationToken ct);
    Task<GlobalOrchestrationResult> DisableAsync(Guid globalAccountId, CancellationToken ct);
}

public sealed class GlobalManagerClientRequest
{
    public string UserName { get; init; } = "";
    public string? Email { get; init; }
    public string? Password { get; init; }
    public string? PhoneNumber { get; init; }
    public string? Address { get; init; }
    public string? UserImage { get; init; }
    public int? ActorUserId { get; init; }
}

public sealed class GlobalSalesManagerOrchestrator : IGlobalSalesManagerOrchestrator
{
    private readonly AdminCitiesService _cities;
    private readonly BranchProxyService _proxy;
    private readonly ILogger<GlobalSalesManagerOrchestrator> _logger;

    public GlobalSalesManagerOrchestrator(
        AdminCitiesService cities,
        BranchProxyService proxy,
        ILogger<GlobalSalesManagerOrchestrator> logger)
    {
        _cities = cities;
        _proxy = proxy;
        _logger = logger;
    }

    public Task<GlobalOrchestrationResult> CreateAsync(GlobalManagerClientRequest request, CancellationToken ct) =>
        RunCreateAsync(request, ct);

    public Task<GlobalOrchestrationResult> UpdateAsync(
        Guid globalAccountId,
        GlobalManagerClientRequest request,
        CancellationToken ct) =>
        RunUpdateAsync(globalAccountId, request, ct);

    public async Task<GlobalOrchestrationResult> DisableAsync(Guid globalAccountId, CancellationToken ct)
    {
        var operationId = Guid.NewGuid();
        var branches = await ProductionSalesBranchesAsync(ct);
        if (branches.Count == 0)
        {
            return new GlobalOrchestrationResult
            {
                Status = GlobalOrchestrationStatus.BadRequest,
                OperationId = operationId,
                GlobalAccountId = globalAccountId,
                Message = "لا توجد فروع إنتاج متاحة."
            };
        }

        var succeeded = new List<BranchOpResult>();
        var failed = new List<BranchOpResult>();
        foreach (var branch in branches)
        {
            var op = await CallBranchAsync(
                branch,
                HttpMethod.Post,
                $"internal/sales-users/global-manager/{globalAccountId:D}/disable",
                null,
                ct);
            (op.Ok ? succeeded : failed).Add(op);
        }

        return Summarize(operationId, globalAccountId, succeeded, failed);
    }

    private async Task<GlobalOrchestrationResult> RunCreateAsync(
        GlobalManagerClientRequest request,
        CancellationToken ct)
    {
        var operationId = Guid.NewGuid();
        var userName = (request.UserName ?? "").Trim();
        if (userName.Length == 0)
        {
            return Bad(operationId, "اسم المستخدم مطلوب.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return Bad(operationId, "كلمة المرور مطلوبة.");
        }

        var branches = await ProductionSalesBranchesAsync(ct);
        if (branches.Count == 0)
        {
            return Bad(operationId, "لا توجد فروع إنتاج متاحة.");
        }

        // Phase 1 — discovery preflight (no GlobalAccountId): adopt vs create vs conflict.
        var discovery = new List<BranchOpResult>();
        var observedIds = new List<Guid?>();
        foreach (var branch in branches)
        {
            var pre = await CallBranchAsync(
                branch,
                HttpMethod.Post,
                "internal/sales-users/global-manager/preflight",
                new
                {
                    userName,
                    globalAccountId = (Guid?)null,
                    request.Email,
                    request.PhoneNumber,
                    request.Password
                },
                ct);
            discovery.Add(pre);
            observedIds.Add(pre.ObservedGlobalAccountId);
            if (!pre.Ok)
            {
                return new GlobalOrchestrationResult
                {
                    Status = GlobalOrchestrationStatus.BranchUnavailable,
                    OperationId = operationId,
                    FailedBranches = [pre with { Code = "BRANCH_UNAVAILABLE" }],
                    Message = "تعذر التحقق من بعض الفروع قبل الكتابة."
                };
            }
        }

        var statuses = discovery.Select(d => d.Code).ToList();
        var plan = DecideCreateWritePlan(statuses);
        if (plan == CreateWritePlan.Conflict)
        {
            return new GlobalOrchestrationResult
            {
                Status = GlobalOrchestrationStatus.Conflict,
                OperationId = operationId,
                FailedBranches = discovery
                    .Where(d => IsConflictStatus(d.Code))
                    .Select(RedactBranchForClient)
                    .ToList(),
                Message = "تعارض legacy/هوية في أحد الفروع."
            };
        }

        if (plan == CreateWritePlan.BranchUnavailable)
        {
            return new GlobalOrchestrationResult
            {
                Status = GlobalOrchestrationStatus.BranchUnavailable,
                OperationId = operationId,
                FailedBranches = discovery.Select(RedactBranchForClient).ToList(),
                Message = "تعذر التحقق من بعض الفروع قبل الكتابة."
            };
        }

        if (HasConflictingObservedGlobals(observedIds))
        {
            return new GlobalOrchestrationResult
            {
                Status = GlobalOrchestrationStatus.Conflict,
                OperationId = operationId,
                FailedBranches = discovery,
                Message = "GlobalAccountId متعارض بين الفروع."
            };
        }

        var globalAccountId = observedIds.FirstOrDefault(g => g.HasValue) ?? Guid.NewGuid();

        // Phase 2 — confirm with chosen GlobalAccountId before writes.
        foreach (var branch in branches)
        {
            var pre = await CallBranchAsync(
                branch,
                HttpMethod.Post,
                "internal/sales-users/global-manager/preflight",
                new
                {
                    userName,
                    globalAccountId,
                    request.Email,
                    request.PhoneNumber,
                    request.Password
                },
                ct);
            if (!pre.Ok)
            {
                return new GlobalOrchestrationResult
                {
                    Status = GlobalOrchestrationStatus.BranchUnavailable,
                    OperationId = operationId,
                    GlobalAccountId = globalAccountId,
                    FailedBranches = [pre with { Code = "BRANCH_UNAVAILABLE" }],
                    Message = "تعذر التحقق من بعض الفروع قبل الكتابة."
                };
            }

            if (IsConflictStatus(pre.Code))
            {
                return new GlobalOrchestrationResult
                {
                    Status = GlobalOrchestrationStatus.Conflict,
                    OperationId = operationId,
                    GlobalAccountId = globalAccountId,
                    FailedBranches = [RedactBranchForClient(pre)],
                    Message = "تعارض في أحد الفروع."
                };
            }
        }

        return await FanOutUpsertAsync(operationId, globalAccountId, request, branches, isCreate: true, ct);
    }

    private async Task<GlobalOrchestrationResult> RunUpdateAsync(
        Guid globalAccountId,
        GlobalManagerClientRequest request,
        CancellationToken ct)
    {
        var operationId = Guid.NewGuid();
        var userName = (request.UserName ?? "").Trim();
        if (userName.Length == 0)
        {
            return Bad(operationId, "اسم المستخدم مطلوب.", globalAccountId);
        }

        var branches = await ProductionSalesBranchesAsync(ct);
        if (branches.Count == 0)
        {
            return Bad(operationId, "لا توجد فروع إنتاج متاحة.", globalAccountId);
        }

        foreach (var branch in branches)
        {
            var pre = await CallBranchAsync(
                branch,
                HttpMethod.Post,
                "internal/sales-users/global-manager/preflight",
                new
                {
                    userName,
                    globalAccountId,
                    request.Email,
                    request.PhoneNumber,
                    request.Password
                },
                ct);
            if (!pre.Ok)
            {
                return new GlobalOrchestrationResult
                {
                    Status = GlobalOrchestrationStatus.BranchUnavailable,
                    OperationId = operationId,
                    GlobalAccountId = globalAccountId,
                    FailedBranches = [pre with { Code = "BRANCH_UNAVAILABLE" }],
                    Message = "تعذر التحقق من بعض الفروع قبل الكتابة."
                };
            }

            if (IsConflictStatus(pre.Code))
            {
                return new GlobalOrchestrationResult
                {
                    Status = GlobalOrchestrationStatus.Conflict,
                    OperationId = operationId,
                    GlobalAccountId = globalAccountId,
                    FailedBranches = [RedactBranchForClient(pre)],
                    Message = "تعارض في أحد الفروع."
                };
            }
        }

        return await FanOutUpsertAsync(operationId, globalAccountId, request, branches, isCreate: false, ct);
    }

    private async Task<GlobalOrchestrationResult> FanOutUpsertAsync(
        Guid operationId,
        Guid globalAccountId,
        GlobalManagerClientRequest request,
        IReadOnlyList<AdminCity> branches,
        bool isCreate,
        CancellationToken ct)
    {
        var body = new
        {
            globalAccountId,
            userName = request.UserName,
            request.Email,
            request.Password,
            request.PhoneNumber,
            request.Address,
            request.UserImage
        };

        var succeeded = new List<BranchOpResult>();
        var failed = new List<BranchOpResult>();
        foreach (var branch in branches)
        {
            var path = isCreate
                ? "internal/sales-users/global-manager"
                : $"internal/sales-users/global-manager/{globalAccountId:D}";
            var method = isCreate ? HttpMethod.Post : HttpMethod.Put;
            var op = await CallBranchAsync(branch, method, path, body, ct);
            (op.Ok ? succeeded : failed).Add(op);
        }

        _logger.LogInformation(
            "GlobalSalesManager op={Op} global={Global} ok={Ok}/{Total}",
            isCreate ? "create" : "update",
            globalAccountId,
            succeeded.Count,
            branches.Count);

        return Summarize(operationId, globalAccountId, succeeded, failed);
    }

    private async Task<IReadOnlyList<AdminCity>> ProductionSalesBranchesAsync(CancellationToken ct) =>
        await _cities.GetSalesBranchesAsync(ct);

    private async Task<BranchOpResult> CallBranchAsync(
        AdminCity branch,
        HttpMethod method,
        string relativePath,
        object? jsonBody,
        CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(12));
            var payload = jsonBody == null ? null : JsonSerializer.Serialize(jsonBody);
            using var response = await _proxy.SendManagerAsync(
                branch.Link,
                relativePath,
                method,
                payload,
                "GlobalSalesManagerOrchestrator",
                cts.Token);
            var raw = await response.Content.ReadAsStringAsync(ct);
            var ok = response.IsSuccessStatusCode;
            string code = ok ? "OK" : $"HTTP_{(int)response.StatusCode}";
            string? message = null;
            Guid? observedGlobal = null;
            try
            {
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw);
                if (doc.RootElement.TryGetProperty("code", out var c))
                {
                    code = c.GetString() ?? code;
                }
                else if (doc.RootElement.TryGetProperty("status", out var s))
                {
                    code = s.GetString() ?? code;
                }

                if (doc.RootElement.TryGetProperty("message", out var m))
                {
                    message = m.GetString();
                }

                if (doc.RootElement.TryGetProperty("globalAccountId", out var g)
                    && g.ValueKind != JsonValueKind.Null
                    && Guid.TryParse(g.GetString() ?? g.ToString(), out var parsed))
                {
                    observedGlobal = parsed;
                }
            }
            catch (JsonException)
            {
                message = null;
            }

            if (message != null && message.Length > 300)
            {
                message = message[..300];
            }

            return new BranchOpResult
            {
                CityValue = branch.Value,
                CityName = branch.Name,
                Database = branch.Database,
                Ok = ok,
                Code = code,
                Message = message,
                HttpStatus = (int)response.StatusCode,
                ObservedGlobalAccountId = observedGlobal
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Branch call failed city={City}", branch.Value);
            return new BranchOpResult
            {
                CityValue = branch.Value,
                CityName = branch.Name,
                Database = branch.Database,
                Ok = false,
                Code = "BRANCH_UNAVAILABLE",
                Message = "تعذر الاتصال بالفرع."
            };
        }
    }

    private enum CreateWritePlan
    {
        AdoptOrCreate,
        Conflict,
        BranchUnavailable
    }

    /// <summary>Mirrors BE_Company GlobalSalesManagerRules.DecideCreateWritePlan — unknown statuses fail closed.</summary>
    private static CreateWritePlan DecideCreateWritePlan(IReadOnlyList<string> statuses)
    {
        if (statuses.Count == 0)
        {
            return CreateWritePlan.BranchUnavailable;
        }

        foreach (var s in statuses)
        {
            var status = (s ?? "").Trim();
            if (IsConflictStatus(status))
            {
                return CreateWritePlan.Conflict;
            }

            if (status is "BRANCH_UNAVAILABLE" || status.StartsWith("HTTP_", StringComparison.Ordinal))
            {
                return CreateWritePlan.BranchUnavailable;
            }

            if (status is not ("NotFound" or "ExistingSameGlobalAccount" or "LegacyAdoptable" or "OK"))
            {
                return CreateWritePlan.Conflict;
            }
        }

        return CreateWritePlan.AdoptOrCreate;
    }

    private static bool IsConflictStatus(string? status) =>
        status is "UsernameConflict" or "GlobalIdConflict" or "DuplicateAmbiguous" or "LegacyConflict"
            or "LEGACY_CONFLICT" or "USERNAMECONFLICT" or "DUPLICATEAMBIGUOUS";

    private static bool HasConflictingObservedGlobals(IEnumerable<Guid?> ids)
    {
        var distinct = ids.Where(g => g.HasValue).Select(g => g!.Value).Distinct().ToList();
        return distinct.Count > 1;
    }

    /// <summary>Hide existence/password oracles from FE: city name only + coarse code.</summary>
    private static BranchOpResult RedactBranchForClient(BranchOpResult branch) =>
        branch with
        {
            Code = IsConflictStatus(branch.Code) ? "LEGACY_CONFLICT" : branch.Code,
            Message = null,
            ObservedGlobalAccountId = null
        };

    private static GlobalOrchestrationResult Bad(Guid operationId, string message, Guid? global = null) =>
        new()
        {
            Status = GlobalOrchestrationStatus.BadRequest,
            OperationId = operationId,
            GlobalAccountId = global,
            Message = message
        };

    private static GlobalOrchestrationResult Summarize(
        Guid operationId,
        Guid globalAccountId,
        List<BranchOpResult> succeeded,
        List<BranchOpResult> failed)
    {
        if (failed.Count == 0)
        {
            return new GlobalOrchestrationResult
            {
                Status = GlobalOrchestrationStatus.Success,
                OperationId = operationId,
                GlobalAccountId = globalAccountId,
                SucceededBranches = succeeded,
                FailedBranches = failed,
                Message = "تم إنشاء مدير المبيعات في جميع الفروع بنجاح"
            };
        }

        if (succeeded.Count == 0)
        {
            return new GlobalOrchestrationResult
            {
                Status = GlobalOrchestrationStatus.BranchUnavailable,
                OperationId = operationId,
                GlobalAccountId = globalAccountId,
                SucceededBranches = succeeded,
                FailedBranches = failed.Select(RedactBranchForClient).ToList(),
                Message = "تعذر تنفيذ العملية على الفروع."
            };
        }

        return new GlobalOrchestrationResult
        {
            Status = GlobalOrchestrationStatus.PartialFailure,
            OperationId = operationId,
            GlobalAccountId = globalAccountId,
            SucceededBranches = succeeded,
            FailedBranches = failed.Select(RedactBranchForClient).ToList(),
            Message = "تم التنفيذ جزئياً، تعذر تحديث بعض الفروع"
        };
    }
}
