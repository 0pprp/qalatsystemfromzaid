using System.Globalization;
using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;

namespace BE_Company.Sales.Services
{
    /// <summary>
    /// Director board filters: Iraq calendar dates plus source groups / specific people.
    /// People tokens use <c>role|city|id|name</c> separated by <c>~</c> (gateway strips city per branch).
    /// </summary>
    public sealed class SalesRequestBoardFilter
    {
        public const char TokenSeparator = '~';
        public const char FieldSeparator = '|';
        public const char NameSeparator = '\u001f';

        private const string CategorySql = @"
    CASE
      WHEN R.CustomerSourceType = N'CustomerApp' THEN N'promoter'
      WHEN R.CustomerSourceType = N'Crm'
        OR R.CreatedByUserType = N'Crm'
        OR R.CreatedByName = N'قسم التسويق'
        OR R.CreatedByUserType = N'قسم التسويق' THEN N'crm'
      WHEN R.CustomerSourceType = N'Follower'
        OR R.CreatedByUserType = N'Follower'
        OR R.CreatedByUserType LIKE N'متابع%' THEN N'follower'
      WHEN R.CustomerSourceType = N'Delegate'
        OR R.CreatedByUserType = N'Delegate'
        OR (R.CreatedByUserType LIKE N'%مندوب%' AND R.CreatedByUserType NOT LIKE N'%موظف%') THEN N'delegate'
      WHEN R.CustomerSourceType = N'EmployeeSubmitted' THEN N'employee'
      ELSE N'manager'
    END";

        /// <summary>
        /// The board prints Unspecified timestamps as their raw calendar date (no +3),
        /// and UTC timestamps as the Iraq calendar day. Keep both so a Nov 5 card stays on Nov 5.
        /// </summary>
        public static readonly string DateSqlAnd = @"
AND (@FromIraqDate IS NULL OR (
  CAST(R.CreatedAtUtc AS DATE) >= @FromIraqDate
  OR CAST(DATEADD(HOUR, 3, R.CreatedAtUtc) AS DATE) >= @FromIraqDate
))
AND (@ToIraqDate IS NULL OR (
  CAST(R.CreatedAtUtc AS DATE) <= @ToIraqDate
  OR CAST(DATEADD(HOUR, 3, R.CreatedAtUtc) AS DATE) <= @ToIraqDate
))";

        public static readonly string SourceSqlAnd = $@"
AND (
  {CategorySql} = N'delegate'
  AND (
    @AllDelegates = 1
    OR CHARINDEX(N',' + CAST(R.CreatedByUserId AS NVARCHAR(20)) + N',', N',' + @DelegateIds + N',') > 0
    OR (R.SourceListId IS NOT NULL AND CHARINDEX(N',' + CAST(R.SourceListId AS NVARCHAR(20)) + N',', N',' + @DelegateIds + N',') > 0)
    OR (ISNULL(R.CreatedByName, N'') <> N'' AND CHARINDEX(NCHAR(31) + R.CreatedByName + NCHAR(31), NCHAR(31) + @DelegateNames + NCHAR(31)) > 0)
  )
  OR (
    {CategorySql} = N'follower'
    AND (
      @AllFollowers = 1
      OR CHARINDEX(N',' + CAST(R.CreatedByUserId AS NVARCHAR(20)) + N',', N',' + @FollowerIds + N',') > 0
      OR (ISNULL(R.CreatedByName, N'') <> N'' AND CHARINDEX(NCHAR(31) + R.CreatedByName + NCHAR(31), NCHAR(31) + @FollowerNames + NCHAR(31)) > 0)
    )
  )
  OR (
    {CategorySql} = N'employee'
    AND (
      @AllEmployees = 1
      OR CHARINDEX(N',' + CAST(R.CreatedByUserId AS NVARCHAR(20)) + N',', N',' + @EmployeeIds + N',') > 0
    )
  )
  OR ({CategorySql} = N'manager' AND @Managers = 1)
  OR ({CategorySql} = N'crm' AND @Crm = 1)
  OR (
    {CategorySql} = N'promoter'
    AND (
      @Promoters = 1
      OR (ISNULL(R.CreatedByName, N'') <> N'' AND CHARINDEX(NCHAR(31) + R.CreatedByName + NCHAR(31), NCHAR(31) + @PromoterNames + NCHAR(31)) > 0)
    )
  )
)";

        public DateTime? FromIraqDate { get; init; }
        public DateTime? ToIraqDate { get; init; }
        public bool HasSource { get; init; }

