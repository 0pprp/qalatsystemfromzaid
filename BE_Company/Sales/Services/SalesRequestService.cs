using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Filtering;
using BE_Company.Sales.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Services
{
    public sealed class SalesRequestService : ISalesRequestService
    {
        private readonly ISalesRequestRepository _repo;
        private readonly IIraqClock _clock;
        private readonly ISalesManagerReadRepository? _employees;
        private readonly ISalesCascadeVoidService? _cascadeVoid;
        private readonly SalesDevelopmentGuard? _guard;
        private readonly ICrmSalesBoardStatusNotifier? _crmStatus;
        private readonly ILegalFollowupService? _legalFollowups;

        public SalesRequestService(
            ISalesRequestRepository repo,
            IIraqClock clock,
            ISalesManagerReadRepository? employees = null,
            ISalesCascadeVoidService? cascadeVoid = null,
            SalesDevelopmentGuard? guard = null,
            ICrmSalesBoardStatusNotifier? crmStatus = null,
            ILegalFollowupService? legalFollowups = null)
        {
            _repo = repo;
            _clock = clock;
            _employees = employees;
            _cascadeVoid = cascadeVoid;
            _guard = guard;
            _crmStatus = crmStatus;
            _legalFollowups = legalFollowups;
        }

        private Task NotifyCrmIfLinkedAsync(
            SalesRequestDTO row,
            string boardStatus,
            string? rejectReason,
            CancellationToken ct)
        {
            if (_crmStatus == null || row.ExternalCrmOrderUuid is not Guid uuid || uuid == Guid.Empty)
            {
                return Task.CompletedTask;
            }

            if (!SalesRequestSources.IsCrm(row.CustomerSourceType))
            {
                return Task.CompletedTask;
            }

            return _crmStatus.NotifyAsync(
                new CrmSalesBoardStatusNotification
                {
                    CrmOrderUuid = uuid,
                    SalesRequestId = row.Id,
                    CityValue = row.CityValue,
                    BoardStatus = boardStatus,
                    RejectReason = rejectReason
                },
                ct);
        }

        public async Task<SalesRequestDTO> CreateAsync(SalesIdentity actor, SalesRequestCreateDTO request, CancellationToken ct, bool validateIraqPhone = true)
        {
            if (SalesRoles.IsSalesEmployee(actor.UserType)
                || (!SalesRoles.CanCreateSalesRequest(actor.UserType)
                    && !string.Equals(actor.Role, SalesRoles.SalesManager, StringComparison.Ordinal)))
            {
                throw new SalesCompleteException(StatusCodes.Status403Forbidden, "غير مصرح.");
            }

            var name = request.Customer?.FullName?.Trim();
            if (string.IsNullOrWhiteSpace(name) && request.ExistingCustomerId is null or <= 0)
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "اسم الزبون مطلوب.");
            }

            string? phone;
            if (validateIraqPhone)
            {
                SalesPhoneNormalizer.RequireValidIfPresent(request.Customer?.Phone);
                phone = string.IsNullOrWhiteSpace(request.Customer?.Phone)
                    ? request.Customer?.Phone
                    : SalesPhoneNormalizer.ForStorage(request.Customer.Phone);
            }
            else
            {
                phone = string.IsNullOrWhiteSpace(request.Customer?.Phone)
                    ? request.Customer?.Phone
                    : SalesPhoneNormalizer.ForStorage(request.Customer.Phone);
            }

            await _repo.EnsureSchemaAsync(ct);
            var sourceType = SalesRoles.IsFollower(actor.UserType)
                ? SalesRequestSources.Follower
                : request.ExistingCustomerId is > 0 ? "ExistingCustomer" : "NewCustomer";
            var row = new SalesRequestDTO
            {
                CreatedByUserId = actor.EmployeeId,
                CreatedByName = actor.EmployeeName,
                CreatedByUserType = SalesRoles.IsFollower(actor.UserType)
                    ? SalesRequestSources.Follower
                    : actor.UserType,
                TargetEmployeeId = 0,
                TargetEmployeeName = null,
                CityValue = actor.BranchId,
                CityName = actor.BranchName,
                CustomerSourceType = sourceType,
                ExistingCustomerId = request.ExistingCustomerId is > 0 ? request.ExistingCustomerId : null,
                CustomerSourceCityValue = request.CustomerSourceCityValue,
                CustomerName = name ?? string.Empty,
                CustomerPhone = phone,
                CustomerProvince = request.Customer?.Province,
                CustomerAddress = request.Customer?.Address,
                Notes = request.Notes,
                MaxSalePrice = SalesRequestPriceCeiling.Normalize(request.MaxSalePrice),
                Status = SalesRequestStatuses.New,
                FilterStatus = SalesFilterStatuses.ReadyForSale,
                CreatedAtUtc = _clock.UtcNow
            };
            if (string.IsNullOrWhiteSpace(row.CustomerName))
            {
                row.CustomerName = "زبون";
            }

            var saved = await _repo.InsertAsync(row, ct);
            await AppendHistoryAsync(saved, SalesRequestEvents.Created, actor, saved.Notes, ct);
            return await HydrateAsync(saved, ct);
        }

        public async Task<SalesRequestImportResultDTO> ImportRowsAsync(
            SalesIdentity actor,
            SalesRequestImportDTO import,
            CancellationToken ct)
        {
            if (SalesRoles.IsSalesEmployee(actor.UserType)
                || (!SalesRoles.CanCreateSalesRequest(actor.UserType)
                    && !string.Equals(actor.Role, SalesRoles.SalesManager, StringComparison.Ordinal)))
            {
                throw new SalesCompleteException(StatusCodes.Status403Forbidden, "غير مصرح.");
            }

            import ??= new SalesRequestImportDTO();
            var rows = import.Rows ?? [];
            var batchCityValue = Trimmed(import.CityValue);
            var batchCityName = Trimmed(import.CityName);
            var result = new SalesRequestImportResultDTO { Total = rows.Count };
            foreach (var row in rows)
            {
                var rowNumber = row.RowNumber > 0 ? row.RowNumber : result.Saved + result.Failed + 1;
                try
                {
                    var name = row.CustomerName?.Trim();
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        throw new SalesCompleteException(StatusCodes.Status400BadRequest, "اسم الزبون مطلوب.");
                    }

                    if (string.IsNullOrWhiteSpace(row.Phone))
                    {
                        throw new SalesCompleteException(StatusCodes.Status400BadRequest, "رقم الهاتف مطلوب.");
                    }

                    // BE is the sole authority for storage form (leading 0, scientific, +964…).
                    SalesPhoneNormalizer.RequireValidIfPresent(row.Phone);
                    var phone = SalesPhoneNormalizer.ForStorage(row.Phone);
                    if (string.IsNullOrWhiteSpace(phone) || !SalesIraqPhone.IsValid(phone))
                    {
                        throw new SalesCompleteException(StatusCodes.Status400BadRequest, SalesIraqPhone.Message);
                    }

                    var provinceText = Trimmed(row.Province) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(provinceText))
                    {
                        throw new SalesCompleteException(
                            StatusCodes.Status400BadRequest,
                            "المحافظة مطلوبة");
                    }

                    // Never stamp actor.BranchId (often نجف on shared/central hosts).
                    // Each Excel row must carry its own resolved CityValue/CityName.
                    var cityValue = Trimmed(row.CityValue) ?? batchCityValue;
                    var cityName = Trimmed(row.CityName) ?? batchCityName ?? provinceText;
                    if (string.IsNullOrWhiteSpace(cityValue))
                    {
                        throw new SalesCompleteException(
                            StatusCodes.Status400BadRequest,
                            $"المحافظة غير معروفة بعد التطبيع: {provinceText}");
                    }

                    // Reject silent Najaf / demo fallbacks when the Excel province is not Najaf.
                    if (IsForbiddenDefaultCityStamp(cityValue, cityName, provinceText))
                    {
                        throw new SalesCompleteException(
                            StatusCodes.Status400BadRequest,
                            $"تعذر مطابقة المحافظة «{provinceText}» — لن يتم تعيين النجف تلقائياً");
                    }

                    if (!actor.IsGateway)
                    {
                        // Branch-bound manager: never trust PassesOptionalCityFilter fail-open for Arabic labels.
                        var ownByExactId = !string.IsNullOrWhiteSpace(actor.BranchId)
                            && string.Equals(cityValue, actor.BranchId.Trim(), StringComparison.OrdinalIgnoreCase);
                        var ownByComparableKey = SalesBranchScope.IsComparableBranchKey(cityValue)
                            && SalesBranchScope.IsComparableBranchKey(actor.BranchId)
                            && string.Equals(cityValue, actor.BranchId.Trim(), StringComparison.OrdinalIgnoreCase);
                        var ownByName = !string.IsNullOrWhiteSpace(actor.BranchName)
                            && (FoldAr(actor.BranchName) == FoldAr(cityName)
                                || FoldAr(actor.BranchName) == FoldAr(provinceText));
                        if (!ownByExactId && !ownByComparableKey && !ownByName)
                        {
                            throw new SalesCompleteException(
                                StatusCodes.Status403Forbidden,
                                $"لا يمكن استيراد محافظة خارج صلاحياتك: {provinceText}");
                        }
                    }

                    var notes = string.IsNullOrWhiteSpace(row.SaleType)
                        ? null
                        : $"نوع المبيع: {row.SaleType.Trim()}";

                    await CreateImportedAsync(
                        actor,
                        cityValue,
                        cityName,
                        name,
                        phone,
                        provinceText,
                        Trimmed(row.Address),
                        notes,
                        ct);
                    result.Saved++;
                }
                catch (SalesCompleteException ex)
                {
                    result.Failed++;
                    result.Errors.Add(new SalesRequestImportErrorDTO
                    {
                        RowNumber = rowNumber,
                        Message = ex.Message
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// Import-only create: persists the Excel-resolved CityValue/CityName, never actor.BranchId.
        /// </summary>
        private async Task<SalesRequestDTO> CreateImportedAsync(
            SalesIdentity actor,
            string cityValue,
            string cityName,
            string customerName,
            string phone,
            string province,
            string? address,
            string? notes,
            CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            var row = new SalesRequestDTO
            {
                CreatedByUserId = actor.EmployeeId,
                CreatedByName = actor.EmployeeName,
                CreatedByUserType = actor.UserType,
                TargetEmployeeId = 0,
                TargetEmployeeName = null,
                CityValue = cityValue,
                CityName = cityName,
                CustomerSourceType = SalesRequestSources.NewCustomer,
                ExistingCustomerId = null,
                CustomerSourceCityValue = null,
                CustomerName = customerName,
                CustomerPhone = phone,
                CustomerProvince = province,
                CustomerAddress = address,
                Notes = notes,
                Status = SalesRequestStatuses.New,
                FilterStatus = SalesFilterStatuses.ReadyForSale,
                CreatedAtUtc = _clock.UtcNow
            };
            var saved = await _repo.InsertAsync(row, ct);
            await AppendHistoryAsync(saved, SalesRequestEvents.Created, actor, saved.Notes, ct);
            return await HydrateAsync(saved, ct);
        }

        /// <summary>
        /// Detects when a non-Najaf Excel province would be persisted under a Najaf/demo city key.
        /// </summary>
        public static bool IsForbiddenDefaultCityStamp(string cityValue, string? cityName, string province)
        {
            if (string.IsNullOrWhiteSpace(province))
            {
                return false;
            }

            var prov = FoldAr(province);
            var isNajafProvince = prov.Contains("نجف", StringComparison.Ordinal)
                                  || prov.Contains("najaf", StringComparison.OrdinalIgnoreCase);
            if (isNajafProvince)
            {
                return false;
            }

            var city = FoldAr(cityValue + " " + (cityName ?? ""));
            var looksNajaf = city.Contains("نجف", StringComparison.Ordinal)
                             || city.Contains("najaf", StringComparison.OrdinalIgnoreCase);
            return looksNajaf;
        }

        private static string FoldAr(string value)
        {
            var text = value.Trim()
                .Replace('\u0640', ' ')
                .Replace('أ', 'ا').Replace('إ', 'ا').Replace('آ', 'ا').Replace('ٱ', 'ا')
                .Replace('ة', 'ه')
                .Replace('ى', 'ي');
            while (text.Contains("  ", StringComparison.Ordinal))
            {
                text = text.Replace("  ", " ", StringComparison.Ordinal);
            }

            return text;
        }

        private static string? Trimmed(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        public async Task<SalesRequestDTO> SubmitByEmployeeAsync(SalesIdentity actor, SalesRequestCreateDTO request, CancellationToken ct)
        {
            if (!SalesRoles.IsSalesEmployee(actor.UserType)
                && !string.Equals(actor.Role, SalesRoles.SalesEmployee, StringComparison.Ordinal))
            {
                throw new SalesCompleteException(StatusCodes.Status403Forbidden, "غير مصرح.");
            }

            var name = request.Customer?.FullName?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "اسم الزبون مطلوب.");
            }

            var phone = request.Customer?.Phone;
            if (string.IsNullOrWhiteSpace(phone) || !SalesIraqPhone.IsValid(phone))
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, SalesIraqPhone.Message);
            }

            var address = request.Customer?.Address?.Trim();
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "العنوان مطلوب.");
            }

            var province = request.Customer?.Province?.Trim();
            if (string.IsNullOrWhiteSpace(province) || SalesCityDisplay.IsInternalKey(province, actor.BranchId))
            {
                province = actor.BranchName;
            }

            await _repo.EnsureSchemaAsync(ct);
            var row = new SalesRequestDTO
            {
                CreatedByUserId = actor.EmployeeId,
                CreatedByName = string.IsNullOrWhiteSpace(actor.EmployeeName) ? "موظف مبيعات" : actor.EmployeeName,
                CreatedByUserType = actor.UserType ?? SalesRoles.UserTypeSalesEmployee,
                TargetEmployeeId = 0,
                TargetEmployeeName = null,
                CityValue = actor.BranchId,
                CityName = actor.BranchName,
                CustomerSourceType = SalesRequestSources.EmployeeSubmitted,
                ExistingCustomerId = null,
                CustomerSourceCityValue = null,
                CustomerName = name,
                CustomerPhone = SalesIraqPhone.Normalize(phone),
                CustomerProvince = province,
                CustomerAddress = address,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                Status = SalesRequestStatuses.New,
                FilterStatus = SalesFilterStatuses.ReadyForSale,
                CreatedAtUtc = _clock.UtcNow
            };
            var saved = await _repo.InsertAsync(row, ct);
            await AppendHistoryAsync(saved, SalesRequestEvents.EmployeeSubmitted, actor, saved.Notes, ct);
            return await HydrateAsync(saved, ct);
        }

        public async Task<IReadOnlyList<SalesRequestDTO>> ListEmployeeSubmittedAsync(CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            var rows = await _repo.ListAsync(null, null, null, null, ct);
            var result = new List<SalesRequestDTO>();
            foreach (var row in rows.Where(r => SalesRequestSources.IsEmployeeSubmitted(r.CustomerSourceType)))
            {
                result.Add(await HydrateAsync(row, ct));
            }

            return result;
        }

        public async Task<int> CountUnreadEmployeeSubmittedAsync(CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            return await _repo.CountUnreadEmployeeSubmittedAsync(ct);
        }

        public async Task<SalesRequestDTO> MarkReadAsync(SalesIdentity manager, int id, CancellationToken ct)
        {
            EnsureManager(manager);
            await _repo.EnsureSchemaAsync(ct);
            var row = await _repo.GetByIdAsync(id, ct)
                      ?? throw new SalesCompleteException(StatusCodes.Status404NotFound, "طلب المبيع غير موجود.");
            if (!SalesRequestSources.IsEmployeeSubmitted(row.CustomerSourceType) || row.ManagerReadAtUtc != null)
            {
                return await HydrateAsync(row, ct);
            }

            row.ManagerReadAtUtc = _clock.UtcNow;
            await _repo.UpdateAsync(row, ct);
            await AppendHistoryAsync(row, SalesRequestEvents.ManagerViewed, manager, null, ct);
            return await HydrateAsync(row, ct);
        }

        public async Task<int> MarkAllReadAsync(SalesIdentity manager, CancellationToken ct)
        {
            EnsureManager(manager);
            await _repo.EnsureSchemaAsync(ct);
            var rows = await _repo.ListAsync(null, null, null, null, ct);
            var unread = rows.Where(r => SalesRequestSources.IsEmployeeSubmitted(r.CustomerSourceType) && r.ManagerReadAtUtc == null).ToList();
            foreach (var row in unread)
            {
                row.ManagerReadAtUtc = _clock.UtcNow;
                await _repo.UpdateAsync(row, ct);
                await AppendHistoryAsync(row, SalesRequestEvents.ManagerViewed, manager, null, ct);
            }

            return unread.Count;
        }

        public async Task<SalesRequestDTO> ManagerRejectAsync(SalesIdentity manager, int id, string reason, CancellationToken ct)
        {
            EnsureManager(manager);
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "سبب الرفض مطلوب.");
            }

            await _repo.EnsureSchemaAsync(ct);
            var row = await _repo.GetByIdAsync(id, ct)
                      ?? throw new SalesCompleteException(StatusCodes.Status404NotFound, "طلب المبيع غير موجود.");
            EnsureNotSold(row);
            if (row.Status == SalesRequestStatuses.Rejected)
            {
                return await HydrateAsync(row, ct);
            }

            if (!SalesRequestStatuses.CanReject(row.Status))
            {
                throw new SalesCompleteException(StatusCodes.Status409Conflict, "لا يمكن رفض هذا الطلب.");
            }

            var trimmed = reason.Trim();
            var previous = row.Status;
            row.Status = SalesRequestStatuses.Rejected;
            row.RejectedAtUtc = _clock.UtcNow;
            row.RejectionReason = trimmed;
            // Manager rejects leave the Rejected tab (employee-app only) and move to Deleted with the reason.
            row.IsDeleted = true;
            row.DeletedAtUtc = _clock.UtcNow;
            row.DeletedByUserId = manager.EmployeeId;
            row.DeletedByName = manager.EmployeeName;
            row.DeleteReason = trimmed;
            await _repo.UpdateAsync(row, ct);
            await AppendHistoryAsync(row, SalesRequestEvents.Rejected, manager, trimmed, ct, previous);
            await AppendHistoryAsync(row, SalesRequestEvents.RejectionReason, manager, trimmed, ct, previous);
            await AppendHistoryAsync(row, SalesRequestEvents.ManagerRequestDeleted, manager, trimmed, ct, previous);
            await NotifyCrmIfLinkedAsync(row, "Cancelled", trimmed, ct);
            return await HydrateAsync(row, ct);
        }

        public async Task<SalesRequestDTO> ManagerPrepareForSaleAsync(SalesIdentity manager, int id, string? note, CancellationToken ct)
        {
            EnsureManager(manager);
            await _repo.EnsureSchemaAsync(ct);
            var row = await _repo.GetByIdAsync(id, ct)
                      ?? throw new SalesCompleteException(StatusCodes.Status404NotFound, "طلب المبيع غير موجود.");
            EnsureNotSold(row);
            if (!SalesRequestStatuses.CanPrepare(row.Status))
            {
                throw new SalesCompleteException(StatusCodes.Status409Conflict, "لا يمكن تجهيز هذا الطلب.");
            }

            var trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            if (row.Status != SalesRequestStatuses.PreparedForSale)
            {
                var previous = row.Status;
                row.Status = SalesRequestStatuses.PreparedForSale;
                row.ProcessingAtUtc = _clock.UtcNow;
                row.ViewedAtUtc ??= row.ProcessingAtUtc;
                row.FilterStatus = SalesFilterStatuses.ReadyForSale;
                if (trimmed != null)
                {
                    row.PreparedForSaleNote = trimmed;
                }

                await _repo.UpdateAsync(row, ct);
                await AppendHistoryAsync(row, SalesRequestEvents.PreparedForSale, manager, trimmed, ct, previous);
                if (trimmed != null)
                {
                    await AppendHistoryAsync(row, SalesRequestEvents.PreparedForSaleNote, manager, trimmed, ct, previous);
                }
            }
            else if (trimmed != null)
            {
                row.PreparedForSaleNote = trimmed;
                await _repo.UpdateAsync(row, ct);
                await AppendHistoryAsync(row, SalesRequestEvents.PreparedForSaleNote, manager, trimmed, ct, row.Status);
            }

            return await HydrateAsync(row, ct);
        }

        public async Task<SalesRequestDTO> ManagerPendAsync(SalesIdentity manager, int id, string note, CancellationToken ct)
        {
            EnsureManager(manager);
            if (string.IsNullOrWhiteSpace(note))
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "ملاحظة التعليق مطلوبة.");
            }

            await _repo.EnsureSchemaAsync(ct);
            var row = await _repo.GetByIdAsync(id, ct)
                      ?? throw new SalesCompleteException(StatusCodes.Status404NotFound, "طلب المبيع غير موجود.");
            EnsureNotSold(row);
            if (!SalesRequestStatuses.CanPend(row.Status))
            {
                throw new SalesCompleteException(StatusCodes.Status409Conflict, "لا يمكن تعليق هذا الطلب.");
            }

            var trimmed = note.Trim();
            var previous = row.Status;
            row.Status = SalesRequestStatuses.Pending;
            row.PendingNote = trimmed;
            row.ViewedAtUtc ??= _clock.UtcNow;
            await _repo.UpdateAsync(row, ct);
            await AppendHistoryAsync(row, SalesRequestEvents.Pending, manager, trimmed, ct, previous);
            await AppendHistoryAsync(row, SalesRequestEvents.PendingNote, manager, trimmed, ct, previous);
            return await HydrateAsync(row, ct);
        }

        /// <summary>
        /// Manager board list. Unbounded historical dumps were making gateway fan-out ~3s+;
        /// cap recent rows when no date window is provided (detail still uses GetById).
        /// </summary>
        public const int ManagerListDefaultTake = 400;

        public async Task<IReadOnlyList<SalesRequestDTO>> ListForManagerAsync(string? status, int? employeeId, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            // Date-filtered lists stay exact; open list uses TOP for first-paint speed.
            int? take = fromUtc == null && toUtc == null ? ManagerListDefaultTake : null;
            var rows = await _repo.ListAsync(employeeId, status, fromUtc, toUtc, ct, take: take);
            var result = new List<SalesRequestDTO>();
            foreach (var row in rows)
            {
                // Backend-enforce: held New/unassigned requests never appear in manager lists (غير مسند).
                if (SalesExceptionHoldStatuses.IsHeldUnassigned(row))
                {
                    continue;
                }

                // List cards do not need history/name-transfers — hydrate only on GetById.
                result.Add(WithTimeline(row));
            }

            return result;
        }

        /// <summary>
        /// Production manager board: page + tab + search. No Timeline (detail uses GetById).
        /// Target: first page under ~2s with 20k+ rows when indexes are present.
        /// </summary>
        public async Task<SalesRequestManagerListPageDTO> ListForManagerPageAsync(
            string? tab,
            int? employeeId,
            string? q,
            int page,
            int pageSize,
            CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize <= 0 ? 40 : pageSize, 1, 100);
            var skip = (page - 1) * pageSize;

            var countsTask = _repo.CountManagerTabsAsync(ct);
            var pageTask = _repo.ListManagerPageAsync(tab, employeeId, q, skip, pageSize, ct);
            await Task.WhenAll(countsTask, pageTask);

            var (items, total) = await pageTask;
            var counts = await countsTask;
            return new SalesRequestManagerListPageDTO
            {
                Items = items,
                Total = total,
                Page = page,
                PageSize = pageSize,
                HasMore = skip + items.Count < total,
                TabCounts = counts
            };
        }

        public async Task<SalesRequestManagerListPageDTO> ListDeletedForManagerPageAsync(
            string? q,
            int page,
            int pageSize,
            CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize <= 0 ? 40 : pageSize, 1, 100);
            var skip = (page - 1) * pageSize;
            var countsTask = _repo.CountManagerTabsAsync(ct);
            var pageTask = _repo.ListDeletedPageAsync(q, skip, pageSize, ct);
            await Task.WhenAll(countsTask, pageTask);
            var (items, total) = await pageTask;
            var counts = await countsTask;
            return new SalesRequestManagerListPageDTO
            {
                Items = items,
                Total = total,
                Page = page,
                PageSize = pageSize,
                HasMore = skip + items.Count < total,
                TabCounts = counts
            };
        }

        public async Task<SalesRequestDTO?> GetForManagerAsync(int id, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            var row = await _repo.GetByIdAsync(id, ct);
            return row == null ? null : await HydrateAsync(row, ct);
        }

        public async Task<IReadOnlyList<SalesRequestDTO>> ListForEmployeeAsync(int employeeId, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            // Filter stage retired: assigned employee sees their requests without ReadyForSale gate.
            var rows = await _repo.ListAsync(employeeId, null, null, null, ct);
            var result = new List<SalesRequestDTO>();
            foreach (var row in rows.Where(r => r.TargetEmployeeId == employeeId && r.TargetEmployeeId > 0))
            {
                result.Add(SanitizeForSalesEmployee(await HydrateAsync(row, ct)));
            }

            return result;
        }

        public async Task<IReadOnlyList<SalesRequestDTO>> ListSubmittedByEmployeeAsync(int employeeId, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            if (employeeId <= 0)
            {
                return [];
            }

            // Include soft-deleted Rejected (manager/monitor) so submitter sees سبب الرفض.
            // SubmittedLogIncludesRejectedDeletes_V1
            var rows = await _repo.ListCreatedByEmployeeForSubmittedLogAsync(employeeId, ct);
            var result = new List<SalesRequestDTO>();
            foreach (var row in rows.Where(r =>
                         r.CreatedByUserId == employeeId
                         && SalesRequestSources.IsEmployeeSubmitted(r.CustomerSourceType)))
            {
                // Prefer RejectionReason; fall back to DeleteReason for older soft-deletes.
                if (string.IsNullOrWhiteSpace(row.RejectionReason) && !string.IsNullOrWhiteSpace(row.DeleteReason))
                {
                    row.RejectionReason = row.DeleteReason;
                }

                result.Add(SanitizeForSalesEmployee(await HydrateAsync(row, ct)));
            }

            return result
                .OrderByDescending(r => r.CreatedAtUtc)
                .ToList();
        }

        public async Task<SalesRequestDTO> GetForEmployeeAsync(int id, int employeeId, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            var row = await RequireOwned(id, employeeId, ct);
            return SanitizeForSalesEmployee(await HydrateAsync(row, ct));
        }

        public async Task<SalesRequestDTO> ViewAsync(int id, int employeeId, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            var row = await RequireOwned(id, employeeId, ct);
            if (row.ViewedAtUtc == null)
            {
                row.ViewedAtUtc = _clock.UtcNow;
                if (row.Status == SalesRequestStatuses.New)
                {
                    row.Status = SalesRequestStatuses.Assigned;
                }

                await _repo.UpdateAsync(row, ct);
                await AppendHistoryAsync(row, SalesRequestEvents.Viewed, EmployeeActor(employeeId, row), null, ct);
            }

            return SanitizeForSalesEmployee(await HydrateAsync(row, ct));
        }

        public Task<SalesRequestDTO> StartProcessingAsync(int id, int employeeId, string note, CancellationToken ct) =>
            PrepareForSaleAsync(id, employeeId, note, ct);

        public async Task<SalesRequestDTO> PrepareForSaleAsync(int id, int employeeId, string note, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(note))
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "ملاحظة جاهز للبيع مطلوبة.");
            }

            await _repo.EnsureSchemaAsync(ct);
            var row = await RequireOwned(id, employeeId, ct);
            EnsureNotSold(row);
            if (!SalesRequestStatuses.CanPrepare(row.Status))
            {
                throw new SalesCompleteException(StatusCodes.Status409Conflict, "لا يمكن تجهيز هذا الطلب.");
            }

            var trimmed = note.Trim();
            if (row.Status != SalesRequestStatuses.PreparedForSale)
            {
                var previous = row.Status;
                row.Status = SalesRequestStatuses.PreparedForSale;
                row.ProcessingAtUtc = _clock.UtcNow;
                row.ViewedAtUtc ??= row.ProcessingAtUtc;
                row.PreparedForSaleNote = trimmed;
                await _repo.UpdateAsync(row, ct);
                var actor = EmployeeActor(employeeId, row);
                await AppendHistoryAsync(row, SalesRequestEvents.PreparedForSale, actor, trimmed, ct, previous);
                await AppendHistoryAsync(row, SalesRequestEvents.PreparedForSaleNote, actor, trimmed, ct, previous);
            }
            else
            {
                row.PreparedForSaleNote = trimmed;
                await _repo.UpdateAsync(row, ct);
                await AppendHistoryAsync(row, SalesRequestEvents.PreparedForSaleNote, EmployeeActor(employeeId, row), trimmed, ct, row.Status);
            }

            return SanitizeForSalesEmployee(await HydrateAsync(row, ct));
        }

        public async Task<SalesRequestDTO> PendAsync(int id, int employeeId, string note, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(note))
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "ملاحظة التعليق مطلوبة.");
            }

            await _repo.EnsureSchemaAsync(ct);
            var row = await RequireOwned(id, employeeId, ct);
            EnsureNotSold(row);
            if (!SalesRequestStatuses.CanPend(row.Status))
            {
                throw new SalesCompleteException(StatusCodes.Status409Conflict, "لا يمكن تعليق هذا الطلب.");
            }

            var trimmed = note.Trim();
            var previous = row.Status;
            row.Status = SalesRequestStatuses.Pending;
            row.PendingNote = trimmed;
            row.ViewedAtUtc ??= _clock.UtcNow;
            await _repo.UpdateAsync(row, ct);
            var actor = EmployeeActor(employeeId, row);
            await AppendHistoryAsync(row, SalesRequestEvents.Pending, actor, trimmed, ct, previous);
            await AppendHistoryAsync(row, SalesRequestEvents.PendingNote, actor, trimmed, ct, previous);
            return SanitizeForSalesEmployee(await HydrateAsync(row, ct));
        }

        public async Task<SalesRequestDTO> RejectAsync(int id, int employeeId, string reason, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "سبب الرفض مطلوب.");
            }

            await _repo.EnsureSchemaAsync(ct);
            var row = await RequireOwned(id, employeeId, ct);
            EnsureNotSold(row);
            if (row.Status == SalesRequestStatuses.Rejected)
            {
                return SanitizeForSalesEmployee(await HydrateAsync(row, ct));
            }

            if (!SalesRequestStatuses.CanReject(row.Status))
            {
                throw new SalesCompleteException(StatusCodes.Status409Conflict, "لا يمكن رفض هذا الطلب.");
            }

            var trimmed = reason.Trim();
            var previous = row.Status;
            row.Status = SalesRequestStatuses.Rejected;
            row.RejectedAtUtc = _clock.UtcNow;
            row.RejectionReason = trimmed;
            await _repo.UpdateAsync(row, ct);
            var actor = EmployeeActor(employeeId, row);
            await AppendHistoryAsync(row, SalesRequestEvents.Rejected, actor, trimmed, ct, previous);
            await AppendHistoryAsync(row, SalesRequestEvents.RejectionReason, actor, trimmed, ct, previous);
            await NotifyCrmIfLinkedAsync(row, "Cancelled", trimmed, ct);
            return SanitizeForSalesEmployee(await HydrateAsync(row, ct));
        }

        public async Task<SalesRequestDTO> AssignAsync(
            SalesIdentity manager,
            int id,
            SalesRequestAssignDTO request,
            CancellationToken ct,
            Guid? exceptionAssignId = null)
        {
            EnsureManager(manager);
            if (request.EmployeeId <= 0)
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "يجب اختيار موظف المبيعات.");
            }

            await _repo.EnsureSchemaAsync(ct);
            var row = await _repo.GetByIdAsync(id, ct)
                      ?? throw new SalesCompleteException(StatusCodes.Status404NotFound, "طلب المبيع غير موجود.");

            if (SalesExceptionHoldStatuses.IsHeld(row.ExceptionHoldStatus))
            {
                var hold = row.ExceptionHoldStatus!;
                if (string.Equals(hold, SalesExceptionHoldStatuses.Pending, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(hold, SalesExceptionHoldStatuses.Rejected, StringComparison.OrdinalIgnoreCase))
                {
                    throw new SalesCompleteException(
                        StatusCodes.Status409Conflict,
                        "الطلب محجوز ضمن طلب استثناء ولا يمكن إسناده.");
                }

                if (string.Equals(hold, SalesExceptionHoldStatuses.Approved, StringComparison.OrdinalIgnoreCase))
                {
                    if (exceptionAssignId is null
                        || row.ActiveExceptionId is null
                        || exceptionAssignId.Value != row.ActiveExceptionId.Value)
                    {
                        throw new SalesCompleteException(
                            StatusCodes.Status409Conflict,
                            "يجب إسناد الطلب عبر طلب الاستثناء الموافق عليه.");
                    }
                }
            }

            if (row.TargetEmployeeId > 0 && row.Status != SalesRequestStatuses.New)
            {
                throw new SalesCompleteException(StatusCodes.Status409Conflict, "الطلب مسند مسبقاً.");
            }

            // EmployeeId is per-branch. Never accept another city's id/name.
            // (Basra UserID 18 = متابع; Karbala UserID 18 = رعد جندي سلطان — same number, different people.)
            const string crossBranchAssignGuard = "CrossBranchAssignGuard_V1";
            var employeeName = request.EmployeeName;
            if (_employees != null)
            {
                // Same universe as the assign dropdown (ListEmployees), then require active.
                var inBranch = (await _employees.ListEmployeesAsync(ct))
                    .FirstOrDefault(e => e.EmployeeId == request.EmployeeId);
                if (inBranch is null)
                {
                    throw new SalesCompleteException(
                        StatusCodes.Status400BadRequest,
                        "الموظف غير موجود ضمن موظفي مبيعات هذا الفرع. لا يمكن الإسناد عبر المحافظات. (" + crossBranchAssignGuard + ")");
                }

                var active = (await _employees.ListActiveSalesEmployeesAsync(ct))
                    .Any(e => e.EmployeeId == request.EmployeeId);
                if (!active)
                {
                    throw new SalesCompleteException(
                        StatusCodes.Status400BadRequest,
                        "حساب موظف المبيعات غير مفعّل (UserState). فعّله ثم أعد الإسناد. (" + crossBranchAssignGuard + ")");
                }

                employeeName = inBranch.EmployeeName;
            }
            else if (string.IsNullOrWhiteSpace(employeeName))
            {
                employeeName = $"موظف #{request.EmployeeId}";
            }

            row.TargetEmployeeId = request.EmployeeId;
            row.TargetEmployeeName = employeeName;
            // Do not rewrite CityValue/CityName from assign payload — FE used to stamp the
            // selected employee's cityName onto the request (e.g. Basra row → كربلاء).

            if (request.KeepNewCustomer)
            {
                if (!SalesRequestSources.IsEmployeeSubmitted(row.CustomerSourceType))
                {
                    row.ExistingCustomerId = null;
                    row.CustomerSourceType = SalesRequestSources.NewCustomer;
                }
            }
            else if (request.ExistingCustomerId is > 0 && !SalesRequestSources.IsEmployeeSubmitted(row.CustomerSourceType))
            {
                row.ExistingCustomerId = request.ExistingCustomerId;
                row.CustomerSourceType = SalesRequestSources.ExistingCustomer;
                row.CustomerSourceCityValue = request.CustomerSourceCityValue;
            }

            if (request.CustomerName != null)
            {
                var name = request.CustomerName.Trim();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    row.CustomerName = name;
                }
            }

            if (request.CustomerPhone != null)
            {
                SalesIraqPhone.RequireIfPresent(request.CustomerPhone);
                row.CustomerPhone = string.IsNullOrWhiteSpace(request.CustomerPhone)
                    ? request.CustomerPhone.Trim()
                    : SalesIraqPhone.Normalize(request.CustomerPhone);
            }

            if (request.CustomerProvince != null)
            {
                row.CustomerProvince = request.CustomerProvince.Trim();
            }

            if (request.CustomerAddress != null)
            {
                row.CustomerAddress = request.CustomerAddress.Trim();
            }

            if (request.Notes != null)
            {
                row.Notes = request.Notes.Trim();
            }

            // Assign is the manager send moment — always apply ceiling (null clears / means none).
            row.MaxSalePrice = SalesRequestPriceCeiling.Normalize(request.MaxSalePrice);

            row.Status = SalesRequestStatuses.Assigned;
            row.AssignedAtUtc = _clock.UtcNow;
            row.AssignedByUserId = manager.EmployeeId;
            row.AssignedByName = string.IsNullOrWhiteSpace(manager.EmployeeName) ? "مدير المبيعات" : manager.EmployeeName;
            // Filter stage retired: assignment goes straight to the sales employee inbox.
            row.FilterStatus = SalesFilterStatuses.ReadyForSale;
            row.FilteredByUserId = null;
            row.FilterNote = null;
            row.FilterRejectReason = null;
            row.FilteredAtUtc = null;
            await _repo.UpdateAsync(row, ct);
            await AppendHistoryAsync(row, SalesRequestEvents.Assigned, manager, null, ct);
            return await HydrateAsync(row, ct);
        }

        public const int AssignBatchMaxIds = 50;

        public async Task<SalesRequestAssignBatchResultDTO> AssignBatchAsync(
            SalesIdentity manager,
            SalesRequestAssignBatchDTO request,
            CancellationToken ct)
        {
            EnsureManager(manager);
            if (request.EmployeeId <= 0)
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "يجب اختيار موظف المبيعات.");
            }

            var ids = (request.RequestIds ?? [])
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            if (ids.Count == 0)
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "اختر طلباً واحداً على الأقل.");
            }

            if (ids.Count > AssignBatchMaxIds)
            {
                throw new SalesCompleteException(
                    StatusCodes.Status400BadRequest,
                    $"الحد الأقصى للإسناد الجماعي {AssignBatchMaxIds} طلباً.");
            }

            var result = new SalesRequestAssignBatchResultDTO();
            var assignBody = new SalesRequestAssignDTO
            {
                EmployeeId = request.EmployeeId,
                EmployeeName = request.EmployeeName,
                CityValue = request.CityValue,
                CityName = request.CityName
            };

            foreach (var id in ids)
            {
                try
                {
                    await AssignAsync(manager, id, assignBody, ct);
                    result.Succeeded.Add(id);
                }
                catch (SalesCompleteException ex)
                {
                    result.Failed.Add(new SalesRequestAssignBatchFailureDTO
                    {
                        Id = id,
                        Message = ex.Message
                    });
                }
            }

            return result;
        }

        public async Task SetExceptionHoldAsync(int id, string? holdStatus, Guid? exceptionId, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            var row = await _repo.GetByIdAsync(id, ct)
                      ?? throw new SalesCompleteException(StatusCodes.Status404NotFound, "طلب المبيع غير موجود.");

            if (string.IsNullOrWhiteSpace(holdStatus))
            {
                row.ExceptionHoldStatus = null;
                row.ActiveExceptionId = null;
            }
            else
            {
                var normalized = holdStatus.Trim();
                if (!SalesExceptionHoldStatuses.IsHeld(normalized))
                {
                    throw new SalesCompleteException(StatusCodes.Status400BadRequest, "حالة حجز الاستثناء غير صحيحة.");
                }

                row.ExceptionHoldStatus = normalized;
                if (exceptionId.HasValue)
                {
                    row.ActiveExceptionId = exceptionId;
                }
            }

            await _repo.UpdateAsync(row, ct);
        }

        public async Task<SalesRequestDTO> ReturnAsync(SalesIdentity manager, int id, string note, CancellationToken ct)
        {
            EnsureManager(manager);
            if (string.IsNullOrWhiteSpace(note))
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "ملاحظة الإعادة مطلوبة.");
            }

            await _repo.EnsureSchemaAsync(ct);
            var row = await _repo.GetByIdAsync(id, ct)
                      ?? throw new SalesCompleteException(StatusCodes.Status404NotFound, "طلب المبيع غير موجود.");
            if (row.Status != SalesRequestStatuses.Rejected)
            {
                throw new SalesCompleteException(StatusCodes.Status409Conflict, "يمكن إعادة الطلب المرفوض فقط.");
            }

            if (row.TargetEmployeeId <= 0)
            {
                // Employee-submitted requests keep TargetEmployeeId=0; return to creator.
                if (SalesRequestSources.IsEmployeeSubmitted(row.CustomerSourceType)
                    && row.CreatedByUserId > 0)
                {
                    row.TargetEmployeeId = row.CreatedByUserId;
                    row.TargetEmployeeName = string.IsNullOrWhiteSpace(row.CreatedByName)
                        ? "موظف مبيعات"
                        : row.CreatedByName;
                }
                else
                {
                    throw new SalesCompleteException(StatusCodes.Status409Conflict, "لا يوجد موظف مسند لإعادة الطلب إليه.");
                }
            }

            var trimmed = note.Trim();
            var previousReason = row.RejectionReason;
            var previous = row.Status;
            row.Status = SalesRequestStatuses.Returned;
            row.ReturnNote = trimmed;
            // Returned work must be visible again to the assigned sales employee.
            row.FilterStatus = SalesFilterStatuses.ReadyForSale;
            // Re-open as fresh incoming work (employee app treats null ViewedAtUtc as new).
            row.ViewedAtUtc = null;
            row.AssignedAtUtc ??= _clock.UtcNow;
            await _repo.UpdateAsync(row, ct);
            if (!string.Equals(row.RejectionReason, previousReason, StringComparison.Ordinal))
            {
                row.RejectionReason = previousReason;
                await _repo.UpdateAsync(row, ct);
            }

            await AppendHistoryAsync(row, SalesRequestEvents.Returned, manager, trimmed, ct, previous);
            await AppendHistoryAsync(row, SalesRequestEvents.ReturnNote, manager, trimmed, ct, previous);
            return await HydrateAsync(row, ct);
        }

        public async Task MarkConvertedAsync(int requestId, int employeeId, int saleId, DateTime utcNow, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            var row = await RequireOwned(requestId, employeeId, ct);
            if (row.Status == SalesRequestStatuses.Completed)
            {
                if (row.ConvertedToSaleId == saleId)
                {
                    return;
                }

                throw new SalesCompleteException(StatusCodes.Status409Conflict, "الطلب مكتمل ولا يمكن ربطه ببيع آخر.");
            }

            if (row.ConvertedToSaleId is > 0 && row.ConvertedToSaleId != saleId)
            {
                throw new SalesCompleteException(StatusCodes.Status409Conflict, "الطلب مرتبط بعملية بيع أخرى.");
            }

            if (row.Status == SalesRequestStatuses.Inspected && row.ConvertedToSaleId == saleId)
            {
                return;
            }

            if (row.Status == SalesRequestStatuses.ConvertedToSale && row.ConvertedToSaleId == saleId)
            {
                return;
            }

            if (!SalesRequestStatuses.CanConvertToSale(row.Status))
            {
                throw new SalesCompleteException(StatusCodes.Status409Conflict, "لا يمكن بدء عملية بيع من حالة الطلب الحالية.");
            }

            if (SalesRequestStatuses.IsInspected(row.Status))
            {
                row.ConvertedToSaleId = saleId;
                row.ProcessingAtUtc ??= utcNow;
                await _repo.UpdateAsync(row, ct);
                return;
            }

            var previous = row.Status;
            row.Status = SalesRequestStatuses.ConvertedToSale;
            row.ConvertedToSaleId = saleId;
            row.ProcessingAtUtc ??= utcNow;
            await _repo.UpdateAsync(row, ct);
            await AppendHistoryAsync(row, SalesRequestEvents.ConvertedToSale, EmployeeActor(employeeId, row), saleId.ToString(), ct, previous);
        }

        public async Task AttachDraftAsync(int requestId, int employeeId, int saleId, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            var row = await RequireOwned(requestId, employeeId, ct);
            EnsureNotSold(row);
            // saleId <= 0 clears a stale ConvertedToSaleId (reassigned request / missing draft).
            if (saleId <= 0)
            {
                if (row.ConvertedToSaleId is > 0)
                {
                    row.ConvertedToSaleId = null;
                    await _repo.UpdateAsync(row, ct);
                }

                return;
            }

            if (row.ConvertedToSaleId is > 0 && row.ConvertedToSaleId != saleId)
            {
                throw new SalesCompleteException(StatusCodes.Status409Conflict, "الطلب مرتبط بعملية بيع أخرى.");
            }

            if (row.ConvertedToSaleId == saleId)
            {
                return;
            }

            row.ConvertedToSaleId = saleId;
            row.ProcessingAtUtc ??= _clock.UtcNow;
            await _repo.UpdateAsync(row, ct);
        }

        public async Task<SalesRequestDTO> InspectAsync(int id, int employeeId, int? saleId, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            var row = await RequireOwned(id, employeeId, ct);
            EnsureNotSold(row);
            if (!SalesRequestStatuses.CanInspect(row.Status))
            {
                throw new SalesCompleteException(StatusCodes.Status409Conflict, "لا يمكن حفظ هذا الطلب كتم الكشف.");
            }

            if (saleId is > 0)
            {
                if (row.ConvertedToSaleId is > 0 && row.ConvertedToSaleId != saleId)
                {
                    throw new SalesCompleteException(StatusCodes.Status409Conflict, "الطلب مرتبط بعملية بيع أخرى.");
                }

                row.ConvertedToSaleId = saleId;
            }

            if (row.Status == SalesRequestStatuses.Inspected)
            {
                await _repo.UpdateAsync(row, ct);
                return SanitizeForSalesEmployee(await HydrateAsync(row, ct));
            }

            var previous = row.Status;
            row.Status = SalesRequestStatuses.Inspected;
            row.ViewedAtUtc ??= _clock.UtcNow;
            row.ProcessingAtUtc ??= _clock.UtcNow;
            await _repo.UpdateAsync(row, ct);
            await AppendHistoryAsync(row, SalesRequestEvents.Inspected, EmployeeActor(employeeId, row), null, ct, previous);
            return SanitizeForSalesEmployee(await HydrateAsync(row, ct));
        }

        public async Task MarkCompletedBySaleIdAsync(int saleId, DateTime utcNow, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            var rows = await _repo.ListAsync(null, null, null, null, ct);
            var row = rows.FirstOrDefault(r => r.ConvertedToSaleId == saleId);
            if (row == null)
            {
                return;
            }

            if (row.Status == SalesRequestStatuses.Completed)
            {
                return;
            }

            var previous = row.Status;
            row.Status = SalesRequestStatuses.Completed;
            row.CompletedAtUtc = utcNow;
            await _repo.UpdateAsync(row, ct);
            await AppendHistoryAsync(row, SalesRequestEvents.Completed, EmployeeActor(row.TargetEmployeeId, row), null, ct, previous);
            await NotifyCrmIfLinkedAsync(row, "Completed", null, ct);
        }

        public async Task<IReadOnlyList<SalesTransferPeerDTO>> ListTransferPeersAsync(SalesIdentity actor, CancellationToken ct)
        {
            EnsureSalesEmployee(actor);
            await _repo.EnsureSchemaAsync(ct);
            if (_employees == null)
            {
                return [];
            }

            var peers = await _employees.ListActiveSalesEmployeesAsync(ct);
            return peers
                .Where(e => e.EmployeeId > 0 && e.EmployeeId != actor.EmployeeId)
                .Select(e => new SalesTransferPeerDTO
                {
                    EmployeeId = e.EmployeeId,
                    EmployeeName = e.EmployeeName
                })
                .OrderBy(e => e.EmployeeName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<SalesRequestDTO> TransferNameAsync(
            SalesIdentity actor,
            int requestId,
            SalesRequestTransferDTO request,
            CancellationToken ct)
        {
            EnsureSalesEmployee(actor);
            var reason = (request.TransferReason ?? request.Reason ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "سبب نقل الاسم مطلوب.");
            }

            if (request.ToEmployeeId <= 0)
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "يجب اختيار موظف المبيعات.");
            }

            if (request.ToEmployeeId == actor.EmployeeId)
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "لا يمكن نقل الاسم إلى نفسك.");
            }

            await _repo.EnsureSchemaAsync(ct);
            var row = await RequireOwned(requestId, actor.EmployeeId, ct);
            if (!SalesRequestStatuses.CanTransferName(row.Status))
            {
                throw new SalesCompleteException(StatusCodes.Status409Conflict, "لا يمكن نقل هذا الطلب في حالته الحالية.");
            }

            if (_employees == null)
            {
                throw new SalesCompleteException(StatusCodes.Status503ServiceUnavailable, "تعذر التحقق من موظفي الفرع.");
            }

            var peers = await _employees.ListActiveSalesEmployeesAsync(ct);
            var peer = peers.FirstOrDefault(e => e.EmployeeId == request.ToEmployeeId);
            if (peer == null)
            {
                throw new SalesCompleteException(
                    StatusCodes.Status400BadRequest,
                    "الموظف المحدد غير فعال أو ليس موظف مبيعات في نفس الفرع.");
            }

            // Same-branch boundary: this host's branch DB + active peer list.
            // Do not raw-equality-match actor.BranchId to legacy SalesRequests.CityValue
            // (short key / catalog / Arabic) — mirrors SalesFilterService trusted-gateway scoping.
            SalesBranchScope.EnsureSameComparableBranch(actor, row.CityValue);

            var fromId = row.TargetEmployeeId;
            var fromName = row.TargetEmployeeName ?? actor.EmployeeName;
            var previous = row.Status;
            var now = _clock.UtcNow;

            var transferred = await _repo.TryTransferTargetAsync(
                row.Id,
                fromId,
                peer.EmployeeId,
                peer.EmployeeName,
                SalesRequestStatuses.Assigned,
                now,
                SalesFilterStatuses.ReadyForSale,
                ct);
            if (!transferred)
            {
                throw new SalesCompleteException(StatusCodes.Status409Conflict, "تم نقل الطلب بواسطة عملية أخرى. أعد المحاولة.");
            }

            row.TargetEmployeeId = peer.EmployeeId;
            row.TargetEmployeeName = peer.EmployeeName;
            row.Status = SalesRequestStatuses.Assigned;
            row.AssignedAtUtc = now;
            row.ViewedAtUtc = null;
            row.FilterStatus = SalesFilterStatuses.ReadyForSale;

            var transfer = new SalesRequestNameTransferDTO
            {
                SaleRequestId = row.Id,
                FromEmployeeId = fromId,
                FromEmployeeName = fromName,
                ToEmployeeId = peer.EmployeeId,
                ToEmployeeName = peer.EmployeeName,
                TransferReason = reason,
                TransferredAtUtc = now,
                TransferredByUserId = actor.EmployeeId,
                TransferredByName = actor.EmployeeName
            };
            await _repo.InsertNameTransferAsync(transfer, ct);
            await AppendHistoryAsync(
                row,
                SalesRequestEvents.NameTransferred,
                actor,
                $"من {fromName} إلى {peer.EmployeeName}: {reason}",
                ct,
                previous);
            return SanitizeForSalesEmployee(await HydrateAsync(row, ct));
        }

        public async Task<SalesRequestDTO> ManagerUpdateAsync(
            SalesIdentity manager,
            int id,
            SalesRequestManagerUpdateDTO body,
            CancellationToken ct)
        {
            EnsureManager(manager);
            await _repo.EnsureSchemaAsync(ct);
            var row = await _repo.GetByIdAsync(id, ct)
                      ?? throw new SalesCompleteException(StatusCodes.Status404NotFound, "الطلب غير موجود.");

            // Completed/sold requests may still receive identity corrections (name/phone/address/notes).
            // Province transfer and CityValue moves stay blocked elsewhere.
            var previousProvince = row.CustomerProvince;
            var originalName = row.CustomerName;
            var originalPhone = row.CustomerPhone;
            var originalAddress = row.CustomerAddress;
            var notes = new List<string>();
            var identityChanged = false;

            if (body.CustomerName is not null)
            {
                var name = body.CustomerName.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new SalesCompleteException(StatusCodes.Status400BadRequest, "اسم الزبون مطلوب.");
                }

                if (!string.Equals(row.CustomerName, name, StringComparison.Ordinal))
                {
                    notes.Add($"الاسم: {row.CustomerName} ← {name}");
                    row.CustomerName = name;
                    identityChanged = true;
                }
            }

            if (body.Phone is not null)
            {
                if (string.IsNullOrWhiteSpace(body.Phone))
                {
                    if (!string.IsNullOrWhiteSpace(row.CustomerPhone))
                    {
                        notes.Add($"الهاتف: {row.CustomerPhone} ← (فارغ)");
                        identityChanged = true;
                    }

                    row.CustomerPhone = null;
                }
                else
                {
                    SalesPhoneNormalizer.RequireValidIfPresent(body.Phone);
                    var phone = SalesPhoneNormalizer.ForStorage(body.Phone);
                    if (!string.Equals(row.CustomerPhone, phone, StringComparison.Ordinal))
                    {
                        notes.Add($"الهاتف: {row.CustomerPhone} ← {phone}");
                        row.CustomerPhone = phone;
                        identityChanged = true;
                    }
                }
            }

            if (body.Address is not null)
            {
                var address = string.IsNullOrWhiteSpace(body.Address) ? null : body.Address.Trim();
                if (!string.Equals(row.CustomerAddress, address, StringComparison.Ordinal))
                {
                    notes.Add("تعديل العنوان");
                    row.CustomerAddress = address;
                    identityChanged = true;
                }
            }

            if (body.Notes is not null)
            {
                row.Notes = string.IsNullOrWhiteSpace(body.Notes) ? null : body.Notes.Trim();
            }

            if (body.SaleType is not null)
            {
                var saleType = body.SaleType.Trim();
                row.Notes = string.IsNullOrWhiteSpace(saleType)
                    ? row.Notes
                    : (row.Notes?.Contains("نوع المبيع:", StringComparison.Ordinal) == true
                        ? row.Notes
                        : string.IsNullOrWhiteSpace(row.Notes) ? $"نوع المبيع: {saleType}" : $"{row.Notes}\nنوع المبيع: {saleType}");
            }

            if (body.Province is not null)
            {
                row.CustomerProvince = string.IsNullOrWhiteSpace(body.Province) ? null : body.Province.Trim();
            }

            // CityValue is the branch tenancy stamp. Cross-branch moves must use province transfer.
            if (!string.IsNullOrWhiteSpace(body.CityValue))
            {
                var city = body.CityValue.Trim();
                if (!string.Equals(row.CityValue, city, StringComparison.OrdinalIgnoreCase))
                {
                    throw new SalesCompleteException(
                        StatusCodes.Status409Conflict,
                        "لا يمكن تغيير محافظة الطلب داخل نفس قاعدة الفرع. استخدم نقل المحافظة إلى فرع آخر.");
                }
            }

            if (body.CityName is not null)
            {
                row.CityName = string.IsNullOrWhiteSpace(body.CityName) ? row.CityName : body.CityName.Trim();
            }

            await _repo.UpdateAsync(row, ct);

            if (identityChanged)
            {
                await PropagateCustomerIdentityAsync(
                    row,
                    originalName,
                    originalPhone,
                    originalAddress,
                    ct);
            }

            var provinceLabelChanged = !string.Equals(previousProvince, row.CustomerProvince, StringComparison.OrdinalIgnoreCase);
            var eventType = provinceLabelChanged
                ? SalesRequestEvents.ManagerProvinceChanged
                : SalesRequestEvents.ManagerRequestEdited;
            await AppendHistoryAsync(
                row,
                eventType,
                manager,
                notes.Count == 0
                    ? (provinceLabelChanged
                        ? $"المحافظة: {previousProvince} ← {row.CustomerProvince}"
                        : "تعديل طلب البيع")
                    : string.Join(" | ", notes),
                ct,
                row.Status);

            return await HydrateAsync(row, ct);
        }

        /// <summary>
        /// Push identity corrections from a sales-request edit onto Customers / drafts / sibling requests
        /// so a completed sale shows the same name everywhere in the branch.
        /// </summary>
        private async Task PropagateCustomerIdentityAsync(
            SalesRequestDTO row,
            string? originalName,
            string? originalPhone,
            string? originalAddress,
            CancellationToken ct)
        {
            var cs = _guard?.GetSalesConnectionString();
            if (string.IsNullOrWhiteSpace(cs))
            {
                // Unit tests / environments without a live branch DB — request row already updated.
                return;
            }

            var name = (row.CustomerName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            var phone = string.IsNullOrWhiteSpace(row.CustomerPhone) ? null : row.CustomerPhone.Trim();
            var address = string.IsNullOrWhiteSpace(row.CustomerAddress) ? null : row.CustomerAddress.Trim();

            try
            {
                await using var connection = new SqlConnection(cs);
                await connection.OpenAsync(ct);

                var customerId = row.ExistingCustomerId is > 0 ? row.ExistingCustomerId : null;
                if (customerId is null && row.ConvertedToSaleId is > 0)
                {
                    customerId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(@"
-- SM_COMPLETED_IDENTITY_PROPAGATE_V1
IF OBJECT_ID(N'dbo.SalesDrafts', N'U') IS NULL SELECT CAST(NULL AS INT);
ELSE SELECT TOP 1 NULLIF(CustomerId, 0) FROM dbo.SalesDrafts WHERE SaleId = @SaleId;",
                        new { SaleId = row.ConvertedToSaleId.Value }, cancellationToken: ct));
                }

                if (customerId is > 0)
                {
                    await connection.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'dbo.Customers', N'U') IS NOT NULL
UPDATE dbo.Customers SET
  CustomerName = @CustomerName,
  PhoneNumber = COALESCE(@PhoneNumber, PhoneNumber),
  Address = COALESCE(@Address, Address)
WHERE CustomerID = @CustomerID;",
                        new
                        {
                            CustomerID = customerId.Value,
                            CustomerName = Trunc(name, 255),
                            PhoneNumber = Trunc(phone, 255),
                            Address = Trunc(address, 255)
                        },
                        cancellationToken: ct));

                    // Keep sibling requests (open + completed) aligned for the same customer.
                    await connection.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'dbo.SalesRequests', N'U') IS NOT NULL
