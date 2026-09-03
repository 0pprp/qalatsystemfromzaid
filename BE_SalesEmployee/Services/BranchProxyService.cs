using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace BE_SalesEmployee.Services
{
    public class BranchProxyService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _configuration;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public BranchProxyService(HttpClient http, IConfiguration configuration)
        {
            _http = http;
            _configuration = configuration;
        }

        public async Task<(bool Ok, string? Token, string UserId, string UserName, string UserType, string Body)> TryEmployeeLoginAsync(
            string cityLink,
            string userName,
            string password,
            CancellationToken ct)
        {
            var url = $"{AdminCitiesService.NormalizeLink(cityLink)}Users/Users_LoginEmployee";
            var payload = JsonSerializer.Serialize(new { userName, password });
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            using var response = await _http.SendAsync(request, ct);
            var parsed = await ParseLoginAsync(response, userName, ct);
            if (parsed.Ok)
            {
                return parsed;
            }

            var adminUrl = $"{AdminCitiesService.NormalizeLink(cityLink)}Users/Users_LoginAdmin";
            using var adminRequest = new HttpRequestMessage(HttpMethod.Post, adminUrl)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            using var adminResponse = await _http.SendAsync(adminRequest, ct);
            return await ParseLoginAsync(adminResponse, userName, ct);
        }

        private static async Task<(bool Ok, string? Token, string UserId, string UserName, string UserType, string Body)> ParseLoginAsync(
            HttpResponseMessage response,
            string userName,
            CancellationToken ct)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                return (false, null, "", "", "", body);
            }

            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var token = ReadIgnoreCase(doc.RootElement, "token")
                        ?? ReadIgnoreCase(doc.RootElement, "Token");
            if (string.IsNullOrWhiteSpace(token))
            {
                return (false, null, "", "", "", body);
            }

            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            var userType = jwt.Claims.FirstOrDefault(c => c.Type == "UserType")?.Value ?? "";
            var userId = jwt.Claims.FirstOrDefault(c => c.Type == "UserID")?.Value ?? "";
            var jwtUserName = jwt.Claims.FirstOrDefault(c => c.Type == "UserName")?.Value ?? userName;
            return (true, token, userId, jwtUserName, userType, body);
        }

        public async Task<HttpResponseMessage> SendAuthorizedAsync(
            string cityLink,
            string relativePath,
            HttpMethod method,
            string? branchToken,
            HttpContent? content,
            CancellationToken ct)
        {
            var url = $"{AdminCitiesService.NormalizeLink(cityLink)}{relativePath.TrimStart('/')}";
            var request = new HttpRequestMessage(method, url);
            if (!string.IsNullOrWhiteSpace(branchToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", branchToken);
            }
            if (content != null)
            {
                request.Content = content;
            }
            return await _http.SendAsync(request, ct);
        }

        public async Task<HttpResponseMessage> SendManagerAsync(
            string cityLink,
            string relativePath,
            HttpMethod method,
            string? jsonBody,
            string? managerName,
            CancellationToken ct)
        {
            var url = $"{AdminCitiesService.NormalizeLink(cityLink)}{relativePath.TrimStart('/')}";
            var request = new HttpRequestMessage(method, url);
            var key = _configuration["InternalApiKey"] ?? "";
            if (!string.IsNullOrWhiteSpace(key))
            {
                request.Headers.TryAddWithoutValidation("X-Sales-Gateway-Key", key);
            }
            if (!string.IsNullOrWhiteSpace(managerName))
            {
                request.Headers.TryAddWithoutValidation("X-Sales-Manager-Name", managerName);
            }
            if (jsonBody != null)
            {
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            }
            return await _http.SendAsync(request, ct);
        }

        public async Task<HttpResponseMessage> SendGatewayKeyAsync(
            string cityLink,
            string relativePath,
            CancellationToken ct)
        {
            var url = $"{AdminCitiesService.NormalizeLink(cityLink)}{relativePath.TrimStart('/')}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var key = _configuration["InternalApiKey"] ?? "";
            if (!string.IsNullOrWhiteSpace(key))
            {
                request.Headers.TryAddWithoutValidation("X-Sales-Gateway-Key", key);
            }
            return await _http.SendAsync(request, ct);
        }

        private readonly Dictionary<string, (string Token, DateTime Expires)> _searchTokens = new(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _tokenLock = new(1, 1);

        public async Task<List<JsonElement>> SearchCustomersAsync(
            AdminCity city,
            string encodedQuery,
            string? callerBranchToken,
            string? callerCityLink,
            CancellationToken ct)
        {
            var segment = string.Equals(encodedQuery, "null", StringComparison.OrdinalIgnoreCase)
                ? "null"
                : Uri.EscapeDataString(encodedQuery);
            var keyedError = "";
            using (var keyed = await SendGatewayKeyAsync(city.Link, $"SalesEmployee/SearchCustomers/{segment}", ct))
            {
                var parsed = await ReadArrayAsync(keyed, ct);
                if (parsed != null)
                {
                    return parsed;
                }
                if ((int)keyed.StatusCode >= 500)
                {
                    keyedError = await keyed.Content.ReadAsStringAsync(ct);
                }
            }

            var token = callerBranchToken;
            var sameCity = !string.IsNullOrWhiteSpace(callerCityLink)
                           && string.Equals(
                               AdminCitiesService.NormalizeLink(callerCityLink),
                               AdminCitiesService.NormalizeLink(city.Link),
                               StringComparison.OrdinalIgnoreCase);
            if (!sameCity)
            {
                token = await GetSearchTokenAsync(city, ct);
                if (string.IsNullOrWhiteSpace(token))
                {
                    throw new InvalidOperationException($"تعذر ربط البحث بـ {city.Name}");
                }
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                if (!string.IsNullOrWhiteSpace(keyedError))
                {
                    throw new InvalidOperationException(keyedError);
                }
                return [];
            }

            using var legacy = await SendAuthorizedAsync(
                city.Link,
                $"Customers/Customers_GetAll/0&&{segment}&&{Uri.EscapeDataString("الجميع")}",
                HttpMethod.Get,
                token,
                null,
                ct);
            var legacyParsed = await ReadArrayAsync(legacy, ct);
            if (legacyParsed != null)
            {
                return legacyParsed;
            }
            if (!string.IsNullOrWhiteSpace(keyedError))
            {
                throw new InvalidOperationException(keyedError);
            }
            return [];
        }

        private async Task<string?> GetSearchTokenAsync(AdminCity city, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(city.SearchUserName) || string.IsNullOrWhiteSpace(city.SearchPassword))
            {
                return null;
            }

            await _tokenLock.WaitAsync(ct);
            try
            {
                if (_searchTokens.TryGetValue(city.Link, out var cached) && cached.Expires > DateTime.UtcNow.AddMinutes(2))
                {
                    return cached.Token;
                }

                var login = await TryEmployeeLoginAsync(city.Link, city.SearchUserName, city.SearchPassword, ct);
                if (!login.Ok || string.IsNullOrWhiteSpace(login.Token))
                {
                    return null;
                }
                _searchTokens[city.Link] = (login.Token, DateTime.UtcNow.AddHours(20));
                return login.Token;
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        private static async Task<List<JsonElement>?> ReadArrayAsync(HttpResponseMessage response, CancellationToken ct)
        {
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            var body = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(body))
            {
                return [];
            }
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }
                return doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public static JsonElement? TryParseJson(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }
            try
            {
                return JsonDocument.Parse(body).RootElement.Clone();
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public static StringContent JsonContent(object value)
        {
            return new StringContent(JsonSerializer.Serialize(value, JsonOptions), Encoding.UTF8, "application/json");
        }

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
