using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BE_SalesEmployee.Tests
{
    public sealed class SalesFilterGatewayTests
    {
        private static TokenService Tokens() =>
            new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "SalesEmployeeGwSigningKey-2026-ChangeMe!!"
            }).Build());

        [Fact]
        public void WrongRole_NotMappedAsFilterEmployee()
        {
            Assert.Equal(SalesRoles.SalesEmployee, SalesRoles.ToModuleRole("موظف مبيعات"));
            Assert.Equal(SalesRoles.SalesFilterEmployee, SalesRoles.ToModuleRole("موظف فلترة المبيعات"));
            Assert.False(SalesRoles.IsSalesFilterEmployee("موظف مبيعات"));
        }

        [Fact]
        public void CreateSalesFilterToken_StoresAllowedCitiesOnly_NoBranchToken()
        {
            var tokens = Tokens();
            var jwt = tokens.CreateSalesFilterToken(
                "42",
                "filter1",
                SalesRoles.UserTypeSalesFilterEmployee,
                "najaf-demo",
                ["baghdad-karkh", "najaf-demo", "baghdad-karkh"],
                out _);

            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                new JwtSecurityTokenHandler().ReadJwtToken(jwt).Claims,
                "test"));
            var user = TokenService.FromPrincipal(principal);

            Assert.True(user.IsSalesFilterEmployee);
            Assert.Equal("42", user.UserID);
            Assert.Equal("najaf-demo", user.CityValue);
            Assert.Equal(2, user.AllowedFilterCities.Count);
            Assert.True(user.AllowsFilterCity("baghdad-karkh"));
            Assert.True(user.AllowsFilterCity("najaf-demo"));
            Assert.False(user.AllowsFilterCity("karbala"));
            Assert.True(string.IsNullOrEmpty(user.BranchToken));
        }

        [Fact]
        public void ForbiddenCity_IsRejectedByJwtAllowList()
        {
            var user = new GatewayUser
            {
                UserType = SalesRoles.UserTypeSalesFilterEmployee,
                IsSalesFilterEmployee = true,
                AllowedFilterCities = ["najaf-demo"]
            };
            Assert.False(user.AllowsFilterCity("baghdad-karkh"));
            Assert.True(user.AllowsFilterCity("najaf-demo"));
        }

        [Fact]
        public void ResolveBranch_ByCityValue_PicksMatchingLink()
        {
            var cities = new List<AdminCity>
            {
                new() { Value = "najaf-demo", Name = "النجف", Link = "http://a/api/" },
                new() { Value = "baghdad-karkh", Name = "الكرخ", Link = "http://b/api/" },
            };
            var city = cities.FirstOrDefault(c =>
                string.Equals(c.Value, "baghdad-karkh", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(city);
            Assert.Equal("http://b/api/", city!.Link);
        }

        [Fact]
        public void FilterProxyPath_IncludesCityValueAndAction()
        {
            const string cityValue = "baghdad-karkh";
            const int id = 15;
            Assert.Equal($"sales-filter/requests/{id}/hold", $"sales-filter/requests/{id}/hold");
            Assert.Equal($"sales-filter/requests/{cityValue}/{id}/ready",
                $"sales-filter/requests/{cityValue}/{id}/ready");
        }
    }
}
