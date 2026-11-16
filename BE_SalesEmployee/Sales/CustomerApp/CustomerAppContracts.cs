using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using BE_SalesEmployee.Services;

namespace BE_SalesEmployee.Sales.CustomerApp;

// Gateway contracts for BE_CustomerApp (api/internal/customer-app/*). cityValue is the AdminCity value as an int.
// Account / sale / payment shapes mirror BE_Company.Sales.DTO.CustomerAppDTOs.

public sealed class CustomerAppBranch
{
    public int CityValue { get; set; }
    public string Name { get; set; } = "";
    [JsonIgnore] public AdminCity City { get; set; } = new();
}

public sealed class CustomerAppItem
{
    public string Name { get; set; } = "";
    public int Quantity { get; set; }
}

public sealed class CustomerAppAccount
{
    // Branches send their own config BranchId here (may be non-numeric); the gateway overwrites it.
    [JsonConverter(typeof(LenientIntConverter))]
    public int CityValue { get; set; }
    public string? CityName { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public DateTime? SaleDate { get; set; }
    public List<CustomerAppItem> Items { get; set; } = [];
    public long TotalAmount { get; set; }
    public long DailyInstallment { get; set; }
    public long PaidAmount { get; set; }
    public long RemainingAmount { get; set; }
    public int PaymentsCount { get; set; }
    public DateTime? LastPaymentDate { get; set; }
}

public sealed class CustomerAppAccountsResult
{
    public bool Partial { get; set; }
    public List<string> UnavailableBranches { get; set; } = [];
    public List<CustomerAppAccount> Accounts { get; set; } = [];
}

public sealed class CustomerAppSale
{
    public int SaleId { get; set; }
    public DateTime? Date { get; set; }
    public List<CustomerAppItem> Items { get; set; } = [];
    public long TotalAmount { get; set; }
    public long DailyInstallment { get; set; }
}

public sealed class CustomerAppPayment
{
    public long PaymentId { get; set; }
    public DateTime? Date { get; set; }
    public long Amount { get; set; }
}

/// <summary>Payment progress of one sale (see BE_Company CustomerAppSaleProgressDTO), stamped with the branch.</summary>
public sealed class CustomerAppSaleProgress
{
    public int CityValue { get; set; }
    public int SaleId { get; set; }
    public int CustomerId { get; set; }
    public long TotalAmount { get; set; }
    public long PaidAmount { get; set; }
    public long RemainingAmount { get; set; }
    public int PaymentsCount { get; set; }
    public DateTime? LastPaymentDate { get; set; }
}

public sealed class CustomerAppAccountDetail
{
    public CustomerAppAccount Account { get; set; } = new();
    public List<CustomerAppSale> Sales { get; set; } = [];
    public List<CustomerAppPayment> Payments { get; set; } = [];
}

public sealed class CustomerAppSalesRequestBody
{
    public Guid OrderUuid { get; set; }
    public int CityValue { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerProvince { get; set; }
    public string? CustomerAddress { get; set; }
    public string? Notes { get; set; }
    public string? ProductName { get; set; }
    /// <summary>The field agent (المروج) who made the order; shown as the request source.</summary>
    public string? SubmittedByName { get; set; }
}

public sealed class CustomerAppSalesRequestCreated
{
    public int CityValue { get; set; }
    public string? CityName { get; set; }
    public int SalesRequestId { get; set; }
    public string? Status { get; set; }
    public bool IdempotentReplay { get; set; }
}

public sealed class CustomerAppStatusQueryItem
{
    public int CityValue { get; set; }
    public Guid OrderUuid { get; set; }
}

public sealed class CustomerAppStatusBody
{
    public List<CustomerAppStatusQueryItem>? Items { get; set; }
}

public sealed class CustomerAppStatusItem
{
    public Guid OrderUuid { get; set; }
    public int CityValue { get; set; }
    public bool Found { get; set; }
    public int? SalesRequestId { get; set; }
    public string? Status { get; set; }
    public string? FilterStatus { get; set; }
    public bool IsDeleted { get; set; }
    public string? RejectionReason { get; set; }
    public int? ConvertedToSaleId { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

public sealed class CustomerAppStatusResult
{
    public List<CustomerAppStatusItem> Items { get; set; } = [];
    /// <summary>Branches that did not answer; their items are omitted (keep the last known status).</summary>
    public List<string> UnavailableBranches { get; set; } = [];
}

/// <summary>Reads numbers or numeric strings; anything else becomes 0. Writes a plain number.</summary>
public sealed class LenientIntConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var n))
        {
            return n;
        }

        if (reader.TokenType == JsonTokenType.String
            && int.TryParse(reader.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var s))
        {
            return s;
        }

        reader.Skip();
        return 0;
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value);
}

/// <summary>Result of a gateway call: HTTP status + body to return as-is.</summary>
public sealed record CustomerAppGatewayResult(int Status, object? Body);
