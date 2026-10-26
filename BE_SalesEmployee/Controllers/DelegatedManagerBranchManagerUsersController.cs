using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

[Authorize(Policy = SalesPolicies.DelegatedManager)]
[Route("api/delegated-manager/branch-manager-users")]
[ApiController]
public sealed class DelegatedManagerBranchManagerUsersController : ControllerBase
{
    private readonly BranchManagerAppUserService _users;
    private readonly AdminCitiesService _cities;

    public DelegatedManagerBranchManagerUsersController(
        BranchManagerAppUserService users,
        AdminCitiesService cities)
    {
        _users = users;
        _cities = cities;
    }

    public sealed class UpsertBody
    {
        public string? UserName { get; set; }
        public string? Password { get; set; }
        public string? DisplayName { get; set; }
        public bool? IsActive { get; set; }
        public List<string>? CityValues { get; set; }
    }

    [HttpGet("cities")]
    public async Task<IActionResult> Cities(CancellationToken ct)
    {
        var cities = await _cities.GetCitiesAsync(ct);
        return Ok(cities.Select(c => new
        {
            value = c.Value,
            name = c.Name,
            database = c.Database,
            link = c.Link
        }));
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _users.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var row = await _users.GetAsync(id, ct);
        return row is null ? NotFound(new { message = "المستخدم غير موجود" }) : Ok(row);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertBody? body, CancellationToken ct)
    {
        body ??= new UpsertBody();
        var actor = TokenService.FromPrincipal(User).UserName;
        var (ok, error, user) = await _users.CreateAsync(
            body.UserName, body.Password, body.DisplayName, body.CityValues, actor, ct);
        return ok ? Ok(user) : BadRequest(new { message = error });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertBody? body, CancellationToken ct)
    {
        body ??= new UpsertBody();
        var (ok, error, user) = await _users.UpdateAsync(
            id, body.UserName, body.Password, body.DisplayName, body.IsActive, body.CityValues, ct);
        if (!ok && error == "المستخدم غير موجود")
            return NotFound(new { message = error });
        return ok ? Ok(user) : BadRequest(new { message = error });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var deleted = await _users.DeleteAsync(id, ct);
        return deleted ? Ok(new { ok = true }) : NotFound(new { message = "المستخدم غير موجود" });
    }
}
