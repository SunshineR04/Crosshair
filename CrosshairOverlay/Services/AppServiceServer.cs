using System;
using System.Text.Json;
using System.Threading;
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

    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private readonly CancellationTokenSource _disposeCts = new();
    private AppServiceConnection? _connection;
    private CrosshairProfile? _latestProfile;
    private Task? _connectTask;
    private bool _packageUnavailable;
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    public async Task InitializeAsync(CrosshairProfile? initialProfile = null)
    {
        if (initialProfile != null)
        {
            await StoreLatestProfileAsync(initialProfile);
        }

        await EnsureConnectionAsync();
    }

    public async Task PushProfile(CrosshairProfile profile)
    {
        try
        {
            await StoreLatestProfileAsync(profile);
            await EnsureConnectionAsync();
            await SendLatestProfileAsync();
        }
        catch (OperationCanceledException)
        {
            // Shutdown canceled an in-flight IPC operation.
        }
    }

    private async Task StoreLatestProfileAsync(CrosshairProfile profile)
    {
        await _connectionGate.WaitAsync(_disposeCts.Token);
        try
        {
            if (!_disposed)
                _latestProfile = CrosshairProfileRules.Sanitize(profile);
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    private async Task EnsureConnectionAsync()
    {
        Task connectTask;

        await _connectionGate.WaitAsync(_disposeCts.Token);
        try
        {
            if (_disposed || _connection != null || _packageUnavailable)
                return;

            _connectTask ??= ConnectCoreAsync();
            connectTask = _connectTask;
        }
        finally
        {
            _connectionGate.Release();
        }

        try
        {
            await connectTask;
        }
        finally
        {
            await _connectionGate.WaitAsync();
            try
            {
                if (ReferenceEquals(_connectTask, connectTask))
                    _connectTask = null;
            }
            finally
            {
                _connectionGate.Release();
            }
        }
    }

    private async Task ConnectCoreAsync()
    {
        AppServiceConnection? candidate = null;

        try
        {
            Windows.ApplicationModel.Package package;
            try
            {
                package = Windows.ApplicationModel.Package.Current;
            }
            catch (Exception ex)
            {
                _packageUnavailable = true;
                LogService.Warn($"AppService unavailable (standalone mode): {ex.Message}");
                return;
            }

            if (package == null)
                return;

            candidate = new AppServiceConnection
            {
                AppServiceName = ServiceName,
                PackageFamilyName = package.Id.FamilyName
            };

            var status = await candidate.OpenAsync();
            if (status != AppServiceConnectionStatus.Success)
            {
                LogService.Warn($"AppService connection failed: {status}");
                return;
            }

            candidate.ServiceClosed += (sender, args) => _ = HandleConnectionClosedAsync(sender);

            await _connectionGate.WaitAsync(_disposeCts.Token);
            try
            {
                if (_disposed)
                    return;

                _connection = candidate;
                candidate = null;
                await SendLatestProfileLockedAsync();
            }
            finally
            {
                _connectionGate.Release();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            LogService.Warn($"AppService initialization failed: {ex.Message}");
        }
        finally
        {
            candidate?.Dispose();
        }
    }

    private async Task SendLatestProfileAsync()
    {
        await _connectionGate.WaitAsync(_disposeCts.Token);
        try
        {
            await SendLatestProfileLockedAsync();
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    private async Task<bool> SendLatestProfileLockedAsync()
    {
        if (_connection == null || _latestProfile == null)
            return false;

        var connection = _connection;
        try
        {
            var json = JsonSerializer.Serialize(_latestProfile, JsonOptions);
            var msg = new ValueSet
            {
                { KeyCommand, CmdUpdateProfile },
                { KeyProfileJson, json }
            };
            var response = await connection.SendMessageAsync(msg);
            if (response.Status != AppServiceResponseStatus.Success)
                throw new InvalidOperationException($"AppService response status: {response.Status}");

            return true;
        }
        catch (Exception ex)
        {
            LogService.Error("AppService PushProfile failed", ex);
            if (ReferenceEquals(_connection, connection))
            {
                _connection = null;
                connection.Dispose();
            }

            return false;
        }
    }

    private async Task HandleConnectionClosedAsync(AppServiceConnection connection)
    {
        try
        {
            await _connectionGate.WaitAsync(_disposeCts.Token);
            try
            {
                if (ReferenceEquals(_connection, connection))
                    _connection = null;
            }
            finally
            {
                _connectionGate.Release();
            }

            await Task.Delay(TimeSpan.FromSeconds(1), _disposeCts.Token);
            await EnsureConnectionAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            LogService.Warn($"AppService reconnect failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _disposeCts.Cancel();
        _connection?.Dispose();
        _connection = null;
    }
}