UPDATE dbo.SalesRequests SET
  CustomerName = @CustomerName,
  CustomerPhone = COALESCE(@CustomerPhone, CustomerPhone),
  CustomerAddress = COALESCE(@CustomerAddress, CustomerAddress)
WHERE ExistingCustomerId = @CustomerId
   OR (ConvertedToSaleId IS NOT NULL AND ConvertedToSaleId IN (
        SELECT SaleId FROM dbo.SalesDrafts WHERE CustomerId = @CustomerId));",
                        new
                        {
                            CustomerId = customerId.Value,
                            CustomerName = Trunc(name, 200),
                            CustomerPhone = Trunc(phone, 50),
                            CustomerAddress = Trunc(address, 255)
                        },
                        cancellationToken: ct));
                }

                if (row.ConvertedToSaleId is > 0)
                {
                    await connection.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'dbo.SalesDrafts', N'U') IS NOT NULL
UPDATE dbo.SalesDrafts SET
  FullName = @FullName,
  Phone = COALESCE(@Phone, Phone),
  Address = COALESCE(@Address, Address)
WHERE SaleId = @SaleId;",
                        new
                        {
                            SaleId = row.ConvertedToSaleId.Value,
                            FullName = Trunc(name, 100),
                            Phone = Trunc(phone, 50),
                            Address = Trunc(address, 255)
                        },
                        cancellationToken: ct));

                    await connection.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'dbo.SalesShopProfiles', N'U') IS NOT NULL
