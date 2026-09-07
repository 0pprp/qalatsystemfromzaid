using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
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
                SalesIraqPhone.RequireIfPresent(request.Customer?.Phone);
                phone = string.IsNullOrWhiteSpace(request.Customer?.Phone)
                    ? request.Customer?.Phone
                    : SalesIraqPhone.Normalize(request.Customer.Phone);
            }
            else
            {
                phone = string.IsNullOrWhiteSpace(request.Customer?.Phone)
                    ? request.Customer?.Phone
                    : request.Customer.Phone.Trim();
            }

            await _repo.EnsureSchemaAsync(ct);
            var row = new SalesRequestDTO
            {
                CreatedByUserId = actor.EmployeeId,
                CreatedByName = actor.EmployeeName,
                CreatedByUserType = actor.UserType,
                TargetEmployeeId = 0,
                TargetEmployeeName = null,
                CityValue = actor.BranchId,
                CityName = actor.BranchName,
                CustomerSourceType = request.ExistingCustomerId is > 0 ? "ExistingCustomer" : "NewCustomer",
                ExistingCustomerId = request.ExistingCustomerId is > 0 ? request.ExistingCustomerId : null,
                CustomerSourceCityValue = request.CustomerSourceCityValue,
                CustomerName = name ?? string.Empty,
                CustomerPhone = phone,
                CustomerProvince = request.Customer?.Province,
                CustomerAddress = request.Customer?.Address,
                Notes = request.Notes,
                Status = SalesRequestStatuses.New,
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

        public async Task<SalesRequestDTO> ManagerPrepareForSaleAsync(SalesIdentity manager, int id, CancellationToken ct)
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

            if (row.Status != SalesRequestStatuses.PreparedForSale)
            {
                var previous = row.Status;
                row.Status = SalesRequestStatuses.PreparedForSale;
                row.ProcessingAtUtc = _clock.UtcNow;
                row.ViewedAtUtc ??= row.ProcessingAtUtc;
                await _repo.UpdateAsync(row, ct);
                await AppendHistoryAsync(row, SalesRequestEvents.PreparedForSale, manager, null, ct, previous);
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
            var rows = await _repo.ListAsync(employeeId, null, null, null, ct);
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

        public Task<SalesRequestDTO> StartProcessingAsync(int id, int employeeId, CancellationToken ct) =>
            PrepareForSaleAsync(id, employeeId, ct);

        public async Task<SalesRequestDTO> PrepareForSaleAsync(int id, int employeeId, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            var row = await RequireOwned(id, employeeId, ct);
            EnsureNotSold(row);
            if (!SalesRequestStatuses.CanPrepare(row.Status))
            {
                throw new SalesCompleteException(StatusCodes.Status409Conflict, "لا يمكن تجهيز هذا الطلب.");
            }

            if (row.Status != SalesRequestStatuses.PreparedForSale)
            {
                var previous = row.Status;
                row.Status = SalesRequestStatuses.PreparedForSale;
                row.ProcessingAtUtc = _clock.UtcNow;
                row.ViewedAtUtc ??= row.ProcessingAtUtc;
                await _repo.UpdateAsync(row, ct);
                await AppendHistoryAsync(row, SalesRequestEvents.PreparedForSale, EmployeeActor(employeeId, row), null, ct, previous);
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

        public async Task<SalesRequestDTO> AssignAsync(SalesIdentity manager, int id, SalesRequestAssignDTO request, CancellationToken ct)
        {
            EnsureManager(manager);
            if (request.EmployeeId <= 0)
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "يجب اختيار موظف المبيعات.");
            }

            await _repo.EnsureSchemaAsync(ct);
            var row = await _repo.GetByIdAsync(id, ct)
                      ?? throw new SalesCompleteException(StatusCodes.Status404NotFound, "طلب المبيع غير موجود.");
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
            await _repo.UpdateAsync(row, ct);
            await AppendHistoryAsync(row, SalesRequestEvents.Assigned, manager, null, ct);
            return await HydrateAsync(row, ct);
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
