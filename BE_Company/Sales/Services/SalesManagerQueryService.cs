using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;

namespace BE_Company.Sales.Services
{
    public sealed class SalesManagerQueryService
    {
        private readonly ISalesManagerReadRepository _read;
        private readonly ISalesRequestRepository _requests;
        private readonly IIraqClock _clock;
        private readonly SalesManagerTrackingOptions _options;
        private readonly IConfiguration _configuration;

        public SalesManagerQueryService(
            ISalesManagerReadRepository read,
            ISalesRequestRepository requests,
            IIraqClock clock,
            SalesManagerTrackingOptions options,
            IConfiguration configuration)
        {
            _read = read;
            _requests = requests;
            _clock = clock;
            _options = options;
            _configuration = configuration;
        }

        public async Task<IReadOnlyList<SalesManagerEmployeeDTO>> ListEmployeesAsync(
            string? shiftStatus,
            string? locationStatus,
            CancellationToken ct)
        {
            var cityValue = _configuration["SalesManagement:BranchId"] ?? "najaf-demo";
            var cityName = _configuration["SalesManagement:BranchName"] ?? "النجف";
            var utc = _clock.UtcNow;
            var rows = new List<SalesManagerEmployeeDTO>();
            foreach (var emp in await _read.ListEmployeesAsync(ct))
            {
                var dto = await MapEmployeeAsync(emp.EmployeeId, emp.EmployeeName, cityValue, cityName, utc, ct);
                if (!string.IsNullOrWhiteSpace(shiftStatus)
                    && !string.Equals(dto.ShiftStatus, shiftStatus, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(locationStatus)
                    && !string.Equals(dto.LocationStatus, locationStatus, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                rows.Add(dto);
            }

            return rows;
        }

        public async Task<SalesManagerEmployeeDTO?> GetEmployeeAsync(int employeeId, CancellationToken ct)
        {
            var emp = (await _read.ListEmployeesAsync(ct)).FirstOrDefault(e => e.EmployeeId == employeeId);
            if (emp == null)
            {
                return null;
            }

            var cityValue = _configuration["SalesManagement:BranchId"] ?? "najaf-demo";
            var cityName = _configuration["SalesManagement:BranchName"] ?? "النجف";
            return await MapEmployeeAsync(emp.EmployeeId, emp.EmployeeName, cityValue, cityName, _clock.UtcNow, ct);
        }

        public async Task<SalesManagerRouteDTO> GetRouteAsync(int employeeId, DateTime iraqDate, CancellationToken ct)
        {
            var emp = (await _read.ListEmployeesAsync(ct)).FirstOrDefault(e => e.EmployeeId == employeeId)
                      ?? throw new SalesCompleteException(StatusCodes.Status404NotFound, "الموظف غير موجود.");
            var shift = await _read.GetShiftForBusinessDateAsync(employeeId, iraqDate.Date, ct);
            DateTime fromUtc;
            DateTime toUtc;
            if (shift != null)
            {
                fromUtc = shift.StartedAtUtc;
                toUtc = shift.CutoffAtUtc;
            }
            else
            {
                var startIraq = iraqDate.Date.Add(IraqTimeService.CutoffTime);
                fromUtc = IraqTimeService.ToUtcFromIraq(startIraq);
                toUtc = IraqTimeService.ToUtcFromIraq(startIraq.AddDays(1));
            }

            var total = await _read.CountRouteAsync(employeeId, fromUtc, toUtc, ct);
            var points = await _read.GetRouteAsync(employeeId, fromUtc, toUtc, _options.MaxRoutePoints, ct);
            var cityName = _configuration["SalesManagement:BranchName"] ?? "النجف";
            return new SalesManagerRouteDTO
            {
                Employee = new SalesManagerRouteEmployeeDTO { Id = emp.EmployeeId, Name = emp.EmployeeName, City = cityName },
                Shift = shift == null ? null : new SalesManagerRouteShiftDTO
                {
                    Id = shift.ShiftId,
                    StartedAt = shift.StartedAtUtc,
                    ClosedAt = shift.ClosedAtUtc,
                    CutoffAt = shift.CutoffAtUtc,
                    Status = shift.Status
                },
                Points = points.ToList(),
                TotalPoints = total,
                ReturnedPoints = points.Count,
                IsTruncated = total > points.Count
            };
        }

        public async Task<IReadOnlyList<SalesManagerTrackingEventDTO>> GetEventsAsync(int employeeId, DateTime iraqDate, CancellationToken ct)
        {
            var startIraq = iraqDate.Date.Add(IraqTimeService.CutoffTime);
            var fromUtc = IraqTimeService.ToUtcFromIraq(startIraq);
            var toUtc = IraqTimeService.ToUtcFromIraq(startIraq.AddDays(1));
            var shift = await _read.GetShiftForBusinessDateAsync(employeeId, iraqDate.Date, ct);
            if (shift != null)
            {
                fromUtc = shift.StartedAtUtc;
                toUtc = shift.CutoffAtUtc;
            }

            return await _read.GetEventsAsync(employeeId, fromUtc, toUtc, ct);
        }

        public Task<IReadOnlyList<SalesDraftDTO>> ListSalesAsync(int? employeeId, string? status, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct) =>
            _read.ListSalesAsync(employeeId, status, fromUtc, toUtc, ct);

        public Task<SalesDraftDTO?> GetSaleAsync(int saleId, CancellationToken ct) =>
            _read.GetSaleAsync(saleId, ct);

        public async Task<SalesManagerDashboardDTO> DashboardAsync(CancellationToken ct)
        {
            await _requests.EnsureSchemaAsync(ct);
            var employees = await ListEmployeesAsync(null, null, ct);
            var todayIraq = IraqTimeService.IraqNow(_clock).Date;
            var windowStart = IraqTimeService.ToUtcFromIraq(todayIraq.Add(IraqTimeService.CutoffTime));
            if (IraqTimeService.IraqNow(_clock).TimeOfDay < IraqTimeService.CutoffTime)
            {
                windowStart = IraqTimeService.ToUtcFromIraq(todayIraq.AddDays(-1).Add(IraqTimeService.CutoffTime));
            }

            var sales = await _read.ListSalesAsync(null, null, windowStart, _clock.UtcNow, ct);
            return new SalesManagerDashboardDTO
            {
                EmployeesOnShift = employees.Count(e => e.ShiftStatus == SalesShiftStatuses.Active),
                EmployeesOffShift = employees.Count(e => e.ShiftStatus != SalesShiftStatuses.Active),
                LiveLocations = employees.Count(e => e.LocationStatus == SalesLocationStatuses.Live),
                SalesToday = sales.Count,
                PendingSales = await _read.CountPendingSalesAsync(null, ct),
                NewSalesRequests = await _requests.CountByStatusAsync(SalesRequestStatuses.New, ct)
            };
        }

        private async Task<SalesManagerEmployeeDTO> MapEmployeeAsync(
            int employeeId,
            string employeeName,
            string cityValue,
            string cityName,
            DateTime utc,
            CancellationToken ct)
        {
            var shift = await _read.GetShiftForBusinessDateAsync(employeeId, IraqTimeService.BusinessDateIraq(IraqTimeService.ToIraq(utc)), ct);
            var active = shift != null && shift.Status == SalesShiftStatuses.Active && !IraqTimeService.IsExpired(shift.CutoffAtUtc, utc);
            if (!active)
            {
                // still try current-looking shift via latest point window; GetShiftForBusinessDate covers today
            }

            var point = await _read.GetLatestPointAsync(employeeId, ct);
            var lastEvent = await _read.GetLatestEventAsync(employeeId, ct);
            var hasShift = active;
            var locationStatus = SalesLocationStatusService.Resolve(hasShift, point?.CapturedAtUtc, lastEvent?.EventType, utc, _options);
            var todayStart = IraqTimeService.ToUtcFromIraq(IraqTimeService.BusinessDateIraq(IraqTimeService.ToIraq(utc)).Add(IraqTimeService.CutoffTime));
            return new SalesManagerEmployeeDTO
            {
                EmployeeId = employeeId,
                EmployeeName = employeeName,
                CityValue = cityValue,
                CityName = cityName,
                ShiftStatus = hasShift ? SalesShiftStatuses.Active : "NoShift",
                ShiftStartedAt = shift?.StartedAtUtc,
                ShiftCutoffAt = shift?.CutoffAtUtc,
                CurrentShiftId = shift?.ShiftId,
                LastLocationAt = point?.CapturedAtUtc,
                LastLatitude = point?.Latitude,
                LastLongitude = point?.Longitude,
                LastAccuracy = point?.Accuracy,
                LocationStatus = locationStatus,
                LastTrackingEvent = lastEvent?.EventType,
                SalesTodayCount = await _read.CountSalesTodayAsync(employeeId, todayStart, utc, ct),
                PendingSalesCount = await _read.CountPendingSalesAsync(employeeId, ct)
            };
        }
    }
}
