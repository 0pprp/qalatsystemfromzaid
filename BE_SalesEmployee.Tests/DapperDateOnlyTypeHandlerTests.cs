using System.Data;
using BE_SalesEmployee.DelegatedManager.Stores;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class DapperDateOnlyTypeHandlerTests
{
    [Fact]
    public void SetValue_writes_DateTime_with_DbType_Date()
    {
        var handler = new DapperDateOnlyTypeHandler();
        var p = new FakeParam();
        handler.SetValue(p, new DateOnly(2026, 10, 30));

        Assert.Equal(DbType.Date, p.DbType);
        Assert.Equal(new DateTime(2026, 10, 30), p.Value);
    }

    [Fact]
    public void Parse_accepts_DateTime_and_string()
    {
        var handler = new DapperDateOnlyTypeHandler();
        Assert.Equal(new DateOnly(2026, 10, 30), handler.Parse(new DateTime(2026, 10, 30)));
        Assert.Equal(new DateOnly(2026, 10, 30), handler.Parse("2026-10-30"));
    }

    private sealed class FakeParam : IDbDataParameter
    {
        public DbType DbType { get; set; }
        public ParameterDirection Direction { get; set; }
        public bool IsNullable { get; set; }
        public string ParameterName { get; set; } = "";
        public string SourceColumn { get; set; } = "";
        public DataRowVersion SourceVersion { get; set; }
        public object? Value { get; set; }
        public byte Precision { get; set; }
        public byte Scale { get; set; }
        public int Size { get; set; }
    }
}
