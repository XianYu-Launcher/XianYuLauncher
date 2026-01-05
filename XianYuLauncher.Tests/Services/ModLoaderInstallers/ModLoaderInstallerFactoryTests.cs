using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Xunit;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services.ModLoaderInstallers;

namespace XianYuLauncher.Tests.Services.ModLoaderInstallers;

public class ModLoaderInstallerFactoryTests
{
    private readonly Mock<IModLoaderInstaller> _mockFabricInstaller;
    private readonly Mock<IModLoaderInstaller> _mockQuiltInstaller;
    private readonly Mock<IModLoaderInstaller> _mockForgeInstaller;
    private readonly Mock<IModLoaderInstaller> _mockNeoForgeInstaller;
    private readonly Mock<IModLoaderInstaller> _mockOptifineInstaller;
    private readonly ModLoaderInstallerFactory _factory;

    public ModLoaderInstallerFactoryTests()
    {
        _mockFabricInstaller = new Mock<IModLoaderInstaller>();
        _mockFabricInstaller.Setup(i => i.ModLoaderType).Returns("Fabric");

        _mockQuiltInstaller = new Mock<IModLoaderInstaller>();
        _mockQuiltInstaller.Setup(i => i.ModLoaderType).Returns("Quilt");

        _mockForgeInstaller = new Mock<IModLoaderInstaller>();
        _mockForgeInstaller.Setup(i => i.ModLoaderType).Returns("Forge");

        _mockNeoForgeInstaller = new Mock<IModLoaderInstaller>();
        _mockNeoForgeInstaller.Setup(i => i.ModLoaderType).Returns("NeoForge");

        _mockOptifineInstaller = new Mock<IModLoaderInstaller>();
        _mockOptifineInstaller.Setup(i => i.ModLoaderType).Returns("Optifine");

        var installers = new List<IModLoaderInstaller>
        {
            _mockFabricInstaller.Object,
            _mockQuiltInstaller.Object,
            _mockForgeInstaller.Object,
            _mockNeoForgeInstaller.Object,
            _mockOptifineInstaller.Object
        };

        _factory = new ModLoaderInstallerFactory(installers);
    }

    #region GetInstaller 测试

    [Theory]
    [InlineData("Fabric")]
    [InlineData("fabric")]
    [InlineData("FABRIC")]
    public void GetInstaller_Fabric_ReturnsFabricInstaller(string modLoaderType)
    {
        // Act
        var installer = _factory.GetInstaller(modLoaderType);

        // Assert
        Assert.Equal("Fabric", installer.ModLoaderType);
    }

    [Theory]
    [InlineData("Quilt")]
    [InlineData("quilt")]
    [InlineData("QUILT")]
    public void GetInstaller_Quilt_ReturnsQuiltInstaller(string modLoaderType)
    {
        // Act
        var installer = _factory.GetInstaller(modLoaderType);

        // Assert
        Assert.Equal("Quilt", installer.ModLoaderType);
    }

    [Theory]
    [InlineData("Forge")]
    [InlineData("forge")]
    [InlineData("FORGE")]
    public void GetInstaller_Forge_ReturnsForgeInstaller(string modLoaderType)
    {
        // Act
        var installer = _factory.GetInstaller(modLoaderType);

        // Assert
        Assert.Equal("Forge", installer.ModLoaderType);
    }

    [Theory]
    [InlineData("NeoForge")]
    [InlineData("neoforge")]
    [InlineData("NEOFORGE")]
    public void GetInstaller_NeoForge_ReturnsNeoForgeInstaller(string modLoaderType)
    {
        // Act
        var installer = _factory.GetInstaller(modLoaderType);

        // Assert
        Assert.Equal("NeoForge", installer.ModLoaderType);
    }

    [Theory]
    [InlineData("Optifine")]
    [InlineData("optifine")]
    [InlineData("OPTIFINE")]
    public void GetInstaller_Optifine_ReturnsOptifineInstaller(string modLoaderType)
    {
        // Act
        var installer = _factory.GetInstaller(modLoaderType);

        // Assert
        Assert.Equal("Optifine", installer.ModLoaderType);
    }

    [Fact]
    public void GetInstaller_UnsupportedType_ThrowsNotSupportedException()
    {
        // Act & Assert
        Assert.Throws<NotSupportedException>(() => _factory.GetInstaller("Unknown"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GetInstaller_NullOrEmpty_ThrowsArgumentException(string? modLoaderType)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _factory.GetInstaller(modLoaderType!));
    }

    #endregion

    #region GetSupportedModLoaderTypes 测试

    [Fact]
    public void GetSupportedModLoaderTypes_ReturnsAllTypes()
    {
        // Act
        var types = _factory.GetSupportedModLoaderTypes().ToList();

        // Assert
        Assert.Equal(5, types.Count);
        Assert.Contains("Fabric", types);
        Assert.Contains("Quilt", types);
        Assert.Contains("Forge", types);
        Assert.Contains("NeoForge", types);
        Assert.Contains("Optifine", types);
    }

    #endregion
}
