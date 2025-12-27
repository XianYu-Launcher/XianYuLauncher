# 实现Forge版本列表获取功能

## 实现步骤

1. **修改IDownloadSource接口**
   - 添加`GetForgeVersionsUrl`方法，用于获取Forge版本列表URL
   - 添加`GetForgeInstallerUrl`方法，用于获取Forge安装包URL

2. **在OfficialDownloadSource中实现Forge相关方法**
   - 实现`GetForgeVersionsUrl`，返回官方Forge版本列表URL：`https://files.minecraftforge.net/net/minecraftforge/forge/promotions_slim.json`
   - 实现`GetForgeInstallerUrl`，根据版本构建官方Forge安装包URL

3. **在BmclapiDownloadSource中实现Forge相关方法**
   - 实现`GetForgeVersionsUrl`，返回BMCLAPI Forge版本列表URL：`https://bmclapi2.bangbang93.com/forge/minecraft/{MC版本}`
   - 实现`GetForgeInstallerUrl`，根据版本构建BMCLAPI Forge安装包URL

4. **创建ForgeService类**
   - 实现`GetForgeVersionsAsync`方法，根据当前下载源获取Forge版本列表
   - 处理官方源返回的promotions_slim.json格式，提取适配当前Minecraft版本的Forge版本
   - 处理BMCLAPI返回的JSON格式，提取version字段
   - 添加调试输出，显示当前下载源和请求URL

5. **修改ModLoader选择ViewModel**
   - 添加ForgeService依赖注入
   - 实现`LoadForgeVersionsAsync`方法，调用ForgeService获取版本列表
   - 在`ExpandModLoaderAsync`方法中调用`LoadForgeVersionsAsync`获取Forge版本

6. **在App.xaml.cs中注册ForgeService**
   - 添加ForgeService的依赖注入配置

## 技术要点

- 遵循现有代码架构，使用DownloadSourceFactory获取当前下载源
- 使用HttpClient进行网络请求，处理不同下载源返回的不同数据格式
- 实现版本匹配逻辑，确保只显示适配当前Minecraft版本的Forge版本
- 添加详细的调试输出，便于调试和监控
- 使用MVVM架构，确保代码结构清晰，易于维护

## 文件修改清单

- `XMCL2025/Core/Services/DownloadSource/IDownloadSource.cs` - 添加Forge相关方法
- `XMCL2025/Core/Services/DownloadSource/OfficialDownloadSource.cs` - 实现Forge相关方法
- `XMCL2025/Core/Services/DownloadSource/BmclapiDownloadSource.cs` - 实现Forge相关方法
- `XMCL2025/Core/Services/ForgeService.cs` - 新建文件，实现Forge版本获取逻辑
- `XMCL2025/ViewModels/ModLoader选择ViewModel.cs` - 添加Forge版本加载逻辑
- `XMCL2025/App.xaml.cs` - 注册ForgeService

## 预期效果

- 当用户选择Forge作为Mod Loader时，自动根据当前Minecraft版本和下载源获取适配的Forge版本列表
- 支持官方源和BMCLAPI两种下载源
- 显示详细的调试信息，包括当前下载源和请求URL
- 版本列表按从新到旧排序，默认选择第一个版本
- 与现有代码结构保持一致，易于扩展和维护