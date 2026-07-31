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

        /// <summary>最近一次成功接收的配置。</summary>
        public CrosshairProfile CurrentProfile { get; private set; }

        /// <summary>是否已经接收过桌面端配置。</summary>
        public bool HasCurrentProfile { get; private set; }

        private AppServiceConnection _connection;

        private AppServiceClient()
        {
            CurrentProfile = new CrosshairProfile();
        }

        public void Initialize(AppServiceConnection connection, Action completeDeferral)
        {
            if (connection == null)
            {
                completeDeferral();
                return;
            }

            if (ReferenceEquals(_connection, connection))
                return;

            _connection = connection;
            IsConnected = true;
            HasCurrentProfile = false;

            connection.RequestReceived += (sender, args) =>
            {
                var d = args.GetDeferral();
                try
                {
                    if (!ReferenceEquals(_connection, connection))
                        return;

                    var cmd = args.Request.Message[KeyCommand] as string;
                    if (cmd == CmdUpdateProfile)
                    {
                        var json = args.Request.Message[KeyProfileJson] as string;
                        if (!string.IsNullOrEmpty(json))
                        {
                            var profile = JsonConvert.DeserializeObject<CrosshairProfile>(json);
                            if (profile != null)
                            {
                                CurrentProfile = CrosshairProfileRules.Sanitize(profile);
                                HasCurrentProfile = true;
                                ProfileUpdated?.Invoke(CurrentProfile);
                            }
                            else
                                System.Diagnostics.Debug.WriteLine("[Widget] AppService profile deserialized to null");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Widget] AppService RequestReceived error: {ex.Message}");
                }
                finally
                {
                    d.Complete();
                }
            };

            connection.ServiceClosed += (s, e) =>
            {
                if (ReferenceEquals(_connection, connection))
                {
                    _connection = null;
                    IsConnected = false;
                    HasCurrentProfile = false;
                }

                completeDeferral();
            };
        }
    }
}
