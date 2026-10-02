using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;
namespace TeslaMurphy;
public sealed partial class MainWindow : Window
{
    private AppWindowPresenter windowedPresenter;
    private OverlappedPresenterState windowedState;
    private bool fullscreenKeyHeld;
    public MainWindow()
    {
        InitializeComponent();
        RootFrame.AddHandler(UIElement.KeyUpEvent, new KeyEventHandler(Root_KeyUp), true);
        Activated += (_, args) =>
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated) fullscreenKeyHeld = false;
        };
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Maximize();
        }
    }

    private void Root_KeyUp(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.F11) fullscreenKeyHeld = false;
    }
    private void Fullscreen_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (fullscreenKeyHeld) return;
        fullscreenKeyHeld = true;
        if (AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen)
        {
            AppWindow.SetPresenter(windowedPresenter ?? OverlappedPresenter.Create());
            if (AppWindow.Presenter is OverlappedPresenter restored)
            {
                if (windowedState == OverlappedPresenterState.Maximized) restored.Maximize();
                else restored.Restore();
            }
        }
        else
        {
            windowedPresenter = AppWindow.Presenter;
            windowedState = (windowedPresenter as OverlappedPresenter)?.State ?? OverlappedPresenterState.Restored;
            AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        }
    }
}
