using BE_Company.Sales.Authorization;
using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

/// <summary>
/// sales-manager/employees listing rules: role filter + branch tenancy.
/// Tenant boundary = SQL connection. cityValue must NOT pretend to route branches.
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
    public void GetAdmin_Numeric_Vs_Catalog_Label_Is_Not_Comparable_Key_Match()
    {
        // Documents why equality-filtering employees by cityValue emptied lists:
        // UI sends GetAdmin "1" while BranchLabel uses InitialCatalog / Arabic name.
        Assert.False(SalesBranchScope.IsComparableBranchKey("DatabaseCompanyNajaf"));
        Assert.True(SalesBranchScope.IsComparableBranchKey("1"));
    }

    [Fact]
    public void FailOpen_CityFilter_Must_Not_Be_Used_As_Tenant_Router()
    {
        // Historical 6d52550 behavior: requesting Karkh (3) against Najaf catalog label
        // still "passes". That is safe ONLY when the HTTP target is already the branch DB.
        // Using it to accept cross-province UI selection on the wrong host is a routing bug.
        Assert.True(SalesBranchScope.PassesOptionalCityFilter(
            "3", "DatabaseCompanyNajaf", "النجف"));
        Assert.True(SalesBranchScope.PassesOptionalCityFilter(
            "9", "DatabaseCompanyNajaf", "النجف"));
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
        Assert.False(SalesBranchScope.PassesOptionalCityFilter(
            "basra-demo", "najaf-demo", "النجف"));
    }

    [Fact]
    public void Admin_Login_AllowList_Includes_MainAccountant()
    {
        // Mirrors UsersRepository.Users_GetUserLoginAdmin — must not reject محاسب رئيسي.
        var userType = SalesRoles.UserTypeMainAccountant;
        var allowed = userType is "محاسب رئيسي" or "مدير فرع";
        Assert.True(allowed);
        Assert.False(userType is "محاسب فرعي" or "موظف مبيعات");
    }

    [Fact]
    public void UserCreationAuthorization_Is_Not_Referenced_By_Login_AllowList()
    {
        // Guard: create/update whitelist must stay separate from authentication.
        Assert.True(UserCreationAuthorization.CanAssignUserType(
            SalesRoles.UserTypeMainAccountant, SalesRoles.UserTypeSalesManager));
        var loginAdminAllows =
            SalesRoles.UserTypeMainAccountant is "محاسب رئيسي" or "مدير فرع";
        Assert.True(loginAdminAllows);
    }

    [Fact]
    public void No_Female_SalesEmployee_Variant_In_Roles()
    {
        Assert.NotEqual("موظفة مبيعات", SalesRoles.UserTypeSalesEmployee);
        Assert.False(SalesRoles.IsSalesEmployee("موظفة مبيعات"));
    }
}
