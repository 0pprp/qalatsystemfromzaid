using Dapper;
using BE_PromotersApp.Auth;
using BE_PromotersApp.Domain.Admin;
using BE_PromotersApp.Domain.Users;
using BE_PromotersApp.Infrastructure;

namespace BE_PromotersApp.Domain.Products;

public sealed record ProductRow(int Id, string Name, long Price, long Commission, bool IsActive, int SortOrder, DateTime CreatedAt, DateTime UpdatedAt);

/// <summary>A product as the promoter sees it: MyCommission is their share after the creator's cut.</summary>
public sealed record AppProductDto(int Id, string Name, long Price, long MyCommission);

public sealed record ProductInput(string? Name, long? Price, long? Commission, bool? IsActive, int? SortOrder);

public sealed class ProductService
{
    private readonly Db _db;
    private readonly AuditService _audit;

    public ProductService(Db db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IReadOnlyList<AppProductDto>> ListForUserAsync(UserRow user, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var rows = await c.QueryAsync<ProductRow>(
            "SELECT Id, Name, Price, Commission, IsActive, SortOrder, CreatedAt, UpdatedAt FROM promoters.Products WHERE IsActive = 1 AND IsDeleted = 0 ORDER BY SortOrder, Name");
        return rows.Select(p => new AppProductDto(p.Id, p.Name, p.Price, CommissionSplit.SellerUnit(p.Commission, user.EffectiveCut))).ToList();
    }

    public async Task<IReadOnlyList<ProductRow>> AdminListAsync(CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        return (await c.QueryAsync<ProductRow>("SELECT Id, Name, Price, Commission, IsActive, SortOrder, CreatedAt, UpdatedAt FROM promoters.Products WHERE IsDeleted = 0 ORDER BY SortOrder, Name")).ToList();
    }

    public async Task<ProductRow> CreateAsync(ProductInput input, AdminActor actor, CancellationToken ct)
    {
        var (name, price, commission) = Validate(input);
        await using var c = await _db.OpenAsync(ct);
        var id = await c.ExecuteScalarAsync<int>("""
            INSERT promoters.Products(Name, Price, Commission, IsActive, SortOrder) OUTPUT inserted.Id
            VALUES (@name, @price, @commission, @active, @sort)
            """, new { name, price, commission, active = input.IsActive ?? true, sort = input.SortOrder ?? 0 });
        await _audit.WriteAsync(actor, "product.create", "Product", id, null, new { name, price, commission }, c);
        return await c.QuerySingleAsync<ProductRow>("SELECT Id, Name, Price, Commission, IsActive, SortOrder, CreatedAt, UpdatedAt FROM promoters.Products WHERE Id = @id", new { id });
    }

    /// <summary>Changes apply to new orders only: every order keeps the price and commission it was made with.</summary>
    public async Task<ProductRow> UpdateAsync(int id, ProductInput input, AdminActor actor, CancellationToken ct)
    {
        var (name, price, commission) = Validate(input);
        await using var c = await _db.OpenAsync(ct);
        var before = await c.QuerySingleOrDefaultAsync<ProductRow>("SELECT Id, Name, Price, Commission, IsActive, SortOrder, CreatedAt, UpdatedAt FROM promoters.Products WHERE Id = @id AND IsDeleted = 0", new { id })
                     ?? throw ApiException.NotFound("المنتج غير موجود");
        await c.ExecuteAsync("""
            UPDATE promoters.Products SET Name = @name, Price = @price, Commission = @commission, IsActive = @active, SortOrder = @sort,
                   UpdatedAt = SYSUTCDATETIME() WHERE Id = @id
            """, new { id, name, price, commission, active = input.IsActive ?? before.IsActive, sort = input.SortOrder ?? before.SortOrder });
        await _audit.WriteAsync(actor, "product.update", "Product", id, new { before.Name, before.Price, before.Commission, before.IsActive },
            new { name, price, commission, IsActive = input.IsActive ?? before.IsActive }, c);
        return await c.QuerySingleAsync<ProductRow>("SELECT Id, Name, Price, Commission, IsActive, SortOrder, CreatedAt, UpdatedAt FROM promoters.Products WHERE Id = @id", new { id });
    }

    public async Task DeleteAsync(int id, AdminActor actor, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var n = await c.ExecuteAsync("UPDATE promoters.Products SET IsDeleted = 1, IsActive = 0, UpdatedAt = SYSUTCDATETIME() WHERE Id = @id AND IsDeleted = 0", new { id });
        if (n == 0) throw ApiException.NotFound("المنتج غير موجود");
        await _audit.WriteAsync(actor, "product.delete", "Product", id, null, null, c);
    }

    private static (string Name, long Price, long Commission) Validate(ProductInput input)
    {
        var e = new Dictionary<string, string>();
        var name = Text.CollapseSpaces(input.Name);
        if (name.Length is < 2 or > 100) e["name"] = "اسم المنتج بين 2 و 100 حرف";
        if (input.Price is null or < 0 or > 1_000_000_000) e["price"] = "أدخل السعر بالدينار";
        if (input.Commission is null or < 0 or > 1_000_000_000) e["commission"] = "أدخل العمولة بالدينار";
        if (e.Count > 0) throw ApiException.Validation(e);
        return (name, input.Price!.Value, input.Commission!.Value);
    }
}
