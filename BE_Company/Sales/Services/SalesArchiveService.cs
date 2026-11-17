using System.Data;
using BE_Company.Sales.DTO;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Services;

public interface ISalesArchiveService
{
    Task RecordCompletedSaleAsync(SalesDraftDTO sale, CancellationToken ct);
    Task<IReadOnlyList<SalesArchiveBundleDto>> ListForEmployeeAsync(int employeeId, CancellationToken ct);
    Task<SalesArchiveBundleDto?> GetForEmployeeAsync(int employeeId, int bundleId, CancellationToken ct);
    Task<SalesArchiveBundleDto> SendAsync(
        int employeeId,
        int bundleId,
        string? recipient,
        string? name,
        string? phone,
        string? note,
        IReadOnlyList<IFormFile> files,
        CancellationToken ct);
    Task<(string ContentType, byte[] Bytes)?> ReadPhotoForEmployeeAsync(int employeeId, int bundleId, int photoId, CancellationToken ct);
    Task<(string ContentType, byte[] Bytes)?> ReadPhotoForEmployeeAsync(int employeeId, int bundleId, CancellationToken ct);
    Task<SalesArchiveBoardDto> BoardAsync(CancellationToken ct);
    Task<SalesArchiveBundleDto?> GetForAccountantAsync(int bundleId, CancellationToken ct);
    Task<int> ArchiveDecideAsync(string actor, IReadOnlyList<int> archiveIds, bool accept, string? note, CancellationToken ct);
    Task<IReadOnlyList<SalesArchiveItemDto>> CommitteeItemsAsync(string committeeUser, CancellationToken ct);
    Task<int> CommitteeReceiveAsync(string committeeUser, IReadOnlyList<int> archiveIds, bool accept, string? note, CancellationToken ct);
    Task<int> CommitteeSendToArchiveAsync(string committeeUser, IReadOnlyList<int> archiveIds, CancellationToken ct);
    Task<int> CommitteeReturnToAgentAsync(string committeeUser, IReadOnlyList<int> archiveIds, string? note, CancellationToken ct);
    Task<(string ContentType, byte[] Bytes)?> ReadPhotoForAccountantAsync(int bundleId, CancellationToken ct);
    Task<(string ContentType, byte[] Bytes)?> ReadPhotoForAccountantAsync(int bundleId, int photoId, CancellationToken ct);
}

public sealed class SalesArchiveService : ISalesArchiveService
{
    private readonly SalesDevelopmentGuard _guard;
    private readonly IWebHostEnvironment _env;
    private readonly IIraqClock _clock;

    public SalesArchiveService(SalesDevelopmentGuard guard, IWebHostEnvironment env, IIraqClock clock)
    {
        _guard = guard;
        _env = env;
        _clock = clock;
    }

    public async Task RecordCompletedSaleAsync(SalesDraftDTO sale, CancellationToken ct)
    {
        if (sale.SaleId <= 0 || sale.EmployeeId <= 0 || sale.CompletedAt == null)
        {
            return;
        }

        await using var connection = await OpenAsync(ct);
        await EnsureSchemaAsync(connection, ct);
        var listName = await LookupListNameAsync(connection, sale.CustomerListId, ct);
        var label = SalesArchiveRules.ItemLabel(
            (sale.Items ?? []).Select(i => (i.ProductName, i.Quantity)));
        await InsertSnapshotAsync(connection, new Snapshot(
            sale.SaleId,
            sale.EmployeeId,
            sale.UserName,
            sale.FullName,
            sale.FinalSalePrice,
            sale.CompletedAt.Value,
            sale.CustomerListId,
            label,
            listName), ct);
    }

    public async Task<IReadOnlyList<SalesArchiveBundleDto>> ListForEmployeeAsync(int employeeId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await EnsureSchemaAsync(connection, ct);
        await BackfillAsync(connection, employeeId, ct);
        var rows = await LoadBundlesAsync(connection, employeeId, ct);
        return rows;
    }

    public async Task<SalesArchiveBundleDto?> GetForEmployeeAsync(int employeeId, int bundleId, CancellationToken ct)
    {
        var bundle = await GetBundleAsync(bundleId, ct);
        if (bundle == null || bundle.EmployeeId != employeeId)
        {
            return null;
        }

        return bundle;
    }

    public async Task<SalesArchiveBundleDto> SendAsync(
        int employeeId,
        int bundleId,
        string? recipient,
        string? name,
        string? phone,
        string? note,
        IReadOnlyList<IFormFile>? files,
        CancellationToken ct)
    {
        var cleanRecipient = SalesArchiveRules.CleanPersonName(recipient);
        if (cleanRecipient == null)
        {
            throw new SalesArchiveException(
                StatusCodes.Status400BadRequest,
                "اختر مسؤول لجنة المراقبة أو مساعده الذي سيستلم الأرشيف (حدّث التطبيق إن لم يظهر الخيار).");
        }

        string? cleanName = null;
        string? cleanPhone = null;
        if (!string.IsNullOrWhiteSpace(name) || !string.IsNullOrWhiteSpace(phone))
        {
            var (courierOk, courierError, courierName, courierPhone) = SalesArchiveRules.ValidateCourier(name, phone);
            if (!courierOk)
            {
                throw new SalesArchiveException(StatusCodes.Status400BadRequest, courierError!);
            }

            cleanName = courierName;
            cleanPhone = courierPhone;
        }

        var uploads = (files ?? []).Where(f => f.Length > 0).ToList();
        var (photosOk, photosError) = SalesArchiveRules.ValidateDeliveryPhotos(uploads.Count, uploads.Select(f => f.Length));
        if (!photosOk)
        {
            throw new SalesArchiveException(StatusCodes.Status400BadRequest, photosError!);
        }

        await using var connection = await OpenAsync(ct);
        await EnsureSchemaAsync(connection, ct);
        var row = await LoadBundleRowAsync(connection, bundleId, ct)
                  ?? throw new SalesArchiveException(StatusCodes.Status404NotFound, "الحزمة غير موجودة.");
        if (row.EmployeeId != employeeId)
        {
            throw new SalesArchiveException(StatusCodes.Status403Forbidden, "هذه الحزمة لا تخصك.");
        }

        var today = SalesArchiveRules.IraqToday(_clock.UtcNow);
        var phase = PhaseOf(row, today);
        if (!SalesArchiveRules.CanSend(phase))
        {
            throw new SalesArchiveException(
                StatusCodes.Status409Conflict,
                phase == SalesArchiveRules.PhaseOpen
                    ? "لا يمكن إرسال الحزمة قبل انتهاء فترة العشرة أيام."
                    : "هذه الحزمة لا تنتظر إرسالاً الآن.");
        }

        List<string>? relatives = null;
        if (uploads.Count > 0)
        {
            relatives = await SavePhotosAsync(bundleId, uploads, ct);
        }

        var cleanNote = SalesArchiveRules.CleanOptionalNote(note);
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.SalesArchiveBundles
SET IntermediateName = @Name,
    IntermediatePhone = @Phone,
    RecipientName = @Recipient,
    Note = @Note,
    PhotoPath = CASE WHEN @UpdatePhoto = 1 THEN @PhotoPath ELSE PhotoPath END,
    SentAt = SYSUTCDATETIME()
WHERE BundleId = @BundleId AND EmployeeId = @EmployeeId",
            new
            {
                BundleId = bundleId,
                EmployeeId = employeeId,
                Name = (object?)cleanName ?? DBNull.Value,
                Phone = (object?)cleanPhone ?? DBNull.Value,
                Recipient = cleanRecipient,
                Note = (object?)cleanNote ?? DBNull.Value,
                UpdatePhoto = relatives != null,
                PhotoPath = (object?)relatives?.FirstOrDefault() ?? DBNull.Value
            }, tx, cancellationToken: ct));
        if (relatives != null)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM dbo.SalesArchivePhotos WHERE BundleId = @BundleId",
                new { BundleId = bundleId }, tx, cancellationToken: ct));
            for (var i = 0; i < relatives.Count; i++)
            {
                await connection.ExecuteAsync(new CommandDefinition(@"
INSERT INTO dbo.SalesArchivePhotos (BundleId, RelativePath, SortOrder)
VALUES (@BundleId, @RelativePath, @SortOrder)",
                    new { BundleId = bundleId, RelativePath = relatives[i], SortOrder = i + 1 },
                    tx, cancellationToken: ct));
            }
        }

        // Everything still with the agent (never sent, or refused back) goes to the chosen committee member.
        await connection.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.SalesArchiveItems
