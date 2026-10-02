using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TeslaMurphy.Models;
using TeslaMurphy.ViewModels;

namespace TeslaMurphy.Services
{
    internal sealed class Model3VehicleApi : IModel3VehicleApi
    {
        public async Task<Model3VehicleState> ReadAsync(Model3VehicleContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateContext(context);
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                cts.CancelAfter(TimeSpan.FromSeconds(25));
                string json;
                try
                {
                    // Match the homepage's complete snapshot request. A partial
                    // response must not make climate readings look unsupported.
                    string endpoint = "/api/1/vehicles/" + Uri.EscapeDataString(context.Vin) + "/vehicle_data";
                    json = await TeslaFleetServices.HttpGetRequestAsync(context.BaseUrl, endpoint,
                        AppSettings.Instance.Access_token, cts);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (OperationCanceledException)
                {
                    throw new InvalidOperationException(Model3Text.Get("读取车辆状态超时，请确认车辆在线后重试。"));
                }
                catch (Exception)
                {
                    throw new InvalidOperationException(Model3Text.Get("无法读取车辆状态，请检查网络连接后刷新。"));
                }
                cancellationToken.ThrowIfCancellationRequested();
                if (cts.IsCancellationRequested)
                    throw new InvalidOperationException(Model3Text.Get("读取车辆状态超时，请确认车辆在线后重试。"));
                ValidateContext(context);
                return ParseVehicleState(json, context.Vin);
            }
        }

