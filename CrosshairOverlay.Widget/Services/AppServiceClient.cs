using System;
using Windows.ApplicationModel.AppService;
using Newtonsoft.Json;
using CrosshairOverlay.Models;

namespace CrosshairOverlay.Widget.Services
{
    public class AppServiceClient
    {
        public static AppServiceClient Instance { get; } = new AppServiceClient();

        public event Action<CrosshairProfile> ProfileUpdated;

        private AppServiceClient() { }

        public void Initialize(AppServiceConnection connection, Windows.ApplicationModel.Background.BackgroundTaskDeferral deferral)
        {
            connection.RequestReceived += (sender, args) =>
            {
                var d = args.GetDeferral();
                try
                {
                    var cmd = args.Request.Message["command"] as string;
                    if (cmd == "UpdateProfile")
                    {
                        var json = args.Request.Message["profileJson"] as string;
                        if (!string.IsNullOrEmpty(json))
                        {
                            var profile = JsonConvert.DeserializeObject<CrosshairProfile>(json);
                            if (profile != null)
                                ProfileUpdated?.Invoke(profile);
                        }
                    }
                }
                catch { }
                d.Complete();
            };

            connection.ServiceClosed += (s, e) =>
            {
                deferral.Complete();
            };
        }
    }
}