SET Status = N'ToCommittee',
    CommitteeUser = @Recipient,
    CommitteeAt = SYSUTCDATETIME(),
    RefusalNote = NULL
WHERE BundleId = @BundleId AND Status IN (N'Pending', N'Refused')",
            new { BundleId = bundleId, Recipient = cleanRecipient }, tx, cancellationToken: ct));

        await tx.CommitAsync(ct);
        return (await GetForEmployeeAsync(employeeId, bundleId, ct))!;
    }

    public Task<(string ContentType, byte[] Bytes)?> ReadPhotoForEmployeeAsync(int employeeId, int bundleId, CancellationToken ct) =>
        ReadPhotoAsync(bundleId, employeeId, null, ct);

    public Task<(string ContentType, byte[] Bytes)?> ReadPhotoForEmployeeAsync(int employeeId, int bundleId, int photoId, CancellationToken ct) =>
        ReadPhotoAsync(bundleId, employeeId, photoId, ct);

    public async Task<SalesArchiveBoardDto> BoardAsync(CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await EnsureSchemaAsync(connection, ct);
        await BackfillAsync(connection, null, ct);
        var rows = await LoadBundlesAsync(connection, null, ct);
        var board = new SalesArchiveBoardDto();
        foreach (var row in rows.OrderBy(r => r.EmployeeName, StringComparer.CurrentCulture).ThenByDescending(r => r.BundleNumber))
        {
            switch (row.Stage)
            {
                case SalesArchiveRules.StageAtArchive:
                    board.AtArchive.Add(row);
                    break;
                case SalesArchiveRules.StageAtCommittee:
                    board.AtCommittee.Add(row);
                    break;
                case SalesArchiveRules.PhaseOpen:
                    board.Current.Add(row);
                    break;
                case SalesArchiveRules.PhaseReady:
                    board.Ready.Add(row);
                    break;
                case SalesArchiveRules.PhaseInTransit:
                    board.InTransit.Add(row);
                    break;
                case SalesArchiveRules.PhaseNeedsFix:
                    board.NeedsFix.Add(row);
                    break;
                case SalesArchiveRules.PhaseApproved:
                    board.Approved.Add(row);
                    break;
            }
        }

        return board;
    }

    public Task<SalesArchiveBundleDto?> GetForAccountantAsync(int bundleId, CancellationToken ct) =>
        GetBundleAsync(bundleId, ct);

    public async Task<int> ArchiveDecideAsync(string actor, IReadOnlyList<int> archiveIds, bool accept, string? note, CancellationToken ct)
    {
        var ids = CleanIds(archiveIds);
        var cleanNote = accept ? null : RequireNote(note);
        var cleanActor = CleanActor(actor);
        await using var connection = await OpenAsync(ct);
        await EnsureSchemaAsync(connection, ct);
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(ct);
        // Refused papers go back to the committee member who handed them over; legacy papers sent
        // straight to the accountant (no committee member) go back to the sales agent.
        var affected = await connection.ExecuteAsync(new CommandDefinition(accept
                ? @"
UPDATE dbo.SalesArchiveItems
SET Status = N'Approved', RefusalNote = NULL, ArchiveBy = @Actor, ArchiveAt = SYSUTCDATETIME()
WHERE ArchiveId IN @Ids AND Status = N'ToArchive'"
                : @"
UPDATE dbo.SalesArchiveItems
SET Status = CASE WHEN CommitteeUser IS NULL THEN N'Refused' ELSE N'ArchiveRefused' END,
    RefusalNote = @Note, ArchiveBy = @Actor, ArchiveAt = SYSUTCDATETIME()
WHERE ArchiveId IN @Ids AND Status = N'ToArchive'",
            new { Ids = ids, Actor = cleanActor, Note = cleanNote }, tx, cancellationToken: ct));
        await EnsureAllAsync(tx, affected, ids.Length, ct);
        await connection.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.SalesArchiveBundles SET ReviewedAt = SYSUTCDATETIME()
WHERE BundleId IN (SELECT BundleId FROM dbo.SalesArchiveItems WHERE ArchiveId IN @Ids)",
            new { Ids = ids }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
        return affected;
    }

    public async Task<IReadOnlyList<SalesArchiveItemDto>> CommitteeItemsAsync(string committeeUser, CancellationToken ct)
    {
        var user = CleanActor(committeeUser);
        await using var connection = await OpenAsync(ct);
        await EnsureSchemaAsync(connection, ct);
        await PurgeDeletedSalesAsync(connection, ct);
        var rows = await connection.QueryAsync<ItemRow>(new CommandDefinition($@"
{ItemSelect}
WHERE i.CommitteeUser = @User
  AND i.Status IN (N'ToCommittee', N'WithCommittee', N'ArchiveRefused', N'ToArchive')
ORDER BY b.EmployeeName, b.BundleNumber, i.SaleDate, i.ArchiveId",
            new { User = user }, commandTimeout: 60, cancellationToken: ct));
        return rows.Select(MapItem).ToList();
    }

    public Task<int> CommitteeReceiveAsync(string committeeUser, IReadOnlyList<int> archiveIds, bool accept, string? note, CancellationToken ct)
    {
        var cleanNote = accept ? null : RequireNote(note);
        return CommitteeMoveAsync(committeeUser, archiveIds, accept
            ? @"
UPDATE dbo.SalesArchiveItems
SET Status = N'WithCommittee', RefusalNote = NULL, CommitteeAt = SYSUTCDATETIME()
WHERE ArchiveId IN @Ids AND CommitteeUser = @User AND Status = N'ToCommittee'"
            : @"
UPDATE dbo.SalesArchiveItems
SET Status = N'Refused', RefusalNote = @Note, CommitteeAt = SYSUTCDATETIME()
WHERE ArchiveId IN @Ids AND CommitteeUser = @User AND Status = N'ToCommittee'", cleanNote, ct);
    }

    public Task<int> CommitteeSendToArchiveAsync(string committeeUser, IReadOnlyList<int> archiveIds, CancellationToken ct) =>
        CommitteeMoveAsync(committeeUser, archiveIds, @"
UPDATE dbo.SalesArchiveItems
SET Status = N'ToArchive', RefusalNote = NULL, CommitteeAt = SYSUTCDATETIME()
WHERE ArchiveId IN @Ids AND CommitteeUser = @User AND Status IN (N'WithCommittee', N'ArchiveRefused')", null, ct);

    public Task<int> CommitteeReturnToAgentAsync(string committeeUser, IReadOnlyList<int> archiveIds, string? note, CancellationToken ct) =>
        CommitteeMoveAsync(committeeUser, archiveIds, @"
UPDATE dbo.SalesArchiveItems
SET Status = N'Refused', RefusalNote = @Note, CommitteeAt = SYSUTCDATETIME()
WHERE ArchiveId IN @Ids AND CommitteeUser = @User AND Status IN (N'WithCommittee', N'ArchiveRefused')", RequireNote(note), ct);

    private async Task<int> CommitteeMoveAsync(string committeeUser, IReadOnlyList<int> archiveIds, string sql, string? note, CancellationToken ct)
    {
        var ids = CleanIds(archiveIds);
        var user = CleanActor(committeeUser);
        await using var connection = await OpenAsync(ct);
        await EnsureSchemaAsync(connection, ct);
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(ct);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            sql, new { Ids = ids, User = user, Note = note }, tx, cancellationToken: ct));
        await EnsureAllAsync(tx, affected, ids.Length, ct);
        await tx.CommitAsync(ct);
        return affected;
    }

    private static int[] CleanIds(IReadOnlyList<int>? archiveIds)
    {
        var ids = (archiveIds ?? []).Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0)
        {
            throw new SalesArchiveException(StatusCodes.Status400BadRequest, "حدّد الأسماء أولاً.");
        }

        if (ids.Length > 2000)
        {
            throw new SalesArchiveException(StatusCodes.Status400BadRequest, "عدد الأسماء كبير جداً.");
        }

        return ids;
    }

    private static string RequireNote(string? note)
    {
        var text = (note ?? string.Empty).Trim();
        if (text.Length < 2)
        {
            throw new SalesArchiveException(StatusCodes.Status400BadRequest, "اكتب سبب الرفض.");
        }

        return text.Length <= 1000 ? text : text[..1000];
    }

    private static string CleanActor(string? actor) =>
        SalesArchiveRules.CleanPersonName(actor)
        ?? throw new SalesArchiveException(StatusCodes.Status401Unauthorized, "تعذر تحديد المستخدم.");

    private static async Task EnsureAllAsync(SqlTransaction tx, int affected, int expected, CancellationToken ct)
    {
        if (affected == expected)
        {
            return;
        }

        await tx.RollbackAsync(ct);
        throw new SalesArchiveException(
            StatusCodes.Status409Conflict,
            "بعض الأسماء المحددة تغيّرت حالتها أو ليست عندك. حدّث الصفحة وأعد المحاولة.");
    }

    public Task<(string ContentType, byte[] Bytes)?> ReadPhotoForAccountantAsync(int bundleId, CancellationToken ct) =>
        ReadPhotoAsync(bundleId, null, null, ct);

    public Task<(string ContentType, byte[] Bytes)?> ReadPhotoForAccountantAsync(int bundleId, int photoId, CancellationToken ct) =>
        ReadPhotoAsync(bundleId, null, photoId, ct);

    private async Task<SalesArchiveBundleDto?> GetBundleAsync(int bundleId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await EnsureSchemaAsync(connection, ct);
        var row = await LoadBundleRowAsync(connection, bundleId, ct);
        if (row == null || row.ItemCount <= 0)
        {
            return null;
        }

        var dto = MapBundle(row, SalesArchiveRules.IraqToday(_clock.UtcNow), includeItems: false);
        dto.Items = await LoadItemsAsync(connection, bundleId, ct);
        dto.PhotoIds = await LoadPhotoIdsAsync(connection, bundleId, ct);
        dto.PhotoCount = dto.PhotoIds.Count;
        dto.HasPhoto = dto.HasPhoto || dto.PhotoCount > 0;
        return dto;
    }

    private async Task<List<SalesArchiveBundleDto>> LoadBundlesAsync(SqlConnection connection, int? employeeId, CancellationToken ct)
    {
        var rows = await connection.QueryAsync<BundleRow>(new CommandDefinition(
            BundleSql,
            new { EmployeeId = employeeId, BundleId = (int?)null },
            commandTimeout: 60,
            cancellationToken: ct));
        var today = SalesArchiveRules.IraqToday(_clock.UtcNow);
        return rows
            .Where(r => r.ItemCount > 0)
            .Select(r => MapBundle(r, today, includeItems: false))
            .OrderByDescending(r => r.PeriodStart)
            .ThenByDescending(r => r.BundleNumber)
            .ToList();
    }

    private async Task<BundleRow?> LoadBundleRowAsync(SqlConnection connection, int bundleId, CancellationToken ct)
    {
        return await connection.QueryFirstOrDefaultAsync<BundleRow>(new CommandDefinition(
            BundleSql,
            new { EmployeeId = (int?)null, BundleId = (int?)bundleId },
            cancellationToken: ct));
    }

    private static async Task<List<SalesArchiveItemDto>> LoadItemsAsync(SqlConnection connection, int bundleId, CancellationToken ct)
    {
        var rows = await connection.QueryAsync<ItemRow>(new CommandDefinition($@"
{ItemSelect}
WHERE i.BundleId = @BundleId
ORDER BY i.SaleDate, i.ArchiveId",
            new { BundleId = bundleId }, cancellationToken: ct));
        return rows.Select(MapItem).ToList();
    }

    private const string ItemSelect = @"
