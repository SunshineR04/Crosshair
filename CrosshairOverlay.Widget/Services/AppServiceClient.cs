using System;
using Windows.ApplicationModel.AppService;
using Newtonsoft.Json;
using CrosshairOverlay.Models;

namespace CrosshairOverlay.Widget.Services
{
    public class AppServiceClient
    {
        /// <summary>消息中的命令字段名，必须与桌面端 AppServiceServer 一致。</summary>
        private const string KeyCommand = "command";
        /// <summary>更新配置命令名。</summary>
        private const string CmdUpdateProfile = "UpdateProfile";
        /// <summary>消息中的 profile JSON 字段名。</summary>
        private const string KeyProfileJson = "profileJson";

        public static AppServiceClient Instance { get; } = new AppServiceClient();

        public event Action<CrosshairProfile> ProfileUpdated;

        /// <summary>AppService 连接是否活跃。活跃时文件轮询可跳过以减少 I/O。</summary>
        public bool IsConnected { get; private set; }

        private AppServiceClient() { }

        public void Initialize(AppServiceConnection connection, Windows.ApplicationModel.Background.BackgroundTaskDeferral deferral)
        {
            IsConnected = true;

            connection.RequestReceived += (sender, args) =>
            {
                var d = args.GetDeferral();
                try
                {
                    var cmd = args.Request.Message[KeyCommand] as string;
                    if (cmd == CmdUpdateProfile)
                    {
                        var json = args.Request.Message[KeyProfileJson] as string;
                        if (!string.IsNullOrEmpty(json))
                        {
                            var profile = JsonConvert.DeserializeObject<CrosshairProfile>(json);
                            if (profile != null)
                                ProfileUpdated?.Invoke(profile);
                            else
                                System.Diagnostics.Debug.WriteLine("[Widget] AppService profile deserialized to null");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Widget] AppService RequestReceived error: {ex.Message}");
                }
                d.Complete();
            };

            connection.ServiceClosed += (s, e) =>
            {
                IsConnected = false;
                deferral.Complete();
            };
        }
    }
}
