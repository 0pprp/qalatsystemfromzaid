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
        RunAsync(Guid.NewGuid(), request, isCreate: true, ct);

    public Task<GlobalOrchestrationResult> UpdateAsync(
        Guid globalAccountId,
        GlobalManagerClientRequest request,
        CancellationToken ct) =>
        RunAsync(globalAccountId, request, isCreate: false, ct);

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

    private async Task<GlobalOrchestrationResult> RunAsync(
        Guid globalAccountId,
        GlobalManagerClientRequest request,
        bool isCreate,
        CancellationToken ct)
    {
        var operationId = Guid.NewGuid();
        var userName = (request.UserName ?? "").Trim();
        if (userName.Length == 0)
        {
            return new GlobalOrchestrationResult
            {
                Status = GlobalOrchestrationStatus.BadRequest,
                OperationId = operationId,
                Message = "اسم المستخدم مطلوب."
            };
        }

        if (isCreate && string.IsNullOrWhiteSpace(request.Password))
        {
            return new GlobalOrchestrationResult
            {
                Status = GlobalOrchestrationStatus.BadRequest,
                OperationId = operationId,
                Message = "كلمة المرور مطلوبة."
            };
        }

        var branches = await ProductionSalesBranchesAsync(ct);
        if (branches.Count == 0)
        {
            return new GlobalOrchestrationResult
            {
                Status = GlobalOrchestrationStatus.BadRequest,
                OperationId = operationId,
                Message = "لا توجد فروع إنتاج متاحة."
            };
        }

        // Preflight all branches before any write.
        var preflightFailed = new List<BranchOpResult>();
        foreach (var branch in branches)
        {
            var pre = await CallBranchAsync(
                branch,
                HttpMethod.Post,
                "internal/sales-users/global-manager/preflight",
                new { userName, globalAccountId },
                ct);
            if (!pre.Ok)
            {
                preflightFailed.Add(pre with { Code = "BRANCH_UNAVAILABLE" });
                continue;
            }

            var status = pre.Code;
            if (status is "UsernameConflict" or "DuplicateAmbiguous" or "GlobalIdConflict")
            {
                return new GlobalOrchestrationResult
                {
                    Status = GlobalOrchestrationStatus.Conflict,
                    OperationId = operationId,
                    GlobalAccountId = globalAccountId,
                    FailedBranches = [pre],
                    Message = "تعارض في أحد الفروع."
                };
            }

            if (status is not ("NotFound" or "ExistingSameGlobalAccount" or "OK"))
            {
                // Prefer explicit preflight statuses; OK without status should not happen.
                if (status.StartsWith("HTTP_", StringComparison.Ordinal) || status == "BRANCH_UNAVAILABLE")
                {
                    preflightFailed.Add(pre with { Code = "BRANCH_UNAVAILABLE" });
                }
            }
        }

        if (preflightFailed.Count > 0)
        {
            return new GlobalOrchestrationResult
            {
                Status = GlobalOrchestrationStatus.BranchUnavailable,
                OperationId = operationId,
                GlobalAccountId = globalAccountId,
                FailedBranches = preflightFailed,
                Message = "تعذر التحقق من بعض الفروع قبل الكتابة."
            };
        }

        var body = new
        {
            globalAccountId,
            userName,
            request.Email,
            request.Password,
            request.PhoneNumber,
            request.Address,
            request.UserImage,
            request.ActorUserId
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

    private async Task<IReadOnlyList<AdminCity>> ProductionSalesBranchesAsync(CancellationToken ct)
    {
        // GetSalesBranchesAsync already excludes قانونية/تجريبي/شهري and honors RequireDemoDatabase.
        return await _cities.GetSalesBranchesAsync(ct);
    }

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
            string? message = raw;
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
                    message = raw;
                }

                if (doc.RootElement.TryGetProperty("message", out var m))
                {
                    message = m.GetString();
                }
            }
            catch (JsonException)
            {
                // keep raw
            }

            // Never treat password-looking content specially; just truncate message length.
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
                HttpStatus = (int)response.StatusCode
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

    private static string? ExtractPreflightStatus(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("status", out var s))
            {
                return s.GetString();
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

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
                FailedBranches = failed,
                Message = "تعذر تنفيذ العملية على الفروع."
            };
        }

        return new GlobalOrchestrationResult
        {
            Status = GlobalOrchestrationStatus.PartialFailure,
            OperationId = operationId,
            GlobalAccountId = globalAccountId,
            SucceededBranches = succeeded,
            FailedBranches = failed,
            Message = "تم التنفيذ جزئياً، تعذر تحديث بعض الفروع"
        };
    }
}
