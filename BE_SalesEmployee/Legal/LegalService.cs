using System.Text.Json;
using BE_SalesEmployee.Services;

namespace BE_SalesEmployee.Legal;

public sealed class BranchCustomer
{
    public int CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string? ShopName { get; set; }
    public int? DelegateId { get; set; }
    public string? DelegateName { get; set; }
    public bool IsLegal { get; set; }
    public long Total { get; set; }
    public long Paid { get; set; }
    public long Remaining { get; set; }
    public DateTime? LastPaymentDate { get; set; }
    public DateTime? SaleDate { get; set; }
    public string? ItemsNames { get; set; }
}

/// <summary>Trusted calls into a branch's api/legal-branch (gateway key + legal-viewer stamp).</summary>
public sealed class LegalBranchClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(40);

    private readonly AdminCitiesService _cities;
    private readonly BranchProxyService _proxy;
    private readonly IConfiguration _configuration;

    public LegalBranchClient(AdminCitiesService cities, BranchProxyService proxy, IConfiguration configuration)
    {
        _cities = cities;
        _proxy = proxy;
        _configuration = configuration;
    }

    /// <summary>
    /// The Aqeel branches (الرصافة عقيل، الكرخ عقيل، ميسان، الكوت) are not part of the legal system.
    /// Matched on the branch name or database name; override with Legal:ExcludedBranches.
    /// </summary>
    private static readonly string[] DefaultExcluded = ["عقيل", "ميسان", "الكوت", "Aqeel", "Maysan", "Kot"];

    public async Task<IReadOnlyList<AdminCity>> BranchesAsync(CancellationToken ct)
    {
        var excluded = _configuration.GetSection("Legal:ExcludedBranches").Get<string[]>() ?? DefaultExcluded;
        bool Out(AdminCity city) => excluded.Any(x => !string.IsNullOrWhiteSpace(x)
            && ((city.Name ?? "").Contains(x.Trim(), StringComparison.OrdinalIgnoreCase)
                || (city.Database ?? "").Contains(x.Trim(), StringComparison.OrdinalIgnoreCase)));
        return (await _cities.GetSalesBranchesAsync(ct)).Where(c => !Out(c)).ToList();
    }

    public async Task<AdminCity> CityAsync(string? cityValue, CancellationToken ct)
    {
        var all = await BranchesAsync(ct);
        return all.FirstOrDefault(c => string.Equals(c.Value, cityValue?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new LegalRuleException("الفرع غير موجود", 404);
    }

    public async Task<(int Status, JsonElement? Body)> SendAsync(
        string cityValue, string path, HttpMethod method, object? body, string actorName, CancellationToken ct)
    {
        var city = await CityAsync(cityValue, ct);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(Timeout);
        try
        {
            using var response = await _proxy.SendManagerAsync(
                city.Link, $"legal-branch/{path}", method,
                body is null ? null : JsonSerializer.Serialize(body, Json), actorName, cts.Token);
            var raw = await response.Content.ReadAsStringAsync(ct);
            return ((int)response.StatusCode, BranchProxyService.TryParseJson(raw));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return (504, null);
        }
        catch (HttpRequestException)
        {
            return (502, null);
        }
    }

    public async Task<T> GetAsync<T>(string cityValue, string path, string actorName, CancellationToken ct)
    {
        var (status, body) = await SendAsync(cityValue, path, HttpMethod.Get, null, actorName, ct);
        return Read<T>(status, body);
    }

    public async Task<T> PostAsync<T>(string cityValue, string path, object body, string actorName, CancellationToken ct)
    {
        var (status, json) = await SendAsync(cityValue, path, HttpMethod.Post, body, actorName, ct);
        return Read<T>(status, json);
    }

    public static string? Message(JsonElement? body)
    {
        if (body is { ValueKind: JsonValueKind.Object } obj && obj.TryGetProperty("message", out var message))
            return message.GetString();
        return body is { ValueKind: JsonValueKind.String } text ? text.GetString() : null;
    }

    private static T Read<T>(int status, JsonElement? body)
    {
        if (status is < 200 or >= 300)
        {
            var message = Message(body) ?? (status is 502 or 504 ? "تعذر الاتصال بالفرع" : $"خطأ من الفرع ({status})");
            throw new LegalRuleException(message, status is 502 or 504 or >= 500 ? 502 : status);
        }

        if (body is null)
            throw new LegalRuleException("رد فارغ من الفرع", 502);
        return body.Value.Deserialize<T>(Json) ?? throw new LegalRuleException("رد غير مفهوم من الفرع", 502);
    }
}

/// <summary>Legal-desk operations that need both the legal database and live branch data.</summary>
public sealed class LegalService
{
    private readonly LegalStore _store;
    private readonly LegalBranchClient _branches;
    private readonly IConfiguration _configuration;

    public LegalService(LegalStore store, LegalBranchClient branches, IConfiguration configuration)
    {
        _store = store;
        _branches = branches;
        _configuration = configuration;
    }

    public async Task<object> CitiesAsync(CancellationToken ct) =>
        (await _branches.BranchesAsync(ct)).Select(c => new { value = c.Value, name = c.Name });

    /// <summary>Branch customers, each stamped with the grouped debtor it already belongs to (if any).</summary>
    public async Task<object> CustomersAsync(string cityValue, int? delegateId, string? q, string? scope, string actor, CancellationToken ct)
    {
        var city = await _branches.CityAsync(cityValue, ct);
        var query = $"customers?scope={Uri.EscapeDataString(scope ?? "legal")}&take=5000"
            + (delegateId is int id ? $"&delegateId={id}" : "")
            + (string.IsNullOrWhiteSpace(q) ? "" : $"&q={Uri.EscapeDataString(q.Trim())}");
        var rows = await _branches.GetAsync<List<BranchCustomer>>(city.Value, query, actor, ct);
        return await StampAsync(city, rows, ct);
    }

    public async Task<object> SimilarAsync(string cityValue, int customerId, string actor, CancellationToken ct)
    {
        var city = await _branches.CityAsync(cityValue, ct);
        var rows = await _branches.GetAsync<List<BranchCustomer>>(city.Value, $"customers/{customerId}/similar", actor, ct);
        return await StampAsync(city, rows, ct);
    }

    /// <summary>
    /// The same person often has a customer row per sale, sometimes in another branch.
    /// Searches every branch by name or phone; a branch that fails is reported, not fatal.
    /// </summary>
    public async Task<object> SearchAllAsync(string q, string actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 3)
            throw new LegalRuleException("اكتب ثلاثة أحرف على الأقل", 400);
        var cities = await _branches.BranchesAsync(ct);
        var path = $"customers?scope=all&take=200&q={Uri.EscapeDataString(q.Trim())}";
        var results = await Task.WhenAll(cities.Select(async city =>
        {
            try
            {
                var rows = await _branches.GetAsync<List<BranchCustomer>>(city.Value, path, actor, ct);
                return (city, rows: (List<BranchCustomer>?)rows);
            }
            catch (LegalRuleException)
            {
                return (city, rows: (List<BranchCustomer>?)null);
            }
        }));
        var stamped = new List<object>();
        foreach (var (city, rows) in results)
        {
            if (rows is { Count: > 0 })
                stamped.AddRange((IEnumerable<object>)await StampAsync(city, rows, ct));
        }

        return new
        {
            rows = stamped,
            failedBranches = results.Where(r => r.rows is null).Select(r => r.city.Name).ToList(),
        };
    }

    private async Task<object> StampAsync(AdminCity city, List<BranchCustomer> rows, CancellationToken ct)
    {
        var linked = await _store.LinkedCustomersAsync(city.Value, ct);
        return rows.Select(r =>
        {
            var has = linked.TryGetValue(r.CustomerId, out var link);
            return (object)new
            {
                cityValue = city.Value,
                cityName = city.Name,
                customerId = r.CustomerId,
                customerName = r.CustomerName,
                phone = r.PhoneNumber,
                address = r.Address,
                shopName = r.ShopName,
                delegateId = r.DelegateId,
                listName = r.DelegateName,
                isLegal = r.IsLegal,
                total = r.Total,
                paid = r.Paid,
                remaining = r.Remaining,
                lastPaymentDate = r.LastPaymentDate,
                saleDate = r.SaleDate,
                items = r.ItemsNames,
                personId = has ? link.PersonId : (int?)null,
                personName = has ? link.PersonName : null,
                personStage = has ? link.Stage : null,
                archiveCode = has ? link.ArchiveCode : null,
            };
        }).ToList();
    }

    public sealed record AccountRef(string CityValue, int CustomerId);

    /// <summary>Reads the chosen accounts from their branches so the group starts with real names and balances.</summary>
    public async Task<List<LegalStore.NewAccount>> ResolveAccountsAsync(IReadOnlyList<AccountRef> refs, string actor, CancellationToken ct)
    {
        var result = new List<LegalStore.NewAccount>();
        foreach (var group in refs.Where(r => r.CustomerId > 0 && !string.IsNullOrWhiteSpace(r.CityValue))
                     .GroupBy(r => r.CityValue.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            var city = await _branches.CityAsync(group.Key, ct);
            var ids = group.Select(r => r.CustomerId).Distinct().ToList();
            var rows = await _branches.PostAsync<List<BranchCustomer>>(city.Value, "customers/by-ids", new { ids }, actor, ct);
            foreach (var id in ids)
            {
                var row = rows.FirstOrDefault(r => r.CustomerId == id)
                    ?? throw new LegalRuleException($"الحساب {id} غير موجود في فرع {city.Name}", 404);
                result.Add(new LegalStore.NewAccount(city.Value, city.Name, id, row.CustomerName, row.DelegateName, row.Total, row.Paid, row.Remaining));
            }
        }

        return result;
    }

    /// <summary>
    /// Refreshes balances from the branches. A branch that cannot be reached keeps its last
    /// snapshot and is named in <c>staleCities</c> so the screen can say the figure may be old.
    /// </summary>
    public async Task<(List<object> Accounts, List<string> StaleCities)> LiveAccountsAsync(
        IReadOnlyList<LegalAccountRow> accounts, string actor, CancellationToken ct)
    {
        var live = new Dictionary<int, BranchCustomer>();
        var stale = new List<string>();
        await Task.WhenAll(accounts.GroupBy(a => a.CityValue, StringComparer.OrdinalIgnoreCase).Select(async group =>
        {
            try
            {
                var rows = await _branches.PostAsync<List<BranchCustomer>>(
                    group.Key, "customers/by-ids", new { ids = group.Select(a => a.CustomerId).ToList() }, actor, ct);
                lock (live)
                {
                    foreach (var account in group)
                    {
                        var row = rows.FirstOrDefault(r => r.CustomerId == account.CustomerId);
                        if (row != null)
                            live[account.Id] = row;
                    }
                }
            }
            catch (LegalRuleException)
            {
                lock (stale)
                    stale.Add(group.First().CityName ?? group.Key);
            }
        }));

        // The branch only knows what was paid before the customer became legal;
        // payments taken since then live in القانونية and are applied on top.
        var legalPaid = await _store.LegalPaidAsync(ct);
        foreach (var account in accounts)
        {
            if (live.TryGetValue(account.Id, out var row)
                && legalPaid.TryGetValue((account.CityValue.ToLowerInvariant(), account.CustomerId), out var paid))
            {
                row.Paid += paid;
                row.Remaining = Math.Max(0, row.Remaining - paid);
            }
        }

        await _store.SaveSnapshotsAsync(
            live.Select(kv => (kv.Key, kv.Value.Total, kv.Value.Paid, kv.Value.Remaining, kv.Value.CustomerName, kv.Value.DelegateName)), ct);

        var shaped = accounts.Select(a =>
        {
            var has = live.TryGetValue(a.Id, out var row);
            return (object)new
            {
                id = a.Id,
                cityValue = a.CityValue,
                cityName = a.CityName,
                customerId = a.CustomerId,
                customerName = has ? row!.CustomerName : a.CustomerName,
                listName = has ? row!.DelegateName : a.ListName,
                phone = has ? row!.PhoneNumber : null,
                address = has ? row!.Address : null,
                items = has ? row!.ItemsNames : null,
                saleDate = has ? row!.SaleDate : null,
                lastPaymentDate = has ? row!.LastPaymentDate : null,
                total = has ? row!.Total : a.LastTotal,
                paid = has ? row!.Paid : a.LastPaid,
                remaining = has ? row!.Remaining : a.LastRemaining,
                archiveCode = a.ArchiveCode,
                isLive = has,
            };
        }).ToList();
        return (shaped, stale);
    }

    /// <summary>Everything about one debtor: accounts with live balances, penalties, payments, case.</summary>
    public async Task<object?> PersonDetailAsync(int personId, string actor, CancellationToken ct)
    {
        var person = await _store.PersonAsync(personId, ct);
        if (person is null)
            return null;
        var accounts = await _store.AccountsAsync(personId, ct);
        var (live, stale) = await LiveAccountsAsync(accounts, actor, ct);
        var money = await _store.MoneyAsync(personId, ct);
        var penaltiesDue = await _store.PenaltiesDueAsync(personId, ct);
        var remaining = live.Sum(a => (long)((dynamic)a).remaining);
        int? caseId = person.caseId;
        var legalCase = caseId is int id ? await _store.CaseAsync(id, ct) : null;
        return new
        {
            person,
            accounts = live,
            staleCities = stale,
            legalCase,
            money.charges,
            money.payments,
            money.requests,
            totals = new
            {
                remaining,
                penaltiesDue,
                totalDue = remaining + penaltiesDue,
            },
        };
    }

    /// <summary>
    /// A تسديد by the legal accountant. The debtor has left his branch, so the whole amount is
    /// recorded in القانونية (debt lines + penalty part); nothing is posted to a branch.
    /// It gets a receipt number. When nothing is left to pay the open file is closed.
    /// </summary>
    public Task<object> PayAsync(int personId, IReadOnlyList<LegalStore.PayLine> lines, long penaltyAmount, string? note,
        string actor, CancellationToken ct) =>
        TakeAsync(personId, lines, penaltyAmount, note, actor, LegalStore.PaymentKinds.Payment, ct);

    /// <summary>
    /// A خصم: one amount taken off what the debtor owes, against the debt first (oldest account
    /// first) and then against the legal penalties. Never more than everything he owes. No receipt.
    /// </summary>
    public async Task<object> DiscountAsync(int personId, long amount, string? note, string actor, CancellationToken ct)
    {
        if (amount <= 0)
            throw new LegalRuleException("أدخل مبلغ الخصم", 400);
        var accounts = await _store.AccountsAsync(personId, ct);
        if (accounts.Count == 0)
            throw new LegalRuleException("المدين غير موجود", 404);
        await LiveAccountsAsync(accounts, actor, ct);
        accounts = await _store.AccountsAsync(personId, ct);
        var left = amount;
        var lines = new List<LegalStore.PayLine>();
        foreach (var account in accounts.Where(a => a.LastRemaining > 0))
        {
            if (left <= 0)
                break;
            var part = Math.Min(left, account.LastRemaining);
            lines.Add(new LegalStore.PayLine(account.Id, part));
            left -= part;
        }

        var penaltiesDue = await _store.PenaltiesDueAsync(personId, ct);
        if (left > penaltiesDue)
        {
            var due = accounts.Sum(a => Math.Max(0, a.LastRemaining)) + Math.Max(0, penaltiesDue);
            throw new LegalRuleException($"الخصم ({amount:N0}) أكبر من كل المطلوب من المدين ({due:N0})");
        }

        return await TakeAsync(personId, lines, left, note, actor, LegalStore.PaymentKinds.Discount, ct);
    }

    private async Task<object> TakeAsync(int personId, IReadOnlyList<LegalStore.PayLine> lines, long penaltyAmount, string? note,
        string actor, string kind, CancellationToken ct)
    {
        var wanted = lines.Where(l => l.Amount != 0).ToList();
        if (wanted.Any(l => l.Amount < 0) || penaltyAmount < 0)
            throw new LegalRuleException("المبلغ غير صحيح", 400);
        if (wanted.Count == 0 && penaltyAmount == 0)
            throw new LegalRuleException("أدخل المبلغ", 400);

        var accounts = await _store.AccountsAsync(personId, ct);
        if (accounts.Count == 0)
            throw new LegalRuleException("المدين غير موجود", 404);
        // Refresh balances first so the amounts are checked against current figures.
        var (_, stale) = await LiveAccountsAsync(accounts, actor, ct);

        var record = await _store.RecordPaymentAsync(personId, wanted, penaltyAmount, note, actor, ct, kind);

        var remaining = (await _store.AccountsAsync(personId, ct)).Sum(a => a.LastRemaining);
        var penaltiesLeft = await _store.PenaltiesDueAsync(personId, ct);
        var closed = false;
        if (stale.Count == 0 && remaining <= 0 && penaltiesLeft <= 0
            && await _store.OpenCaseIdAsync(personId, ct) is int openCase)
        {
            await _store.CloseAsync(openCase, kind == LegalStore.PaymentKinds.Discount ? "خصم كامل المتبقي" : "تسديد كامل", actor, ct);
            closed = true;
        }

        return new
        {
            paymentId = record.Id,
            receiptNumber = record.ReceiptNumber,
            kind,
            ok = true,
            debtPaid = wanted.Sum(l => l.Amount),
            penaltyTaken = penaltyAmount,
            remaining,
            penaltiesDue = penaltiesLeft,
            closed,
        };
    }

    // ───────────────────────── كود ارشيف القانونية ─────────────────────────

    /// <summary>Two-letter branch code. Legal:ArchiveBranchCodes:{cityValue} overrides the name map.</summary>
    private static readonly (string Name, string Code)[] BranchCodes =
    [
        // The live branch list calls بابل «الحلة» and السماوة «المثنى».
        ("الكرخ", "BK"), ("الرصافة", "BR"), ("بابل", "BB"), ("الحلة", "BB"), ("البصرة", "BS"), ("المثنى", "SM"), ("الديوانية", "DW"), ("ديالى", "DY"),
        ("كربلاء", "KB"), ("كركوك", "KR"), ("الموصل", "MS"), ("النجف", "NJ"), ("الناصرية", "NS"), ("السماوة", "SM"),
        ("تجريبي", "QA"),
    ];

    private string? ArchiveBranchCode(string cityValue, string? cityName)
    {
        var configured = _configuration[$"Legal:ArchiveBranchCodes:{cityValue}"];
        if (!string.IsNullOrWhiteSpace(configured))
            return configured.Trim().ToUpperInvariant();
        var name = cityName ?? "";
        foreach (var (key, code) in BranchCodes)
        {
            if (name.Contains(key, StringComparison.Ordinal))
                return code;
        }

        return null;
    }

    public sealed record ArchiveCodeLine(int AccountId, string? CustomerName, string? CityName, DateTime? SaleDate, long Total,
        string? ExistingCode, string? ProposedCode, string? Error);

    /// <summary>
    /// One line per sale of the debtor: the code it already has, or the code it would get now.
    /// The code is built from the sale itself: branch, sale-date year and the total sale amount
    /// (never the remaining). A sale with no sale date, or in a branch with no code, is reported as an error.
    /// </summary>
    public async Task<(List<ArchiveCodeLine> Lines, List<ArchiveCodeRequest> Requests)> ArchiveCodesAsync(int personId, string actor, CancellationToken ct)
    {
        var accounts = await _store.AccountsAsync(personId, ct);
        var branchRows = new Dictionary<int, BranchCustomer>();
        var unreachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in accounts.Where(a => a.ArchiveCode is null).GroupBy(a => a.CityValue, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var rows = await _branches.PostAsync<List<BranchCustomer>>(
                    group.Key, "customers/by-ids", new { ids = group.Select(a => a.CustomerId).ToList() }, actor, ct);
                foreach (var account in group)
                {
                    var row = rows.FirstOrDefault(r => r.CustomerId == account.CustomerId);
                    if (row != null)
                        branchRows[account.Id] = row;
                }
            }
            catch (LegalRuleException)
            {
                unreachable.Add(group.Key);
            }
        }

        var requests = new List<ArchiveCodeRequest>();
        var errors = new Dictionary<int, string>();
        foreach (var account in accounts.Where(a => a.ArchiveCode is null))
        {
            var branch = ArchiveBranchCode(account.CityValue, account.CityName);
            if (unreachable.Contains(account.CityValue) || !branchRows.TryGetValue(account.Id, out var row))
                errors[account.Id] = $"تعذر قراءة بيانات البيع من فرع {account.CityName}";
            else if (branch is null)
                errors[account.Id] = $"لا يوجد رمز ارشيف لفرع {account.CityName}";
            else if (row.SaleDate is null)
                errors[account.Id] = "لا يوجد تاريخ بيع لهذا الحساب";
            else
                requests.Add(new ArchiveCodeRequest(account.Id, branch, row.SaleDate.Value.Year % 100, (int)(Math.Max(0, row.Total) / 1_000_000)));
        }

        var proposed = requests.Count > 0 ? await _store.PreviewArchiveCodesAsync(requests, ct) : new Dictionary<int, string>();
        var lines = accounts.Select(a =>
        {
            branchRows.TryGetValue(a.Id, out var row);
            return new ArchiveCodeLine(a.Id, row?.CustomerName ?? a.CustomerName, a.CityName, row?.SaleDate, row?.Total ?? a.LastTotal,
                a.ArchiveCode, proposed.GetValueOrDefault(a.Id), errors.GetValueOrDefault(a.Id));
        }).ToList();
        return (lines, requests);
    }

    /// <summary>Moves a debtor along the flow; entering the legal archive issues the missing archive codes in the same step.</summary>
    public async Task<object> MoveAsync(int caseId, string to, string? note, string actor, int? lawyerId, CancellationToken ct)
    {
        IReadOnlyList<ArchiveCodeRequest>? requests = null;
        var legalCase = await _store.CaseAsync(caseId, ct) ?? throw new LegalRuleException("الملف غير موجود", 404);
        if ((string)legalCase.stage == LegalStages.Recent && to == LegalStages.Archive)
        {
            var (lines, built) = await ArchiveCodesAsync((int)legalCase.personId, actor, ct);
            var failed = lines.FirstOrDefault(l => l.Error != null);
            if (failed != null)
                throw new LegalRuleException($"لا يمكن النقل إلى ارشيف القانونية: {failed.Error} ({failed.CustomerName})");
            requests = built;
        }

        // Reaching القرارات fixes what the debtor owes at that moment (ذمة القانونية), so read his
        // balances from the branches first. Best effort: a branch that is down leaves the last figures.
        if (to == LegalStages.Decisions)
        {
            try
            {
                await LiveAccountsAsync(await _store.AccountsAsync((int)legalCase.personId, ct), actor, ct);
            }
            catch (Exception)
            {
                // The move itself must not depend on the branches.
            }
        }

        return await _store.MoveAsync(caseId, to, note, actor, ct, lawyerId, requests);
    }

    /// <summary>Re-reads every tracked account from its branch so list totals reflect payments made outside the legal desk.</summary>
    public async Task<object> RefreshAllSnapshotsAsync(string actor, CancellationToken ct)
    {
        var accounts = await _store.AllAccountsAsync(ct);
        var (_, stale) = await LiveAccountsAsync(accounts, actor, ct);
        return new { accounts = accounts.Count, staleCities = stale };
    }
}
