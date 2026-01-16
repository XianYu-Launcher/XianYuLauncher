using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Helpers;
using Newtonsoft.Json;

namespace XianYuLauncher.ViewModels;

public partial class MultiplayerViewModel : ObservableRecipient, INavigationAware
{
    private readonly INavigationService _navigationService;
    private readonly TerracottaService _terracottaService;
    
    // 保存启动的terracotta进程引用
    private Process? _terracottaProcess;
    
    // 保存临时文件路径，用于进程终止后清理
    private string? _tempFilePath;
    
    // HttpClient用于发送HTTP请求
    private readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    
    // CancellationTokenSource用于控制轮询的取消
    private CancellationTokenSource? _pollingCts;
    // 用于JoinGame方法的CancellationTokenSource
    private CancellationTokenSource? _joinGameCts;
    
    // 保存陶瓦联机进程的端口号，用于发送HTTP请求
    private string? _terracottaPort;
    
    // 保存弹窗引用，用于在检测到房间时关闭
    private Microsoft.UI.Xaml.Controls.ContentDialog? _statusDialog;
    
    // 用于实时显示轮询结果的字段
    private string _pollingResult = "";
    
    // 用于显示状态的属性，绑定到弹窗内容
    public string PollingResult
    {
        get => _pollingResult;
        set => SetProperty(ref _pollingResult, value);
    }
    
    // FileService用于获取文件路径
    private readonly IFileService _fileService;

    public MultiplayerViewModel(INavigationService navigationService, IFileService fileService, ILocalSettingsService localSettingsService)
    {
        _navigationService = navigationService;
        _fileService = fileService;
        _terracottaService = new TerracottaService(localSettingsService);
    }
    
    /// <summary>
    /// 获取当前活跃角色名称
    /// </summary>
    /// <returns>当前角色名称，如果没有找到则返回空字符串</returns>
    private string GetCurrentProfileName()
    {
        try
        {
            // 获取角色数据文件路径
            string profilesFilePath = Path.Combine(_fileService.GetMinecraftDataPath(), "profiles.json");
            
            if (File.Exists(profilesFilePath))
            {
                string json = File.ReadAllText(profilesFilePath);
                var profiles = JsonConvert.DeserializeObject<List<MinecraftProfile>>(json) ?? new List<MinecraftProfile>();
                
                // 查找活跃角色
                var activeProfile = profiles.FirstOrDefault(p => p.IsActive) ?? profiles.FirstOrDefault();
                if (activeProfile != null)
                {
                    return activeProfile.Name;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载角色信息失败: {ex.Message}");
        }
        return string.Empty;
    }

    public void OnNavigatedFrom()
    {
        // TODO: 处理页面离开时的逻辑
    }

    public void OnNavigatedTo(object parameter)
    {
        // TODO: 处理页面加载时的逻辑
    }

    [RelayCommand]
    private async Task HostGame()
    {
        bool isSuccess = true;
        string errorMessage = string.Empty;
        string? port = null;
        string? terracottaPath = null;
        
        // 创建进度弹窗
        var progressDialog = new Microsoft.UI.Xaml.Controls.ContentDialog
        {
            Title = "联机Page_DownloadingTerracottaTitle".GetLocalized(),
            Content = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = "联机Page_DownloadingTerracottaMessage".GetLocalized(), Margin = new Microsoft.UI.Xaml.Thickness(0, 0, 0, 20) },
                    new ProgressBar { Width = 300, IsIndeterminate = false, Minimum = 0, Maximum = 100, Value = 0, Name = "DownloadProgressBar" }
                }
            },
            IsPrimaryButtonEnabled = false,
            IsSecondaryButtonEnabled = false,
            XamlRoot = App.MainWindow.Content.XamlRoot
        };
        
        // 显示进度弹窗
        var dialogTask = progressDialog.ShowAsync();
        
