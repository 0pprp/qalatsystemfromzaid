using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.DelegatedManager.Stores;

namespace BE_SalesEmployee.DelegatedManager.Services;

public sealed class PriceBoardService
{
    private readonly IPriceBoardStore _store;

    public PriceBoardService(IPriceBoardStore store)
    {
        _store = store;
    }

    public Task EnsureSchemaAsync(CancellationToken ct = default) => _store.EnsureSchemaAsync(ct);

    public Task<IReadOnlyList<PriceBoardEntry>> ListAsync(string? cityValue, CancellationToken ct = default) =>
        _store.ListAsync(cityValue, ct);

    public async Task<(bool Ok, string? Error, PriceBoardEntry? Row)> CreateAsync(
        string? productName,
        decimal salePrice,
        decimal dailyInstallment,
        decimal downPayment,
        string? targetCityValue,
        string? targetCityName,
        string? actorName,
        CancellationToken ct = default)
    {
        var err = Validate(productName, salePrice, dailyInstallment, downPayment, targetCityValue);
        if (err != null)
            return (false, err, null);

        var row = new PriceBoardEntry
        {
            ProductName = productName!.Trim(),
            SalePrice = salePrice,
            DailyInstallment = dailyInstallment,
            DownPayment = downPayment,
            TargetCityValue = targetCityValue!.Trim(),
            TargetCityName = (targetCityName ?? "").Trim(),
            CreatedByName = actorName,
            UpdatedByName = actorName,
        };
        var saved = await _store.InsertAsync(row, ct);
        return (true, null, saved);
    }

    public async Task<(bool Ok, string? Error, PriceBoardEntry? Row)> UpdateAsync(
        int id,
        string? productName,
        decimal salePrice,
        decimal dailyInstallment,
        decimal downPayment,
        string? targetCityValue,
        string? targetCityName,
        string? actorName,
        CancellationToken ct = default)
    {
        var existing = await _store.GetByIdAsync(id, ct);
        if (existing is null)
            return (false, "الإعلان غير موجود", null);

        var err = Validate(productName, salePrice, dailyInstallment, downPayment, targetCityValue);
        if (err != null)
            return (false, err, null);

        existing.ProductName = productName!.Trim();
        existing.SalePrice = salePrice;
        existing.DailyInstallment = dailyInstallment;
        existing.DownPayment = downPayment;
        existing.TargetCityValue = targetCityValue!.Trim();
        existing.TargetCityName = (targetCityName ?? "").Trim();
        existing.UpdatedByName = actorName;
        await _store.UpdateAsync(existing, ct);
        return (true, null, await _store.GetByIdAsync(id, ct));
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(int id, CancellationToken ct = default)
    {
        if (!await _store.DeleteAsync(id, ct))
            return (false, "الإعلان غير موجود");
        return (true, null);
    }

    public static string? Validate(
        string? productName,
        decimal salePrice,
        decimal dailyInstallment,
        decimal downPayment,
        string? targetCityValue)
    {
        if (string.IsNullOrWhiteSpace(productName))
            return "اسم المنتج مطلوب";
        if (string.IsNullOrWhiteSpace(targetCityValue))
            return "محافظة الهدف مطلوبة";
        if (salePrice < 0 || dailyInstallment < 0 || downPayment < 0)
            return "المبالغ لا يمكن أن تكون سالبة";
        return null;
    }
}
