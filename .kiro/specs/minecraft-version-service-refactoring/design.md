# Design Document: MinecraftVersionService 重构

## Overview

本设计文档描述了 MinecraftVersionService 重构的详细技术方案。当前代码已经被拆分为 9 个 partial 类文件，总计约 3700+ 行代码。虽然已经进行了初步的模块化，但仍存在以下问题：

1. **职责不够清晰**：某些 partial 类仍然包含多种职责
2. **代码重复**：不同 ModLoader 的安装流程存在大量重复代码
3. **测试覆盖不足**：缺少单元测试和集成测试
4. **错误处理不统一**：异常处理方式不一致
5. **性能优化空间**：下载和文件操作可以进一步优化

本次重构将采用渐进式方法，确保每一步都有测试覆盖，不破坏现有功能。

## Architecture

### Current Architecture (现状)

```
MinecraftVersionService (partial class)
├── MinecraftVersionService.cs (主文件，包含版本管理核心功能)
├── MinecraftVersionService.Base.cs (基础功能)
├── MinecraftVersionService.Download.cs (下载功能)
├── MinecraftVersionService.Fabric.cs (Fabric 安装)
├── MinecraftVersionService.Forge.cs (Forge 安装)
├── MinecraftVersionService.NeoForge.cs (NeoForge 安装)
├── MinecraftVersionService.Optifine.cs (Optifine 安装)
├── MinecraftVersionService.Processor.cs (处理器执行)
├── MinecraftVersionService.Quilt.cs (Quilt 安装)
└── MinecraftVersionService.Utils.cs (工具方法)
```

### Target Architecture (目标架构)

```
IMinecraftVersionService (接口层)
├── MinecraftVersionService (协调器)
│   └── 负责协调各个子服务，保持向后兼容的公共 API
│
├── IDownloadManager (下载管理)
│   ├── DownloadManager
│   ├── DownloadTask
│   └── DownloadQueue
│
├── ILibraryManager (依赖库管理)
│   ├── LibraryManager
│   ├── LibraryResolver
│   └── NativeLibraryExtractor
│
├── IAssetManager (资源管理)
│   ├── AssetManager
│   ├── AssetIndexManager
│   └── AssetObjectDownloader
│
├── IVersionInfoManager (版本信息管理)
│   ├── VersionInfoManager
│   ├── VersionConfigManager
│   └── VersionManifestCache
│
└── IModLoaderInstaller (ModLoader 安装)
    ├── FabricInstaller
    ├── ForgeInstaller
    ├── NeoForgeInstaller
    ├── OptifineInstaller
    ├── QuiltInstaller
    └── ProcessorExecutor
```

## Components and Interfaces

### 1. IDownloadManager (下载管理器)

负责所有文件下载操作，提供统一的下载接口。

```csharp
public interface IDownloadManager
{
    Task<string> DownloadFileAsync(string url, string targetPath, 
        Action<double> progressCallback = null, 
        CancellationToken cancellationToken = default);
    
    Task<byte[]> DownloadBytesAsync(string url, 
        CancellationToken cancellationToken = default);
    
    Task DownloadFilesAsync(IEnumerable<DownloadTask> tasks, 
        int maxConcurrency = 4,
        Action<double> progressCallback = null,
        CancellationToken cancellationToken = default);
}
```

### 2. ILibraryManager (依赖库管理器)

负责 Minecraft 依赖库的下载、验证和管理。

```csharp
public interface ILibraryManager
{
    Task DownloadLibrariesAsync(VersionInfo versionInfo, string librariesDirectory,
        Action<double> progressCallback = null,
        CancellationToken cancellationToken = default);
    
    Task ExtractNativeLibrariesAsync(VersionInfo versionInfo, 
        string librariesDirectory, string nativesDirectory,
        CancellationToken cancellationToken = default);
    
    bool IsLibraryDownloaded(Library library, string librariesDirectory);
    
    string GetLibraryPath(string libraryName, string librariesDirectory);
}
```

