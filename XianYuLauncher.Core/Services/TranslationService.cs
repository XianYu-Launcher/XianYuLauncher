using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Newtonsoft.Json;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// MCIM翻译服务实现
/// </summary>
public class TranslationService : ITranslationService
{
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, McimTranslationResponse> _translationCache;
    private const string ModrinthTranslationApiUrl = "https://mod.mcimirror.top/translate/modrinth";
    private const string CurseForgeTranslationApiUrl = "https://mod.mcimirror.top/translate/curseforge";
    
    // 添加一个静态属性来存储当前语言设置
    private static string _currentLanguage = "zh-CN";
    
    /// <summary>
    /// 设置当前语言（由LanguageSelectorService调用）
    /// </summary>
    public static void SetCurrentLanguage(string language)
    {
        _currentLanguage = language;
        System.Diagnostics.Debug.WriteLine($"[翻译服务] 语言已设置为: {language}");
    }
    
    /// <summary>
    /// 获取当前语言设置
    /// </summary>
    public static string GetCurrentLanguage()
    {
        return _currentLanguage;
    }
    
    public TranslationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _translationCache = new Dictionary<string, McimTranslationResponse>();
    }
    
    /// <summary>
    /// 检查是否应该使用翻译（当前语言是否为中文）
    /// </summary>
    public bool ShouldUseTranslation()
    {
        try
        {
            // 使用静态语言字段而不是 CultureInfo，避免跨程序集的文化信息不同步问题
            bool isChinese = _currentLanguage.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            
            System.Diagnostics.Debug.WriteLine($"[翻译服务] 当前语言设置: {_currentLanguage}, 是否为中文: {isChinese}");
            
            return isChinese;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// 获取Modrinth项目的中文翻译
    /// </summary>
    public async Task<McimTranslationResponse?> GetModrinthTranslationAsync(string projectId)
    {
        if (string.IsNullOrEmpty(projectId))
        {
            return null;
        }
        
        // 检查缓存
        var cacheKey = $"modrinth_{projectId}";
        if (_translationCache.TryGetValue(cacheKey, out var cachedTranslation))
        {
            return cachedTranslation;
        }
        
        try
        {
            var url = $"{ModrinthTranslationApiUrl}?project_id={projectId}";
            System.Diagnostics.Debug.WriteLine($"[翻译服务] 请求Modrinth翻译: {url}");
            
            // 使用GET请求而不是POST
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"[翻译服务] Modrinth翻译请求失败: {response.StatusCode}");
                return null;
            }
            
            var content = await response.Content.ReadAsStringAsync();
            var translation = JsonConvert.DeserializeObject<McimTranslationResponse>(content);
            
            if (translation != null && !string.IsNullOrEmpty(translation.Translated))
            {
                // 缓存翻译结果
                _translationCache[cacheKey] = translation;
                System.Diagnostics.Debug.WriteLine($"[翻译服务] Modrinth翻译成功: {projectId}");
                return translation;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[翻译服务] 获取Modrinth翻译失败: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// 获取CurseForge Mod的中文翻译
    /// </summary>
    public async Task<McimTranslationResponse?> GetCurseForgeTranslationAsync(int modId)
    {
        if (modId <= 0)
        {
            return null;
        }
        
        // 检查缓存
        var cacheKey = $"curseforge_{modId}";
        if (_translationCache.TryGetValue(cacheKey, out var cachedTranslation))
        {
            return cachedTranslation;
        }
        
        try
        {
            var url = $"{CurseForgeTranslationApiUrl}?modId={modId}";
            System.Diagnostics.Debug.WriteLine($"[翻译服务] 请求CurseForge翻译: {url}");
            
            // 使用GET请求而不是POST
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"[翻译服务] CurseForge翻译请求失败: {response.StatusCode}");
                return null;
            }
            
            var content = await response.Content.ReadAsStringAsync();
            var translation = JsonConvert.DeserializeObject<McimTranslationResponse>(content);
            
            if (translation != null && !string.IsNullOrEmpty(translation.Translated))
            {
                // 缓存翻译结果
                _translationCache[cacheKey] = translation;
                System.Diagnostics.Debug.WriteLine($"[翻译服务] CurseForge翻译成功: {modId}");
                return translation;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[翻译服务] 获取CurseForge翻译失败: {ex.Message}");
            return null;
        }
    }
}
