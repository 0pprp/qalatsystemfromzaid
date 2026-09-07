using BE_Company.Sales.Services;
using System.Text.RegularExpressions;
using Xunit;

namespace BE_Company.Sales.Tests
{
    public class SalesDraftSchemaTests
    {
        [Fact]
        public void Commands_AreSeparateBatches_AddThenBackfillThenIndex()
        {
            Assert.Equal(
                new[]
                {
                    SalesDraftSchema.CoreSql,
                    SalesDraftSchema.AddPostingColumnsSql,
                    SalesDraftSchema.BackfillPostingSql,
                    SalesDraftSchema.PostingIndexesSql
                },
                SalesDraftSchema.Commands);

            Assert.Equal(4, SalesDraftSchema.Commands.Count);
        }

        [Fact]
        public void AddPostingColumns_DoesNotCompileUsageOfNewColumnsInSameBatch()
        {
            var sql = SalesDraftSchema.AddPostingColumnsSql;
            Assert.Contains("ADD PostingStatus", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ADD PostedAtUtc", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ADD PostingAttempts", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ADD LastPostingError", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("COL_LENGTH", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("UPDATE", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("CREATE INDEX", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("CREATE NONCLUSTERED INDEX", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("SELECT", sql, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Backfill_MarksOnlyNullLegacyCompletedAsPosted_NotExplicitPending()
        {
            var sql = Normalize(SalesDraftSchema.BackfillPostingSql);
            Assert.Contains("SET POSTINGSTATUS = N'POSTED'", sql);
            Assert.Contains("STATUS = N'COMPLETED'", sql);
            Assert.Contains("POSTINGSTATUS IS NULL", sql);
            Assert.Contains($"N'{SalesPostingStatuses.Posted.ToUpperInvariant()}'", sql);
            Assert.Contains($"N'{SalesPostingStatuses.Pending.ToUpperInvariant()}'", sql);
            Assert.DoesNotContain("ALTER TABLE", sql);
            Assert.DoesNotContain("ADD POSTINGSTATUS", sql);

            Assert.Matches(
                new Regex(@"WHERE\s+STATUS\s*=\s*N'COMPLETED'\s+AND\s+POSTINGSTATUS\s+IS\s+NULL", RegexOptions.IgnoreCase),
                SalesDraftSchema.BackfillPostingSql);
        }

        [Fact]
        public void PostingIndex_IsOwnBatch_AfterColumnExists()
        {
            var sql = SalesDraftSchema.PostingIndexesSql;
            Assert.Contains("IX_SalesDrafts_PostingDue", sql);
            Assert.Contains("CREATE NONCLUSTERED INDEX", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("EXEC", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("PostingStatus", sql);
            Assert.Contains("COL_LENGTH", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ALTER TABLE", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("UPDATE", sql, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void NoCommand_AddsPostingColumnsAndThenUsesThemInSameBatch()
        {
            foreach (var batch in SalesDraftSchema.Commands)
            {
                var adds = AddsPostingColumn(batch);
                var uses = UsesPostingColumn(batch);
                Assert.False(
                    adds && uses,
                    "SQL Server compiles a batch before ALTER TABLE ADD is visible. Split add vs UPDATE/INDEX.");
            }
        }

        [Fact]
        public void CoreSchema_DoesNotReferencePostingColumns()
        {
            var sql = SalesDraftSchema.CoreSql;
            Assert.DoesNotContain("PostingStatus", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PostedAtUtc", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PostingAttempts", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("LastPostingError", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("IF OBJECT_ID(N'dbo.SalesDrafts'", sql);
            Assert.Contains("COL_LENGTH", sql, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void EnsureSchema_UsesCommandListSoEmptyLegacyAndMigratedDatabasesStayIdempotent()
        {
            var method = typeof(SalesDraftRepository).GetMethod(nameof(SalesDraftRepository.EnsureSchemaAsync));
            Assert.NotNull(method);

            var add = SalesDraftSchema.AddPostingColumnsSql;
            Assert.Contains("IF COL_LENGTH(N'dbo.SalesDrafts', N'PostingStatus') IS NULL", add);
            Assert.Contains("IF COL_LENGTH(N'dbo.SalesDrafts', N'PostedAtUtc') IS NULL", add);
            Assert.Contains("IF COL_LENGTH(N'dbo.SalesDrafts', N'PostingAttempts') IS NULL", add);
            Assert.Contains("IF COL_LENGTH(N'dbo.SalesDrafts', N'LastPostingError') IS NULL", add);

            Assert.Contains("NOT EXISTS", SalesDraftSchema.PostingIndexesSql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("IX_SalesDrafts_PostingDue", SalesDraftSchema.PostingIndexesSql);

            var commands = SalesDraftSchema.Commands.ToList();
            Assert.True(commands.IndexOf(SalesDraftSchema.AddPostingColumnsSql) <
                        commands.IndexOf(SalesDraftSchema.BackfillPostingSql));
            Assert.True(commands.IndexOf(SalesDraftSchema.BackfillPostingSql) <
                        commands.IndexOf(SalesDraftSchema.PostingIndexesSql));
        }

        private static bool AddsPostingColumn(string sql) =>
            Regex.IsMatch(
                sql,
                @"ALTER\s+TABLE[\s\S]*ADD\s+(PostingStatus|PostedAtUtc|PostingAttempts|LastPostingError)",
                RegexOptions.IgnoreCase);

        private static bool UsesPostingColumn(string sql)
        {
            if (Regex.IsMatch(sql, @"UPDATE\s+dbo\.SalesDrafts[\s\S]*PostingStatus", RegexOptions.IgnoreCase))
            {
                return true;
            }

            if (Regex.IsMatch(sql, @"CREATE\s+(NONCLUSTERED\s+)?INDEX[\s\S]*PostingStatus", RegexOptions.IgnoreCase))
            {
                return true;
            }

            return Regex.IsMatch(sql, @"SELECT[\s\S]*PostingStatus", RegexOptions.IgnoreCase);
        }

        private static string Normalize(string sql) =>
            Regex.Replace(sql, @"\s+", " ").Trim().ToUpperInvariant();
    }
}