UPDATE dbo.SalesShopProfiles SET
  CustomerName = @CustomerName,
  CustomerPhone = COALESCE(@CustomerPhone, CustomerPhone)
WHERE SaleId = @SaleId;",
                        new
                        {
                            SaleId = row.ConvertedToSaleId.Value,
                            CustomerName = Trunc(name, 200),
                            CustomerPhone = Trunc(phone, 50)
                        },
                        cancellationToken: ct));
                }

                if (customerId is > 0)
                {
                    await connection.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'dbo.SalesDrafts', N'U') IS NOT NULL
UPDATE dbo.SalesDrafts SET
  FullName = @FullName,
  Phone = COALESCE(@Phone, Phone),
  Address = COALESCE(@Address, Address)
WHERE CustomerId = @CustomerId;",
                        new
                        {
                            CustomerId = customerId.Value,
                            FullName = Trunc(name, 100),
                            Phone = Trunc(phone, 50),
                            Address = Trunc(address, 255)
                        },
                        cancellationToken: ct));

                    await connection.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'dbo.SalesShopProfiles', N'U') IS NOT NULL
UPDATE dbo.SalesShopProfiles SET
  CustomerName = @CustomerName,
  CustomerPhone = COALESCE(@CustomerPhone, CustomerPhone)
WHERE CustomerId = @CustomerId;",
                        new
                        {
                            CustomerId = customerId.Value,
                            CustomerName = Trunc(name, 200),
                            CustomerPhone = Trunc(phone, 50)
                        },
                        cancellationToken: ct));

                    // Official posted sale header when present.
                    await connection.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'dbo.CustomersSales', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.CustomersSales', N'CustomerName') IS NOT NULL
