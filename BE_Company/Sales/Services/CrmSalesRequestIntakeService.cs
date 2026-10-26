using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Filtering;
using BE_Company.Sales.Models;

namespace BE_Company.Sales.Services;

public sealed class CrmSalesRequestIntakeRequest
{
    public Guid CrmOrderUuid { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerProvince { get; set; }
    public string? CustomerAddress { get; set; }
    public string? Notes { get; set; }
    public string? ProductName { get; set; }
}

public sealed class CrmSalesRequestIntakeResult
{
    public int SalesRequestId { get; init; }
    public string Status { get; init; } = "";
    public int TargetEmployeeId { get; init; }
    public string? TargetEmployeeName { get; init; }
    public bool HasAnyMatch { get; init; }
    public bool AutoAssigned { get; init; }
    public bool IdempotentReplay { get; init; }
    public string? CityValue { get; init; }
    public string? CityName { get; init; }
}

public interface ICrmSalesRequestIntakeService
{
    Task<CrmSalesRequestIntakeResult> IntakeAsync(CrmSalesRequestIntakeRequest request, CancellationToken ct);
}

/// <summary>
/// CRM → branch intake: create Unassigned, evaluate matches, auto-assign round-robin when none.
/// </summary>
public sealed class CrmSalesRequestIntakeService : ICrmSalesRequestIntakeService
{
    private readonly ISalesRequestRepository _repo;
    private readonly ISalesRequestService _requests;
    private readonly ISalesRequestEvaluationService _evaluation;
    private readonly ISalesManagerReadRepository _employees;
    private readonly IIraqClock _clock;
    private readonly IConfiguration _configuration;
    private readonly ICrmSalesBoardStatusNotifier _statusNotifier;

    public CrmSalesRequestIntakeService(
        ISalesRequestRepository repo,
        ISalesRequestService requests,
        ISalesRequestEvaluationService evaluation,
        ISalesManagerReadRepository employees,
        IIraqClock clock,
        IConfiguration configuration,
        ICrmSalesBoardStatusNotifier statusNotifier)
    {
        _repo = repo;
        _requests = requests;
        _evaluation = evaluation;
        _employees = employees;
        _clock = clock;
        _configuration = configuration;
        _statusNotifier = statusNotifier;
    }

    public async Task<CrmSalesRequestIntakeResult> IntakeAsync(CrmSalesRequestIntakeRequest request, CancellationToken ct)
    {
        if (request.CrmOrderUuid == Guid.Empty)
        {
            throw new SalesCompleteException(StatusCodes.Status400BadRequest, "crmOrderUuid مطلوب.");
        }

        var name = request.CustomerName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new SalesCompleteException(StatusCodes.Status400BadRequest, "اسم الزبون مطلوب.");
        }

        await _repo.EnsureSchemaAsync(ct);

        var existing = await _repo.GetByExternalCrmOrderUuidAsync(request.CrmOrderUuid, ct);
        if (existing != null)
        {
            return new CrmSalesRequestIntakeResult
            {
                SalesRequestId = existing.Id,
                Status = existing.Status,
                TargetEmployeeId = existing.TargetEmployeeId,
                TargetEmployeeName = existing.TargetEmployeeName,
                HasAnyMatch = false,
                AutoAssigned = existing.TargetEmployeeId > 0,
                IdempotentReplay = true,
                CityValue = existing.CityValue,
                CityName = existing.CityName
            };
        }

        var actor = BuildGatewayManager();
        SalesPhoneNormalizer.RequireValidIfPresent(request.CustomerPhone);
        var phone = string.IsNullOrWhiteSpace(request.CustomerPhone)
            ? null
            : SalesPhoneNormalizer.ForStorage(request.CustomerPhone);

        var notes = string.IsNullOrWhiteSpace(request.Notes)
            ? (string.IsNullOrWhiteSpace(request.ProductName) ? null : $"منتج CRM: {request.ProductName.Trim()}")
            : request.Notes.Trim();
        if (!string.IsNullOrWhiteSpace(request.ProductName)
            && !string.IsNullOrWhiteSpace(request.Notes)
            && !notes!.Contains(request.ProductName.Trim(), StringComparison.Ordinal))
        {
            notes = $"{notes}\nمنتج CRM: {request.ProductName.Trim()}";
        }

        var row = new SalesRequestDTO
        {
            CreatedByUserId = 0,
            CreatedByName = "قسم التسويق",
            CreatedByUserType = SalesRequestSources.Crm,
            TargetEmployeeId = 0,
            TargetEmployeeName = null,
            CityValue = actor.BranchId,
            CityName = actor.BranchName,
            CustomerSourceType = SalesRequestSources.Crm,
            ExternalCrmOrderUuid = request.CrmOrderUuid,
            CustomerName = name,
            CustomerPhone = phone,
            CustomerProvince = request.CustomerProvince?.Trim(),
            CustomerAddress = request.CustomerAddress?.Trim(),
            Notes = notes,
            Status = SalesRequestStatuses.New,
            FilterStatus = SalesFilterStatuses.ReadyForSale,
            CreatedAtUtc = _clock.UtcNow
        };

