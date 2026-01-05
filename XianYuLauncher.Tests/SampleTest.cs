namespace XianYuLauncher.Tests;

/// <summary>
/// 示例测试类，用于验证测试项目配置正确
/// </summary>
public class SampleTest
{
    [Fact]
    public void TestProjectSetup_ShouldWork()
    {
        // Arrange
        var expected = 4;
        
        // Act
        var actual = 2 + 2;
        
        // Assert
        Assert.Equal(expected, actual);
    }
    
    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(5, 5, 10)]
    [InlineData(-1, 1, 0)]
    public void Addition_ShouldReturnCorrectSum(int a, int b, int expected)
    {
        // Act
        var actual = a + b;
        
        // Assert
        Assert.Equal(expected, actual);
    }
}
