using BE_Company.Sales.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BE_Company.Sales.Services
{
    public interface ISalesLocationBroadcaster
    {
        Task PublishAsync(SalesLiveLocationDTO update, CancellationToken ct);
    }

    public sealed class NullSalesLocationBroadcaster : ISalesLocationBroadcaster
    {
        public static readonly NullSalesLocationBroadcaster Instance = new();
        public Task PublishAsync(SalesLiveLocationDTO update, CancellationToken ct) => Task.CompletedTask;
    }

    public sealed class SignalRSalesLocationBroadcaster : ISalesLocationBroadcaster
    {
        public const string ManagersGroup = "sales-managers";
        public const string LocationUpdated = "locationUpdated";

        private readonly IHubContext<SalesTrackingHub> _hub;

        public SignalRSalesLocationBroadcaster(IHubContext<SalesTrackingHub> hub)
        {
            _hub = hub;
        }

        public Task PublishAsync(SalesLiveLocationDTO update, CancellationToken ct) =>
            _hub.Clients.Group(ManagersGroup).SendAsync(LocationUpdated, update, ct);
    }
}

namespace BE_Company.Sales.Services
{
    [Authorize(Policy = Authorization.SalesPolicies.SalesManager)]
    public sealed class SalesTrackingHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, SignalRSalesLocationBroadcaster.ManagersGroup);
            await base.OnConnectedAsync();
        }
    }
}