### 3. IAssetManager (资源管理器)

负责 Minecraft 游戏资源的下载和管理。

```csharp
public interface IAssetManager
{
    Task EnsureAssetIndexAsync(string versionId, VersionInfo versionInfo,
        string minecraftDirectory,
        Action<double> progressCallback = null,
        CancellationToken cancellationToken = default);
    
    Task DownloadAllAssetObjectsAsync(string versionId, string minecraftDirectory,
        Action<double> progressCallback = null,
        Action<string> currentDownloadCallback = null,
        CancellationToken cancellationToken = default);
    
    Task<AssetIndex> GetAssetIndexAsync(string assetIndexId, string minecraftDirectory);
}
```

### 4. IVersionInfoManager (版本信息管理器)

负责版本信息的获取、缓存和管理。

```csharp
public interface IVersionInfoManager
{
    Task<VersionManifest> GetVersionManifestAsync(
        CancellationToken cancellationToken = default);
    
    Task<VersionInfo> GetVersionInfoAsync(string versionId, 
        string minecraftDirectory = null,
        bool allowNetwork = true,
        CancellationToken cancellationToken = default);
    
    Task<List<string>> GetInstalledVersionsAsync(string minecraftDirectory = null);
    
    Task<VersionConfig> GetVersionConfigAsync(string versionId, string minecraftDirectory);
    
    Task SaveVersionConfigAsync(string versionId, string minecraftDirectory, VersionConfig config);
}
```

### 5. IModLoaderInstaller (ModLoader 安装器接口)

定义统一的 ModLoader 安装接口。

```csharp
public interface IModLoaderInstaller
{
    string ModLoaderType { get; }
    
    Task InstallAsync(string minecraftVersionId, string modLoaderVersion,
        string minecraftDirectory,
        Action<double> progressCallback = null,
        CancellationToken cancellationToken = default,
        string customVersionName = null);
    
    Task<List<string>> GetAvailableVersionsAsync(string minecraftVersionId);
}
```

## Data Models

### DownloadTask (下载任务)

```csharp
public class DownloadTask
{
    public string Url { get; set; }
    public string TargetPath { get; set; }
    public string ExpectedSha1 { get; set; }
    public long ExpectedSize { get; set; }
    public string Description { get; set; }
    public int Priority { get; set; }
}
```

### DownloadResult (下载结果)

```csharp
public class DownloadResult
{
    public bool Success { get; set; }
    public string FilePath { get; set; }
    public string ErrorMessage { get; set; }
    public Exception Exception { get; set; }
}
```

### AssetIndex (资源索引)

```csharp
public class AssetIndex
{
    public string Id { get; set; }
    public Dictionary<string, AssetObject> Objects { get; set; }
}

public class AssetObject
{
    public string Hash { get; set; }
    public long Size { get; set; }
}
```

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: 下载文件完整性验证

*For any* 下载的文件，如果提供了 SHA1 哈希值，验证下载文件的 SHA1 哈希应该与预期值匹配

**Validates: Requirements 3.4**

### Property 2: 依赖库路径一致性

*For any* 依赖库名称（Maven 坐标格式），通过 GetLibraryPath 方法计算的路径应该与实际下载位置一致

**Validates: Requirements 5.2**

### Property 3: 版本信息继承正确性

*For any* ModLoader 版本，如果它继承自原版 Minecraft，合并后的版本信息应该包含原版的所有必要属性（mainClass, arguments, assetIndex, downloads, javaVersion）

**Validates: Requirements 7.4**

### Property 4: 原生库平台过滤

*For any* 原生库列表和目标平台，提取的原生库应该只包含适用于当前平台和架构的库文件

**Validates: Requirements 5.6**

### Property 5: 并发下载任务数量限制

*For any* 并发下载任务集合和最大并发数 N，同时执行的下载任务数量不应超过 N

**Validates: Requirements 11.1**

### Property 6: 文件缓存命中率

*For any* 已下载且哈希匹配的文件，再次请求下载时应该跳过网络请求，直接使用本地文件

