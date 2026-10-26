using BE_SalesEmployee.DelegatedManager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

/// <summary>Public login gate for تطبيق إدارة الفرع — only DM-created accounts.</summary>
[Route("api/branch-manager-app")]
[ApiController]
public sealed class BranchManagerAppAuthController : ControllerBase
{
    private readonly BranchManagerAppUserService _users;

    public BranchManagerAppAuthController(BranchManagerAppUserService users) => _users = users;

    public sealed class LoginBody
    {
        public string? UserName { get; set; }
        public string? Password { get; set; }
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginBody? body, CancellationToken ct)
    {
        body ??= new LoginBody();
        var (ok, error, payload) = await _users.AuthenticateAsync(body.UserName, body.Password, ct);
        return ok ? Ok(payload) : Unauthorized(new { message = error });
    }
}
