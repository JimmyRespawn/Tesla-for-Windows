using System;
using System.Threading;
using System.Threading.Tasks;

namespace TeslaMurphy.Services;

internal record Model3VehicleContext(string Vin, string BaseUrl, bool IsDemo, bool HasSession);
internal record Model3TirePressures(double? FrontLeft, double? FrontRight, double? RearLeft, double? RearRight);
internal record Model3WindowStates(bool? FrontDriver, bool? FrontPassenger, bool? RearDriver, bool? RearPassenger)
{
    public bool Matches(bool open) => FrontDriver == open && FrontPassenger == open && RearDriver == open && RearPassenger == open;
    public bool AnyOpen => FrontDriver == true || FrontPassenger == true || RearDriver == true || RearPassenger == true;
    public bool Complete => FrontDriver.HasValue && FrontPassenger.HasValue && RearDriver.HasValue && RearPassenger.HasValue;
    public static Model3WindowStates All(bool open) => new(open, open, open, open);
}
internal record Model3VehicleState(bool? Locked, bool? PortOpen, string ChargingState = null, string VehicleName = null, Model3TirePressures Tires = null, Model3WindowStates Windows = null, bool? TrunkOpen = null, bool? FrunkOpen = null, bool? ClimateOn = null, double? InsideTemperature = null, double? OutsideTemperature = null);
internal record Model3CommandResult(bool Success, string Error = null);
internal record Model3VehicleUpdate(string Mode, string VehicleName, bool CanControl, bool Busy,
    string Status, string Error, string LastUpdated, Model3VehicleState State, string Action = null);

internal interface IModel3VehicleApi
{
    Task<Model3VehicleState> ReadAsync(Model3VehicleContext context, CancellationToken cancellationToken);
    Task<Model3CommandResult> SendAsync(Model3VehicleContext context, string action, CancellationToken cancellationToken);
}

// Owns confirmed state independently of the renderer. No credentials enter its updates.
internal sealed class Model3VehicleController : IDisposable
{
    private readonly IModel3VehicleApi api;
    private readonly Action<string> markChanged;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;
    private readonly Func<DateTimeOffset> now;
    private bool? acknowledgedLock, acknowledgedPort, acknowledgedWindows, acknowledgedTrunk, acknowledgedFrunk, acknowledgedClimate;
    private DateTimeOffset lockExpires, portExpires, windowsExpires, trunkExpires, frunkExpires, climateExpires;
    private CancellationTokenSource lifetime = new();
    private Model3VehicleContext context;
    private Model3VehicleState state = new(null, null);
    private bool busy;
    private bool disposed;
    private long generation;
    private string status = Model3Text.Get("请先在主页面登录并选择车辆。");
    private string error;
    private string lastUpdated;

    public Model3VehicleController(IModel3VehicleApi api, Action<string> markChanged,
        Func<TimeSpan, CancellationToken, Task> delay = null, Func<DateTimeOffset> now = null)
    {
        this.api = api;
        this.markChanged = markChanged;
        this.delay = delay ?? ((wait, token) => Task.Delay(wait, token));
        this.now = now ?? (() => DateTimeOffset.UtcNow);
    }

    public event Action<Model3VehicleUpdate> Changed;
    private bool CanControl => !disposed && context != null &&
        (context.IsDemo || context.HasSession && !string.IsNullOrWhiteSpace(context.Vin));
    public Model3VehicleUpdate Current => new(
        context?.IsDemo == true ? "demo" : CanControl ? "live" : "unavailable",
        state.VehicleName ?? "Model 3", CanControl, busy, status, error, lastUpdated, state);

    public bool Bind(Model3VehicleContext next)
    {
        if (disposed || next == context) return false;
        lifetime.Cancel();
        lifetime.Dispose();
        lifetime = new();
        generation++;
        context = next;
        acknowledgedLock = acknowledgedPort = acknowledgedWindows = acknowledgedTrunk = acknowledgedFrunk = acknowledgedClimate = null;
        busy = false;
        error = lastUpdated = null;
        state = next.IsDemo ? new(true, false, "Disconnected", "Model 3", Windows: Model3WindowStates.All(false), TrunkOpen: false, FrunkOpen: false, ClimateOn: false, InsideTemperature: 24, OutsideTemperature: 20) : new(null, null);
        status = next.IsDemo ? Model3Text.Get("演示模式 · 操作仅改变模型。") :
            CanControl ? Model3Text.Get("已选择当前车辆，正在读取状态…") : Model3Text.Get("请先在主页面登录并选择车辆。");
        Publish();
        return true;
    }