        /// <summary>False until the director actually picks a date or a source. The board query stays unchanged.</summary>
        public bool IsActive => FromIraqDate.HasValue || ToIraqDate.HasValue || HasSource;
        public bool AllDelegates { get; init; }
        public bool AllFollowers { get; init; }
        public bool AllEmployees { get; init; }
        public bool Managers { get; init; }
        public bool Crm { get; init; }
        public bool Promoters { get; init; }
        public IReadOnlyList<SalesRequestSourcePerson> People { get; init; } = [];

        public static string SqlFor(SalesRequestBoardFilter? filter)
        {
            if (filter is not { IsActive: true })
            {
                return "";
            }

            var sql = "";
            if (filter.FromIraqDate.HasValue || filter.ToIraqDate.HasValue)
            {
                sql += DateSqlAnd;
            }

            if (filter.HasSource)
            {
                sql += SourceSqlAnd;
            }

            return sql;
        }

        public static SalesRequestBoardFilter Parse(
            string? from,
            string? to,
            string? sources,
            string? people,
            string? sourceFilter)
        {
            var fromDate = ParseDay(from);
            var toDate = ParseDay(to);
            if (fromDate.HasValue && !toDate.HasValue)
            {
                toDate = fromDate;
            }

            if (toDate.HasValue && !fromDate.HasValue)
            {
                fromDate = toDate;
            }

            if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
            {
                (fromDate, toDate) = (toDate, fromDate);
            }

            var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in SplitCsv(sources))
            {
                var key = NormalizeRole(part);
                if (key != null)
                {
                    groups.Add(key);
                }
            }

            var parsedPeople = new List<SalesRequestSourcePerson>();
            foreach (var token in (people ?? "").Split(TokenSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (TryParsePerson(token, out var person))
                {
                    parsedPeople.Add(person);
                }
            }

            var flagged = string.Equals(sourceFilter, "1", StringComparison.OrdinalIgnoreCase)
                          || string.Equals(sourceFilter, "true", StringComparison.OrdinalIgnoreCase);
            var hasSource = flagged || groups.Count > 0 || parsedPeople.Count > 0;

            return new SalesRequestBoardFilter
            {
                FromIraqDate = fromDate,
                ToIraqDate = toDate,
                HasSource = hasSource,
                AllDelegates = groups.Contains("delegate"),
                AllFollowers = groups.Contains("follower"),
                AllEmployees = groups.Contains("employee"),
                Managers = groups.Contains("manager"),
                Crm = groups.Contains("crm"),
                Promoters = groups.Contains("promoter"),
                People = parsedPeople
            };
        }

        public static object Parameters(SalesRequestBoardFilter? filter)
        {
            var f = filter ?? new SalesRequestBoardFilter();
            return new
            {
                FromIraqDate = f.FromIraqDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ToIraqDate = f.ToIraqDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                AllDelegates = f.AllDelegates ? 1 : 0,
                AllFollowers = f.AllFollowers ? 1 : 0,
                AllEmployees = f.AllEmployees ? 1 : 0,
                Managers = f.Managers ? 1 : 0,
                Crm = f.Crm ? 1 : 0,
                Promoters = f.Promoters ? 1 : 0,
                DelegateIds = JoinIds(f, "delegate"),
                DelegateNames = JoinNames(f, "delegate"),
                FollowerIds = JoinIds(f, "follower"),
                FollowerNames = JoinNames(f, "follower"),
                EmployeeIds = JoinIds(f, "employee"),
                EmployeeNames = JoinNames(f, "employee"),
                PromoterNames = JoinNames(f, "promoter")
            };
        }

        public bool Matches(SalesRequestDTO row)
        {
            if (row == null)
            {
                return false;
            }

            if (!DateHits(row.CreatedAtUtc))
            {
                return false;
            }

            if (!HasSource)
            {
                return true;
            }

            var category = Category(row);
            return category switch
            {
                "delegate" => AllDelegates || PersonHit(row, "delegate"),
                "follower" => AllFollowers || PersonHit(row, "follower"),
                "employee" => AllEmployees || PersonHit(row, "employee"),
                "manager" => Managers,
                "crm" => Crm,
                "promoter" => Promoters || PersonHit(row, "promoter"),
                _ => false
            };
        }

        private bool DateHits(DateTime createdAtUtc)
        {
            var raw = createdAtUtc.Date;
            var iraq = createdAtUtc.AddHours(3).Date;
            if (FromIraqDate is DateTime from && raw < from && iraq < from)
            {
                return false;
            }

            if (ToIraqDate is DateTime to && raw > to && iraq > to)
            {
                return false;
            }

            return true;
        }

