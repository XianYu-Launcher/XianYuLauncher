using System;
using System.Collections.Generic;
using System.IO;
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
/// Fabric ModLoader安装器
/// </summary>
public class FabricInstaller : ModLoaderInstallerBase
{
    private readonly HttpClient _httpClient;
    
    /// <summary>
    /// Fabric Meta API基础URL
    /// </summary>
    private const string FabricMetaApiUrl = "https://meta.fabricmc.net/v2";
    
    /// <inheritdoc/>
    public override string ModLoaderType => "Fabric";

    public FabricInstaller(
        IDownloadManager downloadManager,
        ILibraryManager libraryManager,
        IVersionInfoManager versionInfoManager,
        ILogger<FabricInstaller> logger)
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
        Logger.LogInformation("开始安装Fabric: {FabricVersion} for Minecraft {MinecraftVersion}",
            modLoaderVersion, minecraftVersionId);

        try
        {
            // 1. 生成版本ID和创建目录
            var versionId = GetVersionId(minecraftVersionId, modLoaderVersion, customVersionName);
            var versionDirectory = CreateVersionDirectory(minecraftDirectory, versionId);
            var librariesDirectory = Path.Combine(minecraftDirectory, "libraries");

            progressCallback?.Invoke(5);

            // 2. 获取原版Minecraft版本信息
            Logger.LogInformation("获取原版Minecraft版本信息: {MinecraftVersion}", minecraftVersionId);
            var originalVersionInfo = await VersionInfoManager.GetVersionInfoAsync(
                minecraftVersionId,
                minecraftDirectory,
                allowNetwork: true,
                cancellationToken);

            progressCallback?.Invoke(10);

            // 3. 获取Fabric Profile
            Logger.LogInformation("获取Fabric Profile");
            var fabricProfile = await GetFabricProfileAsync(minecraftVersionId, modLoaderVersion, cancellationToken);

            progressCallback?.Invoke(15);

            // 4. 保存版本配置
            await SaveVersionConfigAsync(versionDirectory, minecraftVersionId, modLoaderVersion);

            // 5. 下载原版Minecraft JAR
            Logger.LogInformation("下载Minecraft JAR");
            await DownloadMinecraftJarAsync(
                versionDirectory,
                versionId,
                originalVersionInfo,
                p => ReportProgress(progressCallback, p, 15, 35),
                cancellationToken);

            progressCallback?.Invoke(35);

            // 6. 下载Fabric库文件
            Logger.LogInformation("下载Fabric库文件");
            var fabricLibraries = ParseFabricLibraries(fabricProfile);
            await DownloadModLoaderLibrariesAsync(
                fabricLibraries,
                librariesDirectory,
                p => ReportProgress(progressCallback, p, 35, 80),
                cancellationToken);

            progressCallback?.Invoke(80);

            // 7. 生成Fabric版本JSON
            Logger.LogInformation("生成Fabric版本JSON");
            var fabricVersionJson = CreateFabricVersionJson(versionId, minecraftVersionId, fabricProfile);
            await SaveVersionJsonAsync(versionDirectory, versionId, fabricVersionJson);

            progressCallback?.Invoke(100);

            Logger.LogInformation("Fabric安装完成: {VersionId}", versionId);
            return versionId;
        }
        catch (OperationCanceledException)
        {
            Logger.LogWarning("Fabric安装已取消");
            throw;
        }
        catch (Exception ex) when (ex is not ModLoaderInstallException)
        {
            Logger.LogError(ex, "Fabric安装失败");
            throw new ModLoaderInstallException(
                $"Fabric安装失败: {ex.Message}",
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
            var url = $"{FabricMetaApiUrl}/versions/loader/{minecraftVersionId}";
            var response = await _httpClient.GetStringAsync(url, cancellationToken);
            var versions = JsonConvert.DeserializeObject<List<FabricLoaderVersion>>(response);

            return versions?.Select(v => v.Loader?.Version ?? string.Empty)
                .Where(v => !string.IsNullOrEmpty(v))
                .ToList() ?? new List<string>();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "获取Fabric版本列表失败: {MinecraftVersion}", minecraftVersionId);
            return new List<string>();
        }
    }

    #region 私有方法

    /// <summary>
    /// 获取Fabric Profile
    /// </summary>
    private async Task<JObject> GetFabricProfileAsync(
        string minecraftVersionId,
        string fabricVersion,
        CancellationToken cancellationToken)
    {
        var url = $"{FabricMetaApiUrl}/versions/loader/{minecraftVersionId}/{fabricVersion}/profile/json";
        Logger.LogDebug("获取Fabric Profile: {Url}", url);

        var response = await _httpClient.GetStringAsync(url, cancellationToken);
        var profile = JObject.Parse(response);

        if (profile == null)
        {
            throw new ModLoaderInstallException(
                "无法解析Fabric Profile",
                ModLoaderType,
                fabricVersion,
                minecraftVersionId,
                "获取Profile");
        }

        return profile;
    }

    /// <summary>
    /// 解析Fabric库列表
    /// </summary>
    private List<ModLoaderLibrary> ParseFabricLibraries(JObject fabricProfile)
    {
        var libraries = new List<ModLoaderLibrary>();
        var librariesArray = fabricProfile["libraries"] as JArray;

        if (librariesArray == null)
        {
            return libraries;
        }

        foreach (var lib in librariesArray)
        {
            var name = lib["name"]?.ToString();
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            libraries.Add(new ModLoaderLibrary
            {
                Name = name,
                Url = lib["url"]?.ToString() ?? "https://maven.fabricmc.net/",
                Sha1 = lib["sha1"]?.ToString()
            });
        }

        return libraries;
    }

    /// <summary>
    /// 创建Fabric版本JSON
    /// </summary>
    private object CreateFabricVersionJson(string versionId, string minecraftVersionId, JObject fabricProfile)
    {
        // 从Fabric Profile中提取必要信息
        var mainClass = fabricProfile["mainClass"]?.ToString() ?? "net.fabricmc.loader.launch.knot.KnotClient";
        var arguments = fabricProfile["arguments"];
        var libraries = fabricProfile["libraries"];

        return new
        {
            id = versionId,
            inheritsFrom = minecraftVersionId,
            type = "release",
            mainClass = mainClass,
            arguments = arguments,
            libraries = libraries,
            releaseTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            time = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        };
    }

    #endregion

    #region 内部类

    private class FabricLoaderVersion
    {
        [JsonProperty("loader")]
        public FabricLoader? Loader { get; set; }
    }

    private class FabricLoader
    {
        [JsonProperty("version")]
        public string? Version { get; set; }
    }

    #endregion
}
