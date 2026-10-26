namespace BE_SalesEmployee.DelegatedManager.Models;

public sealed class BranchManagerAppUser
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? CreatedByUserName { get; set; }
    public List<BranchManagerAppUserCity> Cities { get; set; } = [];

    public BranchManagerAppUser Clone()
    {
        var copy = (BranchManagerAppUser)MemberwiseClone();
        copy.Cities = Cities.Select(c => c.Clone()).ToList();
        return copy;
    }
}

public sealed class BranchManagerAppUserCity
{
    public string CityValue { get; set; } = "";
    public string? CityName { get; set; }
    public string? CityLink { get; set; }
    public string? Database { get; set; }

    public BranchManagerAppUserCity Clone() => (BranchManagerAppUserCity)MemberwiseClone();
}
