using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Exceptions;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services.ModLoaderInstallers;

/// <summary>
/// Optifine ModLoader安装器
/// </summary>
public class OptifineInstaller : ModLoaderInstallerBase
{
    private readonly HttpClient _httpClient;
    
    /// <inheritdoc/>
    public override string ModLoaderType => "Optifine";

    public OptifineInstaller(
        IDownloadManager downloadManager,
        ILibraryManager libraryManager,
        IVersionInfoManager versionInfoManager,
        ILogger<OptifineInstaller> logger)
        : base(downloadManager, libraryManager, versionInfoManager, logger)
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "XianYuLauncher/1.0");
    }

    /// <inheritdoc/>
    public override async Task<string> InstallAsync(
        string minecraftVersionId,
        string modLoaderVersion,
        string minecraftDirectory,
        Action<double>? progressCallback = null,
        CancellationToken cancellationToken = default,
        string? customVersionName = null)
    {
        Logger.LogInformation("开始安装Optifine: {OptifineVersion} for Minecraft {MinecraftVersion}",
            modLoaderVersion, minecraftVersionId);

        string? cacheDirectory = null;
        string? optifineJarPath = null;

        try
        {
            // 1. 生成版本ID和创建目录
            var versionId = GetVersionId(minecraftVersionId, modLoaderVersion, customVersionName);
            var versionDirectory = CreateVersionDirectory(minecraftDirectory, versionId);
            var librariesDirectory = Path.Combine(minecraftDirectory, "libraries");

            progressCallback?.Invoke(5);

            // 2. 保存版本配置
            await SaveVersionConfigAsync(versionDirectory, minecraftVersionId, modLoaderVersion);

            // 3. 获取原版Minecraft版本信息
            Logger.LogInformation("获取原版Minecraft版本信息: {MinecraftVersion}", minecraftVersionId);
            var originalVersionInfo = await VersionInfoManager.GetVersionInfoAsync(
                minecraftVersionId,
                minecraftDirectory,
                allowNetwork: true,
                cancellationToken);

            progressCallback?.Invoke(10);

            // 4. 下载原版Minecraft JAR
            Logger.LogInformation("下载Minecraft JAR");
            var originalJarPath = Path.Combine(versionDirectory, $"{versionId}.jar");
            await DownloadMinecraftJarAsync(
                versionDirectory,
                versionId,
                originalVersionInfo,
                p => ReportProgress(progressCallback, p, 10, 40),
                cancellationToken);

            progressCallback?.Invoke(40);

            // 5. 下载Optifine JAR（需要用户提供或从BMCLAPI获取）
            Logger.LogInformation("准备Optifine JAR");
            cacheDirectory = Path.Combine(Path.GetTempPath(), "XianYuLauncher", "cache", "optifine");
            Directory.CreateDirectory(cacheDirectory);
            
            // Optifine下载URL（使用BMCLAPI）
            optifineJarPath = Path.Combine(cacheDirectory, $"OptiFine_{minecraftVersionId}_{modLoaderVersion}.jar");
            var optifineUrl = GetOptifineDownloadUrl(minecraftVersionId, modLoaderVersion);
            
            var downloadResult = await DownloadManager.DownloadFileAsync(
                optifineUrl,
                optifineJarPath,
                null,
                p => ReportProgress(progressCallback, p, 40, 60),
                cancellationToken);

            if (!downloadResult.Success)
            {
                throw new ModLoaderInstallException(
                    $"下载Optifine失败: {downloadResult.ErrorMessage}",
                    ModLoaderType,
                    modLoaderVersion,
                    minecraftVersionId,
                    "下载Optifine",
                    downloadResult.Exception);
            }

            progressCallback?.Invoke(60);

            // 6. 将Optifine复制到libraries目录
            Logger.LogInformation("安装Optifine库文件");
            var optifineLibraryPath = GetOptifineLibraryPath(minecraftVersionId, modLoaderVersion, librariesDirectory);
            var optifineLibraryDir = Path.GetDirectoryName(optifineLibraryPath);
            if (!string.IsNullOrEmpty(optifineLibraryDir))
            {
                Directory.CreateDirectory(optifineLibraryDir);
            }
            File.Copy(optifineJarPath, optifineLibraryPath, overwrite: true);

            progressCallback?.Invoke(70);

            // 7. 修补Minecraft JAR（将Optifine内容合并到JAR中）
            Logger.LogInformation("修补Minecraft JAR");
            await PatchMinecraftJarAsync(originalJarPath, optifineJarPath, cancellationToken);

            progressCallback?.Invoke(85);

            // 8. 生成版本JSON
            Logger.LogInformation("生成Optifine版本JSON");
            var optifineVersionInfo = CreateOptifineVersionInfo(
                versionId, minecraftVersionId, modLoaderVersion, originalVersionInfo);
            await SaveVersionJsonAsync(versionDirectory, versionId, optifineVersionInfo);

            progressCallback?.Invoke(100);

            Logger.LogInformation("Optifine安装完成: {VersionId}", versionId);
            return versionId;
        }
        catch (OperationCanceledException)
        {
            Logger.LogWarning("Optifine安装已取消");
            throw;
        }
        catch (Exception ex) when (ex is not ModLoaderInstallException)
        {
            Logger.LogError(ex, "Optifine安装失败");
            throw new ModLoaderInstallException(
                $"Optifine安装失败: {ex.Message}",
                ModLoaderType,
                modLoaderVersion,
                minecraftVersionId,
                innerException: ex);
        }
    }

    /// <inheritdoc/>
    public override async Task<List<string>> GetAvailableVersionsAsync(
        string minecraftVersionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 使用BMCLAPI获取Optifine版本列表
            var url = $"https://bmclapi2.bangbang93.com/optifine/{minecraftVersionId}";
            var response = await _httpClient.GetStringAsync(url, cancellationToken);
            var versions = JsonConvert.DeserializeObject<List<OptifineVersionInfo>>(response);

            return versions?.Select(v => $"{v.Type}_{v.Patch}")
                .Where(v => !string.IsNullOrEmpty(v))
                .ToList() ?? new List<string>();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "获取Optifine版本列表失败: {MinecraftVersion}", minecraftVersionId);
            return new List<string>();
        }
    }

    #region 私有方法

    private string GetOptifineDownloadUrl(string minecraftVersionId, string optifineVersion)
    {
        // 使用BMCLAPI下载Optifine
        return $"https://bmclapi2.bangbang93.com/optifine/{minecraftVersionId}/{optifineVersion.Replace("_", "/")}";
    }

    private string GetOptifineLibraryPath(string minecraftVersionId, string optifineVersion, string librariesDirectory)
    {
        return Path.Combine(
            librariesDirectory,
            "optifine",
            "OptiFine",
            $"{minecraftVersionId}_{optifineVersion}",
            $"OptiFine-{minecraftVersionId}_{optifineVersion}.jar");
    }

    private async Task PatchMinecraftJarAsync(string minecraftJarPath, string optifineJarPath, CancellationToken cancellationToken)
    {
        // 创建临时目录用于合并
        var tempDir = Path.Combine(Path.GetTempPath(), $"optifine_patch_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // 解压原版JAR
            await Task.Run(() => ZipFile.ExtractToDirectory(minecraftJarPath, tempDir, overwriteFiles: true), cancellationToken);

            // 解压Optifine JAR并覆盖
            using (var optifineArchive = ZipFile.OpenRead(optifineJarPath))
            {
                foreach (var entry in optifineArchive.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // 跳过META-INF目录
                    if (entry.FullName.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase))
                        continue;
                    
                    if (string.IsNullOrEmpty(entry.Name)) continue;

                    var destinationPath = Path.Combine(tempDir, entry.FullName);
                    var destinationDir = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(destinationDir))
                    {
                        Directory.CreateDirectory(destinationDir);
                    }
                    entry.ExtractToFile(destinationPath, overwrite: true);
                }
            }

            // 删除原JAR并重新打包
            File.Delete(minecraftJarPath);
            await Task.Run(() => ZipFile.CreateFromDirectory(tempDir, minecraftJarPath), cancellationToken);
        }
        finally
        {
            // 清理临时目录
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
            catch { /* 忽略清理错误 */ }
        }
    }

    private object CreateOptifineVersionInfo(
        string versionId,
        string minecraftVersionId,
        string optifineVersion,
        VersionInfo originalVersionInfo)
    {
        var optifineLibraryName = $"optifine:OptiFine:{minecraftVersionId}_{optifineVersion}";

        return new
        {
            id = versionId,
            inheritsFrom = minecraftVersionId,
            type = "release",
            mainClass = "net.minecraft.client.main.Main",
            libraries = new[]
            {
                new
                {
                    name = optifineLibraryName
                }
            },
            releaseTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            time = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        };
    }

    #endregion

    #region 内部类

    private class OptifineVersionInfo
    {
        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("patch")]
        public string? Patch { get; set; }

        [JsonProperty("filename")]
        public string? Filename { get; set; }
    }

    #endregion
}