    public async Task HandleAsync(string action)
    {
        if (!CanControl || busy || action is not ("refresh" or "lock" or "unlock" or "openPort" or "closePort" or "ventWindows" or "closeWindows" or "openTrunk" or "openFrunk" or "flashLights" or "climateOn" or "climateOff" or "remoteStart")) return;
        if (context.IsDemo)
        {
            state = action switch
            {
                "lock" => state with { Locked = true },
                "unlock" => state with { Locked = false },
                "openPort" => state with { PortOpen = true },
                "closePort" => state with { PortOpen = false },
                "ventWindows" => state with { Windows = Model3WindowStates.All(true) },
                "closeWindows" => state with { Windows = Model3WindowStates.All(false) },
                "openTrunk" => state with { TrunkOpen = true },
                "openFrunk" => state with { FrunkOpen = true },
                "climateOn" => state with { ClimateOn = true },
                "climateOff" => state with { ClimateOn = false },
                _ => state
            };
            status = action == "remoteStart" ? Model3Text.Get("演示模式：模拟无钥匙驾驶确认，不控制真实车辆。") : Model3Text.Get("演示模式 · 操作仅改变模型。");
            error = null;
            Publish(action == "refresh" ? null : action);
            return;
        }

        var captured = context;
        long version = generation;
        var token = lifetime.Token;
        busy = true;
        error = null;
        status = action == "refresh" ? Model3Text.Get("正在读取车辆状态…") : Model3Text.Get("正在向当前车辆发送指令…");
        Publish();
        try
        {
            if (action == "refresh")
            {
                var fresh = await api.ReadAsync(captured, token);
                if (!IsCurrent(version, token)) return;
                AcceptState(fresh);
                status = HasPendingAcknowledgement ? Model3Text.Get("指令已确认，车辆状态同步中，可稍后刷新。") :
                    HasMissingState ? Model3Text.Get("已读取车辆，部分状态暂未返回。") : Model3Text.Get("车辆状态已同步。");
            }
            else
            {
                // Check the latest charge state before closing, even if the displayed state is older.
                if (action == "closePort")
                {
                    var fresh = await api.ReadAsync(captured, token);
                    if (!IsCurrent(version, token)) return;
                    if (fresh.PortOpen == false && (acknowledgedPort != true || now() >= portExpires))
                    {
                        acknowledgedPort = null;
                        AcceptState(fresh);
                        status = Model3Text.Get("充电口已经关闭，状态已同步。");
                        return;
                    }
                    AcceptState(fresh);
                    if (!string.IsNullOrWhiteSpace(fresh.ChargingState) &&
                        !string.Equals(fresh.ChargingState.Trim(), "Disconnected", StringComparison.OrdinalIgnoreCase))
                    {
                        status = Model3Text.Get("充电口未关闭。");
                        error = Model3Text.Get("请先拔下充电线；车辆充电状态为 ") + fresh.ChargingState + Model3Text.Get("，暂不关闭充电口。");
                        return;
                    }
                    // Missing telemetry is not proof of a connected cable.
                    // The vehicle command performs the authoritative interlock.
                }
                token.ThrowIfCancellationRequested();
                // Cancellation cannot recall a command already received by the vehicle.
                // Require a fresh homepage snapshot even when its acknowledgement is lost.
                markChanged?.Invoke(captured.Vin);
                var result = await api.SendAsync(captured, action, token);
                if (!IsCurrent(version, token)) return;
                if (!result.Success)
                {
                    status = Model3Text.Get("车辆未确认指令，模型保留已知状态。");
                    error = result.Error ?? Model3Text.Get("指令未成功，请稍后重试。");
                    return;
                }
                if (action is "flashLights" or "remoteStart")
                {
                    // This is a momentary command, not a Boolean vehicle state.
                    // Do not infer driving or unlock state from remote-start acceptance.
                    status = action == "flashLights" ? Model3Text.Get("车辆已确认闪灯指令。")
                        : Model3Text.Get("车辆已确认无钥匙驾驶请求，请在 2 分钟内上车开始驾驶。");
                    Publish(action);
                    return;
                }
                state = action switch
                {
                    "lock" => state with { Locked = true },
                    "unlock" => state with { Locked = false },
                    "openPort" => state with { PortOpen = true },
                    "closePort" => state with { PortOpen = false },
                    "ventWindows" => state with { Windows = Model3WindowStates.All(true) },
                    "closeWindows" => state with { Windows = Model3WindowStates.All(false) },
                "openTrunk" => state with { TrunkOpen = true },
                "openFrunk" => state with { FrunkOpen = true },
                "climateOn" => state with { ClimateOn = true },
                "climateOff" => state with { ClimateOn = false },
                    _ => state
                };
                if (action is "lock" or "unlock")
                {
                    acknowledgedLock = action == "lock";
                    lockExpires = now().AddSeconds(15);
                }
                else if (action is "openPort" or "closePort")
                {
                    acknowledgedPort = action == "openPort";
                    portExpires = now().AddSeconds(15);
                }
                else if (action is "climateOn" or "climateOff")
                {
                    acknowledgedClimate = action == "climateOn";
                    climateExpires = now().AddSeconds(15);
                }
                else if (action == "openTrunk")
                {
                    acknowledgedTrunk = true;
                    trunkExpires = now().AddSeconds(15);
                }
                else if (action == "openFrunk")
                {
                    acknowledgedFrunk = true;
                    frunkExpires = now().AddSeconds(15);
                }
                else
                {
                    acknowledgedWindows = action == "ventWindows";
                    windowsExpires = now().AddSeconds(15);
                }
                status = Model3Text.Get("车辆已确认指令，正在核对状态…");
                Publish(action);
                // Vehicle data may lag a successful command. Recheck only this
                // operation, without resending the command or waking for reads.
                try
                {
                    bool expected = action is "lock" or "openPort" or "ventWindows" or "openTrunk" or "openFrunk" or "climateOn";
                    for (int attempt = 0; attempt < 3; attempt++)
                    {
                        if (attempt > 0) await delay(TimeSpan.FromMilliseconds(750 * attempt), token);
                        if (!IsCurrent(version, token)) return;
                        var fresh = await api.ReadAsync(captured, token);
                        if (!IsCurrent(version, token)) return;
                        AcceptState(fresh);
                        bool matched = action is "ventWindows" or "closeWindows"
                            ? fresh.Windows?.Matches(expected) == true
                            : action is "climateOn" or "climateOff" ? fresh.ClimateOn == expected
                            : (action == "openFrunk" ? fresh.FrunkOpen : action == "openTrunk" ? fresh.TrunkOpen : action is "lock" or "unlock" ? fresh.Locked : fresh.PortOpen) == expected;
                        if (matched)
                        {
                            status = Model3Text.Get("指令成功，车辆状态已同步。");
                            break;
                        }
                        status = Model3Text.Get("指令已确认，车辆状态同步中，可稍后刷新。");
                    }
                }
                catch (OperationCanceledException)
                {
                    if (!IsCurrent(version, token)) return;
                    status = Model3Text.Get("指令已确认，状态读取暂时超时，可稍后刷新。");
                }
                catch (Exception ex)
                {
                    if (!IsCurrent(version, token)) return;
                    status = Model3Text.Get("指令已确认，暂时无法核对车辆状态。");
                    status += " " + ex.Message;
                }
            }
        }
        catch (OperationCanceledException)
        {
            if (IsCurrent(version, token))
            {
                status = Model3Text.Get("请求超时，模型保留已知状态。");
                error = Model3Text.Get("车辆暂未响应，请稍后重试。");
            }
        }
        catch (Exception ex)
        {
            if (!IsCurrent(version, token)) return;
            status = Model3Text.Get("操作未完成，模型保留已知状态。");
            error = ex.Message;
        }
        finally
        {
            if (IsCurrent(version, token))
            {
                busy = false;
                Publish();
            }
        }
    }

