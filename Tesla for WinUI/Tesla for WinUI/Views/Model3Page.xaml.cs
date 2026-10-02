using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TeslaMurphy.Models;
using TeslaMurphy.Services;

namespace TeslaMurphy.Views;

public sealed partial class Model3Page : Page
{
    private const string ModelHost = "model3.tesla.local";
    private readonly Model3VehicleController vehicle;
    private bool initializing;
    private bool ready;
    private bool leaving;
    private bool settingsAttached;

    public Model3Page()
    {
        InitializeComponent();
        vehicle = new Model3VehicleController(new Model3VehicleApi(), VehicleDataRefreshTracker.MarkChanged);
        vehicle.Changed += Vehicle_Changed;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (!settingsAttached)
        {
            AppSettings.Instance.PropertyChanged += Settings_PropertyChanged;
            settingsAttached = true;
        }
        BindCurrentVehicle();
    }

    private bool BindCurrentVehicle()
    {
        var settings = AppSettings.Instance;
        return vehicle.Bind(new Model3VehicleContext(settings.Current_carvin, settings.Base_URL,
            settings.IsTestMode, !string.IsNullOrWhiteSpace(settings.Access_token)));
    }

    private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(AppSettings.Current_carvin) or nameof(AppSettings.Base_URL)
            or nameof(AppSettings.IsTestMode) or nameof(AppSettings.Access_token))) return;
        // Token rotation keeps the same context; logout, region changes and vehicle changes cancel it.
        DispatcherQueue.TryEnqueue(async () =>
        {
            if (leaving) return;
            if (BindCurrentVehicle() && ready) await vehicle.HandleAsync("refresh");
        });
    }

    private async void ModelWebView_Loaded(object sender, RoutedEventArgs e)
    {
        if (initializing || leaving) return;
        initializing = true;
        try
        {
            string assets = Path.Combine(AppContext.BaseDirectory, "Assets", "Model3");
            if (!File.Exists(Path.Combine(assets, "model3.glb"))) throw new FileNotFoundException("未找到本地 Model 3 模型资源。");
            await ModelWebView.EnsureCoreWebView2Async();
            if (leaving) return;
            var core = ModelWebView.CoreWebView2;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.SetVirtualHostNameToFolderMapping(ModelHost, assets, CoreWebView2HostResourceAccessKind.DenyCors);
            core.WebMessageReceived += Core_WebMessageReceived;
            core.NavigationStarting += Core_NavigationStarting;
            core.NewWindowRequested += Core_NewWindowRequested;
            core.ProcessFailed += Core_ProcessFailed;
            ModelWebView.Source = new Uri("https://" + ModelHost + "/index.html");
        }
        catch (Exception ex) { if (!leaving) ShowError(ex.Message); }
    }

    private static bool IsLocalPage(string address) => Uri.TryCreate(address, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && uri.Host == ModelHost && uri.IsDefaultPort
        && uri.AbsolutePath == "/index.html" && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.UserInfo);

    private async void Core_WebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (leaving || !IsLocalPage(args.Source)) return;
        try
        {
            var message = JObject.Parse(args.WebMessageAsJson);
            switch ((string)message["type"])
            {
                case "ready" when !ready:
                    ready = true;
                    ErrorInfo.IsOpen = false;
                    BindCurrentVehicle();
                    await PushUpdateAsync(vehicle.Current);
                    await vehicle.HandleAsync("refresh");
                    break;
                case "command" when ready:
                    string action = (string)message["action"];
                    if (action is not ("refresh" or "openPort" or "closePort" or "lock" or "unlock")) return;
                    BindCurrentVehicle();
                    await vehicle.HandleAsync(action);
                    // Also release the web UI's pending state if a duplicate/disabled command was ignored.
                    await PushUpdateAsync(vehicle.Current);
                    break;
                case "error": ShowError((string)message["message"] ?? "无法显示 3D 场景。"); break;
            }
        }
        catch (JsonException) { if (!leaving) ShowError("无法读取模型页面消息。"); }
        catch (Exception) { if (!leaving) ShowError("模型页面连接中断，请返回后重新打开。"); }
    }

    private async void Vehicle_Changed(Model3VehicleUpdate update) => await PushUpdateAsync(update);

    private async Task PushUpdateAsync(Model3VehicleUpdate update)
    {
        if (!ready || leaving) return;
        // The browser receives display fields only. Tokens, VIN and API addresses stay in native code.
        string connection = JsonConvert.SerializeObject(new
        {
            mode = update.Mode, vehicleName = update.VehicleName, canControl = update.CanControl,
            busy = update.Busy, status = update.Status, error = update.Error, lastUpdated = update.LastUpdated
        });
        string state = JsonConvert.SerializeObject(new { locked = update.State.Locked, portOpen = update.State.PortOpen });
        string options = JsonConvert.SerializeObject(new { action = update.Action, focusPort = update.Action == "openPort" });
        try
        {
            await ModelWebView.ExecuteScriptAsync("window.model3Demo?.setConnection(" + connection
                + ");window.model3Demo?.applyVehicleState(" + state + "," + options + ");");
        }
        catch (Exception) { if (!leaving) ShowError("模型页面连接中断，请返回后重新打开。"); }
    }

    private void Core_NavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        if (!IsLocalPage(args.Uri)) args.Cancel = true;
    }
    private void Core_NewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args) => args.Handled = true;
    private void Core_ProcessFailed(CoreWebView2 sender, CoreWebView2ProcessFailedEventArgs args)
        => ShowError("3D 渲染进程中断，请返回后重新打开页面。");

    private void ShowError(string message)
    {
        ready = false;
        vehicle.Dispose();
        ErrorInfo.Message = message;
        ErrorInfo.IsOpen = true;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        leaving = true;
        ready = false;
        if (settingsAttached) AppSettings.Instance.PropertyChanged -= Settings_PropertyChanged;
        vehicle.Changed -= Vehicle_Changed;
        vehicle.Dispose();
        var core = ModelWebView.CoreWebView2;
        if (core != null)
        {
            core.WebMessageReceived -= Core_WebMessageReceived;
            core.NavigationStarting -= Core_NavigationStarting;
            core.NewWindowRequested -= Core_NewWindowRequested;
            core.ProcessFailed -= Core_ProcessFailed;
        }
        ModelWebView.Close();
    }
}
