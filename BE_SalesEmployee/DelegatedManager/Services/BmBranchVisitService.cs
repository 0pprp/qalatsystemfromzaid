using System.Text.Json;
using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.DelegatedManager.Stores;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Sales.Services;
using BE_SalesEmployee.Services;

namespace BE_SalesEmployee.DelegatedManager.Services;

public sealed class BmBranchVisitService
{
    private readonly IBmBranchVisitStore _store;
    private readonly IBranchManagerAppUserStore _users;
    private readonly BmBranchVisitFileStorage _files;
    private readonly DirectorAssignedTaskService _tasks;
    private readonly ISalesManagerBranchAggregator? _aggregator;

    public BmBranchVisitService(
        IBmBranchVisitStore store,
        IBranchManagerAppUserStore users,
        BmBranchVisitFileStorage files,
        DirectorAssignedTaskService tasks,
        ISalesManagerBranchAggregator? aggregator = null)
    {
        _store = store;
        _users = users;
        _files = files;
        _tasks = tasks;
        _aggregator = aggregator;
    }

    public async Task<(bool Ok, string? Error, BmBranchVisit? Visit, IReadOnlyList<object> AssignedTasks)> OpenAsync(
        Guid actorUserId,
        string actorUserName,
        string actorDisplayName,
        string cityValue,
        string? cityName,
        CancellationToken ct)
    {
        await _store.EnsureSchemaAsync(ct);
        await _users.EnsureSchemaAsync(ct);

        var city = (cityValue ?? "").Trim();
        if (city.Length == 0)
            return (false, "المحافظة مطلوبة.", null, []);

        var existing = await _store.GetOpenForUserAsync(actorUserId, ct);
        if (existing != null)
        {
            if (string.Equals(existing.CityValue, city, StringComparison.OrdinalIgnoreCase))
            {
                var tasksSame = await LoadAssignedTasksAsync(actorUserId, city, ct);
                return (true, null, existing, tasksSame);
            }

            return (false, "لديك زيارة مفتوحة لمحافظة أخرى. غادر الفرع أولاً.", null, []);
        }

        if (!BmBranchVisitRules.CanOpen(hasOpenVisitForActor: false))
            return (false, "تعذر فتح الزيارة.", null, []);

        var allUsers = await _users.ListAsync(ct);
        var memberTuples = allUsers.Select(u => (
            u.Id,
            u.DisplayName,
            u.IsActive,
            (IReadOnlyList<string>)u.Cities.Select(c => c.CityValue).ToList()));
        var members = BmBranchVisitRules.SelectMembersForCity(memberTuples, city);

        var now = DateTime.UtcNow;
        var visit = new BmBranchVisit
        {
            Id = Guid.NewGuid(),
            CityValue = city,
            CityName = string.IsNullOrWhiteSpace(cityName) ? null : cityName.Trim(),
            OpenedByUserId = actorUserId,
            OpenedByUserName = actorUserName?.Trim() ?? "",
            OpenedByDisplayName = string.IsNullOrWhiteSpace(actorDisplayName) ? actorUserName?.Trim() ?? "" : actorDisplayName.Trim(),
            ArrivedAtUtc = now,
            Status = BmBranchVisitStatus.Open,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Members = members.Select(m => new BmBranchVisitMember
            {
                UserId = m.UserId,
                DisplayName = m.DisplayName,
            }).ToList(),
        };

        var saved = await _store.InsertAsync(visit, ct);
        var tasks = await LoadAssignedTasksAsync(actorUserId, city, ct);
        return (true, null, saved, tasks);
    }

