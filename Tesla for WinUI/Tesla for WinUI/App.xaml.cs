using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;
namespace TeslaMurphy;
public partial class App : Application
{
    public static MainWindow MainWindow { get; private set; }
    internal static Uri PendingAuthentication { get; private set; }
    public App() { InitializeComponent(); }
    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
        var instance = AppInstance.FindOrRegisterForKey("TeslaForWinUI");
        if (!instance.IsCurrent)
        {
            await instance.RedirectActivationToAsync(activation);
            Exit();
            return;
        }
        if (activation.Data is ProtocolActivatedEventArgs protocol) PendingAuthentication = protocol.Uri;
        instance.Activated += (s, e) => MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            if (e.Data is ProtocolActivatedEventArgs callback)
                Views.MainPage.Instance?.NavigateToAuthentication(callback.Uri);
            MainWindow.Activate();
        });
        MainWindow = new MainWindow();
        var frame = (Frame)MainWindow.Content;
        frame.Navigate(typeof(Views.MainPage));
        MainWindow.Activate();
    }
}
