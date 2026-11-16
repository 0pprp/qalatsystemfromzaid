using System.Globalization;
using System.Text;
using Dapper;
using Microsoft.Data.SqlClient;
using BE_CustomerApp.Auth;
using BE_CustomerApp.Domain.Admin;
using BE_CustomerApp.Domain.Notifications;
using BE_CustomerApp.Domain.Users;
using BE_CustomerApp.Infrastructure;
using BE_CustomerApp.Integrations.Gateway;

namespace BE_CustomerApp.Domain.Orders;

public sealed class OrderService
{
    private const int MaxItems = 10;
    private const int MaxQuantity = 50;
    private const int MaxItemName = 100;

    /// <summary>Shop photos are uploaded first (UploadShopPhotoAsync) and the order refers to them by this path.</summary>
    public const string ShopPhotoPrefix = "media/shops/";

    private readonly Db _db;
    private readonly BranchCatalog _branches;
    private readonly IGatewayClient _gateway;
    private readonly NotificationService _notifications;
    private readonly AuditService _audit;
    private readonly PublicUrls _urls;
    private readonly ILogger<OrderService> _logger;

    public OrderService(Db db, BranchCatalog branches, IGatewayClient gateway,
        NotificationService notifications, AuditService audit, PublicUrls urls, ILogger<OrderService> logger)
    {
        _db = db;
        _branches = branches;
        _gateway = gateway;
        _notifications = notifications;
        _audit = audit;
        _urls = urls;
        _logger = logger;
    }

    // ===================== Customer =====================

    public async Task<OrderDetailDto> CreateAsync(UserRow user, CreateOrderInput input, string? idempotencyKey, CancellationToken ct)
    {
        idempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim()[..Math.Min(64, idempotencyKey.Trim().Length)];
        if (idempotencyKey is not null)
        {
            await using var c0 = await _db.OpenAsync(ct);
            var existing = await c0.ExecuteScalarAsync<long?>(
                "SELECT Id FROM agents.Orders WHERE SubmittedByUserId = @Id AND IdempotencyKey = @idempotencyKey", new { user.Id, idempotencyKey });
            if (existing is { } existingId) return await GetForCustomerAsync(user.Id, existingId, ct);
        }

        var errors = new Dictionary<string, string>();
        var branchName = await _branches.NameAsync(input.CityValue, ct);
        if (branchName is null) errors["cityValue"] = "اختر الفرع";

        // Sales are daily installments only (cash is still accepted from older app versions).
        var purchaseType = input.PurchaseType == PurchaseTypes.Cash ? PurchaseTypes.Cash : PurchaseTypes.DailyInstallments;

        var customer = input.Customer ?? new OrderCustomerInput(null, null, null, null, null, null);
        PersonName name;
        if (customer.FullName is not null)
        {
            var parsed = PersonName.FromFullName(customer.FullName);
            name = parsed ?? new PersonName("", "", "", null);
            if (parsed is null) errors["customer.fullName"] = "اكتب اسم الزبون الثلاثي على الأقل";
            else if (UserService.ValidateName(parsed).Count > 0) errors["customer.fullName"] = "اكتب الاسم بالحروف العربية";
        }
        else
        {
            name = new PersonName(customer.FirstName ?? "", customer.FatherName ?? "", customer.GrandfatherName ?? "", customer.Surname);
            foreach (var (k, v) in UserService.ValidateName(name, "customer.")) errors[k] = v;
        }

        var phone = IraqiPhone.Normalize(customer.Phone);
        if (phone is null) errors["customer.phone"] = "رقم هاتف عراقي غير صحيح (07XXXXXXXXX)";
        string? secondary = null;
        if (!string.IsNullOrWhiteSpace(customer.SecondaryPhone))
        {
            secondary = IraqiPhone.Normalize(customer.SecondaryPhone);
            if (secondary is null) errors["customer.secondaryPhone"] = "رقم هاتف عراقي غير صحيح";
        }

        var address = input.Address ?? new OrderAddressInput(null, null, null, null, null);
        var area = address.Area?.Trim() ?? "";
        var details = address.Details?.Trim() ?? "";
        // One address field is enough (area is optional).
        if (area.Length > 150) errors["address.area"] = "النص طويل جداً";
        if (details.Length is < 3 or > 400) errors["address.details"] = "أدخل عنوان الزبون";
        if ((address.NearestLandmark?.Length ?? 0) > 200) errors["address.nearestLandmark"] = "النص طويل جداً";
        // The shop's GPS location and photo are optional.
        if ((address.Latitude is null) != (address.Longitude is null))
            errors["address.latitude"] = "الموقع غير مكتمل";
        else if (address.Latitude is < 29 or > 38 || address.Longitude is < 38 or > 49)
            errors["address.latitude"] = "الموقع خارج العراق";

        string? shopPhoto = null;
        if (!string.IsNullOrWhiteSpace(input.ShopPhotoUrl))
        {
            shopPhoto = _urls.ToStored(input.ShopPhotoUrl);
            if (shopPhoto is null || !shopPhoto.StartsWith(ShopPhotoPrefix, StringComparison.Ordinal) || shopPhoto.Contains(".."))
                errors["shopPhotoUrl"] = "صورة المحل غير صالحة، التقطها من جديد";
        }

        var shop = string.IsNullOrWhiteSpace(input.Shop?.Name) && string.IsNullOrWhiteSpace(input.Shop?.BusinessType) ? null : input.Shop;
        if ((input.Notes?.Length ?? 0) > 500) errors["notes"] = "الملاحظات طويلة جداً (500 حرف كحد أقصى)";

        // Items are typed by the agent; the same name typed twice is one line.
        var items = (input.Items ?? [])
            .Select(i => (Name: CollapseSpaces(i.Name), i.Quantity))
            .Where(i => i.Name.Length > 0)
            .GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => new OrderItemDto(g.First().Name, g.Sum(x => x.Quantity)))
            .ToList();
        if (items.Count == 0) errors["items"] = "اختر المنتج";
        else if (items.Count > MaxItems) errors["items"] = $"الحد الأقصى {MaxItems} مواد في الطلب";
        else if (items.Any(i => i.Name.Length is < 2 or > MaxItemName)) errors["items"] = $"اسم المادة بين 2 و {MaxItemName} حرفاً";
        else if (items.Any(i => i.Quantity is < 1 or > MaxQuantity)) errors["items"] = $"الكمية يجب أن تكون بين 1 و {MaxQuantity}";
        if (errors.Count > 0) throw ApiException.Validation(errors);

