using System.Data;
using Dapper;

namespace BE_SalesEmployee.DelegatedManager.Stores;

/// <summary>Dapper 2.1 does not map DateOnly parameters/results out of the box.</summary>
public sealed class DapperDateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public static void Register()
    {
        SqlMapper.AddTypeHandler(new DapperDateOnlyTypeHandler());
    }

    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value.ToDateTime(TimeOnly.MinValue);
    }

    public override DateOnly Parse(object value)
    {
        return value switch
        {
            DateOnly d => d,
            DateTime dt => DateOnly.FromDateTime(dt),
            string s when DateOnly.TryParse(s, out var parsed) => parsed,
            _ => DateOnly.FromDateTime(Convert.ToDateTime(value)),
        };
    }
}
