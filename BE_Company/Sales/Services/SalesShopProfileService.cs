using BE_Company.DTO;
using BE_Company.IRepository;
using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Services
{
    public interface ISalesShopProfileService
    {
        Task EnsureSchemaAsync(CancellationToken ct);
        void RequireCompletePayload(SalesShopCompleteDTO? shop);
        Task<SalesShopProfileDTO> SaveImageAsync(int saleId, int employeeId, IFormFile file, CancellationToken ct);
        Task UpsertFromCompleteAsync(SalesDraftDTO sale, SalesShopCompleteDTO shop, CancellationToken ct);
        Task<SalesShopProfileDTO?> GetBySaleIdAsync(int saleId, CancellationToken ct);
        Task<(string FileName, byte[] Bytes)?> ReadImageAsync(int saleId, CancellationToken ct);
        Task<SalesCustomerProfileDTO> GetCustomerProfileAsync(int? customerId, string? customerName, string? phone, CancellationToken ct);
        Task<SalesCustomerProfileDTO> UpdateCustomerProfileAsync(SalesCustomerUpdateDTO request, int? userUpdateId, CancellationToken ct);
        Task<SalesCustomerNoteDTO> AddNoteAsync(SalesCustomerNoteCreateDTO note, string authorRole, string? authorName, CancellationToken ct);
    }

    public sealed class SalesShopProfileService : ISalesShopProfileService
    {
        private readonly SalesDevelopmentGuard _guard;
        private readonly IWebHostEnvironment _env;
        private readonly ISalesCompleteRepository _complete;
        private readonly ISalesDocumentService _documents;
        private readonly ISalesManagerReadRepository _sales;
        private readonly ISalesRequestService _requests;
        private readonly ICustomersPaymentsRepository _payments;
        private readonly ICustomersSalesRepository _customerSales;
        private readonly ICustomersRepository _customers;
        private readonly ISalesRequestRepository _requestRows;

        public SalesShopProfileService(
            SalesDevelopmentGuard guard,
            IWebHostEnvironment env,
            ISalesCompleteRepository complete,
            ISalesDocumentService documents,
            ISalesManagerReadRepository sales,
            ISalesRequestService requests,
            ICustomersPaymentsRepository payments,
            ICustomersSalesRepository customerSales,
            ICustomersRepository customers,
            ISalesRequestRepository requestRows)
        {
            _guard = guard;
            _env = env;
            _complete = complete;
            _documents = documents;
            _sales = sales;
            _requests = requests;
            _payments = payments;
            _customerSales = customerSales;
            _customers = customers;
            _requestRows = requestRows;
        }

        public async Task EnsureSchemaAsync(CancellationToken ct)
        {
            var cs = _guard.GetSalesConnectionString()
                     ?? throw new InvalidOperationException("Sales module has no usable branch connection.");
            await using var connection = new SqlConnection(cs);
            await connection.ExecuteAsync(new CommandDefinition(SchemaSql, cancellationToken: ct));
        }

        public void RequireCompletePayload(SalesShopCompleteDTO? shop)
        {
            if (shop == null)
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "بيانات المحل مطلوبة قبل إتمام البيع.");
            }

            if (string.IsNullOrWhiteSpace(shop.ShopName)
                || string.IsNullOrWhiteSpace(shop.ShopBusinessType)
                || string.IsNullOrWhiteSpace(shop.ShopImageKey))
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "اسم المحل وطبيعة العمل وصورة المحل مطلوبة.");
            }

            if (shop.ShopStockEstimatedValue <= 0 || shop.EstimatedDailyRevenue <= 0)
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "التقديرات المالية يجب أن تكون أكبر من صفر.");
            }

            if (shop.ShopLength <= 0 || shop.ShopWidth <= 0)
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "طول وعرض المحل يجب أن يكونا أكبر من صفر.");
            }

            shop.ShopArea = Math.Round(shop.ShopLength * shop.ShopWidth, 2, MidpointRounding.AwayFromZero);

            if (shop.Latitude is null or < -90 or > 90 || shop.Longitude is null or < -180 or > 180)
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "يجب تحديد موقع المحل.");
            }

            var path = ResolveImagePath(shop.ShopImageKey);
            if (!File.Exists(path))
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "صورة المحل غير موجودة. أعد رفع الصورة.");
            }
        }

        public async Task<SalesShopProfileDTO> SaveImageAsync(int saleId, int employeeId, IFormFile file, CancellationToken ct)
        {
            if (file == null || file.Length <= 0)
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "صورة المحل مطلوبة.");
            }

            var sale = await _complete.GetOwnedSaleAsync(saleId, employeeId, ct)
                       ?? throw new SalesCompleteException(StatusCodes.Status403Forbidden, "لا يمكنك رفع صورة لعملية لا تخصك.");
            if (SalesCompleteRules.AlreadyCompleted(sale.Status))
            {
                throw new SalesCompleteException(StatusCodes.Status409Conflict, "لا يمكن تعديل صورة محل لعملية مكتملة.");
            }

            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(ext) || ext.Length > 8)
            {
                ext = ".jpg";
            }

            var folder = Path.Combine(_env.ContentRootPath, "App_Data", "sales", saleId.ToString());
            Directory.CreateDirectory(folder);
            var fileName = "shop" + ext.ToLowerInvariant();
            var path = Path.Combine(folder, fileName);
            await using (var stream = File.Create(path))
            {
                await file.CopyToAsync(stream, ct);
            }

            var key = $"sales/{saleId}/{fileName}";
            return new SalesShopProfileDTO
            {
                SaleId = saleId,
                ShopImageKey = key,
                ShopImageUrl = $"/api/sales/{saleId}/shop-image"
            };
        }

        public async Task UpsertFromCompleteAsync(SalesDraftDTO sale, SalesShopCompleteDTO shop, CancellationToken ct)
        {
            await EnsureSchemaAsync(ct);
            var area = Math.Round(shop.ShopLength * shop.ShopWidth, 2, MidpointRounding.AwayFromZero);
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var existing = await connection.QueryFirstOrDefaultAsync<int?>(new CommandDefinition(
                "SELECT Id FROM dbo.SalesShopProfiles WHERE SaleId = @SaleId",
                new { sale.SaleId }, cancellationToken: ct));
            if (existing is > 0)
            {
                await connection.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.SalesShopProfiles SET
 CustomerId = @CustomerId, CustomerName = @CustomerName, CustomerPhone = @CustomerPhone,
 ShopName = @ShopName, ShopBusinessType = @ShopBusinessType,
 ShopStockEstimatedValue = @ShopStockEstimatedValue, EstimatedDailyRevenue = @EstimatedDailyRevenue,
 ShopLength = @ShopLength, ShopWidth = @ShopWidth, ShopArea = @ShopArea,
 ShopImageKey = @ShopImageKey, Latitude = @Latitude, Longitude = @Longitude, UpdatedAtUtc = SYSUTCDATETIME()
WHERE SaleId = @SaleId",
                    Params(sale, shop, area), cancellationToken: ct));
            }
            else
            {
                await connection.ExecuteAsync(new CommandDefinition(@"
INSERT INTO dbo.SalesShopProfiles
(SaleId, CustomerId, CustomerName, CustomerPhone, ShopName, ShopBusinessType,
 ShopStockEstimatedValue, EstimatedDailyRevenue, ShopLength, ShopWidth, ShopArea, ShopImageKey, Latitude, Longitude, CreatedAtUtc)
VALUES
(@SaleId, @CustomerId, @CustomerName, @CustomerPhone, @ShopName, @ShopBusinessType,
 @ShopStockEstimatedValue, @EstimatedDailyRevenue, @ShopLength, @ShopWidth, @ShopArea, @ShopImageKey, @Latitude, @Longitude, SYSUTCDATETIME())",
                    Params(sale, shop, area), cancellationToken: ct));
            }

            if (!string.IsNullOrWhiteSpace(shop.EmployeeNote))
            {
                await AddNoteAsync(new SalesCustomerNoteCreateDTO
                {
                    CustomerId = sale.CustomerId,
                    CustomerName = sale.FullName,
                    CustomerPhone = sale.Phone,
                    Note = shop.EmployeeNote
                }, "SalesEmployee", sale.UserName, ct);
            }
        }

        public async Task<SalesShopProfileDTO?> GetBySaleIdAsync(int saleId, CancellationToken ct)
        {
            await EnsureSchemaAsync(ct);
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var row = await connection.QueryFirstOrDefaultAsync<SalesShopProfileDTO>(new CommandDefinition(
                ShopSelect + " WHERE SaleId = @SaleId", new { SaleId = saleId }, cancellationToken: ct));
            if (row != null)
            {
                row.ShopImageUrl = $"/api/sales/{row.SaleId}/shop-image";
            }

            return row;
        }

        public async Task<(string FileName, byte[] Bytes)?> ReadImageAsync(int saleId, CancellationToken ct)
        {
            var row = await GetBySaleIdAsync(saleId, ct);
            var key = row?.ShopImageKey;
            if (string.IsNullOrWhiteSpace(key))
            {
                var folder = Path.Combine(_env.ContentRootPath, "App_Data", "sales", saleId.ToString());
                if (Directory.Exists(folder))
                {
                    key = Directory.GetFiles(folder, "shop.*").FirstOrDefault();
                }
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            var path = Path.IsPathRooted(key) ? key : ResolveImagePath(key);
            if (!File.Exists(path))
            {
                return null;
            }

            return (Path.GetFileName(path), await File.ReadAllBytesAsync(path, ct));
        }

        public async Task<SalesCustomerProfileDTO> GetCustomerProfileAsync(
            int? customerId,
            string? customerName,
            string? phone,
            CancellationToken ct)
        {
            await EnsureSchemaAsync(ct);
            var sales = await _sales.ListSalesAsync(null, null, null, null, ct);
            var matchedSales = sales.Where(s => MatchesCustomer(s.CustomerId, s.FullName, s.Phone, customerId, customerName, phone)).ToList();
            var requests = await _requests.ListForManagerAsync(null, null, null, null, ct);
            var matchedRequests = requests.Where(r =>
                MatchesCustomer(r.ExistingCustomerId, r.CustomerName, r.CustomerPhone, customerId, customerName, phone)).ToList();

            var shops = new List<SalesShopProfileDTO>();
            var profileSales = new List<SalesCustomerProfileSaleDTO>();
            foreach (var sale in matchedSales.OrderByDescending(s => s.CompletedAt ?? s.CreatedAt))
            {
                var full = await _sales.GetSaleAsync(sale.SaleId, ct) ?? sale;
                var shop = await GetBySaleIdAsync(sale.SaleId, ct);
                if (shop != null)
                {
                    shops.Add(shop);
                }

                var documents = new List<SalesDocumentDTO>();
                try
                {
                    IReadOnlyList<SalesDocumentDTO> raw;
                    if (SalesCompleteRules.AlreadyCompleted(sale.Status) || sale.Status == SalesStatuses.Completed)
                    {
                        try
                        {
                            raw = await _documents.EnsureGeneratedAsync(full, ct);
                        }
                        catch
                        {
                            raw = (await _complete.GetDocumentsAsync(sale.SaleId, sale.EmployeeId, ct))
                                .Select(SalesDocumentMapper.ToDto)
                                .ToList();
                        }
                    }
                    else
                    {
                        raw = (await _complete.GetDocumentsAsync(sale.SaleId, sale.EmployeeId, ct))
                            .Select(SalesDocumentMapper.ToDto)
                            .ToList();
                    }

                    documents = PreferFinalDocuments(raw
                        .Select(d =>
                        {
                            d.DownloadUrl = $"/api/sales-manager/sales/{sale.SaleId}/documents/{d.DocumentId}/download";
                            return d;
                        })
                        .ToList());
                }
                catch
                {
                    documents = [];
                }

                profileSales.Add(new SalesCustomerProfileSaleDTO
                {
                    SaleId = full.SaleId,
                    Date = full.CompletedAt ?? full.CreatedAt,
                    Status = full.Status,
                    EmployeeName = full.UserName,
                    BaseSalePrice = full.BaseSalePrice,
                    DefaultTotalSalePrice = full.DefaultTotalSalePrice > 0 ? full.DefaultTotalSalePrice : full.BaseSalePrice,
                    OverrideTotalSalePrice = full.OverrideTotalSalePrice,
                    FinalSalePrice = full.FinalSalePrice,
                    DefaultDailyInstallment = full.DefaultDailyInstallment,
                    OverrideDailyInstallment = full.OverrideDailyInstallment,
                    DailyInstallment = full.DailyInstallment,
                    DefaultDownPayment = full.DefaultDownPayment,
                    OverrideDownPayment = full.OverrideDownPayment,
                    DownPayment = full.DownPayment,
                    Items = full.Items ?? [],
                    Shop = shop,
                    Documents = documents
                });
            }

            var notes = await ListStoredNotesAsync(customerId, customerName, phone, ct);
            foreach (var sale in matchedSales.Where(s => !string.IsNullOrWhiteSpace(s.EvaluationNote)))
            {
                notes.Add(new SalesCustomerNoteDTO
                {
                    AuthorRole = "SalesEmployee",
                    AuthorName = sale.UserName,
                    Note = sale.EvaluationNote,
                    CreatedAtUtc = sale.CreatedAt,
                    Source = "Evaluation"
                });
            }

            foreach (var req in matchedRequests)
            {
                AddRequestNote(notes, req.Notes, req.CreatedByName, req.CreatedAtUtc, "Request");
                AddRequestNote(notes, req.PendingNote, req.TargetEmployeeName, req.CreatedAtUtc, "Pending");
                AddRequestNote(notes, req.PreparedForSaleNote, req.TargetEmployeeName, req.ProcessingAtUtc ?? req.CreatedAtUtc, "PreparedForSale");
                AddRequestNote(notes, req.ReturnNote, req.AssignedByName, req.CreatedAtUtc, "Return");
                AddRequestNote(notes, req.RejectionReason, req.TargetEmployeeName, req.RejectedAtUtc ?? req.CreatedAtUtc, "Rejection");
            }

            var sample = matchedSales.FirstOrDefault() ?? new SalesDraftDTO
            {
                FullName = customerName ?? matchedRequests.FirstOrDefault()?.CustomerName ?? "",
                Phone = phone ?? matchedRequests.FirstOrDefault()?.CustomerPhone,
                CustomerId = customerId,
                CityValue = matchedRequests.FirstOrDefault()?.CityValue,
                CityName = matchedRequests.FirstOrDefault()?.CityName,
                Address = matchedRequests.FirstOrDefault()?.CustomerAddress,
                Province = matchedRequests.FirstOrDefault()?.CustomerProvince
            };
            var sampleFull = sample.SaleId > 0 ? await _sales.GetSaleAsync(sample.SaleId, ct) ?? sample : sample;

            CustomersGetDTO? account = null;
            IEnumerable<CustomersPaymentsGetDTO> payments = [];
            IEnumerable<CustomersSalesGetDTO> officialSales = [];
            var resolvedCustomerId = customerId ?? sample.CustomerId ?? matchedRequests.FirstOrDefault()?.ExistingCustomerId;
            if (resolvedCustomerId is > 0)
            {
                try
                {
                    account = await _customerSales.Customers_GetByCustomerID(resolvedCustomerId);
                }
                catch
                {
                    account = null;
                }

                try
                {
                    payments = await _payments.CustomersPayments_GetByCustomerID(resolvedCustomerId)
                               ?? [];
                }
                catch
                {
                    payments = [];
                }

                try
                {
                    officialSales = await _customerSales.CustomersSales_GetByCustomerIDNew(resolvedCustomerId, null, null)
                                    ?? [];
                }
                catch
                {
                    officialSales = [];
                }
            }

            var history = matchedRequests.SelectMany(r => r.History ?? []).OrderBy(h => h.CreatedAtUtc).ToList();
            var latestShop = shops.OrderByDescending(s => s.CreatedAtUtc).FirstOrDefault();
            var firstRequest = matchedRequests.FirstOrDefault();
            var openRequest = matchedRequests.FirstOrDefault(r => !IsFrozenRequest(r.Status));

            var listId = FirstPositive(
                sampleFull.CustomerListId,
                matchedSales.Select(s => s.CustomerListId).FirstOrDefault(id => id is > 0),
                account?.DelegateID);
            var listName = FirstNonEmpty(
                sampleFull.CustomerListName,
                matchedSales.Select(s => s.CustomerListName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)));
            if (string.IsNullOrWhiteSpace(listName) && listId is > 0)
            {
                var cs = _guard.GetSalesConnectionString();
                if (!string.IsNullOrWhiteSpace(cs))
                {
                    await using var connection = new SqlConnection(cs);
                    listName = await SalesInventoryService.NameAsync(connection, listId, ct);
                }
            }

            return new SalesCustomerProfileDTO
            {
                CustomerId = resolvedCustomerId,
                CustomerName = SalesCustomerIdentity.PreferName(
                    sampleFull.FullName,
                    account?.CustomerName,
                    firstRequest?.CustomerName,
                    openRequest?.CustomerName),
                Phone = SalesCustomerIdentity.PreferText(
                    sampleFull.Phone,
                    account?.PhoneNumber,
                    openRequest?.CustomerPhone,
                    firstRequest?.CustomerPhone),
                Address = SalesCustomerIdentity.PreferText(
                    sampleFull.Address,
                    account?.Address,
                    openRequest?.CustomerAddress,
                    firstRequest?.CustomerAddress),
                Province = FirstNonEmpty(
                    openRequest?.CustomerProvince,
                    sampleFull.Province,
                    firstRequest?.CustomerProvince,
                    account?.CityName),
                NationalCardNumber = sampleFull.NationalCardNumber,
                DelegateName = listName,
                DelegateId = listId,
                CustomerListId = listId,
                CustomerListName = listName,
                CityValue = sample.CityValue ?? firstRequest?.CityValue,
                CityName = account?.CityName ?? sample.CityName ?? firstRequest?.CityName,
                PaymentSummary = account == null ? null : new SalesPaymentSummaryDTO
                {
                    AmountTotalSales = account.AmountTotalSales,
                    ReceiptsTotal = account.ReceiptsTotal,
                    AmountRemaining = account.AmountRemaining,
                    LastPaymentDate = account.LastPaymentDate,
                    CountReceiptDevice = account.CountReceiptDevice
                },
                Payments = payments
                    .OrderByDescending(p => p.PaymentDate)
                    .Select(p => new SalesCustomerPaymentDTO
                    {
                        CustomerPaymentId = p.CustomerPaymentID,
                        PaymentDate = p.PaymentDate,
                        Amount = p.AmountDenar,
                        BoundNumber = p.BoundNumber,
                        Note = p.Location,
                        ItemsNames = p.ItemsNames
                    })
                    .ToList(),
                OfficialSales = officialSales
                    .OrderByDescending(s => s.DateCreate)
                    .Select(s => new SalesOfficialSaleDTO
                    {
                        CustomerSaleId = s.CustomerSaleID,
                        DateCreate = s.DateCreate,
                        ItemsNames = s.ItemsNames,
                        AmountTotalSales = s.AmountTotalSalesDenar ?? s.AmountTotalDenar,
                        ReceiptsTotal = s.ReceiptsTotal,
                        AmountRemaining = s.AmountRemaining,
                        SaleName = s.SaleName,
                        UserName = s.UserName
                    })
                    .ToList(),
                Sales = profileSales,
                Shops = shops,
                LatestShop = latestShop,
                Evaluations = matchedSales.Select(s => new SalesCustomerProfileEvaluationDTO
                {
                    SaleId = s.SaleId,
                    EvaluationLevel = s.EvaluationLevel,
                    EvaluationName = SalesEvaluationLevels.DisplayName(s.EvaluationLevel),
                    EvaluationNote = s.EvaluationNote
                }).ToList(),
                SalesRequests = matchedRequests.ToList(),
                History = history,
                Notes = notes.OrderBy(n => n.CreatedAtUtc).ToList()
            };
        }

        public async Task<SalesCustomerProfileDTO> UpdateCustomerProfileAsync(
            SalesCustomerUpdateDTO request,
            int? userUpdateId,
            CancellationToken ct)
        {
            var name = request.CustomerName?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "اسم الزبون مطلوب.");
            }

            var phone = EmptyToNull(request.Phone);
            SalesIraqPhone.RequireIfPresent(phone);
            if (phone != null)
            {
                phone = SalesIraqPhone.Normalize(phone);
            }
            var province = EmptyToNull(request.Province);
            var address = EmptyToNull(request.Address);
            var originalName = EmptyToNull(request.OriginalName);
            var originalPhone = EmptyToNull(request.OriginalPhone);
            var customerId = request.CustomerId is > 0 ? request.CustomerId : null;

            await EnsureSchemaAsync(ct);

            if (customerId is > 0)
            {
                await UpdateOfficialCustomerAsync(customerId.Value, name, phone, address, userUpdateId, ct);
            }

            var requests = await _requests.ListForManagerAsync(null, null, null, null, ct);
            var matchedOpen = requests
                .Where(r => MatchesForUpdate(r.ExistingCustomerId, r.CustomerName, r.CustomerPhone, customerId, originalName, originalPhone)
                            && !IsFrozenRequest(r.Status))
                .ToList();
            foreach (var row in matchedOpen)
            {
                row.CustomerName = name;
                row.CustomerPhone = phone;
                if (province != null)
                {
                    row.CustomerProvince = province;
                }

                if (address != null)
                {
                    row.CustomerAddress = address;
                }

                await _requestRows.UpdateAsync(row, ct);
            }

            if (customerId is null or <= 0 && matchedOpen.Count == 0)
            {
                throw new SalesCompleteException(
                    StatusCodes.Status409Conflict,
                    "لا يمكن تعديل هذا الزبون من طلبات مكتملة فقط. أنشئ التعديل على حساب الزبون أو على طلب بيع ما زال مفتوحاً حتى لا يتغير التاريخ السابق.");
            }

            await RelinkIdentityKeysAsync(customerId, originalName, originalPhone, name, phone, ct);
            await UpdatePendingDraftIdentityAsync(customerId, originalName, originalPhone, name, phone, province, address, ct);

            return await GetCustomerProfileAsync(customerId, name, phone, ct);
        }

        public async Task<SalesCustomerNoteDTO> AddNoteAsync(
            SalesCustomerNoteCreateDTO note,
            string authorRole,
            string? authorName,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(note.Note))
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "الملاحظة مطلوبة.");
            }

            await EnsureSchemaAsync(ct);
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
INSERT INTO dbo.SalesCustomerNotes (CustomerId, CustomerName, CustomerPhone, AuthorRole, AuthorName, Note, CreatedAtUtc)
OUTPUT INSERTED.Id
VALUES (@CustomerId, @CustomerName, @CustomerPhone, @AuthorRole, @AuthorName, @Note, SYSUTCDATETIME())",
                new
                {
                    note.CustomerId,
                    note.CustomerName,
                    note.CustomerPhone,
                    AuthorRole = authorRole,
                    AuthorName = authorName,
                    Note = note.Note.Trim()
                }, cancellationToken: ct));
            return new SalesCustomerNoteDTO
            {
                Id = id,
                AuthorRole = authorRole,
                AuthorName = authorName,
                Note = note.Note.Trim(),
                CreatedAtUtc = DateTime.UtcNow,
                Source = "Manual"
            };
        }

        private async Task<List<SalesCustomerNoteDTO>> ListStoredNotesAsync(
            int? customerId, string? customerName, string? phone, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var rows = await connection.QueryAsync<SalesCustomerNoteDTO>(new CommandDefinition(@"
SELECT Id, CustomerId, CustomerName, CustomerPhone, AuthorRole, AuthorName, Note, CreatedAtUtc, N'Manual' AS Source
FROM dbo.SalesCustomerNotes
ORDER BY CreatedAtUtc", cancellationToken: ct));
            return rows.Where(n =>
                MatchesCustomer(n.CustomerId, n.CustomerName, n.CustomerPhone, customerId, customerName, phone)).ToList();
        }

        private static void AddRequestNote(
            List<SalesCustomerNoteDTO> notes, string? text, string? author, DateTime at, string source)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            notes.Add(new SalesCustomerNoteDTO
            {
                AuthorRole = source == "Return" ? "SalesManager" : "SalesEmployee",
                AuthorName = author,
                Note = text,
                CreatedAtUtc = at,
                Source = source
            });
        }

        private static bool MatchesCustomer(
            int? rowCustomerId,
            string? rowName,
            string? rowPhone,
            int? customerId,
            string? customerName,
            string? phone)
        {
            if (customerId is > 0)
            {
                return rowCustomerId == customerId;
            }

            var nameOk = !string.IsNullOrWhiteSpace(customerName)
                         && string.Equals(rowName?.Trim(), customerName.Trim(), StringComparison.OrdinalIgnoreCase);
            var phoneOk = !string.IsNullOrWhiteSpace(phone)
                          && string.Equals(rowPhone?.Trim(), phone.Trim(), StringComparison.OrdinalIgnoreCase);
            return nameOk || phoneOk;
        }

        private static bool MatchesForUpdate(
            int? rowCustomerId,
            string? rowName,
            string? rowPhone,
            int? customerId,
            string? originalName,
            string? originalPhone)
        {
            if (customerId is > 0 && rowCustomerId == customerId)
            {
                return true;
            }

            if (rowCustomerId is > 0 && customerId is > 0 && rowCustomerId != customerId)
            {
                return false;
            }

            return MatchesCustomer(null, rowName, rowPhone, null, originalName, originalPhone);
        }

        private static bool IsFrozenRequest(string? status) =>
            SalesRequestStatuses.IsSold(status)
            || string.Equals(status, SalesRequestStatuses.Rejected, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, SalesRequestStatuses.ConvertedToSale, StringComparison.OrdinalIgnoreCase);

        private static string? FirstNonEmpty(params string?[] values) =>
            values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        private static int? FirstPositive(params int?[] values) =>
            values.FirstOrDefault(v => v is > 0);

        private static string? EmptyToNull(string? value)
        {
            var text = value?.Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        private async Task UpdateOfficialCustomerAsync(
            int customerId,
            string name,
            string? phone,
            string? address,
            int? userUpdateId,
            CancellationToken ct)
        {
            CustomersGetDTO? account = null;
            try
            {
                account = await _customerSales.Customers_GetByCustomerID(customerId);
            }
            catch
            {
                account = null;
            }

            if (account == null)
            {
                await TrySqlUpdateCustomersAsync(customerId, name, phone, address, ct);
                return;
            }

            var put = new CustomersPutDTO
            {
                CustomerID = customerId,
                UserUpdateID = userUpdateId is > 0 ? userUpdateId : account.UserID,
                CustomerName = name,
                PhoneNumber = phone ?? account.PhoneNumber,
                Address = address ?? account.Address,
                ShopName = account.ShopName,
                SaleName = account.SaleName,
                Notes = account.Notes
            };

            try
            {
                await _customers.Customers_Update(customerId, put);
            }
            catch
            {
                await TrySqlUpdateCustomersAsync(customerId, name, phone ?? account.PhoneNumber, address ?? account.Address, ct);
            }
        }

        private async Task TrySqlUpdateCustomersAsync(
            int customerId,
            string name,
            string? phone,
            string? address,
            CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            await connection.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'dbo.Customers', N'U') IS NOT NULL
UPDATE dbo.Customers SET
 CustomerName = @CustomerName,
 PhoneNumber = COALESCE(@PhoneNumber, PhoneNumber),
 Address = COALESCE(@Address, Address)
WHERE CustomerID = @CustomerID",
                new { CustomerID = customerId, CustomerName = name, PhoneNumber = phone, Address = address },
                cancellationToken: ct));
        }

        private async Task RelinkIdentityKeysAsync(
            int? customerId,
            string? originalName,
            string? originalPhone,
            string name,
            string? phone,
            CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var notes = await connection.QueryAsync<(int Id, int? CustomerId, string? CustomerName, string? CustomerPhone)>(
                new CommandDefinition(
                    "SELECT Id, CustomerId, CustomerName, CustomerPhone FROM dbo.SalesCustomerNotes",
                    cancellationToken: ct));
            foreach (var row in notes.Where(n =>
                         MatchesForUpdate(n.CustomerId, n.CustomerName, n.CustomerPhone, customerId, originalName, originalPhone)))
            {
                await connection.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.SalesCustomerNotes SET CustomerName = @CustomerName, CustomerPhone = @CustomerPhone WHERE Id = @Id",
                    new { row.Id, CustomerName = name, CustomerPhone = phone }, cancellationToken: ct));
            }

            var docsExist = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT CASE WHEN OBJECT_ID(N'dbo.SalesCustomerDocuments', N'U') IS NULL THEN 0 ELSE 1 END",
                cancellationToken: ct));
            if (docsExist == 1)
            {
                var docs = await connection.QueryAsync<(int Id, int? CustomerId, string? CustomerName, string? CustomerPhone)>(
                    new CommandDefinition(
                        "SELECT Id, CustomerId, CustomerName, CustomerPhone FROM dbo.SalesCustomerDocuments",
                        cancellationToken: ct));
                foreach (var row in docs.Where(n =>
                             MatchesForUpdate(n.CustomerId, n.CustomerName, n.CustomerPhone, customerId, originalName, originalPhone)))
                {
                    await connection.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.SalesCustomerDocuments SET CustomerName = @CustomerName, CustomerPhone = @CustomerPhone WHERE Id = @Id",
                        new { row.Id, CustomerName = name, CustomerPhone = phone }, cancellationToken: ct));
                }
            }

            var shops = await connection.QueryAsync<(int Id, int? CustomerId, string? CustomerName, string? CustomerPhone)>(
                new CommandDefinition(
                    "SELECT Id, CustomerId, CustomerName, CustomerPhone FROM dbo.SalesShopProfiles",
                    cancellationToken: ct));
            foreach (var row in shops.Where(n =>
                         n.CustomerId is null or <= 0
                         && MatchesForUpdate(n.CustomerId, n.CustomerName, n.CustomerPhone, customerId, originalName, originalPhone)))
            {
                await connection.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.SalesShopProfiles SET CustomerName = @CustomerName, CustomerPhone = @CustomerPhone WHERE Id = @Id",
                    new { row.Id, CustomerName = name, CustomerPhone = phone }, cancellationToken: ct));
            }
        }

        private async Task UpdatePendingDraftIdentityAsync(
            int? customerId,
            string? originalName,
            string? originalPhone,
            string name,
            string? phone,
            string? province,
            string? address,
            CancellationToken ct)
        {
            var drafts = await _sales.ListSalesAsync(null, null, null, null, ct);
            var pending = drafts.Where(s =>
                string.Equals(s.Status, SalesStatuses.Pending, StringComparison.OrdinalIgnoreCase)
                && MatchesForUpdate(s.CustomerId, s.FullName, s.Phone, customerId, originalName, originalPhone)).ToList();
            if (pending.Count == 0)
            {
                return;
            }

            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            foreach (var draft in pending)
            {
                await connection.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.SalesDrafts SET
 FullName = @FullName,
 Phone = COALESCE(@Phone, Phone),
 Province = COALESCE(@Province, Province),
 Address = COALESCE(@Address, Address)
WHERE SaleId = @SaleId AND Status = N'Pending'",
                    new
                    {
                        draft.SaleId,
                        FullName = name,
                        Phone = phone,
                        Province = province,
                        Address = address
                    },
                    cancellationToken: ct));
            }
        }

        private static List<SalesDocumentDTO> PreferFinalDocuments(List<SalesDocumentDTO> documents) =>
            SalesDocumentService.PreferDisplayDocuments(documents).ToList();

        private string ResolveImagePath(string key)
        {
            if (Path.IsPathRooted(key) || key.Contains(":\\", StringComparison.Ordinal))
            {
                return key;
            }

            var relative = key.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
            if (relative.StartsWith("App_Data", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(_env.ContentRootPath, relative);
            }

            return Path.Combine(_env.ContentRootPath, "App_Data", relative);
        }

        private string RequireConnection() =>
            _guard.GetSalesConnectionString()
            ?? throw new InvalidOperationException("Sales module has no usable branch connection.");

        private static object Params(SalesDraftDTO sale, SalesShopCompleteDTO shop, decimal area) => new
        {
            sale.SaleId,
            sale.CustomerId,
            CustomerName = sale.FullName,
            CustomerPhone = sale.Phone,
            ShopName = shop.ShopName!.Trim(),
            ShopBusinessType = shop.ShopBusinessType!.Trim(),
            shop.ShopStockEstimatedValue,
            shop.EstimatedDailyRevenue,
            shop.ShopLength,
            shop.ShopWidth,
            ShopArea = area,
            ShopImageKey = shop.ShopImageKey!.Trim(),
            shop.Latitude,
            shop.Longitude
        };

        private const string ShopSelect = @"
SELECT Id, SaleId, CustomerId, CustomerName, CustomerPhone, ShopName, ShopBusinessType,
 ShopStockEstimatedValue, EstimatedDailyRevenue, ShopLength, ShopWidth, ShopArea, ShopImageKey,
 CAST(Latitude AS FLOAT) AS Latitude, CAST(Longitude AS FLOAT) AS Longitude, CreatedAtUtc
FROM dbo.SalesShopProfiles";

        private const string SchemaSql = @"
IF OBJECT_ID(N'dbo.SalesShopProfiles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesShopProfiles (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        SaleId INT NOT NULL UNIQUE,
        CustomerId INT NULL,
        CustomerName NVARCHAR(255) NULL,
        CustomerPhone NVARCHAR(50) NULL,
        ShopName NVARCHAR(255) NOT NULL,
        ShopBusinessType NVARCHAR(255) NOT NULL,
        ShopStockEstimatedValue DECIMAL(18,0) NOT NULL,
        EstimatedDailyRevenue DECIMAL(18,0) NOT NULL,
        ShopLength DECIMAL(18,2) NOT NULL,
        ShopWidth DECIMAL(18,2) NOT NULL,
        ShopArea DECIMAL(18,2) NOT NULL,
        ShopImageKey NVARCHAR(500) NOT NULL,
        Latitude DECIMAL(9,6) NULL,
        Longitude DECIMAL(9,6) NULL,
        CreatedAtUtc DATETIME NOT NULL CONSTRAINT DF_SalesShopProfiles_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc DATETIME NULL
    );
END;
IF OBJECT_ID(N'dbo.SalesCustomerNotes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesCustomerNotes (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CustomerId INT NULL,
        CustomerName NVARCHAR(255) NULL,
        CustomerPhone NVARCHAR(50) NULL,
        AuthorRole NVARCHAR(50) NOT NULL,
        AuthorName NVARCHAR(200) NULL,
        Note NVARCHAR(MAX) NOT NULL,
        CreatedAtUtc DATETIME NOT NULL CONSTRAINT DF_SalesCustomerNotes_CreatedAt DEFAULT (SYSUTCDATETIME())
    );
END;
IF OBJECT_ID(N'dbo.SalesShopProfiles', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.SalesShopProfiles', N'Latitude') IS NULL
        ALTER TABLE dbo.SalesShopProfiles ADD Latitude DECIMAL(9,6) NULL;
    IF COL_LENGTH(N'dbo.SalesShopProfiles', N'Longitude') IS NULL
        ALTER TABLE dbo.SalesShopProfiles ADD Longitude DECIMAL(9,6) NULL;
END;";
    }
}