        SalesRequestDTO saved;
        try
        {
            saved = await _repo.InsertAsync(row, ct);
        }
        catch (Exception ex) when (IsUniqueViolation(ex))
        {
            var raced = await _repo.GetByExternalCrmOrderUuidAsync(request.CrmOrderUuid, ct)
                        ?? throw new SalesCompleteException(StatusCodes.Status409Conflict, "تعذر إنشاء طلب CRM (تكرار).");
            return new CrmSalesRequestIntakeResult
            {
                SalesRequestId = raced.Id,
                Status = raced.Status,
                TargetEmployeeId = raced.TargetEmployeeId,
                TargetEmployeeName = raced.TargetEmployeeName,
                HasAnyMatch = false,
                AutoAssigned = raced.TargetEmployeeId > 0,
                IdempotentReplay = true,
                CityValue = raced.CityValue,
                CityName = raced.CityName
            };
        }

        await _repo.InsertHistoryAsync(new SalesRequestHistoryDTO
        {
            RequestId = saved.Id,
            Event = SalesRequestEvents.Created,
            Status = saved.Status,
            ActorUserId = 0,
            ActorName = "قسم التسويق",
            ActorType = SalesRequestSources.Crm,
            Note = saved.Notes,
            CreatedAtUtc = _clock.UtcNow
        }, ct);

        var eval = await _evaluation.EvaluateBatchAsync(
            actor,
            new SalesRequestEvaluationBatchRequestDTO { RequestIds = [saved.Id] },
            ct);
        var summary = eval.Items?.FirstOrDefault(i => i.RequestId == saved.Id);
        var programHasMatch = summary?.HasAnyMatch == true;

        // Board duplicates = «بحث داخل طلبات البيع» (active + deleted), same 3 criteria as UI.
        var activePeers = await _repo.ListAsync(null, null, null, null, ct, take: 5000);
        var deletedPeers = await _repo.ListDeletedAsync(ct);
        var boardHasMatch = CrmSalesBoardMatchGate.HasBoardDuplicate(
            saved,
            activePeers.Concat(deletedPeers));

        var hasMatch = CrmSalesBoardMatchGate.ShouldBlockAutoAssign(programHasMatch, boardHasMatch);

        var autoAssigned = false;
        if (!hasMatch)
        {
            var peers = (await _employees.ListActiveSalesEmployeesAsync(ct))
                .Where(e => e.EmployeeId > 0)
                .OrderBy(e => e.EmployeeId)
                .ToList();
            if (peers.Count > 0)
            {
                var nextId = await _repo.NextCrmRoundRobinEmployeeIdAsync(
                    peers.Select(p => p.EmployeeId).ToList(),
                    ct);
                var peer = peers.FirstOrDefault(p => p.EmployeeId == nextId) ?? peers[0];
                saved = await _requests.AssignAsync(
                    actor,
                    saved.Id,
                    new SalesRequestAssignDTO
                    {
                        EmployeeId = peer.EmployeeId,
                        EmployeeName = peer.EmployeeName
                    },
                    ct);
                autoAssigned = true;
            }
        }
        else
        {
            saved = await _repo.GetByIdAsync(saved.Id, ct) ?? saved;
        }

        await _statusNotifier.NotifyAsync(
            new CrmSalesBoardStatusNotification
            {
                CrmOrderUuid = request.CrmOrderUuid,
                SalesRequestId = saved.Id,
                CityValue = saved.CityValue,
                BoardStatus = "Processing",
                RejectReason = null
            },
            ct);

        return new CrmSalesRequestIntakeResult
        {
            SalesRequestId = saved.Id,
            Status = saved.Status,
            TargetEmployeeId = saved.TargetEmployeeId,
            TargetEmployeeName = saved.TargetEmployeeName,
            HasAnyMatch = hasMatch,
            AutoAssigned = autoAssigned,
            IdempotentReplay = false,
            CityValue = saved.CityValue,
            CityName = saved.CityName
        };
    }

    private SalesIdentity BuildGatewayManager()
    {
        var branchId = _configuration["SalesManagement:BranchId"] ?? "branch";
        var branchName = _configuration["SalesManagement:BranchName"] ?? branchId;
        return new SalesIdentity
        {
            EmployeeId = 0,
            EmployeeName = "قسم التسويق",
            BranchId = branchId,
            BranchName = branchName,
            Role = SalesRoles.SalesManager,
            UserType = SalesRoles.UserTypeSalesManager,
            IsGateway = true
        };
    }

    private static bool IsUniqueViolation(Exception ex)
    {
        for (var cur = ex; cur != null; cur = cur.InnerException)
        {
            var msg = cur.Message ?? "";
            if (msg.Contains("UX_SalesRequests_ExternalCrmOrderUuid", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("unique", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("2627", StringComparison.Ordinal)
                || msg.Contains("2601", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
