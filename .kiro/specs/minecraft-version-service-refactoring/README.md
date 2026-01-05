# MinecraftVersionService 重构计划

## 📋 概述

这是一个针对 XianYuLauncher 项目中 MinecraftVersionService 的全面重构计划。当前该服务类已经被拆分为 9 个 partial 类文件，总计约 3700+ 行代码，但仍存在职责不清晰、测试覆盖不足、代码重复等问题。

## 🎯 重构目标

1. **提高代码质量**：遵循 SOLID 原则，提高代码的可维护性
2. **增加测试覆盖**：添加单元测试和集成测试，确保代码正确性
3. **优化性能**：改进下载和文件操作的性能
4. **改善错误处理**：提供更清晰的错误消息和更好的错误恢复机制
5. **保持向后兼容**：不破坏现有的公共 API

## 📁 文档结构

- **requirements.md** - 详细的需求文档，包含 15 个主要需求
- **design.md** - 技术设计文档，包含架构设计、接口定义、数据模型等
- **tasks.md** - 可执行的任务列表，分为 8 个阶段

## 🏗️ 当前架构

```
MinecraftVersionService (partial class - 9 个文件)
├── MinecraftVersionService.cs (主文件)
├── MinecraftVersionService.Base.cs
├── MinecraftVersionService.Download.cs
├── MinecraftVersionService.Fabric.cs
├── MinecraftVersionService.Forge.cs
├── MinecraftVersionService.NeoForge.cs
├── MinecraftVersionService.Optifine.cs
├── MinecraftVersionService.Processor.cs
├── MinecraftVersionService.Quilt.cs
└── MinecraftVersionService.Utils.cs
```

## 🎨 目标架构

```
IMinecraftVersionService (接口层)
├── MinecraftVersionService (协调器 - 保持向后兼容)
├── IDownloadManager (下载管理)
├── ILibraryManager (依赖库管理)
├── IAssetManager (资源管理)
├── IVersionInfoManager (版本信息管理)
└── IModLoaderInstaller (ModLoader 安装)
    ├── FabricInstaller
    ├── ForgeInstaller
    ├── NeoForgeInstaller
    ├── OptifineInstaller
    └── QuiltInstaller
```

## 📊 主要改进点

### 1. 服务拆分
- 将单一的大型服务类拆分为多个职责单一的服务
- 使用依赖注入来管理服务之间的依赖关系

### 2. 下载管理
- 统一的下载接口
- 自动重试机制（指数退避）
- SHA1 哈希验证
- 并发下载控制
- 进度报告

### 3. 依赖库管理
- 本地缓存检查
- 平台相关的原生库过滤
- 批量下载优化

### 4. 资源管理
- 资源索引验证
- 并发下载资源对象
- 增量更新支持

### 5. 版本信息管理
- 本地缓存优先
- 版本继承关系处理
- 版本配置文件管理

### 6. ModLoader 安装
- 统一的安装接口
- 策略模式选择安装器
- 共同逻辑复用

## 🧪 测试策略

### 单元测试
- 使用 xUnit 和 Moq
- 目标代码覆盖率：≥ 80%
- 每个公共方法至少 3 个测试用例

### 集成测试
- 测试完整的下载和安装流程
- 使用模拟的网络环境

### 性能测试
- 下载速度：≥ 10 MB/s
- 并发效率：4 个任务比单任务快 2-3 倍
- 内存增长：< 100 MB
- CPU 使用：< 30%

## 📅 实施计划

| 阶段 | 任务 | 预计时间 |
|------|------|----------|
| Phase 1 | 基础设施准备 | 1-2 天 |
| Phase 2 | 下载管理器重构 | 2-3 天 |
| Phase 3 | 依赖库管理器重构 | 2-3 天 |
| Phase 4 | 资源管理器重构 | 2-3 天 |
| Phase 5 | 版本信息管理器重构 | 2-3 天 |
| Phase 6 | ModLoader 安装器重构 | 3-4 天 |
| Phase 7 | 集成测试和性能优化 | 2-3 天 |
| Phase 8 | 文档和清理 | 1-2 天 |
| **总计** | | **15-23 天** |

## 🔄 迁移策略

### 渐进式迁移
1. 保持 MinecraftVersionService 的所有公共 API
2. 内部实现逐步委托给新的服务类
3. 使用 [Obsolete] 标记计划废弃的 API
4. 提供详细的迁移指南

### 向后兼容性保证
- 所有公共 API 签名保持不变
- 所有公共 API 行为保持不变
- 每个阶段完成后运行完整测试套件

## ⚠️ 风险评估

### 高风险
- ❌ 破坏现有功能 → ✅ 通过完整测试覆盖降低
- ❌ 性能退化 → ✅ 通过性能测试监控

### 中风险
- ⚠️ 时间超出预期 → ✅ 采用渐进式方法，分阶段交付
- ⚠️ 测试覆盖不足 → ✅ 优先编写关键路径测试

### 低风险
- ℹ️ 文档更新不及时 → ✅ 每阶段完成后立即更新
- ℹ️ 团队不熟悉新架构 → ✅ 提供培训和文档

## ✅ 成功标准

1. ✅ 所有单元测试通过（覆盖率 ≥ 80%）
2. ✅ 所有集成测试通过
3. ✅ 性能测试达标
4. ✅ 代码审查通过
5. ✅ 文档完整且准确
6. ✅ 向后兼容性验证通过
7. ✅ 用户验收测试通过

## 🚀 如何开始

1. **阅读文档**：
   - 先阅读 `requirements.md` 了解需求
   - 再阅读 `design.md` 了解技术设计
   - 最后查看 `tasks.md` 了解具体任务

2. **准备环境**：
   - 创建测试项目
   - 配置 CI/CD 管道
   - 安装必要的工具和依赖

3. **开始重构**：
   - 按照 Phase 1 → Phase 8 的顺序执行
   - 每完成一个 Phase 运行所有测试
   - 使用 Git 分支管理每个 Phase 的开发

4. **持续验证**：
   - 每次提交前运行测试
   - 定期进行代码审查
   - 监控性能指标

## 📞 联系方式

如有任何问题或建议，请通过以下方式联系：
- 创建 GitHub Issue
- 在团队讨论区发帖
- 直接联系项目维护者

## 📝 更新日志

- 2025-01-06: 创建初始重构计划文档