        public async Task<Model3CommandResult> SendAsync(Model3VehicleContext context, string action, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string contextError = GetContextError(context);
            if (contextError != null) return new Model3CommandResult(false, contextError);
            if (action is not ("lock" or "unlock" or "openPort" or "closePort" or "ventWindows" or "closeWindows" or "openTrunk" or "openFrunk" or "flashLights" or "climateOn" or "climateOff" or "remoteStart"))
                return new Model3CommandResult(false, Model3Text.Get("不支持此车辆操作。"));

            var settings = AppSettings.Instance;
            string access = settings.Access_token;
            string refresh = settings.Refresh_token;
            string clientSecret = settings.Client_secret;
            var viewModel = new CarPageViewModel();
            try
            {
                bool succeeded = action switch
                {
                    "lock" => await viewModel.SetDoorLockAsync(context.Vin, true, cancellationToken),
                    "unlock" => await viewModel.SetDoorLockAsync(context.Vin, false, cancellationToken),
                    "openPort" => await viewModel.SetChargePortAsync(context.Vin, true, cancellationToken),
                    "closePort" => await viewModel.SetChargePortAsync(context.Vin, false, cancellationToken),
                    "ventWindows" => await viewModel.SetWindowsAsync(context.Vin, true, cancellationToken),
                    "closeWindows" => await viewModel.SetWindowsAsync(context.Vin, false, cancellationToken),
                    "openTrunk" => await viewModel.OpenRearTrunkAsync(context.Vin, cancellationToken),
                    "openFrunk" => await viewModel.OpenFrunkAsync(context.Vin, cancellationToken),
                    "flashLights" => await viewModel.FlashLightsAsync(context.Vin, cancellationToken),
                    "climateOn" => await viewModel.SetClimateAsync(context.Vin, true, cancellationToken),
                    "climateOff" => await viewModel.SetClimateAsync(context.Vin, false, cancellationToken),
                    "remoteStart" => await viewModel.RemoteStartDriveAsync(context.Vin, cancellationToken),
                    _ => false
                };
                cancellationToken.ThrowIfCancellationRequested();
                if (GetContextError(context) != null)
                    return new Model3CommandResult(false, Model3Text.Get("操作期间车辆或登录状态已变化，请刷新查看结果。"));
                return succeeded
                    ? new Model3CommandResult(true)
                    : new Model3CommandResult(false, CommandError(viewModel.VehicleCommandError,
                        context.Vin, access, refresh, clientSecret));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (OperationCanceledException)
            {
                return new Model3CommandResult(false, Model3Text.Get("车辆指令超时，请刷新状态检查执行结果。"));
            }
            catch (Exception)
            {
                return new Model3CommandResult(false, Model3Text.Get("车辆指令未成功，请检查网络、命令代理与授权后重试。"));
            }
        }

        internal static Model3VehicleState ParseVehicleState(string json, string expectedVin)
        {
            string status = json?.Trim();
            switch (status)
            {
                case null:
                case "": throw new InvalidOperationException(Model3Text.Get("无法读取车辆状态，请检查网络连接后刷新。"));
                case "Unauthorized": throw new InvalidOperationException(Model3Text.Get("登录已过期，请重新登录。"));
                case "Forbidden": throw new InvalidOperationException(Model3Text.Get("没有读取车辆状态的权限，请检查 Tesla 授权。"));
                case "NotFound": throw new InvalidOperationException(Model3Text.Get("未找到当前车辆，请重新选择车辆后刷新。"));
                case "TooManyRequests": throw new InvalidOperationException(Model3Text.Get("请求过于频繁，请稍后再刷新。"));
                case "RequestTimeout": throw new InvalidOperationException(Model3Text.Get("车辆暂时不可用或处于休眠状态，请确认车辆在线后刷新。"));
                case "InternalServerError":
                case "ServiceUnavailable": throw new InvalidOperationException(Model3Text.Get("Tesla 服务暂时不可用，请稍后刷新。"));
            }
            JObject body;
            try { body = JObject.Parse(json); }
            catch (JsonException) { throw new InvalidOperationException(Model3Text.Get("车辆返回了无法识别的状态，请稍后刷新。")); }
            if (body["error"] != null && body["error"].Type != JTokenType.Null)
                throw new InvalidOperationException(Model3Text.Get("Tesla 未返回有效车辆状态，请检查授权或稍后刷新。"));
            if (body["response"] is not JObject response)
                throw new InvalidOperationException(Model3Text.Get("车辆状态不完整，请稍后刷新。"));

            string actualVin = StringValue(response["vin"]);
            if (string.IsNullOrWhiteSpace(expectedVin) || string.IsNullOrWhiteSpace(actualVin)
                || !string.Equals(actualVin, expectedVin, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(Model3Text.Get("车辆数据与当前选择不一致，请重新选择车辆后刷新。"));
            var vehicleState = response["vehicle_state"] as JObject;
            var chargeState = response["charge_state"] as JObject;
            var climateState = response["climate_state"] as JObject;
            bool? climateOn = BooleanValue(climateState?["is_climate_on"]);
            bool? locked = BooleanValue(vehicleState?["locked"]);
            bool? portOpen = BooleanValue(chargeState?["charge_port_door_open"]);
            if (locked == null && portOpen == null && climateOn == null)
                throw new InvalidOperationException(Model3Text.Get("车辆尚未返回锁车或充电口状态，请确认车辆在线后刷新。"));
            string vehicleName = StringValue(vehicleState?["vehicle_name"]);
            if (vehicleName?.Length > 200) vehicleName = vehicleName.Substring(0, 200);
            return new Model3VehicleState(locked, portOpen, StringValue(chargeState?["charging_state"]), vehicleName,
                new Model3TirePressures(PressureValue(vehicleState?["tpms_pressure_fl"]), PressureValue(vehicleState?["tpms_pressure_fr"]),
                    PressureValue(vehicleState?["tpms_pressure_rl"]), PressureValue(vehicleState?["tpms_pressure_rr"])),
                new Model3WindowStates(WindowOpenValue(vehicleState?["fd_window"]), WindowOpenValue(vehicleState?["fp_window"]),
                    WindowOpenValue(vehicleState?["rd_window"]), WindowOpenValue(vehicleState?["rp_window"])), WindowOpenValue(vehicleState?["rt"]), WindowOpenValue(vehicleState?["ft"]),
                climateOn, TemperatureValue(climateState?["inside_temp"]), TemperatureValue(climateState?["outside_temp"]));
        }

        private static bool? WindowOpenValue(JToken token)
        {
            // vehicle_data uses binary window flags, not Fleet Telemetry's
            // separate WindowState enum. Missing or malformed is unknown.
            if (token?.Type != JTokenType.Integer) return null;
            return token.Value<long>() switch { 0 => false, 1 => true, _ => null };
        }

        private static double? TemperatureValue(JToken token)
        {
            double value;
            if (token?.Type is JTokenType.Integer or JTokenType.Float) value = token.Value<double>();
            else if (token?.Type == JTokenType.String && double.TryParse(token.Value<string>(),
                NumberStyles.Float, CultureInfo.InvariantCulture, out value)) { }
            else return null;
            return double.IsFinite(value) ? value : null;
        }

        private static double? PressureValue(JToken token)
        {
            if (token?.Type is not (JTokenType.Integer or JTokenType.Float)) return null;
            double value = token.Value<double>();
            return double.IsFinite(value) && value > 0 ? value : null;
        }

        private static bool? BooleanValue(JToken token)
            => token?.Type == JTokenType.Boolean ? (bool?)token.Value<bool>() : null;

        private static string StringValue(JToken token)
            => token?.Type == JTokenType.String ? token.Value<string>() : null;

        private static void ValidateContext(Model3VehicleContext context)
        {
            string error = GetContextError(context);
            if (error != null) throw new InvalidOperationException(error);
        }

        private static string GetContextError(Model3VehicleContext context)
        {
            var settings = AppSettings.Instance;
            if (context == null || string.IsNullOrWhiteSpace(context.Vin)) return Model3Text.Get("请先在车辆主页选择要控制的车辆。");
            if (context.IsDemo || settings.IsTestMode) return Model3Text.Get("体验模式不会连接或控制真实车辆。");
            bool hasSession = !string.IsNullOrWhiteSpace(settings.Access_token);
            if (!context.HasSession || !hasSession) return Model3Text.Get("请先登录 Tesla 账户后连接车辆。");
            if (!string.Equals(context.Vin, settings.Current_carvin, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(context.BaseUrl)
                || !string.Equals(context.BaseUrl.TrimEnd('/'), settings.Base_URL?.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                return Model3Text.Get("当前车辆或区域已变化，请刷新后重试。");
            return null;
        }

        private static string CommandError(string error, string vin, params string[] requestSecrets)
        {
            if (string.IsNullOrWhiteSpace(error)) return Model3Text.Get("车辆未确认指令，请刷新状态检查执行结果。");
            string safe = error;
            var settings = AppSettings.Instance;
            foreach (string value in requestSecrets)
                if (!string.IsNullOrEmpty(value)) safe = safe.Replace(value, Model3Text.Get("[已隐藏]"));
            foreach (string value in new[] { settings.Access_token, settings.Refresh_token, settings.Client_secret, vin })
                if (!string.IsNullOrEmpty(value)) safe = safe.Replace(value, Model3Text.Get("[已隐藏]"));
            safe = Regex.Replace(safe, @"\b[A-HJ-NPR-Z0-9]{17}\b", Model3Text.Get("[车辆]"), RegexOptions.IgnoreCase);
            if (safe.Length > 500) safe = safe.Substring(0, 500);
            string lower = safe.ToLowerInvariant();
            if (lower.Contains("cable connected")) return Model3Text.Get("车辆拒绝关闭充电口：充电线仍连接，请拔下后重试。");
            if (lower.Contains("non-motorized charge port")) return Model3Text.Get("车辆充电口不支持电动关闭。");
            if (lower.Contains("already closed")) return Model3Text.Get("车辆报告充电口已经关闭，请刷新状态。");
            if (lower.Contains("signed") || lower.Contains("virtual key") || lower.Contains("vehicle command protocol"))
                return Model3Text.Get("车辆要求签名指令，请检查命令代理与虚拟钥匙配对。\n") + safe;
            if (lower.Contains("http 401") || lower.Contains("authorization expired")) return Model3Text.Get("登录已过期，请重新登录后重试。");
            if (lower.Contains("http 403")) return Model3Text.Get("车辆拒绝了指令，请检查授权与虚拟钥匙配对。");
            if (lower.Contains("unable to wake")) return Model3Text.Get("暂时无法唤醒车辆，请确认车辆在线后重试。");
            if (lower.Contains("timed out")) return Model3Text.Get("车辆指令超时，请刷新状态检查执行结果。");
            if (lower.Contains("canceled")) return Model3Text.Get("车辆操作已取消。");
            if (lower.Contains("unable to reach")) return Model3Text.Get("无法连接 Tesla 或命令代理，请检查网络与代理设置。");
            return Model3Text.Get("车辆未确认指令，请刷新状态检查执行结果。\n") + safe;
        }
    }
}
