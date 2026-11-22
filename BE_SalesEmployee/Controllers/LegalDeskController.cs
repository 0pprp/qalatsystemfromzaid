using BE_SalesEmployee.Legal;
using BE_SalesEmployee.Sales.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BE_SalesEmployee.Controllers;

/// <summary>Turns a broken legal rule into its HTTP status with an Arabic message the screen can show.</summary>
public sealed class LegalErrorsAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is LegalRuleException rule)
        {
            context.Result = new ObjectResult(new { message = rule.Message, code = rule.Code }) { StatusCode = rule.Status };
            context.ExceptionHandled = true;
        }
    }
}

/// <summary>
/// Company-dashboard side of القانونية (محاسب القانونية + المدير): legal lists, grouping a debtor's
/// sales into one profile, sending debtors to the legal app, following their progress, and taking
/// their payments. Registers are read-only here.
/// </summary>
[Route("api/legal-desk")]
[ApiController]
[Authorize(Policy = SalesPolicies.LegalDesk)]
[LegalErrors]
public sealed class LegalDeskController : ControllerBase
{
    private readonly LegalStore _store;
    private readonly LegalService _service;
    private readonly LegalBranchClient _branches;

    public LegalDeskController(LegalStore store, LegalService service, LegalBranchClient branches)
    {
        _store = store;
        _service = service;
        _branches = branches;
    }

    private string Actor => User.FindFirst("UserName")?.Value ?? "";

    [HttpGet("cities")]
    public async Task<IActionResult> Cities(CancellationToken ct) => Ok(await _service.CitiesAsync(ct));

    // —— القوائم والزبائن على الفروع ——

    [HttpGet("lists")]
    public async Task<IActionResult> Lists([FromQuery] string cityValue, CancellationToken ct)
    {
        var (status, body) = await _branches.SendAsync(cityValue, "lists", HttpMethod.Get, null, Actor, ct);
        return StatusCode(status, body);
    }

    [HttpGet("customers")]
    public async Task<IActionResult> Customers(
        [FromQuery] string cityValue, [FromQuery] int? delegateId, [FromQuery] string? q, [FromQuery] string? scope, CancellationToken ct) =>
        Ok(await _service.CustomersAsync(cityValue, delegateId, q, scope, Actor, ct));

    [HttpGet("customers/similar")]
    public async Task<IActionResult> Similar([FromQuery] string cityValue, [FromQuery] int customerId, CancellationToken ct) =>
        Ok(await _service.SimilarAsync(cityValue, customerId, Actor, ct));

    [HttpGet("customers/detail")]
    public async Task<IActionResult> CustomerDetail([FromQuery] string cityValue, [FromQuery] int customerId, CancellationToken ct)
    {
        var (status, body) = await _branches.SendAsync(cityValue, $"customers/{customerId}/detail", HttpMethod.Get, null, Actor, ct);
        return StatusCode(status, body);
    }

    /// <summary>Name/phone search across every branch, finished sales included.</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct) =>
        Ok(await _service.SearchAllAsync(q, Actor, ct));

    // —— المدينون (الملف الموحد) ——

    [HttpGet("persons")]
    public async Task<IActionResult> Persons([FromQuery] string? stage, [FromQuery] string? q, CancellationToken ct) =>
        Ok(await _store.PersonsAsync(stage, q, ct));

    [HttpGet("persons/{id:int}")]
    public async Task<IActionResult> Person(int id, CancellationToken ct)
    {
        var detail = await _service.PersonDetailAsync(id, Actor, ct);
        return detail is null ? NotFound(new { message = "المدين غير موجود" }) : Ok(detail);
    }

    public sealed class AccountBody
    {
        public string CityValue { get; set; } = "";
        public int CustomerId { get; set; }
    }

    public sealed class PersonBody
    {
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? Notes { get; set; }
        public List<AccountBody>? Accounts { get; set; }
    }

    [HttpPost("persons")]
    public async Task<IActionResult> CreatePerson([FromBody] PersonBody body, CancellationToken ct)
    {
        var accounts = await _service.ResolveAccountsAsync(Refs(body.Accounts), Actor, ct);
        var id = await _store.CreatePersonAsync(body.FullName ?? "", body.Phone, body.Address, body.Notes, accounts, Actor, ct);
        return Ok(new { id });
    }

    [HttpPut("persons/{id:int}")]
    public async Task<IActionResult> UpdatePerson(int id, [FromBody] PersonBody body, CancellationToken ct)
    {
        await _store.UpdatePersonAsync(id, body.FullName ?? "", body.Phone, body.Address, body.Notes, ct);
        return Ok(new { id });
    }

    [HttpDelete("persons/{id:int}")]
    public async Task<IActionResult> DeletePerson(int id, CancellationToken ct)
    {
        await _store.DeletePersonAsync(id, ct);
        return Ok(new { id });
    }

    [HttpPost("persons/{id:int}/accounts")]
    public async Task<IActionResult> AddAccounts(int id, [FromBody] PersonBody body, CancellationToken ct)
    {
        var accounts = await _service.ResolveAccountsAsync(Refs(body.Accounts), Actor, ct);
        if (accounts.Count == 0)
            return BadRequest(new { message = "اختر حساباً واحداً على الأقل" });
        await _store.AddAccountsAsync(id, accounts, Actor, ct);
        return Ok(new { id });
    }

