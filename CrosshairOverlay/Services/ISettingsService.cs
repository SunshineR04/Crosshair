using CrosshairOverlay.Models;

namespace CrosshairOverlay.Services;

/// <summary>
/// 设置持久化服务接口，抽象配置的加载、保存和 Widget 同步写入。
/// </summary>
public interface ISettingsService
{
    /// <summary>从磁盘加载准心配置。文件不存在或损坏时返回默认配置。</summary>
    CrosshairProfile Load();

    /// <summary>将配置保存到主设置文件（%APPDATA%\CrosshairOverlay\settings.json）。</summary>
    void Save(CrosshairProfile profile);

    /// <summary>将配置写入 Widget 的 LocalState 文件夹供轮询读取。</summary>
    void SaveForWidget(CrosshairProfile profile);
}
