using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.DelegatedManager.Stores;

namespace BE_SalesEmployee.DelegatedManager.Services;

public sealed class AppCommitteeTaskService
{
    private const int MaxBodyLength = 8000;
    private readonly IAppCommitteeTaskStore _store;

    public AppCommitteeTaskService(IAppCommitteeTaskStore store)
    {
        _store = store;
    }

    public Task EnsureSchemaAsync(CancellationToken ct) => _store.EnsureSchemaAsync(ct);

    public Task<IReadOnlyList<AppCommitteeTask>> ListForManagerAsync(CancellationToken ct) =>
        _store.ListAllAsync(ct);

    public async Task<IReadOnlyList<AppCommitteeTask>> ListPublicAsync(CancellationToken ct)
    {
        var rows = await _store.ListEnabledAsync(ct);
        return rows.Where(r => !string.IsNullOrWhiteSpace(r.Body)).ToList();
    }

    public async Task<(bool Ok, string? Error, AppCommitteeTask? Row)> CreateAsync(
        string? body,
        bool isEnabled,
        string? updatedBy,
        CancellationToken ct)
    {
        var (ok, error, text) = ValidateBody(body);
        if (!ok)
            return (false, error, null);

        var existing = await _store.ListAllAsync(ct);
        var now = DateTime.UtcNow;
        var row = new AppCommitteeTask
        {
            Body = text!,
            IsEnabled = isEnabled,
            SortOrder = existing.Count == 0 ? 0 : existing.Max(r => r.SortOrder) + 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            UpdatedBy = string.IsNullOrWhiteSpace(updatedBy) ? null : updatedBy.Trim()
        };
        var saved = await _store.InsertAsync(row, ct);
        return (true, null, saved);
    }

    public async Task<(bool Ok, string? Error, AppCommitteeTask? Row)> UpdateAsync(
        int id,
        string? body,
        bool isEnabled,
        string? updatedBy,
        CancellationToken ct)
    {
        var existing = await _store.GetByIdAsync(id, ct);
        if (existing is null)
            return (false, "المهمة غير موجودة.", null);

        var (ok, error, text) = ValidateBody(body);
        if (!ok)
            return (false, error, null);

        existing.Body = text!;
        existing.IsEnabled = isEnabled;
        existing.UpdatedBy = string.IsNullOrWhiteSpace(updatedBy) ? existing.UpdatedBy : updatedBy.Trim();
        await _store.UpdateAsync(existing, ct);
        return (true, null, existing);
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(int id, CancellationToken ct)
    {
        var existing = await _store.GetByIdAsync(id, ct);
        if (existing is null)
            return (false, "المهمة غير موجودة.");
        await _store.DeleteAsync(id, ct);
        return (true, null);
    }

    private static (bool Ok, string? Error, string? Body) ValidateBody(string? body)
    {
        var text = (body ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text))
            return (false, "نص المهمة مطلوب.", null);
        if (text.Length > MaxBodyLength)
            return (false, $"النص طويل جداً (الحد {MaxBodyLength} حرف).", null);
        return (true, null, text);
    }
}
