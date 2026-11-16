using BE_Company.Sales.DTO;

namespace BE_Company.Sales.Services
{
    public interface ICustomerAppService
    {
        Task<CustomerAppAccountsResponseDTO> GetAccountsByPhoneAsync(string? phone, CancellationToken ct);
        /// <summary>Null when the customer does not exist, is fake, or the phone does not match (→ 404).</summary>
        Task<CustomerAppAccountDetailDTO?> GetAccountDetailAsync(int customerId, string? phone, CancellationToken ct);
        Task<CustomerAppStatusResponseDTO> GetStatusesAsync(CustomerAppStatusRequestDTO? request, CancellationToken ct);
        Task<CustomerAppSaleProgressDTO?> GetSaleProgressAsync(int saleId, CancellationToken ct);
    }

    /// <summary>
    /// Branch side of the customer store app: "مشترياتي" lookup by phone and order-status polling.
    /// Gateway-only (X-Sales-Gateway-Key); read-only over existing branch data.
    /// </summary>
    public sealed class CustomerAppService : ICustomerAppService
    {
        public const int MaxStatusItems = 200;

        private readonly ICustomerAppRepository _repo;
        private readonly ISalesRequestRepository _requests;
        private readonly IConfiguration _configuration;

        public CustomerAppService(
            ICustomerAppRepository repo,
            ISalesRequestRepository requests,
            IConfiguration configuration)
        {
            _repo = repo;
            _requests = requests;
            _configuration = configuration;
        }

        private string BranchId => _configuration["SalesManagement:BranchId"] ?? "branch";
        private string BranchName => _configuration["SalesManagement:BranchName"] ?? BranchId;

        public async Task<CustomerAppAccountsResponseDTO> GetAccountsByPhoneAsync(string? phone, CancellationToken ct)
        {
            var key = RequireMatchKey(phone);
            var rows = await _repo.ListAccountsByPhoneAsync(key, ct);
            var items = await _repo.ListItemsAsync(rows.Select(r => r.CustomerID).ToList(), ct);
            return new CustomerAppAccountsResponseDTO
            {
                CityValue = BranchId,
                CityName = BranchName,
                Accounts = rows
                    .GroupBy(r => r.CustomerID)
                    .Select(g => ToAccount(g.First(), items, BranchId, BranchName))
                    .ToList()
            };
        }

        public async Task<CustomerAppAccountDetailDTO?> GetAccountDetailAsync(int customerId, string? phone, CancellationToken ct)
        {
            var key = RequireMatchKey(phone);
            if (customerId <= 0)
            {
                return null;
            }

            var row = await _repo.GetAccountByIdAndPhoneAsync(customerId, key, ct);
            if (row is null)
            {
                return null;
            }

            var items = await _repo.ListItemsAsync([customerId], ct);
            var sales = await _repo.ListSalesAsync(customerId, ct);
            var payments = await _repo.ListPaymentsAsync(customerId, ct);
            return new CustomerAppAccountDetailDTO
            {
                Account = ToAccount(row, items, BranchId, BranchName),
                Sales = sales.Select(s => new CustomerAppSaleDTO
                {
                    SaleId = s.CustomerSaleID,
                    Date = s.DateCreate,
                    Items = MergeItems(items.Where(i => i.CustomerSaleID == s.CustomerSaleID)),
                    TotalAmount = s.TotalAmount,
                    DailyInstallment = s.DailyInstallment
                }).ToList(),
                Payments = payments.ToList()
            };
        }

        public async Task<CustomerAppSaleProgressDTO?> GetSaleProgressAsync(int saleId, CancellationToken ct)
        {
            if (saleId <= 0)
            {
                return null;
            }

            var row = await _repo.GetAccountBySaleDraftAsync(saleId, ct);
            return row is null
                ? null
                : new CustomerAppSaleProgressDTO
                {
                    SaleId = saleId,
                    CustomerId = row.CustomerID,
                    TotalAmount = row.TotalAmount,
                    PaidAmount = row.PaidAmount,
                    RemainingAmount = Math.Max(0, row.RemainingAmount),
                    PaymentsCount = row.PaymentsCount,
                    LastPaymentDate = row.LastPaymentDate
                };
        }

