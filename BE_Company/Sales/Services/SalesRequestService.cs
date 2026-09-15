using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Filtering;
using BE_Company.Sales.Models;

namespace BE_Company.Sales.Services
{
    public sealed class SalesRequestService : ISalesRequestService
    {
        private readonly ISalesRequestRepository _repo;
        private readonly IIraqClock _clock;
        private readonly ISalesManagerReadRepository? _employees;

        public SalesRequestService(
            ISalesRequestRepository repo,
            IIraqClock clock,
            ISalesManagerReadRepository? employees = null)
        {
            _repo = repo;
            _clock = clock;
            _employees = employees;
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
                Status = SalesRequestStatuses.New,
                FilterStatus = SalesFilterStatuses.PendingFilter,
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
                FilterStatus = SalesFilterStatuses.PendingFilter,
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
                FilterStatus = SalesFilterStatuses.PendingFilter,
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
            var rows = await _repo.ListAsync(null, null, null, null, ct);
            return rows.Count(r => SalesRequestSources.IsEmployeeSubmitted(r.CustomerSourceType) && r.ManagerReadAtUtc == null);
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
            await _repo.UpdateAsync(row, ct);
            await AppendHistoryAsync(row, SalesRequestEvents.Rejected, manager, trimmed, ct, previous);
            await AppendHistoryAsync(row, SalesRequestEvents.RejectionReason, manager, trimmed, ct, previous);
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

        public async Task<IReadOnlyList<SalesRequestDTO>> ListForManagerAsync(string? status, int? employeeId, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            var rows = await _repo.ListAsync(employeeId, status, fromUtc, toUtc, ct);
            var result = new List<SalesRequestDTO>();
            foreach (var row in rows)
            {
                // Backend-enforce: held New/unassigned requests never appear in manager lists (غير مسند).
                if (SalesExceptionHoldStatuses.IsHeldUnassigned(row))
                {
                    continue;
                }

                result.Add(await HydrateAsync(row, ct));
            }

            return result;
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
            // Server-side gate: only ReadyForSale reaches the sales employee app.
            var rows = await _repo.ListAsync(employeeId, null, null, null, ct, SalesFilterStatuses.ReadyForSale);
            var result = new List<SalesRequestDTO>();
            foreach (var row in rows.Where(r => r.TargetEmployeeId == employeeId && r.TargetEmployeeId > 0))
            {
                result.Add(await HydrateAsync(row, ct));
            }

            return result;
        }

        public async Task<SalesRequestDTO> GetForEmployeeAsync(int id, int employeeId, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            var row = await RequireOwned(id, employeeId, ct);
            return await HydrateAsync(row, ct);
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

            return await HydrateAsync(row, ct);
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

            return await HydrateAsync(row, ct);
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
            return await HydrateAsync(row, ct);
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
            await _repo.UpdateAsync(row, ct);
            var actor = EmployeeActor(employeeId, row);
            await AppendHistoryAsync(row, SalesRequestEvents.Rejected, actor, trimmed, ct, previous);
            await AppendHistoryAsync(row, SalesRequestEvents.RejectionReason, actor, trimmed, ct, previous);
            return await HydrateAsync(row, ct);
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

            var employeeName = request.EmployeeName;
            if (_employees != null && string.IsNullOrWhiteSpace(employeeName))
            {
                var match = (await _employees.ListEmployeesAsync(ct))
                    .FirstOrDefault(e => e.EmployeeId == request.EmployeeId);
                employeeName = match?.EmployeeName;
            }

            row.TargetEmployeeId = request.EmployeeId;
            row.TargetEmployeeName = employeeName;
            if (!string.IsNullOrWhiteSpace(request.CityValue))
            {
                row.CityValue = request.CityValue.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.CityName))
            {
                row.CityName = request.CityName.Trim();
            }

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

            row.Status = SalesRequestStatuses.Assigned;
            row.AssignedAtUtc = _clock.UtcNow;
            row.AssignedByUserId = manager.EmployeeId;
            row.AssignedByName = string.IsNullOrWhiteSpace(manager.EmployeeName) ? "مدير المبيعات" : manager.EmployeeName;
            // New assignments enter filter queue — not visible to sales employee until ReadyForSale.
            row.FilterStatus = SalesFilterStatuses.PendingFilter;
            row.FilteredByUserId = null;
            row.FilterNote = null;
            row.FilterRejectReason = null;
            row.FilteredAtUtc = null;
            await _repo.UpdateAsync(row, ct);
            await AppendHistoryAsync(row, SalesRequestEvents.Assigned, manager, null, ct);
            return await HydrateAsync(row, ct);
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
                throw new SalesCompleteException(StatusCodes.Status409Conflict, "لا يوجد موظف مسند لإعادة الطلب إليه.");
            }

            var trimmed = note.Trim();
            var previousReason = row.RejectionReason;
            var previous = row.Status;
            row.Status = SalesRequestStatuses.Returned;
            row.ReturnNote = trimmed;
            // Returned work must be visible again to the assigned sales employee.
            row.FilterStatus = SalesFilterStatuses.ReadyForSale;
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
                return await HydrateAsync(row, ct);
            }

            var previous = row.Status;
            row.Status = SalesRequestStatuses.Inspected;
            row.ViewedAtUtc ??= _clock.UtcNow;
            row.ProcessingAtUtc ??= _clock.UtcNow;
            await _repo.UpdateAsync(row, ct);
            await AppendHistoryAsync(row, SalesRequestEvents.Inspected, EmployeeActor(employeeId, row), null, ct, previous);
            return await HydrateAsync(row, ct);
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
            return await HydrateAsync(row, ct);
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
            EnsureNotSold(row);

            var previousProvince = row.CustomerProvince;
            var notes = new List<string>();

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
                }
            }

            if (body.Phone is not null)
            {
                if (string.IsNullOrWhiteSpace(body.Phone))
                {
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
                    }
                }
            }

            if (body.Address is not null)
            {
                row.CustomerAddress = string.IsNullOrWhiteSpace(body.Address) ? null : body.Address.Trim();
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
                FilterStatus = SalesFilterStatuses.PendingFilter,
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

        public async Task SoftDeleteAsync(SalesIdentity manager, int id, CancellationToken ct)
        {
            EnsureManager(manager);
            await _repo.EnsureSchemaAsync(ct);
            var row = await _repo.GetByIdAsync(id, ct)
                      ?? throw new SalesCompleteException(StatusCodes.Status404NotFound, "الطلب غير موجود.");

            if (SalesRequestStatuses.IsSold(row.Status) || row.ConvertedToSaleId is > 0)
            {
                throw new SalesCompleteException(
                    StatusCodes.Status409Conflict,
                    "لا يمكن حذف طلب مرتبط بعملية بيع مكتملة أو محوّلة. استخدم الأرشفة فقط للطلبات غير المالية.");
            }

            row.IsDeleted = true;
            row.DeletedAtUtc = _clock.UtcNow;
            row.DeletedByUserId = manager.EmployeeId;
            row.DeletedByName = manager.EmployeeName;
            await _repo.UpdateAsync(row, ct);
            await AppendHistoryAsync(
                row,
                SalesRequestEvents.ManagerRequestDeleted,
                manager,
                "حذف ناعم لطلب البيع",
                ct,
                row.Status);
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

            if (!SalesFilterStatuses.IsVisibleToSalesEmployee(row.FilterStatus))
            {
                throw new SalesCompleteException(StatusCodes.Status404NotFound, "طلب المبيع غير موجود.");
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