        var cleanName = UserService.Clean(name);
        await using var c = await _db.OpenAsync(ct);

        // One open order per customer phone + item (prevents sending the same sale twice).
        var duplicate = await c.ExecuteScalarAsync<string?>("""
            SELECT TOP 1 o.OrderNumber FROM agents.Orders o JOIN agents.OrderItems i ON i.OrderId = o.Id
            WHERE o.CustomerPhone = @phone AND o.Status IN @open AND i.Name IN @names
            """, new { phone, open = OrderStatus.Open, names = items.Select(i => i.Name).ToList() });
        if (duplicate is not null)
            throw ApiException.Conflict("duplicate_open_order", $"يوجد طلب مفتوح لنفس الزبون ونفس المادة (رقم {duplicate})", new { orderNumber = duplicate });

        long orderId;
        try
        {
            await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
            orderId = await c.ExecuteScalarAsync<long>("""
                INSERT agents.Orders(OrderUuid, SubmittedByUserId, IdempotencyKey, CityValue, CityName, PurchaseType, ForSelf,
                    FirstName, FatherName, GrandfatherName, Surname, CustomerPhone, SecondaryPhone, Area, AddressDetails, NearestLandmark,
                    Latitude, Longitude, ShopName, ShopBusinessType, ShopAddress, OwnsShop, Notes, Status, ShopPhotoUrl)
                OUTPUT inserted.Id
                VALUES (NEWID(), @UserId, @idempotencyKey, @CityValue, @branchName, @purchaseType, 0,
                    @FirstName, @FatherName, @GrandfatherName, @Surname, @phone, @secondary, @area, @details, @landmark,
                    @Latitude, @Longitude, @shopName, @shopType, @shopAddress, @ownsShop, @notes, 'New', @shopPhoto)
                """, new
            {
                UserId = user.Id, idempotencyKey, input.CityValue, branchName, purchaseType, shopPhoto,
                cleanName.FirstName, cleanName.FatherName, cleanName.GrandfatherName, cleanName.Surname, phone, secondary, area, details,
                landmark = address.NearestLandmark?.Trim(), address.Latitude, address.Longitude,
                shopName = shop?.Name?.Trim(), shopType = shop?.BusinessType?.Trim(), shopAddress = shop?.Address?.Trim(), ownsShop = shop?.OwnsShop,
                notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(),
            }, tx);
            await c.ExecuteAsync("UPDATE agents.Orders SET OrderNumber = @number WHERE Id = @orderId",
                new { orderId, number = $"QO-{DateTime.UtcNow:yyMM}-{orderId:D6}" }, tx);
            await c.ExecuteAsync("""
                INSERT agents.OrderItems(OrderId, Name, Quantity) VALUES (@orderId, @Name, @Quantity)
                """, items.Select(i => new { orderId, i.Name, i.Quantity }), tx);
            await AddEventAsync(c, tx, orderId, OrderStatus.New, null, false, null);
            await tx.CommitAsync(ct);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627 && ex.Message.Contains("UX_Orders_Idempotency"))
        {
            var existingId = await c.ExecuteScalarAsync<long>(
                "SELECT Id FROM agents.Orders WHERE SubmittedByUserId = @Id AND IdempotencyKey = @idempotencyKey", new { user.Id, idempotencyKey });
            return await GetForCustomerAsync(user.Id, existingId, ct);
        }