        try
        {
            // 使用TerracottaService确保陶瓦插件已下载并安装，传入进度回调
            double currentProgress = 0;
            terracottaPath = await _terracottaService.EnsureTerracottaAsync(progress =>
            {
                currentProgress = progress;
                // 更新进度条
                if (progressDialog.Content is StackPanel stackPanel)
                {
                    if (stackPanel.FindName("DownloadProgressBar") is ProgressBar progressBar)
                    {
                        progressBar.Value = progress;
                    }
                }
            });
            
            // 关闭进度弹窗
            progressDialog.Hide();
            
            // 检查陶瓦插件是否成功获取
            if (!string.IsNullOrEmpty(terracottaPath) && File.Exists(terracottaPath))
            {
                // 获取真实的物理路径（非虚拟化路径）
                string terracottaDir = Path.GetDirectoryName(terracottaPath);
                
                // 在MSIX环境下，需要转换为真实物理路径
                string realTerracottaDir = terracottaDir;
                string realTempDir;
                
                if (!terracottaDir.Contains("Packages"))
                {
                    // 这是虚拟化路径，需要转换为真实路径
                    string packagePath = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
                    // packagePath 类似: C:\Users\...\Packages\...\LocalState
                    string packagesRoot = packagePath.Substring(0, packagePath.LastIndexOf("LocalState"));
                    realTerracottaDir = Path.Combine(packagesRoot, "LocalCache", "Local", "XianYuLauncher", "terracotta");
                    realTempDir = Path.Combine(packagesRoot, "LocalCache", "Local", "XianYuLauncher", "temp");
                }
                else
                {
                    // 已经是真实路径
                    realTempDir = Path.Combine(Path.GetDirectoryName(realTerracottaDir), "temp");
                }
                
                // 确保目录存在
                Directory.CreateDirectory(realTempDir);
                
                System.Diagnostics.Debug.WriteLine($"[Multiplayer] 虚拟Terracotta目录: {terracottaDir}");
                System.Diagnostics.Debug.WriteLine($"[Multiplayer] 真实Terracotta目录: {realTerracottaDir}");
                System.Diagnostics.Debug.WriteLine($"[Multiplayer] 真实临时文件目录: {realTempDir}");
                
                // 生成时间戳
                string timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
                
                // 生成文件名
                string tempFileName = $"terracotta-{timestamp}-Port.json";
                string tempFilePath = Path.Combine(realTempDir, tempFileName);
                
                System.Diagnostics.Debug.WriteLine($"[Multiplayer] 临时文件完整路径: {tempFilePath}");
                
                // 创建空的json文件
                File.WriteAllText(tempFilePath, "{}");
                
                // 保存临时文件路径
                _tempFilePath = tempFilePath;
                
                // 启动联机服务，添加--hmcl参数
                // 使用真实物理路径启动
                string realExePath = Path.Combine(realTerracottaDir, Path.GetFileName(terracottaPath));
                
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c cd /d \"{realTerracottaDir}\" && \"{Path.GetFileName(terracottaPath)}\" --hmcl \"{tempFilePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = realTerracottaDir
                };
                
                System.Diagnostics.Debug.WriteLine($"[Multiplayer] 准备启动 Terracotta (通过CMD)");
                System.Diagnostics.Debug.WriteLine($"[Multiplayer] 真实可执行文件: {realExePath}");
                System.Diagnostics.Debug.WriteLine($"[Multiplayer] 真实工作目录: {realTerracottaDir}");
                System.Diagnostics.Debug.WriteLine($"[Multiplayer] CMD命令: {processStartInfo.Arguments}");
                System.Diagnostics.Debug.WriteLine($"[Multiplayer] 真实临时文件路径: {tempFilePath}");
                
                // 启动进程并保存引用
                _terracottaProcess = Process.Start(processStartInfo);
                
                if (_terracottaProcess == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[Multiplayer] 错误：Process.Start 返回 null");
                    isSuccess = false;
                    errorMessage = "无法启动 Terracotta 进程";
                    progressDialog.Hide();
                    goto ShowResult;
                }
                
                // 读取输出
                _terracottaProcess.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        System.Diagnostics.Debug.WriteLine($"[Terracotta CMD Output] {e.Data}");
                    }
                };
                _terracottaProcess.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        System.Diagnostics.Debug.WriteLine($"[Terracotta CMD Error] {e.Data}");
                    }
                };
                _terracottaProcess.BeginOutputReadLine();
                _terracottaProcess.BeginErrorReadLine();
                
                System.Diagnostics.Debug.WriteLine($"[Multiplayer] CMD 进程已启动，PID: {_terracottaProcess?.Id}");
                
                // 注意：_terracottaProcess 是 CMD 进程，不是 Terracotta 进程
                // CMD 会启动 Terracotta 后立即退出，这是正常的
                // 我们只需要等待文件被写入即可
                
                // 等待并多次尝试读取端口信息
                int maxRetries = 20; // 增加到20次
                for (int i = 0; i < maxRetries; i++)
                {
                    await Task.Delay(500); // 每次等待500ms
                    
                    // 尝试读取临时文件
                    if (File.Exists(tempFilePath))
                    {
                        try
                        {
                            string jsonContent = File.ReadAllText(tempFilePath);
                            System.Diagnostics.Debug.WriteLine($"[Multiplayer] 尝试 {i + 1}/{maxRetries}，文件内容: {jsonContent}");
                            
                            // 检查文件是否为空或只有空对象
                            if (string.IsNullOrWhiteSpace(jsonContent) || jsonContent.Trim() == "{}")
                            {
                                System.Diagnostics.Debug.WriteLine($"[Multiplayer] 文件内容为空，继续等待...");
                                
                                // 在第 5 次尝试时，检查进程是否真的在运行
                                if (i == 4 && _terracottaProcess != null)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[Multiplayer] 进程状态检查：");
                                    System.Diagnostics.Debug.WriteLine($"  - HasExited: {_terracottaProcess.HasExited}");
                                    System.Diagnostics.Debug.WriteLine($"  - Responding: {_terracottaProcess.Responding}");
                                    try
                                    {
                                        System.Diagnostics.Debug.WriteLine($"  - WorkingSet64: {_terracottaProcess.WorkingSet64} bytes");
                                        System.Diagnostics.Debug.WriteLine($"  - Threads: {_terracottaProcess.Threads.Count}");
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"  - 无法获取进程详细信息: {ex.Message}");
                                    }
                                }
                                
                                continue;
                            }
                            
                            // 解析json，获取port字段
                            using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                            {
                                JsonElement root = doc.RootElement;
                                if (root.TryGetProperty("port", out JsonElement portElement))
                                {
                                    port = portElement.ToString();
                                    _terracottaPort = port; // 保存端口号
                                    System.Diagnostics.Debug.WriteLine($"[Multiplayer] 成功获取端口号: {port}");
                                    
                                    // 找到真正的 Terracotta 进程并保存引用
                                    try
                                    {
                                        var terracottaProcesses = Process.GetProcessesByName("terracotta-0.4.1-windows-x86_64");
                                        if (terracottaProcesses.Length > 0)
                                        {
                                            _terracottaProcess = terracottaProcesses[0];
                                            System.Diagnostics.Debug.WriteLine($"[Multiplayer] 找到 Terracotta 进程，PID: {_terracottaProcess.Id}");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[Multiplayer] 查找 Terracotta 进程失败: {ex.Message}");
                                    }
                                    
                                    break; // 成功获取端口，退出循环
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"[Multiplayer] JSON 中没有 port 字段");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Multiplayer] 读取/解析文件失败: {ex.Message}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[Multiplayer] 尝试 {i + 1}/{maxRetries}，临时文件不存在");
                    }
                }
                
                // 检查是否成功获取端口
                if (string.IsNullOrEmpty(port))
                {
                    System.Diagnostics.Debug.WriteLine($"[Multiplayer] 超时：未能获取端口号");
                    isSuccess = false;
                    errorMessage = "无法获取 Terracotta 端口信息，请检查：\n1. 防火墙是否阻止了程序\n2. 杀毒软件是否拦截了程序\n3. 是否有足够的磁盘空间";
                    
                    // 终止进程
                    if (_terracottaProcess != null && !_terracottaProcess.HasExited)
                    {
                        _terracottaProcess.Kill();
                        _terracottaProcess.Dispose();
                        _terracottaProcess = null;
                    }
                    
                    progressDialog.Hide();
                    goto ShowResult;
                }
            }
            else
            {
                isSuccess = false;
                errorMessage = "联机服务文件不存在或无法下载，请检查网络连接后重试";
            }
        }
        catch (Exception ex)
        {
            isSuccess = false;
            errorMessage = $"启动联机服务时发生错误：{ex.Message}";
            // 关闭进度弹窗
            progressDialog.Hide();
        }

        ShowResult: // 标签，用于 goto 跳转
        // 根据启动结果显示不同的弹窗
        if (isSuccess)
        {
            // 如果获取到端口号，启动轮询
            if (!string.IsNullOrEmpty(port))
            {
                // 初始化轮询结果
                PollingResult = "联机Page_GettingStatusText".GetLocalized();
                
                // 创建CancellationTokenSource
                _pollingCts = new CancellationTokenSource();
                
                // 启动轮询
                StartPolling(port, _pollingCts.Token);
            }
            
            // 创建弹窗
            _statusDialog = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                Title = "联机Page_FindingRoomTitle".GetLocalized(),
                Content = this, // 绑定到当前ViewModel，以便显示PollingResult
                ContentTemplate = (DataTemplate)Microsoft.UI.Xaml.Application.Current.Resources["PollingContentTemplate"],
                PrimaryButtonText = "联机Page_StopButton".GetLocalized(),
                XamlRoot = App.MainWindow.Content.XamlRoot
            };
            
            // 处理停止按钮点击事件
            _statusDialog.PrimaryButtonClick += (sender, args) =>
            {
                StopTerracottaProcess();
            };
            
            // 处理对话框关闭事件，确保轮询停止
            _statusDialog.Closed += (sender, args) =>
            {
                StopPolling();
                _statusDialog = null; // 清除引用
            };
            
            await _statusDialog.ShowAsync();
        }
        else
        {
            // 显示错误信息
            var errorDialog = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                Title = "Common_ErrorTitle".GetLocalized(),
                Content = errorMessage,
                CloseButtonText = "Common_OKButton".GetLocalized(),
                XamlRoot = App.MainWindow.Content.XamlRoot
            };
            
            await errorDialog.ShowAsync();
        }
    }
    
    /// <summary>
    /// 开始轮询状态
    /// </summary>
    /// <param name="port">端口号</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async void StartPolling(string port, CancellationToken cancellationToken)
    {
        try
        {
            // 获取当前启动器内选择的角色名
            string playerName = GetCurrentProfileName();
            
            string stateUrl = $"http://localhost:{port}/state";
            // 在扫描URL后面加上?player={当前角色名}
            string scanningUrl = $"http://localhost:{port}/state/scanning?player={Uri.EscapeDataString(playerName)}";
            
            // 首先发送一次请求到/scanning，用于设置当前为查询状态
            try
            {
                HttpResponseMessage scanningResponse = await _httpClient.GetAsync(scanningUrl, cancellationToken);
                if (scanningResponse.IsSuccessStatusCode)
                {
                    PollingResult = "联机Page_ScanningStatusSetText".GetLocalized();
                }
                else
                {
                    PollingResult = "联机Page_ScanningStatusFailedText".GetLocalized(scanningResponse.StatusCode);
                }
            }
            catch (Exception ex)
            {
                PollingResult = "联机Page_ScanningStatusErrorText".GetLocalized(ex.Message);
            }
            
            // 等待短暂时间，然后开始轮询
            await Task.Delay(500, cancellationToken);
            
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // 发送GET请求
                    HttpResponseMessage response = await _httpClient.GetAsync(stateUrl, cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        // 读取响应内容
                        string content = await response.Content.ReadAsStringAsync(cancellationToken);
                        // 更新轮询结果
                        PollingResult = content;
                        
                        // 解析JSON，检查index是否为3
                        try
                        {
                            using (JsonDocument doc = JsonDocument.Parse(content))
                            {
                                JsonElement root = doc.RootElement;
                                if (root.TryGetProperty("index", out JsonElement indexElement) && 
                                    indexElement.ValueKind == JsonValueKind.Number && 
                                    indexElement.GetInt32() == 3)
                                {
                                    // 检测到index为3，检查是否有room字段
                                    if (root.TryGetProperty("room", out JsonElement roomElement) && 
                                        roomElement.ValueKind == JsonValueKind.String)
                                    {
                                        string roomId = roomElement.GetString();
                                        if (!string.IsNullOrEmpty(roomId))
                                        {
                                            // 停止轮询
                                            StopPolling();
                                            
                                            // 关闭弹窗
                                            if (_statusDialog != null)
                                            {
                                                _statusDialog.Hide();
                                                _statusDialog = null;
                                            }
                                            
                                            // 导航到联机大厅页面，传递端口和房间ID信息
                                            _navigationService.NavigateTo(typeof(MultiplayerLobbyViewModel).FullName, new { RoomId = roomId, Port = port });
                                            
                                            // 退出循环
                                            return;
                                        }
                                    }
                                }
                            }
                        }
                        catch (System.Text.Json.JsonException ex)
                        {
                            // JSON解析错误，忽略
                            System.Diagnostics.Debug.WriteLine($"解析轮询结果JSON错误: {ex.Message}");
                        }                        
                    }                        
                    else
                    {
                        PollingResult = "联机Page_PollingFailedText".GetLocalized(response.StatusCode);
                    }
                }
                catch (HttpRequestException ex)
                {
                    PollingResult = "联机Page_NetworkErrorText".GetLocalized(ex.Message);
                }
                catch (Exception ex)
                {
                    PollingResult = "联机Page_ErrorText".GetLocalized(ex.Message);
                }
                
                // 等待1秒后再次轮询
                await Task.Delay(1000, cancellationToken);
            }
        }
        catch (TaskCanceledException)
        {
            // 轮询被取消，忽略
        }
        catch (OperationCanceledException)
        {
            // 轮询被取消，忽略
        }
    }
    
    /// <summary>
    /// 停止轮询
    /// </summary>
    private void StopPolling()
    {
        if (_pollingCts != null)
        {
            _pollingCts.Cancel();
            _pollingCts.Dispose();
            _pollingCts = null;
        }
    }
    
    /// <summary>
    /// 停止terracotta进程
    /// </summary>
    private async void StopTerracottaProcess()
    {
        try
        {
            // 首先停止轮询
            StopPolling();
            
            // 取消JoinGame方法中的异步操作
            if (_joinGameCts != null)
            {
                _joinGameCts.Cancel();
                _joinGameCts.Dispose();
                _joinGameCts = null;
            }
            
            // 尝试使用terracotta官方HTTP接口优雅关闭进程
            if (!string.IsNullOrEmpty(_terracottaPort))
            {
                try
                {
                    // 首先尝试使用peaceful=true优雅退出
                    string panicUrl = $"http://localhost:{_terracottaPort}/panic?peaceful=true";
                    HttpResponseMessage response = await _httpClient.GetAsync(panicUrl, CancellationToken.None);
                    System.Diagnostics.Debug.WriteLine($"调用terracotta /panic接口结果：{response.StatusCode}");
                    
                    // 等待短暂时间，让进程有时间优雅退出
                    await Task.Delay(2000);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"调用terracotta /panic接口时发生错误：{ex.Message}");
                    // 可以尝试使用peaceful=false强制退出
                    try
                    {
                        string panicUrl = $"http://localhost:{_terracottaPort}/panic?peaceful=false";
                        HttpResponseMessage response = await _httpClient.GetAsync(panicUrl, CancellationToken.None);
                        System.Diagnostics.Debug.WriteLine($"调用terracotta /panic?peaceful=false接口结果：{response.StatusCode}");
                        await Task.Delay(2000);
                    }
                    catch (Exception ex2)
                    {
                        System.Diagnostics.Debug.WriteLine($"调用terracotta强制退出接口时发生错误：{ex2.Message}");
                    }
                }
            }
            
            // 验证进程是否已退出，如果没有则使用传统方式终止
            if (_terracottaProcess != null && !_terracottaProcess.HasExited)
            {
                _terracottaProcess.Kill();
                _terracottaProcess.WaitForExit(5000); // 等待最多5秒
                _terracottaProcess.Dispose();
                _terracottaProcess = null;
            }
            
            // 检查是否还有剩余的terracotta进程，如果有则终止
            Process[] terracottaProcesses = Process.GetProcessesByName("terracotta-windows-x86_64");
            foreach (Process process in terracottaProcesses)
            {
                try
                {
                    process.Kill();
                    process.WaitForExit(5000);
                    process.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"终止进程时发生错误：{ex.Message}");
                }
            }
            
            // 清理临时文件
            if (!string.IsNullOrEmpty(_tempFilePath) && File.Exists(_tempFilePath))
            {
                File.Delete(_tempFilePath);
                _tempFilePath = null;
            }
            
            // 清理端口号
            _terracottaPort = null;
        }
        catch (Exception ex)
        {
            // 这里可以添加日志记录，但不显示给用户，避免影响体验
            System.Diagnostics.Debug.WriteLine($"停止terracotta进程时发生错误：{ex.Message}");
        }
    }
    
    [RelayCommand]
    private async Task JoinGame()
    {
        // 1. 弹出输入房间号的弹窗
        var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
        {
            Title = "联机Page_InputRoomIdTitle".GetLocalized(),
            Content = new TextBox 
            { 
                PlaceholderText = "联机Page_RoomIdPlaceholder".GetLocalized(),
                Width = 300,
                Margin = new Microsoft.UI.Xaml.Thickness(0, 10, 0, 0)
            },
            PrimaryButtonText = "联机Page_ConfirmButton".GetLocalized(),
            SecondaryButtonText = "联机Page_CancelButton".GetLocalized(),
            XamlRoot = App.MainWindow.Content.XamlRoot
        };
        
        var result = await dialog.ShowAsync();
        
        if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary && 
            dialog.Content is TextBox textBox && 
            !string.IsNullOrWhiteSpace(textBox.Text))
        {
            string roomId = textBox.Text;
            
            // 2. 启动客户端进程
            bool isSuccess = true;
            string? port = null;
            string? terracottaPath = null;
            
            // 创建进度弹窗
            var progressDialog = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                Title = "联机Page_DownloadingTerracottaTitle".GetLocalized(),
                Content = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = "联机Page_DownloadingTerracottaMessage".GetLocalized(), Margin = new Microsoft.UI.Xaml.Thickness(0, 0, 0, 20) },
                        new ProgressBar { Width = 300, IsIndeterminate = false, Minimum = 0, Maximum = 100, Value = 0, Name = "DownloadProgressBar" }
                    }
                },
                IsPrimaryButtonEnabled = false,
                IsSecondaryButtonEnabled = false,
                XamlRoot = App.MainWindow.Content.XamlRoot
            };
            
            // 显示进度弹窗
            var dialogTask = progressDialog.ShowAsync();
            
            try
            {
                // 使用TerracottaService确保陶瓦插件已下载并安装，传入进度回调
                double currentProgress = 0;
                terracottaPath = await _terracottaService.EnsureTerracottaAsync(progress =>
                {
                    currentProgress = progress;
                    // 更新进度条
                    if (progressDialog.Content is StackPanel stackPanel)
                    {
                        if (stackPanel.FindName("DownloadProgressBar") is ProgressBar progressBar)
                        {
                            progressBar.Value = progress;
                        }
                    }
                });
                
                // 关闭进度弹窗
                progressDialog.Hide();
                
                // 检查陶瓦插件是否成功获取
                if (!string.IsNullOrEmpty(terracottaPath) && File.Exists(terracottaPath))
                {
                    // 获取真实的物理路径（与HostGame相同的逻辑）
                    string terracottaDir = Path.GetDirectoryName(terracottaPath);
                    string realTerracottaDir = terracottaDir;
                    string realTempDir;
                    
                    if (!terracottaDir.Contains("Packages"))
                    {
                        string packagePath = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
                        string packagesRoot = packagePath.Substring(0, packagePath.LastIndexOf("LocalState"));
                        realTerracottaDir = Path.Combine(packagesRoot, "LocalCache", "Local", "XianYuLauncher", "terracotta");
                        realTempDir = Path.Combine(packagesRoot, "LocalCache", "Local", "XianYuLauncher", "temp");
                    }
                    else
                    {
                        realTempDir = Path.Combine(Path.GetDirectoryName(realTerracottaDir), "temp");
                    }
                    
                    Directory.CreateDirectory(realTempDir);
                    
                    // 生成时间戳
                    string timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
                    string tempFileName = $"terracotta-{timestamp}-Port.json";
                    string tempFilePath = Path.Combine(realTempDir, tempFileName);
                    
                    // 创建空的json文件
                    File.WriteAllText(tempFilePath, "{}");
                    
                    // 保存临时文件路径，用于进程终止后清理
                    _tempFilePath = tempFilePath;
                    
                    // 启动联机服务，添加--hmcl参数（使用CMD启动，与HostGame相同）
                    var processStartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c cd /d \"{realTerracottaDir}\" && \"{Path.GetFileName(terracottaPath)}\" --hmcl \"{tempFilePath}\" --client --id \"{roomId}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WorkingDirectory = realTerracottaDir
                    };
                    
                    // 启动进程并保存引用
                    _terracottaProcess = Process.Start(processStartInfo);
                    
                    // 等待并读取端口信息
                    await Task.Delay(1000);
                    
                    // 读取临时文件，获取端口号
                    if (File.Exists(tempFilePath))
                    {
                        string jsonContent = File.ReadAllText(tempFilePath);
                        // 解析json，获取port字段
                        using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                        {
                            JsonElement root = doc.RootElement;
                            if (root.TryGetProperty("port", out JsonElement portElement))
                        {
                            port = portElement.ToString();
                            _terracottaPort = port; // 保存端口号
                        }
                        }
                    }
                }
                else
                {
                    isSuccess = false;
                    await ShowErrorDialogAsync("联机服务文件不存在或无法下载，请检查网络连接后重试");
                }
            }
            catch (Exception ex)
            {
                isSuccess = false;
                await ShowErrorDialogAsync($"启动联机服务时发生错误：{ex.Message}");
                // 关闭进度弹窗
                progressDialog.Hide();
            }
            
            if (isSuccess && !string.IsNullOrEmpty(port))
            {
                // 3. 访问http://localhost:{端口}/state/guesting?room=房间号&player=角色名
                try
                {
                    // 初始化JoinGame的CancellationTokenSource
                    _joinGameCts = new CancellationTokenSource();
                    CancellationToken cancellationToken = _joinGameCts.Token;
                    
                    // 获取当前启动器内选择的角色名
                    string playerName = GetCurrentProfileName();
                    // 构建包含玩家名的URL
                    string guestingUrl = $"http://localhost:{port}/state/guesting?room={roomId}&player={Uri.EscapeDataString(playerName)}";
                    HttpResponseMessage response = await _httpClient.GetAsync(guestingUrl, cancellationToken);
                    
                    if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    {
                        // 400表示错误，通知玩家房间不存在
                        await ShowErrorDialogAsync("联机Page_RoomNotFoundError".GetLocalized());
                        // 停止进程
                        StopTerracottaProcess();
                        return;
                    }
                    
                    // 4. 轮询访问http://localhost:{端口}/state，直到state为guest-ok
                    bool isGuestOk = false;
                    string url = string.Empty;
                    
                    for (int i = 0; i < 30 && !cancellationToken.IsCancellationRequested; i++) // 最多尝试30次，每次间隔1秒
                    {
                        try
                        {
                            string stateUrl = $"http://localhost:{port}/state";
                            HttpResponseMessage stateResponse = await _httpClient.GetAsync(stateUrl, cancellationToken);
                            
                            if (stateResponse.IsSuccessStatusCode)
                            {
                                string content = await stateResponse.Content.ReadAsStringAsync(cancellationToken);
                                
                                // 解析JSON
                                using (JsonDocument doc = JsonDocument.Parse(content))
                                {
                                    JsonElement root = doc.RootElement;
                                    if (root.TryGetProperty("state", out JsonElement stateElement) && 
                                        stateElement.ValueKind == JsonValueKind.String &&
                                        stateElement.GetString() == "guest-ok")
                                    {
                                        // 获取url字段
                                        if (root.TryGetProperty("url", out JsonElement urlElement) &&
                                            urlElement.ValueKind == JsonValueKind.String)
                                        {
                                            url = urlElement.GetString();
                                            isGuestOk = true;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        catch (TaskCanceledException)
                        {
                            // 任务被取消，退出循环
                            break;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"获取状态失败: {ex.Message}");
                        }
                        
                        // 等待1秒后再次尝试，支持取消
                        await Task.Delay(1000, cancellationToken);
                    }
                    
                    if (isSuccess && !string.IsNullOrEmpty(url))
                    {
                        // 5. 导航至联机大厅页，传递房间号、端口和房客标识
                    _navigationService.NavigateTo(typeof(MultiplayerLobbyViewModel).FullName, new { 
                            RoomId = roomId, 
                            Port = port, 
                            IsGuest = true, 
                            Url = url 
                        });
                    }
                    else if (!cancellationToken.IsCancellationRequested)
                    {
                        await ShowErrorDialogAsync("联机Page_JoinFailedError".GetLocalized());
                        // 停止进程
                        StopTerracottaProcess();
                    }
                }
                catch (TaskCanceledException)
                {
                    // 任务被取消，不显示错误
                    StopTerracottaProcess();
                }
                catch (Exception ex)
                {
                    await ShowErrorDialogAsync($"连接房间时发生错误：{ex.Message}");
                    // 停止进程
                    StopTerracottaProcess();
                }
                finally
                {
                    // 清理CancellationTokenSource
                    if (_joinGameCts != null)
                    {
                        _joinGameCts.Dispose();
                        _joinGameCts = null;
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// 显示错误对话框
    /// </summary>
    private async Task ShowErrorDialogAsync(string message)
    {
        var errorDialog = new Microsoft.UI.Xaml.Controls.ContentDialog
        {
            Title = "Common_ErrorTitle".GetLocalized(),
            Content = message,
            CloseButtonText = "Common_OKButton".GetLocalized(),
            XamlRoot = App.MainWindow.Content.XamlRoot
        };
        
        await errorDialog.ShowAsync();
    }
}