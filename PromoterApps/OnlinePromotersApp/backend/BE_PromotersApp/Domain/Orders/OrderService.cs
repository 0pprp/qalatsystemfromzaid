using System.Text;
using Dapper;
using Microsoft.Data.SqlClient;
using BE_PromotersApp.Auth;
using BE_PromotersApp.Domain.Admin;
using BE_PromotersApp.Domain.Notifications;
using BE_PromotersApp.Domain.Products;
using BE_PromotersApp.Domain.Users;
using BE_PromotersApp.Domain.Wallet;
using BE_PromotersApp.Infrastructure;
using BE_PromotersApp.Integrations.Gateway;

namespace BE_PromotersApp.Domain.Orders;

public sealed record OverviewDto(
    long Balance, long EarnedThisMonth, int OrdersThisMonth, int SoldThisMonth, int OpenOrders, int TeamCount, int UnreadNotifications,
    IReadOnlyList<OrderSummaryDto> RecentOrders);

/// <summary>
/// A promoter's sale order goes straight to the company system («طلبات البيع» → غير المسندة, source «المروج: أونلاين - …»).
/// The sync worker follows it; when the company marks it sold, the commission is added to the wallets.
/// </summary>
public sealed class OrderService
{
    public const int MaxQuantity = 10;
    /// <summary>Prefix of the request's source in the company system, so these orders are told apart from the field agents'.</summary>
    public const string SourcePrefix = "أونلاين - ";

    private readonly Db _db;
    private readonly BranchCatalog _branches;
    private readonly IGatewayClient _gateway;
    private readonly NotificationService _notifications;
    private readonly ILogger<OrderService> _logger;

    public OrderService(Db db, BranchCatalog branches, IGatewayClient gateway, NotificationService notifications, ILogger<OrderService> logger)
    {
        _db = db;
        _branches = branches;
        _gateway = gateway;
        _notifications = notifications;
        _logger = logger;
    }

    // ===================== App =====================