        public async Task<CustomerAppStatusResponseDTO> GetStatusesAsync(CustomerAppStatusRequestDTO? request, CancellationToken ct)
        {
            var uuids = (request?.OrderUuids ?? [])
                .Where(u => u != Guid.Empty)
                .Distinct()
                .ToList();
            if (uuids.Count > MaxStatusItems)
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest,
                    $"الحد الأقصى {MaxStatusItems} طلباً في كل استعلام.");
            }

            if (uuids.Count == 0)
            {
                return new CustomerAppStatusResponseDTO();
            }

            // Older branches may not have run the SalesRequests migrations yet (ExternalCrmOrderUuid, IsDeleted…).
            await _requests.EnsureSchemaAsync(ct);
            var rows = await _repo.ListStatusesAsync(uuids, ct);
            return BuildStatusResponse(uuids, rows);
        }

        /// <summary>One item per requested uuid, in request order; unknown uuids come back with found=false.</summary>
        public static CustomerAppStatusResponseDTO BuildStatusResponse(
            IReadOnlyList<Guid> requested,
            IEnumerable<CustomerAppStatusRow> rows)
        {
            var byUuid = rows
                .GroupBy(r => r.ExternalCrmOrderUuid)
                .ToDictionary(g => g.Key, g => g.First());
            return new CustomerAppStatusResponseDTO
            {
                Items = requested.Select(uuid => byUuid.TryGetValue(uuid, out var r)
                    ? new CustomerAppStatusItemDTO
                    {
                        OrderUuid = uuid,
                        Found = true,
                        SalesRequestId = r.Id,
                        Status = r.Status,
                        FilterStatus = r.FilterStatus,
                        IsDeleted = r.IsDeleted,
                        RejectionReason = !string.IsNullOrWhiteSpace(r.RejectionReason)
                            ? r.RejectionReason.Trim()
                            : (r.IsDeleted && !string.IsNullOrWhiteSpace(r.DeleteReason) ? r.DeleteReason.Trim() : null),
                        ConvertedToSaleId = r.ConvertedToSaleId,
                        UpdatedAtUtc = LatestChange(r)
                    }
                    : new CustomerAppStatusItemDTO { OrderUuid = uuid, Found = false }).ToList()
            };
        }

        public static DateTime LatestChange(CustomerAppStatusRow r)
        {
            DateTime?[] stamps =
            [
                r.AssignedAtUtc, r.ViewedAtUtc, r.ProcessingAtUtc, r.CompletedAtUtc,
                r.RejectedAtUtc, r.DeletedAtUtc, r.LastHistoryAtUtc
            ];
            var latest = r.CreatedAtUtc;
            foreach (var s in stamps)
            {
                if (s is { } value && value > latest)
                {
                    latest = value;
                }
            }

            return latest;
        }

        public static CustomerAppAccountDTO ToAccount(
            CustomerAppAccountRow row,
            IEnumerable<CustomerAppItemRow> items,
            string? cityValue,
            string? cityName) => new()
        {
            CityValue = cityValue,
            CityName = cityName,
            CustomerId = row.CustomerID,
            CustomerName = row.CustomerName?.Trim() ?? string.Empty,
            SaleDate = row.SaleDate,
            Items = MergeItems(items.Where(i => i.CustomerID == row.CustomerID)),
            TotalAmount = row.TotalAmount,
            DailyInstallment = row.DailyInstallment,
            PaidAmount = row.PaidAmount,
            RemainingAmount = row.RemainingAmount,
            PaymentsCount = row.PaymentsCount,
            LastPaymentDate = row.LastPaymentDate
        };

        /// <summary>Same item name across rows → one entry with summed quantity (first-seen order).</summary>
        public static List<CustomerAppItemDTO> MergeItems(IEnumerable<CustomerAppItemRow> rows)
        {
            var result = new List<CustomerAppItemDTO>();
            foreach (var row in rows)
            {
                var name = row.ItemName?.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                var existing = result.FirstOrDefault(i => string.Equals(i.Name, name, StringComparison.Ordinal));
                if (existing is null)
                {
                    result.Add(new CustomerAppItemDTO { Name = name, Quantity = row.Quantity });
                }
                else
                {
                    existing.Quantity += row.Quantity;
                }
            }

            return result;
        }

        private static string RequireMatchKey(string? phone)
        {
            var key = CustomerAppPhoneMatch.ToMatchKey(phone);
            if (key.Length != 10)
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, SalesIraqPhone.Message);
            }

            return key;
        }
    }
}
