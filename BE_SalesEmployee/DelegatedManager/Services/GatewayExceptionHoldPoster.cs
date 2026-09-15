using System.Text.Json;
using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Sales.Services;
using BE_SalesEmployee.Services;

namespace BE_SalesEmployee.DelegatedManager.Services;

/// <summary>
/// Mirrors <see cref="GatewayBranchNotePoster"/>: posts hold status to the branch
/// <c>sales-manager/sales-requests/{id}/exception-hold</c> endpoint with the gateway key.
/// </summary>
public sealed class GatewayExceptionHoldPoster : IBranchExceptionHoldPoster
{
    private readonly ISalesManagerBranchAggregator _aggregator;

    public GatewayExceptionHoldPoster(ISalesManagerBranchAggregator aggregator)
    {
        _aggregator = aggregator;
    }

    public async Task<bool> SyncHoldAsync(
        SalesExceptionRequest request,
        string? holdStatus,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CityValue) || request.SalesRequestId is null or <= 0)
        {
            return false;
        }

        var user = new GatewayUser
        {
            UserID = "0",
            UserName = request.DecisionMakerUserName
                       ?? request.RequestingManagerUserName
                       ?? "delegated-manager",
            UserType = SalesRoles.UserTypeDelegatedManager,
            IsCentral = true,
            IsDelegatedManager = true,
            CityValue = request.CityValue,
            CityName = request.CityName ?? ""
        };

        var json = JsonSerializer.Serialize(new
        {
            status = holdStatus,
            exceptionId = request.Id
        });

        try
        {
            var path = $"sales-manager/sales-requests/{request.SalesRequestId.Value}/exception-hold";
            var (status, _) = await _aggregator.PostAsync(user, request.CityValue, path, json, ct);
            return status is >= 200 and < 300;
        }
        catch
        {
            return false;
        }
    }
}
