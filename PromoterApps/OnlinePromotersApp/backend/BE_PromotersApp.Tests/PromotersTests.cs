using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Collections.Concurrent;
using BE_PromotersApp.Integrations.Push;
using Microsoft.IdentityModel.Tokens;
using BE_PromotersApp.Domain.Orders;
using BE_PromotersApp.Domain.Users;
using BE_PromotersApp.Integrations.Gateway;

namespace BE_PromotersApp.Tests;

public class CommissionSplitTests
{
    [Fact]
    public void Parent_cut_comes_out_of_the_promoter_commission()
    {
        // The example from the request: 25,000 commission and a 60% cut → the sub-account sees 10,000, the creator gets 15,000.
        Assert.Equal((25_000, 10_000, 15_000), CommissionSplit.For(25_000, 1, 60m));
        Assert.Equal(10_000, CommissionSplit.SellerUnit(25_000, 60m));
    }

    [Theory]
    [InlineData(25_000, 0, 25_000)]
    [InlineData(25_000, 100, 0)]
    [InlineData(10_001, 50, 5_000)]   // parent's share rounds half away from zero (5,000.5 → 5,001)
    [InlineData(999, 33.33, 666)]
    public void Seller_share_per_unit_is_whole_dinars(long commission, double cut, long expected) =>
        Assert.Equal(expected, CommissionSplit.SellerUnit(commission, (decimal)cut));

    [Fact]
    public void Quantity_multiplies_the_unit_shares_so_both_add_up()
    {
        var (total, seller, parent) = CommissionSplit.For(10_001, 3, 50m);
        Assert.Equal(30_003, total);
        Assert.Equal(15_000, seller);
        Assert.Equal(15_003, parent);
    }

    [Theory]
    [InlineData("محمد جاسم علي", "محمد جاسم علي")]
    [InlineData("  عبد الله  حسين كاظم ", "عبد الله حسين كاظم")]
    [InlineData("علي عبد الله", null)]   // «عبد الله» is one name → only two names
    [InlineData("Ali Hassan Kadhim", null)]
    [InlineData("", null)]
    public void Customer_name_needs_three_arabic_names(string input, string? expected) => Assert.Equal(expected, CustomerName.Clean(input));

    [Fact]
    public void Name_errors_say_what_is_wrong_and_ignore_keyboard_marks()
    {
        // What happened on the test phone: no space between two names, and «عبد الحسن» is one name.
        Assert.Equal(CustomerName.TooShort, CustomerName.Check("محمدعلي عبد الحسن").Error);
        Assert.Equal(CustomerName.NotArabic, CustomerName.Check("محمد Ali حسن").Error);
        // Diacritics, tatweel and invisible marks from the keyboard are dropped.
        Assert.Equal("محمد علي حسن", CustomerName.Check("مُحَمَّد عـلي‏ حسن‌").Name);
    }

    [Fact]
    public void Usernames_ignore_case_diacritics_and_digit_style()
    {
        Assert.Equal("ali.hassan", Usernames.Normalize(" Ali.Hassan "));
        Assert.Equal("حيدر7", Usernames.Normalize("حَيـدر٧"));
        Assert.False(Usernames.IsValid("with space"));
        Assert.True(Usernames.IsValid("مروج_1"));
    }
}

/// <summary>The real API against a throw-away SQL Server database and the fake company gateway.</summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public const string GatewayKey = "test-gateway-jwt-key-0123456789-abcdefghijkl";
    private readonly string _server = Environment.GetEnvironmentVariable("CUSTOMERAPP_TEST_SQL") ?? @"Server=.\SQLEXPRESS;Trusted_Connection=True;TrustServerCertificate=True";
    private readonly string _database = "PromotersAppDb_Test_" + Guid.NewGuid().ToString("N")[..8];
    private WebApplicationFactory<Program>? _factory;

    public bool Available { get; private set; }
    public HttpClient Client { get; private set; } = null!;
    public IServiceProvider Services => _factory!.Services;
    public string ConnectionString => $"{_server};Database={_database}";
    public RecordingPushSender Push { get; } = new();

    public async Task InitializeAsync()
    {
        try
        {
            await using var c = new SqlConnection(_server + ";Connect Timeout=3");
            await c.OpenAsync();
            await new SqlCommand($"CREATE DATABASE [{_database}]", c).ExecuteNonQueryAsync();
            Available = true;
        }
        catch (SqlException)
        {
            return;
        }

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            foreach (var (k, v) in new Dictionary<string, string?>
            {
                ["ConnectionStrings:PromotersApp"] = ConnectionString,
                ["GatewayJwt:Key"] = GatewayKey,
                ["Gateway:UseFake"] = "true",
                ["Workers:Enabled"] = "false",
                ["Demo:Seed"] = "false",
                ["RateLimits:AuthPerMinute"] = "1000",
                ["RateLimits:OrdersPer10Minutes"] = "1000",
                ["Media:Root"] = Path.Combine(Path.GetTempPath(), _database),
                ["Receipts:Root"] = Path.Combine(Path.GetTempPath(), _database + "-receipts"),
            })
                b.UseSetting(k, v);
            b.ConfigureTestServices(s => s.Replace(ServiceDescriptor.Singleton<IPushSender>(Push)));
        });
        Client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null) await _factory.DisposeAsync();
        if (!Available) return;
        SqlConnection.ClearAllPools();
        await using var c = new SqlConnection(_server);
        await c.OpenAsync();
        await new SqlCommand($"ALTER DATABASE [{_database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_database}];", c).ExecuteNonQueryAsync();
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

