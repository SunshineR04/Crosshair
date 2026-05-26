using System.Text.Json;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using CrosshairOverlay.Models;

namespace CrosshairOverlay.Services;

public class AppServiceServer : IDisposable
{
    private AppServiceConnection? _connection;

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
            }
        }
        catch
        {
            // Not running in MSIX context
            _connection = null;
        }
    }

    public async Task PushProfile(CrosshairProfile profile)
    {
        if (_connection == null) return;

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
        catch
        {
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
    }
}