    public async Task<OrderDetailDto> CreateAsync(UserRow user, CreateOrderInput input, string? idempotencyKey, CancellationToken ct)
    {
        idempotencyKey = Text.NullIfBlank(idempotencyKey) is { } k ? k[..Math.Min(64, k.Length)] : null;
        await using var c = await _db.OpenAsync(ct);
        if (idempotencyKey is not null)
        {
            var existing = await c.ExecuteScalarAsync<long?>(
                "SELECT Id FROM promoters.Orders WHERE UserId = @Id AND IdempotencyKey = @idempotencyKey", new { user.Id, idempotencyKey });
            if (existing is { } id) return await GetForUserAsync(user.Id, id, ct);
        }

        var errors = new Dictionary<string, string>();
        var product = input.ProductId is null ? null
            : await c.QuerySingleOrDefaultAsync<ProductRow>("SELECT Id, Name, Price, Commission, IsActive, SortOrder, CreatedAt, UpdatedAt FROM promoters.Products WHERE Id = @ProductId AND IsActive = 1 AND IsDeleted = 0", new { input.ProductId });
        if (product is null) errors["productId"] = "اختر المنتج";
        var quantity = input.Quantity ?? 1;
        if (quantity is < 1 or > MaxQuantity) errors["quantity"] = $"الكمية بين 1 و {MaxQuantity}";
        var (customerName, nameError) = CustomerName.Check(input.CustomerName);
        if (nameError is not null) errors["customerName"] = nameError;
        var phone = IraqiPhone.Normalize(input.CustomerPhone);
        if (phone is null) errors["customerPhone"] = "رقم هاتف عراقي غير صحيح (07XXXXXXXXX)";
        var cityName = input.CityValue is { } cv ? await _branches.NameAsync(cv, ct) : null;
        if (cityName is null) errors["cityValue"] = "اختر المحافظة";
        var address = Text.CollapseSpaces(input.Address);
        if (address.Length is < 3 or > 400) errors["address"] = "اكتب عنوان الزبون";
        var notes = Text.NullIfBlank(input.Notes);
        if ((notes?.Length ?? 0) > 500) errors["notes"] = "الملاحظات طويلة جداً (500 حرف كحد أقصى)";
        if (errors.Count > 0) throw ApiException.Validation(errors);

        // The same customer and product cannot be sent twice while the first order is still open.
        var duplicate = await c.ExecuteScalarAsync<string?>(
            "SELECT TOP 1 OrderNumber FROM promoters.Orders WHERE CustomerPhone = @phone AND ProductId = @Id AND Status IN @open",
            new { phone, product!.Id, open = OrderStatus.Open });
        if (duplicate is not null)
            throw ApiException.Conflict("duplicate_open_order", $"يوجد طلب مفتوح لنفس الزبون ونفس المنتج (رقم {duplicate})", new { orderNumber = duplicate });

        var cut = user.EffectiveCut;
        var split = CommissionSplit.For(product.Commission, quantity, cut);
        var parentId = cut > 0 ? user.ParentUserId : null;

        long orderId;
        try
        {
            await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
            orderId = await c.ExecuteScalarAsync<long>("""
                INSERT promoters.Orders(OrderUuid, UserId, IdempotencyKey, ProductId, ProductName, UnitPrice, Quantity, CommissionTotal, SellerCommission,
                    ParentUserId, ParentCutPercent, ParentCommission, CityValue, CityName, CustomerName, CustomerPhone, Address, Notes, Status)
                OUTPUT inserted.Id
                VALUES (NEWID(), @UserId, @idempotencyKey, @ProductId, @ProductName, @Price, @quantity, @Total, @Seller,
                    @parentId, @cut, @Parent, @CityValue, @cityName, @customerName, @phone, @address, @notes, 'New')
                """, new
            {
                UserId = user.Id, idempotencyKey, ProductId = product.Id, ProductName = product.Name, product.Price, quantity,
                split.Total, split.Seller, parentId, cut = parentId is null ? 0 : cut, split.Parent, input.CityValue, cityName, customerName, phone, address, notes,
            }, tx);
            await c.ExecuteAsync("UPDATE promoters.Orders SET OrderNumber = @number WHERE Id = @orderId",
                new { orderId, number = $"PO-{DateTime.UtcNow:yyMM}-{orderId:D5}" }, tx);
            await AddEventAsync(c, tx, orderId, OrderStatus.New, null, null);
            await tx.CommitAsync(ct);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627 && ex.Message.Contains("UX_Orders_Idempotency"))
        {
            var existingId = await c.ExecuteScalarAsync<long>(
                "SELECT Id FROM promoters.Orders WHERE UserId = @Id AND IdempotencyKey = @idempotencyKey", new { user.Id, idempotencyKey });
            return await GetForUserAsync(user.Id, existingId, ct);
        }

        await TryForwardAsync(orderId, ct);
        return await GetForUserAsync(user.Id, orderId, ct);
    }

