using System.Reflection;
using BE_SalesEmployee.Controllers;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Sales.Services;
using BE_SalesEmployee.Services;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace BE_SalesEmployee.Tests;

/// <summary>«ادارة الفرع» on the dashboard — المدير / المراقبين / لجنة المراقبة, committee scoped to its provinces.</summary>
public sealed class BranchAppOversightRulesTests
{
    [Fact]
    public void Controller_uses_director_monitor_committee_policy()
    {
        var attr = Assert.Single(typeof(BranchAppOversightController).GetCustomAttributes<AuthorizeAttribute>(inherit: true));
        Assert.Equal(SalesPolicies.DirectorOrMonitor, attr.Policy);
    }

    [Theory]
    [InlineData("المدير", "", "DatabaseCompanyNajaf", true)]
    [InlineData("مسؤول المراقبين", "", "DatabaseCompanyBasra", true)]
    [InlineData("مسؤول لجنة المراقبة", "DatabaseCompanyNajaf,DatabaseCompanyKarbala", "DatabaseCompanyKarbala", true)]
    [InlineData("مساعد مسؤول لجنة المراقبة", "DatabaseCompanyNajaf", "databasecompanynajaf", true)]
    [InlineData("مسؤول لجنة المراقبة", "DatabaseCompanyNajaf", "DatabaseCompanyBasra", false)]
    [InlineData("مساعد مسؤول لجنة المراقبة", "", "DatabaseCompanyNajaf", false)]
    [InlineData("المدير", "", "", false)]
    public void Committee_is_limited_to_assigned_provinces(string userType, string allowed, string city, bool expected)
    {
        var actor = new GatewayUser
        {
            UserType = userType,
            AllowedFilterCities = allowed.Split(',', StringSplitOptions.RemoveEmptyEntries),
        };
        Assert.Equal(expected, BranchAppOversightController.CanAccessCity(actor, city));
    }

    [Fact]
    public void Actor_display_names_role()
    {
        Assert.Equal("حيدر (المدير)", BranchAppOversightController.ActorDisplay(new GatewayUser { UserName = "حيدر", UserType = "المدير" }));
        Assert.Equal("الإدارة", BranchAppOversightController.ActorDisplay(new GatewayUser()));
    }

    [Fact]
    public void Payments_path_matches_branch_route()
    {
        var path = BranchAppOversightController.PaymentsPath(new DateTime(2026, 11, 1), new DateTime(2026, 11, 15), 12, null);
        Assert.Equal("CustomersPayments/CustomersPayments_GetAll/2026-11-01&&2026-11-15&&12&&null", path);
        var searched = BranchAppOversightController.PaymentsPath(new DateTime(2026, 11, 1), new DateTime(2026, 11, 1), 3, " علي ");
        Assert.EndsWith("&&3&&" + Uri.EscapeDataString("علي"), searched);
    }

    [Theory]
    [InlineData("branch-oversight/new-sales")]
    [InlineData("branch-oversight/customers/declining")]
    [InlineData("CustomerDecisions/WeakWeekPayers")]
    [InlineData("CustomerDecisions/Decisions?fromDate=2026-11-01")]
    [InlineData("CustomersPayments/CustomersPayments_GetAll/2026-11-01&&2026-11-15&&12&&null")]
    public void Slow_branch_reads_get_long_timeout(string path) =>
        Assert.True(SalesManagerBranchAggregator.NeedsLongBranchTimeout(path));
}