    [HttpDelete("persons/{id:int}/accounts/{accountId:int}")]
    public async Task<IActionResult> RemoveAccount(int id, int accountId, CancellationToken ct)
    {
        await _store.RemoveAccountAsync(id, accountId, ct);
        return Ok(new { id });
    }

    public sealed class NoteBody
    {
        public string? Note { get; set; }
    }

    /// <summary>Hands the debtor to the legal app, where he lands in «الاسماء المضافة مؤخرا».</summary>
    [HttpPost("persons/{id:int}/send")]
    public async Task<IActionResult> Send(int id, [FromBody] NoteBody? body, CancellationToken ct) =>
        Ok(new { caseId = await _store.SendPersonAsync(id, body?.Note, Actor, ct) });

    public sealed class PayLineBody
    {
        public int AccountId { get; set; }
        public long Amount { get; set; }
    }

    public sealed class PayBody
    {
        public List<PayLineBody>? Lines { get; set; }
        public long PenaltyAmount { get; set; }
        public string? Note { get; set; }
    }

    [HttpPost("persons/{id:int}/payments")]
    public async Task<IActionResult> Pay(int id, [FromBody] PayBody body, CancellationToken ct)
    {
        var lines = (body.Lines ?? new List<PayLineBody>()).Select(l => new LegalStore.PayLine(l.AccountId, l.Amount)).ToList();
        return Ok(await _service.PayAsync(id, lines, body.PenaltyAmount, body.Note, Actor, ct));
    }

    // —— طلبات التسديد الكامل ——

    [HttpGet("settlement-requests")]
    public async Task<IActionResult> SettlementRequests([FromQuery] string? status, CancellationToken ct) =>
        Ok(await _store.SettlementRequestsAsync(status, ct));

    public sealed class DecideBody
    {
        public bool Approve { get; set; }
        public long Penalty { get; set; }
        public string? Note { get; set; }
    }

    [HttpPost("settlement-requests/{id:int}/decide")]
    public async Task<IActionResult> Decide(int id, [FromBody] DecideBody body, CancellationToken ct) =>
        Ok(new { personId = await _store.DecideSettlementAsync(id, body.Approve, body.Penalty, body.Note, Actor, ct) });

    // —— طلبات الابطال ——

    [HttpGet("annul-requests")]
    public async Task<IActionResult> AnnulRequests([FromQuery] string? status, CancellationToken ct) =>
        Ok(await _store.AnnulRequestsAsync(status, ct));

    public sealed class AnnulDecideBody
    {
        public bool Approve { get; set; }
        public string? Note { get; set; }
    }

    /// <summary>Accepting records مبلغ الابطال as a تسديد and marks the debtor ابطال.</summary>
    [HttpPost("annul-requests/{id:int}/decide")]
    public async Task<IActionResult> DecideAnnul(int id, [FromBody] AnnulDecideBody body, CancellationToken ct) =>
        Ok(await _service.DecideAnnulAsync(id, body.Approve, body.Note, Actor, ct));

    public sealed class ReturnAnnulBody
    {
        public string? Note { get; set; }
    }

    /// <summary>
    /// The debtor stopped paying: ابطال is lifted and he goes back to the legal app as an ordinary
    /// file. His station, steps and what he already paid are untouched.
    /// </summary>
    [HttpPost("cases/{caseId:int}/return-from-annul")]
    public async Task<IActionResult> ReturnFromAnnul(int caseId, [FromBody] ReturnAnnulBody? body, CancellationToken ct)
    {
        var note = string.IsNullOrWhiteSpace(body?.Note) ? "أُعيد من التسديد: المدين لا يسدد حالياً" : $"أُعيد من التسديد: {body!.Note!.Trim()}";
        await _store.ReturnFromAnnulAsync(caseId, note, Actor, ct);
        return Ok(new { caseId });
    }

    // —— نظرة عامة (قراءة فقط) ——

    /// <summary>القاصة: collected تسديدات and the money put into the legal app's قاصة.</summary>
    [HttpGet("cashbox")]
    public async Task<IActionResult> Cashbox(CancellationToken ct) => Ok(await _store.DeskCashboxAsync(ct));

    [HttpGet("overview")]
    public async Task<IActionResult> Overview(CancellationToken ct) => Ok(await _store.OverviewAsync(ct));

    /// <summary>What القانونية collected from debtors (debt + penalties), newest first.</summary>
    [HttpGet("payments")]
    public async Task<IActionResult> Payments([FromQuery] int take = 300, [FromQuery] string? q = null, CancellationToken ct = default) =>
        Ok(await _store.RecentPaymentsAsync(take, ct, q));

    [HttpGet("registers")]
    public async Task<IActionResult> Registers([FromQuery] int take = 300, CancellationToken ct = default) =>
        Ok(await _store.RegistersAsync(take, ct));

    /// <summary>المدير only: debtors in the legal app with no step and no new station for the given number of days.</summary>
    [HttpGet("stalled")]
    [Authorize(Policy = SalesPolicies.Director)]
    public async Task<IActionResult> Stalled([FromQuery] int days = 30, CancellationToken ct = default) =>
        Ok(await _store.StalledAsync(days, ct));

    [HttpPost("refresh-balances")]
    public async Task<IActionResult> RefreshBalances(CancellationToken ct) =>
        Ok(await _service.RefreshAllSnapshotsAsync(Actor, ct));

    private static List<LegalService.AccountRef> Refs(List<AccountBody>? accounts) =>
        (accounts ?? new List<AccountBody>()).Select(a => new LegalService.AccountRef(a.CityValue, a.CustomerId)).ToList();
}
