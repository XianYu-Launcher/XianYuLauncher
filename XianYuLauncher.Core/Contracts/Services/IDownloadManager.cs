using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XianYuLauncher.Core.Contracts.Services;

/// <summary>
/// 下载管理器接口，提供统一的文件下载功能
/// </summary>
public interface IDownloadManager
{
    /// <summary>
    /// 下载单个文件到指定路径
    /// </summary>
    /// <param name="url">下载URL</param>
    /// <param name="targetPath">目标文件路径</param>
    /// <param name="expectedSha1">预期的SHA1哈希值（可选，用于验证）</param>
    /// <param name="progressCallback">进度回调（0-100）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>下载结果</returns>
    Task<DownloadResult> DownloadFileAsync(
        string url, 
        string targetPath, 
        string? expectedSha1 = null,
        Action<double>? progressCallback = null, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 下载文件内容到内存
    /// </summary>
    /// <param name="url">下载URL</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>文件字节数组</returns>
    Task<byte[]> DownloadBytesAsync(
        string url, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 下载文件内容为字符串
    /// </summary>
    /// <param name="url">下载URL</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>文件内容字符串</returns>
    Task<string> DownloadStringAsync(
        string url, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 批量下载多个文件
    /// </summary>
    /// <param name="tasks">下载任务列表</param>
    /// <param name="maxConcurrency">最大并发数</param>
    /// <param name="progressCallback">总体进度回调（0-100）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>所有下载结果</returns>
    Task<IEnumerable<DownloadResult>> DownloadFilesAsync(
        IEnumerable<DownloadTask> tasks, 
        int maxConcurrency = 4,
        Action<double>? progressCallback = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 下载任务
/// </summary>
public class DownloadTask
{
    /// <summary>
    /// 下载URL
    /// </summary>
    public string Url { get; set; } = string.Empty;
    
    /// <summary>
    /// 目标文件路径
    /// </summary>
    public string TargetPath { get; set; } = string.Empty;
    
    /// <summary>
    /// 预期的SHA1哈希值（可选）
    /// </summary>
    public string? ExpectedSha1 { get; set; }
    
    /// <summary>
    /// 预期的文件大小（可选）
    /// </summary>
    public long? ExpectedSize { get; set; }
    
    /// <summary>
    /// 任务描述（用于日志和进度报告）
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// 优先级（数值越小优先级越高）
    /// </summary>
    public int Priority { get; set; } = 0;
}


/// <summary>
/// 下载结果
/// </summary>
public class DownloadResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// 下载的文件路径
    /// </summary>
    public string? FilePath { get; set; }
    
    /// <summary>
    /// 下载的URL
    /// </summary>
    public string? Url { get; set; }
    
    /// <summary>
    /// 错误消息（如果失败）
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// 异常信息（如果失败）
    /// </summary>
    public Exception? Exception { get; set; }
    
    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; set; }
    
    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static DownloadResult Succeeded(string filePath, string url) => new()
    {
        Success = true,
        FilePath = filePath,
        Url = url
    };
    
    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static DownloadResult Failed(string url, string errorMessage, Exception? exception = null, int retryCount = 0) => new()
    {
        Success = false,
        Url = url,
        ErrorMessage = errorMessage,
        Exception = exception,
        RetryCount = retryCount
    };
}
