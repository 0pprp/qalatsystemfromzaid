using BE_Company.DTO;
using BE_Company.Services.LegalSearch;
using Xunit;

namespace BE_Company.Sales.Tests;

/// <summary>
/// Accountant «بحث القانونية»: full triple-name match (no family) + phone match + export dedupe.
/// </summary>
public class LegalCustomerSearchTests
{
    [Fact]
    public void LegalNormalize_MapsAlefTehMarbutaAndStripsDeviceSuffix()
    {
        Assert.Equal("احمد علي", LegalArabicText.Normalize("أحمد على"));
        Assert.Equal("فاطمه محمد", LegalArabicText.Normalize("فاطمة محمد"));
        Assert.Equal("علاء حميد حمزه", LegalArabicText.Normalize("علاء حميد حمزة-A51"));
        Assert.Equal("اركان هلال حسين", LegalArabicText.Normalize("اركان هلال حسين-Y9a"));
    }

    [Fact]
    public void NameMatch_IsFullTripleOnly_NotFamilyBrothers()
    {
        Assert.True(LegalCustomerNameMatch.IsNameMatch("حسين محمد علي", "حسين محمد علي"));
        Assert.True(LegalCustomerNameMatch.IsNameMatch("حسين محمد علي حسن", "حسين محمد علي"));
        // brothers via father+grandfather must NOT match
        Assert.False(LegalCustomerNameMatch.IsNameMatch("علي محمد علي", "حسين محمد علي"));
        Assert.False(LegalCustomerNameMatch.IsNameMatch("كرار محمد علي", "حسين محمد علي"));
    }

    [Fact]
    public void Engine_SeparatesNameAndPhone_AndDedupesExportWhenSameCustomer()
    {
        var catalog = new List<CustomersGetDTO>
        {
            Cust(1, "حسين محمد علي", "07801234567"),
            Cust(2, "علي محمد علي", "07809999999"),
            Cust(3, "سارة احمد", "07805554433"),
        };

        var queries = new[]
        {
            new LegalSearchQueryInput { Name = "حسين محمد علي", Phone = "07801234567" }, // same account both
            new LegalSearchQueryInput { Name = "سارة", Phone = "07805554433" }, // phone only (name too short / not logical)
            new LegalSearchQueryInput { Name = "علي محمد علي", Phone = null },
        };

        var result = LegalCustomerSearchEngine.Run(catalog, queries);

        var q0 = result.Queries[0];
        Assert.Single(q0.NameMatches);
        Assert.Equal(1, q0.NameMatches[0].CustomerID);
        Assert.Single(q0.PhoneMatches);
        Assert.Equal(1, q0.PhoneMatches[0].CustomerID);

        // export: customer 1 once even though name+phone both hit
        Assert.Contains(result.ExportRows, r => r.Customer.CustomerID == 1);
        Assert.Equal(1, result.ExportRows.Count(r => r.Customer.CustomerID == 1));

        var q2 = result.Queries[2];
        Assert.Single(q2.NameMatches);
        Assert.Equal(2, q2.NameMatches[0].CustomerID);
        Assert.Empty(q2.PhoneMatches);

        // different customers stay separate rows
        Assert.True(result.ExportRows.Count >= 2);
        Assert.Contains(result.ExportRows, r => r.Customer.CustomerID == 2);
    }

    [Fact]
    public void Engine_DifferentAccountsForNameAndPhone_TwoExportRows()
    {
        var catalog = new List<CustomersGetDTO>
        {
            Cust(10, "محمد جاسم علي", "07801111111"),
            Cust(20, "شخص اخر", "07802222222"),
        };

        var result = LegalCustomerSearchEngine.Run(catalog, new[]
        {
            new LegalSearchQueryInput { Name = "محمد جاسم علي", Phone = "07802222222" }
        });

        Assert.Equal(10, Assert.Single(result.Queries[0].NameMatches).CustomerID);
        Assert.Equal(20, Assert.Single(result.Queries[0].PhoneMatches).CustomerID);
        Assert.Equal(2, result.ExportRows.Count);
        Assert.Contains(result.ExportRows, r => r.Customer.CustomerID == 10 && r.MatchKinds.Contains("name"));
        Assert.Contains(result.ExportRows, r => r.Customer.CustomerID == 20 && r.MatchKinds.Contains("phone"));
    }

    private static CustomersGetDTO Cust(int id, string name, string phone) => new()
    {
        CustomerID = id,
        CustomerName = name,
        PhoneNumber = phone,
        AmountTotalSales = 100,
        ReceiptRateDevice = 50
    };
}
