using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace BE_CustomerApp.Tests;

/// <summary>
/// Spins up the real API against a throw-away SQL Server database (local SQLEXPRESS by default; override with
/// env CUSTOMERAPP_TEST_SQL) and the fake gateway. Skipped automatically when no SQL Server is reachable.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public const string GatewayKey = "test-gateway-jwt-key-0123456789-abcdefghijkl";
    public static readonly string[] Phones = ["07700000011", "07700000013", "07700000015", "07700000017", "07700000019", "07700000021", "07700000027", "07700000029"];

    private readonly string _server = Environment.GetEnvironmentVariable("CUSTOMERAPP_TEST_SQL") ?? @"Server=.\SQLEXPRESS;Trusted_Connection=True;TrustServerCertificate=True";
    private readonly string _database = "CustomerAppDb_Test_" + Guid.NewGuid().ToString("N")[..8];
    private WebApplicationFactory<Program>? _factory;

    public bool Available { get; private set; }
    public HttpClient Client { get; private set; } = null!;
    public IServiceProvider Services => _factory!.Services;
    public string ConnectionString => $"{_server};Database={_database}";

    /// <summary>The published APK's release.json (absent until a test writes it).</summary>
    public string ReleaseFile { get; } = Path.Combine(Path.GetTempPath(), "release-" + Guid.NewGuid().ToString("N")[..8] + ".json");

    public async Task InitializeAsync()
    {
        try
        {
            await using var c = new SqlConnection(_server + ";Connect Timeout=3");
            await c.OpenAsync();
            Available = true;
        }
        catch (SqlException)
        {
            return;
        }

        var fixedCodes = Phones.Select((p, i) => new KeyValuePair<string, string?>($"Otp:FixedCodes:{p}", "111111"));
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            foreach (var (k, v) in new Dictionary<string, string?>
            {
                ["ConnectionStrings:CustomerApp"] = $"{_server};Database={_database}",
                ["GatewayJwt:Key"] = GatewayKey,
                ["Gateway:UseFake"] = "true",
                ["Workers:Enabled"] = "false",
                ["Demo:Seed"] = "false",
                ["RateLimits:AuthPerMinute"] = "1000",
                ["RateLimits:OrdersPer10Minutes"] = "1000",
                ["Media:Root"] = Path.Combine(Path.GetTempPath(), _database),
                ["Updates:ReleaseFile"] = ReleaseFile,
            }.Concat(fixedCodes))
            {
                b.UseSetting(k, v);
            }
        });
        Client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        if (_factory is null) return;
        await _factory.DisposeAsync();
        SqlConnection.ClearAllPools();
        await using var c = new SqlConnection(_server);
        await c.OpenAsync();
        await using var cmd = new SqlCommand($"ALTER DATABASE [{_database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_database}];", c);
        await cmd.ExecuteNonQueryAsync();
    }

    public static string AdminToken(string userType = "المدير")
    {
        var jwt = new JwtSecurityToken(
            claims: [new Claim("UserID", "0"), new Claim("UserName", "مدير الاختبار"), new Claim("UserType", userType), new Claim("Central", "true")],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GatewayKey)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}

