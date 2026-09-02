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

        public async Task<SalesRequestDTO> CreateAsync(SalesIdentity manager, SalesRequestCreateDTO request, CancellationToken ct)
        {
            if (!string.Equals(manager.Role, SalesRoles.SalesManager, StringComparison.Ordinal))
            {
                throw new SalesCompleteException(StatusCodes.Status403Forbidden, "غير مصرح.");
            }

            if (request.TargetEmployeeId <= 0)
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "يجب اختيار موظف المبيعات.");
            }

            var name = request.Customer?.FullName?.Trim();
            if (string.IsNullOrWhiteSpace(name) && request.ExistingCustomerId is null or <= 0)
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "اسم الزبون مطلوب.");
            }

            await _repo.EnsureSchemaAsync(ct);
            var employeeName = request.TargetEmployeeName;
            if (_employees != null && string.IsNullOrWhiteSpace(employeeName))
            {
                var match = (await _employees.ListEmployeesAsync(ct))
                    .FirstOrDefault(e => e.EmployeeId == request.TargetEmployeeId);
                employeeName = match?.EmployeeName;
            }

            var row = new SalesRequestDTO
            {
                CreatedByUserId = manager.EmployeeId,
                CreatedByName = manager.EmployeeName,
                CreatedByUserType = manager.UserType,
                TargetEmployeeId = request.TargetEmployeeId,
                TargetEmployeeName = employeeName,
                CityValue = manager.BranchId,
                CityName = manager.BranchName,
                CustomerSourceType = request.ExistingCustomerId is > 0 ? "ExistingCustomer" : "NewCustomer",
                ExistingCustomerId = request.ExistingCustomerId is > 0 ? request.ExistingCustomerId : null,
                CustomerSourceCityValue = request.CustomerSourceCityValue,
                CustomerName = name ?? string.Empty,
                CustomerPhone = request.Customer?.Phone,
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
            return WithTimeline(saved);
        }

        public async Task<IReadOnlyList<SalesRequestDTO>> ListForManagerAsync(string? status, int? employeeId, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            var rows = await _repo.ListAsync(employeeId, status, fromUtc, toUtc, ct);
            return rows.Select(WithTimeline).ToList();
        }

        public async Task<SalesRequestDTO?> GetForManagerAsync(int id, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            var row = await _repo.GetByIdAsync(id, ct);
            return row == null ? null : WithTimeline(row);
        }

        public async Task<IReadOnlyList<SalesRequestDTO>> ListForEmployeeAsync(int employeeId, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            var rows = await _repo.ListAsync(employeeId, null, null, null, ct);
            return rows.Select(WithTimeline).ToList();
        }

        public async Task<SalesRequestDTO> GetForEmployeeAsync(int id, int employeeId, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            var row = await RequireOwned(id, employeeId, ct);
            return WithTimeline(row);
        }

        public async Task<SalesRequestDTO> ViewAsync(int id, int employeeId, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            var row = await RequireOwned(id, employeeId, ct);
            if (row.Status == SalesRequestStatuses.New)
            {
                row.Status = SalesRequestStatuses.Viewed;
                row.ViewedAtUtc = _clock.UtcNow;
                await _repo.UpdateAsync(row, ct);
            }

            return WithTimeline(row);
        }

        public async Task<SalesRequestDTO> StartProcessingAsync(int id, int employeeId, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            var row = await RequireOwned(id, employeeId, ct);
            EnsureNotTerminal(row);
            if (row.Status is SalesRequestStatuses.New or SalesRequestStatuses.Viewed)
            {
                if (row.Status == SalesRequestStatuses.New)
                {
                    row.ViewedAtUtc ??= _clock.UtcNow;
                }

                row.Status = SalesRequestStatuses.InProgress;
                row.ProcessingAtUtc = _clock.UtcNow;
                await _repo.UpdateAsync(row, ct);
            }

            return WithTimeline(row);
        }

        public async Task<SalesRequestDTO> RejectAsync(int id, int employeeId, string reason, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "سبب الرفض مطلوب.");
            }

            await _repo.EnsureSchemaAsync(ct);
            var row = await RequireOwned(id, employeeId, ct);
            EnsureNotConverted(row);
            if (row.Status == SalesRequestStatuses.Rejected)
            {
                return WithTimeline(row);
            }

            EnsureNotTerminal(row);
            row.Status = SalesRequestStatuses.Rejected;
            row.RejectedAtUtc = _clock.UtcNow;
            row.RejectionReason = reason.Trim();
            await _repo.UpdateAsync(row, ct);
            return WithTimeline(row);
        }

        public async Task MarkConvertedAsync(int requestId, int employeeId, int saleId, DateTime utcNow, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            var row = await RequireOwned(requestId, employeeId, ct);
            if (row.Status == SalesRequestStatuses.Rejected)
            {
                throw new SalesCompleteException(StatusCodes.Status409Conflict, "لا يمكن تحويل طلب مرفوض إلى عملية بيع.");
            }

            if (row.Status == SalesRequestStatuses.ConvertedToSale || row.Status == SalesRequestStatuses.Completed)
            {
                if (row.ConvertedToSaleId == saleId)
                {
                    return;
                }

                throw new SalesCompleteException(StatusCodes.Status409Conflict, "الطلب مرتبط بعملية بيع أخرى.");
            }

            row.Status = SalesRequestStatuses.ConvertedToSale;
            row.ConvertedToSaleId = saleId;
            row.ProcessingAtUtc ??= utcNow;
            await _repo.UpdateAsync(row, ct);
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

            row.Status = SalesRequestStatuses.Completed;
            row.CompletedAtUtc = utcNow;
            await _repo.UpdateAsync(row, ct);
        }

        private async Task<SalesRequestDTO> RequireOwned(int id, int employeeId, CancellationToken ct)
        {
            var row = await _repo.GetByIdAsync(id, ct)
                      ?? throw new SalesCompleteException(StatusCodes.Status404NotFound, "طلب المبيع غير موجود.");
            if (row.TargetEmployeeId != employeeId)
            {
                throw new SalesCompleteException(StatusCodes.Status403Forbidden, "لا يمكنك الوصول إلى طلب موظف آخر.");
            }

            return row;
        }

        private static void EnsureNotConverted(SalesRequestDTO row)
        {
            if (row.Status is SalesRequestStatuses.ConvertedToSale or SalesRequestStatuses.Completed)
            {
                throw new SalesCompleteException(StatusCodes.Status409Conflict, "لا يمكن رفض طلب تحول إلى عملية بيع.");
            }
        }

        private static void EnsureNotTerminal(SalesRequestDTO row)
        {
            if (row.Status is SalesRequestStatuses.Rejected or SalesRequestStatuses.Completed)
            {
                throw new SalesCompleteException(StatusCodes.Status409Conflict, "لا يمكن تعديل هذا الطلب.");
            }
        }

        internal static SalesRequestDTO WithTimeline(SalesRequestDTO row)
        {
            row.Timeline =
            [
                new() { Event = "Created", AtUtc = row.CreatedAtUtc },
                new() { Event = "Viewed", AtUtc = row.ViewedAtUtc },
                new() { Event = "InProgress", AtUtc = row.ProcessingAtUtc },
                new() { Event = "Converted", AtUtc = row.ConvertedToSaleId == null ? null : row.ProcessingAtUtc, Detail = row.ConvertedToSaleId?.ToString() },
                new() { Event = row.Status == SalesRequestStatuses.Rejected ? "Rejected" : "Completed", AtUtc = row.RejectedAtUtc ?? row.CompletedAtUtc, Detail = row.RejectionReason }
            ];
            return row;
        }
    }
}