    public async Task<(bool Ok, string? Error, BmBranchVisit? Visit)> UpsertDelegateRatingsAsync(
        Guid visitId,
        Guid actorUserId,
        string? actorDisplayName,
        IReadOnlyList<(int DelegateId, string DelegateName, int Score)> ratings,
        CancellationToken ct)
    {
        var visit = await _store.GetAsync(visitId, ct);
        if (visit is null)
            return (false, "الزيارة غير موجودة.", null);
        if (!string.Equals(visit.Status, BmBranchVisitStatus.Open, StringComparison.Ordinal))
            return (false, "الزيارة مغلقة.", null);
        if (visit.OpenedByUserId != actorUserId)
            return (false, "فقط فاتح الزيارة يمكنه حفظ التقييمات.", null);

        foreach (var r in ratings)
        {
            if (!BmBranchVisitRules.IsValidDelegateScore(r.Score))
                return (false, "تقييم المندوب يجب أن يكون من 0 إلى 10.", null);
            if (r.DelegateId <= 0)
                return (false, "معرف المندوب غير صالح.", null);

            await _store.UpsertDelegateRatingAsync(new BmBranchVisitDelegateRating
            {
                VisitId = visitId,
                DelegateId = r.DelegateId,
                DelegateName = string.IsNullOrWhiteSpace(r.DelegateName) ? $"#{r.DelegateId}" : r.DelegateName.Trim(),
                Score = r.Score,
                RatedByUserId = actorUserId,
                RatedByDisplayName = actorDisplayName,
                RatedAtUtc = DateTime.UtcNow,
            }, ct);
        }

        var refreshed = await _store.GetAsync(visitId, ct);
        if (refreshed != null)
            await TryMirrorSupervisionRatingsAsync(refreshed, actorDisplayName, ratings, ct);
        return (true, null, refreshed);
    }

    /// <summary>SupervisionMirrorGateway_V1 — push to company ListSupervisionDayRatings for المراقب board.</summary>
    private async Task TryMirrorSupervisionRatingsAsync(
        BmBranchVisit visit,
        string? actorDisplayName,
        IReadOnlyList<(int DelegateId, string DelegateName, int Score)> ratings,
        CancellationToken ct)
    {
        if (_aggregator is null || string.IsNullOrWhiteSpace(visit.CityValue) || ratings.Count == 0)
            return;

        const string mirrorMarker = "SupervisionMirrorGateway_V1";
        try
        {
            var user = new GatewayUser
            {
                UserID = "0",
                UserName = string.IsNullOrWhiteSpace(actorDisplayName) ? "branch-manager" : actorDisplayName.Trim(),
                UserType = SalesRoles.UserTypeSalesManager,
                IsCentral = true,
                CityValue = visit.CityValue,
                CityName = visit.CityName ?? ""
            };
            var payload = JsonSerializer.Serialize(new
            {
                ratings = ratings.Select(r => new
                {
                    listId = r.DelegateId,
                    score = r.Score,
                    note = (string?)null,
                    ratedBy = actorDisplayName
                }).ToList()
            });
            await _aggregator.PostAsync(
                user,
                visit.CityValue,
                "branch-manager/list-supervision-ratings",
                payload,
                ct);
        }
        catch (Exception ex)
        {
            // Non-fatal: gateway ratings remain source of truth until next sync.
            System.Diagnostics.Trace.WriteLine(mirrorMarker + ": " + ex.Message);
        }
    }

