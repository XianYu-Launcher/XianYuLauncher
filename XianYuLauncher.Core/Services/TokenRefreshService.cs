using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 令牌刷新服务实现
/// 注意：此服务需要在 UI 层注入 ITokenRefreshCallback 来执行实际的令牌刷新
/// 因为令牌刷新涉及 CharacterManagementViewModel，它在 UI 层
/// </summary>
public class TokenRefreshService : ITokenRefreshService
{
    private ITokenRefreshCallback? _callback;
    
    /// <summary>
    /// 设置令牌刷新回调
    /// </summary>
    public void SetCallback(ITokenRefreshCallback callback)
    {
        _callback = callback;
    }
    
    /// <summary>
    /// 检查并刷新令牌（如果需要）
    /// </summary>
    public async Task<TokenRefreshResult> CheckAndRefreshTokenAsync(MinecraftProfile profile)
    {
        var result = new TokenRefreshResult
        {
            Success = true,
            WasRefreshed = false,
            UpdatedProfile = profile
        };
        
        // 记录角色基本信息
        Serilog.Log.Information("=== 开始令牌检查流程 ===");
        Serilog.Log.Information("角色名称: {ProfileName}", profile.Name);
        Serilog.Log.Information("角色类型: {ProfileType}", profile.IsOffline ? "离线" : "在线");
        Serilog.Log.Information("令牌类型: {TokenType}", profile.TokenType ?? "未知");
        
        // 如果是离线角色，不需要刷新
        if (profile.IsOffline)
        {
            result.StatusMessage = "离线角色无需刷新令牌";
            Serilog.Log.Information("离线角色，跳过令牌刷新");
            Serilog.Log.Information("=== 令牌检查流程结束 ===");
            return result;
        }
        
        try
        {
            // 检查网络连接
            bool isInternetAvailable = CheckInternetConnection();
            Serilog.Log.Information("网络连接状态: {IsAvailable}", isInternetAvailable ? "可用" : "不可用");
            
            if (!isInternetAvailable)
            {
                result.StatusMessage = "无网络连接，跳过令牌刷新";
                Serilog.Log.Warning("无网络连接，跳过令牌刷新");
                Serilog.Log.Information("=== 令牌检查流程结束 ===");
                return result;
            }
            
            // 计算令牌剩余有效期
            var issueTime = profile.IssueInstant;
            var expiresIn = profile.ExpiresIn;
            var expiryTime = issueTime.AddSeconds(expiresIn);
            var timeUntilExpiry = expiryTime - DateTime.UtcNow;
            
            // 详细记录令牌时间信息
            Serilog.Log.Information("令牌颁发时间 (UTC): {IssueTime}", issueTime.ToString("yyyy-MM-dd HH:mm:ss"));
            Serilog.Log.Information("令牌有效期 (秒): {ExpiresIn}", expiresIn);
            Serilog.Log.Information("令牌过期时间 (UTC): {ExpiryTime}", expiryTime.ToString("yyyy-MM-dd HH:mm:ss"));
            Serilog.Log.Information("当前时间 (UTC): {CurrentTime}", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            Serilog.Log.Information("令牌剩余有效期: {RemainingMinutes:F2} 分钟 ({RemainingSeconds:F0} 秒)", timeUntilExpiry.TotalMinutes, timeUntilExpiry.TotalSeconds);
            
            // 判断令牌状态
            if (timeUntilExpiry <= TimeSpan.Zero)
            {
                Serilog.Log.Warning("⚠️ 令牌已过期! 过期时长: {ExpiredMinutes:F2} 分钟", Math.Abs(timeUntilExpiry.TotalMinutes));
            }
            else if (timeUntilExpiry < TimeSpan.FromMinutes(5))
            {
                Serilog.Log.Warning("⚠️ 令牌即将在 {RemainingMinutes:F2} 分钟内过期", timeUntilExpiry.TotalMinutes);
            }
            else if (timeUntilExpiry < TimeSpan.FromHours(1))
            {
                Serilog.Log.Information("令牌剩余有效期不足1小时，需要刷新");
            }
            else
            {
                Serilog.Log.Information("令牌有效期充足 (剩余 {RemainingHours:F2} 小时)", timeUntilExpiry.TotalHours);
            }
            
            // 如果剩余有效期小于1小时，刷新令牌
            if (timeUntilExpiry < TimeSpan.FromHours(1))
            {
                Serilog.Log.Information("开始执行令牌刷新...");
                
                // 根据角色类型设置消息
                string renewingText, renewedText;
                if (profile.TokenType == "external")
                {
                    renewingText = "正在进行外置登录续签";
                    renewedText = "外置登录续签成功";
                    Serilog.Log.Information("令牌类型: 外置登录");
                }
                else
                {
                    renewingText = "正在刷新微软账户令牌";
                    renewedText = "微软账户令牌刷新成功";
                    Serilog.Log.Information("令牌类型: 微软账户");
                }
                
                result.StatusMessage = renewingText;
                
                // 执行令牌刷新
                if (_callback != null)
                {
                    Serilog.Log.Information("调用令牌刷新回调...");
                    var refreshedProfile = await _callback.RefreshTokenAsync(profile);
                    
                    if (refreshedProfile != null)
                    {
                        // 记录刷新后的令牌信息
                        var newExpiryTime = refreshedProfile.IssueInstant.AddSeconds(refreshedProfile.ExpiresIn);
                        var newTimeUntilExpiry = newExpiryTime - DateTime.UtcNow;
                        
                        result.Success = true;
                        result.WasRefreshed = true;
                        result.UpdatedProfile = refreshedProfile;
                        result.StatusMessage = renewedText;
                        
                        Serilog.Log.Information("✅ 令牌刷新成功!");
                        Serilog.Log.Information("新令牌颁发时间 (UTC): {NewIssueTime}", refreshedProfile.IssueInstant.ToString("yyyy-MM-dd HH:mm:ss"));
                        Serilog.Log.Information("新令牌有效期 (秒): {NewExpiresIn}", refreshedProfile.ExpiresIn);
                        Serilog.Log.Information("新令牌过期时间 (UTC): {NewExpiryTime}", newExpiryTime.ToString("yyyy-MM-dd HH:mm:ss"));
                        Serilog.Log.Information("新令牌剩余有效期: {NewRemainingHours:F2} 小时", newTimeUntilExpiry.TotalHours);
                    }
                    else
                    {
                        // 刷新失败，但不阻止游戏启动
                        result.Success = true;
                        result.WasRefreshed = false;
                        result.StatusMessage = "令牌刷新失败，但将继续启动游戏";
                        Serilog.Log.Error("❌ 令牌刷新失败 (回调返回 null)，但将继续启动游戏");
                        Serilog.Log.Warning("建议用户重新登录以获取新令牌");
                    }
                }
                else
                {
                    Serilog.Log.Error("❌ 令牌刷新回调未设置!");
                    result.StatusMessage = "令牌刷新服务未配置";
                }
            }
            else
            {
                Serilog.Log.Information("令牌有效期充足，无需刷新");
                result.StatusMessage = "令牌有效";
            }
        }
        catch (HttpRequestException ex)
        {
            // 网络异常，跳过刷新，继续启动
            Serilog.Log.Error(ex, "网络异常，跳过令牌刷新");
            result.Success = true;
            result.WasRefreshed = false;
            result.StatusMessage = "网络异常，跳过令牌刷新";
        }
        catch (Exception ex)
        {
            // 其他刷新失败，继续启动，但记录错误
            Serilog.Log.Error(ex, "令牌刷新过程中发生异常");
            result.Success = true;
            result.WasRefreshed = false;
            result.ErrorMessage = ex.Message;
            result.StatusMessage = "令牌刷新失败，但将继续启动游戏";
        }
        
        Serilog.Log.Information("=== 令牌检查流程结束 ===");
        return result;
    }
    
    /// <summary>
    /// 检查网络连接
    /// </summary>
    private bool CheckInternetConnection()
    {
        try
        {
            // 简单的网络检查：尝试解析一个常用域名
            var hostEntry = System.Net.Dns.GetHostEntry("www.microsoft.com");
            return hostEntry.AddressList.Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