        if (!await TryForwardAsync(orderId, ct))
        {
            var pending = await GetForCustomerAsync(user.Id, orderId, ct);
            await _notifications.NotifyAsync(user.Id, NotificationTypes.Order, "تم استلام طلب البيع",
                $"طلب البيع رقم {pending.OrderNumber} محفوظ وسيُرسل إلى قسم المبيعات تلقائياً", new Dictionary<string, string> { ["orderId"] = orderId.ToString() });
        }
        return await GetForCustomerAsync(user.Id, orderId, ct);
    }

    /// <param name="status">One status or a comma-separated list (e.g. "New,UnderReview").</param>
    public async Task<PagedResult<OrderSummaryDto>> ListForCustomerAsync(long userId, string? status, Paging paging, CancellationToken ct)
    {
        var statuses = (status ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var any = statuses.Count > 0 ? 1 : 0;
        await using var c = await _db.OpenAsync(ct);
        using var multi = await c.QueryMultipleAsync("""
            SELECT COUNT(*) FROM agents.Orders WHERE SubmittedByUserId = @userId AND (@any = 0 OR Status IN @statuses);
            SELECT * FROM agents.Orders WHERE SubmittedByUserId = @userId AND (@any = 0 OR Status IN @statuses)
            ORDER BY Id DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """, new { userId, any, statuses = any == 1 ? statuses : [""], paging.Offset, paging.PageSize });
        var total = await multi.ReadSingleAsync<int>();
        var rows = (await multi.ReadAsync<OrderRow>()).ToList();
        var summaries = await ItemsSummariesAsync(c, rows.Select(r => r.Id).ToList());
        return new PagedResult<OrderSummaryDto>(rows.Select(r => ToSummary(r, summaries.GetValueOrDefault(r.Id, ""))).ToList(), total, paging.Page, paging.PageSize);
    }

    /// <summary>The app's home page. Days and months are Iraq time.</summary>
    public async Task<AgentOverviewDto> OverviewForAgentAsync(long userId, CancellationToken ct)
    {
        var today = Work.IraqTime.DateOf(DateTime.UtcNow);
        var dayStart = Work.IraqTime.DayStartUtc(today);
        var monthStart = Work.IraqTime.DayStartUtc(new DateOnly(today.Year, today.Month, 1));
        await using var c = await _db.OpenAsync(ct);
        var n = await c.QuerySingleAsync<(int Today, int Month, int SoldMonth, int Open, int Waiting, int Paid, int NotSoldMonth, int Pins)>("""
            SELECT
                (SELECT COUNT(*) FROM agents.Orders WHERE SubmittedByUserId = @userId AND CreatedAt >= @dayStart),
                (SELECT COUNT(*) FROM agents.Orders WHERE SubmittedByUserId = @userId AND CreatedAt >= @monthStart),
                (SELECT COUNT(*) FROM agents.Orders WHERE SubmittedByUserId = @userId AND Status IN ('Sold','Finished') AND SoldAt >= @monthStart),
                (SELECT COUNT(*) FROM agents.Orders WHERE SubmittedByUserId = @userId AND Status IN @open),
                (SELECT COUNT(*) FROM agents.Orders WHERE SubmittedByUserId = @userId AND Status = 'Sold'),
                (SELECT COUNT(*) FROM agents.Orders WHERE SubmittedByUserId = @userId AND Status = 'Finished'),
                (SELECT COUNT(*) FROM agents.Orders WHERE SubmittedByUserId = @userId AND Status IN ('NotSold','Denied') AND UpdatedAt >= @monthStart),
                (SELECT COUNT(*) FROM agents.Prospects WHERE CreatedByUserId = @userId AND IsDeleted = 0 AND CreatedAt >= @monthStart)
            """, new { userId, dayStart, monthStart, open = OrderStatus.Open });
        var recent = (await c.QueryAsync<OrderRow>(
            "SELECT TOP 5 * FROM agents.Orders WHERE SubmittedByUserId = @userId ORDER BY Id DESC", new { userId })).ToList();
        var summaries = await ItemsSummariesAsync(c, recent.Select(r => r.Id).ToList());
        // The agent's pinned shops (all time) per interest level; every level is listed, most interested first.
        var byLevel = (await c.QueryAsync<(int Interest, int Count)>("""
            SELECT CAST(Interest AS INT), COUNT(*) FROM agents.Prospects
            WHERE CreatedByUserId = @userId AND IsDeleted = 0 GROUP BY Interest
            """, new { userId })).ToDictionary(x => x.Interest, x => x.Count);
        var pinsByInterest = Enumerable.Range(1, 5).Reverse()
            .Select(l => new PinsByInterestDto(l, Work.ProspectInterest.Label(l), byLevel.GetValueOrDefault(l)))
            .ToList();
        return new AgentOverviewDto(n.Today, n.Month, n.SoldMonth, n.Open, n.Waiting, n.Paid, n.NotSoldMonth, n.Pins,
            recent.Select(r => ToSummary(r, summaries.GetValueOrDefault(r.Id, ""))).ToList(),
            byLevel.Values.Sum(), pinsByInterest);
    }

    public async Task<Dictionary<string, int>> CountsForCustomerAsync(long userId, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var counts = (await c.QueryAsync<(string Status, int Count)>(
                "SELECT Status, COUNT(*) FROM agents.Orders WHERE SubmittedByUserId = @userId GROUP BY Status", new { userId }))
            .ToDictionary(x => x.Status, x => x.Count);
        var result = OrderStatus.All.ToDictionary(s => s, s => counts.GetValueOrDefault(s));
        result["all"] = counts.Values.Sum();
        return result;
    }

    public async Task<OrderDetailDto> GetForCustomerAsync(long userId, long orderId, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var order = await c.QuerySingleOrDefaultAsync<OrderRow>("SELECT * FROM agents.Orders WHERE Id = @orderId AND SubmittedByUserId = @userId", new { orderId, userId })
                    ?? throw ApiException.NotFound("الطلب غير موجود");
        var items = await LoadItemsAsync(c, orderId);
        var timeline = (await c.QueryAsync<(string Status, DateTime CreatedAt, string? Note)>(
                "SELECT Status, CreatedAt, Note FROM agents.OrderEvents WHERE OrderId = @orderId AND IsInternal = 0 ORDER BY Id", new { orderId }))
            .Select(e => new TimelineDto(e.Status, OrderStatus.Label(e.Status), e.CreatedAt, e.Note)).ToList();
        var summary = ToSummary(order, Summarize(items));
        return new OrderDetailDto(summary.Id, summary.OrderNumber, summary.Status, summary.StatusLabel, summary.PurchaseType,
            summary.CustomerName, summary.CityName, summary.ItemsSummary, summary.CreatedAt, summary.UpdatedAt,
            order.CityValue, CustomerOf(order), AddressOf(order), ShopOf(order), order.Notes, items,
            order.Status is OrderStatus.Denied or OrderStatus.NotSold ? order.PublicReason : null,
            timeline, order.Status is OrderStatus.New or OrderStatus.UnderReview, _urls.MediaUrl(order.ShopPhotoUrl), summary.Sale);
    }

    public async Task<OrderDetailDto> CancelByCustomerAsync(long userId, long orderId, string? reason, CancellationToken ct)
    {
        await _db.InTransactionAsync(async (c, tx) =>
        {
            var updated = await c.ExecuteAsync("""
                UPDATE agents.Orders SET Status = 'Cancelled', UpdatedAt = SYSUTCDATETIME()
                WHERE Id = @orderId AND SubmittedByUserId = @userId AND Status IN ('New','UnderReview')
                """, new { orderId, userId }, tx);
            if (updated == 0) throw ApiException.Conflict("cannot_cancel", "لا يمكن إلغاء الطلب بعد تحويله إلى مندوب المبيعات");
            await AddEventAsync(c, tx, orderId, OrderStatus.Cancelled, string.IsNullOrWhiteSpace(reason) ? "ألغاه المسوّق" : reason.Trim(), false, null);
            return 0;
        }, ct: ct);
        return await GetForCustomerAsync(userId, orderId, ct);
    }

    // ===================== Admin (screening page) =====================

    /// <param name="from">UTC start (inclusive).</param>
    /// <param name="to">UTC end (exclusive).</param>
    public async Task<PagedResult<AdminOrderRow>> AdminListAsync(string? status, int? cityValue, string? q, DateTime? from, DateTime? to, Paging paging, CancellationToken ct)
    {
        var where = new StringBuilder("1 = 1");
        var args = new DynamicParameters(new { paging.Offset, paging.PageSize });
        if (!string.IsNullOrWhiteSpace(status)) { where.Append(" AND o.Status = @status"); args.Add("status", status); }
        if (cityValue is not null) { where.Append(" AND o.CityValue = @cityValue"); args.Add("cityValue", cityValue); }
        if (from is not null) { where.Append(" AND o.CreatedAt >= @from"); args.Add("from", from); }
        if (to is not null) { where.Append(" AND o.CreatedAt < @to"); args.Add("to", to); } // exclusive: start of the day after (Iraq time)
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            var phone = IraqiPhone.Normalize(term);
            where.Append(" AND (o.OrderNumber = @term OR o.CustomerPhone = @phone OR CONCAT(o.FirstName,' ',o.FatherName,' ',o.GrandfatherName,' ',o.Surname) LIKE '%' + @term + '%')");
            args.Add("term", term);
            args.Add("phone", phone ?? term);
        }

        await using var c = await _db.OpenAsync(ct);
        using var multi = await c.QueryMultipleAsync($"""
            SELECT COUNT(*) FROM agents.Orders o WHERE {where};
            SELECT o.Id FROM agents.Orders o WHERE {where} ORDER BY o.Id DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """, args);
        var total = await multi.ReadSingleAsync<int>();
        var ids = (await multi.ReadAsync<long>()).ToList();
        var rows = await LoadAdminRowsAsync(c, ids);
        return new PagedResult<AdminOrderRow>(rows, total, paging.Page, paging.PageSize);
    }

    public async Task<Dictionary<string, int>> AdminCountsAsync(int? cityValue, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var counts = (await c.QueryAsync<(string Status, int Count)>(
                "SELECT Status, COUNT(*) FROM agents.Orders WHERE (@cityValue IS NULL OR CityValue = @cityValue) GROUP BY Status", new { cityValue }))
            .ToDictionary(x => x.Status, x => x.Count);
        var result = OrderStatus.All.ToDictionary(s => s, s => counts.GetValueOrDefault(s));
        result["all"] = counts.Values.Sum();
        return result;
    }

    public async Task<AdminOrderDetailDto> AdminGetAsync(long orderId, AdminActor? openedBy, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var order = await c.QuerySingleOrDefaultAsync<OrderRow>("SELECT * FROM agents.Orders WHERE Id = @orderId", new { orderId })
                    ?? throw ApiException.NotFound("الطلب غير موجود");
        if (openedBy is not null && order.Status == OrderStatus.New)
        {
            await MarkUnderReviewAsync(c, order, openedBy);
            order = await c.QuerySingleAsync<OrderRow>("SELECT * FROM agents.Orders WHERE Id = @orderId", new { orderId });
        }
        var row = (await LoadAdminRowsAsync(c, [orderId])).Single();
        var items = await LoadItemsAsync(c, orderId);
        var timeline = (await c.QueryAsync<(string Status, DateTime CreatedAt, string? Note, bool IsInternal, string? Actor)>(
                "SELECT Status, CreatedAt, Note, IsInternal, Actor FROM agents.OrderEvents WHERE OrderId = @orderId ORDER BY Id", new { orderId }))
            .Select(e => new AdminTimelineDto(e.Status, OrderStatus.Label(e.Status), e.CreatedAt, e.Note, e.IsInternal, e.Actor)).ToList();
        return new AdminOrderDetailDto(row, CustomerOf(order), AddressOf(order), ShopOf(order), order.Notes, items, order.PublicReason,
            order.InternalNote, timeline, new LinkedSalesRequestDto(order.CityValue, order.SalesRequestId, order.BranchStatus, order.BranchRejectReason, order.LastSyncAt),
            order.ReviewedBy, order.DecidedBy, order.DecidedAt, _urls.MediaUrl(order.ShopPhotoUrl), SaleOf(order));
    }

    public async Task MarkUnderReviewAsync(long orderId, AdminActor actor, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var order = await c.QuerySingleOrDefaultAsync<OrderRow>("SELECT * FROM agents.Orders WHERE Id = @orderId", new { orderId }) ?? throw ApiException.NotFound();
        if (order.Status != OrderStatus.New) return;
        await MarkUnderReviewAsync(c, order, actor);
    }

    private async Task MarkUnderReviewAsync(SqlConnection c, OrderRow order, AdminActor actor)
    {
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync();
        var updated = await c.ExecuteAsync(
            "UPDATE agents.Orders SET Status = 'UnderReview', ReviewedBy = @Name, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id AND Status = 'New'",
            new { actor.Name, order.Id }, tx);
        if (updated == 1) await AddEventAsync(c, tx, order.Id, OrderStatus.UnderReview, null, false, actor.Name);
        await tx.CommitAsync();
        if (updated == 1)
            await _notifications.NotifyAsync(order.SubmittedByUserId, NotificationTypes.Order, "طلبك قيد المعالجة",
                $"بدأنا مراجعة طلبك رقم {order.OrderNumber}", new Dictionary<string, string> { ["orderId"] = order.Id.ToString() });
    }

    /// <summary>Who forwards app orders automatically (shown in the order timeline).</summary>
    public static readonly AdminActor AutoForwarder = new("إرسال تلقائي", "system");

    /// <summary>
    /// Orders go straight to the company system («طلبات البيع» → غير المسندة). Called right after an order is created;
    /// when the company system is unreachable the order stays New and <see cref="ForwardPendingAsync"/> retries it.
    /// </summary>
    public async Task<bool> TryForwardAsync(long orderId, CancellationToken ct)
    {
        try
        {
            await ApproveAsync(orderId, null, null, AutoForwarder, ct);
            return true;
        }
        catch (GatewayUnavailableException ex)
        {
            _logger.LogWarning("Order {OrderId} not forwarded yet: {Message}", orderId, ex.Message);
        }
        catch (ApiException ex) when (ex.Status is 409)
        {
            // Already forwarded (or cancelled) by a parallel call.
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Order {OrderId} not forwarded yet", orderId);
        }
        return false;
    }

    /// <summary>Retries orders still waiting to be forwarded (company system was down when they were created).</summary>
    public async Task<int> ForwardPendingAsync(CancellationToken ct)
    {
        List<long> ids;
        await using (var c = await _db.OpenAsync(ct))
        {
            ids = (await c.QueryAsync<long>("""
                SELECT TOP 50 Id FROM agents.Orders WHERE Status IN ('New','UnderReview') AND CreatedAt < DATEADD(SECOND, -30, SYSUTCDATETIME())
                ORDER BY Id
                """)).ToList();
        }
        var sent = 0;
        foreach (var id in ids)
            if (await TryForwardAsync(id, ct)) sent++;
        return sent;
    }

    /// <summary>Approve: create the sales request in the company system (idempotent on OrderUuid), then mark Transferred.</summary>
    public async Task<AdminOrderDetailDto> ApproveAsync(long orderId, string? note, string? salesNotes, AdminActor actor, CancellationToken ct)
    {
        OrderRow order;
        List<OrderItemDto> items;
        UserRow? submitter;
        await using (var c = await _db.OpenAsync(ct))
        {
            order = await c.QuerySingleOrDefaultAsync<OrderRow>("SELECT * FROM agents.Orders WHERE Id = @orderId", new { orderId }) ?? throw ApiException.NotFound();
            if (order.Status is not (OrderStatus.New or OrderStatus.UnderReview))
                throw ApiException.Conflict("invalid_state", $"لا يمكن قبول طلب حالته «{OrderStatus.Label(order.Status)}»");
            items = await LoadItemsAsync(c, orderId);
            submitter = await c.QuerySingleOrDefaultAsync<UserRow>($"SELECT {UserService.Columns} FROM agents.Users WHERE Id = @SubmittedByUserId", new { order.SubmittedByUserId });
        }

        var created = await _gateway.CreateSalesRequestAsync(new GatewaySalesRequestCreate(
            order.OrderUuid, order.CityValue, order.CustomerName, order.CustomerPhone, order.CityName,
            Truncate(BuildAddress(order), 400), BuildSalesNotes(order, salesNotes), Truncate(Summarize(items), 200),
            submitter is null ? null : Truncate(submitter.FullName, 100)), ct);

        await _db.InTransactionAsync(async (c, tx) =>
        {
            var updated = await c.ExecuteAsync("""
                UPDATE agents.Orders SET Status = 'Transferred', SalesRequestId = @SalesRequestId, BranchStatus = @Status,
                       DecidedBy = @Name, DecidedAt = SYSUTCDATETIME(), InternalNote = COALESCE(@note, InternalNote), UpdatedAt = SYSUTCDATETIME()
                WHERE Id = @orderId AND Status IN ('New','UnderReview')
                """, new { created.SalesRequestId, created.Status, actor.Name, note, orderId }, tx);
            if (updated == 0) throw ApiException.Conflict("invalid_state", "تم تغيير حالة الطلب من قبل مستخدم آخر");
            await AddEventAsync(c, tx, orderId, OrderStatus.Transferred, null, false, actor.Name);
            if (!string.IsNullOrWhiteSpace(note)) await AddEventAsync(c, tx, orderId, OrderStatus.Transferred, note.Trim(), true, actor.Name);
            await _audit.WriteAsync(actor, "order.approve", "Order", orderId, new { order.Status }, new { Status = OrderStatus.Transferred, created.SalesRequestId, created.CityValue }, c, tx);
            return 0;
        }, ct: ct);

        await _notifications.NotifyAsync(order.SubmittedByUserId, NotificationTypes.Order, "تم إرسال الطلب إلى قسم المبيعات",
            $"طلب البيع رقم {order.OrderNumber} وصل إلى قسم المبيعات وسيتم إسناده لموظف لإكمال البيع",
            new Dictionary<string, string> { ["orderId"] = orderId.ToString() });
        return await AdminGetAsync(orderId, null, ct);
    }

    public async Task<AdminOrderDetailDto> DenyAsync(long orderId, string? publicReason, string? internalNote, AdminActor actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(publicReason) || publicReason.Trim().Length < 3)
            throw ApiException.Validation(new() { ["publicReason"] = "اكتب سبب الرفض الذي سيظهر للزبون" });
        var reason = Truncate(publicReason.Trim(), 400);
        var order = await _db.InTransactionAsync(async (c, tx) =>
        {
            var o = await c.QuerySingleOrDefaultAsync<OrderRow>("SELECT * FROM agents.Orders WITH (UPDLOCK) WHERE Id = @orderId", new { orderId }, tx) ?? throw ApiException.NotFound();
            if (o.Status is not (OrderStatus.New or OrderStatus.UnderReview))
                throw ApiException.Conflict("invalid_state", $"لا يمكن رفض طلب حالته «{OrderStatus.Label(o.Status)}»");
            await c.ExecuteAsync("""
                UPDATE agents.Orders SET Status = 'Denied', PublicReason = @reason, InternalNote = @internalNote, DecidedBy = @Name,
                       DecidedAt = SYSUTCDATETIME(), UpdatedAt = SYSUTCDATETIME() WHERE Id = @orderId
                """, new { reason, internalNote = internalNote?.Trim(), actor.Name, orderId }, tx);
            await AddEventAsync(c, tx, orderId, OrderStatus.Denied, reason, false, actor.Name);
            if (!string.IsNullOrWhiteSpace(internalNote)) await AddEventAsync(c, tx, orderId, OrderStatus.Denied, internalNote.Trim(), true, actor.Name);
            await _audit.WriteAsync(actor, "order.deny", "Order", orderId, new { o.Status }, new { Status = OrderStatus.Denied, reason, internalNote }, c, tx);
            return o;
        }, ct: ct);

        await _notifications.NotifyAsync(order.SubmittedByUserId, NotificationTypes.Order, "تعذر قبول طلبك",
            $"نعتذر، لم يتم قبول طلبك رقم {order.OrderNumber}. السبب: {reason}",
            new Dictionary<string, string> { ["orderId"] = orderId.ToString() });
        return await AdminGetAsync(orderId, null, ct);
    }

    public async Task<AdminOrderDetailDto> SetPublicReasonAsync(long orderId, string? publicReason, AdminActor actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(publicReason)) throw ApiException.Validation(new() { ["publicReason"] = "مطلوب" });
        await using (var c = await _db.OpenAsync(ct))
        {
            var updated = await c.ExecuteAsync(
                "UPDATE agents.Orders SET PublicReason = @reason, UpdatedAt = SYSUTCDATETIME() WHERE Id = @orderId AND Status IN ('NotSold','Denied')",
                new { reason = Truncate(publicReason.Trim(), 400), orderId });
            if (updated == 0) throw ApiException.Conflict("invalid_state", "يمكن تعديل السبب فقط للطلبات المرفوضة أو التي لم يتم بيعها");
            await _audit.WriteAsync(actor, "order.public_reason", "Order", orderId, null, new { publicReason }, c);
        }
        return await AdminGetAsync(orderId, null, ct);
    }

    // ===================== Sync with the company system =====================

    /// <summary>
    /// Pulls branch status for orders waiting on the company system (and recently sold ones, to catch cancellations).
    /// Safe to run concurrently with itself: every transition is a conditional UPDATE and commissions are idempotent.
    /// </summary>
    public async Task<int> SyncAsync(IReadOnlyCollection<long>? onlyOrderIds, CancellationToken ct)
    {
        List<OrderRow> orders;
        await using (var c = await _db.OpenAsync(ct))
        {
            orders = (await c.QueryAsync<OrderRow>(onlyOrderIds is { Count: > 0 }
                ? "SELECT * FROM agents.Orders WHERE Id IN @onlyOrderIds AND SalesRequestId IS NOT NULL"
                : """
                  SELECT TOP 1000 * FROM agents.Orders
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

    /// <summary>
    /// Follows sold orders in the company system until they are fully paid: refreshes paid/remaining and moves the
    /// order to Finished at the last payment. Least recently synced first.
    /// </summary>
    public async Task<int> SyncSalesProgressAsync(IReadOnlyCollection<long>? onlyOrderIds, CancellationToken ct)
    {
        List<OrderRow> orders;
        await using (var c = await _db.OpenAsync(ct))
        {
            orders = (await c.QueryAsync<OrderRow>(onlyOrderIds is { Count: > 0 }
                ? "SELECT * FROM agents.Orders WHERE Id IN @onlyOrderIds AND Status = 'Sold' AND SaleId IS NOT NULL"
                : "SELECT TOP 300 * FROM agents.Orders WHERE Status = 'Sold' AND SaleId IS NOT NULL ORDER BY ISNULL(SaleProgressSyncAt, '2000-01-01')",
                new { onlyOrderIds })).ToList();
        }

        var finished = 0;
        foreach (var order in orders)
        {
            GatewaySaleProgress? p;
            try
            {
                p = await _gateway.GetSaleProgressAsync(order.CityValue, order.SaleId!.Value, ct);
            }
            catch (GatewayUnavailableException ex)
            {
                _logger.LogWarning("Sale progress for order {OrderId} unavailable: {Detail}", order.Id, ex.Detail);
                continue;
            }
            if (p is null) continue;

            var done = p.TotalAmount > 0 && p.RemainingAmount <= 0;
            await using var c = await _db.OpenAsync(ct);
            await c.ExecuteAsync("""
                UPDATE agents.Orders SET SaleTotal = @TotalAmount, SalePaid = @PaidAmount, SalePaymentsCount = @PaymentsCount,
                       SaleLastPaymentAt = @LastPaymentDate, SaleProgressSyncAt = SYSUTCDATETIME()
                WHERE Id = @Id
                """, new { p.TotalAmount, p.PaidAmount, p.PaymentsCount, p.LastPaymentDate, order.Id });
            if (!done) continue;

            var moved = await _db.InTransactionAsync(async (cx, tx) =>
            {
                var updated = await cx.ExecuteAsync(
                    "UPDATE agents.Orders SET Status = 'Finished', FinishedAt = SYSUTCDATETIME(), UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id AND Status = 'Sold'",
                    new { order.Id }, tx);
                if (updated == 1) await AddEventAsync(cx, tx, order.Id, OrderStatus.Finished, null, false, "المنظومة");
                return updated == 1;
            }, ct: ct);
            if (!moved) continue;
            finished++;
            await NotifyStatusAsync(order, OrderStatus.Finished, null);
        }
        return finished;
    }

    /// <summary>Stores a shop photo for an order the agent is about to submit. Returns the URL to send with the order.</summary>
    public async Task<string> UploadShopPhotoAsync(long userId, IFormFile file, string mediaRoot, CancellationToken ct)
    {
        const long maxBytes = 8 * 1024 * 1024;
        var ext = Path.GetExtension(file.FileName ?? "").ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp")) ext = ".jpg";
        if (file.Length == 0 || file.Length > maxBytes) throw ApiException.BadRequest("invalid_file", "حجم الصورة يجب ألا يتجاوز 8 ميغابايت");

        await using var input = file.OpenReadStream();
        var header = new byte[12];
        var read = await input.ReadAsync(header, ct);
        var isJpeg = read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        var isPng = read >= 4 && header.AsSpan(0, 4).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        var isWebp = read >= 12 && header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 4).SequenceEqual("WEBP"u8);
        if (!(isJpeg || isPng || isWebp)) throw ApiException.BadRequest("invalid_file", "الملف ليس صورة");
        input.Position = 0;

        var month = DateTime.UtcNow.ToString("yyyy-MM");
        var dir = Path.Combine(mediaRoot, "shops", month);
        Directory.CreateDirectory(dir);
        var name = $"{userId}-{Guid.NewGuid():N}{(isPng ? ".png" : isWebp ? ".webp" : ".jpg")}";
        await using (var output = File.Create(Path.Combine(dir, name)))
        {
            await input.CopyToAsync(output, ct);
        }
        return _urls.MediaUrl($"{ShopPhotoPrefix}{month}/{name}")!;
    }

    private async Task<bool> ApplyBranchStatusAsync(OrderRow order, GatewayRequestStatus s, CancellationToken ct)
    {
        if (order.Status == OrderStatus.Finished) return false;
        var target = OrderStatus.FromBranch(s.Found, s.Status, s.IsDeleted);
        await using var c = await _db.OpenAsync(ct);
        await c.ExecuteAsync("""
            UPDATE agents.Orders SET BranchStatus = @Status, BranchRejectReason = @RejectionReason, LastSyncAt = SYSUTCDATETIME(),
                   SalesRequestId = COALESCE(@SalesRequestId, SalesRequestId), SaleId = COALESCE(@ConvertedToSaleId, SaleId) WHERE Id = @Id
            """, new { s.Status, s.RejectionReason, s.SalesRequestId, s.ConvertedToSaleId, order.Id });
        if (target is null || target == order.Status) return false;

        // The company's reason (written by the sales employee) is what the agent sees; a generic text only without one.
        var companyReason = AgentReason(s.RejectionReason);

        // A sale already marked Sold that the company later cancels.
        if (order.Status == OrderStatus.Sold)
        {
            if (target != OrderStatus.NotSold) return false;
            var cancelReason = companyReason is null ? "تم إلغاء عملية البيع" : "تم إلغاء عملية البيع: " + companyReason;
            var cancelled = await _db.InTransactionAsync(async (cx, tx) =>
            {
                var updated = await cx.ExecuteAsync(
                    "UPDATE agents.Orders SET Status = 'NotSold', PublicReason = @reason, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id AND Status = 'Sold'",
                    new { reason = Truncate(cancelReason, 400), order.Id }, tx);
                if (updated == 0) return 0;
                await AddEventAsync(cx, tx, order.Id, OrderStatus.NotSold, Truncate(cancelReason, 400), false, "المنظومة");
                return 1;
            }, ct: ct);
            if (cancelled == 0) return false;
            await NotifyStatusAsync(order, OrderStatus.NotSold, cancelReason);
            return true;
        }

        if (target == OrderStatus.Sold)
        {
            var sold = await _db.InTransactionAsync(async (cx, tx) =>
            {
                var updated = await cx.ExecuteAsync(
                    "UPDATE agents.Orders SET Status = 'Sold', SoldAt = SYSUTCDATETIME(), UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id AND Status IN ('Transferred','FieldInspection')",
                    new { order.Id }, tx);
                if (updated == 1) await AddEventAsync(cx, tx, order.Id, OrderStatus.Sold, null, false, "المنظومة");
                return updated;
            }, ct: ct);
            if (sold == 0) return false;
            await NotifyStatusAsync(order, OrderStatus.Sold, null);
            return true;
        }

        var publicReason = target == OrderStatus.NotSold ? companyReason ?? OrderStatus.DefaultNotSoldReason : null;
        var changed = await c.ExecuteAsync("""
            UPDATE agents.Orders SET Status = @target, PublicReason = COALESCE(@publicReason, PublicReason), UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND Status IN ('Transferred','FieldInspection')
            """, new { target, publicReason, order.Id });
        if (changed == 0) return false;
        await using (var tx = (SqlTransaction)await c.BeginTransactionAsync(ct))
        {
            // The order timeline shows the reason to the agent.
            await AddEventAsync(c, tx, order.Id, target, target == OrderStatus.NotSold ? companyReason : null, false, "المنظومة");
            await tx.CommitAsync(ct);
        }
        await NotifyStatusAsync(order, target, publicReason);
        return true;
    }

    /// <summary>The company system's rejection / cancel reason, tidied for the agent (one line, at most 400 chars).</summary>
    private static string? AgentReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return null;
        var oneLine = string.Join(' ', reason.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return Truncate(oneLine, 400);
    }

    private Task NotifyStatusAsync(OrderRow order, string status, string? reason)
    {
        var (title, body) = status switch
        {
            OrderStatus.FieldInspection => ("طلبك قيد الكشف الميداني", $"مندوب المبيعات يعمل على طلبك رقم {order.OrderNumber}"),
            OrderStatus.Sold => ("تم البيع بنجاح 🎉", $"تحوّل طلبك رقم {order.OrderNumber} إلى مبيعة. سنتابع تسديدها حتى الاكتمال"),
            OrderStatus.Finished => ("اكتمل تسديد المبيعة ✅", $"المبيعة رقم {order.OrderNumber} مسددة بالكامل"),
            OrderStatus.NotSold => ("لم يتم إتمام البيع", $"طلبك رقم {order.OrderNumber}: {reason ?? OrderStatus.DefaultNotSoldReason}"),
            _ => ("تحديث على طلبك", $"حالة طلبك رقم {order.OrderNumber}: {OrderStatus.Label(status)}"),
        };
        return _notifications.NotifyAsync(order.SubmittedByUserId, NotificationTypes.Order, title, body,
            new Dictionary<string, string> { ["orderId"] = order.Id.ToString() });
    }

    // ===================== Helpers =====================

    private static Task AddEventAsync(SqlConnection c, SqlTransaction tx, long orderId, string status, string? note, bool isInternal, string? actor) =>
        c.ExecuteAsync("INSERT agents.OrderEvents(OrderId, Status, Note, IsInternal, Actor) VALUES (@orderId, @status, @note, @isInternal, @actor)",
            new { orderId, status, note, isInternal, actor }, tx);

    private static async Task<List<OrderItemDto>> LoadItemsAsync(SqlConnection c, long orderId) =>
        (await c.QueryAsync<OrderItemDto>("SELECT Name, Quantity FROM agents.OrderItems WHERE OrderId = @orderId ORDER BY Id", new { orderId })).ToList();

    private static async Task<Dictionary<long, string>> ItemsSummariesAsync(SqlConnection c, List<long> ids)
    {
        if (ids.Count == 0) return [];
        return (await c.QueryAsync<(long OrderId, string Name, int Quantity)>(
                "SELECT OrderId, Name, Quantity FROM agents.OrderItems WHERE OrderId IN @ids ORDER BY Id", new { ids }))
            .GroupBy(r => r.OrderId)
            .ToDictionary(g => g.Key, g => Summarize(g.Select(r => new OrderItemDto(r.Name, r.Quantity))));
    }

    private static async Task<List<AdminOrderRow>> LoadAdminRowsAsync(SqlConnection c, List<long> ids)
    {
        if (ids.Count == 0) return [];
        var rows = (await c.QueryAsync<OrderRow>("SELECT * FROM agents.Orders WHERE Id IN @ids", new { ids })).ToDictionary(o => o.Id);
        var summaries = await ItemsSummariesAsync(c, ids);
        var submitters = (await c.QueryAsync<(long Id, string FirstName, string FatherName, string GrandfatherName, string? Surname, string Phone)>(
                "SELECT Id, FirstName, FatherName, GrandfatherName, Surname, Phone FROM agents.Users WHERE Id IN @userIds",
                new { userIds = rows.Values.Select(r => r.SubmittedByUserId).Distinct().ToList() }))
            .ToDictionary(u => u.Id);
        var duplicates = (await c.QueryAsync<(long Id, int Count)>("""
                SELECT o.Id, (SELECT COUNT(*) FROM agents.Orders x WHERE x.CustomerPhone = o.CustomerPhone AND x.Id <> o.Id AND x.Status IN @open)
                FROM agents.Orders o WHERE o.Id IN @ids
                """, new { ids, open = OrderStatus.Open }))
            .ToDictionary(d => d.Id, d => d.Count);

        return ids.Where(rows.ContainsKey).Select(id =>
        {
            var o = rows[id];
            var u = submitters[o.SubmittedByUserId];
            return new AdminOrderRow(o.Id, o.OrderNumber, o.Status, OrderStatus.Label(o.Status), o.PurchaseType, o.CityValue, o.CityName,
                o.CustomerName, o.CustomerPhone, o.FirstName, o.FatherName, o.GrandfatherName, o.Surname, summaries.GetValueOrDefault(id, ""),
                new SubmittedByDto(u.Id, string.Join(' ', new[] { u.FirstName, u.FatherName, u.GrandfatherName, u.Surname }.Where(s => !string.IsNullOrEmpty(s))), u.Phone),
                o.CreatedAt, o.SalesRequestId, duplicates.GetValueOrDefault(id));
        }).ToList();
    }

    private static OrderSummaryDto ToSummary(OrderRow o, string itemsSummary) =>
        new(o.Id, o.OrderNumber, o.Status, OrderStatus.Label(o.Status), o.PurchaseType, o.CustomerName, o.CityName,
            itemsSummary, o.CreatedAt, o.UpdatedAt, SaleOf(o));

    /// <summary>Null until the order is sold. The total comes from the company system (0 until the first payment sync).</summary>
    private static SaleProgressDto? SaleOf(OrderRow o)
    {
        if (o.Status is not (OrderStatus.Sold or OrderStatus.Finished)) return null;
        var total = o.SaleTotal ?? 0;
        var paid = o.Status == OrderStatus.Finished ? Math.Max(total, o.SalePaid ?? 0) : o.SalePaid ?? 0;
        var remaining = Math.Max(0, total - paid);
        var percent = o.Status == OrderStatus.Finished ? 100 : total <= 0 ? 0 : (int)Math.Clamp(Math.Round(100.0 * paid / total), 0, 100);
        return new SaleProgressDto(o.SaleId, total, paid, remaining, o.SalePaymentsCount ?? 0, o.SaleLastPaymentAt, percent,
            o.SoldAt, o.FinishedAt, o.SaleProgressSyncAt);
    }

    private static OrderCustomerDto CustomerOf(OrderRow o) =>
        new(o.FirstName, o.FatherName, o.GrandfatherName, o.Surname, o.CustomerName, o.CustomerPhone, o.SecondaryPhone);

    private static OrderAddressDto AddressOf(OrderRow o) => new(o.Area, o.AddressDetails, o.NearestLandmark, o.Latitude, o.Longitude);

    private static OrderShopDto? ShopOf(OrderRow o) =>
        o.ShopName is null && o.ShopBusinessType is null ? null : new OrderShopDto(o.ShopName, o.ShopBusinessType, o.ShopAddress, o.OwnsShop);

    /// <summary>«ثلاجة، غسالة ×2»: the quantity is shown only when more than one.</summary>
    private static string Summarize(IEnumerable<OrderItemDto> items) =>
        string.Join("، ", items.Select(i => i.Quantity > 1 ? $"{i.Name} ×{i.Quantity}" : i.Name));

    private static string CollapseSpaces(string? s) => string.Join(' ', (s ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string BuildAddress(OrderRow o)
    {
        var parts = new List<string> { o.Area, o.AddressDetails }.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (!string.IsNullOrWhiteSpace(o.NearestLandmark)) parts.Add("قرب " + o.NearestLandmark);
        return string.Join(" - ", parts);
    }

    /// <summary>
    /// The notes of the company sales request, written like a normal request: purchase type, the shop, a second phone
    /// and the agent's own notes. The item is sent separately (productName); the agent is the request's source
    /// («المروج: الاسم»). The shop photo and map location stay in the dashboard («طلبات المروّجين»).
    /// </summary>
    public static string BuildSalesNotes(OrderRow o, string? adminNote)
    {
        // Sales are installments; only the exception (cash, from older app versions) is worth a line.
        var lines = new List<string>();
        if (o.PurchaseType == PurchaseTypes.Cash) lines.Add($"نوع الشراء: {PurchaseTypes.Label(o.PurchaseType)}");
        if (!string.IsNullOrWhiteSpace(o.ShopName))
            lines.Add(string.IsNullOrWhiteSpace(o.ShopBusinessType) ? $"المحل: {o.ShopName}" : $"المحل: {o.ShopName} ({o.ShopBusinessType})");
        if (o.SecondaryPhone is not null) lines.Add($"هاتف ثانٍ: {o.SecondaryPhone}");
        if (!string.IsNullOrWhiteSpace(o.Notes)) lines.Add(o.Notes.Trim());
        if (!string.IsNullOrWhiteSpace(adminNote)) lines.Add(adminNote.Trim());
        return Truncate(string.Join('\n', lines), 750);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
}