    public async Task<(bool Ok, string? Error, BmBranchVisit? Visit)> DepartAsync(
        Guid visitId,
        Guid actorUserId,
        string? notes,
        IReadOnlyList<IFormFile>? files,
        CancellationToken ct,
        IReadOnlyList<int>? requiredDelegateIds = null)
    {
        var visit = await _store.GetAsync(visitId, ct);
        if (visit is null)
            return (false, "الزيارة غير موجودة.", null);
        if (!BmBranchVisitRules.CanDepart(
                isOpen: string.Equals(visit.Status, BmBranchVisitStatus.Open, StringComparison.Ordinal),
                openedByActor: visit.OpenedByUserId == actorUserId))
            return (false, "لا يمكن مغادرة هذه الزيارة.", null);
        if (!BmBranchVisitRules.IsValidDepartNote(notes))
            return (false, $"الملاحظة الطويلة مطلوبة (على الأقل {BmBranchVisitRules.MinDepartNoteLength} حرفاً).", null);

        var required = (requiredDelegateIds ?? Array.Empty<int>()).ToList();
        // If caller did not send ids, still require at least one rating when any exist on visit
        // is insufficient — prefer explicit required set from the app.
        if (required.Count > 0)
        {
            var ratedIds = visit.DelegateRatings.Select(r => r.DelegateId);
            if (!BmBranchVisitRules.AreAllDelegatesRated(required, ratedIds))
                return (false, "يجب تقييم كل المندوبين (0–10) قبل مغادرة الفرع.", null);
        }
        else if (visit.DelegateRatings.Count == 0)
        {
            return (false, "يجب تقييم المندوبين قبل مغادرة الفرع.", null);
        }

        if (files != null)
        {
            foreach (var file in files.Where(f => f != null && f.Length > 0))
            {
                var (ok, error, stored) = await _files.SaveAsync(visitId, file, ct);
                if (!ok || string.IsNullOrWhiteSpace(stored))
                    return (false, error ?? "تعذر رفع المرفق.", null);

                await _store.AddAttachmentAsync(new BmBranchVisitAttachment
                {
                    Id = Guid.NewGuid(),
                    VisitId = visitId,
                    FileName = Path.GetFileName(file.FileName) ?? stored,
                    StoredFileName = stored,
                    ContentType = file.ContentType,
                    SizeBytes = file.Length,
                    UploadedAtUtc = DateTime.UtcNow,
                    UploadedBy = visit.OpenedByDisplayName,
                }, ct);
            }
        }

        visit.Notes = notes!.Trim();
        visit.DepartedAtUtc = DateTime.UtcNow;
        visit.Status = BmBranchVisitStatus.Closed;
        visit.UpdatedAtUtc = DateTime.UtcNow;
        var updated = await _store.UpdateAsync(visit, ct);
        return (true, null, updated);
    }

    public Task<BmBranchVisit?> GetAsync(Guid id, CancellationToken ct) => _store.GetAsync(id, ct);

    public Task<BmBranchVisit?> GetOpenForUserAsync(Guid openedByUserId, CancellationToken ct) =>
        _store.GetOpenForUserAsync(openedByUserId, ct);

    public Task<IReadOnlyList<BmBranchVisit>> ListAsync(
        string? cityValue,
        DateTime? fromUtc,
        DateTime? toUtc,
        int take,
        CancellationToken ct) =>
        _store.ListAsync(cityValue, fromUtc, toUtc, take, ct);

    public async Task<(bool Ok, string? Error, string? FullPath, string? ContentType, string? FileName)> DownloadAttachmentAsync(
        Guid visitId,
        Guid attachmentId,
        CancellationToken ct)
    {
        var att = await _store.GetAttachmentAsync(visitId, attachmentId, ct);
        if (att is null)
            return (false, "المرفق غير موجود.", null, null, null);
        var (ok, full, contentType) = _files.Resolve(visitId, att.StoredFileName);
        if (!ok || string.IsNullOrWhiteSpace(full))
            return (false, "الملف غير موجود على القرص.", null, null, null);
        return (true, null, full, contentType, att.FileName);
    }

    private async Task<IReadOnlyList<object>> LoadAssignedTasksAsync(Guid actorUserId, string cityValue, CancellationToken ct)
    {
        try
        {
            var key = actorUserId.ToString("D");
            var inbox = await _tasks.InboxForBmAsync(key, ct);
            return inbox
                .Where(t =>
                    string.IsNullOrWhiteSpace(t.AssigneeCityValue)
                    || string.Equals(t.AssigneeCityValue, cityValue, StringComparison.OrdinalIgnoreCase))
                .Select(t => (object)new
                {
                    id = t.Id,
                    title = t.Title,
                    description = t.Description,
                    status = t.Status,
                    dueDate = t.DueDate,
                    priority = t.Priority,
                })
                .ToList();
        }
        catch
        {
            return [];
        }
    }
}
