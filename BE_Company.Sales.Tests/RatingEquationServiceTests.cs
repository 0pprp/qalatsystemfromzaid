using BE_Company.Sales.Rating;
using Xunit;

namespace BE_Company.Sales.Tests;

public class RatingEquationServiceTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(1.5, 1.5)]
    [InlineData(2, 2)]
    [InlineData(2.456, 2.46)]
    [InlineData(0, 1)]
    [InlineData(-3, 1)]
    [InlineData(0.2, 0.5)]
    [InlineData(99, 10)]
    public void ClampFactor_Bounds(double input, double expected)
    {
        Assert.Equal(expected, RatingEquationService.ClampFactor(input), 2);
    }
}