SELECT i.ArchiveId, i.SaleId, i.CustomerName, i.SaleDate, i.SalePrice, i.ItemName, i.ListName, i.Status,
       i.RefusalNote, i.CommitteeUser, i.BundleId, b.BundleNumber, b.EmployeeName
FROM dbo.SalesArchiveItems i
INNER JOIN dbo.SalesArchiveBundles b ON b.BundleId = i.BundleId";

    private static SalesArchiveItemDto MapItem(ItemRow r) => new()
    {
        ArchiveId = r.ArchiveId,
        SaleId = r.SaleId,
        CustomerName = r.CustomerName ?? string.Empty,
        SaleDate = DateOnly.FromDateTime(r.SaleDate).ToString("yyyy-MM-dd"),
        SalePrice = r.SalePrice,
        ItemName = string.IsNullOrWhiteSpace(r.ItemName) ? "—" : r.ItemName,
        ListName = string.IsNullOrWhiteSpace(r.ListName) ? "بدون قائمة" : r.ListName,
        Status = r.Status ?? SalesArchiveRules.ItemPending,
        RefusalNote = r.RefusalNote,
        CommitteeUser = r.CommitteeUser,
        BundleId = r.BundleId,
        BundleNumber = r.BundleNumber,
        EmployeeName = r.EmployeeName ?? string.Empty
    };

    private async Task BackfillAsync(SqlConnection connection, int? employeeId, CancellationToken ct)
    {
        var branchSaleGone = await PurgeDeletedSalesAsync(connection, ct);
        for (var batch = 0; batch < 30; batch++)
        {
            var inserted = await BackfillBatchAsync(connection, employeeId, branchSaleGone, ct);
            if (inserted == 0)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Drops archive papers whose sale no longer exists: the sales-app draft is gone or no longer
    /// Completed (voided), or its posted branch sale was deleted in the branch system.
    /// Runs on every board/list load, so it also clears sales deleted before this rule existed.
    /// Returns the "branch sale gone" predicate (over draft alias d) so the backfill skips the same
    /// sales, or null when this branch database cannot be checked.
    /// </summary>
    private static async Task<string?> PurgeDeletedSalesAsync(SqlConnection connection, CancellationToken ct)
    {
        var caps = await connection.QueryFirstAsync<BranchSaleCaps>(new CommandDefinition(BranchSaleCapsSql, cancellationToken: ct));
        if (!caps.HasArchive)
        {
            return null;
        }

        var gone = BranchSaleGonePredicate(
            caps.HasCustomersSales && caps.HasCustomerSaleId,
            // On hold by request (2026-11-17): the CustomerId rule would drop 13 more papers on live.
            ByCustomerIdRuleEnabled && caps.HasCustomersSales && caps.HasCustomerSaleId && caps.HasPostingStatus && caps.HasCustomerId,
            caps.HasCustomersSales && caps.HasCustomerSaleId && caps.HasPostingStatus && caps.HasCustomerId && caps.HasCustomerMatchColumns);
        var sql = @"
DELETE i
FROM dbo.SalesArchiveItems i
LEFT JOIN dbo.SalesDrafts d ON d.SaleId = i.SaleId
WHERE d.SaleId IS NULL OR d.Status <> N'Completed'" + (gone == null ? ";" : $@"
   OR ({gone});");
        await connection.ExecuteAsync(new CommandDefinition(sql, commandTimeout: 60, cancellationToken: ct));
        return gone;
    }

    /// <summary>Rule 2 of <see cref="BranchSaleGonePredicate"/> (posted draft with only a CustomerId). Off until approved.</summary>
    internal const bool ByCustomerIdRuleEnabled = false;

    /// <summary>Seconds between the draft's completion and its branch customer row being created.</summary>
    internal const int CustomerMatchWindowSeconds = 120;

    /// <summary>
    /// SQL condition (draft alias d) that is true when a completed draft's branch sale was deleted:
    /// 1) linked CustomerSaleId no longer exists; 2) posted, no CustomerSaleId, linked CustomerId has
    /// no sales left; 3) posted, unlinked: exactly one branch customer matches by phone, salesperson
    /// and creation time (within <see cref="CustomerMatchWindowSeconds"/>) and that customer has no
    /// sales left. No match or several matches → kept.
    /// </summary>
    internal static string? BranchSaleGonePredicate(bool bySaleId, bool byCustomerId, bool byMatch)
    {
        var parts = new List<string>();
        if (bySaleId)
        {
            parts.Add(@"(d.CustomerSaleId IS NOT NULL
        AND NOT EXISTS (SELECT 1 FROM dbo.CustomersSales gs WHERE gs.CustomerSaleID = d.CustomerSaleId))");
        }

        if (byCustomerId)
        {
            parts.Add(@"(d.CustomerSaleId IS NULL AND ISNULL(d.PostingStatus, N'') = N'Posted' AND d.CustomerId IS NOT NULL
        AND NOT EXISTS (SELECT 1 FROM dbo.CustomersSales gs WHERE gs.CustomerID = d.CustomerId))");
        }

        if (byMatch)
        {
            const string match = @"gc.PhoneNumber = d.Phone AND gc.SaleName = LEFT(d.UserName, 100)
            AND gc.CreatedDate BETWEEN DATEADD(SECOND, -{0}, d.CompletedAt) AND DATEADD(SECOND, {0}, d.CompletedAt)";
            var on = string.Format(System.Globalization.CultureInfo.InvariantCulture, match, CustomerMatchWindowSeconds);
            parts.Add($@"(d.CustomerSaleId IS NULL AND ISNULL(d.PostingStatus, N'') = N'Posted' AND d.CustomerId IS NULL
        AND d.Phone IS NOT NULL AND LTRIM(RTRIM(d.Phone)) <> N''
        AND (SELECT COUNT(1) FROM dbo.Customers gc WHERE {on}) = 1
        AND NOT EXISTS (SELECT 1 FROM dbo.Customers gc INNER JOIN dbo.CustomersSales gs ON gs.CustomerID = gc.CustomerID WHERE {on}))");
        }

        return parts.Count == 0 ? null : string.Join("\n     OR ", parts);
    }

    private sealed class BranchSaleCaps
    {
        public bool HasArchive { get; set; }
        public bool HasCustomersSales { get; set; }
        public bool HasCustomerSaleId { get; set; }
        public bool HasCustomerId { get; set; }
        public bool HasPostingStatus { get; set; }
        public bool HasCustomerMatchColumns { get; set; }
    }

    private const string BranchSaleCapsSql = @"
IF OBJECT_ID(N'dbo.SalesDrafts', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.SalesDrafts', N'CustomerSaleId') IS NULL
    ALTER TABLE dbo.SalesDrafts ADD CustomerSaleId INT NULL;
SELECT
    CAST(CASE WHEN OBJECT_ID(N'dbo.SalesArchiveItems', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.SalesDrafts', N'U') IS NOT NULL THEN 1 ELSE 0 END AS bit) AS HasArchive,
    CAST(CASE WHEN OBJECT_ID(N'dbo.CustomersSales', N'U') IS NOT NULL THEN 1 ELSE 0 END AS bit) AS HasCustomersSales,
    CAST(CASE WHEN COL_LENGTH(N'dbo.SalesDrafts', N'CustomerSaleId') IS NOT NULL THEN 1 ELSE 0 END AS bit) AS HasCustomerSaleId,
    CAST(CASE WHEN COL_LENGTH(N'dbo.SalesDrafts', N'CustomerId') IS NOT NULL THEN 1 ELSE 0 END AS bit) AS HasCustomerId,
    CAST(CASE WHEN COL_LENGTH(N'dbo.SalesDrafts', N'PostingStatus') IS NOT NULL THEN 1 ELSE 0 END AS bit) AS HasPostingStatus,
    CAST(CASE WHEN COL_LENGTH(N'dbo.SalesDrafts', N'Phone') IS NOT NULL
               AND COL_LENGTH(N'dbo.SalesDrafts', N'UserName') IS NOT NULL
               AND COL_LENGTH(N'dbo.Customers', N'PhoneNumber') IS NOT NULL
               AND COL_LENGTH(N'dbo.Customers', N'SaleName') IS NOT NULL
               AND COL_LENGTH(N'dbo.Customers', N'CreatedDate') IS NOT NULL THEN 1 ELSE 0 END AS bit) AS HasCustomerMatchColumns;";

    private async Task<int> BackfillBatchAsync(SqlConnection connection, int? employeeId, string? branchSaleGone, CancellationToken ct)
    {
        var missing = (await connection.QueryAsync<MissingSaleRow>(new CommandDefinition($@"
SELECT TOP (400)
       d.SaleId, d.EmployeeId, d.UserName AS EmployeeName, d.FullName,
       d.FinalSalePrice, d.CompletedAt, d.CustomerListId
FROM dbo.SalesDrafts d
WHERE d.Status = N'Completed'
  AND d.CompletedAt IS NOT NULL
  AND (@EmployeeId IS NULL OR d.EmployeeId = @EmployeeId)
  AND NOT EXISTS (SELECT 1 FROM dbo.SalesArchiveItems a WHERE a.SaleId = d.SaleId){(branchSaleGone == null ? string.Empty : $"\n  AND NOT ({branchSaleGone})")}
ORDER BY d.CompletedAt, d.SaleId",
            new { EmployeeId = employeeId }, commandTimeout: 60, cancellationToken: ct))).ToList();
        if (missing.Count == 0)
        {
            return 0;
        }

        var ids = missing.Select(m => m.SaleId).ToArray();
        var itemRows = (await connection.QueryAsync<SaleLineRow>(new CommandDefinition(@"
SELECT SaleId, ProductName, Quantity
FROM dbo.SalesDraftItems
WHERE SaleId IN @Ids",
            new { Ids = ids }, commandTimeout: 60, cancellationToken: ct))).ToList();
        var bySale = itemRows.GroupBy(i => i.SaleId).ToDictionary(g => g.Key, g => g.ToList());
        var listIds = missing.Where(m => m.CustomerListId is > 0).Select(m => m.CustomerListId!.Value).Distinct().ToArray();
        var lists = await LookupListsAsync(connection, listIds, ct);
        var count = 0;
        foreach (var sale in missing)
        {
            bySale.TryGetValue(sale.SaleId, out var lines);
            var label = SalesArchiveRules.ItemLabel((lines ?? []).Select(l => (l.ProductName, l.Quantity)));
            string? listName = null;
            if (sale.CustomerListId is > 0)
            {
                lists.TryGetValue(sale.CustomerListId.Value, out listName);
            }

            var inserted = await InsertSnapshotAsync(connection, new Snapshot(
                sale.SaleId,
                sale.EmployeeId,
                sale.EmployeeName,
                sale.FullName,
                sale.FinalSalePrice,
                sale.CompletedAt,
                sale.CustomerListId,
                label,
                listName), ct);
            if (inserted)
            {
                count++;
            }
        }

        return count;
    }

    private async Task<bool> InsertSnapshotAsync(SqlConnection connection, Snapshot sale, CancellationToken ct)
    {
        if (sale.SaleId <= 0 || sale.EmployeeId <= 0)
        {
            return false;
        }

        var saleDate = SalesArchiveRules.SaleCalendarDate(sale.CompletedAt);
        var period = SalesArchiveRules.PeriodFor(saleDate);
        var customer = string.IsNullOrWhiteSpace(sale.CustomerName) ? "—" : sale.CustomerName.Trim();
        if (customer.Length > 255)
        {
            customer = customer[..255];
        }

        var employeeName = (sale.EmployeeName ?? string.Empty).Trim();
        if (employeeName.Length > 200)
        {
            employeeName = employeeName[..200];
        }

        var listName = string.IsNullOrWhiteSpace(sale.ListName) ? "بدون قائمة" : sale.ListName.Trim();
        if (listName.Length > 255)
        {
            listName = listName[..255];
        }

        var price = sale.SalePrice < 0 ? 0 : decimal.Round(sale.SalePrice, 0, MidpointRounding.AwayFromZero);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    "SELECT COUNT(1) FROM dbo.SalesArchiveItems WITH (UPDLOCK, HOLDLOCK) WHERE SaleId = @SaleId",
                    new { sale.SaleId }, tx, cancellationToken: ct));
                if (exists > 0)
                {
                    await tx.CommitAsync(ct);
                    return false;
                }

                var bundleId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(@"
SELECT BundleId
FROM dbo.SalesArchiveBundles WITH (UPDLOCK, HOLDLOCK)
WHERE EmployeeId = @EmployeeId AND PeriodStart = @PeriodStart",
                    new { sale.EmployeeId, PeriodStart = period.Start.ToDateTime(TimeOnly.MinValue) }, tx, cancellationToken: ct));
                if (bundleId is not > 0)
                {
                    var next = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT ISNULL(MAX(BundleNumber), 0) + 1
FROM dbo.SalesArchiveBundles WITH (UPDLOCK, HOLDLOCK)
WHERE EmployeeId = @EmployeeId",
                        new { sale.EmployeeId }, tx, cancellationToken: ct));
                    bundleId = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
INSERT INTO dbo.SalesArchiveBundles
    (EmployeeId, EmployeeName, BundleNumber, PeriodStart, PeriodEnd)
VALUES
    (@EmployeeId, @EmployeeName, @BundleNumber, @PeriodStart, @PeriodEnd);
SELECT CAST(SCOPE_IDENTITY() AS INT);",
                        new
                        {
                            sale.EmployeeId,
                            EmployeeName = employeeName,
                            BundleNumber = next,
                            PeriodStart = period.Start.ToDateTime(TimeOnly.MinValue),
                            PeriodEnd = period.End.ToDateTime(TimeOnly.MinValue)
                        }, tx, cancellationToken: ct));
                }
                else if (employeeName.Length > 0)
                {
                    await connection.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.SalesArchiveBundles
SET EmployeeName = @EmployeeName
WHERE BundleId = @BundleId AND (EmployeeName IS NULL OR LTRIM(RTRIM(EmployeeName)) = N'')",
                        new { BundleId = bundleId, EmployeeName = employeeName }, tx, cancellationToken: ct));
                }

                await connection.ExecuteAsync(new CommandDefinition(@"
INSERT INTO dbo.SalesArchiveItems
    (SaleId, BundleId, EmployeeId, CustomerName, SaleDate, SalePrice, ItemName, ListId, ListName, Status, CreatedAt)
VALUES
    (@SaleId, @BundleId, @EmployeeId, @CustomerName, @SaleDate, @SalePrice, @ItemName, @ListId, @ListName, N'Pending', SYSUTCDATETIME())",
                    new
                    {
                        sale.SaleId,
                        BundleId = bundleId,
                        sale.EmployeeId,
                        CustomerName = customer,
                        SaleDate = saleDate.ToDateTime(TimeOnly.MinValue),
                        SalePrice = price,
                        ItemName = sale.ItemName,
                        ListId = sale.ListId is > 0 ? sale.ListId : null,
                        ListName = listName
                    }, tx, cancellationToken: ct));
                await tx.CommitAsync(ct);
                return true;
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627 && attempt == 0)
            {
                // The using-disposal rolls the doomed transaction back, then the loop retries.
            }
        }

        return false;
    }

    private static async Task<string?> LookupListNameAsync(SqlConnection connection, int? listId, CancellationToken ct)
    {
        if (listId is not > 0)
        {
            return null;
        }

        try
        {
            return await connection.QueryFirstOrDefaultAsync<string?>(new CommandDefinition(
                "SELECT TOP 1 DelegateName FROM dbo.Delegates WHERE DelegateID = @Id",
                new { Id = listId }, cancellationToken: ct));
        }
        catch
        {
            return null;
        }
    }

    private static async Task<Dictionary<int, string>> LookupListsAsync(SqlConnection connection, int[] ids, CancellationToken ct)
    {
        if (ids.Length == 0)
        {
            return new Dictionary<int, string>();
        }

        try
        {
            var rows = await connection.QueryAsync<(int Id, string? Name)>(new CommandDefinition(
                "SELECT DelegateID AS Id, DelegateName AS Name FROM dbo.Delegates WHERE DelegateID IN @Ids",
                new { Ids = ids }, cancellationToken: ct));
            return rows
                .GroupBy(r => r.Id)
                .ToDictionary(g => g.Key, g => g.First().Name ?? string.Empty);
        }
        catch
        {
            return new Dictionary<int, string>();
        }
    }

    private async Task<List<string>> SavePhotosAsync(int bundleId, IReadOnlyList<IFormFile> files, CancellationToken ct)
    {
        var folder = Path.Combine(_env.ContentRootPath, "App_Data", "sales-archive", bundleId.ToString());
        Directory.CreateDirectory(folder);
        foreach (var old in Directory.EnumerateFiles(folder))
        {
            File.Delete(old);
        }

        var paths = new List<string>(files.Count);
        for (var i = 0; i < files.Count; i++)
        {
            var ext = NormalizeImageExtension(files[i]);
            var fileName = $"delivery-{i + 1}{ext}";
            var path = Path.Combine(folder, fileName);
            await using (var stream = File.Create(path))
            {
                await files[i].CopyToAsync(stream, ct);
            }

            paths.Add($"sales-archive/{bundleId}/{fileName}");
        }

        return paths;
    }

    private static async Task<List<int>> LoadPhotoIdsAsync(SqlConnection connection, int bundleId, CancellationToken ct)
    {
        var ids = await connection.QueryAsync<int>(new CommandDefinition(@"
SELECT PhotoId
FROM dbo.SalesArchivePhotos
WHERE BundleId = @BundleId
ORDER BY SortOrder, PhotoId",
            new { BundleId = bundleId }, cancellationToken: ct));
        return ids.ToList();
    }

    private async Task<(string ContentType, byte[] Bytes)?> ReadPhotoAsync(int bundleId, int? employeeId, int? photoId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await EnsureSchemaAsync(connection, ct);
        var row = await connection.QueryFirstOrDefaultAsync<PhotoRow>(new CommandDefinition(@"
SELECT b.EmployeeId,
       CASE
           WHEN @PhotoId IS NULL THEN COALESCE(p.RelativePath, b.PhotoPath)
           ELSE p.RelativePath
       END AS PhotoPath
FROM dbo.SalesArchiveBundles b
LEFT JOIN dbo.SalesArchivePhotos p
    ON p.BundleId = b.BundleId
   AND (@PhotoId IS NULL OR p.PhotoId = @PhotoId)
WHERE b.BundleId = @BundleId
ORDER BY p.SortOrder, p.PhotoId",
            new { BundleId = bundleId, PhotoId = photoId }, cancellationToken: ct));
        if (row == null || (employeeId is > 0 && row.EmployeeId != employeeId))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(row.PhotoPath) || row.PhotoPath.Contains("..", StringComparison.Ordinal))
        {
            return null;
        }

        var path = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "App_Data", row.PhotoPath.Replace('/', Path.DirectorySeparatorChar)));
        var root = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "App_Data", "sales-archive"));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
        {
            return null;
        }

        var bytes = await File.ReadAllBytesAsync(path, ct);
        return (ContentTypeFor(path), bytes);
    }

    private static string ContentTypeFor(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".heic" => "image/heic",
            _ => "image/jpeg"
        };
    }

    private static string NormalizeImageExtension(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".heic";
        if (!allowed)
        {
            var type = (file.ContentType ?? string.Empty).ToLowerInvariant();
            ext = type switch
            {
                "image/png" => ".png",
                "image/webp" => ".webp",
                "image/heic" => ".heic",
                _ => ".jpg"
            };
        }

        if (ext == ".jpeg")
        {
            ext = ".jpg";
        }

        return ext;
    }

    private static SalesArchiveBundleDto MapBundle(BundleRow row, DateOnly today, bool includeItems)
    {
        var start = DateOnly.FromDateTime(row.PeriodStart);
        var end = DateOnly.FromDateTime(row.PeriodEnd);
        var phase = PhaseOf(row, today);
        return new SalesArchiveBundleDto
        {
            BundleId = row.BundleId,
            BundleNumber = row.BundleNumber,
            EmployeeId = row.EmployeeId,
            EmployeeName = row.EmployeeName ?? string.Empty,
            PeriodStart = start.ToString("yyyy-MM-dd"),
            PeriodEnd = end.ToString("yyyy-MM-dd"),
            Phase = phase,
            Stage = SalesArchiveRules.Stage(phase, row.AtCommitteeCount, row.ToArchiveCount),
            CanSend = SalesArchiveRules.CanSend(phase),
            RecipientName = row.RecipientName,
            AtCommitteeCount = row.AtCommitteeCount,
            ToArchiveCount = row.ToArchiveCount,
            IntermediateName = row.IntermediateName,
            IntermediatePhone = row.IntermediatePhone,
            Note = row.Note,
            SentAt = row.SentAt,
            HasPhoto = !string.IsNullOrWhiteSpace(row.PhotoPath),
            ItemCount = row.ItemCount,
            ApprovedCount = row.ApprovedCount,
            RefusedCount = row.RefusedCount,
            PendingCount = row.PendingCount,
            Items = includeItems ? [] : []
        };
    }

    private static string PhaseOf(BundleRow row, DateOnly today) =>
        SalesArchiveRules.Phase(
            DateOnly.FromDateTime(row.PeriodEnd),
            today,
            row.ItemCount,
            row.ApprovedCount,
            row.RefusedCount,
            row.PendingCount);

    private async Task<SqlConnection> OpenAsync(CancellationToken ct)
    {
        var cs = _guard.GetSalesConnectionString()
                 ?? throw new SalesArchiveException(StatusCodes.Status503ServiceUnavailable, "أرشيف المبيعات غير متاح على هذا الفرع.");
        var connection = new SqlConnection(cs);
        await connection.OpenAsync(ct);
        return connection;
    }

    private static async Task EnsureSchemaAsync(SqlConnection connection, CancellationToken ct)
    {
        foreach (var sql in SchemaCommands)
        {
            await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: ct));
        }
    }

    private const string BundleSql = @"