**Validates: Requirements 5.3, 11.5**

### Property 7: 错误重试机制

*For any* 下载失败的文件，如果配置了重试次数 N，系统应该最多尝试 N+1 次（初始尝试 + N 次重试）

**Validates: Requirements 3.3**

### Property 8: 进度报告单调性

*For any* 下载或安装过程，报告的进度值应该是单调递增的（0% → 100%），不应该出现进度倒退

**Validates: Requirements 3.2, 6.6**

### Property 9: 版本配置文件一致性

*For any* 已安装的 ModLoader 版本，版本配置文件（XianYuL.cfg）中的 ModLoaderType 和 ModLoaderVersion 应该与版本 ID 一致

**Validates: Requirements 7.5**

### Property 10: API 向后兼容性

*For any* 公共 API 方法，重构后的行为应该与重构前保持一致（相同输入产生相同输出）

**Validates: Requirements 13.2**

## Error Handling

### 异常类型层次结构

```csharp
public class MinecraftVersionException : Exception
{
    public MinecraftVersionException(string message) : base(message) { }
    public MinecraftVersionException(string message, Exception innerException) 
        : base(message, innerException) { }
}

public class DownloadException : MinecraftVersionException
{
    public string Url { get; set; }
    public int RetryCount { get; set; }
}

public class LibraryNotFoundException : MinecraftVersionException
{
    public string LibraryName { get; set; }
}

public class VersionNotFoundException : MinecraftVersionException
{
    public string VersionId { get; set; }
}

public class ModLoaderInstallException : MinecraftVersionException
{
    public string ModLoaderType { get; set; }
    public string ModLoaderVersion { get; set; }
}
```

### 错误处理策略

1. **网络错误**：自动重试 3 次，使用指数退避策略
2. **文件 I/O 错误**：记录详细日志，向上传播异常
3. **验证错误**：删除损坏的文件，重新下载
4. **配置错误**：提供清晰的错误消息和解决建议
5. **超时错误**：允许用户配置超时时间，提供取消机制

## Testing Strategy

### 单元测试策略

使用 xUnit 测试框架和 Moq 模拟框架。

**测试覆盖目标**：
- 代码覆盖率：≥ 80%
- 分支覆盖率：≥ 70%
- 每个公共方法至少有 3 个测试用例（正常、边界、异常）

**测试组织**：
```
XianYuLauncher.Tests/
├── Services/
│   ├── DownloadManagerTests.cs
│   ├── LibraryManagerTests.cs
│   ├── AssetManagerTests.cs
│   ├── VersionInfoManagerTests.cs
│   └── ModLoaderInstallers/
│       ├── FabricInstallerTests.cs
│       ├── ForgeInstallerTests.cs
│       └── NeoForgeInstallerTests.cs
└── Integration/
    ├── VersionDownloadIntegrationTests.cs
    └── ModLoaderInstallIntegrationTests.cs
```

### 集成测试策略

使用真实的测试数据和模拟的网络环境。

**测试场景**：
1. 完整的原版 Minecraft 下载流程
2. 完整的 Fabric 安装流程
3. 完整的 Forge 安装流程
4. 完整的 NeoForge 安装流程
5. 依赖库下载和原生库提取
6. 资源文件下载

### 性能测试

**测试指标**：
- 下载速度：≥ 10 MB/s（在良好网络条件下）
- 并发下载效率：4 个并发任务应该比单任务快 2-3 倍
- 内存使用：下载过程中内存增长 < 100 MB
- CPU 使用：下载过程中 CPU 使用率 < 30%

## Implementation Plan

### Phase 1: 基础设施准备（1-2 天）

1. 创建测试项目和测试基础设施
2. 配置 CI/CD 管道
3. 创建接口定义
4. 创建异常类型层次结构

### Phase 2: 下载管理器重构（2-3 天）

1. 实现 IDownloadManager 接口
2. 提取下载相关代码到 DownloadManager
3. 添加单元测试
4. 更新 MinecraftVersionService 使用新的 DownloadManager