    /// <param name="status">One status or a comma-separated list.</param>
    public async Task<PagedResult<OrderSummaryDto>> ListForUserAsync(long userId, string? status, Paging paging, CancellationToken ct)
    {
        var statuses = (status ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var any = statuses.Count > 0 ? 1 : 0;
        await using var c = await _db.OpenAsync(ct);
        using var multi = await c.QueryMultipleAsync("""
            SELECT COUNT(*) FROM promoters.Orders WHERE UserId = @userId AND (@any = 0 OR Status IN @statuses);
            SELECT * FROM promoters.Orders WHERE UserId = @userId AND (@any = 0 OR Status IN @statuses)
            ORDER BY Id DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """, new { userId, any, statuses = any == 1 ? statuses : [""], paging.Offset, paging.PageSize });
        var total = await multi.ReadSingleAsync<int>();
        var rows = (await multi.ReadAsync<OrderRow>()).Select(ToSummary).ToList();
        return new PagedResult<OrderSummaryDto>(rows, total, paging.Page, paging.PageSize);
    }

    public async Task<Dictionary<string, int>> CountsForUserAsync(long userId, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var counts = (await c.QueryAsync<(string Status, int Count)>(
                "SELECT Status, COUNT(*) FROM promoters.Orders WHERE UserId = @userId GROUP BY Status", new { userId }))
            .ToDictionary(x => x.Status, x => x.Count);
        var result = OrderStatus.All.ToDictionary(s => s, s => counts.GetValueOrDefault(s));
        result["all"] = counts.Values.Sum();
        return result;
    }

    public async Task<OrderDetailDto> GetForUserAsync(long userId, long orderId, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var o = await c.QuerySingleOrDefaultAsync<OrderRow>("SELECT * FROM promoters.Orders WHERE Id = @orderId AND UserId = @userId", new { orderId, userId })
                ?? throw ApiException.NotFound("الطلب غير موجود");
        var timeline = await TimelineAsync(c, orderId);
        return new OrderDetailDto(o.Id, o.OrderNumber, o.Status, OrderStatus.Label(o.Status), o.ProductName, o.Quantity, o.UnitPrice,
            o.UnitPrice * o.Quantity, o.SellerCommission, o.CustomerName, o.CustomerPhone, o.CityValue, o.CityName, o.Address, o.Notes,
            o.Status is OrderStatus.NotSold or OrderStatus.Cancelled ? o.PublicReason : null, timeline, o.CreatedAt, o.UpdatedAt, o.SoldAt,
            o.Status == OrderStatus.New);
    }

    /// <summary>Only an order the company system has not received yet can be cancelled from the app.</summary>
    public async Task<OrderDetailDto> CancelAsync(long userId, long orderId, CancellationToken ct)
    {
        await _db.InTransactionAsync(async (c, tx) =>
        {
            var n = await c.ExecuteAsync("""
                UPDATE promoters.Orders SET Status = 'Cancelled', PublicReason = N'ألغاه المروّج', UpdatedAt = SYSUTCDATETIME()
                WHERE Id = @orderId AND UserId = @userId AND Status = 'New'
                """, new { orderId, userId }, tx);
            if (n == 0) throw ApiException.Conflict("cannot_cancel", "لا يمكن إلغاء الطلب بعد وصوله إلى قسم المبيعات");
            await AddEventAsync(c, tx, orderId, OrderStatus.Cancelled, "ألغاه المروّج", null);
            return 0;
        }, ct: ct);
        return await GetForUserAsync(userId, orderId, ct);
    }

    /// <summary>The app's home page («الرئيسية»).</summary>
    public async Task<OverviewDto> OverviewAsync(long userId, CancellationToken ct)
    {
        var monthStart = IraqTime.MonthStartUtc(DateTime.UtcNow);
        await using var c = await _db.OpenAsync(ct);
        var n = await c.QuerySingleAsync<(long Balance, long Earned, int Month, int Sold, int Open, int Team, int Unread)>("""
            SELECT
              ISNULL((SELECT SUM(Amount) FROM promoters.WalletEntries WHERE UserId = @userId), 0),
              ISNULL((SELECT SUM(Amount) FROM promoters.WalletEntries WHERE UserId = @userId AND Type IN @earn AND CreatedAt >= @monthStart), 0),
              (SELECT COUNT(*) FROM promoters.Orders WHERE UserId = @userId AND CreatedAt >= @monthStart),
              (SELECT COUNT(*) FROM promoters.Orders WHERE UserId = @userId AND Status = 'Sold' AND SoldAt >= @monthStart),
              (SELECT COUNT(*) FROM promoters.Orders WHERE UserId = @userId AND Status IN @open),
              (SELECT COUNT(*) FROM promoters.Users WHERE ParentUserId = @userId),
              (SELECT COUNT(*) FROM promoters.Notifications WHERE UserId = @userId AND ReadAt IS NULL)
            """, new { userId, monthStart, earn = EntryTypes.Earnings, open = OrderStatus.Open });
        var recent = (await c.QueryAsync<OrderRow>("SELECT TOP 5 * FROM promoters.Orders WHERE UserId = @userId ORDER BY Id DESC", new { userId }))
            .Select(ToSummary).ToList();
        return new OverviewDto(n.Balance, n.Earned, n.Month, n.Sold, n.Open, n.Team, n.Unread, recent);
    }

    // ===================== Company system =====================

    public static readonly string SystemActor = "المنظومة";

    /// <summary>Sends the order to «طلبات البيع». When the company system is down the order stays New and the worker retries.</summary>
    public async Task<bool> TryForwardAsync(long orderId, CancellationToken ct)
    {
        try
        {
            OrderRow order;
            string username;
            await using (var c = await _db.OpenAsync(ct))
            {
                order = await c.QuerySingleAsync<OrderRow>("SELECT * FROM promoters.Orders WHERE Id = @orderId", new { orderId });
                if (order.Status != OrderStatus.New) return false;
                username = await c.ExecuteScalarAsync<string>("SELECT Username FROM promoters.Users WHERE Id = @UserId", new { order.UserId }) ?? "";
            }

            var created = await _gateway.CreateSalesRequestAsync(new GatewaySalesRequestCreate(
                order.OrderUuid, order.CityValue, order.CustomerName, order.CustomerPhone, order.CityName, order.Address,
                BuildSalesNotes(order), Text.Truncate(order.Quantity > 1 ? $"{order.ProductName} ×{order.Quantity}" : order.ProductName, 200),
                Text.Truncate(SourcePrefix + username, 100)), ct);

            var moved = await _db.InTransactionAsync(async (c, tx) =>
            {
                var n = await c.ExecuteAsync("""
                    UPDATE promoters.Orders SET Status = 'Transferred', SalesRequestId = @SalesRequestId, BranchStatus = @Status, UpdatedAt = SYSUTCDATETIME()
                    WHERE Id = @orderId AND Status = 'New'
                    """, new { created.SalesRequestId, created.Status, orderId }, tx);
                if (n == 1) await AddEventAsync(c, tx, orderId, OrderStatus.Transferred, null, SystemActor);
                return n == 1;
            }, ct: ct);
            return moved;
        }
        catch (GatewayUnavailableException ex)
        {
            _logger.LogWarning("Order {OrderId} not forwarded yet: {Detail}", orderId, ex.Detail);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Order {OrderId} not forwarded yet", orderId);
        }
        return false;
    }

    public async Task<int> ForwardPendingAsync(CancellationToken ct)
    {
        List<long> ids;
        await using (var c = await _db.OpenAsync(ct))
            ids = (await c.QueryAsync<long>(
                "SELECT TOP 50 Id FROM promoters.Orders WHERE Status = 'New' AND CreatedAt < DATEADD(SECOND, -30, SYSUTCDATETIME()) ORDER BY Id")).ToList();
        var sent = 0;
        foreach (var id in ids)
            if (await TryForwardAsync(id, ct)) sent++;
        return sent;
    }

    /// <summary>Pulls the company status of open orders (and of recent sales, to catch cancellations).</summary>
    public async Task<int> SyncAsync(IReadOnlyCollection<long>? onlyOrderIds, CancellationToken ct)
    {
        List<OrderRow> orders;
        await using (var c = await _db.OpenAsync(ct))
        {
            orders = (await c.QueryAsync<OrderRow>(onlyOrderIds is { Count: > 0 }
                ? "SELECT * FROM promoters.Orders WHERE Id IN @onlyOrderIds AND SalesRequestId IS NOT NULL"
                : """
                  SELECT TOP 1000 * FROM promoters.Orders
                  WHERE SalesRequestId IS NOT NULL
                    AND (Status IN ('Transferred','FieldInspection') OR (Status = 'Sold' AND SoldAt > DATEADD(DAY, -60, SYSUTCDATETIME())))
                  ORDER BY ISNULL(LastSyncAt, '2000-01-01')
                  """, new { onlyOrderIds })).ToList();
        }

        var changed = 0;
        foreach (var batch in orders.Chunk(200))
        {
            var statuses = await _gateway.GetStatusesAsync(batch.Select(o => new GatewayStatusQuery(o.CityValue, o.OrderUuid)).ToList(), ct);
            var byUuid = statuses.ToDictionary(s => s.OrderUuid);
            foreach (var order in batch)
            {
                if (!byUuid.TryGetValue(order.OrderUuid, out var s)) continue;
                try
                {
                    if (await ApplyBranchStatusAsync(order, s, ct)) changed++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Failed to apply branch status for order {OrderId}", order.Id);
                }
            }
        }
        return changed;
    }

    private async Task<bool> ApplyBranchStatusAsync(OrderRow order, GatewayRequestStatus s, CancellationToken ct)
    {
        var target = OrderStatus.FromBranch(s.Found, s.Status, s.IsDeleted);
        await using (var c = await _db.OpenAsync(ct))
            await c.ExecuteAsync("""
                UPDATE promoters.Orders SET BranchStatus = @Status, BranchRejectReason = @RejectionReason, LastSyncAt = SYSUTCDATETIME(),
                       SalesRequestId = COALESCE(@SalesRequestId, SalesRequestId), SaleId = COALESCE(@ConvertedToSaleId, SaleId) WHERE Id = @Id
                """, new { s.Status, s.RejectionReason, s.SalesRequestId, s.ConvertedToSaleId, order.Id });
        if (target is null || target == order.Status) return false;
        var reason = string.IsNullOrWhiteSpace(s.RejectionReason) ? null : Text.Truncate(Text.CollapseSpaces(s.RejectionReason), 400);

        // A sale the company cancels later: the commission is taken back.
        if (order.Status == OrderStatus.Sold)
        {
            if (target != OrderStatus.NotSold) return false;
            var cancelReason = reason is null ? "تم إلغاء عملية البيع" : "تم إلغاء عملية البيع: " + reason;
            var cancelled = await _db.InTransactionAsync(async (c, tx) =>
            {
                var n = await c.ExecuteAsync(
                    "UPDATE promoters.Orders SET Status = 'NotSold', PublicReason = @reason, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id AND Status = 'Sold'",
                    new { reason = Text.Truncate(cancelReason, 400), order.Id }, tx);
                if (n == 0) return false;
                await AddEventAsync(c, tx, order.Id, OrderStatus.NotSold, Text.Truncate(cancelReason, 400), SystemActor);
                await WalletService.ReverseSaleAsync(c, tx, order.Id, $"إلغاء البيع للطلب {order.OrderNumber}");
                return true;
            }, ct: ct);
            if (!cancelled) return false;
            await _notifications.NotifyAsync(order.UserId, NotificationTypes.Order, "أُلغيت عملية البيع",
                $"الطلب {order.OrderNumber} ({order.ProductName}): {cancelReason}. خُصمت العمولة من رصيدك", OrderData(order));
            if (order.ParentUserId is { } p && order.ParentCommission > 0)
                await _notifications.NotifyAsync(p, NotificationTypes.Wallet, "أُلغيت عمولة من فريقك",
                    $"أُلغي بيع الطلب {order.OrderNumber} فخُصمت عمولته ({order.ParentCommission:N0} د.ع)");
            return true;
        }

        if (target == OrderStatus.Sold)
        {
            var sold = await _db.InTransactionAsync(async (c, tx) =>
            {
                var n = await c.ExecuteAsync(
                    "UPDATE promoters.Orders SET Status = 'Sold', SoldAt = SYSUTCDATETIME(), UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id AND Status IN ('Transferred','FieldInspection')",
                    new { order.Id }, tx);
                if (n == 0) return false;
                await AddEventAsync(c, tx, order.Id, OrderStatus.Sold, null, SystemActor);
                await WalletService.CreditSaleAsync(c, tx, order.Id, order.UserId, order.SellerCommission, order.ParentUserId, order.ParentCommission);
                return true;
            }, ct: ct);
            if (!sold) return false;
            await _notifications.NotifyAsync(order.UserId, NotificationTypes.Order, "تم البيع 🎉",
                $"تحوّل طلبك {order.OrderNumber} ({order.ProductName}) إلى مبيعة. أضيفت {order.SellerCommission:N0} د.ع إلى رصيدك", OrderData(order));
            if (order.ParentUserId is { } parent && order.ParentCommission > 0)
            {
                string seller;
                await using (var c = await _db.OpenAsync(ct))
                    seller = await c.ExecuteScalarAsync<string>("SELECT Username FROM promoters.Users WHERE Id = @UserId", new { order.UserId }) ?? "";
                await _notifications.NotifyAsync(parent, NotificationTypes.Wallet, "عمولة من فريقك 💰",
                    $"باع {seller} ({order.ProductName}). أضيفت {order.ParentCommission:N0} د.ع إلى رصيدك");
            }
            return true;
        }

        var publicReason = target == OrderStatus.NotSold ? reason ?? OrderStatus.DefaultNotSoldReason : null;
        var moved = await _db.InTransactionAsync(async (c, tx) =>
        {
            var n = await c.ExecuteAsync("""
                UPDATE promoters.Orders SET Status = @target, PublicReason = COALESCE(@publicReason, PublicReason), UpdatedAt = SYSUTCDATETIME()
                WHERE Id = @Id AND Status IN ('Transferred','FieldInspection')
                """, new { target, publicReason, order.Id }, tx);
            if (n == 1) await AddEventAsync(c, tx, order.Id, target, target == OrderStatus.NotSold ? publicReason : null, SystemActor);
            return n == 1;
        }, ct: ct);
        if (!moved) return false;
        if (target == OrderStatus.NotSold)
            await _notifications.NotifyAsync(order.UserId, NotificationTypes.Order, "لم يتم إتمام البيع",
                $"الطلب {order.OrderNumber} ({order.ProductName}): {publicReason}", OrderData(order));
        return true;
    }

    // ===================== Dashboard =====================

    private const string AdminSelect = """
        o.Id, o.OrderNumber, o.Status, CAST('' AS NVARCHAR(40)) AS StatusLabel, o.UserId, u.Username, pu.Username AS ParentUsername,
        o.ProductName, o.Quantity, o.UnitPrice, o.CommissionTotal, o.SellerCommission, o.ParentCommission, o.ParentCutPercent,
        o.CustomerName, o.CustomerPhone, o.CityName, o.Address, o.Notes, o.PublicReason, o.SalesRequestId, o.BranchStatus, o.LastSyncAt, o.CreatedAt, o.SoldAt
        FROM promoters.Orders o JOIN promoters.Users u ON u.Id = o.UserId LEFT JOIN promoters.Users pu ON pu.Id = o.ParentUserId
        """;

    /// <param name="from">UTC start (inclusive).</param>
    /// <param name="to">UTC end (exclusive).</param>
    public async Task<PagedResult<AdminOrderRow>> AdminListAsync(string? status, long? userId, string? q, DateTime? from, DateTime? to, Paging paging, CancellationToken ct)
    {
        var where = new StringBuilder("1 = 1");
        var args = new DynamicParameters(new { paging.Offset, paging.PageSize });
        if (!string.IsNullOrWhiteSpace(status))
        {
            where.Append(" AND o.Status IN @statuses");
            args.Add("statuses", status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
        if (userId is not null) { where.Append(" AND (o.UserId = @userId OR o.ParentUserId = @userId)"); args.Add("userId", userId); }
        if (from is not null) { where.Append(" AND o.CreatedAt >= @from"); args.Add("from", from); }
        if (to is not null) { where.Append(" AND o.CreatedAt < @to"); args.Add("to", to); }
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            where.Append(" AND (o.OrderNumber = @term OR o.CustomerPhone = @phone OR o.CustomerName LIKE '%' + @term + '%' OR u.Username LIKE '%' + @term + '%' OR o.ProductName LIKE '%' + @term + '%')");
            args.Add("term", term);
            args.Add("phone", IraqiPhone.Normalize(term) ?? term);
        }
        await using var c = await _db.OpenAsync(ct);
        using var multi = await c.QueryMultipleAsync($"""
            SELECT COUNT(*) FROM promoters.Orders o JOIN promoters.Users u ON u.Id = o.UserId WHERE {where};
            SELECT {AdminSelect} WHERE {where} ORDER BY o.Id DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """, args);
        var total = await multi.ReadSingleAsync<int>();
        var rows = (await multi.ReadAsync<AdminOrderRow>()).Select(r => r with { StatusLabel = OrderStatus.Label(r.Status) }).ToList();
        return new PagedResult<AdminOrderRow>(rows, total, paging.Page, paging.PageSize);
    }

    public async Task<Dictionary<string, int>> AdminCountsAsync(CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var counts = (await c.QueryAsync<(string Status, int Count)>("SELECT Status, COUNT(*) FROM promoters.Orders GROUP BY Status"))
            .ToDictionary(x => x.Status, x => x.Count);
        var result = OrderStatus.All.ToDictionary(s => s, s => counts.GetValueOrDefault(s));
        result["all"] = counts.Values.Sum();
        return result;
    }

    public async Task<AdminOrderDetailDto> AdminGetAsync(long orderId, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var row = await c.QuerySingleOrDefaultAsync<AdminOrderRow>($"SELECT {AdminSelect} WHERE o.Id = @orderId", new { orderId })
                  ?? throw ApiException.NotFound("الطلب غير موجود");
        return new AdminOrderDetailDto(row with { StatusLabel = OrderStatus.Label(row.Status) }, await TimelineAsync(c, orderId));
    }

    // ===================== Helpers =====================

    private static Task AddEventAsync(SqlConnection c, SqlTransaction tx, long orderId, string status, string? note, string? actor) =>
        c.ExecuteAsync("INSERT promoters.OrderEvents(OrderId, Status, Note, Actor) VALUES (@orderId, @status, @note, @actor)",
            new { orderId, status, note, actor }, tx);

    private static async Task<List<TimelineDto>> TimelineAsync(SqlConnection c, long orderId) =>
        (await c.QueryAsync<(string Status, DateTime CreatedAt, string? Note)>(
            "SELECT Status, CreatedAt, Note FROM promoters.OrderEvents WHERE OrderId = @orderId ORDER BY Id", new { orderId }))
        .Select(e => new TimelineDto(e.Status, OrderStatus.Label(e.Status), e.CreatedAt, e.Note)).ToList();

    private static OrderSummaryDto ToSummary(OrderRow o) => new(o.Id, o.OrderNumber, o.Status, OrderStatus.Label(o.Status), o.ProductName,
        o.Quantity, o.CustomerName, o.CityName, o.SellerCommission, o.CreatedAt, o.UpdatedAt, o.SoldAt);

    private static Dictionary<string, string> OrderData(OrderRow o) => new() { ["orderId"] = o.Id.ToString() };

    /// <summary>Notes of the company sales request: the price the customer was told and the promoter's own notes.</summary>
    public static string BuildSalesNotes(OrderRow o)
    {
        var lines = new List<string> { $"السعر: {o.UnitPrice * o.Quantity:N0} د.ع" };
        if (!string.IsNullOrWhiteSpace(o.Notes)) lines.Add(o.Notes.Trim());
        return Text.Truncate(string.Join('\n', lines), 750);
    }
}
