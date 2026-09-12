using System.Text.Json;
using BE_SalesEmployee.Sales.Authorization;

namespace BE_SalesEmployee.Services
{
    public sealed class SalesFilterLoginResult
    {
        public bool Ok { get; init; }
        public string? Message { get; init; }
        public string? Token { get; init; }
        public DateTime Expiration { get; init; }
        public string UserId { get; init; } = "";
        public string UserName { get; init; } = "";
        public string UserType { get; init; } = "";
        public string HomeCityValue { get; init; } = "";
        public IReadOnlyList<string> AllowedFilterCities { get; init; } = Array.Empty<string>();
        public IReadOnlyList<object> Cities { get; init; } = Array.Empty<object>();
    }

    public sealed class SalesFilterLoginService
    {
        private readonly AdminCitiesService _cities;
        private readonly BranchProxyService _proxy;
        private readonly TokenService _tokens;

        public SalesFilterLoginService(
            AdminCitiesService cities,
            BranchProxyService proxy,
            TokenService tokens)
        {
            _cities = cities;
            _proxy = proxy;
            _tokens = tokens;
        }

        public async Task<SalesFilterLoginResult> LoginAsync(string userName, string password, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
            {
                return Fail("اسم المستخدم وكلمة المرور مطلوبان");
            }

            var cities = await _cities.GetCitiesAsync(ct);
            foreach (var city in cities)
            {
                try
                {
                    var login = await _proxy.TryEmployeeLoginAsync(city.Link, userName.Trim(), password, ct);
                    if (!login.Ok || !SalesRoles.IsSalesFilterEmployee(login.UserType))
                    {
                        continue;
                    }

                    var allowedFromBranch = await LoadAllowedCitiesAsync(city.Link, login.Token!, ct);
                    if (allowedFromBranch.Count == 0)
                    {
                        return Fail("لا توجد محافظات مسموحة لهذا الحساب");
                    }

                    var catalog = cities
                        .Where(c => !string.IsNullOrWhiteSpace(c.Value))
                        .ToDictionary(c => c.Value, c => c, StringComparer.OrdinalIgnoreCase);
                    var allowed = new List<string>();
                    var cityDtos = new List<object>();
                    foreach (var entry in allowedFromBranch)
                    {
                        if (!catalog.TryGetValue(entry.CityValue, out var adminCity))
                        {
                            continue;
                        }

                        allowed.Add(adminCity.Value);
                        cityDtos.Add(new
                        {
                            cityValue = adminCity.Value,
                            cityName = string.IsNullOrWhiteSpace(entry.CityName) ? adminCity.Name : entry.CityName,
                            value = adminCity.Value,
                            name = string.IsNullOrWhiteSpace(entry.CityName) ? adminCity.Name : entry.CityName,
                        });
                    }

                    if (allowed.Count == 0)
                    {
                        return Fail("المحافظات المسموحة للحساب غير معرفة في دليل الفروع");
                    }

                    var token = _tokens.CreateSalesFilterToken(
                        login.UserId,
                        login.UserName,
                        login.UserType,
                        city.Value,
                        allowed,
                        out var expiration);

                    return new SalesFilterLoginResult
                    {
                        Ok = true,
                        Token = token,
                        Expiration = expiration,
                        UserId = login.UserId,
                        UserName = login.UserName,
                        UserType = login.UserType,
                        HomeCityValue = city.Value,
                        AllowedFilterCities = allowed,
                        Cities = cityDtos,
                    };
                }
                catch
                {
                    // try next city
                }
            }

            return Fail("اسم المستخدم أو كلمة المرور غير صحيحة، أو الحساب ليس موظف فلترة المبيعات");
        }

        private async Task<List<(string CityValue, string? CityName)>> LoadAllowedCitiesAsync(
            string cityLink,
            string branchToken,
            CancellationToken ct)
        {
            using var response = await _proxy.SendAuthorizedAsync(
                cityLink,
                "sales-filter/me/cities",
                HttpMethod.Get,
                branchToken,
                null,
                ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(body))
            {
                return [];
            }

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var list = new List<(string, string?)>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var value = ReadIgnoreCase(el, "cityValue") ?? ReadIgnoreCase(el, "CityValue") ?? ReadIgnoreCase(el, "value");
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                var name = ReadIgnoreCase(el, "cityName") ?? ReadIgnoreCase(el, "CityName") ?? ReadIgnoreCase(el, "name");
                list.Add((value.Trim(), name));
            }

            return list;
        }

        private static SalesFilterLoginResult Fail(string message) => new() { Ok = false, Message = message };

        private static string? ReadIgnoreCase(JsonElement element, string name)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return prop.Value.ValueKind == JsonValueKind.String
                        ? prop.Value.GetString()
                        : prop.Value.ToString();
                }
            }

            return null;
        }
    }
}
