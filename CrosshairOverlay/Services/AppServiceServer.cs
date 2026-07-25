using System.Text.Json;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using CrosshairOverlay.Models;

namespace CrosshairOverlay.Services;

public class AppServiceServer : IDisposable
{
    /// <summary>AppService 服务名称，必须与 Widget 端一致。</summary>
    public const string ServiceName = "CrosshairProfileService";
    /// <summary>消息中的命令字段名。</summary>
    private const string KeyCommand = "command";
    /// <summary>更新配置命令名。</summary>
    private const string CmdUpdateProfile = "UpdateProfile";
    /// <summary>消息中的 profile JSON 字段名。</summary>
    private const string KeyProfileJson = "profileJson";
    private AppServiceConnection? _connection;
    private CrosshairProfile? _pendingProfile;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    public async Task InitializeAsync()
    {
        try
        {
            var pkg = Windows.ApplicationModel.Package.Current;
            if (pkg == null) return;

            _connection = new AppServiceConnection
            {
                AppServiceName = ServiceName,
                PackageFamilyName = pkg.Id.FamilyName
            };

            var status = await _connection.OpenAsync();
            if (status != AppServiceConnectionStatus.Success)
            {
                LogService.Warn($"AppService connection failed: {status}");
                _connection.Dispose();
                _connection = null;
                return;
            }

            if (_pendingProfile != null)
            {
                await PushProfile(_pendingProfile);
                _pendingProfile = null;
            }
        }
        catch (System.Exception ex)
        {
            // 独立 exe 模式下 Package.Current 不可用，这是预期行为（文件同步作为 fallback）
            LogService.Warn($"AppService unavailable (standalone mode): {ex.Message}");
            _connection = null;
        }
    }

    public async Task PushProfile(CrosshairProfile profile)
    {
        if (_connection == null)
        {
            _pendingProfile = profile;
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(profile, JsonOptions);
            var msg = new ValueSet
            {
                { KeyCommand, CmdUpdateProfile },
                { KeyProfileJson, json }
            };
            await _connection.SendMessageAsync(msg);
        }
        catch (System.Exception ex)
        {
            LogService.Error("AppService PushProfile failed", ex);
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
    }
}
