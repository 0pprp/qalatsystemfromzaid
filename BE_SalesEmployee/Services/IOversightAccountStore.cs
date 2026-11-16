namespace BE_SalesEmployee.Services;

public sealed class OversightCommitteeRecord
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string? CreatedBy { get; init; }
    public IReadOnlyList<string> CityValues { get; init; } = Array.Empty<string>();
}

public sealed class OversightAccountRecord
{
    public int Id { get; init; }
    public string UserName { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Password { get; init; } = "";
    public string Role { get; init; } = "";
    public int? CommitteeId { get; init; }
    public string? CommitteeName { get; init; }
    public IReadOnlyList<string> CityValues { get; init; } = Array.Empty<string>();
}

public interface IOversightAccountStore
{
    Task<IReadOnlyList<OversightCommitteeRecord>> ListCommitteesAsync(CancellationToken ct);
    Task<OversightCommitteeRecord> CreateCommitteeAsync(string name, IReadOnlyList<string> cityValues, string? createdBy, CancellationToken ct);
    Task<bool> SetCommitteeBranchesAsync(int committeeId, IReadOnlyList<string> cityValues, CancellationToken ct);
    Task<IReadOnlyList<OversightAccountRecord>> ListAccountsAsync(CancellationToken ct);
    Task<OversightAccountRecord?> FindByUserNameAsync(string userName, CancellationToken ct);
    Task<bool> UserNameExistsAsync(string userName, CancellationToken ct);
    Task<OversightAccountRecord> CreateAccountAsync(
        string userName,
        string displayName,
        string password,
        string role,
        int? committeeId,
        string? createdBy,
        CancellationToken ct);
    Task<bool> DeleteAccountAsync(int id, CancellationToken ct);
    /// <summary>Removes a committee and its branches. Callers must ensure it has no accounts.</summary>
    Task<bool> DeleteCommitteeAsync(int committeeId, CancellationToken ct);
}
