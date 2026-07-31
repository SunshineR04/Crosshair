using System;
using System.Threading;

namespace CrosshairOverlay.Services;

/// <summary>
/// Prevents duplicate desktop instances and lets a later launch request the
/// existing instance to show its settings window.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = @"Local\CrosshairOverlay.Instance";
    private const string ShowSettingsEventName = @"Local\CrosshairOverlay.ShowSettings";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _showSettingsEvent;
    private readonly RegisteredWaitHandle _registeredWait;
    private bool _disposed;

    public event Action? ShowSettingsRequested;

    private SingleInstanceGuard(Mutex mutex, EventWaitHandle showSettingsEvent)
    {
        _mutex = mutex;
        _showSettingsEvent = showSettingsEvent;
        _registeredWait = ThreadPool.RegisterWaitForSingleObject(
            _showSettingsEvent,
            (_, _) => ShowSettingsRequested?.Invoke(),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    public static SingleInstanceGuard? TryCreate()
    {
        Mutex mutex;
        bool createdNew;

        try
        {
            mutex = new Mutex(initiallyOwned: true, MutexName, out createdNew);
        }
        catch (AbandonedMutexException ex)
        {
            var abandonedMutex = ex.Mutex;
            if (abandonedMutex == null)
                throw new InvalidOperationException("The abandoned mutex handle was not returned.");
            mutex = abandonedMutex;
            createdNew = true;
        }

        if (!createdNew)
        {
            using (mutex)
            {
                try
                {
                    using var showSettingsEvent = EventWaitHandle.OpenExisting(ShowSettingsEventName);
                    showSettingsEvent.Set();
                }
                catch (WaitHandleCannotBeOpenedException)
                {
                    // The first instance may still be creating its signal event.
                }
                catch (UnauthorizedAccessException)
                {
                    // A different integrity level may prevent opening the signal event.
                }
            }

            return null;
        }

        var signal = new EventWaitHandle(
            initialState: false,
            mode: EventResetMode.AutoReset,
            name: ShowSettingsEventName);
        return new SingleInstanceGuard(mutex, signal);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _registeredWait.Unregister(null);
        _showSettingsEvent.Dispose();

        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // The mutex may already have been abandoned during process shutdown.
        }

        _mutex.Dispose();
    }
}
