using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using HelixToolkit.SharpDX.Utilities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using TeslaMurphy.Models;
using TeslaMurphy.Services;

namespace TeslaMurphy.Views;

public sealed partial class Model3HelixPage : Page
{
    private readonly Model3VehicleController vehicle;
    private readonly CancellationTokenSource lifetime = new();
    private Model3HelixScene scene;
    private bool initializing, ready, leaving, settingsAttached;
    private bool hotspotUpdateQueued;
    private string selectedHotspot;
    private bool hudHidden;
    private bool? shownShowroom, shownInterior;
    private bool? shownDriving, shownDriveRunning;
    private long lastDriveHud;
    private bool driveLaneWasChanging;

    public Model3HelixPage()
    {
        InitializeComponent();
        var resources = new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader();
        Model3Text.Resolve = resources.GetString;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(FlashLightsButton, Model3Text.Get("闪灯"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(PortHotspot, Model3Text.Get("查看充电口状态"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(DoorsHotspot, Model3Text.Get("查看车锁状态"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(WheelHotspot, Model3Text.Get("查看车轮信息"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(CloseHotspotButton, Model3Text.Get("关闭状态卡"));
        SetDriveButtonHint(DriveSettingsButton, Model3Text.Get("行车设置"));
        SetDriveButtonHint(DriveEndButton, Model3Text.Get("结束模拟"));
        vehicle = new Model3VehicleController(new Model3VehicleApi(), VehicleDataRefreshTracker.MarkChanged);
        vehicle.Changed += Vehicle_Changed;
        HelixViewport.RenderExceptionOccurred += Viewport_RenderExceptionOccurred;
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
        bool changed = vehicle.Bind(new Model3VehicleContext(settings.Current_carvin, settings.Base_URL,
            settings.IsTestMode, !string.IsNullOrWhiteSpace(settings.Access_token)));
        if (changed)
        {
            scene?.ResetWindows();
            scene?.ResetTrunk();
            scene?.ResetFrunk();
            scene?.SetAllDoorsOpen(false);
        }
        return changed;
    }

    private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(AppSettings.Current_carvin) or nameof(AppSettings.Base_URL)
            or nameof(AppSettings.IsTestMode) or nameof(AppSettings.Access_token))) return;
        DispatcherQueue.TryEnqueue(async () =>
        {
            if (leaving) return;
            if (BindCurrentVehicle() && ready) await vehicle.HandleAsync("refresh");
        });
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (initializing || leaving) return;
        initializing = true;
        try
        {
            if (RuntimeInformation.ProcessArchitecture is not (Architecture.X64 or Architecture.X86))
                throw new PlatformNotSupportedException(Model3Text.Get("当前模型导入支持 x64/x86，请运行 x64 版本。"));
            string path = Path.Combine(AppContext.BaseDirectory, "Assets", "Model3", "model3.glb");
            if (!File.Exists(path)) throw new FileNotFoundException(Model3Text.Get("未找到本地 Model 3 模型资源。"));
            scene = new Model3HelixScene(HelixViewport);
            scene.SetLightTheme(ActualTheme == ElementTheme.Light);
            scene.ViewChanged += QueueHotspotPositions;
            await scene.LoadAsync(path, lifetime.Token);
            if (leaving || lifetime.IsCancellationRequested) return;
            ready = true;
            UpdateVisibleArea();
            ModelProgress.IsActive = false;
            LoadingOverlay.Visibility = Visibility.Collapsed;
            Vehicle_Changed(vehicle.Current);
            await vehicle.HandleAsync("refresh");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!leaving) ShowRenderError(Model3Text.Get("原生场景加载失败。") + ex.Message); }
    }

    private void Vehicle_Changed(Model3VehicleUpdate update)
    {
        if (leaving) return;
        VehicleNameText.Text = update.VehicleName;
        string mode = update.Mode switch { "live" => Model3Text.Get("实时车辆"), "demo" => Model3Text.Get("演示模式"), _ => Model3Text.Get("未连接车辆") };
        if (DateTimeOffset.TryParse(update.LastUpdated, out var updated)) mode += " · " + updated.ToLocalTime().ToString("HH:mm:ss");
        ConnectionText.Text = mode;
        VehicleStateText.Text = (update.State.Locked.HasValue ? update.State.Locked.Value ? Model3Text.Get("已上锁") : Model3Text.Get("已解锁") : Model3Text.Get("锁车状态未知"))
            + " · " + (update.State.PortOpen.HasValue ? update.State.PortOpen.Value ? Model3Text.Get("充电口已打开") : Model3Text.Get("充电口已关闭") : Model3Text.Get("充电口状态未知"));
        StatusText.Text = update.Status;
        ModeNoteText.Text = update.Mode switch
        {
            "live" => Model3Text.Get("操作将发送到真实车辆"),
            "demo" => Model3Text.Get("仅改变模型，不发送车辆指令"),
            _ => Model3Text.Get("登录并选择车辆后可操作")
        };
        OperationErrorInfo.Message = update.Error ?? "";
        OperationErrorInfo.IsOpen = !string.IsNullOrEmpty(update.Error);
        OperationErrorInfo.Visibility = scene?.IsDriving != true && OperationErrorInfo.IsOpen ? Visibility.Visible : Visibility.Collapsed;
        CommandProgress.IsActive = update.Busy;
        CommandProgress.Visibility = update.Busy ? Visibility.Visible : Visibility.Collapsed;
        bool canAct = ready && update.CanControl && !update.Busy && scene?.IsDriving != true;
        PortButtonLabel.Text = update.State.PortOpen == true ? Model3Text.Get("关闭充电口") : Model3Text.Get("打开充电口");
        PortButton.IsEnabled = FlashLightsButton.IsEnabled = canAct;
        RefreshButton.IsEnabled = ready && update.CanControl && !update.Busy;
        UpdateLockButton(update.State.Locked, canAct);
        PortViewButton.IsEnabled = ResetViewButton.IsEnabled = ready;
        if (ready)
        {
            scene.Update(update.State.Locked, update.State.PortOpen, update.Action);
            scene.UpdateWindows(update.State.Windows, update.Action);
            scene.UpdateTrunk(update.State.TrunkOpen, update.Action);
            scene.UpdateFrunk(update.State.FrunkOpen, update.Action, update.Mode == "demo");
        }
        UpdateHotspotCard(update);
        UpdateInteriorControls(update);
        QueueHotspotPositions();
    }

    private void QueueHotspotPositions()
    {
        if (leaving || hotspotUpdateQueued) return;
        hotspotUpdateQueued = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            hotspotUpdateQueued = false;
            if (leaving) return;
            ShowroomButton.IsEnabled = InteriorButton.IsEnabled = ready;
            DriveButton.IsEnabled = ready;
            bool driving = scene?.IsDriving == true, running = scene?.DriveRunning == true;
            bool driveModeChanged = shownDriving != driving;
            if (driveModeChanged)
            {
                shownDriving = driving;
                DriveControls.Visibility = driving ? Visibility.Visible : Visibility.Collapsed;
                ActionsGrid.Visibility = FooterDivider.Visibility = VehicleStatusFooter.Visibility = driving ? Visibility.Collapsed : Visibility.Visible;
                OperationErrorInfo.Visibility = !driving && OperationErrorInfo.IsOpen ? Visibility.Visible : Visibility.Collapsed;
                FooterPanel.Padding = driving ? new Thickness(12, 10, 12, 10) : new Thickness(16, 14, 16, 14);
                FooterContent.Spacing = driving ? 0 : 12;
                if (!driving) DriveSettingsFlyout.Hide();
                var current = vehicle.Current;
                bool canAct = ready && current.CanControl && !current.Busy && !driving;
                PortButton.IsEnabled = FlashLightsButton.IsEnabled = canAct;
                UpdateLockButton(current.State.Locked, canAct);
                UpdateHotspotCard(current);
                UpdateInteriorControls(current);
            }
            if (shownDriveRunning != running || driveModeChanged)
            {
                shownDriveRunning = running;
                DriveButton.Content = driving ? running ? Model3Text.Get("暂停行车") : Model3Text.Get("继续行车") : Model3Text.Get("模拟行车");
                DrivePauseIcon.Glyph = running ? "\uE769" : "\uE768";
                DrivePauseButton.IsEnabled = ready;
                SetDriveButtonHint(DrivePauseButton, running ? Model3Text.Get("暂停行车") : Model3Text.Get("继续行车"));
            }
            if (driving && (driveModeChanged || Environment.TickCount64 - lastDriveHud >= 100))
            {
                lastDriveHud = Environment.TickCount64;
                DriveStatus.Text = Model3Text.Get("模拟速度：") + scene.DriveSpeed.ToString("0", System.Globalization.CultureInfo.CurrentCulture) + " km/h";
                UpdateDriveControls();
            }
            bool showroom = scene?.IsShowroom == true, interior = scene?.IsInterior == true;
            if (shownShowroom != showroom)
            {
                shownShowroom = showroom;
                ShowroomButton.Content = showroom ? Model3Text.Get("暂停展厅") : Model3Text.Get("展厅模式");
            }
            if (shownInterior != interior)
            {
                shownInterior = interior;
                InteriorButton.Content = interior ? Model3Text.Get("返回车外") : Model3Text.Get("内饰视角");
                DriveInteriorButton.Content = InteriorButton.Content;
                UpdateInteriorControls(vehicle.Current);
            }
            UpdateHotspotVisibility();
            PositionHotspot(PortHotspot, "port");
            PositionHotspot(DoorsHotspot, "doors");
            PositionHotspot(WheelHotspot, "wheel");
            PositionHotspot(TrunkHotspot, "trunk");
            PositionHotspot(FrunkHotspot, "frunk");
        });
    }

    private void UpdateHotspotVisibility()
    {
        bool show = ready && !leaving && !hudHidden && scene?.IsDriving != true && scene?.IsInterior != true;
        HotspotCanvas.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        HotspotCard.Visibility = show && selectedHotspot != null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateInteriorControls(Model3VehicleUpdate update)
    {
        bool visible = ready && scene?.IsInterior == true && !hudHidden && !leaving;
        InteriorControlCard.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        bool canAct = visible && update.CanControl && !update.Busy && scene?.IsDriving != true;
        ClimateToggleButton.IsEnabled = RemoteStartButton.IsEnabled = canAct;
        ClimateStateText.Text = update.State.ClimateOn switch
        {
            true => Model3Text.Get("空调已开启"), false => Model3Text.Get("空调已关闭"), _ => Model3Text.Get("空调状态暂未返回")
        };
        ClimateToggleButton.Content = update.State.ClimateOn switch
        {
            true => Model3Text.Get("关闭空调"), false => Model3Text.Get("开启空调"), _ => Model3Text.Get("刷新状态")
        };
        InsideTemperatureText.Text = Temperature(update.State.InsideTemperature);
        OutsideTemperatureText.Text = Temperature(update.State.OutsideTemperature);
        TemperatureUnavailableNote.Visibility = update.Mode == "live" &&
            (!update.State.InsideTemperature.HasValue || !update.State.OutsideTemperature.HasValue)
            ? Visibility.Visible : Visibility.Collapsed;
        InteriorRefreshButton.IsEnabled = visible && update.CanControl && !update.Busy;
        InteriorFeedbackText.Text = scene?.IsDriving == true ? Model3Text.Get("模拟行车中，真实车辆操作暂时禁用。")
            : update.Error ?? (update.Busy ? Model3Text.Get("正在处理车辆操作…") : update.Mode switch
            {
                "live" => Model3Text.Get("操作将发送到真实车辆。") + update.Status,
                "demo" => update.Status,
                _ => Model3Text.Get("登录并选择车辆后可以操作。")
            });
    }

    private static string Temperature(double? value) => value.HasValue
        ? value.Value.ToString("0.#", System.Globalization.CultureInfo.CurrentCulture) + " °C" : Model3Text.Get("暂未返回");

    private async void ClimateToggle_Click(object sender, RoutedEventArgs e)
    {
        if (hudHidden || scene?.IsInterior != true) return;
        await SendCommandAsync(vehicle.Current.State.ClimateOn switch { true => "climateOff", false => "climateOn", _ => "refresh" });
    }

    private async void RemoteStart_Click(object sender, RoutedEventArgs e)
    {
        if (hudHidden || scene?.IsInterior != true) return;
        await SendCommandAsync("remoteStart");
    }

    private void PositionHotspot(Button button, string id)
    {
        if (!ready || hudHidden || scene == null || scene.IsDriving || !scene.TryProjectHotspot(id, out var point))
        {
            button.Visibility = Visibility.Collapsed;
            return;
        }
        var top = SceneSpace.TransformToVisual(HelixViewport).TransformPoint(new Windows.Foundation.Point(0, 0)).Y;
        var width = Math.Max(100, button.ActualWidth);
        var height = Math.Max(40, button.ActualHeight);
        if (point.X < width / 2 + 8 || point.X > HelixViewport.ActualWidth - width / 2 - 8 ||
            point.Y < top + height / 2 || point.Y > top + SceneSpace.ActualHeight - height / 2 - 20)
        {
            button.Visibility = Visibility.Collapsed;
            return;
        }
        Canvas.SetLeft(button, point.X - width / 2);
        Canvas.SetTop(button, point.Y - height / 2);
        button.Visibility = Visibility.Visible;
    }

    private void Hotspot_Click(object sender, RoutedEventArgs e)
    {
        if (!ready || leaving || hudHidden || scene?.IsDriving == true || scene?.IsInterior == true) return;
        selectedHotspot = (string)((Button)sender).Tag;
        HotspotCard.Visibility = Visibility.Visible;
        UpdateHotspotCard(vehicle.Current);
    }

    private static string LockAction(bool? locked) => locked switch
    {
        true => "unlock", false => "lock", _ => "refresh"
    };
    private void UpdateLockButton(bool? locked, bool canAct)
    {
        LockToggleButton.IsEnabled = canAct;
        LockToggleLabel.Text = locked switch
        {
            true => Model3Text.Get("解锁"), false => Model3Text.Get("上锁"), _ => Model3Text.Get("刷新状态")
        };
        LockToggleIcon.Glyph = locked switch { true => "\uE785", false => "\uE72E", _ => "\uE72C" };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(LockToggleButton, LockToggleLabel.Text);
    }

    private void CloseHotspot_Click(object sender, RoutedEventArgs e)
    {
        selectedHotspot = null;
        HotspotCard.Visibility = Visibility.Collapsed;
    }

    private void UpdateHotspotCard(Model3VehicleUpdate update)
    {
        if (selectedHotspot == null) return;
        bool port = selectedHotspot == "port", doors = selectedHotspot == "doors", trunk = selectedHotspot == "trunk", frunk = selectedHotspot == "frunk";
        HotspotTitle.Text = port ? Model3Text.Get("充电口") : doors ? Model3Text.Get("车辆门锁") : trunk ? Model3Text.Get("后备箱") : frunk ? Model3Text.Get("前备箱") : Model3Text.Get("车轮");
        HotspotState.Text = port
            ? update.State.PortOpen switch { true => Model3Text.Get("充电口已打开"), false => Model3Text.Get("充电口已关闭"), _ => Model3Text.Get("充电口状态未知") }
            : doors ? update.State.Locked switch { true => Model3Text.Get("车辆已上锁"), false => Model3Text.Get("车辆已解锁"), _ => Model3Text.Get("车锁状态未知") }
            : trunk ? update.State.TrunkOpen switch { true => Model3Text.Get("后备箱已打开"), false => Model3Text.Get("后备箱已关闭"), _ => Model3Text.Get("后备箱状态暂未返回") }
            : frunk ? update.State.FrunkOpen switch { true => Model3Text.Get("前备箱未关闭"), false => Model3Text.Get("前备箱已关闭"), _ => Model3Text.Get("前备箱状态暂未返回") }
            : Model3Text.Get("胎压（车辆最近测量值）");
        HotspotDetail.Text = port
            ? Model3Text.Get("充电状态：") + (update.State.ChargingState switch
            {
                "Disconnected" => Model3Text.Get("未连接充电线"), "Charging" => Model3Text.Get("正在充电"), "Complete" => Model3Text.Get("充电完成"),
                "Stopped" => Model3Text.Get("充电已停止"), "Starting" => Model3Text.Get("准备充电"), "NoPower" => Model3Text.Get("无充电电源"),
                null or "" => Model3Text.Get("暂未返回"), _ => update.State.ChargingState
            }) + Model3Text.Get("。关闭口盖时会检查车辆返回的充电状态。")
            : doors ? Model3Text.Get("这里显示整车门锁状态。车门和后视镜动画为示意，不代表真实车门开合状态。")
            : trunk ? Model3Text.Get("实车打开已接入 API。当前签名代理只支持打开；关闭请使用 Tesla 应用或车上按钮，随后刷新状态。动画开度为示意。")
            : frunk ? Model3Text.Get("打开会释放实车前备箱锁扣，仍需手动抬起盖板；关闭也需手动完成。模型微抬为示意，不代表实际开度。")
            : Model3Text.Get("左前：") + Pressure(update.State.Tires?.FrontLeft) + Model3Text.Get("\n右前：") + Pressure(update.State.Tires?.FrontRight)
                + Model3Text.Get("\n左后：") + Pressure(update.State.Tires?.RearLeft) + Model3Text.Get("\n右后：") + Pressure(update.State.Tires?.RearRight)
                + Model3Text.Get("\n车辆停放时可能返回上次行驶的测量值；缺失或零值显示为暂未返回。");
        HotspotCommand.Visibility = port || doors || trunk || frunk ? Visibility.Visible : Visibility.Collapsed;
        HotspotCommand.Content = port ? update.State.PortOpen == true ? Model3Text.Get("关闭充电口") : Model3Text.Get("打开充电口")
            : trunk ? update.State.TrunkOpen == true ? Model3Text.Get("刷新状态") : Model3Text.Get("打开后备箱")
            : frunk ? update.State.FrunkOpen == true ? Model3Text.Get("刷新状态") : Model3Text.Get("打开前备箱")
            : update.State.Locked switch
            {
                true => Model3Text.Get("解锁车辆"), false => Model3Text.Get("上锁车辆"), _ => Model3Text.Get("刷新状态")
            };
        HotspotCommand.IsEnabled = ready && update.CanControl && !update.Busy && scene?.IsDriving != true;
        HotspotView.Visibility = port ? Visibility.Visible : Visibility.Collapsed;
        HotspotView.IsEnabled = ready;
        FrunkControls.Visibility = frunk ? Visibility.Visible : Visibility.Collapsed;
        FrunkDemoButton.Content = scene?.IsFrunkFullyOpen == true ? Model3Text.Get("模拟关闭前备箱") : Model3Text.Get("模拟完全打开前备箱");
        FrunkDemoButton.IsEnabled = ready && scene?.IsDriving != true;
        TrunkControls.Visibility = trunk ? Visibility.Visible : Visibility.Collapsed;
        TrunkDemoButton.Content = scene?.IsTrunkOpen == true ? Model3Text.Get("模拟关闭后备箱") : Model3Text.Get("模拟打开后备箱");
        TrunkDemoButton.IsEnabled = ready && scene?.IsDriving != true;
        WindowControls.Visibility = doors ? Visibility.Visible : Visibility.Collapsed;
        DoorControls.Visibility = doors ? Visibility.Visible : Visibility.Collapsed;
        WindowVehicleControls.Visibility = doors ? Visibility.Visible : Visibility.Collapsed;
        VentWindowsButton.IsEnabled = CloseWindowsButton.IsEnabled = ready && update.CanControl && !update.Busy && scene?.IsDriving != true;
        WindowVehicleStateText.Text = update.State.Windows?.Matches(false) == true ? Model3Text.Get("车窗已关闭")
            : update.State.Windows?.Matches(true) == true ? Model3Text.Get("四扇车窗已打开")
            : update.State.Windows?.AnyOpen == true ? Model3Text.Get("部分车窗未关闭") : Model3Text.Get("车窗状态暂未返回");
        WindowCommandNote.Text = update.Mode switch
        {
            "live" => Model3Text.Get("通风与关窗会发送到真实车辆。"),
            "demo" => Model3Text.Get("演示模式：仅改变模型。"),
            _ => Model3Text.Get("登录并选择车辆后可以操作。")
        };
        if (doors) { UpdateWindowActions(); UpdateDoorActions(); }
        HotspotFeedback.Text = update.Error ?? (update.Busy ? Model3Text.Get("正在处理车辆操作…") : update.Mode switch
        {
            "live" => Model3Text.Get("操作将发送到真实车辆。") + update.Status,
            "demo" => Model3Text.Get("演示模式：仅改变模型。"),
            _ => Model3Text.Get("登录并选择车辆后可以操作。")
        });
    }

    private async void HotspotCommand_Click(object sender, RoutedEventArgs e)
    {
        if (selectedHotspot == "port") await SendCommandAsync(vehicle.Current.State.PortOpen == true ? "closePort" : "openPort");
        else if (selectedHotspot == "frunk") await SendCommandAsync(vehicle.Current.State.FrunkOpen == true ? "refresh" : "openFrunk");
        else if (selectedHotspot == "trunk") await SendCommandAsync(vehicle.Current.State.TrunkOpen == true ? "refresh" : "openTrunk");
        else if (selectedHotspot == "doors") await SendCommandAsync(LockAction(vehicle.Current.State.Locked));
    }

    private void GarageBack_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack) Frame.GoBack();
        else Frame.Navigate(typeof(CarPage));
    }
    private void FrunkDemo_Click(object sender, RoutedEventArgs e)
    {
        if (!ready || leaving || scene == null || scene.IsDriving) return;
        scene.ToggleFrunkDemo();
        UpdateHotspotCard(vehicle.Current);
    }
    private void TrunkDemo_Click(object sender, RoutedEventArgs e)
    {
        if (!ready || leaving || scene == null || scene.IsDriving) return;
        scene.ToggleTrunkDemo();
        UpdateHotspotCard(vehicle.Current);
    }
    private void UpdateWindowActions()
    {
        FrontLeftWindowAction.Text = WindowAction("lf");
        FrontRightWindowAction.Text = WindowAction("rf");
        RearLeftWindowAction.Text = WindowAction("lr");
        RearRightWindowAction.Text = WindowAction("rr");
    }

    private void UpdateDoorActions()
    {
        FrontLeftDoorAction.Text = DoorAction("lf");
        FrontRightDoorAction.Text = DoorAction("rf");
        RearLeftDoorAction.Text = DoorAction("lr");
        RearRightDoorAction.Text = DoorAction("rr");
    }
    private string DoorAction(string id) => scene?.IsDoorOpen(id) == true
        ? Model3Text.Get("关闭") : Model3Text.Get("打开");
    private void DoorToggle_Click(object sender, RoutedEventArgs e)
    {
        if (!ready || leaving || scene == null) return;
        scene.ToggleDoor((string)((Button)sender).Tag);
        UpdateDoorActions();
    }
    private void AllDoors_Click(object sender, RoutedEventArgs e)
    {
        if (!ready || leaving || scene == null) return;
        scene.SetAllDoorsOpen((string)((Button)sender).Tag == "open");
        UpdateDoorActions();
    }
    private string WindowAction(string id) => scene?.IsWindowLowered(id) == true
        ? Model3Text.Get("升起") : Model3Text.Get("降下");

    private void WindowToggle_Click(object sender, RoutedEventArgs e)
    {
        if (!ready || leaving || scene == null) return;
        scene.ToggleWindow((string)((Button)sender).Tag);
        UpdateWindowActions();
    }
    private void AllWindows_Click(object sender, RoutedEventArgs e)
    {
        if (!ready || leaving || scene == null) return;
        scene.SetAllWindowsLowered((string)((Button)sender).Tag == "down");
        UpdateWindowActions();
    }

    private async void VehicleWindows_Click(object sender, RoutedEventArgs e)
        => await SendCommandAsync((string)((Button)sender).Tag);

    private async Task SendCommandAsync(string action)
    {
        if (!ready || leaving) return;
        if (scene.IsDriving && action != "refresh") return;
        // A click from the previous vehicle must not become a command for a
        // newly selected vehicle before its state is displayed.
        if (BindCurrentVehicle())
        {
            await vehicle.HandleAsync("refresh");
            return;
        }
        await vehicle.HandleAsync(action);
    }
    private async void PortButton_Click(object sender, RoutedEventArgs e)
        => await SendCommandAsync(vehicle.Current.State.PortOpen == true ? "closePort" : "openPort");
    private async void FlashLightsButton_Click(object sender, RoutedEventArgs e)
        => await SendCommandAsync("flashLights");
    private async void LockToggleButton_Click(object sender, RoutedEventArgs e)
        => await SendCommandAsync(LockAction(vehicle.Current.State.Locked));
    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await SendCommandAsync("refresh");
    private void PortViewButton_Click(object sender, RoutedEventArgs e) { if (ready && !leaving) scene.ViewPort(); }
    private void ResetViewButton_Click(object sender, RoutedEventArgs e) { if (ready && !leaving) scene.ResetView(); }
    private void ShowroomButton_Click(object sender, RoutedEventArgs e) { if (ready && !leaving) scene.ToggleShowroom(); }
    private void InteriorButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ready || leaving) return;
        selectedHotspot = null;
        HotspotCard.Visibility = Visibility.Collapsed;
        if (scene.IsInterior) scene.ResetView(); else scene.ViewInterior();
    }
    private void DriveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ready || leaving) return;
        selectedHotspot = null;
        HotspotCard.Visibility = Visibility.Collapsed;
        scene.SetDriveSpeed(DriveSpeedSlider.Value);
        DriveFeedbackText.Text = "";
        scene.ToggleDriving();
        UpdateDriveControls();
    }
    private static void SetDriveButtonHint(Button button, string text)
    {
        ToolTipService.SetToolTip(button, text);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, text);
    }
    private void DriveInterior_Click(object sender, RoutedEventArgs e)
    {
        DriveSettingsFlyout.Hide();
        InteriorButton_Click(sender, e);
    }
    private void DriveResetView_Click(object sender, RoutedEventArgs e)
    {
        DriveSettingsFlyout.Hide();
        ResetViewButton_Click(sender, e);
    }
    private void DriveEnvironment_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ready || leaving || scene == null || DriveRoadPicker?.SelectedItem is not ComboBoxItem road || DriveWeatherPicker?.SelectedItem is not ComboBoxItem weather) return;
        if (Enum.TryParse<Model3RoadKind>((string)road.Tag, out var kind) && Enum.TryParse<Model3WeatherKind>((string)weather.Tag, out var condition))
            scene.SetDriveEnvironment(kind, condition);
    }

    private void UpdateDriveControls()
    {
        if (driveLaneWasChanging && !scene.DriveChangingLane) DriveFeedbackText.Text = "";
        driveLaneWasChanging = scene.DriveChangingLane;
        DriveFeedbackText.Visibility = string.IsNullOrEmpty(DriveFeedbackText.Text) ? Visibility.Collapsed : Visibility.Visible;
        DriveLeftLaneButton.IsEnabled = ready && scene.CanChangeDriveLane(1);
        DriveRightLaneButton.IsEnabled = ready && scene.CanChangeDriveLane(-1);
        DriveLeftSignalButton.IsEnabled = DriveRightSignalButton.IsEnabled = ready && !scene.DriveChangingLane;
        DriveBrakeButton.IsEnabled = ready;
        DriveLeftSignalLabel.Text = scene.DriveSignal == 1 ? Model3Text.Get("左灯已开启") : Model3Text.Get("左转向灯");
        DriveRightSignalLabel.Text = scene.DriveSignal == -1 ? Model3Text.Get("右灯已开启") : Model3Text.Get("右转向灯");
        DriveBrakeLabel.Text = scene.DriveBraking ? Model3Text.Get("松开刹车") : Model3Text.Get("刹车");
        DriveLaneText.Text = scene.DriveChangingLane ? Model3Text.Get("正在变道…") : scene.DriveLane == 1 ? Model3Text.Get("左侧车道") : Model3Text.Get("右侧车道");
    }

    private void DriveSignal_Click(object sender, RoutedEventArgs e)
    {
        if (!ready || leaving || scene?.IsDriving != true) return;
        scene.ToggleDriveSignal(int.Parse((string)((Button)sender).Tag, System.Globalization.CultureInfo.InvariantCulture));
        UpdateDriveControls();
    }
    private void DriveBrake_Click(object sender, RoutedEventArgs e)
    {
        if (!ready || leaving || scene?.IsDriving != true) return;
        scene.ToggleDriveBrake(); UpdateDriveControls();
    }
    private void DriveLane_Click(object sender, RoutedEventArgs e)
    {
        if (!ready || leaving || scene?.IsDriving != true) return;
        int direction = int.Parse((string)((Button)sender).Tag, System.Globalization.CultureInfo.InvariantCulture);
        DriveFeedbackText.Text = scene.ChangeDriveLane(direction) ? Model3Text.Get("转向灯已开启，正在变道。") : Model3Text.Get("相邻车道有车或暂不满足变道条件，请稍后重试。");
        UpdateDriveControls();
    }

    private void StopDriving_Click(object sender, RoutedEventArgs e) { DriveSettingsFlyout.Hide(); if (ready && !leaving) scene.StopDriving(); }
    private void DriveSpeed_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        => scene?.SetDriveSpeed(e.NewValue);
    private static string Pressure(double? bar) => bar.HasValue
        ? (bar.Value * 100).ToString("0", System.Globalization.CultureInfo.CurrentCulture) + " kPa" : Model3Text.Get("暂未返回");

    private void HudToggle_Click(object sender, RoutedEventArgs e)
    {
        DriveSettingsFlyout.Hide();
        hudHidden = !hudHidden;
        HeaderHud.Visibility = FooterHud.Visibility = hudHidden ? Visibility.Collapsed : Visibility.Visible;
        SceneSpace.Opacity = hudHidden ? 0 : 1;
        UpdateHotspotVisibility();
        UpdateInteriorControls(vehicle.Current);
        HudToggle.Content = hudHidden ? Model3Text.Get("显示 HUD") : Model3Text.Get("隐藏 HUD");
        GarageNavigationBar.Margin = hudHidden ? new Thickness(16) : new Thickness(16, 10, 16, 8);
        UpdateVisibleArea();
    }

    private void Page_ActualThemeChanged(FrameworkElement sender, object args)
    {
        if (!leaving) scene?.SetLightTheme(ActualTheme == ElementTheme.Light);
    }

    private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (VehicleActions == null || ViewActions == null) return;
        DriveSettingsScroll.Width = Math.Max(160, Math.Min(320, e.NewSize.Width - 80));
        DriveSettingsScroll.MaxHeight = Math.Max(120, Math.Min(360, e.NewSize.Height - 150));
        bool narrow = e.NewSize.Width < 1450;
        Grid.SetRow(ViewActions, narrow ? 1 : 0);
        Grid.SetColumn(ViewActions, narrow ? 0 : 1);
        Grid.SetColumnSpan(ViewActions, narrow ? 2 : 1);
        ViewActions.Orientation = e.NewSize.Width < 800 ? Orientation.Vertical : Orientation.Horizontal;
        ViewActions.HorizontalAlignment = narrow ? HorizontalAlignment.Center : HorizontalAlignment.Right;
        VehicleActions.Width = narrow ? double.NaN : 460;
        bool compactActions = e.NewSize.Width < 600;
        FlashActionColumn.Width = compactActions ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        FlashActionColumn.MinWidth = compactActions ? 0 : 96;
        Grid.SetRow(FlashLightsButton, compactActions ? 1 : 0);
        Grid.SetColumn(FlashLightsButton, compactActions ? 0 : 2);
        Grid.SetColumnSpan(FlashLightsButton, compactActions ? 2 : 1);
        FlashLightsButton.Margin = compactActions ? new Thickness(0, 8, 0, 0) : new Thickness(0);
        VehicleActions.HorizontalAlignment = narrow ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
        ModeNoteText.Visibility = e.NewSize.Width < 700 ? Visibility.Collapsed : Visibility.Visible;
        VehicleStateText.MaxWidth = e.NewSize.Width < 600 ? 145 : double.PositiveInfinity;
        VehicleStateText.TextWrapping = TextWrapping.Wrap;
        HotspotCard.MaxWidth = Math.Max(180, Math.Min(310, e.NewSize.Width - 32));
        InteriorControlCard.MaxWidth = Math.Max(1, Math.Min(310, e.NewSize.Width - 32));
        QueueHotspotPositions();
    }

    private void SceneSpace_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Keep the expanded control card within the scene between header/footer.
        HotspotScroll.MaxHeight = Math.Max(1, Math.Min(520, e.NewSize.Height - 64));
        InteriorControlScroll.MaxHeight = Math.Max(1, Math.Min(520, e.NewSize.Height - 64));
        UpdateVisibleArea();
    }
    private void UpdateVisibleArea()
    {
        if (scene == null || leaving || SceneSpace.ActualHeight == 0) return;
        if (hudHidden) { scene.SetVisibleArea(8, 8); return; }
        var position = SceneSpace.TransformToVisual(HelixViewport).TransformPoint(new Windows.Foundation.Point(0, 0));
        double bottomInset = Math.Max(0, HelixViewport.ActualHeight - position.Y - SceneSpace.ActualHeight + 28);
        scene.SetVisibleArea(position.Y + 8, bottomInset);
        QueueHotspotPositions();
    }

    private void Viewport_RenderExceptionOccurred(object sender, RelayExceptionEventArgs e)
    {
        e.Handled = true;
        DispatcherQueue.TryEnqueue(() => { if (!leaving) ShowRenderError(Model3Text.Get("DirectX 渲染中断，请重新打开此页面。")); });
    }

    private void ShowRenderError(string message)
    {
        ready = false;
        HotspotCanvas.Visibility = HotspotCard.Visibility = InteriorControlCard.Visibility = Visibility.Collapsed;
        ClimateToggleButton.IsEnabled = RemoteStartButton.IsEnabled = false;
        lifetime.Cancel();
        vehicle.Dispose();
        scene?.Dispose();
        ModelProgress.IsActive = false;
        LoadingOverlay.Visibility = Visibility.Collapsed;
        RenderErrorInfo.Message = message.Length > 500 ? message.Substring(0, 500) : message;
        RenderErrorInfo.IsOpen = true;
        PortButton.IsEnabled = LockToggleButton.IsEnabled = FlashLightsButton.IsEnabled = RefreshButton.IsEnabled = false;
        PortViewButton.IsEnabled = ResetViewButton.IsEnabled = false;
        StatusText.Text = Model3Text.Get("场景暂不可用，请重新打开此页面。");
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        leaving = true;
        DriveSettingsFlyout.Hide();
        ready = false;
        lifetime.Cancel();
        if (settingsAttached) AppSettings.Instance.PropertyChanged -= Settings_PropertyChanged;
        vehicle.Changed -= Vehicle_Changed;
        vehicle.Dispose();
        HelixViewport.RenderExceptionOccurred -= Viewport_RenderExceptionOccurred;
        if (scene != null) scene.ViewChanged -= QueueHotspotPositions;
        scene?.Dispose();
        lifetime.Dispose();
    }
}
