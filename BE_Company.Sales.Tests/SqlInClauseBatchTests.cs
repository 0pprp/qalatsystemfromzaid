using BE_Company.Sales;
using Xunit;

namespace BE_Company.Sales.Tests;

public class SqlInClauseBatchTests
{
    [Fact]
    public void Empty_Yields_No_Chunks()
    {
        Assert.Empty(SqlInClauseBatch.Chunk([]));
    }

    [Fact]
    public void Ten_Ids_Single_Chunk()
    {
        var chunks = SqlInClauseBatch.Chunk(Enumerable.Range(1, 10)).ToList();
        Assert.Single(chunks);
        Assert.Equal(10, chunks[0].Length);
    }

    [Fact]
    public void OneThousand_Fits_In_Two_Chunks_Or_Less_With_Default()
    {
        var chunks = SqlInClauseBatch.Chunk(Enumerable.Range(1, 1000)).ToList();
        Assert.True(chunks.Count <= 2);
        Assert.Equal(1000, chunks.Sum(c => c.Length));
        Assert.All(chunks, c => Assert.True(c.Length <= SqlInClauseBatch.DefaultSafeSize));
    }

    [Theory]
    [InlineData(2100)]
    [InlineData(2500)]
    [InlineData(5000)]
    public void Large_Sets_Never_Exceed_Safe_Batch_Size(int count)
    {
        var chunks = SqlInClauseBatch.Chunk(Enumerable.Range(1, count)).ToList();
        Assert.Equal(count, chunks.Sum(c => c.Length));
        Assert.All(chunks, c =>
        {
            Assert.True(c.Length <= SqlInClauseBatch.DefaultSafeSize);
            Assert.True(c.Length < SqlInClauseBatch.SqlServerMaxParameters);
        });
        Assert.True(chunks.Count >= (count + SqlInClauseBatch.DefaultSafeSize - 1) / SqlInClauseBatch.DefaultSafeSize);
    }

    [Fact]
    public void Customer_In_Third_Batch_Is_Present()
    {
        var chunks = SqlInClauseBatch.Chunk(Enumerable.Range(1, 2500)).ToList();
        Assert.True(chunks.Count >= 3);
        Assert.Contains(2101, chunks[2]);
        Assert.Contains(2500, chunks[^1]);
    }

    [Fact]
    public void Duplicates_Are_Removed_Before_Chunking()
    {
        var ids = Enumerable.Repeat(42, 5000).Concat(Enumerable.Range(1, 10));
        var chunks = SqlInClauseBatch.Chunk(ids).ToList();
        Assert.Equal(11, chunks.Sum(c => c.Length));
        Assert.Equal(1, chunks.SelectMany(c => c).Count(id => id == 42));
    }

    [Fact]
    public void NonPositive_Ids_Are_Skipped()
    {
        var chunks = SqlInClauseBatch.Chunk([0, -1, 5, 0, 6]).ToList();
        Assert.Single(chunks);
        Assert.Equal(new[] { 5, 6 }, chunks[0]);
    }

    [Fact]
    public void Rejects_BatchSize_At_Or_Above_Sql_Limit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SqlInClauseBatch.Chunk([1], SqlInClauseBatch.SqlServerMaxParameters).ToList());
    }
}
