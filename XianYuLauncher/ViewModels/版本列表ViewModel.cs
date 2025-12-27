using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.System;
using XMCL2025.Core.Contracts.Services;
using XMCL2025.Contracts.Services;
using XMCL2025.Helpers;

namespace XMCL2025.ViewModels;

public partial class 版本列表ViewModel : ObservableRecipient
{
    private readonly IMinecraftVersionService _minecraftVersionService;
    private readonly IFileService _fileService;

    /// <summary>
    /// 版本信息模型
    /// </summary>
    public class VersionInfoItem
    {
        /// <summary>
        /// 版本名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 版本类型（Release/Snapshot/Beta/Alpha）
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// 安装日期
        /// </summary>
        public DateTime InstallDate { get; set; }

        /// <summary>
        /// 版本号
        /// </summary>
        public string VersionNumber { get; set; } = string.Empty;

        /// <summary>
        /// 版本文件夹路径
        /// </summary>
        public string Path { get; set; } = string.Empty;
    }

    [ObservableProperty]
    private ObservableCollection<VersionInfoItem> _versions = new();

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public 版本列表ViewModel(IMinecraftVersionService minecraftVersionService, IFileService fileService)
    {
        _minecraftVersionService = minecraftVersionService;
        _fileService = fileService;
        
        // 订阅Minecraft路径变化事件
        _fileService.MinecraftPathChanged += OnMinecraftPathChanged;
        
        InitializeAsync().ConfigureAwait(false);
    }
    
    /// <summary>
    /// 当Minecraft路径变化时触发
    /// </summary>
    private async void OnMinecraftPathChanged(object? sender, string newPath)
    {
        await LoadVersionsAsync();
    }

    private async Task InitializeAsync()
    {
        await LoadVersionsAsync();
    }

    [RelayCommand]
    private async Task LoadVersionsAsync()
    {
        IsLoading = true;
        StatusMessage = "VersionListPage_LoadingVersionsText".GetLocalized();

        try
        {
            // 获取已安装的版本列表
            var installedVersions = await _minecraftVersionService.GetInstalledVersionsAsync();
            var minecraftPath = _fileService.GetMinecraftDataPath();
            var versionsPath = Path.Combine(minecraftPath, "versions");

            Versions.Clear();

            foreach (var versionName in installedVersions)
            {
                var versionDir = Path.Combine(versionsPath, versionName);
                var versionJsonPath = Path.Combine(versionDir, $"{versionName}.json");

                if (Directory.Exists(versionDir) && File.Exists(versionJsonPath))
                {
                    // 获取版本类型和版本号
                    string type = "Release";
                    string versionNumber = versionName;

                    // 尝试从版本名称中提取版本类型
                    if (versionName.Contains("-snapshot", StringComparison.OrdinalIgnoreCase))
                    {
                        type = "Snapshot";
                    }
                    else if (versionName.Contains("-beta", StringComparison.OrdinalIgnoreCase))
                    {
                        type = "Beta";
                    }
                    else if (versionName.Contains("-alpha", StringComparison.OrdinalIgnoreCase))
                    {
                        type = "Alpha";
                    }
                    else if (versionName.StartsWith("fabric-"))
                    {
                        type = "Fabric";
                        // 提取实际Minecraft版本号
                        versionNumber = versionName.Substring("fabric-".Length);
                        if (versionNumber.Contains("-"))
                        {
                            versionNumber = versionNumber.Split('-')[0];
                        }
                    }
                    else if (versionName.StartsWith("forge-"))
                    {
                        type = "Forge";
                        // 提取实际Minecraft版本号
                        versionNumber = versionName.Substring("forge-".Length);
                        if (versionNumber.Contains("-"))
                        {
                            versionNumber = versionNumber.Split('-')[0];
                        }
                    }

                    // 获取安装日期（使用文件夹创建日期）
                    var dirInfo = new DirectoryInfo(versionDir);
                    var installDate = dirInfo.CreationTime;

                    // 创建版本信息项
                    var versionItem = new VersionInfoItem
                    {
                        Name = versionName,
                        Type = type,
                        InstallDate = installDate,
                        VersionNumber = versionNumber,
                        Path = versionDir
                    };

                    Versions.Add(versionItem);
                }
            }

            // 按安装日期降序排序
            Versions = new ObservableCollection<VersionInfoItem>(Versions.OrderByDescending(v => v.InstallDate));

            StatusMessage = Versions.Count > 0 ? $"{"VersionListPage_FoundVersionsText".GetLocalized()} {Versions.Count} {"VersionListPage_InstalledVersionsText".GetLocalized()}" : "VersionListPage_NoVersionsFoundText".GetLocalized();
        }
        catch (Exception ex)
        {
            StatusMessage = $"{"VersionListPage_LoadFailedText".GetLocalized()}: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 打开版本文件夹命令
    /// </summary>
    [RelayCommand]
    private async Task OpenFolderAsync(VersionInfoItem version)
    {
        if (version == null || string.IsNullOrEmpty(version.Path))
        {
            StatusMessage = "VersionListPage_InvalidVersionPathText".GetLocalized();
            return;
        }

        try
        {
            var folder = await StorageFolder.GetFolderFromPathAsync(version.Path);
            await Launcher.LaunchFolderAsync(folder);
            StatusMessage = $"{"VersionListPage_FolderOpenedText".GetLocalized()} {version.Name} {"VersionListPage_FolderText".GetLocalized()}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"{"VersionListPage_OpenFolderFailedText".GetLocalized()}: {ex.Message}";
        }
    }

    /// <summary>
    /// 删除版本命令
    /// </summary>
    [RelayCommand]
    private async Task DeleteVersionAsync(VersionInfoItem version)
    {
        if (version == null || string.IsNullOrEmpty(version.Path))
        {
            StatusMessage = "VersionListPage_InvalidVersionInfoText".GetLocalized();
            return;
        }

        try
        {
            // 检查版本文件夹是否存在
            if (!Directory.Exists(version.Path))
            {
                StatusMessage = $"{"VersionListPage_VersionDoesNotExistText".GetLocalized()} {version.Name}";
                return;
            }

            // 创建确认对话框
            var dialog = new ContentDialog
            {
                Title = "VersionListPage_ConfirmDeleteText".GetLocalized(),
                Content = $"{"VersionListPage_ConfirmDeleteContentText".GetLocalized()} {version.Name} {"VersionListPage_ConfirmDeleteWarningText".GetLocalized()}",
                PrimaryButtonText = "VersionListPage_DeleteText".GetLocalized(),
                CloseButtonText = "VersionListPage_CancelText".GetLocalized(),
                DefaultButton = ContentDialogButton.Close
            };

            // 设置XamlRoot
            if (App.MainWindow.Content is FrameworkElement rootElement)
            {
                dialog.XamlRoot = rootElement.XamlRoot;
            }

            // 显示对话框
            var result = await dialog.ShowAsync();

            // 如果用户确认删除
            if (result == ContentDialogResult.Primary)
            {
                // 删除版本文件夹
                Directory.Delete(version.Path, true);
                
                // 从列表中移除
                Versions.Remove(version);
                
                // 更新状态信息
                StatusMessage = $"{"VersionListPage_VersionDeletedText".GetLocalized()} {version.Name}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"{"VersionListPage_DeleteFailedText".GetLocalized()}: {ex.Message}";
        }
    }
}