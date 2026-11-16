using BE_DelegateWebApplication.Services;
using Xunit;

namespace BE_DelegateWebApplication.Tests
{
    public sealed class BranchProvinceTests
    {
        [Fact]
        public void Kirkuk_DatabaseName_MapsToGetCityIdAndArabicName()
        {
            var hit = BranchProvince.FromDatabaseName("DatabaseCompanyKarkok");
            Assert.NotNull(hit);
            Assert.Equal(14, hit!.Value.CityId);
            Assert.Equal("كركوك", hit.Value.Name);
        }

        [Theory]
        [InlineData("databasecompanykarkok")]
        [InlineData("  DatabaseCompanyKarkok  ")]
        public void DatabaseName_IsCaseAndWhitespaceInsensitive(string db)
        {
            Assert.Equal(14, BranchProvince.FromDatabaseName(db)!.Value.CityId);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("SomeOtherDb")]
        public void UnknownDatabase_ReturnsNull(string? db)
        {
            Assert.Null(BranchProvince.FromDatabaseName(db));
        }

        [Fact]
        public void FirstNonEmpty_PrefersCitiesRow_ThenStaticLabel()
        {
            Assert.Equal("كركوك - المركز", BranchProvince.FirstNonEmpty(" كركوك - المركز ", "كركوك"));
            Assert.Equal("كركوك", BranchProvince.FirstNonEmpty(null, "  ", "كركوك"));
            Assert.Null(BranchProvince.FirstNonEmpty(null, ""));
        }
    }
}