        public static string Category(SalesRequestDTO row)
        {
            var source = row.CustomerSourceType ?? "";
            var role = row.CreatedByUserType ?? "";
            if (SalesRequestSources.IsCustomerApp(source))
            {
                return "promoter";
            }

            if (SalesRequestSources.IsCrm(source)
                || string.Equals(role, SalesRequestSources.Crm, StringComparison.OrdinalIgnoreCase)
                || string.Equals(row.CreatedByName?.Trim(), "قسم التسويق", StringComparison.Ordinal)
                || string.Equals(role, "قسم التسويق", StringComparison.Ordinal))
            {
                return "crm";
            }

            if (SalesRequestSources.IsFollower(source) || SalesRoles.IsFollower(role))
            {
                return "follower";
            }

            if (SalesRequestSources.IsDelegate(source) || SalesRoles.IsDelegate(role))
            {
                return "delegate";
            }

            if (SalesRequestSources.IsEmployeeSubmitted(source))
            {
                return "employee";
            }

            return "manager";
        }

        /// <summary>
        /// Accepts <c>role|id|name</c> (branch) or <c>role|city|id|name</c> (gateway).
        /// City may be numeric (GetAdmin ids), so a 4-part token is always the city form.
        /// </summary>
        public static bool TryParsePerson(string token, out SalesRequestSourcePerson person)
        {
            person = new SalesRequestSourcePerson("", 0, "");
            var parts = (token ?? "").Split(FieldSeparator);
            if (parts.Length < 2)
            {
                return false;
            }

            var role = NormalizeRole(parts[0]);
            if (role is not ("delegate" or "follower" or "employee" or "promoter"))
            {
                return false;
            }

            string idText;
            string name;
            if (parts.Length >= 4)
            {
                idText = parts[2];
                name = string.Join(FieldSeparator, parts.Skip(3));
            }
            else
            {
                idText = parts[1];
                name = parts.Length >= 3 ? string.Join(FieldSeparator, parts.Skip(2)) : "";
            }

            _ = int.TryParse(idText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id);
            name = (name ?? "").Replace(NameSeparator, ' ').Trim();
            if (id <= 0 && string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            person = new SalesRequestSourcePerson(role, id, name);
            return true;
        }

        private bool PersonHit(SalesRequestDTO row, string role)
        {
            foreach (var person in People)
            {
                if (!string.Equals(person.Role, role, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (role == "employee")
                {
                    if (person.Id > 0 && row.CreatedByUserId == person.Id)
                    {
                        return true;
                    }

                    continue;
                }

                if (person.Id > 0 && row.CreatedByUserId == person.Id)
                {
                    return true;
                }

                if (role == "delegate" && person.Id > 0 && row.SourceListId == person.Id)
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(person.Name)
                    && string.Equals(row.CreatedByName?.Trim(), person.Name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static DateTime? ParseDay(string? raw)
        {
            var text = (raw ?? "").Trim();
            if (text.Length == 0)
            {
                return null;
            }

            if (DateTime.TryParseExact(
                    text,
                    ["yyyy-MM-dd", "yyyy-M-d"],
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var exact))
            {
                return exact.Date;
            }

            return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var loose)
                ? loose.Date
                : null;
        }

        private static string? NormalizeRole(string? raw)
        {
            var key = (raw ?? "").Trim().ToLowerInvariant();
            return key switch
            {
                "delegate" or "مندوب" => "delegate",
                "follower" or "متابع" => "follower",
                "employee" or "موظف" => "employee",
                "manager" or "مدير" => "manager",
                "crm" or "تسويق" => "crm",
                "promoter" or "مروج" => "promoter",
                _ => null
            };
        }

        private static IEnumerable<string> SplitCsv(string? raw) =>
            (raw ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        private static string JoinIds(SalesRequestBoardFilter filter, string role)
        {
            var ids = filter.People
                .Where(p => string.Equals(p.Role, role, StringComparison.OrdinalIgnoreCase) && p.Id > 0)
                .Select(p => p.Id)
                .Distinct()
                .ToArray();
            return ids.Length == 0 ? "" : string.Join(",", ids);
        }

        private static string JoinNames(SalesRequestBoardFilter filter, string role)
        {
            var names = filter.People
                .Where(p => string.Equals(p.Role, role, StringComparison.OrdinalIgnoreCase))
                .Select(p => (p.Name ?? "").Trim())
                .Where(n => n.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return names.Length == 0 ? "" : string.Join(NameSeparator, names);
        }
    }

    public readonly record struct SalesRequestSourcePerson(string Role, int Id, string Name);
}
