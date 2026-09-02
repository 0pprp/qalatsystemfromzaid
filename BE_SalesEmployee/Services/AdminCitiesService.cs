using System.Text.Json;

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

    public class AdminCitiesService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AdminCitiesService> _logger;
        private List<AdminCity>? _cache;
        private DateTime _cacheAt;

        public AdminCitiesService(HttpClient http, IConfiguration configuration, ILogger<AdminCitiesService> logger)
        {
            _http = http;
            _configuration = configuration;
            _logger = logger;
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

            if (_cache != null && DateTime.UtcNow - _cacheAt < TimeSpan.FromMinutes(10))
            {
                return _cache;
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

            _cache = cities;
            _cacheAt = DateTime.UtcNow;
            _logger.LogInformation("Loaded {Count} cities from GetAdmin", cities.Count);
            return cities;
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
