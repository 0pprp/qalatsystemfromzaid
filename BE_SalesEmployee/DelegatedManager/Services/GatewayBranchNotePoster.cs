using System.Text.Json;
using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Sales.Services;
using BE_SalesEmployee.Services;

namespace BE_SalesEmployee.DelegatedManager.Services;

/// <summary>
/// Records approval intent and posts a durable shared customer note to the branch via gateway fan-in.
/// </summary>
public sealed class GatewayBranchNotePoster : IBranchNotePoster
{
    private readonly ISalesManagerBranchAggregator _aggregator;
    private readonly IntentRecordingBranchNotePoster _intent;

    public GatewayBranchNotePoster(
        ISalesManagerBranchAggregator aggregator,
        IntentRecordingBranchNotePoster intent)
    {
        _aggregator = aggregator;
        _intent = intent;
    }

    public async Task<bool> PostApprovalNoteAsync(SalesExceptionRequest request, CancellationToken ct = default)
    {
        await _intent.PostApprovalNoteAsync(request, ct);

        if (string.IsNullOrWhiteSpace(request.CityValue) || request.CustomerId is null or <= 0)
        {
            return false;
        }

        var note = IntentRecordingBranchNotePosterNote.Build(request);
        var user = new GatewayUser
        {
            UserID = "0",
            UserName = request.DecisionMakerUserName ?? "delegated-manager",
            UserType = SalesRoles.UserTypeDelegatedManager,
            IsCentral = true,
            IsDelegatedManager = true,
            CityValue = request.CityValue,
            CityName = request.CityName ?? ""
        };

        var json = JsonSerializer.Serialize(new
        {
            customerId = request.CustomerId.Value,
            note = note
        });

        try
        {
            var (status, _) = await _aggregator.PostAsync(
                user,
                request.CityValue,
                "sales-manager/customers/notes",
                json,
                ct);
            return status is >= 200 and < 300;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>Shared note text builder (kept public for tests).</summary>
public static class IntentRecordingBranchNotePosterNote
{
    public static string Build(SalesExceptionRequest request)
    {
        var approver = string.IsNullOrWhiteSpace(request.DecisionMakerDisplayName)
            ? "المدير المفوض"
            : request.DecisionMakerDisplayName!;
        var note =
            $"تمت الموافقة على استثناء لهذا الزبون من قبل المدير المفوض ({approver}). المرجع: {request.Id}";
        if (!string.IsNullOrWhiteSpace(request.DecisionNote))
        {
            note = $"{note} — ملاحظة: {request.DecisionNote}";
        }

        return note;
    }
}