### Phase 3: 依赖库管理器重构（2-3 天）

1. 实现 ILibraryManager 接口
2. 提取依赖库相关代码到 LibraryManager
3. 添加单元测试
4. 更新 MinecraftVersionService 使用新的 LibraryManager

### Phase 4: 资源管理器重构（2-3 天）

1. 实现 IAssetManager 接口
2. 提取资源相关代码到 AssetManager
3. 添加单元测试
4. 更新 MinecraftVersionService 使用新的 AssetManager

### Phase 5: 版本信息管理器重构（2-3 天）

1. 实现 IVersionInfoManager 接口
2. 提取版本信息相关代码到 VersionInfoManager
3. 添加单元测试
4. 更新 MinecraftVersionService 使用新的 VersionInfoManager

### Phase 6: ModLoader 安装器重构（3-4 天）

1. 实现 IModLoaderInstaller 接口
2. 为每种 ModLoader 创建独立的安装器类
3. 提取共同逻辑到基类或工具方法
4. 添加单元测试
5. 更新 MinecraftVersionService 使用新的安装器

### Phase 7: 集成测试和性能优化（2-3 天）

1. 编写集成测试
2. 运行性能测试
3. 识别性能瓶颈
4. 实施性能优化

### Phase 8: 文档和清理（1-2 天）

1. 更新 API 文档
2. 创建架构图和序列图
3. 编写迁移指南
4. 清理未使用的代码
5. 代码审查和最终测试

**总计时间估算：15-23 天**

## Migration Strategy

### 渐进式迁移

1. **保持向后兼容**：MinecraftVersionService 保留所有公共 API
2. **内部重构**：逐步将实现委托给新的服务类
3. **标记过时 API**：使用 [Obsolete] 特性标记计划废弃的 API
4. **提供迁移路径**：在文档中说明如何迁移到新 API

### 示例迁移代码

**旧代码**：
```csharp
await minecraftVersionService.DownloadVersionAsync(versionId, targetDirectory);
```

**新代码（推荐）**：
```csharp
var versionInfo = await versionInfoManager.GetVersionInfoAsync(versionId);
await downloadManager.DownloadFileAsync(versionInfo.Downloads.Client.Url, jarPath);
await libraryManager.DownloadLibrariesAsync(versionInfo, librariesDirectory);
await assetManager.DownloadAllAssetObjectsAsync(versionId, minecraftDirectory);
```

**过渡期代码（向后兼容）**：
```csharp
// MinecraftVersionService 内部实现
public async Task DownloadVersionAsync(string versionId, string targetDirectory)
{
    var versionInfo = await _versionInfoManager.GetVersionInfoAsync(versionId);
    await _downloadManager.DownloadFileAsync(versionInfo.Downloads.Client.Url, jarPath);
    await _libraryManager.DownloadLibrariesAsync(versionInfo, librariesDirectory);
    await _assetManager.DownloadAllAssetObjectsAsync(versionId, minecraftDirectory);
}
```

## Risk Assessment

### 高风险项

1. **破坏现有功能**：通过完整的测试覆盖来降低风险
2. **性能退化**：通过性能测试来监控和优化
3. **引入新 bug**：通过代码审查和测试来降低风险

### 中风险项

1. **重构时间超出预期**：采用渐进式方法，可以分阶段交付
2. **测试覆盖不足**：优先编写关键路径的测试

### 低风险项

1. **文档更新不及时**：在每个阶段完成后立即更新文档
2. **团队成员不熟悉新架构**：提供培训和文档支持

## Success Criteria

1. ✅ 所有单元测试通过（覆盖率 ≥ 80%）
2. ✅ 所有集成测试通过
3. ✅ 性能测试达标（下载速度、内存使用、CPU 使用）
4. ✅ 代码审查通过
5. ✅ 文档完整且准确
6. ✅ 向后兼容性验证通过
7. ✅ 用户验收测试通过
