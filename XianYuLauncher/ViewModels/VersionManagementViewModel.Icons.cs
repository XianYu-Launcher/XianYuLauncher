using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Helpers;
using XianYuLauncher.ViewModels;
using XianYuLauncher.Models.VersionManagement;


namespace XianYuLauncher.ViewModels;

public partial class VersionManagementViewModel
{
    #region 延迟加载图标方法
    
    /// <summary>
    /// 延迟加载资源包图标（仅在切换到资源包 Tab 时调用）
    /// </summary>
    private async Task LoadResourcePackIconsAsync()
    {
        System.Diagnostics.Debug.WriteLine("[延迟加载] 开始加载资源包图标");
        
        var loadTasks = new List<Task>();
        foreach (var resourcePackInfo in ResourcePacks)
        {
            if (string.IsNullOrEmpty(resourcePackInfo.Icon))
            {
                loadTasks.Add(LoadResourceIconAsync(icon => resourcePackInfo.Icon = icon, resourcePackInfo.FilePath, "resourcepack"));
            }
        }
        
        if (loadTasks.Count > 0)
        {
            await Task.WhenAll(loadTasks);
            System.Diagnostics.Debug.WriteLine($"[延迟加载] 完成加载 {loadTasks.Count} 个资源包图标");
        }
    }
    
    /// <summary>
    /// 延迟加载地图图标（仅在切换到地图 Tab 时调用）
    /// </summary>
    private async Task LoadMapIconsAsync()
    {
        System.Diagnostics.Debug.WriteLine("[延迟加载] 开始加载地图图标");
        
        var loadTasks = new List<Task>();
        foreach (var mapInfo in Maps)
        {
            if (string.IsNullOrEmpty(mapInfo.Icon))
            {
                loadTasks.Add(LoadMapIconAsync(mapInfo, mapInfo.FilePath));
            }
        }
        
        if (loadTasks.Count > 0)
        {
            await Task.WhenAll(loadTasks);
            System.Diagnostics.Debug.WriteLine($"[延迟加载] 完成加载 {loadTasks.Count} 个地图图标");
        }
    }
    
    #endregion
}
