using System;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
namespace TeslaMurphy.Helpers;
internal static class DesktopHelper
{
    public static Task WaitForLoadedAsync(Microsoft.UI.Xaml.FrameworkElement element)
    {
        if (element.IsLoaded && element.XamlRoot != null) return Task.CompletedTask;
        var completion = new TaskCompletionSource();
        Microsoft.UI.Xaml.RoutedEventHandler loaded = null;
        loaded = (s, e) => { element.Loaded -= loaded; completion.TrySetResult(); };
        element.Loaded += loaded;
        return completion.Task;
    }
    public static async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
    {
        dialog.XamlRoot = App.MainWindow.Content.XamlRoot;
        return await dialog.ShowAsync();
    }
    public static Task RunAsync(DispatcherQueue dispatcher, Action action)
    {
        if (dispatcher.HasThreadAccess) { action(); return Task.CompletedTask; }
        var completion = new TaskCompletionSource();
        if (!dispatcher.TryEnqueue(() => { try { action(); completion.SetResult(); } catch (Exception ex) { completion.SetException(ex); } }))
            completion.SetException(new InvalidOperationException("The UI dispatcher is shutting down."));
        return completion.Task;
    }
    public static Task RestartAsync()
    {
        Microsoft.Windows.AppLifecycle.AppInstance.Restart("");
        return Task.CompletedTask;
    }
}
