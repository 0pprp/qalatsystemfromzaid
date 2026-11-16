using BE_Company.Sales.DTO;
using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests
{
    public sealed class SalesRequestBoardFilterTests
    {
        [Fact]
        public void Date_range_is_inclusive_Iraq_days()
        {
            var filter = SalesRequestBoardFilter.Parse("2026-11-01", "2026-11-01", null, null, null);

            var inside = Row(new DateTime(2026, 10, 31, 21, 30, 0, DateTimeKind.Utc));
            var before = Row(new DateTime(2026, 10, 31, 20, 0, 0, DateTimeKind.Utc));
            var eveningOnCard = Row(new DateTime(2026, 11, 1, 22, 0, 0, DateTimeKind.Unspecified));
            var after = Row(new DateTime(2026, 11, 2, 10, 0, 0, DateTimeKind.Utc));

            Assert.True(filter.Matches(inside));
            Assert.False(filter.Matches(before));
            Assert.True(filter.Matches(eveningOnCard));
            Assert.False(filter.Matches(after));
        }

        [Fact]
        public void Whole_delegate_group_excludes_other_sources()
        {
            var filter = SalesRequestBoardFilter.Parse(null, null, "delegate", null, "1");
            Assert.True(filter.Matches(Row(DateTime.UtcNow, "Delegate", "مندوب", "أحمد", 4)));
            Assert.False(filter.Matches(Row(DateTime.UtcNow, "EmployeeSubmitted", "موظف مبيعات", "علي", 9)));
            Assert.False(filter.Matches(Row(DateTime.UtcNow, "NewCustomer", "مدير مبيعات", "مدير", 1)));
        }

        [Fact]
        public void Specific_person_matches_id_or_name_inside_that_source_only()
        {
            var filter = SalesRequestBoardFilter.Parse(null, null, null, "employee|9|علي~follower|3|سارة", "1");
            Assert.True(filter.Matches(Row(DateTime.UtcNow, "EmployeeSubmitted", "موظف مبيعات", "شخص", 9)));
            Assert.True(filter.Matches(Row(DateTime.UtcNow, "Follower", "متابع", "سارة", 99)));
            Assert.False(filter.Matches(Row(DateTime.UtcNow, "EmployeeSubmitted", "موظف مبيعات", "علي", 8)));
            Assert.False(filter.Matches(Row(DateTime.UtcNow, "Delegate", "مندوب", "سارة", 3)));
        }

        [Fact]
        public void Delegate_list_id_counts_as_that_delegate()
        {
            var filter = SalesRequestBoardFilter.Parse(null, null, null, "delegate|najaf|15|حيدر", "1");
            var row = Row(DateTime.UtcNow, "Delegate", "مندوب", "مندوب", 4);
            row.SourceListId = 15;
            Assert.True(filter.Matches(row));
        }

        [Fact]
        public void Source_flag_with_no_selection_matches_nothing()
        {
            var filter = SalesRequestBoardFilter.Parse(null, null, null, null, "1");
            Assert.False(filter.Matches(Row(DateTime.UtcNow, "Delegate", "مندوب", "أحمد", 4)));
        }

        [Fact]
        public void Empty_filter_matches_every_row()
        {
            var filter = SalesRequestBoardFilter.Parse(null, null, null, null, null);
            Assert.False(filter.IsActive);
            Assert.True(filter.Matches(Row(DateTime.UtcNow, "Crm", "Crm", "قسم التسويق", 0)));
            Assert.True(filter.Matches(Row(DateTime.UtcNow, "CustomerApp", "CustomerApp", "المروج", 0)));
        }

        [Fact]
        public void Swapped_dates_still_cover_the_later_day()
        {
            var filter = SalesRequestBoardFilter.Parse("2026-11-06", "2026-11-01", null, null, null);
            var day = new DateTime(2026, 11, 5, 10, 0, 0, DateTimeKind.Utc);
            Assert.True(filter.IsActive);
            Assert.True(filter.Matches(Row(day)));
        }

        [Fact]
        public void One_date_covers_that_Iraq_day_only()
        {
            var filter = SalesRequestBoardFilter.Parse("2026-11-01", null, null, null, null);
            Assert.Equal(new DateTime(2026, 11, 1), filter.FromIraqDate);
            Assert.Equal(new DateTime(2026, 11, 1), filter.ToIraqDate);
            Assert.True(filter.Matches(Row(new DateTime(2026, 11, 1, 20, 30, 0, DateTimeKind.Utc))));
            Assert.True(filter.Matches(Row(new DateTime(2026, 11, 1, 22, 15, 0, DateTimeKind.Unspecified))));
            Assert.False(filter.Matches(Row(new DateTime(2026, 11, 2, 10, 0, 0, DateTimeKind.Utc))));
        }

        [Fact]
        public void Follower_and_delegate_role_variants_count_in_the_group()
        {
            var filter = SalesRequestBoardFilter.Parse(null, null, "follower,delegate", null, "1");
            Assert.True(filter.Matches(Row(DateTime.UtcNow, "NewCustomer", "متابع رئيسي", "سارة", 3)));
            Assert.True(filter.Matches(Row(DateTime.UtcNow, "ExistingCustomer", "مندوب مبيعات", "أحمد", 4)));
        }

        [Fact]
        public void Marketing_and_manager_sources_keep_both_groups()
        {
            var filter = SalesRequestBoardFilter.Parse(null, null, "crm,manager", null, "1");
            Assert.True(filter.Matches(Row(DateTime.UtcNow, "Crm", "Crm", "قسم التسويق", 0)));
            Assert.True(filter.Matches(Row(DateTime.UtcNow, "NewCustomer", "قسم التسويق", "قسم التسويق", 0)));
            Assert.True(filter.Matches(Row(DateTime.UtcNow, "NewCustomer", "مدير مبيعات", "مدير", 1)));
            Assert.True(filter.Matches(Row(DateTime.UtcNow, "ExistingCustomer", "المدير", "المدير", 2)));
            Assert.False(filter.Matches(Row(DateTime.UtcNow, "Delegate", "مندوب", "أحمد", 4)));
            Assert.False(filter.Matches(Row(DateTime.UtcNow, "EmployeeSubmitted", "موظف مبيعات", "علي", 9)));
            Assert.Equal("crm", SalesRequestBoardFilter.Category(Row(DateTime.UtcNow, "NewCustomer", "مدير مبيعات", "قسم التسويق", 1)));
            Assert.DoesNotContain("STRING_SPLIT", SalesRequestBoardFilter.SqlFor(filter));
        }

        [Fact]
        public void One_promoter_matches_that_name_only()
        {
            var filter = SalesRequestBoardFilter.Parse(null, null, null, "promoter|all|7|أحمد علي", "1");
            Assert.True(filter.Matches(Row(DateTime.UtcNow, "CustomerApp", "CustomerApp", "أحمد علي", 0)));
            Assert.False(filter.Matches(Row(DateTime.UtcNow, "CustomerApp", "CustomerApp", "شخص آخر", 0)));
            Assert.False(filter.Matches(Row(DateTime.UtcNow, "NewCustomer", "مدير مبيعات", "أحمد علي", 1)));
        }

        [Fact]
        public void Numeric_city_person_token_uses_the_user_id_not_the_city()
        {
            var filter = SalesRequestBoardFilter.Parse(null, null, null, "delegate|9|15|حيدر", "1");
            var row = Row(DateTime.UtcNow, "Delegate", "مندوب", "مندوب", 4);
            row.SourceListId = 15;
            Assert.True(filter.Matches(row));
        }

        private static SalesRequestDTO Row(
            DateTime created,
            string source = "NewCustomer",
            string role = "مدير مبيعات",
            string name = "مدير",
            int userId = 1) => new()
        {
            CreatedAtUtc = created,
            CustomerSourceType = source,
            CreatedByUserType = role,
            CreatedByName = name,
            CreatedByUserId = userId,
            CustomerName = "زبون"
        };
    }
}
