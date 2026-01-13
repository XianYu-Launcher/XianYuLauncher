using System.Threading.Tasks;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services;

/// <summary>
/// MCIM翻译服务接口
/// </summary>
public interface ITranslationService
{
    /// <summary>
    /// 获取Modrinth项目的中文翻译
    /// </summary>
    /// <param name="projectId">Modrinth项目ID</param>
    /// <returns>翻译响应，如果翻译不可用则返回null</returns>
    Task<McimTranslationResponse?> GetModrinthTranslationAsync(string projectId);
    
    /// <summary>
    /// 获取CurseForge Mod的中文翻译
    /// </summary>
    /// <param name="modId">CurseForge Mod ID</param>
    /// <returns>翻译响应，如果翻译不可用则返回null</returns>
    Task<McimTranslationResponse?> GetCurseForgeTranslationAsync(int modId);
    
    /// <summary>
    /// 检查是否应该使用翻译（当前语言是否为中文）
    /// </summary>
    bool ShouldUseTranslation();
}
