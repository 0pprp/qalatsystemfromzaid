using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using BE_PromotersApp.Auth;
using BE_PromotersApp.Infrastructure;

namespace BE_PromotersApp.Domain.Admin;

public sealed record AuditRow(long Id, string ActorName, string ActorRole, string Action, string Entity, string? EntityId, string? Before, string? After, DateTime CreatedAt);

/// <summary>Owner spec (section 10): every administrative and financial change is logged with who and when.</summary>
public sealed class AuditService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly Db _db;
    private readonly IHttpContextAccessor _http;

    public AuditService(Db db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public async Task WriteAsync(AdminActor actor, string action, string entity, object? entityId, object? before, object? after,
        SqlConnection? connection = null, SqlTransaction? tx = null)
    {
        var args = new
        {
            actor.Name, actor.Role, action, entity,
            entityId = entityId?.ToString(),
            before = before is null ? null : JsonSerializer.Serialize(before, Json),
            after = after is null ? null : JsonSerializer.Serialize(after, Json),
            ip = _http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
        };
        const string sql = """
            INSERT promoters.AuditLog(ActorName, ActorRole, Action, Entity, EntityId, BeforeJson, AfterJson, Ip)
            VALUES (@Name, @Role, @action, @entity, @entityId, @before, @after, @ip)
            """;
        if (connection is not null)
        {
            await connection.ExecuteAsync(sql, args, tx);
            return;
        }
        await using var own = await _db.OpenAsync();
        await own.ExecuteAsync(sql, args);
    }

    public async Task<PagedResult<AuditRow>> SearchAsync(string? entity, string? actor, DateTime? from, DateTime? to, Paging paging, CancellationToken ct)
    {
        await using var connection = await _db.OpenAsync(ct);
        const string where = """
            (@entity IS NULL OR Entity = @entity) AND (@actor IS NULL OR ActorName LIKE '%' + @actor + '%')
            AND (@from IS NULL OR CreatedAt >= @from) AND (@to IS NULL OR CreatedAt < @to)
            """;
        using var multi = await connection.QueryMultipleAsync($"""
            SELECT COUNT(*) FROM promoters.AuditLog WHERE {where};
            SELECT Id, ActorName, ActorRole, Action, Entity, EntityId, BeforeJson AS Before, AfterJson AS After, CreatedAt
            FROM promoters.AuditLog WHERE {where} ORDER BY Id DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """, new { entity, actor, from, to, paging.Offset, paging.PageSize });
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<AuditRow>((await multi.ReadAsync<AuditRow>()).ToList(), total, paging.Page, paging.PageSize);
    }
}
