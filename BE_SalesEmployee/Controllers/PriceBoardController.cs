using System.Security.Claims;
using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.Sales.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

/// <summary>Monitor CRUD for «لوحة الأسعار»; director/monitor can read.</summary>
[Route("api/director/price-board")]
[ApiController]
[Authorize(Policy = SalesPolicies.DirectorOrMonitor)]
public sealed class PriceBoardController : ControllerBase
{
    private readonly PriceBoardService _service;

    public PriceBoardController(PriceBoardService service)
    {
        _service = service;
    }

    public sealed class UpsertPriceBoardRequest
    {
        public string? ProductName { get; set; }
        public decimal SalePrice { get; set; }
        public decimal DailyInstallment { get; set; }
        public decimal DownPayment { get; set; }
        public string? TargetCityValue { get; set; }
        public string? TargetCityName { get; set; }
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? cityValue, CancellationToken ct)
    {
        var rows = await _service.ListAsync(cityValue, ct);
        return Ok(rows.Select(ToDto));
    }

    [HttpPost]
    [Authorize(Policy = SalesPolicies.Monitor)]
    public async Task<IActionResult> Create([FromBody] UpsertPriceBoardRequest? body, CancellationToken ct)
    {
        body ??= new UpsertPriceBoardRequest();
        var (ok, error, row) = await _service.CreateAsync(
            body.ProductName,
            body.SalePrice,
            body.DailyInstallment,
            body.DownPayment,
            body.TargetCityValue,
            body.TargetCityName,
            ActorName(),
            ct);
        if (!ok || row is null)
            return BadRequest(new { message = error ?? "تعذر الإضافة" });
        return Ok(ToDto(row));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = SalesPolicies.Monitor)]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertPriceBoardRequest? body, CancellationToken ct)
    {
        body ??= new UpsertPriceBoardRequest();
        var (ok, error, row) = await _service.UpdateAsync(
            id,
            body.ProductName,
            body.SalePrice,
            body.DailyInstallment,
            body.DownPayment,
            body.TargetCityValue,
            body.TargetCityName,
            ActorName(),
            ct);
        if (!ok || row is null)
            return BadRequest(new { message = error ?? "تعذر التعديل" });
        return Ok(ToDto(row));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = SalesPolicies.Monitor)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var (ok, error) = await _service.DeleteAsync(id, ct);
        if (!ok)
            return BadRequest(new { message = error ?? "تعذر الحذف" });
        return Ok(new { message = "تم الحذف" });
    }

    private string ActorName() =>
        User.FindFirstValue("UserName")
        ?? User.FindFirstValue(ClaimTypes.Name)
        ?? "المراقب";

    private static object ToDto(PriceBoardEntry r) => new
    {
        id = r.Id,
        productName = r.ProductName,
        salePrice = r.SalePrice,
        dailyInstallment = r.DailyInstallment,
        downPayment = r.DownPayment,
        targetCityValue = r.TargetCityValue,
        targetCityName = r.TargetCityName,
        createdAtUtc = r.CreatedAtUtc,
        updatedAtUtc = r.UpdatedAtUtc,
        createdByName = r.CreatedByName,
        updatedByName = r.UpdatedByName,
    };
}
