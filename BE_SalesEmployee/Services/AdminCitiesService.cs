using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace BE_SalesEmployee.Services
{
    public class AdminCity
    {
        public string Value { get; set; } = "";
        public string Name { get; set; } = "";
        public string Database { get; set; } = "";
        public string Link { get; set; } = "";
        public List<string> AcceptUserTypes { get; set; } = ["موظف مبيعات"];
        public string? SearchUserName { get; set; }
        public string? SearchPassword { get; set; }

        public bool Accepts(string? userType)
        {
            if (string.IsNullOrWhiteSpace(userType))
            {
                return false;
            }
            var allowed = AcceptUserTypes is { Count: > 0 } ? AcceptUserTypes : ["موظف مبيعات"];
            return allowed.Any(t => string.Equals(t, userType, StringComparison.Ordinal));
        }
    }

    public class AdminCityConfig
    {
        public string? Name { get; set; }
        public string? Value { get; set; }
        public string? Link { get; set; }
        public string? Database { get; set; }
        public List<string>? AcceptUserTypes { get; set; }
        public string? SearchUserName { get; set; }
        public string? SearchPassword { get; set; }
    }

    /// <summary>
    /// Branch directory loader. Cache lives in <see cref="IMemoryCache"/> (singleton lifetime),
    /// not on the typed-HttpClient instance (which is transient per request).
    /// </summary>
    public class AdminCitiesService
    {
        public const string CacheKey = "admin-cities:v1";
        public static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

        private readonly HttpClient _http;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AdminCitiesService> _logger;
        private readonly IMemoryCache _cache;
        private readonly SemaphoreSlim _reloadGate = new(1, 1);

        public AdminCitiesService(
            HttpClient http,
            IConfiguration configuration,
            ILogger<AdminCitiesService> logger,
            IMemoryCache cache)
        {
            _http = http;
            _configuration = configuration;
            _logger = logger;
            _cache = cache;
        }

        public async Task<List<AdminCity>> GetCitiesAsync(CancellationToken ct = default)
        {
            var lab = _configuration.GetSection("LabCities").Get<List<AdminCityConfig>>();
            if (lab != null && lab.Count > 0)
            {
                return lab.Select(c => new AdminCity
                {
                    Value = c.Value ?? "",
                    Name = c.Name ?? "",
                    Database = c.Database ?? "",
                    Link = NormalizeLink(c.Link),
                    AcceptUserTypes = c.AcceptUserTypes ?? ["موظف مبيعات"],
                    SearchUserName = c.SearchUserName,
                    SearchPassword = c.SearchPassword
                }).Where(c => !string.IsNullOrWhiteSpace(c.Link)).ToList();
            }

            var overrideSection = _configuration.GetSection("CityOverride");
            if (overrideSection.GetValue<bool>("Enabled"))
            {
                return
                [
                    new AdminCity
                    {
                        Value = overrideSection["Value"] ?? "local",
                        Name = overrideSection["Name"] ?? "محلي",
                        Link = NormalizeLink(overrideSection["Link"] ?? "http://127.0.0.1:5180/api/"),
                        AcceptUserTypes = ["موظف مبيعات"]
                    }
                ];
            }

            if (_cache.TryGetValue(CacheKey, out List<AdminCity>? cached) && cached != null)
            {
                return cached;
            }

            await _reloadGate.WaitAsync(ct);
            try
            {
                // Double-check after acquiring gate (stampede prevention).
                if (_cache.TryGetValue(CacheKey, out cached) && cached != null)
                {
                    return cached;
                }

                var url = _configuration["GetAdminUrl"] ?? "http://defaultdata.alsaaeidy.com/GetAdmin";
                using var response = await _http.GetAsync(url, ct);
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                var data = await JsonSerializer.DeserializeAsync<List<JsonElement>>(stream, cancellationToken: ct)
                           ?? [];

                var cities = data.Select(item => new AdminCity
                {
                    Value = ReadString(item, "value"),
                    Name = ReadString(item, "name"),
                    Database = ReadString(item, "database"),
                    Link = NormalizeLink(ReadString(item, "link"))
                }).Where(c => !string.IsNullOrWhiteSpace(c.Link)).ToList();

                _cache.Set(CacheKey, cities, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = CacheTtl
                });
                _logger.LogInformation("Loaded {Count} cities from GetAdmin (cache TTL {TtlMinutes}m)",
                    cities.Count, CacheTtl.TotalMinutes);
                return cities;
            }
            finally
            {
                _reloadGate.Release();
            }
        }

        /// <summary>Explicit invalidation for ops / after directory changes.</summary>
        public void InvalidateCache() => _cache.Remove(CacheKey);

        public static bool IsExcludedSalesBranch(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return true;
            }

            return name.Contains("قانونية الشركة", StringComparison.Ordinal)
                   || name.Contains("تجريبي", StringComparison.Ordinal)
                   || name.Contains("الشهري", StringComparison.Ordinal);
        }

        public async Task<List<AdminCity>> GetSalesBranchesAsync(CancellationToken ct = default)
        {
            var cities = await GetCitiesAsync(ct);
            var requireDemo = _configuration.GetValue("SalesManagement:RequireDemoDatabase", true);
            if (requireDemo)
            {
                var allowed = _configuration["SalesManagement:AllowedDemoDatabase"] ?? "DatabaseCompanyNajaf_DEMO";
                return cities.Where(c =>
                    string.Equals(c.Database, allowed, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(c.Link)).ToList();
            }

            return cities.Where(c =>
                !string.IsNullOrWhiteSpace(c.Link)
                && !IsExcludedSalesBranch(c.Name)).ToList();
        }

        public async Task<AdminCity?> FindByValueAsync(string? value)
        {
            var cities = await GetCitiesAsync();
            return cities.FirstOrDefault(c =>
                string.Equals(c.Value, value, StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.Name, value, StringComparison.OrdinalIgnoreCase));
        }

        public static string NormalizeLink(string? link)
        {
            if (string.IsNullOrWhiteSpace(link))
            {
                return "";
            }
            var trimmed = link.Trim();
            if (!trimmed.EndsWith('/'))
            {
                trimmed += "/";
            }
            if (!trimmed.Contains("/api/", StringComparison.OrdinalIgnoreCase))
            {
                trimmed += "api/";
            }
            return trimmed;
        }

        private static string ReadString(JsonElement item, string name)
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                return "";
            }
            foreach (var prop in item.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return prop.Value.ValueKind == JsonValueKind.String
                        ? prop.Value.GetString() ?? ""
                        : prop.Value.ToString();
                }
            }
            return "";
        }
    }
}