SELECT
    b.BundleId,
    b.EmployeeId,
    b.EmployeeName,
    b.BundleNumber,
    b.PeriodStart,
    b.PeriodEnd,
    b.IntermediateName,
    b.IntermediatePhone,
    b.Note,
    b.SentAt,
    b.PhotoPath,
    b.RecipientName,
    CAST(COUNT(i.ArchiveId) AS INT) AS ItemCount,
    CAST(SUM(CASE WHEN i.Status IN (N'ToCommittee', N'WithCommittee', N'ArchiveRefused') THEN 1 ELSE 0 END) AS INT) AS AtCommitteeCount,
    CAST(SUM(CASE WHEN i.Status = N'ToArchive' THEN 1 ELSE 0 END) AS INT) AS ToArchiveCount,
    CAST(SUM(CASE WHEN i.Status = N'Approved' THEN 1 ELSE 0 END) AS INT) AS ApprovedCount,
    CAST(SUM(CASE WHEN i.Status = N'Refused' THEN 1 ELSE 0 END) AS INT) AS RefusedCount,
    CAST(SUM(CASE WHEN i.Status = N'Pending' THEN 1 ELSE 0 END) AS INT) AS PendingCount
FROM dbo.SalesArchiveBundles b
INNER JOIN dbo.SalesArchiveItems i ON i.BundleId = b.BundleId
WHERE (@EmployeeId IS NULL OR b.EmployeeId = @EmployeeId)
  AND (@BundleId IS NULL OR b.BundleId = @BundleId)