public sealed class PromotersEndToEndTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _fx;
    private static readonly string Admin = ApiFixture.AdminToken();

    public PromotersEndToEndTests(ApiFixture fx) => _fx = fx;

    private async Task<JsonNode> Send(HttpMethod method, string url, string? token = null, object? body = null, HttpStatusCode expect = HttpStatusCode.OK, HttpContent? content = null)
    {
        using var req = new HttpRequestMessage(method, url);
        if (token is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (content is not null) req.Content = content;
        else if (body is not null) req.Content = JsonContent.Create(body);
        using var res = await _fx.Client.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expect, $"{method} {url} → {(int)res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : JsonNode.Parse(text)!;
    }

    private async Task<string> Login(string username, string password = "pass1234") =>
        (await Send(HttpMethod.Post, "/api/v1/auth/login", body: new { username, password }))["accessToken"]!.GetValue<string>();

    /// <summary>A 1×1 PNG, standing in for a photo of the transfer receipt.</summary>
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private static MultipartFormDataContent PaidForm((string Name, byte[] Bytes)? receipt, string? note)
    {
        var form = new MultipartFormDataContent();
        if (receipt is { } r) form.Add(new ByteArrayContent(r.Bytes), "receipt", r.Name);
        if (note is not null) form.Add(new StringContent(note), "note");
        return form;
    }

    private static MultipartFormDataContent FileForm(string name, byte[] bytes, string? body)
    {
        var form = new MultipartFormDataContent { { new ByteArrayContent(bytes), "file", name } };
        if (body is not null) form.Add(new StringContent(body), "body");
        return form;
    }

    private async Task<long> Balance(string token) => (await Send(HttpMethod.Get, "/api/v1/wallet", token))["balance"]!.GetValue<long>();

    private async Task SyncTimes(int n)
    {
        for (var i = 0; i < n; i++) await Send(HttpMethod.Post, "/api/v1/admin/orders/sync", Admin);
    }

    private async Task<long> NewProduct(string name, long price, long commission) =>
        (await Send(HttpMethod.Post, "/api/v1/admin/products", Admin, new { name, price, commission }, HttpStatusCode.Created))["id"]!.GetValue<long>();

    private async Task<JsonNode> NewOrder(string token, long productId, string phone, HttpStatusCode expect = HttpStatusCode.Created, int quantity = 1) =>
        await Send(HttpMethod.Post, "/api/v1/orders", token, new
        {
            productId, quantity, customerName = "حسين علي كاظم", customerPhone = phone, cityValue = 1, address = "النجف - حي الأمير قرب الجامع",
        }, expect);

    [Fact]
    public async Task Full_flow_commission_split_wallet_payout_chat_and_cancellation()
    {
        if (!_fx.Available) return;

        var productId = await NewProduct("ايفون 15 برو", 1_450_000, 25_000);

        // The dashboard creates a (full) account with a username, password and phone.
        var parent = await Send(HttpMethod.Post, "/api/v1/admin/users", Admin, new { username = "Father1", password = "pass1234", phone = "07701111111" }, HttpStatusCode.Created);
        Assert.True(parent["user"]!["isFull"]!.GetValue<bool>());
        var parentToken = await Login("father1");
        var me = await Send(HttpMethod.Get, "/api/v1/me", parentToken);
        Assert.True(me["canCreateAccounts"]!.GetValue<bool>());
        Assert.Equal(25_000, (await Send(HttpMethod.Get, "/api/v1/products", parentToken)).AsArray().Single(p => p!["id"]!.GetValue<long>() == productId)!["myCommission"]!.GetValue<long>());

        // The parent creates a sub-account keeping 60%.
        await Send(HttpMethod.Post, "/api/v1/me/team", parentToken, new { username = "daughter1", password = "pass1234", phone = "07702222222", cutPercent = 120 }, HttpStatusCode.BadRequest);
        var child = await Send(HttpMethod.Post, "/api/v1/me/team", parentToken, new { username = "daughter1", password = "pass1234", phone = "07702222222", cutPercent = 60 }, HttpStatusCode.Created);
        var childId = child["id"]!.GetValue<long>();
        await Send(HttpMethod.Post, "/api/v1/me/team", parentToken, new { username = "Daughter1", password = "x", phone = "07702222222", cutPercent = 10 }, HttpStatusCode.BadRequest);

        var childToken = await Login("daughter1");
        var childMe = await Send(HttpMethod.Get, "/api/v1/me", childToken);
        Assert.False(childMe["canCreateAccounts"]!.GetValue<bool>());
        // The sub-account is not told who created it or that a cut is taken.
        Assert.Null(childMe["parentUsername"]);
        Assert.Null(childMe["parentCutPercent"]);
        Assert.DoesNotContain("father1", childMe.ToJsonString());
        // The sub-account sees its commission after the parent's cut.
        Assert.Equal(10_000, (await Send(HttpMethod.Get, "/api/v1/products", childToken)).AsArray().Single(p => p!["id"]!.GetValue<long>() == productId)!["myCommission"]!.GetValue<long>());
        // …and cannot create accounts of its own.
        await Send(HttpMethod.Post, "/api/v1/me/team", childToken, new { username = "grand1", password = "p", phone = "07703333333", cutPercent = 10 }, HttpStatusCode.Forbidden);

        // Validation errors are per field.
        var bad = await Send(HttpMethod.Post, "/api/v1/orders", childToken, new { productId, customerName = "Ali", customerPhone = "123", cityValue = 99, address = "" }, HttpStatusCode.BadRequest);
        Assert.Equal(4, bad["fields"]!.AsObject().Count);

        // The order goes straight to the company system.
        var order = await NewOrder(childToken, productId, "07811111111");
        Assert.Equal("Transferred", order["status"]!.GetValue<string>());
        Assert.Equal(10_000, order["myCommission"]!.GetValue<long>());
        var orderId = order["id"]!.GetValue<long>();
        await NewOrder(childToken, productId, "07811111111", HttpStatusCode.Conflict);

        // Not sold yet → no commission.
        await SyncTimes(2);
        Assert.Equal("FieldInspection", (await Send(HttpMethod.Get, $"/api/v1/orders/{orderId}", childToken))["status"]!.GetValue<string>());
        Assert.Equal(0, await Balance(childToken));

        // Sold → the sub-account gets 10,000 and the parent 15,000.
        await SyncTimes(1);
        var sold = await Send(HttpMethod.Get, $"/api/v1/orders/{orderId}", childToken);
        Assert.Equal("Sold", sold["status"]!.GetValue<string>());
        Assert.Equal(10_000, await Balance(childToken));
        Assert.Equal(15_000, await Balance(parentToken));
        await SyncTimes(1); // idempotent
        Assert.Equal(10_000, await Balance(childToken));

        var team = await Send(HttpMethod.Get, "/api/v1/me/team", parentToken);
        var member = team["items"]!.AsArray().Single()!;
        Assert.Equal(15_000, member["earnedFromMember"]!.GetValue<long>());
        Assert.Equal(1, member["soldCount"]!.GetValue<int>());
        Assert.Equal(15_000, team["totalEarned"]!.GetValue<long>());
        var parentEntries = await Send(HttpMethod.Get, "/api/v1/wallet/entries", parentToken);
        Assert.Equal("Team", parentEntries["items"]![0]!["type"]!.GetValue<string>());
        Assert.Equal("daughter1", parentEntries["items"]![0]!["fromUsername"]!.GetValue<string>());

        // Withdrawal: not more than the balance, one open request at a time.
        await Send(HttpMethod.Post, "/api/v1/wallet/payouts", childToken, new { amount = 20_000, method = "ZainCash", accountNumber = "07702222222" }, HttpStatusCode.BadRequest);
        await Send(HttpMethod.Post, "/api/v1/wallet/payouts", childToken, new { amount = 5_000, method = "ZainCash", accountNumber = "12" }, HttpStatusCode.BadRequest);
        var payout = await Send(HttpMethod.Post, "/api/v1/wallet/payouts", childToken, new { amount = 5_000, method = "ZainCash", accountNumber = "0770 222 2222" }, HttpStatusCode.Created);
        var payoutId = payout["payout"]!["id"]!.GetValue<long>();
        Assert.Equal("07702222222", payout["payout"]!["accountNumber"]!.GetValue<string>());
        Assert.Equal(5_000, await Balance(childToken));
        await Send(HttpMethod.Post, "/api/v1/wallet/payouts", childToken, new { amount = 5_000, method = "QiCard", accountNumber = "1234567890" }, HttpStatusCode.Conflict);

        // Follow-up chat.
        await Send(HttpMethod.Post, $"/api/v1/wallet/payouts/{payoutId}/messages", childToken, new { body = "متى يصل المبلغ؟" });
        Assert.Equal(1, (await Send(HttpMethod.Get, "/api/v1/admin/payouts/counts", Admin))["unreadMessages"]!.GetValue<int>());
        var adminView = await Send(HttpMethod.Get, $"/api/v1/admin/payouts/{payoutId}", Admin);
        Assert.Equal(5_000, adminView["payout"]!["balance"]!.GetValue<long>());
        Assert.Equal(0, (await Send(HttpMethod.Get, "/api/v1/admin/payouts/counts", Admin))["unreadMessages"]!.GetValue<int>());
        var reply = await Send(HttpMethod.Post, $"/api/v1/admin/payouts/{payoutId}/messages", Admin, new { body = "خلال يوم عمل" });
        var newMessages = await Send(HttpMethod.Get, $"/api/v1/wallet/payouts/{payoutId}/messages?afterId=0", childToken);
        Assert.Equal(2, newMessages.AsArray().Count);
        Assert.True(newMessages[1]!["fromAdmin"]!.GetValue<bool>());
        Assert.Empty((await Send(HttpMethod.Get, $"/api/v1/wallet/payouts/{payoutId}/messages?afterId={reply["id"]}", childToken)).AsArray());

        // Photos and documents in the chat, both ways; private to the dashboard and the request's owner.
        var photoMsg = await Send(HttpMethod.Post, $"/api/v1/wallet/payouts/{payoutId}/messages/attachment", childToken,
            content: FileForm("صورة.png", TinyPng, null));
        Assert.Equal("image", photoMsg["attachmentKind"]!.GetValue<string>());
        Assert.Equal("", photoMsg["body"]!.GetValue<string>());
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n%test\n");
        var docMsg = await Send(HttpMethod.Post, $"/api/v1/admin/payouts/{payoutId}/messages/attachment", Admin,
            content: FileForm("كشف.pdf", pdf, "كشف الحساب"));
        Assert.Equal("file", docMsg["attachmentKind"]!.GetValue<string>());
        Assert.Equal("كشف.pdf", docMsg["attachmentName"]!.GetValue<string>());
        await Send(HttpMethod.Post, $"/api/v1/wallet/payouts/{payoutId}/messages/attachment", childToken,
            content: FileForm("virus.exe", Encoding.ASCII.GetBytes("MZ...."), null), expect: HttpStatusCode.BadRequest);
        await Send(HttpMethod.Post, $"/api/v1/wallet/payouts/{payoutId}/messages", childToken, new { body = "" }, HttpStatusCode.BadRequest);
        async Task<byte[]> Download(string url, string token, HttpStatusCode expect = HttpStatusCode.OK)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var res = await _fx.Client.SendAsync(req);
            Assert.Equal(expect, res.StatusCode);
            return await res.Content.ReadAsByteArrayAsync();
        }
        Assert.Equal(TinyPng, await Download($"/api/v1/admin/payouts/{payoutId}/messages/{photoMsg["id"]}/attachment", Admin));
        Assert.Equal(pdf, await Download($"/api/v1/wallet/payouts/{payoutId}/messages/{docMsg["id"]}/attachment", childToken));
        await Download($"/api/v1/wallet/payouts/{payoutId}/messages/{docMsg["id"]}/attachment", parentToken, HttpStatusCode.NotFound);
        var listed = await Send(HttpMethod.Get, $"/api/v1/wallet/payouts/{payoutId}", childToken);
        Assert.Contains(listed["messages"]!.AsArray(), m => m!["attachmentName"]?.GetValue<string>() == "كشف.pdf");

        // «تم التحويل» needs a picture of the transfer receipt.
        Assert.False(adminView["payout"]!["hasReceipt"]!.GetValue<bool>());
        await Send(HttpMethod.Post, $"/api/v1/admin/payouts/{payoutId}/paid", Admin, content: PaidForm(null, "بدون وصل"), expect: HttpStatusCode.BadRequest);
        await Send(HttpMethod.Post, $"/api/v1/admin/payouts/{payoutId}/paid", Admin,
            content: PaidForm(("receipt.pdf", Encoding.ASCII.GetBytes("%PDF-1.4 not an image")), null), expect: HttpStatusCode.BadRequest);
        Assert.Equal("Pending", (await Send(HttpMethod.Get, $"/api/v1/admin/payouts/{payoutId}", Admin))["payout"]!["status"]!.GetValue<string>());
        var paid = await Send(HttpMethod.Post, $"/api/v1/admin/payouts/{payoutId}/paid", Admin, content: PaidForm(("وصل.png", TinyPng), "رقم الحوالة 5566"));
        Assert.True(paid["payout"]!["hasReceipt"]!.GetValue<bool>());
        await Send(HttpMethod.Post, $"/api/v1/admin/payouts/{payoutId}/paid", Admin, content: PaidForm(("r.png", TinyPng), "x"), expect: HttpStatusCode.Conflict);

        // The receipt is not public: the dashboard and the promoter who asked can open it, nobody else.
        Assert.True((await Send(HttpMethod.Get, $"/api/v1/wallet/payouts/{payoutId}", childToken))["payout"]!["hasReceipt"]!.GetValue<bool>());
        foreach (var token in new[] { Admin, childToken })
        {
            var url = token == Admin ? $"/api/v1/admin/payouts/{payoutId}/receipt" : $"/api/v1/wallet/payouts/{payoutId}/receipt";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var res = await _fx.Client.SendAsync(req);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Equal("image/png", res.Content.Headers.ContentType!.MediaType);
            Assert.Equal(TinyPng, await res.Content.ReadAsByteArrayAsync());
        }
        await Send(HttpMethod.Get, $"/api/v1/wallet/payouts/{payoutId}/receipt", parentToken, expect: HttpStatusCode.NotFound);
        await Send(HttpMethod.Get, $"/api/v1/admin/payouts/{payoutId}/receipt", expect: HttpStatusCode.Unauthorized);
        Assert.Equal(5_000, await Balance(childToken));
        Assert.Equal(5_000, (await Send(HttpMethod.Get, "/api/v1/wallet", childToken))["totalWithdrawn"]!.GetValue<long>());

        // Qi Card: digits only, 10 to 13 of them.
        foreach (var badCard in new[] { "123456789", "12345678901234", "12345abcde" })
        {
            var err = await Send(HttpMethod.Post, "/api/v1/wallet/payouts", childToken, new { amount = 5_000, method = "QiCard", accountNumber = badCard }, HttpStatusCode.BadRequest);
            Assert.Contains("10 إلى 13", err["fields"]!["accountNumber"]!.GetValue<string>());
        }

        // A rejected request returns its amount.
        var second = await Send(HttpMethod.Post, "/api/v1/wallet/payouts", childToken, new { amount = 5_000, method = "QiCard", accountNumber = "1234-5678-90" }, HttpStatusCode.Created);
        Assert.Equal("1234567890", second["payout"]!["accountNumber"]!.GetValue<string>());
        Assert.Equal(0, await Balance(childToken));
        await Send(HttpMethod.Post, $"/api/v1/admin/payouts/{second["payout"]!["id"]}/reject", Admin, new { note = "" }, HttpStatusCode.BadRequest);
        await Send(HttpMethod.Post, $"/api/v1/admin/payouts/{second["payout"]!["id"]}/reject", Admin, new { note = "رقم الحساب غير صحيح" });
        Assert.Equal(5_000, await Balance(childToken));

        // The company cancels the sale later: both commissions are taken back.
        var uuid = await OrderUuid(orderId);
        ((FakeGatewayClient)_fx.Services.GetRequiredService<IGatewayClient>()).Reject(uuid, "الزبون تراجع عن الشراء");
        await SyncTimes(1);
        var cancelled = await Send(HttpMethod.Get, $"/api/v1/orders/{orderId}", childToken);
        Assert.Equal("NotSold", cancelled["status"]!.GetValue<string>());
        Assert.Contains("الزبون تراجع", cancelled["publicReason"]!.GetValue<string>());
        Assert.Equal(-5_000, await Balance(childToken));
        Assert.Equal(0, await Balance(parentToken));
        await SyncTimes(1);
        Assert.Equal(0, await Balance(parentToken));

        // Every change is in the record, newest first.
        var childEntries = (await Send(HttpMethod.Get, "/api/v1/wallet/entries", childToken))["items"]!.AsArray().Select(e => e!["type"]!.GetValue<string>()).ToList();
        Assert.Equal(["SaleReversal", "PayoutReturned", "Payout", "Payout", "Sale"], childEntries);

        // Notifications reached both accounts.
        var notes = await Send(HttpMethod.Get, "/api/v1/notifications", childToken);
        Assert.True(notes["unreadCount"]!.GetValue<int>() >= 4);
    }

    [Fact]
    public async Task Dashboard_can_make_a_sub_account_full_and_change_the_cut()
    {
        if (!_fx.Available) return;
        var productId = await NewProduct("كرسي مساج", 900_000, 40_000);
        await Send(HttpMethod.Post, "/api/v1/admin/users", Admin, new { username = "boss2", password = "pass1234", phone = "07704444444" }, HttpStatusCode.Created);
        var bossToken = await Login("boss2");
        var sub = await Send(HttpMethod.Post, "/api/v1/me/team", bossToken, new { username = "sub2", password = "pass1234", phone = "07705555555", cutPercent = 25 }, HttpStatusCode.Created);
        var subId = sub["id"]!.GetValue<long>();
        var subToken = await Login("sub2");
        long MyCommission(JsonNode products) => products.AsArray().Single(p => p!["id"]!.GetValue<long>() == productId)!["myCommission"]!.GetValue<long>();
        Assert.Equal(30_000, MyCommission(await Send(HttpMethod.Get, "/api/v1/products", subToken)));

        // The creator lowers the cut; the dashboard can too.
        await Send(HttpMethod.Put, $"/api/v1/me/team/{subId}", bossToken, new { cutPercent = 50 });
        Assert.Equal(20_000, MyCommission(await Send(HttpMethod.Get, "/api/v1/products", subToken)));
        await Send(HttpMethod.Post, $"/api/v1/admin/users/{subId}/cut", Admin, new { cutPercent = 10 });
        Assert.Equal(36_000, MyCommission(await Send(HttpMethod.Get, "/api/v1/products", subToken)));

        // An order keeps the split it was made with.
        var order = await NewOrder(subToken, productId, "07812222222", quantity: 2);
        Assert.Equal(72_000, order["myCommission"]!.GetValue<long>());

        // Full account: 100% and may create its own sub-accounts.
        await Send(HttpMethod.Post, $"/api/v1/admin/users/{subId}/full", Admin, new { isFull = true });
        Assert.Equal(40_000, MyCommission(await Send(HttpMethod.Get, "/api/v1/products", subToken)));
        await Send(HttpMethod.Post, "/api/v1/me/team", subToken, new { username = "grand2", password = "pass1234", phone = "07706666666", cutPercent = 30 }, HttpStatusCode.Created);
        await Send(HttpMethod.Put, $"/api/v1/me/team/{subId}", bossToken, new { cutPercent = 20 }, HttpStatusCode.Conflict);

        await SyncTimes(3);
        // Made before the account became full → still split 90/10.
        Assert.Equal(72_000, await Balance(subToken));
        Assert.Equal(8_000, await Balance(bossToken));

        var detail = await Send(HttpMethod.Get, $"/api/v1/admin/users/{subId}", Admin);
        Assert.Equal("boss2", detail["user"]!["parentUsername"]!.GetValue<string>());
        Assert.Single(detail["team"]!.AsArray());

        // Manual adjustment from the dashboard.
        await Send(HttpMethod.Post, $"/api/v1/admin/users/{subId}/wallet/adjust", Admin, new { amount = -2_000, note = "تصحيح" });
        Assert.Equal(70_000, await Balance(subToken));

        // Suspending signs out and blocks sign-in.
        await Send(HttpMethod.Post, $"/api/v1/admin/users/{subId}/suspend", Admin, new { suspended = true, reason = "مراجعة" });
        await Send(HttpMethod.Post, "/api/v1/auth/login", body: new { username = "sub2", password = "pass1234" }, expect: HttpStatusCode.Forbidden);
        await Send(HttpMethod.Get, "/api/v1/products", subToken, expect: HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Products_and_materials_from_the_dashboard_reach_the_app()
    {
        if (!_fx.Available) return;
        await Send(HttpMethod.Post, "/api/v1/admin/users", Admin, new { username = "mat3", password = "pass1234", phone = "07707777777" }, HttpStatusCode.Created);
        var token = await Login("mat3");

        var hidden = await NewProduct("منتج مخفي", 100_000, 5_000);
        await Send(HttpMethod.Put, $"/api/v1/admin/products/{hidden}", Admin, new { name = "منتج مخفي", price = 100_000, commission = 5_000, isActive = false });
        Assert.DoesNotContain((await Send(HttpMethod.Get, "/api/v1/products", token)).AsArray(), p => p!["id"]!.GetValue<long>() == hidden);
        await NewOrder(token, hidden, "07813333333", HttpStatusCode.BadRequest);

        var pdf = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4\n%test\n"));
        pdf.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        var form = new MultipartFormDataContent { { pdf, "file", "عرض الأسعار.pdf" }, { new StringContent("عرض نوفمبر"), "title" } };
        var created = await Send(HttpMethod.Post, "/api/v1/admin/materials", Admin, content: form, expect: HttpStatusCode.Created);
        Assert.Equal("pdf", created["kind"]!.GetValue<string>());

        var fake = new ByteArrayContent(Encoding.ASCII.GetBytes("not an image"));
        var badForm = new MultipartFormDataContent { { fake, "file", "photo.jpg" } };
        await Send(HttpMethod.Post, "/api/v1/admin/materials", Admin, content: badForm, expect: HttpStatusCode.BadRequest);

        var list = await Send(HttpMethod.Get, "/api/v1/materials", token);
        var item = list.AsArray().Single(m => m!["id"]!.GetValue<long>() == created["id"]!.GetValue<long>())!;
        Assert.Equal("عرض نوفمبر", item["title"]!.GetValue<string>());
        var path = new Uri(item["url"]!.GetValue<string>()).AbsolutePath;
        using var file = await _fx.Client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.StartsWith("%PDF", await file.Content.ReadAsStringAsync());

        await Send(HttpMethod.Delete, $"/api/v1/admin/materials/{created["id"]}", Admin, expect: HttpStatusCode.NoContent);
        Assert.DoesNotContain((await Send(HttpMethod.Get, "/api/v1/materials", token)).AsArray(), m => m!["id"]!.GetValue<long>() == created["id"]!.GetValue<long>());
    }

    [Fact]
    public async Task Dashboard_api_is_for_director_and_monitor_only()
    {
        if (!_fx.Available) return;
        await Send(HttpMethod.Get, "/api/v1/admin/users", ApiFixture.AdminToken("مدير مبيعات"), expect: HttpStatusCode.Forbidden);
        await Send(HttpMethod.Get, "/api/v1/admin/users", ApiFixture.AdminToken("المراقب"));
        await Send(HttpMethod.Get, "/api/v1/admin/users", ApiFixture.AdminToken("مسؤول المراقبين"));
        await Send(HttpMethod.Get, "/api/v1/admin/users", ApiFixture.AdminToken("مسؤول لجنة المراقبة"), expect: HttpStatusCode.Forbidden);
        await Send(HttpMethod.Get, "/api/v1/admin/users", expect: HttpStatusCode.Unauthorized);
        await Send(HttpMethod.Post, "/api/v1/admin/users", Admin, new { username = "app4", password = "pass1234", phone = "07708888888" }, HttpStatusCode.Created);
        var appToken = await Login("app4");
        await Send(HttpMethod.Get, "/api/v1/admin/users", appToken, expect: HttpStatusCode.Unauthorized);
        await Send(HttpMethod.Post, "/api/v1/auth/login", body: new { username = "app4", password = "wrong" }, expect: HttpStatusCode.Unauthorized);
        var overview = await Send(HttpMethod.Get, "/api/v1/admin/overview", Admin);
        Assert.True(overview["users"]!.GetValue<int>() >= 1);
    }

    private async Task<Guid> OrderUuid(long orderId)
    {
        await using var c = new SqlConnection(_fx.ConnectionString);
        return await c.ExecuteScalarAsync<Guid>("SELECT OrderUuid FROM promoters.Orders WHERE Id = @orderId", new { orderId });
    }
}

/// <summary>Stands in for Firebase: records every push; the token "dead-token" is rejected like an uninstalled app.</summary>
public sealed class RecordingPushSender : IPushSender
{
    public ConcurrentQueue<(string Token, PushMessage Message)> Sent { get; } = new();
    public bool Enabled => true;

    public Task<PushResult> SendAsync(string deviceToken, PushMessage message, CancellationToken ct)
    {
        Sent.Enqueue((deviceToken, message));
        return Task.FromResult(deviceToken == "dead-token" ? PushResult.InvalidToken : PushResult.Sent);
    }
}

public sealed class PushTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _fx;
    public PushTests(ApiFixture fx) => _fx = fx;

    [Fact]
    public async Task Sale_and_chat_reply_reach_the_registered_phone()
    {
        if (!_fx.Available) return;
        var admin = ApiFixture.AdminToken();
        async Task<JsonNode> Send(HttpMethod m, string url, string? token, object? body, HttpStatusCode expect = HttpStatusCode.OK)
        {
            using var req = new HttpRequestMessage(m, url);
            if (token is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body is not null) req.Content = JsonContent.Create(body);
            using var res = await _fx.Client.SendAsync(req);
            var text = await res.Content.ReadAsStringAsync();
            Assert.True(res.StatusCode == expect, $"{m} {url} → {(int)res.StatusCode}: {text}");
            return string.IsNullOrEmpty(text) ? new JsonObject() : JsonNode.Parse(text)!;
        }
        async Task<bool> Eventually(Func<bool> check)
        {
            for (var i = 0; i < 50 && !check(); i++) await Task.Delay(100);
            return check();
        }

        var product = await Send(HttpMethod.Post, "/api/v1/admin/products", admin, new { name = "شاشة 55", price = 600_000, commission = 20_000 }, HttpStatusCode.Created);
        await Send(HttpMethod.Post, "/api/v1/admin/users", admin, new { username = "push5", password = "pass1234", phone = "07709999999" }, HttpStatusCode.Created);
        // The phone token comes with the sign-in.
        var login = await Send(HttpMethod.Post, "/api/v1/auth/login", null, new { username = "push5", password = "pass1234", device = new { platform = "android", pushToken = "phone-token-1", appVersion = "1.0.1" } });
        var token = login["accessToken"]!.GetValue<string>();
        await Send(HttpMethod.Post, "/api/v1/me/devices", token, new { platform = "android", pushToken = "dead-token" }, HttpStatusCode.NoContent);

        await Send(HttpMethod.Post, "/api/v1/orders", token, new
        {
            productId = product["id"]!.GetValue<long>(), quantity = 1, customerName = "علي حسن كاظم", customerPhone = "07814444444", cityValue = 1, address = "النجف - الحنانة",
        }, HttpStatusCode.Created);
        for (var i = 0; i < 3; i++) await Send(HttpMethod.Post, "/api/v1/admin/orders/sync", admin, null);

        Assert.True(await Eventually(() => _fx.Push.Sent.Any(p => p.Token == "phone-token-1" && p.Message.Title.StartsWith("تم البيع"))));
        var sold = _fx.Push.Sent.First(p => p.Token == "phone-token-1" && p.Message.Title.StartsWith("تم البيع"));
        Assert.Equal("Order", sold.Message.Data["type"]);
        Assert.True(sold.Message.Data.ContainsKey("orderId"));

        // Firebase rejected the dead token → it is removed, so the next push only goes to the live phone.
        await using (var c = new SqlConnection(_fx.ConnectionString))
            Assert.True(await Eventually(() => c.ExecuteScalar<int>("SELECT COUNT(*) FROM promoters.Devices WHERE Token = 'dead-token'") == 0));

        // A dashboard reply in the withdrawal chat is pushed too.
        var payout = await Send(HttpMethod.Post, "/api/v1/wallet/payouts", token, new { amount = 20_000, method = "ZainCash", accountNumber = "07709999999" }, HttpStatusCode.Created);
        var before = _fx.Push.Sent.Count;
        await Send(HttpMethod.Post, $"/api/v1/admin/payouts/{payout["payout"]!["id"]}/messages", admin, new { body = "تم استلام طلبك" });
        Assert.True(await Eventually(() => _fx.Push.Sent.Skip(before).Any(p => p.Token == "phone-token-1" && p.Message.Data.ContainsKey("payoutId"))));
        Assert.DoesNotContain(_fx.Push.Sent.Skip(before), p => p.Token == "dead-token");

        // Logging out with the token stops pushes to that phone.
        await Send(HttpMethod.Post, "/api/v1/auth/logout", token, new { refreshToken = login["refreshToken"]!.GetValue<string>(), pushToken = "phone-token-1" }, HttpStatusCode.NoContent);
        await using (var c = new SqlConnection(_fx.ConnectionString))
            Assert.Equal(0, await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM promoters.Devices WHERE Token = 'phone-token-1'"));
    }
}