    private bool HasMissingState => state.Locked == null || state.PortOpen == null;
    private bool HasPendingAcknowledgement => acknowledgedLock.HasValue || acknowledgedPort.HasValue || acknowledgedWindows.HasValue || acknowledgedTrunk.HasValue || acknowledgedFrunk.HasValue || acknowledgedClimate.HasValue;
    private bool IsCurrent(long version, CancellationToken token) => !disposed && version == generation && !token.IsCancellationRequested;
    private void AcceptState(Model3VehicleState fresh)
    {
        var time = now();
        if (fresh.Locked == acknowledgedLock || time >= lockExpires) acknowledgedLock = null;
        if (fresh.PortOpen == acknowledgedPort || time >= portExpires) acknowledgedPort = null;
        if (acknowledgedWindows.HasValue && (fresh.Windows?.Matches(acknowledgedWindows.Value) == true || time >= windowsExpires)) acknowledgedWindows = null;
        if (fresh.TrunkOpen == acknowledgedTrunk || time >= trunkExpires) acknowledgedTrunk = null;
        if (fresh.FrunkOpen == acknowledgedFrunk || time >= frunkExpires) acknowledgedFrunk = null;
        if (fresh.ClimateOn == acknowledgedClimate || time >= climateExpires) acknowledgedClimate = null;
        // Preserve only the acknowledged field for a bounded synchronization
        // window. Other observed fields and the cable safety check stay fresh.
        fresh = fresh with { Locked = acknowledgedLock ?? fresh.Locked, PortOpen = acknowledgedPort ?? fresh.PortOpen,
            Windows = acknowledgedWindows.HasValue ? Model3WindowStates.All(acknowledgedWindows.Value) : fresh.Windows, TrunkOpen = acknowledgedTrunk ?? fresh.TrunkOpen,
            FrunkOpen = acknowledgedFrunk ?? fresh.FrunkOpen, ClimateOn = acknowledgedClimate ?? fresh.ClimateOn };
        state = fresh;
        lastUpdated = DateTimeOffset.Now.ToString("O");
    }
    private void Publish(string action = null) => Changed?.Invoke(Current with { Action = action });

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        generation++;
        lifetime.Cancel();
        lifetime.Dispose();
        Changed = null;
    }
}