public sealed class EndToEndTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _fx;

    public EndToEndTests(ApiFixture fx) => _fx = fx;

    private HttpClient Http => _fx.Client;

    private async Task<JsonNode> Send(HttpMethod method, string url, string? token = null, object? body = null, HttpStatusCode expect = HttpStatusCode.OK, string? idempotencyKey = null, string? appVersion = null)
    {
        using var req = new HttpRequestMessage(method, url);
        if (token is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) req.Content = JsonContent.Create(body);
        if (idempotencyKey is not null) req.Headers.Add("Idempotency-Key", idempotencyKey);
        if (appVersion is not null) req.Headers.Add("X-App-Version", appVersion);
        using var res = await Http.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expect, $"{method} {url} → {(int)res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : JsonNode.Parse(text)!;
    }

    /// <summary>Creates an agent from the dashboard and signs in as them. Returns (userId, accessToken).</summary>
    private async Task<(long Id, string Token)> CreateAgentAndLogin(string username, string phone, string first)
    {
        var agent = await Send(HttpMethod.Post, "/api/v1/admin/agents", ApiFixture.AdminToken(), new
        {
            username, password = "pass1234", firstName = first, fatherName = "حسن", grandfatherName = "كاظم", phone,
        });
        var id = agent["agent"]!["id"]!.GetValue<long>();
        var auth = await Send(HttpMethod.Post, "/api/v1/auth/login", body: new { username, password = "pass1234" });
        return (id, auth["accessToken"]!.GetValue<string>());
    }

    /// <summary>A tiny valid JPEG header is enough for the content sniffing.</summary>
    private async Task<string> UploadShopPhoto(string token)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders/shop-photo");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1, 0xFF, 0xD9]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(file, "file", "shop.jpg");
        req.Content = content;
        using var res = await Http.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, text);
        return JsonNode.Parse(text)!["url"]!.GetValue<string>();
    }

    [Fact]
    public async Task Agents_are_created_from_the_dashboard_and_sign_in_with_username_and_password()
    {
        if (!_fx.Available) return; // no SQL Server on this machine
        var admin = ApiFixture.AdminToken();

        var bad = await Send(HttpMethod.Post, "/api/v1/admin/agents", admin, new
        {
            username = "ع", password = "", firstName = "Ali", fatherName = "حسن", grandfatherName = "كاظم", phone = "123",
        }, HttpStatusCode.BadRequest);
        foreach (var f in new[] { "username", "password", "firstName", "phone" }) Assert.NotNull(bad["fields"]![f]);
        Assert.Null(bad["fields"]!["cityValue"]); // agents belong to HQ, no branch

        var (id, token) = await CreateAgentAndLogin("Ali.Hassan", ApiFixture.Phones[0], "علي");
        var me = await Send(HttpMethod.Get, "/api/v1/me", token);
        Assert.Equal("ali.hassan", me["username"]!.GetValue<string>());
        Assert.Equal(id, me["id"]!.GetValue<long>());
        // Dates are sent as UTC with "Z" so the app and dashboard show Iraq time (not the raw UTC clock).
        Assert.EndsWith("Z", me["createdAt"]!.GetValue<string>());

        // Arabic usernames and any non-empty password; diacritics, tatweel and Arabic digits are ignored at sign-in.
        var arabic = await Send(HttpMethod.Post, "/api/v1/admin/agents", admin, new
        {
            username = "حيدر٧", password = "1", firstName = "حيدر", fatherName = "علي", grandfatherName = "كاظم", phone = ApiFixture.Phones[6],
        });
        Assert.Equal("حيدر7", arabic["agent"]!["username"]!.GetValue<string>());
        var arabicLogin = await Send(HttpMethod.Post, "/api/v1/auth/login", body: new { username = "حَيـدر7", password = "1" });
        Assert.Equal("حيدر7", arabicLogin["user"]!["username"]!.GetValue<string>());
        await Send(HttpMethod.Post, "/api/v1/admin/agents", admin, new
        {
            username = "حيدر 7", password = "1", firstName = "حيدر", fatherName = "علي", grandfatherName = "كاظم", phone = ApiFixture.Phones[7],
        }, HttpStatusCode.BadRequest); // no spaces
        Assert.Null(me["cityValue"]?.GetValue<int?>());

        // Username is unique (case-insensitive); wrong password and unknown user answer the same.
        var dup = await Send(HttpMethod.Post, "/api/v1/admin/agents", admin, new
        {
            username = "ALI.HASSAN", password = "pass1234", firstName = "حيدر", fatherName = "حسن", grandfatherName = "كاظم", phone = ApiFixture.Phones[1], cityValue = 1,
        }, HttpStatusCode.BadRequest);
        Assert.NotNull(dup["fields"]!["username"]);
        var wrong = await Send(HttpMethod.Post, "/api/v1/auth/login", body: new { username = "ali.hassan", password = "nope1234" }, expect: HttpStatusCode.Unauthorized);
        var unknown = await Send(HttpMethod.Post, "/api/v1/auth/login", body: new { username = "nobody", password = "nope1234" }, expect: HttpStatusCode.Unauthorized);
        Assert.Equal(wrong["error"]!.GetValue<string>(), unknown["error"]!.GetValue<string>());

        // No sign-up, no SMS login, no invites.
        await Send(HttpMethod.Post, "/api/v1/auth/otp/request", body: new { phone = ApiFixture.Phones[2] }, expect: HttpStatusCode.NotFound);
        await Send(HttpMethod.Get, "/api/v1/network/summary", token, expect: HttpStatusCode.NotFound);

        // New password: old one stops working, sessions are revoked.
        var login = await Send(HttpMethod.Post, "/api/v1/auth/login", body: new { username = "ali.hassan", password = "pass1234" });
        await Send(HttpMethod.Post, $"/api/v1/admin/agents/{id}/password", admin, new { password = "newpass99" }, HttpStatusCode.NoContent);
        await Send(HttpMethod.Post, "/api/v1/auth/refresh", body: new { refreshToken = login["refreshToken"]!.GetValue<string>() }, expect: HttpStatusCode.Unauthorized);
        await Send(HttpMethod.Post, "/api/v1/auth/login", body: new { username = "ali.hassan", password = "pass1234" }, expect: HttpStatusCode.Unauthorized);
        await Send(HttpMethod.Post, "/api/v1/auth/login", body: new { username = "ali.hassan", password = "newpass99" });

        // Edit and suspend.
        var edited = await Send(HttpMethod.Put, $"/api/v1/admin/agents/{id}", admin, new
        {
            username = "ali.h", firstName = "علي", fatherName = "حسين", grandfatherName = "كاظم", phone = ApiFixture.Phones[0], cityValue = 9,
        });
        Assert.Equal("ali.h", edited["agent"]!["username"]!.GetValue<string>());
        Assert.Null(edited["agent"]!["cityValue"]?.GetValue<int?>()); // a branch sent by an old client is ignored
        await Send(HttpMethod.Post, $"/api/v1/admin/agents/{id}/suspend", admin, new { suspended = true, reason = "انتهى العقد" });
        await Send(HttpMethod.Post, "/api/v1/auth/login", body: new { username = "ali.h", password = "newpass99" }, expect: HttpStatusCode.Forbidden);
        var list = await Send(HttpMethod.Get, "/api/v1/admin/agents?q=ali.h", admin);
        Assert.True(list["items"]![0]!["isSuspended"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Sale_order_is_short_and_is_followed_until_fully_paid()
    {
        if (!_fx.Available) return;
        var admin = ApiFixture.AdminToken();
        var (_, agent) = await CreateAgentAndLogin("sajjad", ApiFixture.Phones[3], "سجاد");

        var body = new Dictionary<string, object?>
        {
            ["cityValue"] = 1, ["purchaseType"] = "DailyInstallments",
            ["customer"] = new { firstName = "محمد", fatherName = "جاسم", grandfatherName = "عبد", phone = "07812345678" },
            ["address"] = new { area = "حي الأنصار", details = "شارع 20 قرب الجامع الكبير" },
            ["shop"] = new { name = "أسواق النور", businessType = "مواد غذائية", ownsShop = true },
            ["items"] = new[] { new { name = " ", quantity = 1 } },
        };
        var missing = await Send(HttpMethod.Post, "/api/v1/orders", agent, body, HttpStatusCode.BadRequest);
        Assert.NotNull(missing["fields"]!["items"]); // the agent must say what was sold
        // Shop photo and GPS location are optional.
        Assert.Null(missing["fields"]!["address.latitude"]);
        Assert.Null(missing["fields"]!["shopPhotoUrl"]);

        // The shortest order: product, full name in one field, phone, branch and one address line; installments by default.
        var minimal = await Send(HttpMethod.Post, "/api/v1/orders", agent, new
        {
            cityValue = 1,
            customer = new { fullName = "  عبد الله حسين علي الكعبي ", phone = "07812345690" },
            address = new { details = "الكوفة - حي ميثم" },
            items = new[] { new { name = "ايفون", quantity = 1 } },
        }, HttpStatusCode.Created, Guid.NewGuid().ToString());
        Assert.Equal("عبدالله حسين علي الكعبي", minimal["customerName"]!.GetValue<string>());
        Assert.Equal("DailyInstallments", minimal["purchaseType"]!.GetValue<string>());
        Assert.Null(minimal["shopPhotoUrl"]);
        Assert.Null(minimal["address"]!["latitude"]);
        Assert.Equal("ايفون", minimal["itemsSummary"]!.GetValue<string>());
        var shortName = await Send(HttpMethod.Post, "/api/v1/orders", agent, new
        {
            cityValue = 1, customer = new { fullName = "محمد علي", phone = "07812345691" }, address = new { details = "الكوفة" },
            items = new[] { new { name = "دراجة", quantity = 1 } },
        }, HttpStatusCode.BadRequest);
        Assert.NotNull(shortName["fields"]!["customer.fullName"]);

        // Items are typed freely: no catalog, no prices. The same name twice becomes one line.
        body["items"] = new[] { new { name = "ثلاجة  سامسونج 18 قدم", quantity = 1 }, new { name = "ثلاجة سامسونج 18 قدم", quantity = 1 }, new { name = "مبردة هواء", quantity = 1 } };

        body["address"] = new { area = "حي الأنصار", details = "شارع 20 قرب الجامع الكبير", latitude = 32.0256m, longitude = 44.3269m };
        body["shopPhotoUrl"] = "https://example.com/not-ours.jpg";
        Assert.NotNull((await Send(HttpMethod.Post, "/api/v1/orders", agent, body, HttpStatusCode.BadRequest))["fields"]!["shopPhotoUrl"]);

        body["shopPhotoUrl"] = await UploadShopPhoto(agent);
        var order = await Send(HttpMethod.Post, "/api/v1/orders", agent, body, HttpStatusCode.Created, Guid.NewGuid().ToString());
        var orderId = order["id"]!.GetValue<long>();
        Assert.Contains("/media/shops/", order["shopPhotoUrl"]!.GetValue<string>());
        Assert.Equal(32.0256m, order["address"]!["latitude"]!.GetValue<decimal>());
        Assert.Null(order["sale"]);
        Assert.Equal("ثلاجة سامسونج 18 قدم ×2، مبردة هواء", order["itemsSummary"]!.GetValue<string>());
        Assert.Equal(2, order["items"]!.AsArray().Count);
        Assert.Null(order["estimatedTotal"]);

        // Sending the same item for the same customer again is refused while the first order is open.
        body["items"] = new[] { new { name = "مبردة هواء", quantity = 1 } };
        await Send(HttpMethod.Post, "/api/v1/orders", agent, body, HttpStatusCode.Conflict, Guid.NewGuid().ToString());
        // Sent straight to the company system's sales requests (no review step in the dashboard).
        Assert.Equal("Transferred", order["status"]!.GetValue<string>());
        await Send(HttpMethod.Post, $"/api/v1/admin/orders/{orderId}/approve", admin, new { note = "موافق" }, HttpStatusCode.Conflict);
        string status = "";
        for (var i = 0; i < 4 && status != "Sold"; i++)
            status = (await Send(HttpMethod.Post, $"/api/v1/admin/orders/{orderId}/sync", admin))["order"]!["status"]!.GetValue<string>();
        Assert.Equal("Sold", status);

        // The admin sync also polls payments: the fake company system pays 35% per poll.
        var sold = await Send(HttpMethod.Get, $"/api/v1/orders/{orderId}", agent);
        Assert.Equal("Sold", sold["status"]!.GetValue<string>());
        var sale = sold["sale"]!;
        Assert.NotNull(sale["saleId"]);
        Assert.True(sale["paid"]!.GetValue<long>() > 0);
        Assert.True(sale["remaining"]!.GetValue<long>() > 0);

        for (var i = 0; i < 3; i++) await Send(HttpMethod.Post, $"/api/v1/admin/orders/{orderId}/sync", admin);
        var finished = await Send(HttpMethod.Get, $"/api/v1/orders/{orderId}", agent);
        Assert.Equal("Finished", finished["status"]!.GetValue<string>());
        Assert.Equal(0, finished["sale"]!["remaining"]!.GetValue<long>());
        Assert.Equal(100, finished["sale"]!["progressPercent"]!.GetValue<int>());
        Assert.Equal(["New", "Transferred", "FieldInspection", "Sold", "Finished"],
            finished["timeline"]!.AsArray().Select(t => t!["status"]!.GetValue<string>()).ToArray());
        Assert.Equal(1, (await Send(HttpMethod.Get, "/api/v1/orders?status=Finished", agent))["total"]!.GetValue<int>());
        Assert.Equal(1, (await Send(HttpMethod.Get, "/api/v1/orders?status=Sold,Finished", agent))["total"]!.GetValue<int>());
        Assert.Equal(0, (await Send(HttpMethod.Get, "/api/v1/orders?status=New,UnderReview", agent))["total"]!.GetValue<int>());
        var counts = await Send(HttpMethod.Get, "/api/v1/orders/counts", agent);
        Assert.Equal(1, counts["Finished"]!.GetValue<int>());
        Assert.Equal(2, counts["all"]!.GetValue<int>()); // + the minimal order, still with the sales department

        // The store and commissions are gone.
        await Send(HttpMethod.Get, "/api/v1/earnings/summary", agent, expect: HttpStatusCode.NotFound);
        await Send(HttpMethod.Get, "/api/v1/catalog/products", expect: HttpStatusCode.NotFound);
        await Send(HttpMethod.Get, "/api/v1/admin/products", admin, expect: HttpStatusCode.NotFound);

        // The app's home page and the dashboard overview count it.
        var overview = await Send(HttpMethod.Get, "/api/v1/me/overview", agent);
        Assert.Equal(2, overview["ordersToday"]!.GetValue<int>());
        Assert.Equal(1, overview["soldThisMonth"]!.GetValue<int>());
        Assert.Equal(1, overview["fullyPaid"]!.GetValue<int>());
        Assert.Equal(1, overview["openOrders"]!.GetValue<int>());
        Assert.Equal(orderId, overview["recentOrders"]![0]!["id"]!.GetValue<long>());
        var report = await Send(HttpMethod.Get, "/api/v1/admin/reports/overview", admin);
        Assert.True(report["sold"]!.GetValue<int>() >= 1);
        var row = report["byAgent"]!.AsArray().Single(a => a!["username"]!.GetValue<string>() == "sajjad")!;
        Assert.Equal(1, row["sold"]!.GetValue<int>());
        // Every number of the row opens the list of names counted in it.
        foreach (var bucket in new[] { "orders", "waiting", "withAgent", "sold", "finished", "denied", "notSold", "pins" })
        {
            var names = (await Send(HttpMethod.Get, $"/api/v1/admin/reports/agent-details?userId={row["userId"]}&bucket={bucket}", admin)).AsArray();
            Assert.Equal(row[bucket]!.GetValue<int>(), names.Count);
        }
        await Send(HttpMethod.Get, $"/api/v1/admin/reports/agent-details?userId={row["userId"]}&bucket=nope", admin, expect: HttpStatusCode.BadRequest);
        Assert.Contains(report["topItems"]!.AsArray(), i => i!["name"]!.GetValue<string>() == "ثلاجة سامسونج 18 قدم" && i["quantity"]!.GetValue<int>() == 2);

        var adminOrder = await Send(HttpMethod.Get, $"/api/v1/admin/orders/{orderId}", admin);
        Assert.NotNull(adminOrder["shopPhotoUrl"]);
        Assert.Equal("Finished", adminOrder["order"]!["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Work_day_records_sessions_and_live_location_for_the_dashboard()
    {
        if (!_fx.Available) return;
        var admin = ApiFixture.AdminToken();
        var (id, agent) = await CreateAgentAndLogin("karrar", ApiFixture.Phones[4], "كرار");

        var today = await Send(HttpMethod.Get, "/api/v1/work/today", agent);
        Assert.False(today["isWorking"]!.GetValue<bool>());
        Assert.Empty(today["sessions"]!.AsArray());

        // Points sent before starting are dropped and tell the app to stop tracking.
        var early = await Send(HttpMethod.Post, "/api/v1/work/locations", agent, new { points = new[] { new { latitude = 32.0m, longitude = 44.3m, recordedAt = DateTime.UtcNow } } });
        Assert.False(early["isWorking"]!.GetValue<bool>());

        var started = await Send(HttpMethod.Post, "/api/v1/work/start", agent, new { latitude = 32.0256m, longitude = 44.3269m });
        Assert.True(started["isWorking"]!.GetValue<bool>());
        await Send(HttpMethod.Post, "/api/v1/work/start", agent, new { }); // starting twice keeps one open session

        var now = DateTime.UtcNow;
        var batch = await Send(HttpMethod.Post, "/api/v1/work/locations", agent, new
        {
            points = new object[]
            {
                new { latitude = 32.0260m, longitude = 44.3275m, accuracy = 8f, recordedAt = now.AddSeconds(1) },
                new { latitude = 32.0270m, longitude = 44.3290m, accuracy = 6f, battery = 80, recordedAt = now.AddSeconds(30) },
                new { latitude = 0m, longitude = 0m, recordedAt = now.AddSeconds(40) },            // no fix
                new { latitude = 32.0280m, longitude = 44.3300m, recordedAt = now.AddHours(2) },    // future
            },
        });
        Assert.Equal(2, batch["accepted"]!.GetValue<int>());
        Assert.True(batch["isWorking"]!.GetValue<bool>());

        var live = (await Send(HttpMethod.Get, "/api/v1/admin/attendance/live", admin)).AsArray();
        var me = live.Single(a => a!["userId"]!.GetValue<long>() == id)!;
        Assert.Equal(32.027m, me["lastPoint"]!["lat"]!.GetValue<decimal>());

        var row = (await Send(HttpMethod.Get, "/api/v1/admin/attendance", admin)).AsArray().Single(r => r!["userId"]!.GetValue<long>() == id)!;
        Assert.True(row["isWorking"]!.GetValue<bool>());
        Assert.NotNull(row["firstStartedAt"]);
        Assert.Null(row["lastEndedAt"]);
        Assert.Equal(3, row["pointsCount"]!.GetValue<int>()); // start point + 2

        var ended = await Send(HttpMethod.Post, "/api/v1/work/end", agent, new { latitude = 32.0290m, longitude = 44.3310m });
        Assert.False(ended["isWorking"]!.GetValue<bool>());
        Assert.NotNull(ended["lastEndedAt"]);
        Assert.Equal(4, ended["track"]!.AsArray().Count); // the end point was recorded before closing

        // A second shift the same day adds a session.
        await Send(HttpMethod.Post, "/api/v1/work/start", agent, new { });
        await Send(HttpMethod.Post, "/api/v1/work/end", agent, new { });
        var day = await Send(HttpMethod.Get, $"/api/v1/admin/attendance/{id}/day", admin);
        Assert.Equal(2, day["sessions"]!.AsArray().Count);
        Assert.Equal(2, day["summary"]!["sessionsCount"]!.GetValue<int>());
        Assert.Equal(4, day["track"]!.AsArray().Count);
        Assert.DoesNotContain((await Send(HttpMethod.Get, "/api/v1/admin/attendance/live", admin)).AsArray(), a => a!["userId"]!.GetValue<long>() == id);

        var history = (await Send(HttpMethod.Get, $"/api/v1/admin/attendance/{id}/history", admin)).AsArray();
        Assert.Single(history);
        Assert.Equal(2, history[0]!["sessionsCount"]!.GetValue<int>());

        // Background tracking (app closed): the phone's service sends points with the day's key, not the sign-in token.
        await Send(HttpMethod.Post, "/api/v1/work/tracking-key", agent, expect: HttpStatusCode.Conflict); // not working
        await Send(HttpMethod.Post, "/api/v1/work/start", agent, new { });
        var key = (await Send(HttpMethod.Post, "/api/v1/work/tracking-key", agent))["key"]!.GetValue<string>();
        async Task<JsonNode> Track(string k)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/work/track");
            req.Headers.Add("X-Tracking-Key", k);
            req.Content = JsonContent.Create(new { points = new[] { new { latitude = 32.031m, longitude = 44.335m, recordedAt = DateTime.UtcNow } } });
            using var res = await Http.SendAsync(req);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            return JsonNode.Parse(await res.Content.ReadAsStringAsync())!;
        }
        var tracked = await Track(key);
        Assert.Equal(1, tracked["accepted"]!.GetValue<int>());
        Assert.True(tracked["isWorking"]!.GetValue<bool>());
        Assert.False((await Track("wrong-key"))["isWorking"]!.GetValue<bool>());
        await Send(HttpMethod.Post, "/api/v1/work/end", agent, new { });
        Assert.False((await Track(key))["isWorking"]!.GetValue<bool>()); // the key dies with the day

        await Send(HttpMethod.Get, "/api/v1/admin/attendance", agent, expect: HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Agents_pin_potential_shops_that_every_agent_and_the_dashboard_can_see()
    {
        if (!_fx.Available) return;
        var admin = ApiFixture.AdminToken();
        var (aliId, ali) = await CreateAgentAndLogin("ali.pins", ApiFixture.Phones[5], "علي");
        var (_, zaid) = await CreateAgentAndLogin("zaid.pins", "07700000023", "زيد");

        var invalid = await Send(HttpMethod.Post, "/api/v1/prospects", ali, new { latitude = 32.5m, longitude = 44.4m }, HttpStatusCode.BadRequest);
        Assert.NotNull(invalid["fields"]!["shopName"]);
        Assert.NotNull(invalid["fields"]!["interest"]);
        Assert.NotNull((await Send(HttpMethod.Post, "/api/v1/prospects", ali,
            new { latitude = 32.5m, longitude = 44.4m, shopName = "محل", interest = 3, phone = "123" }, HttpStatusCode.BadRequest))["fields"]!["phone"]);

        var pin = await Send(HttpMethod.Post, "/api/v1/prospects", ali, new
        {
            latitude = 32.512345m, longitude = 44.421234m, shopName = "أسواق الرافدين", description = "مواد غذائية بالجملة", interest = 4,
            phone = "07801234567", goodsValue = 25_000_000, lengthM = 8.5m, widthM = 4m,
        });
        var pinId = pin["id"]!.GetValue<long>();
        Assert.Equal("مهتم", pin["interestLabel"]!.GetValue<string>());
        Assert.Equal(34m, pin["areaM2"]!.GetValue<decimal>());
        Assert.True(pin["isMine"]!.GetValue<bool>());
        await Send(HttpMethod.Post, "/api/v1/prospects", ali, new { latitude = 30.5m, longitude = 47.8m, shopName = "محل البصرة", interest = 1 });

        // Another agent in the same area sees it (not editable by them); an area elsewhere does not include it.
        var near = (await Send(HttpMethod.Get, "/api/v1/prospects?minLat=32.4&minLng=44.3&maxLat=32.6&maxLng=44.5", zaid)).AsArray();
        var seen = Assert.Single(near)!;
        Assert.Equal("أسواق الرافدين", seen["shopName"]!.GetValue<string>());
        Assert.False(seen["isMine"]!.GetValue<bool>());
        Assert.Contains("علي", seen["createdByName"]!.GetValue<string>());
        await Send(HttpMethod.Put, $"/api/v1/prospects/{pinId}", zaid, new { latitude = 32.5m, longitude = 44.4m, shopName = "x y", interest = 1 }, HttpStatusCode.Forbidden);
        await Send(HttpMethod.Delete, $"/api/v1/prospects/{pinId}", zaid, expect: HttpStatusCode.Forbidden);

        var edited = await Send(HttpMethod.Put, $"/api/v1/prospects/{pinId}", ali, new
        {
            latitude = 32.512345m, longitude = 44.421234m, shopName = "أسواق الرافدين", interest = 5,
        });
        Assert.Equal(5, edited["interest"]!.GetValue<int>());
        Assert.Null(edited["phone"]);

        // «ملخصي» counts the agent's own pins per interest level (all five levels, most interested first).
        var mine = await Send(HttpMethod.Get, "/api/v1/me/overview", ali);
        Assert.Equal(2, mine["pinsTotal"]!.GetValue<int>());
        var levels = mine["pinsByInterest"]!.AsArray();
        Assert.Equal([5, 4, 3, 2, 1], levels.Select(l => l!["interest"]!.GetValue<int>()).ToArray());
        Assert.Equal([1, 0, 0, 0, 1], levels.Select(l => l!["count"]!.GetValue<int>()).ToArray());
        Assert.Equal("مهتم جداً", levels[0]!["label"]!.GetValue<string>());
        Assert.Equal(0, (await Send(HttpMethod.Get, "/api/v1/me/overview", zaid))["pinsTotal"]!.GetValue<int>());

        // «محلاتي»: own pins only, newest first, filter by level or search.
        var list = await Send(HttpMethod.Get, "/api/v1/prospects/mine", ali);
        Assert.Equal(2, list["total"]!.GetValue<int>());
        Assert.Equal(["محل البصرة", "أسواق الرافدين"], list["items"]!.AsArray().Select(p => p!["shopName"]!.GetValue<string>()).ToArray());
        Assert.True(list["items"]![0]!["isMine"]!.GetValue<bool>());
        Assert.Equal(1, (await Send(HttpMethod.Get, "/api/v1/prospects/mine?interest=5", ali))["total"]!.GetValue<int>());
        Assert.Equal(1, (await Send(HttpMethod.Get, "/api/v1/prospects/mine?q=" + Uri.EscapeDataString("البصرة"), ali))["total"]!.GetValue<int>());
        Assert.Equal(0, (await Send(HttpMethod.Get, "/api/v1/prospects/mine", zaid))["total"]!.GetValue<int>());

        var all = await Send(HttpMethod.Get, $"/api/v1/admin/prospects?userId={aliId}", admin);
        Assert.Equal(2, all["total"]!.GetValue<int>());
        Assert.Equal(1, (await Send(HttpMethod.Get, "/api/v1/admin/prospects?minInterest=5", admin))["total"]!.GetValue<int>());
        await Send(HttpMethod.Delete, $"/api/v1/admin/prospects/{pinId}", admin, expect: HttpStatusCode.NoContent);
        Assert.Empty((await Send(HttpMethod.Get, "/api/v1/prospects?minLat=32.4&minLng=44.3&maxLat=32.6&maxLng=44.5", zaid)).AsArray());
        await Send(HttpMethod.Get, "/api/v1/admin/prospects", ali, expect: HttpStatusCode.Unauthorized);
    }

    /// <summary>Moves pins to fixed minutes after 08:00 (Iraq time) of today, oldest first.</summary>
    private async Task SetPinTimes(IReadOnlyList<long> ids, params int[] minutesAfterEight)
    {
        var start = DateTime.UtcNow.AddHours(3).Date.AddHours(8).AddHours(-3); // 08:00 Iraq time, in UTC
        await using var c = new SqlConnection(_fx.ConnectionString);
        await c.OpenAsync();
        for (var i = 0; i < ids.Count; i++)
        {
            await using var cmd = new SqlCommand("UPDATE agents.Prospects SET CreatedAt = @at WHERE Id = @id", c);
            cmd.Parameters.AddWithValue("@at", start.AddMinutes(minutesAfterEight[i]));
            cmd.Parameters.AddWithValue("@id", ids[i]);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task Pin_periods_measure_work_time_and_the_daily_time_target()
    {
        if (!_fx.Available) return;
        var admin = ApiFixture.AdminToken();
        var (aId, a) = await CreateAgentAndLogin("target.met", "07700000035", "حسين");
        var (bId, b) = await CreateAgentAndLogin("target.missed", "07700000037", "عباس");
        var (cId, _) = await CreateAgentAndLogin("target.off", "07700000039", "كاظم");

        // Default target 20 minutes for everyone; agent B has their own target of 60.
        await Send(HttpMethod.Put, "/api/v1/admin/settings", admin, new { supportPhone = "", supportWhatsapp = "", dailyWorkMinutesTarget = 20 });
        Assert.NotNull((await Send(HttpMethod.Put, $"/api/v1/admin/agents/{bId}/target", admin, new { dailyWorkMinutesTarget = 1441 },
            HttpStatusCode.BadRequest))["fields"]!["dailyWorkMinutesTarget"]);
        await Send(HttpMethod.Put, $"/api/v1/admin/agents/{bId}/target", admin, new { dailyWorkMinutesTarget = 60 });

        async Task<long> Pin(string token, int n) => (await Send(HttpMethod.Post, "/api/v1/prospects", token,
            new { latitude = 32.05m + n / 1000m, longitude = 44.37m, shopName = $"محل الهدف {n}", interest = 2,
                agentLatitude = 32.0505m, agentLongitude = 44.3702m }, appVersion: "1.4.9"))["id"]!.GetValue<long>();
        var aPins = new List<long>();
        for (var i = 1; i <= 5; i++) aPins.Add(await Pin(a, i));
        var bPins = new List<long> { await Pin(b, 6), await Pin(b, 7) };
        // A: 08:00, 08:10, 08:12 (quick: 2 min) | 28 min break | 08:40, 08:50 → periods of 12 + 10 = 22 minutes.
        await SetPinTimes(aPins, 0, 10, 12, 40, 50);
        // B: 08:00, 08:30 → two single-pin periods, 0 minutes.
        await SetPinTimes(bPins, 0, 30);

        // The app never sees a target.
        Assert.Equal(0, (await Send(HttpMethod.Get, "/api/v1/me/overview", a))["dailyPinTarget"]!.GetValue<int>());
        Assert.Equal(0, (await Send(HttpMethod.Get, "/api/v1/me/overview", b))["dailyPinTarget"]!.GetValue<int>());

        // «المحلات المثبّتة»: A's timeline for today.
        var t = await Send(HttpMethod.Get, $"/api/v1/admin/prospects/timeline?userId={aId}", admin);
        Assert.Equal(22, t["workMinutes"]!.GetValue<int>());
        Assert.Equal(20, t["targetMinutes"]!.GetValue<int>());
        Assert.True(t["met"]!.GetValue<bool>());
        Assert.Equal(1, t["quickPins"]!.GetValue<int>());
        var periods = t["periods"]!.AsArray();
        Assert.Equal(2, periods.Count);
        Assert.Equal(12 * 60, periods[0]!["seconds"]!.GetValue<int>());
        Assert.Equal(3, periods[0]!["pins"]!.GetValue<int>());
        var pins = t["pins"]!.AsArray();
        Assert.Equal(aPins, pins.Select(x => x!["pin"]!["id"]!.GetValue<long>()).ToList());
        Assert.True(pins[0]!["startsPeriod"]!.GetValue<bool>());
        Assert.Null(pins[0]!["secondsOnShop"]);
        Assert.Equal(600, pins[1]!["secondsOnShop"]!.GetValue<int>());
        Assert.True(pins[2]!["quick"]!.GetValue<bool>());
        Assert.True(pins[2]!["endsPeriod"]!.GetValue<bool>());
        Assert.True(pins[3]!["startsPeriod"]!.GetValue<bool>());
        Assert.Equal(28 * 60, pins[3]!["breakSecondsBefore"]!.GetValue<int>());
        Assert.Equal(32.0505m, pins[0]!["pin"]!["agentLatitude"]!.GetValue<decimal>());
        await Send(HttpMethod.Get, $"/api/v1/admin/prospects/timeline?userId={aId}", a, expect: HttpStatusCode.Unauthorized);

        // The dashboard's day view (today, live).
        static JsonNode ResultOf(JsonNode report, long id) =>
            report["agents"]!.AsArray().Single(r => r!["userId"]!.GetValue<long>() == id)!["result"]!;
        var day = await Send(HttpMethod.Get, "/api/v1/admin/targets", admin);
        Assert.True(day["isToday"]!.GetValue<bool>());
        Assert.Equal("met", ResultOf(day, aId)["status"]!.GetValue<string>());
        Assert.Equal(22, ResultOf(day, aId)["workMinutes"]!.GetValue<int>());
        Assert.Equal("missed", ResultOf(day, bId)["status"]!.GetValue<string>());
        Assert.Equal(2, ResultOf(day, bId)["periods"]!.GetValue<int>());
        Assert.Equal("off", ResultOf(day, cId)["status"]!.GetValue<string>());

        // Finished days are frozen with that day's target: move the pins and the record marker one day back, then close.
        await using (var c = new SqlConnection(_fx.ConnectionString))
        {
            await c.OpenAsync();
            const string sql =
                "UPDATE agents.Prospects SET CreatedAt = DATEADD(DAY, -1, CreatedAt) WHERE CreatedByUserId IN (@a, @b); " +
                "MERGE agents.AppSettings AS t USING (SELECT 'WorkTargetLastClosedDay' AS [Key], " +
                "CONVERT(VARCHAR(10), DATEADD(DAY, -2, DATEADD(HOUR, 3, SYSUTCDATETIME())), 23) AS [Value]) AS s " +
                "ON t.[Key] = s.[Key] WHEN MATCHED THEN UPDATE SET [Value] = s.[Value] " +
                "WHEN NOT MATCHED THEN INSERT ([Key], [Value]) VALUES (s.[Key], s.[Value]);";
            await using var cmd = new SqlCommand(sql, c);
            cmd.Parameters.AddWithValue("@a", aId);
            cmd.Parameters.AddWithValue("@b", bId);
            await cmd.ExecuteNonQueryAsync();
        }
        using (var scope = _fx.Services.CreateScope())
        {
            var recorded = await scope.ServiceProvider.GetRequiredService<BE_CustomerApp.Domain.Work.WorkTargetService>().CloseDaysAsync(CancellationToken.None);
            Assert.True(recorded >= 2);
        }
        // Changing the target later does not rewrite that day.
        await Send(HttpMethod.Put, $"/api/v1/admin/agents/{bId}/target", admin, new { dailyWorkMinutesTarget = 1 });

        var historyB = await Send(HttpMethod.Get, $"/api/v1/admin/agents/{bId}/targets", admin);
        var frozenB = historyB["days"]!.AsArray().First(d => d!["final"]!.GetValue<bool>())!;
        Assert.Equal("missed", frozenB["status"]!.GetValue<string>());
        Assert.Equal(60, frozenB["targetMinutes"]!.GetValue<int>());
        Assert.Equal(1, historyB["missedDays"]!.GetValue<int>());
        Assert.Equal(1, historyB["currentTargetMinutes"]!.GetValue<int>());
        Assert.Equal(1, historyB["ownTargetMinutes"]!.GetValue<int>());
        var historyA = await Send(HttpMethod.Get, $"/api/v1/admin/agents/{aId}/targets", admin);
        Assert.Equal(1, historyA["metDays"]!.GetValue<int>());
        Assert.Null(historyA["ownTargetMinutes"]);

        var frozenDay = frozenB["day"]!.GetValue<string>();
        var past = await Send(HttpMethod.Get, $"/api/v1/admin/targets?date={frozenDay}", admin);
        Assert.False(past["isToday"]!.GetValue<bool>());
        Assert.Equal("met", ResultOf(past, aId)["status"]!.GetValue<string>());
        Assert.Equal("missed", ResultOf(past, bId)["status"]!.GetValue<string>());
        // The frozen day's timeline keeps the target it was judged with.
        var pastTimeline = await Send(HttpMethod.Get, $"/api/v1/admin/prospects/timeline?userId={bId}&date={frozenDay}", admin);
        Assert.Equal(60, pastTimeline["targetMinutes"]!.GetValue<int>());
        Assert.Equal(2, pastTimeline["pins"]!.AsArray().Count);

        // Back to no default target so other tests are not affected.
        await Send(HttpMethod.Put, "/api/v1/admin/settings", admin, new { supportPhone = "", supportWhatsapp = "", dailyWorkMinutesTarget = 0 });
    }

    [Fact]
    public async Task Pins_need_only_name_interest_and_location_and_keep_the_agents_position()
    {
        if (!_fx.Available) return;
        var (_, agent) = await CreateAgentAndLogin("minutes.agent", "07700000033", "علي");
        var pin = new { latitude = 32.03m, longitude = 44.35m, shopName = "محلات الهدى", interest = 3 };

        // Minutes are no longer asked for (1.4.9); apps 1.4.7–1.4.8 still send them and they are still checked.
        var saved = await Send(HttpMethod.Post, "/api/v1/prospects", agent,
            new { pin.latitude, pin.longitude, pin.shopName, pin.interest, agentLatitude = 32.0312m, agentLongitude = 44.3521m }, appVersion: "1.4.9");
        Assert.Null(saved["minutesSpent"]);
        Assert.Equal(32.0312m, saved["agentLatitude"]!.GetValue<decimal>());
        Assert.Equal(44.3521m, saved["agentLongitude"]!.GetValue<decimal>());
        var tooLong = await Send(HttpMethod.Post, "/api/v1/prospects", agent, new { pin.latitude, pin.longitude, pin.shopName, pin.interest, minutesSpent = 601 },
            HttpStatusCode.BadRequest, appVersion: "1.4.8");
        Assert.NotNull(tooLong["fields"]!["minutesSpent"]);
        // A bad phone position never blocks the pin; it is just not kept.
        var outside = await Send(HttpMethod.Post, "/api/v1/prospects", agent,
            new { latitude = 32.04m, longitude = 44.36m, shopName = "أسواق السلام", interest = 4, minutesSpent = 25, agentLatitude = 0m, agentLongitude = 0m },
            appVersion: "1.4.8");
        Assert.Null(outside["agentLatitude"]);
        Assert.Equal(25, outside["minutesSpent"]!.GetValue<int>());
        var id = outside["id"]!.GetValue<long>();

        // An edit without minutes keeps the stored value, and never changes the agent's position.
        var kept = await Send(HttpMethod.Put, $"/api/v1/prospects/{saved["id"]!.GetValue<long>()}", agent,
            new { latitude = 32.0301m, longitude = 44.3501m, shopName = "محلات الهدى", interest = 2, agentLatitude = 33m, agentLongitude = 44m });
        Assert.Equal(32.0312m, kept["agentLatitude"]!.GetValue<decimal>());
        Assert.Equal(25, (await Send(HttpMethod.Put, $"/api/v1/prospects/{id}", agent,
            new { latitude = 32.04m, longitude = 44.36m, shopName = "أسواق السلام", interest = 2 }))["minutesSpent"]!.GetValue<int>());

        // «المحلات» tab: a range of Iraq days (today by default in the app).
        var today = DateTime.UtcNow.AddHours(3).ToString("yyyy-MM-dd");
        var yesterday = DateTime.UtcNow.AddHours(3).AddDays(-1).ToString("yyyy-MM-dd");
        Assert.Equal(2, (await Send(HttpMethod.Get, $"/api/v1/prospects/mine?from={today}&to={today}", agent))["total"]!.GetValue<int>());
        Assert.Equal(0, (await Send(HttpMethod.Get, $"/api/v1/prospects/mine?from={yesterday}&to={yesterday}", agent))["total"]!.GetValue<int>());
        Assert.Equal(2, (await Send(HttpMethod.Get, $"/api/v1/prospects/mine?from={yesterday}", agent))["total"]!.GetValue<int>());
    }

    [Fact]
    public async Task The_company_rejection_reason_is_shown_to_the_agent()
    {
        if (!_fx.Available) return;
        var admin = ApiFixture.AdminToken();
        var (_, agent) = await CreateAgentAndLogin("reason.agent", "07700000031", "مصطفى");
        var order = await Send(HttpMethod.Post, "/api/v1/orders", agent, new
        {
            cityValue = 1, customer = new { fullName = "حسن علي كاظم", phone = "07812345601" }, address = new { details = "الكوفة - حي السعد" },
            items = new[] { new { name = "شاشة", quantity = 1 } },
        }, HttpStatusCode.Created, Guid.NewGuid().ToString());
        var orderId = order["id"]!.GetValue<long>();
        Assert.Equal("Transferred", order["status"]!.GetValue<string>());

        // The sales employee rejects it in the company system, with a reason.
        Guid uuid;
        await using (var c = new SqlConnection(_fx.ConnectionString))
        {
            await c.OpenAsync();
            await using var cmd = new SqlCommand("SELECT OrderUuid FROM agents.Orders WHERE Id = @id", c);
            cmd.Parameters.AddWithValue("@id", orderId);
            uuid = (Guid)(await cmd.ExecuteScalarAsync())!;
        }
        ((BE_CustomerApp.Integrations.Gateway.FakeGatewayClient)_fx.Services.GetRequiredService<BE_CustomerApp.Integrations.Gateway.IGatewayClient>())
            .Reject(uuid, "  الزبون لا يملك" + Environment.NewLine + " كفيلاً ");
        await Send(HttpMethod.Post, $"/api/v1/admin/orders/{orderId}/sync", admin);

        // The agent sees the company's reason (tidied), not the generic text: on the order, its timeline and the notification.
        var detail = await Send(HttpMethod.Get, $"/api/v1/orders/{orderId}", agent);
        Assert.Equal("NotSold", detail["status"]!.GetValue<string>());
        Assert.Equal("الزبون لا يملك كفيلاً", detail["publicReason"]!.GetValue<string>());
        var notSoldStep = detail["timeline"]!.AsArray().Single(t => t!["status"]!.GetValue<string>() == "NotSold")!;
        Assert.Equal("الزبون لا يملك كفيلاً", notSoldStep["note"]!.GetValue<string>());
        var notes = (await Send(HttpMethod.Get, "/api/v1/notifications", agent))["items"]!.AsArray();
        Assert.Contains(notes, n => n!["body"]!.GetValue<string>().Contains("الزبون لا يملك كفيلاً"));
    }

    [Fact]
    public async Task The_published_apk_version_is_mandatory_and_not_a_dashboard_setting()
    {
        if (!_fx.Available) return;
        // Nothing published: nothing is forced.
        File.Delete(_fx.ReleaseFile);
        var config = await Send(HttpMethod.Get, "/api/v1/app/config");
        Assert.Equal("0.0.0", config["minSupportedVersion"]!.GetValue<string>());

        // Publishing writes release.json: its version is both the latest and the minimum.
        const string url = "http://admincompany.example/mobile-updates/agents/qalaa-agents-latest.apk";
        await File.WriteAllTextAsync(_fx.ReleaseFile, $$"""{"version":"1.4.6","apkUrl":"{{url}}"}""");
        config = await Send(HttpMethod.Get, "/api/v1/app/config");
        Assert.Equal("1.4.6", config["minSupportedVersion"]!.GetValue<string>());
        Assert.Equal("1.4.6", config["latestVersion"]!.GetValue<string>());
        Assert.Equal(url, config["apkUrl"]!.GetValue<string>());

        // A newer upload is picked up without a restart.
        await Task.Delay(1100); // file timestamps can be 1 s granular
        await File.WriteAllTextAsync(_fx.ReleaseFile, $$"""{"version":"1.4.7","apkUrl":"{{url}}"}""");
        Assert.Equal("1.4.7", (await Send(HttpMethod.Get, "/api/v1/app/config"))["minSupportedVersion"]!.GetValue<string>());

        // A broken file never blocks the app.
        await Task.Delay(1100);
        await File.WriteAllTextAsync(_fx.ReleaseFile, """{"version":"latest"}""");
        Assert.Equal("0.0.0", (await Send(HttpMethod.Get, "/api/v1/app/config"))["minSupportedVersion"]!.GetValue<string>());
        File.Delete(_fx.ReleaseFile);

        // The dashboard settings no longer carry versions.
        var admin = ApiFixture.AdminToken();
        var settings = await Send(HttpMethod.Get, "/api/v1/admin/settings", admin);
        Assert.Null(settings["minSupportedVersion"]);
        Assert.Null(settings["apkDownloadUrl"]);
    }

    [Theory]
    [InlineData("المدير", HttpStatusCode.OK)]
    [InlineData("المراقب", HttpStatusCode.OK)]
    [InlineData("مسؤول المراقبين", HttpStatusCode.OK)]
    [InlineData("مسؤول لجنة المراقبة", HttpStatusCode.Forbidden)]
    [InlineData("مدير مبيعات", HttpStatusCode.Forbidden)]
    [InlineData("محاسب رئيسي", HttpStatusCode.Forbidden)]
    public async Task Dashboard_api_is_for_the_director_and_the_monitor_only(string userType, HttpStatusCode expected)
    {
        if (!_fx.Available) return;
        await Send(HttpMethod.Get, "/api/v1/admin/attendance", ApiFixture.AdminToken(userType), expect: expected);
    }

    [Fact]
    public async Task Dashboard_is_alerted_when_a_working_agent_stays_in_one_place_for_an_hour()
    {
        if (!_fx.Available) return;
        var admin = ApiFixture.AdminToken();
        var (id, agent) = await CreateAgentAndLogin("still.agent", "07700000025", "حيدر");
        await Send(HttpMethod.Post, "/api/v1/work/start", agent, new { });

        // Backdate the session and write 70 minutes of points within ~40 m of one shop.
        var now = DateTime.UtcNow;
        await using (var c = new SqlConnection(_fx.ConnectionString))
        {
            await c.OpenAsync();
            await using var cmd = new SqlCommand("""
                DECLARE @s BIGINT = (SELECT Id FROM agents.WorkSessions WHERE UserId = @u AND EndedAt IS NULL);
                UPDATE agents.WorkSessions SET StartedAt = DATEADD(MINUTE, -90, @now) WHERE Id = @s;
                DELETE agents.LocationPoints WHERE SessionId = @s;
                INSERT agents.LocationPoints(UserId, SessionId, Latitude, Longitude, RecordedAt) VALUES
                    (@u, @s, 32.0100, 44.3100, DATEADD(MINUTE, -85, @now)),
                    (@u, @s, 32.0200, 44.3200, DATEADD(MINUTE, -72, @now)),
                    (@u, @s, 32.0202, 44.3201, DATEADD(MINUTE, -50, @now)),
                    (@u, @s, 32.0199, 44.3203, DATEADD(MINUTE, -20, @now)),
                    (@u, @s, 32.0201, 44.3199, DATEADD(MINUTE, -2, @now));
                """, c);
            cmd.Parameters.AddWithValue("@u", id);
            cmd.Parameters.AddWithValue("@now", now);
            await cmd.ExecuteNonQueryAsync();
        }

        using (var scope = _fx.Services.CreateScope())
        {
            var alerts = scope.ServiceProvider.GetRequiredService<Domain.Work.WorkAlertService>();
            Assert.Equal(1, await alerts.DetectAsync(CancellationToken.None));
            Assert.Equal(0, await alerts.DetectAsync(CancellationToken.None)); // one alert per stay
        }

        var unseen = await Send(HttpMethod.Get, "/api/v1/admin/attendance/alerts/unseen", admin);
        var alert = unseen["latest"]!.AsArray().Single(a => a!["userId"]!.GetValue<long>() == id)!;
        Assert.True(alert["minutes"]!.GetValue<int>() >= 69);
        Assert.True(alert["isOpen"]!.GetValue<bool>());
        Assert.Equal(32.02m, Math.Round(alert["latitude"]!.GetValue<decimal>(), 2));

        await Send(HttpMethod.Post, $"/api/v1/admin/attendance/alerts/{alert["id"]!.GetValue<long>()}/seen", admin, expect: HttpStatusCode.NoContent);
        Assert.DoesNotContain((await Send(HttpMethod.Get, "/api/v1/admin/attendance/alerts/unseen", admin))["latest"]!.AsArray(),
            a => a!["userId"]!.GetValue<long>() == id);

        // Moving on resolves it.
        await Send(HttpMethod.Post, "/api/v1/work/locations", agent, new { points = new[] { new { latitude = 32.0300m, longitude = 44.3400m, recordedAt = DateTime.UtcNow } } });
        using (var scope = _fx.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<Domain.Work.WorkAlertService>().DetectAsync(CancellationToken.None);
        var today = (await Send(HttpMethod.Get, "/api/v1/admin/attendance/alerts", admin)).AsArray().Single(a => a!["userId"]!.GetValue<long>() == id)!;
        Assert.False(today["isOpen"]!.GetValue<bool>());
        Assert.Equal("مدير الاختبار", today["seenBy"]!.GetValue<string>());
    }

    /// <summary>Sets pins' times to minutes from now (negative = in the past).</summary>
    private async Task SetPinTimesFromNow(IReadOnlyList<long> ids, params int[] minutesFromNow)
    {
        await using var c = new SqlConnection(_fx.ConnectionString);
        await c.OpenAsync();
        for (var i = 0; i < ids.Count; i++)
        {
            await using var cmd = new SqlCommand("UPDATE agents.Prospects SET CreatedAt = @at WHERE Id = @id", c);
            cmd.Parameters.AddWithValue("@at", DateTime.UtcNow.AddMinutes(minutesFromNow[i]));
            cmd.Parameters.AddWithValue("@id", ids[i]);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task Agents_are_told_about_the_daily_target_and_the_dashboard_about_a_day_closed_early()
    {
        if (!_fx.Available) return;
        // The pins below are moved up to 2 h 10 min back and must stay inside today (Iraq time).
        if (DateTime.UtcNow.AddHours(3).TimeOfDay < TimeSpan.FromMinutes(140)) return;
        var admin = ApiFixture.AdminToken();
        var (aId, a) = await CreateAgentAndLogin("notice.reach", "07700000041", "مرتضى");
        var (bId, b) = await CreateAgentAndLogin("notice.idle", "07700000043", "سجاد");
        var (_, free) = await CreateAgentAndLogin("notice.none", "07700000045", "أمير");
        await Send(HttpMethod.Put, $"/api/v1/admin/agents/{aId}/target", admin, new { dailyWorkMinutesTarget = 20 });
        await Send(HttpMethod.Put, $"/api/v1/admin/agents/{bId}/target", admin, new { dailyWorkMinutesTarget = 120 });

        async Task<long> Pin(string token, int n) => (await Send(HttpMethod.Post, "/api/v1/prospects", token,
            new { latitude = 32.07m + n / 1000m, longitude = 44.39m, shopName = $"محل التنبيه {n}", interest = 3 }, appVersion: "1.4.11"))["id"]!.GetValue<long>();
        async Task<int> Detect()
        {
            using var scope = _fx.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<BE_CustomerApp.Domain.Work.DailyTargetService>().DetectAsync(CancellationToken.None);
        }
        async Task<List<JsonNode>> Inbox(string token, string title) =>
            (await Send(HttpMethod.Get, "/api/v1/notifications", token))["items"]!.AsArray()
                .Where(n => n!["title"]!.GetValue<string>() == title).Select(n => n!).ToList();

        // No target: nothing to warn about.
        var none = await Send(HttpMethod.Get, "/api/v1/work/target-status", free);
        Assert.False(none["hasTarget"]!.GetValue<bool>());

        // A worked 10 minutes of a 20-minute target.
        var aPins = new List<long> { await Pin(a, 1), await Pin(a, 2) };
        await SetPinTimesFromNow(aPins, -30, -20);
        var status = await Send(HttpMethod.Get, "/api/v1/work/target-status", a);
        Assert.True(status["hasTarget"]!.GetValue<bool>());
        Assert.False(status["reached"]!.GetValue<bool>());
        Assert.Equal(10, status["workMinutes"]!.GetValue<int>());
        Assert.Equal("10 دقائق", status["workedText"]!.GetValue<string>());
        Assert.Null(status["targetMinutes"]); // the target is never sent to the agent
        Assert.Null((await Send(HttpMethod.Get, "/api/v1/work/target-summary", a))["summary"]);

        // Ending the day now tells the dashboard.
        await Send(HttpMethod.Post, "/api/v1/work/start", a, new { });
        await Send(HttpMethod.Post, "/api/v1/work/end", a, new { });
        var early = (await Send(HttpMethod.Get, "/api/v1/admin/attendance/alerts/unseen", admin))["latest"]!.AsArray()
            .Single(x => x!["userId"]!.GetValue<long>() == aId)!;
        Assert.Equal("ClosedEarly", early["type"]!.GetValue<string>());
        Assert.Equal(10, early["workMinutes"]!.GetValue<int>());
        Assert.Equal(20, early["targetMinutes"]!.GetValue<int>());
        Assert.False(early["isOpen"]!.GetValue<bool>());
        Assert.Null(early["latitude"]);

        // B: 5 minutes of work, last pin 2 h 5 min ago: one «لم تحقق هدفك» notification with the time worked.
        var bPins = new List<long> { await Pin(b, 3), await Pin(b, 4) };
        await SetPinTimesFromNow(bPins, -130, -125);
        // A pins again 8 minutes ago: 22 minutes, target reached (found by the worker pass, the pin was just moved).
        aPins.Add(await Pin(a, 5));
        await SetPinTimesFromNow(aPins, -30, -20, -8);
        Assert.Equal(2, await Detect());
        Assert.Equal(0, await Detect()); // each is told once

        var idle = Assert.Single(await Inbox(b, "لم تحقق هدفك اليومي بعد"));
        Assert.Contains("5 دقائق", idle["body"]!.GetValue<string>());
        Assert.Empty(await Inbox(a, "لم تحقق هدفك اليومي بعد"));

        var reached = Assert.Single(await Inbox(a, "أحسنت! حققت هدفك اليومي"));
        Assert.DoesNotContain(reached["body"]!.GetValue<string>(), char.IsDigit); // no hours in the notification
        Assert.Equal("target_reached", reached["data"]!["kind"]!.GetValue<string>());

        // The summary waits until the agent closes it; it has the day's numbers and no hours.
        var summary = (await Send(HttpMethod.Get, "/api/v1/work/target-summary", a))["summary"]!;
        Assert.True(summary["isToday"]!.GetValue<bool>());
        Assert.Equal(3, summary["stores"]!.GetValue<int>());
        Assert.Equal(0, summary["orders"]!.GetValue<int>());
        Assert.Equal(0, summary["sales"]!.GetValue<int>());
        Assert.Null(summary["workMinutes"]);

        // Not opened until the next day: the same summary, dated yesterday.
        await using (var c = new SqlConnection(_fx.ConnectionString))
        {
            await c.OpenAsync();
            await using var cmd = new SqlCommand(
                "UPDATE agents.WorkTargetNotices SET [Day] = DATEADD(DAY, -1, [Day]) WHERE UserId = @u; " +
                "UPDATE agents.Prospects SET CreatedAt = DATEADD(DAY, -1, CreatedAt) WHERE CreatedByUserId = @u;", c);
            cmd.Parameters.AddWithValue("@u", aId);
            await cmd.ExecuteNonQueryAsync();
        }
        var yesterday = (await Send(HttpMethod.Get, "/api/v1/work/target-summary", a))["summary"]!;
        Assert.False(yesterday["isToday"]!.GetValue<bool>());
        Assert.True(yesterday["isYesterday"]!.GetValue<bool>());
        Assert.Equal(DateTime.UtcNow.AddHours(3).AddDays(-1).ToString("yyyy-MM-dd"), yesterday["date"]!.GetValue<string>());
        Assert.Equal(3, yesterday["stores"]!.GetValue<int>());

        await Send(HttpMethod.Post, $"/api/v1/work/target-summary/{yesterday["id"]!.GetValue<long>()}/seen", a, expect: HttpStatusCode.NoContent);
        Assert.Null((await Send(HttpMethod.Get, "/api/v1/work/target-summary", a))["summary"]);

        // A day ended after the target raises nothing.
        var (cId, cTok) = await CreateAgentAndLogin("notice.done", "07700000047", "ياسر");
        await Send(HttpMethod.Put, $"/api/v1/admin/agents/{cId}/target", admin, new { dailyWorkMinutesTarget = 5 });
        await SetPinTimesFromNow(new List<long> { await Pin(cTok, 6), await Pin(cTok, 7) }, -12, -2);
        Assert.True((await Send(HttpMethod.Get, "/api/v1/work/target-status", cTok))["reached"]!.GetValue<bool>());
        await Send(HttpMethod.Post, "/api/v1/work/start", cTok, new { });
        await Send(HttpMethod.Post, "/api/v1/work/end", cTok, new { });
        Assert.DoesNotContain((await Send(HttpMethod.Get, "/api/v1/admin/attendance/alerts", admin)).AsArray(),
            x => x!["userId"]!.GetValue<long>() == cId);
    }
}
