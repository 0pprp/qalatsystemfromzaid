using BE_Company.Sales.Authorization;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace BE_Company.Legal;

/// <summary>
/// Branch side of the legal desk (محاسب القانونية / مدير القانونية / المدير): read-only.
/// A customer marked قانونية leaves his branch; his later payments are recorded in القانونية
/// itself, never in this branch's boxes, so this controller only reads.
/// Reached only through the sales gateway with the legal-viewer stamp; branch logins get 403.
/// Amounts are Iraqi dinars.
/// </summary>
[Authorize]
[Route("api/legal-branch")]
[ApiController]
public sealed class LegalBranchController : ControllerBase
{
    private readonly string _connectionString;

    public LegalBranchController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DataBaseConnection")!;
    }

    private const string CustomerColumns = """
v.CustomerID AS CustomerId, v.CustomerName, v.PhoneNumber, v.Address, v.ShopName,
v.DelegateID AS DelegateId, v.DelegateName,
CAST(ISNULL(v.IsLegal, 0) AS bit) AS IsLegal,
CAST(ISNULL(v.AmountTotalSales, 0) AS bigint) AS Total,
CAST(ISNULL(v.ReceiptsTotal, 0) AS bigint) AS Paid,
CAST(ISNULL(v.AmountRemaining, 0) AS bigint) AS Remaining,
v.LastPaymentDate, v.DateSaleDevice AS SaleDate, v.ItemsNames
""";

    /// <summary>Folds the letter variants people type differently, so names match across sales.</summary>
    private static string Norm(string expression) =>
        $"REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE({expression}, N'أ', N'ا'), N'إ', N'ا'), N'آ', N'ا'), N'ة', N'ه'), N'ى', N'ي'), N'  ', N' ')";

    private IActionResult? Gate()
    {
        var fromGateway = string.Equals(
            User.FindFirst(SalesGatewayKeyHandler.AuthSourceClaim)?.Value,
            SalesGatewayKeyHandler.AuthSourceGateway,
            StringComparison.Ordinal);
        if (!fromGateway || !LegalVisibility.IsLegalViewer(HttpContext))
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "هذه الواجهة للقانونية فقط" });
        return null;
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken ct)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    /// <summary>The lists that hold legal customers, with how many each holds.</summary>
    [HttpGet("lists")]
    public async Task<IActionResult> Lists(CancellationToken ct)
    {
        if (Gate() is { } denied) return denied;
        await using var connection = await OpenAsync(ct);
        var rows = await connection.QueryAsync<ListRow>(new CommandDefinition("""
SELECT ISNULL(d.DelegateID, 0) AS DelegateId, ISNULL(d.DelegateName, N'بدون قائمة') AS DelegateName,
       COUNT(*) AS LegalCustomers
FROM dbo.Customers c
LEFT JOIN dbo.Delegates d ON d.DelegateID = c.DelegateID
WHERE ISNULL(c.IsLegal, 0) = 1 AND ISNULL(c.CustomerState, 1) = 1
GROUP BY d.DelegateID, d.DelegateName
ORDER BY COUNT(*) DESC, d.DelegateName;
""", cancellationToken: ct));
        return Ok(rows);
    }

    /// <summary>
    /// scope=legal: customers marked قانونية, optionally of one list. scope=all: any customer,
    /// finished sales included — used to find a debtor's other sales when grouping.
    /// </summary>
    [HttpGet("customers")]
    public async Task<IActionResult> Customers(
        [FromQuery] int? delegateId,
        [FromQuery] string? q,
        [FromQuery] string? scope,
        [FromQuery] int take = 500,
        CancellationToken ct = default)
    {
        if (Gate() is { } denied) return denied;
        var all = string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase);
        var text = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        if (all && text is null)
            return BadRequest(new { message = "اكتب اسماً أو رقم هاتف للبحث" });

        await using var connection = await OpenAsync(ct);
        var rows = await connection.QueryAsync<CustomerRow>(new CommandDefinition($"""
SELECT TOP (@Take) {CustomerColumns}
FROM dbo.View_CustomersDelegate v
WHERE (@DelegateId IS NULL OR v.DelegateID = @DelegateId)
  AND (@All = 1 OR ISNULL(v.IsLegal, 0) = 1)
  AND (@Text IS NULL
       OR {Norm("v.CustomerName")} LIKE N'%' + {Norm("@Text")} + N'%'
       OR v.PhoneNumber LIKE N'%' + @Text + N'%')
ORDER BY v.CustomerName;
""", new { Take = Math.Clamp(take, 1, 5000), DelegateId = delegateId, All = all ? 1 : 0, Text = text }, cancellationToken: ct));
        return Ok(rows);
    }

    public sealed class IdsBody
    {
        public List<int>? Ids { get; set; }
    }

    /// <summary>Balances as the branch knows them for the accounts of a grouped debtor.</summary>
    [HttpPost("customers/by-ids")]
    public async Task<IActionResult> CustomersByIds([FromBody] IdsBody body, CancellationToken ct)
    {
        if (Gate() is { } denied) return denied;
        var ids = (body.Ids ?? new List<int>()).Where(id => id > 0).Distinct().Take(1500).ToArray();
        if (ids.Length == 0)
            return Ok(Array.Empty<CustomerRow>());
        await using var connection = await OpenAsync(ct);
        var rows = await connection.QueryAsync<CustomerRow>(new CommandDefinition(
            $"SELECT {CustomerColumns} FROM dbo.View_CustomersDelegate v WHERE v.CustomerID IN @Ids;",
            new { Ids = ids }, cancellationToken: ct));
        return Ok(rows);
    }

    /// <summary>Other accounts that look like the same person (same phone, or same normalized name).</summary>
    [HttpGet("customers/{customerId:int}/similar")]
    public async Task<IActionResult> Similar(int customerId, CancellationToken ct)
    {
        if (Gate() is { } denied) return denied;
        await using var connection = await OpenAsync(ct);
        var rows = await connection.QueryAsync<CustomerRow>(new CommandDefinition($"""
DECLARE @Name NVARCHAR(255), @Phone NVARCHAR(255);
SELECT @Name = LTRIM(RTRIM(CustomerName)), @Phone = LTRIM(RTRIM(PhoneNumber))
FROM dbo.Customers WHERE CustomerID = @Id;

SELECT TOP (100) {CustomerColumns}
FROM dbo.View_CustomersDelegate v
WHERE v.CustomerID <> @Id
  AND ((LEN(ISNULL(@Phone, N'')) >= 7 AND LTRIM(RTRIM(v.PhoneNumber)) = @Phone)
       OR (LEN(ISNULL(@Name, N'')) >= 5 AND {Norm("LTRIM(RTRIM(v.CustomerName))")} = {Norm("@Name")}))
ORDER BY v.DateSaleDevice DESC;
""", new { Id = customerId }, cancellationToken: ct));
        return Ok(rows);
    }

    [HttpGet("customers/{customerId:int}/detail")]
    public async Task<IActionResult> Detail(int customerId, CancellationToken ct)
    {
        if (Gate() is { } denied) return denied;
        await using var connection = await OpenAsync(ct);
        var customer = await connection.QueryFirstOrDefaultAsync<CustomerRow>(new CommandDefinition(
            $"SELECT {CustomerColumns} FROM dbo.View_CustomersDelegate v WHERE v.CustomerID = @Id;",
            new { Id = customerId }, cancellationToken: ct));
        if (customer is null)
            return NotFound(new { message = "الزبون غير موجود" });

        var sales = await connection.QueryAsync(new CommandDefinition("""
SELECT CustomerSaleID AS saleId, DateCreate AS saleDate, NumberOfItemsSales AS items,
       CAST(ROUND(ISNULL(AmountTotalSalesDenar, 0), -3) AS bigint) AS total
FROM dbo.View_CustomersSalesDelegate WHERE CustomerID = @Id ORDER BY DateCreate DESC;
""", new { Id = customerId }, cancellationToken: ct));
        var payments = await connection.QueryAsync(new CommandDefinition("""
SELECT TOP (300) CustomerPaymentID AS paymentId, PaymentDate AS paymentDate,
       CAST(ROUND(ISNULL(AmountDenar, 0), 0) AS bigint) AS amount, UserName AS userName, BoxName AS boxName
FROM dbo.View_CustomersPayments WHERE CustomerID = @Id ORDER BY PaymentDate DESC;
""", new { Id = customerId }, cancellationToken: ct));
        return Ok(new { customer, sales, payments });
    }

    public sealed class ListRow
    {
        public int DelegateId { get; set; }
        public string? DelegateName { get; set; }
        public int LegalCustomers { get; set; }
    }

    /// <summary>This route is exempt from the visibility filter, so the rows may carry CustomerId / IsLegal.</summary>
    public sealed class CustomerRow
    {
        public int CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public string? ShopName { get; set; }
        public int? DelegateId { get; set; }
        public string? DelegateName { get; set; }
        public bool IsLegal { get; set; }
        public long Total { get; set; }
        public long Paid { get; set; }
        public long Remaining { get; set; }
        public DateTime? LastPaymentDate { get; set; }
        public DateTime? SaleDate { get; set; }
        public string? ItemsNames { get; set; }
    }
}
