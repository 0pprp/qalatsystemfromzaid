namespace BE_Company.Sales.Services
{
    public static class SalesCityDisplay
    {
        public static bool IsInternalKey(string? value, string? cityValue = null)
        {
            var text = value?.Trim() ?? string.Empty;
            if (text.Length == 0)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(cityValue)
                && string.Equals(text, cityValue.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (text.StartsWith("Database", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return text.EndsWith("_DEMO", StringComparison.OrdinalIgnoreCase)
                   && text.IndexOfAny([' ', '\u00A0']) < 0
                   && text.All(c => char.IsLetterOrDigit(c) || c == '_');
        }

        public static string HumanName(string? candidate, string? fallbackName, string? internalKey = null)
        {
            foreach (var item in new[] { candidate, fallbackName })
            {
                if (!IsInternalKey(item, internalKey))
                {
                    return item!.Trim();
                }
            }

            var mapped = MapLegacyDatabaseKey(internalKey) ?? MapLegacyDatabaseKey(candidate) ?? MapLegacyDatabaseKey(fallbackName);
            if (!string.IsNullOrWhiteSpace(mapped))
            {
                return mapped;
            }

            return string.Empty;
        }

        /// <summary>Legacy InitialCatalog / DatabaseCompany* → Arabic province label.</summary>
        public static string? MapLegacyDatabaseKey(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var key = value.Trim();
            if (key.EndsWith("_DEMO", StringComparison.OrdinalIgnoreCase))
            {
                key = key[..^5];
            }

            return key.ToLowerInvariant() switch
            {
                "databasecompanynajaf" or "najaf-demo" or "najaf" => "النجف",
                "databasecompanybasra" or "basra-demo" or "basra" => "البصرة",
                "databasecompanybaghdadkarak" or "karkh-demo" or "karkh" => "الكرخ",
                "databasecompanybaghdadrosafa" or "rusafa-demo" or "rosafa" or "rusafa" => "الرصافة",
                "databasecompanykarbala" or "karbala-demo" or "karbala" => "كربلاء",
                "databasecompanybabil" or "babil-demo" or "babil" => "بابل",
                "databasecompanydiwaniya" or "diwaniya-demo" or "diwaniya" => "الديوانية",
                "databasecompanymaysan" or "maysan-demo" or "maysan" => "ميسان",
                "databasecompanywasit" or "wasit-demo" or "wasit" => "واسط",
                "databasecompanydhiqar" or "dhiqar-demo" or "dhiqar" => "ذي قار",
                "databasecompanymuthanna" or "muthanna-demo" or "muthanna" => "المثنى",
                "databasecompanyanbar" or "anbar-demo" or "anbar" => "الأنبار",
                "databasecompanynineveh" or "nineveh-demo" or "nineveh" => "نينوى",
                "databasecompanykirkuk" or "kirkuk-demo" or "kirkuk" => "كركوك",
                "databasecompanysalahaddin" or "salahaddin-demo" or "salahaddin" => "صلاح الدين",
                "databasecompanydiyala" or "diyala-demo" or "diyala" => "ديالى",
                "databasecompanyerbil" or "erbil-demo" or "erbil" => "أربيل",
                "databasecompanysulaymaniyah" or "sulaymaniyah-demo" or "sulaymaniyah" => "السليمانية",
                "databasecompanyduhok" or "duhok-demo" or "duhok" => "دهوك",
                _ => null
            };
        }

        public static string FriendlyOrUnavailable(string? cityName, string? cityValue)
        {
            var name = HumanName(cityName, null, cityValue);
            return string.IsNullOrWhiteSpace(name) ? "غير متوفر" : name;
        }
    }
}
