using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

[Route("api/director/oversight")]
[ApiController]
[Authorize(Policy = SalesPolicies.DirectorOrMonitor)]
public sealed class OversightAccountsController : ControllerBase
{
    private readonly OversightAccountService _accounts;

    public OversightAccountsController(OversightAccountService accounts)
    {
        _accounts = accounts;
    }

    [HttpGet("committees")]
    public async Task<IActionResult> Committees(CancellationToken ct)
    {
        if (!OversightAccessRules.CanManageCommittees(ActorRole()))
            return Forbid();
        var rows = await _accounts.ListCommitteesAsync(ct);
        return Ok(rows.Select(c => new
        {
            id = c.Id,
            name = c.Name,
            createdBy = c.CreatedBy,
            cityValues = c.CityValues,
        }));
    }

    [HttpPost("committees")]
    public async Task<IActionResult> CreateCommittee([FromBody] CommitteeBody? body, CancellationToken ct)
    {
        body ??= new CommitteeBody();
        var (ok, error, row) = await _accounts.CreateCommitteeAsync(
            ActorRole(), body.Name, body.CityValues, ActorName(), ct);
        if (!ok || row is null)
            return BadRequest(new { message = error ?? "تعذر إنشاء اللجنة" });
        return Ok(new { id = row.Id, name = row.Name, cityValues = row.CityValues });
    }

    [HttpPut("committees/{id:int}/branches")]
    public async Task<IActionResult> SetBranches(int id, [FromBody] CommitteeBody? body, CancellationToken ct)
    {
        body ??= new CommitteeBody();
        var (ok, error) = await _accounts.SetBranchesAsync(ActorRole(), id, body.CityValues, ct);
        if (!ok)
            return BadRequest(new { message = error ?? "تعذر حفظ الفروع" });
        return Ok(new { message = "تم حفظ فروع اللجنة" });
    }

    [HttpDelete("committees/{id:int}")]
    public async Task<IActionResult> DeleteCommittee(int id, CancellationToken ct)
    {
        var (ok, error) = await _accounts.DeleteCommitteeAsync(ActorRole(), id, ct);
        if (!ok)
            return BadRequest(new { message = error ?? "تعذر حذف اللجنة" });
        return Ok(new { message = "تم حذف اللجنة" });
    }

    [HttpGet("accounts")]
    public async Task<IActionResult> Accounts(CancellationToken ct)
    {
        if (!OversightAccessRules.CanManageCommittees(ActorRole()))
            return Forbid();
        var rows = await _accounts.ListAccountsAsync(ct);
        return Ok(rows.Select(a => new
        {
            id = a.Id,
            userName = a.UserName,
            displayName = a.DisplayName,
            role = a.Role,
            committeeId = a.CommitteeId,
            committeeName = a.CommitteeName,
            cityValues = a.CityValues,
        }));
    }

    [HttpPost("accounts")]
    public async Task<IActionResult> CreateAccount([FromBody] AccountBody? body, CancellationToken ct)
    {
        body ??= new AccountBody();
        var (ok, error, row) = await _accounts.CreateAccountAsync(
            ActorRole(), body.UserName, body.Password, body.Role, body.CommitteeId, ActorName(), ct);
        if (!ok || row is null)
            return BadRequest(new { message = error ?? "تعذر إنشاء الحساب" });
        return Ok(new
        {
            id = row.Id,
            userName = row.UserName,
            role = row.Role,
            committeeId = row.CommitteeId,
        });
    }

    [HttpDelete("accounts/{id:int}")]
    public async Task<IActionResult> DeleteAccount(int id, CancellationToken ct)
    {
        var (ok, error) = await _accounts.DeleteAccountAsync(ActorRole(), id, ct);
        if (!ok)
            return BadRequest(new { message = error ?? "تعذر حذف الحساب" });
        return Ok(new { message = "تم حذف الحساب" });
    }

    private string ActorRole() => User.FindFirst("UserType")?.Value ?? "";
    private string ActorName() => User.FindFirst("UserName")?.Value ?? "";

    public sealed class CommitteeBody
    {
        public string? Name { get; set; }
        public List<string>? CityValues { get; set; }
    }

    public sealed class AccountBody
    {
        public string? UserName { get; set; }
        public string? Password { get; set; }
        public string? Role { get; set; }
        public int? CommitteeId { get; set; }
    }
}
