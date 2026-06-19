using Xunit;

namespace MiniBank.Shared.Domain.UnitTests;

public class SmokeTest
{
    [Fact]
    public void Solution_ShouldBuild_AndTestsShouldRun()
    {
        var result = 1 + 1;
        Assert.Equal(2, result);
    }
}
