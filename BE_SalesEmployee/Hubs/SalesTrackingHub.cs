using BE_SalesEmployee.Sales.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BE_SalesEmployee.Hubs
{
    [Authorize(Policy = SalesPolicies.SalesManager)]
    public sealed class SalesTrackingHub : Hub
    {
        public const string ManagersGroup = "sales-managers";
        public const string LocationUpdated = "locationUpdated";

        public override async Task OnConnectedAsync()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, ManagersGroup);
            await base.OnConnectedAsync();
        }
    }
}
