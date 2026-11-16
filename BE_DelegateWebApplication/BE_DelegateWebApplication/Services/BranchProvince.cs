namespace BE_DelegateWebApplication.Services
{
    /// <summary>
    /// Last-resort province for a branch API: every branch API talks to exactly one branch
    /// database, so the database name identifies the governorate even when a delegate row
    /// has no usable CityID. City ids mirror dbo.GetCityID; Arabic names are the fallback
    /// label when the branch Cities table has no row for that id.
    /// </summary>
    public static class BranchProvince
    {
        private static readonly Dictionary<string, (int CityId, string Name)> ByDatabase =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["DatabaseCompanyNajaf"] = (1, "النجف"),
                ["DatabaseCompanyBaghdadRosafa"] = (2, "بغداد - الرصافة"),
                ["DatabaseCompanyBaghdadKarak"] = (3, "بغداد - الكرخ"),
                ["DatabaseCompanyKarbala"] = (4, "كربلاء"),
                ["DatabaseCompanyBabil"] = (5, "بابل"),
                ["DatabaseCompanyDewania"] = (6, "الديوانية"),
                ["DatabaseCompanyKot"] = (7, "واسط"),
                ["DatabaseCompanyNasria"] = (8, "ذي قار"),
                ["DatabaseCompanyBasra"] = (9, "البصرة"),
                ["DatabaseCompanyMothana"] = (10, "المثنى"),
                ["DatabaseCompanyDeiala"] = (11, "ديالى"),
                ["DatabaseCompanyBasraAlanwar"] = (12, "البصرة - الأنوار"),
                ["DatabaseCompanyMusol"] = (13, "الموصل"),
                ["DatabaseCompanyKarkok"] = (14, "كركوك"),
                ["DatabaseCompanyRusafaAqeel"] = (15, "الرصافة - عقيل"),
                ["DatabaseCompanyMaysan"] = (16, "ميسان"),
                ["DatabaseCompanyKarakAqeel"] = (18, "الكرخ - عقيل"),
            };

        public static (int CityId, string Name)? FromDatabaseName(string? databaseName)
        {
            if (string.IsNullOrWhiteSpace(databaseName)) return null;
            return ByDatabase.TryGetValue(databaseName.Trim(), out var hit) ? hit : null;
        }

        /// <summary>First non-empty candidate, trimmed; null when all are empty.</summary>
        public static string? FirstNonEmpty(params string?[] candidates)
        {
            foreach (var c in candidates)
            {
                if (!string.IsNullOrWhiteSpace(c)) return c.Trim();
            }
            return null;
        }
    }
}
