using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Microsoft.Gaming.XboxGameBar;
using CrosshairOverlay.Widget.Services;

namespace CrosshairOverlay.Widget
{
    sealed partial class App : Application
    {
        private XboxGameBarWidget _widget = null!;

        public App()
        {
            InitializeComponent();
        }

        protected override void OnActivated(IActivatedEventArgs args)
        {
            if (args is XboxGameBarWidgetActivatedEventArgs widgetArgs)
            {
                var rootFrame = Window.Current.Content as Frame ?? new Frame();

                if (widgetArgs.IsLaunchActivation)
                {
                    _widget = new XboxGameBarWidget(
                        widgetArgs,
                        Window.Current.CoreWindow,
                        rootFrame);

                    rootFrame.Navigate(typeof(CrosshairPage), _widget);
                    Window.Current.Content = rootFrame;
                    Window.Current.Activate();

                    Window.Current.Closed += OnWindowClosed;
                }
            }
        }

        protected override void OnBackgroundActivated(BackgroundActivatedEventArgs args)
        {
            base.OnBackgroundActivated(args);

            if (args.TaskInstance.TriggerDetails is AppServiceTriggerDetails details
                && details.CallerPackageFamilyName == Package.Current.Id.FamilyName)
            {
                var deferral = args.TaskInstance.GetDeferral();
                args.TaskInstance.Canceled += (s, e) => deferral.Complete();

                AppServiceClient.Instance.Initialize(details.AppServiceConnection, deferral);
            }
        }

        private void OnWindowClosed(object sender, Windows.UI.Core.CoreWindowEventArgs e)
        {
            Window.Current.Closed -= OnWindowClosed;
            _widget = null;
        }
    }
}
