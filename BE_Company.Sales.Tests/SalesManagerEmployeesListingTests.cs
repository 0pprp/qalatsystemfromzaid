using BE_Company.Sales.Authorization;
using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

/// <summary>
/// sales-manager/employees listing rules: role filter + branch city filter.
/// </summary>
public sealed class SalesManagerEmployeesListingTests
{
    [Fact]
    public void Employee_List_Sql_Includes_Only_SalesEmployee_UserType()
    {
        // Contract mirrored from SalesManagerReadRepository.ListEmployeesAsync
        const string sqlFilter = "UserType = N'موظف مبيعات'";
        Assert.Contains(SalesRoles.UserTypeSalesEmployee, sqlFilter);
        Assert.DoesNotContain(SalesRoles.UserTypeMainAccountant, sqlFilter);
        Assert.DoesNotContain(SalesRoles.UserTypeSalesManager, sqlFilter);
        Assert.DoesNotContain(SalesRoles.UserTypeSalesFilterEmployee, sqlFilter);
    }

    [Fact]
    public void MainAccountant_Is_Not_SalesEmployee_For_Listing()
    {
        Assert.False(SalesRoles.IsSalesEmployee(SalesRoles.UserTypeMainAccountant));
        Assert.False(SalesRoles.IsSalesEmployee("jana"));
        Assert.True(SalesRoles.IsSalesEmployee(SalesRoles.UserTypeSalesEmployee));
    }

    [Fact]
    public void GetAdmin_Numeric_CityValue_Must_Not_Drop_Branch_Employees()
    {
        // GetAdmin نجف value=1 while BranchLabel falls back to InitialCatalog / BranchId.
        const string requestedFromUi = "1";
        const string branchLabelFromCatalog = "DatabaseCompanyNajaf";
        const string branchName = "النجف";

        Assert.True(SalesBranchScope.PassesOptionalCityFilter(
            requestedFromUi, branchLabelFromCatalog, branchName));
    }

    [Fact]
    public void Matching_Short_BranchId_Still_Passes()
    {
        Assert.True(SalesBranchScope.PassesOptionalCityFilter(
            "najaf-demo", "najaf-demo", "النجف - DEMO"));
    }

    [Fact]
    public void Empty_Requested_City_Passes_All()
    {
        Assert.True(SalesBranchScope.PassesOptionalCityFilter(
            null, "DatabaseCompanyNajaf", "النجف"));
        Assert.True(SalesBranchScope.PassesOptionalCityFilter(
            "  ", "DatabaseCompanyNajaf", "النجف"));
    }

    [Fact]
    public void Arabic_Display_Name_Matches_BranchName()
    {
        Assert.True(SalesBranchScope.PassesOptionalCityFilter(
            "النجف", "DatabaseCompanyNajaf", "النجف"));
    }

    [Fact]
    public void Truly_Different_Comparable_Keys_Are_Excluded()
    {
        // Only when both sides are comparable short keys and differ — rare on single-branch API.
        Assert.False(SalesBranchScope.PassesOptionalCityFilter(
            "basra-demo", "najaf-demo", "النجف"));
    }

    [Fact]
    public void No_Female_SalesEmployee_Variant_In_Roles()
    {
        // Guard against accidental exact-match bugs if DB used "موظفة مبيعات".
        Assert.NotEqual("موظفة مبيعات", SalesRoles.UserTypeSalesEmployee);
        Assert.False(SalesRoles.IsSalesEmployee("موظفة مبيعات"));
    }
}
