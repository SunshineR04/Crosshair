using System.Text.Json;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using CrosshairOverlay.Models;

namespace CrosshairOverlay.Services;

public class AppServiceServer : IDisposable
{
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
                AppServiceName = "CrosshairProfileService",
                PackageFamilyName = pkg.Id.FamilyName
            };

            var status = await _connection.OpenAsync();
            if (status != AppServiceConnectionStatus.Success)
            {
                System.Diagnostics.Debug.WriteLine($"[AppService] Connection failed: {status}");
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
            System.Diagnostics.Debug.WriteLine($"[AppService] Initialize failed: {ex.Message}");
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
                { "command", "UpdateProfile" },
                { "profileJson", json }
            };
            await _connection.SendMessageAsync(msg);
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppService] PushProfile failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
    }
}
