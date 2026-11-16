using BE_SalesEmployee.Sales.Services;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class SalesRequestPeopleQueryTests
{
    [Fact]
    public void Scope_keeps_only_the_target_city_and_drops_the_city_segment()
    {
        var people = "delegate|najaf|4|أحمد~employee|basra|9|علي~follower|najaf|2|سارة";
        var scoped = SalesRequestPeopleQuery.Scope(people, "najaf");
        Assert.Equal("delegate|4|أحمد~follower|2|سارة", scoped);
    }

        [Fact]
        public void Scope_keeps_promoters_for_every_city()
        {
            var people = "promoter|all|7|أحمد~delegate|najaf|4|علي";
            Assert.Equal("promoter|7|أحمد~delegate|4|علي", SalesRequestPeopleQuery.Scope(people, "najaf"));
            Assert.Equal("promoter|7|أحمد", SalesRequestPeopleQuery.Scope(people, "basra"));
        }

        [Fact]
        public void RewritePath_removes_people_when_the_city_has_nobody_and_keeps_sourceFilter()
    {
        var path = "sales-manager/sales-requests?page=1&sourceFilter=1&people=employee%7Cbasra%7C9%7C%D8%B9%D9%84%D9%8A&tab=all";
        var rewritten = SalesRequestPeopleQuery.RewritePath(path, "najaf");
        Assert.DoesNotContain("people=", rewritten, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sourceFilter=1", rewritten, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tab=all", rewritten, StringComparison.OrdinalIgnoreCase);
    }
}
