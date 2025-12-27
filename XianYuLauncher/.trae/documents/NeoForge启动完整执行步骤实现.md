# NeoForge启动完整执行步骤实现

## 1. 问题分析

当前NeoForge实现缺少了对`install_profile.json`中`processors`字段的处理，这是NeoForge启动所必需的步骤。根据用户提供的信息，需要实现以下功能：

- 解析`install_profile.json`中的`processors`字段
- 按顺序执行`processors`，跳过`sides`为`server`的处理器
- 执行`DOWNLOAD_MOJMAPS`任务，获取映射文件
- 执行`PROCESS_MINECRAFT_JAR`任务，处理Minecraft JAR文件
- 在启动时避免添加`neoforge-universal.jar`到classpath

## 2. 实现计划

### 2.1 修改`MinecraftVersionService.cs`

1. **扩展`ExtractNeoForgeInstallerFiles`方法**：
   - 提取完整的`data`目录，而不仅仅是`data/client.lzma`
   - 保留`installer.jar`文件，用于后续处理器执行

2. **添加`ProcessNeoForgeInstallProfile`方法**：
   - 解析`install_profile.json`中的`processors`字段
   - 过滤出`sides`不为`server`的处理器
   - 按顺序执行处理器

3. **添加`ExecuteProcessor`方法**：
   - 下载所需的`installertools`
   - 执行Java命令来运行处理器
   - 处理处理器的参数替换（如`{INSTALLER}`、`{ROOT}`、`{SIDE}`等）

4. **添加`GetMainClassFromJar`方法**：
   - 从`installertools-fatjar.jar`中读取`META-INF/MANIFEST.MF`文件
   - 获取`Main-Class`字段的值

5. **修改`DownloadNeoForgeVersionAsync`方法**：
   - 在下载依赖库后，调用`ProcessNeoForgeInstallProfile`方法
   - 保存处理后的文件到正确位置

### 2.2 修改启动逻辑

1. **修改`启动ViewModel.cs`中的`BuildCommandLineArgs`方法**：
   - 避免将`neoforge-universal.jar`添加到classpath

## 3. 执行流程

1. **下载NeoForge安装器**：从NeoForge Maven仓库下载installer.jar
2. **解包installer.jar**：提取`install_profile.json`和完整的`data`目录
3. **解析processors**：过滤出客户端处理器
4. **下载installertools**：下载所需的installertools-fatjar.jar
5. **执行DOWNLOAD_MOJMAPS**：获取Minecraft映射文件
6. **执行PROCESS_MINECRAFT_JAR**：处理Minecraft JAR文件
7. **合并版本JSON**：生成最终的version.json文件
8. **启动游戏**：使用处理后的文件启动游戏，避免添加neoforge-universal.jar

## 4. 技术细节

1. **处理器执行命令格式**：
   ```
   java -cp {installertools路径} {主类名} {args字段参数}
   ```

2. **参数替换规则**：
   - `{INSTALLER}`：installer.jar的完整路径
   - `{ROOT}`：Minecraft根目录
   - `{SIDE}`：client
   - `{MOJMAPS}`：映射文件保存路径
   - `{PATCHED}`：处理后的JAR文件保存路径
   - `{BINPATCH}`：二进制补丁文件路径

3. **主类获取**：从`installertools-fatjar.jar`的`META-INF/MANIFEST.MF`文件中读取`Main-Class`字段

## 5. 预期结果

- NeoForge安装过程完整，包括所有处理器执行
- 生成正确的处理后的JAR文件
- 游戏能够正常启动，不再出现因缺少处理步骤导致的错误
- 启动时不再添加`neoforge-universal.jar`到classpath

## 6. 测试策略

1. 测试不同版本的NeoForge安装
2. 验证处理器执行日志
3. 检查生成的文件完整性
4. 测试游戏启动功能

这个实现计划将确保NeoForge启动的完整执行步骤得到正确实现，解决当前实现中的缺失环节。