GROUP BY
    b.BundleId, b.EmployeeId, b.EmployeeName, b.BundleNumber,
    b.PeriodStart, b.PeriodEnd, b.IntermediateName, b.IntermediatePhone,
    b.Note, b.SentAt, b.PhotoPath, b.RecipientName";

    private static readonly string[] SchemaCommands =
    [
        @"
IF OBJECT_ID(N'dbo.SalesArchiveBundles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesArchiveBundles (
        BundleId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SalesArchiveBundles PRIMARY KEY,
        EmployeeId INT NOT NULL,
        EmployeeName NVARCHAR(200) NULL,
        BundleNumber INT NOT NULL,
        PeriodStart DATE NOT NULL,
        PeriodEnd DATE NOT NULL,
        IntermediateName NVARCHAR(200) NULL,
        IntermediatePhone NVARCHAR(20) NULL,
        Note NVARCHAR(1000) NULL,
        PhotoPath NVARCHAR(500) NULL,
        SentAt DATETIME NULL,
        ReviewedAt DATETIME NULL
    );
END",
        @"
IF OBJECT_ID(N'dbo.SalesArchiveItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesArchiveItems (
        ArchiveId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SalesArchiveItems PRIMARY KEY,
        SaleId INT NOT NULL,
        BundleId INT NOT NULL,
        EmployeeId INT NOT NULL,
        CustomerName NVARCHAR(255) NOT NULL,
        SaleDate DATE NOT NULL,
        SalePrice DECIMAL(18, 0) NOT NULL,
        ItemName NVARCHAR(500) NOT NULL,
        ListId INT NULL,
        ListName NVARCHAR(255) NULL,
        Status NVARCHAR(20) NOT NULL,
        RefusalNote NVARCHAR(1000) NULL,
        CreatedAt DATETIME NOT NULL CONSTRAINT DF_SalesArchiveItems_CreatedAt DEFAULT (GETDATE()),
        CONSTRAINT FK_SalesArchiveItems_Bundle FOREIGN KEY (BundleId) REFERENCES dbo.SalesArchiveBundles (BundleId)
    );
END",
        @"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_SalesArchiveBundles_EmpPeriod' AND object_id = OBJECT_ID(N'dbo.SalesArchiveBundles'))
    CREATE UNIQUE INDEX UX_SalesArchiveBundles_EmpPeriod ON dbo.SalesArchiveBundles (EmployeeId, PeriodStart);",
        @"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_SalesArchiveBundles_EmpNumber' AND object_id = OBJECT_ID(N'dbo.SalesArchiveBundles'))
    CREATE UNIQUE INDEX UX_SalesArchiveBundles_EmpNumber ON dbo.SalesArchiveBundles (EmployeeId, BundleNumber);",
        @"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_SalesArchiveItems_Sale' AND object_id = OBJECT_ID(N'dbo.SalesArchiveItems'))
    CREATE UNIQUE INDEX UX_SalesArchiveItems_Sale ON dbo.SalesArchiveItems (SaleId);",
        @"
IF OBJECT_ID(N'dbo.SalesArchivePhotos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesArchivePhotos (
        PhotoId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SalesArchivePhotos PRIMARY KEY,
        BundleId INT NOT NULL,
        RelativePath NVARCHAR(500) NOT NULL,
        SortOrder INT NOT NULL,
        CreatedAt DATETIME NOT NULL CONSTRAINT DF_SalesArchivePhotos_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_SalesArchivePhotos_Bundle FOREIGN KEY (BundleId) REFERENCES dbo.SalesArchiveBundles (BundleId)
    );
END",
        @"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SalesArchivePhotos_Bundle' AND object_id = OBJECT_ID(N'dbo.SalesArchivePhotos'))
    CREATE INDEX IX_SalesArchivePhotos_Bundle ON dbo.SalesArchivePhotos (BundleId, SortOrder);",
        @"
INSERT INTO dbo.SalesArchivePhotos (BundleId, RelativePath, SortOrder)
SELECT b.BundleId, b.PhotoPath, 1
FROM dbo.SalesArchiveBundles b
WHERE b.PhotoPath IS NOT NULL
  AND LTRIM(RTRIM(b.PhotoPath)) <> N''
  AND NOT EXISTS (SELECT 1 FROM dbo.SalesArchivePhotos p WHERE p.BundleId = b.BundleId);",
        @"
IF COL_LENGTH(N'dbo.SalesArchiveBundles', N'RecipientName') IS NULL
    ALTER TABLE dbo.SalesArchiveBundles ADD RecipientName NVARCHAR(200) NULL;",
        // Committee/archive-manager custody. On first run, papers already sent to the main accountant
        // (Pending in a sent bundle) move straight to the archive manager's inbox.
        @"
IF COL_LENGTH(N'dbo.SalesArchiveItems', N'CommitteeUser') IS NULL
BEGIN
    ALTER TABLE dbo.SalesArchiveItems ADD
        CommitteeUser NVARCHAR(200) NULL,
        CommitteeAt DATETIME NULL,
        ArchiveBy NVARCHAR(200) NULL,
        ArchiveAt DATETIME NULL;
    UPDATE i SET Status = N'ToArchive'
    FROM dbo.SalesArchiveItems i
    INNER JOIN dbo.SalesArchiveBundles b ON b.BundleId = i.BundleId
    WHERE i.Status = N'Pending' AND b.SentAt IS NOT NULL;
END",
        @"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SalesArchiveItems_Committee' AND object_id = OBJECT_ID(N'dbo.SalesArchiveItems'))
    CREATE INDEX IX_SalesArchiveItems_Committee ON dbo.SalesArchiveItems (CommitteeUser, Status);"
    ];

    private sealed class BundleRow
    {
        public int BundleId { get; set; }
        public int EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public int BundleNumber { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public string? IntermediateName { get; set; }
        public string? IntermediatePhone { get; set; }
        public string? Note { get; set; }
        public DateTime? SentAt { get; set; }
        public string? PhotoPath { get; set; }
        public string? RecipientName { get; set; }
        public int ItemCount { get; set; }
        public int AtCommitteeCount { get; set; }
        public int ToArchiveCount { get; set; }
        public int ApprovedCount { get; set; }
        public int RefusedCount { get; set; }
        public int PendingCount { get; set; }
    }

    private sealed class ItemRow
    {
        public int ArchiveId { get; set; }
        public int SaleId { get; set; }
        public string? CustomerName { get; set; }
        public DateTime SaleDate { get; set; }
        public decimal SalePrice { get; set; }
        public string? ItemName { get; set; }
        public string? ListName { get; set; }
        public string? Status { get; set; }
        public string? RefusalNote { get; set; }
        public string? CommitteeUser { get; set; }
        public int BundleId { get; set; }
        public int BundleNumber { get; set; }
        public string? EmployeeName { get; set; }
    }

    private sealed class ItemStatusRow
    {
        public int ArchiveId { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    private sealed class MissingSaleRow
    {
        public int SaleId { get; set; }
        public int EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public string? FullName { get; set; }
        public decimal FinalSalePrice { get; set; }
        public DateTime CompletedAt { get; set; }
        public int? CustomerListId { get; set; }
    }

    private sealed class SaleLineRow
    {
        public int SaleId { get; set; }
        public string? ProductName { get; set; }
        public int Quantity { get; set; }
    }

    private sealed class PhotoRow
    {
        public int EmployeeId { get; set; }
        public string? PhotoPath { get; set; }
    }

    private readonly record struct Snapshot(
        int SaleId,
        int EmployeeId,
        string? EmployeeName,
        string? CustomerName,
        decimal SalePrice,
        DateTime CompletedAt,
        int? ListId,
        string ItemName,
        string? ListName);
}

public sealed class SalesArchiveException : Exception
{
    public SalesArchiveException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}