UPDATE dbo.CustomersSales SET CustomerName = @CustomerName
WHERE CustomerID = @CustomerId;",
                        new
                        {
                            CustomerId = customerId.Value,
                            CustomerName = Trunc(name, 100)
                        },
                        cancellationToken: ct));
                }

                // Fallback: no customer id — still sync drafts matched by previous identity keys.
                if (customerId is null or <= 0
                    && (!string.IsNullOrWhiteSpace(originalName) || !string.IsNullOrWhiteSpace(originalPhone)))
                {
                    await connection.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'dbo.SalesDrafts', N'U') IS NOT NULL
UPDATE dbo.SalesDrafts SET
  FullName = @FullName,
  Phone = COALESCE(@Phone, Phone),
  Address = COALESCE(@Address, Address)
WHERE (@OriginalPhone IS NOT NULL AND LTRIM(RTRIM(ISNULL(Phone, N''))) = @OriginalPhone)
   OR (@OriginalName IS NOT NULL AND LTRIM(RTRIM(ISNULL(FullName, N''))) = @OriginalName
       AND (@OriginalPhone IS NULL OR LTRIM(RTRIM(ISNULL(Phone, N''))) = @OriginalPhone));",
                        new
                        {
                            FullName = Trunc(name, 100),
                            Phone = Trunc(phone, 50),
                            Address = Trunc(address, 255),
                            OriginalName = string.IsNullOrWhiteSpace(originalName) ? null : originalName.Trim(),
                            OriginalPhone = string.IsNullOrWhiteSpace(originalPhone) ? null : originalPhone.Trim()
                        },
                        cancellationToken: ct));
                }

                _ = originalAddress; // reserved for future address-keyed matching
            }
            catch
            {
                // Identity sync must not fail the manager edit of the request itself.
            }
        }

        private static string Trunc(string? value, int max)
        {
            var text = value?.Trim() ?? string.Empty;
            if (text.Length <= max)
            {
                return text;
            }

            return text[..max];
        }

        public async Task<SalesRequestDTO> AcceptProvinceTransferAsync(
            SalesIdentity manager,
            SalesRequestAcceptTransferDTO body,
            CancellationToken ct)
        {
            EnsureManager(manager);
            if (body.FromRequestId <= 0 || string.IsNullOrWhiteSpace(body.FromCityValue))
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "بيانات النقل غير مكتملة.");
            }

            var name = body.CustomerName?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "اسم الزبون مطلوب.");
            }

            await _repo.EnsureSchemaAsync(ct);

            var assignEmployeeId = body.ToEmployeeId is > 0 ? body.ToEmployeeId.Value : 0;
            string? assignEmployeeName = null;
            if (assignEmployeeId > 0)
            {
                if (_employees is null)
                {
                    throw new SalesCompleteException(StatusCodes.Status503ServiceUnavailable, "تعذر التحقق من موظفي الفرع الهدف.");
                }

                var peers = await _employees.ListActiveSalesEmployeesAsync(ct);
                var peer = peers.FirstOrDefault(e => e.EmployeeId == assignEmployeeId)
                           ?? throw new SalesCompleteException(StatusCodes.Status400BadRequest, "الموظف المحدد غير موجود في الفرع الهدف.");
                assignEmployeeName = string.IsNullOrWhiteSpace(body.ToEmployeeName) ? peer.EmployeeName : body.ToEmployeeName.Trim();
            }

            string? phone = null;
            if (!string.IsNullOrWhiteSpace(body.CustomerPhone))
            {
                SalesPhoneNormalizer.RequireValidIfPresent(body.CustomerPhone);
                phone = SalesPhoneNormalizer.ForStorage(body.CustomerPhone);
            }

            var now = _clock.UtcNow;
            var row = new SalesRequestDTO
            {
                CreatedByUserId = manager.EmployeeId,
                CreatedByName = string.IsNullOrWhiteSpace(body.CreatedByName) ? manager.EmployeeName : body.CreatedByName,
                CreatedByUserType = string.IsNullOrWhiteSpace(body.CreatedByUserType) ? manager.UserType : body.CreatedByUserType,
                TargetEmployeeId = assignEmployeeId,
                TargetEmployeeName = assignEmployeeId > 0 ? assignEmployeeName : null,
                CityValue = manager.BranchId,
                CityName = string.IsNullOrWhiteSpace(body.CityName) ? manager.BranchName : body.CityName.Trim(),
                CustomerSourceType = string.IsNullOrWhiteSpace(body.CustomerSourceType)
                    ? SalesRequestSources.NewCustomer
                    : body.CustomerSourceType.Trim(),
                ExistingCustomerId = body.ExistingCustomerId is > 0 ? body.ExistingCustomerId : null,
                CustomerSourceCityValue = body.CustomerSourceCityValue,
                CustomerName = name,
                CustomerPhone = phone,
                CustomerProvince = string.IsNullOrWhiteSpace(body.CustomerProvince) ? manager.BranchName : body.CustomerProvince.Trim(),
                CustomerAddress = string.IsNullOrWhiteSpace(body.CustomerAddress) ? null : body.CustomerAddress.Trim(),
                Notes = string.IsNullOrWhiteSpace(body.Notes) ? null : body.Notes.Trim(),
                SaleRequestType = body.SaleRequestType,
                SourceListId = body.SourceListId,
                Status = assignEmployeeId > 0 ? SalesRequestStatuses.Assigned : SalesRequestStatuses.New,
                FilterStatus = SalesFilterStatuses.ReadyForSale,
                AssignedAtUtc = assignEmployeeId > 0 ? now : null,
                CreatedAtUtc = now
            };

            var saved = await _repo.InsertAsync(row, ct);
            await AppendHistoryAsync(
                saved,
                SalesRequestEvents.ProvinceTransferred,
                manager,
                $"نقل وارد من {body.FromCityValue}#{body.FromRequestId} إلى {saved.CityValue}#{saved.Id}"
                + (string.IsNullOrWhiteSpace(body.FromCityName) ? "" : $" ({body.FromCityName})"),
                ct);
            return await HydrateAsync(saved, ct);
        }

        public async Task MarkProvinceTransferredOutAsync(
            SalesIdentity manager,
            int id,
            SalesRequestMarkTransferredOutDTO body,
            CancellationToken ct)
        {
            EnsureManager(manager);
            if (string.IsNullOrWhiteSpace(body.ToCityValue) || body.ToRequestId <= 0)
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "بيانات وجهة النقل مطلوبة.");
            }

            await _repo.EnsureSchemaAsync(ct);
            var row = await _repo.GetByIdIncludingDeletedAsync(id, ct)
                      ?? throw new SalesCompleteException(StatusCodes.Status404NotFound, "الطلب غير موجود.");

            if (SalesRequestStatuses.IsSold(row.Status) || row.ConvertedToSaleId is > 0)
            {
                throw new SalesCompleteException(
                    StatusCodes.Status409Conflict,
                    "لا يمكن نقل طلب مرتبط بعملية بيع مكتملة أو محوّلة.");
            }

            if (row.IsDeleted)
            {
                // Idempotent: already archived after a prior successful transfer.
                return;
            }

            var fromCity = row.CityValue;
            var previousAssignee = row.TargetEmployeeId;
            var previousAssigneeName = row.TargetEmployeeName;
            row.TargetEmployeeId = 0;
            row.TargetEmployeeName = null;
            row.AssignedAtUtc = null;
            row.IsDeleted = true;
            row.DeletedAtUtc = _clock.UtcNow;
            row.DeletedByUserId = manager.EmployeeId;
            row.DeletedByName = manager.EmployeeName;
            row.DeleteReason = $"نقل محافظة إلى {body.ToCityValue.Trim()}";
            await _repo.UpdateAsync(row, ct);
            await AppendHistoryAsync(
                row,
                SalesRequestEvents.ProvinceTransferred,
                manager,
                $"ProvinceTransferred FromCity={fromCity} ToCity={body.ToCityValue.Trim()} "
                + $"OriginalRequestId={id} DestinationRequestId={body.ToRequestId} "
                + $"ClearedAssignee={previousAssignee}:{previousAssigneeName}",
                ct,
                row.Status);
        }

        public async Task<SalesRequestDTO> ManagerReassignAsync(
            SalesIdentity manager,
            int id,
            SalesRequestManagerReassignDTO body,
            CancellationToken ct)
        {
            EnsureManager(manager);
            if (body.EmployeeId <= 0)
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "يجب اختيار موظف المبيعات.");
            }

            // Reuse AssignAsync business rules (PendingFilter gate, history, ownership).
            return await AssignAsync(manager, id, new SalesRequestAssignDTO
            {
                EmployeeId = body.EmployeeId,
                EmployeeName = body.EmployeeName
            }, ct);
        }

        public async Task SoftDeleteAsync(SalesIdentity manager, int id, string reason, CancellationToken ct) =>
            await SoftDeleteAsync(manager, id, reason, confirmCascadePayments: false, ct);

        public async Task SoftDeleteAsync(
            SalesIdentity manager,
            int id,
            string reason,
            bool confirmCascadePayments,
            CancellationToken ct)
        {
            EnsureManager(manager);
            var trimmed = reason?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "سبب الحذف مطلوب.");
            }

            await _repo.EnsureSchemaAsync(ct);
            var row = await _repo.GetByIdAsync(id, ct)
                      ?? throw new SalesCompleteException(StatusCodes.Status404NotFound, "الطلب غير موجود.");

            if (row.IsDeleted)
            {
                return;
            }

            var linked = SalesRequestStatuses.IsSold(row.Status) || row.ConvertedToSaleId is > 0;
            if (linked)
            {
                if (_cascadeVoid == null)
                {
                    throw new SalesCompleteException(
                        StatusCodes.Status500InternalServerError,
                        "خدمة التعويض المالي غير مهيأة. تعذر حذف طلب مرتبط بمبيع.");
                }

                await _cascadeVoid.VoidLinkedAsync(manager, row, confirmCascadePayments, trimmed, ct);
            }

            var previous = row.Status;
            row.IsDeleted = true;
            row.DeletedAtUtc = _clock.UtcNow;
            row.DeletedByUserId = manager.EmployeeId;
            row.DeletedByName = manager.EmployeeName;
            row.DeleteReason = trimmed;
            // Follower inbox: soft-delete must surface under «مرفوض» with the note (not stay «قيد العمل»).
            if (!SalesRequestStatuses.IsSold(row.Status) && row.Status != SalesRequestStatuses.Completed)
            {
                row.Status = SalesRequestStatuses.Rejected;
                row.RejectedAtUtc = _clock.UtcNow;
                row.RejectionReason = trimmed;
            }

            await _repo.UpdateAsync(row, ct);
            if (previous != row.Status && row.Status == SalesRequestStatuses.Rejected)
            {
                await AppendHistoryAsync(row, SalesRequestEvents.Rejected, manager, trimmed, ct, previous);
                await AppendHistoryAsync(row, SalesRequestEvents.RejectionReason, manager, trimmed, ct, previous);
            }

            await AppendHistoryAsync(
                row,
                SalesRequestEvents.ManagerRequestDeleted,
                manager,
                trimmed,
                ct,
                previous);
            await NotifyCrmIfLinkedAsync(row, "Cancelled", trimmed, ct);
        }

        public async Task<SalesRequestSoftDeleteImpactDTO> GetSoftDeleteImpactAsync(
            SalesIdentity manager,
            int id,
            CancellationToken ct)
        {
            EnsureManager(manager);
            await _repo.EnsureSchemaAsync(ct);
            var row = await _repo.GetByIdAsync(id, ct)
                      ?? throw new SalesCompleteException(StatusCodes.Status404NotFound, "الطلب غير موجود.");

            if (_cascadeVoid == null || (row.ConvertedToSaleId is not > 0 && !SalesRequestStatuses.IsSold(row.Status)))
            {
                return new SalesRequestSoftDeleteImpactDTO
                {
                    RequestId = row.Id,
                    WarningAr = "سيتم نقل الطلب إلى تبويب المحذوفين مع الاحتفاظ بكل البيانات."
                };
            }

            return await _cascadeVoid.GetImpactAsync(row, ct);
        }

        public async Task<SalesRequestSoftDeleteImpactDTO> GetCancelImpactAsync(
            SalesIdentity manager,
            int id,
            CancellationToken ct)
        {
            EnsureManager(manager);
            await _repo.EnsureSchemaAsync(ct);
            var row = await _repo.GetByIdAsync(id, ct)
                      ?? throw new SalesCompleteException(StatusCodes.Status404NotFound, "الطلب غير موجود.");

            if (row.IsDeleted)
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "الطلب محذوف ولا يمكن إلغاؤه.");
            }

            if (!SalesRequestStatuses.CanCancelToUnassigned(row.Status))
            {
                throw new SalesCompleteException(
                    StatusCodes.Status400BadRequest,
                    "لا يمكن إلغاء طلب مرفوض. استخدم الإرجاع أو الحذف حسب الحاجة.");
            }

            if (_cascadeVoid == null || (row.ConvertedToSaleId is not > 0 && !SalesRequestStatuses.IsSold(row.Status)))
            {
                return new SalesRequestSoftDeleteImpactDTO
                {
                    RequestId = row.Id,
                    WarningAr = "سيتم إلغاء الطلب وإعادته إلى غير المسند وإخفاؤه من تطبيق موظف المبيعات."
                };
            }

            var impact = await _cascadeVoid.GetImpactAsync(row, ct);
            impact.WarningAr = (impact.WarningAr ?? "").Trim();
            if (string.IsNullOrWhiteSpace(impact.WarningAr))
            {
                impact.WarningAr = "سيتم إلغاء المبيع وإرجاع الكميات ثم إعادة الطلب إلى غير المسند.";
            }
            else
            {
                impact.WarningAr += " ثم يُعاد الطلب إلى غير المسند (وليس المحذوفين).";
            }

            return impact;
        }

        public async Task<SalesRequestDTO> CancelAndUnassignAsync(
            SalesIdentity manager,
            int id,
            string reason,
            bool confirmCascadePayments,
            CancellationToken ct)
        {
            EnsureManager(manager);
            var trimmed = reason?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "سبب الإلغاء مطلوب.");
            }

            await _repo.EnsureSchemaAsync(ct);
            var row = await _repo.GetByIdAsync(id, ct)
                      ?? throw new SalesCompleteException(StatusCodes.Status404NotFound, "الطلب غير موجود.");

            if (row.IsDeleted)
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "الطلب محذوف ولا يمكن إلغاؤه.");
            }

            if (!SalesRequestStatuses.CanCancelToUnassigned(row.Status))
            {
                throw new SalesCompleteException(
                    StatusCodes.Status400BadRequest,
                    "لا يمكن إلغاء طلب مرفوض.");
            }

            var previous = row.Status;
            var previousAssignee = row.TargetEmployeeId;
            var previousAssigneeName = row.TargetEmployeeName;
            var linked = SalesRequestStatuses.IsSold(row.Status) || row.ConvertedToSaleId is > 0;
            if (linked)
            {
                if (_cascadeVoid == null)
                {
                    throw new SalesCompleteException(
                        StatusCodes.Status500InternalServerError,
                        "خدمة التعويض المالي غير مهيأة. تعذر إلغاء طلب مرتبط بمبيع.");
                }

                await _cascadeVoid.VoidLinkedAsync(manager, row, confirmCascadePayments, trimmed, ct);
            }

            // Reset to غير مسند — keep customer fields; clear assignee + workflow terminal fields.
            row.Status = SalesRequestStatuses.New;
            row.TargetEmployeeId = 0;
            row.TargetEmployeeName = null;
            row.AssignedAtUtc = null;
            row.AssignedByUserId = null;
            row.AssignedByName = null;
            row.ViewedAtUtc = null;
            row.ProcessingAtUtc = null;
            row.ConvertedToSaleId = null;
            row.CompletedAtUtc = null;
            row.RejectedAtUtc = null;
            row.RejectionReason = null;
            row.PendingNote = null;
            row.PreparedForSaleNote = null;
            row.ReturnNote = null;
            row.FilterStatus = null;
            row.FilteredByUserId = null;
            row.FilteredByUserName = null;
            row.FilterNote = null;
            row.FilterRejectReason = null;
            row.FilteredAtUtc = null;
            row.ExceptionHoldStatus = null;
            row.ActiveExceptionId = null;
            row.IsDeleted = false;
            row.DeletedAtUtc = null;
            row.DeletedByUserId = null;
            row.DeletedByName = null;
            row.DeleteReason = null;

            await _repo.UpdateAsync(row, ct);
            await AppendHistoryAsync(
                row,
                SalesRequestEvents.CancelledToUnassigned,
                manager,
                $"سبب={trimmed}; PreviousStatus={previous}; ClearedAssignee={previousAssignee}:{previousAssigneeName}",
                ct,
                previous);

            // Close open legal cases so الزبائن القديمة / follower app do not keep orphans.
            if (_legalFollowups != null
                && string.Equals(previous, SalesRequestStatuses.LegalFollowup, StringComparison.OrdinalIgnoreCase))
            {
                await _legalFollowups.CancelOpenInProgressForSalesRequestAsync(
                    id, trimmed, manager.EmployeeId, manager.EmployeeName, ct);
            }

            return (await _repo.GetByIdAsync(id, ct))!;
        }

        public async Task<IReadOnlyList<SalesRequestDTO>> ListDeletedForManagerAsync(CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            // Legacy uncapped callers — hard cap so soft-deleted dumps cannot hang production.
            var (items, _) = await _repo.ListDeletedPageAsync(null, 0, 200, ct);
            return items;
        }

        private static void EnsureSalesEmployee(SalesIdentity actor)
        {
            if (!SalesRoles.IsSalesEmployee(actor.UserType)
                && !string.Equals(actor.Role, SalesRoles.SalesEmployee, StringComparison.Ordinal))
            {
                throw new SalesCompleteException(StatusCodes.Status403Forbidden, "غير مصرح.");
            }
        }

        private async Task<SalesRequestDTO> RequireOwned(int id, int employeeId, CancellationToken ct)
        {
            var row = await _repo.GetByIdAsync(id, ct)
                      ?? throw new SalesCompleteException(StatusCodes.Status404NotFound, "طلب المبيع غير موجود.");
            if (row.TargetEmployeeId <= 0 || row.TargetEmployeeId != employeeId)
            {
                throw new SalesCompleteException(StatusCodes.Status403Forbidden, "لا يمكنك الوصول إلى طلب موظف آخر.");
            }

            return row;
        }

        private static void EnsureManager(SalesIdentity manager)
        {
            if (!string.Equals(manager.Role, SalesRoles.SalesManager, StringComparison.Ordinal)
                && !SalesRoles.IsSalesManager(manager.UserType))
            {
                throw new SalesCompleteException(StatusCodes.Status403Forbidden, "غير مصرح.");
            }
        }

        private static void EnsureNotSold(SalesRequestDTO row)
        {
            if (SalesRequestStatuses.IsSold(row.Status))
            {
                throw new SalesCompleteException(StatusCodes.Status409Conflict, "لا يمكن تعديل طلب مكتمل.");
            }
        }

        private async Task AppendHistoryAsync(
            SalesRequestDTO row,
            string eventType,
            SalesIdentity actor,
            string? note,
            CancellationToken ct,
            string? previousStatus = null)
        {
            await _repo.InsertHistoryAsync(new SalesRequestHistoryDTO
            {
                RequestId = row.Id,
                Event = eventType,
                PreviousStatus = previousStatus,
                Status = row.Status,
                ActorUserId = actor.EmployeeId,
                ActorName = actor.EmployeeName,
                ActorType = actor.UserType ?? actor.Role,
                EmployeeId = row.TargetEmployeeId > 0 ? row.TargetEmployeeId : null,
                Note = note,
                CreatedAtUtc = _clock.UtcNow
            }, ct);
        }

        private static SalesIdentity EmployeeActor(int employeeId, SalesRequestDTO row) => new()
        {
            EmployeeId = employeeId,
            EmployeeName = row.TargetEmployeeName ?? "موظف مبيعات",
            BranchId = row.CityValue ?? string.Empty,
            BranchName = row.CityName ?? string.Empty,
            Role = SalesRoles.SalesEmployee,
            UserType = SalesRoles.UserTypeSalesEmployee
        };

        private async Task<SalesRequestDTO> HydrateAsync(SalesRequestDTO row, CancellationToken ct)
        {
            row.History = (await _repo.ListHistoryAsync(row.Id, ct)).ToList();
            row.NameTransfers = (await _repo.ListNameTransfersAsync(row.Id, ct)).ToList();
            row.LatestNameTransfer = row.NameTransfers.Count == 0
                ? null
                : row.NameTransfers[^1];
            return WithTimeline(row);
        }

        /// <summary>
        /// Hide WrongSale scare lines from notes the sales employee APK already displays as plain text.
        /// Does not mutate stored DB rows — response-only.
        /// </summary>
        public static SalesRequestDTO SanitizeForSalesEmployee(SalesRequestDTO row)
        {
            row.Notes = SanitizeEmployeeFacingNotes(row.Notes);
            return row;
        }

        public static string? SanitizeEmployeeFacingNotes(string? notes)
        {
            if (string.IsNullOrWhiteSpace(notes))
            {
                return notes;
            }

            var kept = notes
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Select(static l => l.TrimEnd())
                .Where(static l =>
                {
                    var t = l.Trim();
                    if (t.Length == 0)
                    {
                        return true;
                    }

                    if (t.Contains("تقييم المتجر", StringComparison.Ordinal)
                        && t.Contains("مبيعات خاطئة", StringComparison.Ordinal))
                    {
                        return false;
                    }

                    if (string.Equals(t, "مبيعات خاطئة", StringComparison.Ordinal)
                        || string.Equals(t, "تقييم المتجر", StringComparison.Ordinal))
                    {
                        return false;
                    }

                    return true;
                });
            var joined = string.Join("\n", kept).Trim();
            return joined.Length == 0 ? null : joined;
        }

        internal static SalesRequestDTO WithTimeline(SalesRequestDTO row)
        {
            if (row.History.Count > 0)
            {
                row.Timeline = row.History.Select(h => new SalesRequestTimelineItemDTO
                {
                    Event = h.Event,
                    AtUtc = h.CreatedAtUtc,
                    Detail = h.Note
                }).ToList();
                return row;
            }

            row.Timeline =
            [
                new() { Event = "Created", AtUtc = row.CreatedAtUtc },
                new() { Event = "Assigned", AtUtc = row.AssignedAtUtc },
                new() { Event = "Viewed", AtUtc = row.ViewedAtUtc },
                new() { Event = "PreparedForSale", AtUtc = row.ProcessingAtUtc },
                new() { Event = "Converted", AtUtc = row.ConvertedToSaleId == null ? null : row.ProcessingAtUtc, Detail = row.ConvertedToSaleId?.ToString() },
                new() { Event = row.Status == SalesRequestStatuses.Rejected ? "Rejected" : "Completed", AtUtc = row.RejectedAtUtc ?? row.CompletedAtUtc, Detail = row.RejectionReason }
            ];
            return row;
        }
    }
}
