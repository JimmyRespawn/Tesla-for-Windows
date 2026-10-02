using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.SharpDX.Model;
using HelixToolkit.SharpDX.Model.Scene;
using HelixToolkit.WinUI.SharpDX;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace TeslaMurphy.Services
{
    internal sealed class Model3HelixScene : IDisposable
    {
        private readonly Viewport3DX viewport;
        private readonly DefaultEffectsManager effects;
        private readonly SceneNodeGroupModel3D root = new SceneNodeGroupModel3D();
        private readonly PerspectiveCamera camera = new PerspectiveCamera
        {
            NearPlaneDistance = .05,
            FarPlaneDistance = 1000,
            FieldOfView = 38,
            UpDirection = Vector3.UnitY
        };
        private readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private GroupNode studio;
        private SceneNode car, port, mirrorLeft, mirrorRight, socket, led;
        private Matrix4x4 portBase, mirrorLeftBase, mirrorRightBase;
        private MemoryStream environment;
        private List<(MeshNode Source, MeshNode Reflection)> reflections;
        private PBRMaterialCore portLight;
        private Model3DrivingLights drivingLights;
        private readonly Model3DriveDynamics driveDynamics = new();
        private readonly List<PBRMaterialCore> headlights = new List<PBRMaterialCore>();
        private bool disposed, loaded, locked, portOpen, manualView;
        private float portAngle, mirrorAngle;
        private double lastTick, flashUntil, topInset, bottomInset;
        private readonly Model3HeadlightFlash headlightFlash = new();
        private string activeView = "front";
        private CameraMotion motion;
        private bool clampingCamera, clampQueued;
        private bool movingQuality;
        private double cameraChangedAt;
        private bool showroom;
        private float showroomAngle;
        private Model3DrivingScene drivingScene;
        private Model3WindowAnimation windows;
        private Model3DoorAnimation doors;
        private Model3TrunkAnimation trunk, frunk;
        private bool? lastTrunkState, lastFrunkState;
        private Model3WindowStates lastWindowState;
        private bool lightTheme;
        public void SetLightTheme(bool light)
        {
            if (disposed) return;
            lightTheme = light;
            viewport.BackgroundColor = light ? Windows.UI.Color.FromArgb(255,241,243,247) : Windows.UI.Color.FromArgb(255,16,21,30);
            
            drivingScene?.SetLightTheme(light);
            ApplyDriveAtmosphere();
            viewport.InvalidateRender();
        }
        private readonly Model3DriveMotion driveMotion = new();
        public bool IsDriving { get; private set; }
        public bool DriveRunning => IsDriving && driveMotion.Running;
        public double DriveSpeed => driveMotion.Speed;
        public int DriveSignal => driveDynamics.Signal;
        public int DriveLane => driveDynamics.Lane;
        public bool DriveChangingLane => driveDynamics.ChangingLane;
        public bool DriveBraking => driveMotion.BrakeHeld;
        public void SetDriveEnvironment(Model3RoadKind road, Model3WeatherKind weather)
        {
            if (disposed || !loaded) return;
            drivingScene.SetEnvironment(road, weather);
            drivingScene.Apply(driveMotion.WheelAngle, driveMotion.RoadOffset, driveMotion.Distance, IsDriving ? driveDynamics : null, clock.Elapsed.TotalSeconds);
            ApplyDriveAtmosphere();
            ApplyTransforms();
            RefreshFraming();
            ClampCamera();
            WakeAnimation(); ViewChanged?.Invoke();
        }

        private void ApplyDriveAtmosphere()
        {
            if (studio == null) return;
            var background = Model3HelixSceneAssets.ApplyDrivingAtmosphere(studio, lightTheme, IsDriving, drivingScene.IsWet, drivingScene.RoadKind == Model3RoadKind.Tunnel, reflections);
            viewport.BackgroundColor = Windows.UI.Color.FromArgb(255, (byte)Math.Round(background.Red*255), (byte)Math.Round(background.Green*255), (byte)Math.Round(background.Blue*255));
        }

        public bool CanChangeDriveLane(int direction) => IsDriving && driveDynamics.CanChangeLane(direction, DriveSpeed, DriveRunning, DriveBraking);
        public void ToggleDriveSignal(int direction)
        {
            if (!IsDriving || disposed) return;
            driveDynamics.ToggleSignal(direction); WakeAnimation(); ViewChanged?.Invoke();
        }
        public void ToggleDriveBrake()
        {
            if (!IsDriving || disposed) return;
            driveMotion.BrakeHeld = !driveMotion.BrakeHeld; WakeAnimation(); ViewChanged?.Invoke();
        }
        public bool ChangeDriveLane(int direction)
        {
            if (!CanChangeDriveLane(direction) || !drivingScene.LaneClear(driveDynamics.Lane + direction, driveMotion.Distance)) return false;
            if (!driveDynamics.ChangeLane(direction, DriveSpeed, DriveRunning, DriveBraking)) return false;
            WakeAnimation(); ViewChanged?.Invoke(); return true;
        }
        public void SetDriveSpeed(double speed)
        {
            driveMotion.RequestedSpeed = Math.Clamp(speed, 0, 120);
            if (IsDriving) WakeAnimation();
        }
        private static readonly Vector3 InteriorPosition = new Vector3(.05f, 1.06f, .36f);
        private static readonly Vector3 InteriorTarget = new Vector3(-1.45f, .94f, .28f);
        public bool IsShowroom => showroom;
        public bool IsInterior => activeView == "interior";
        private long positionCallback, lookCallback;
        private static readonly Vector3 FrontPosition = new Vector3(-5.25f, 2.13f, 4.87f);
        private static readonly Vector3 FrontTarget = new Vector3(0, .55f, 0);
        private static readonly Vector3 PortPosition = new Vector3(4.15f, 1.77f, 3.25f);
        private static readonly Vector3 PortTarget = new Vector3(1.5f, .84f, .6f);
        public event Action ViewChanged;

        public bool TryProjectHotspot(string id, out Vector2 screen)
        {
            screen = default;
            if (!loaded || disposed || IsInterior || IsDriving || viewport.ActualWidth <= 0 || viewport.ActualHeight <= 0) return false;
            var point = id switch
            {
                "port" => new Vector3(1.94f, .98f, .99f),
                "frunk" => new Vector3(-1.87f, .96f, .25f),
                "trunk" => new Vector3(2.10f, 1.10f, .25f),
                "doors" => new Vector3(-.35f, .95f, 1.02f),
                _ => new Vector3(1.35f, .38f, 1.04f)
            };
            // Show the visible side's door/wheel marker; the port is left-side only.
            if (id != "port" && camera.Position.Z < 0) point.Z = -point.Z;
            if (id == "frunk" ? camera.Position.X > -1.3f : id == "trunk" ? camera.Position.X < 1.5f : (camera.Position.Z - point.Z) * Math.Sign(point.Z) <= 0) return false;
            var clip = Vector4.Transform(new Vector4(point, 1), camera.CreateViewMatrix() *
                camera.CreateProjectionMatrix(viewport.ActualWidth / viewport.ActualHeight));
            if (clip.W <= 0 || !float.IsFinite(clip.W)) return false;
            var ndc = new Vector3(clip.X, clip.Y, clip.Z) / clip.W;
            if (ndc.Z < 0 || ndc.Z > 1 || Math.Abs(ndc.X) > 1 || Math.Abs(ndc.Y) > 1) return false;
            screen = new Vector2((float)((ndc.X + 1) * viewport.ActualWidth / 2),
                (float)((1 - ndc.Y) * viewport.ActualHeight / 2));
            return true;
        }

        public Model3HelixScene(Viewport3DX viewport)
        {
            this.viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
            effects = new DefaultEffectsManager();
            viewport.EffectsManager = effects;
            viewport.Camera = camera;
            viewport.BackgroundColor = Windows.UI.Color.FromArgb(255, 16, 21, 30);
            viewport.ModelUpDirection = Vector3.UnitY;
            viewport.CameraRotationMode = CameraRotationMode.Turntable;
            viewport.InputController = new GarageInputController();
            viewport.ShowViewCube = false;
            viewport.ShowCoordinateSystem = false;
            viewport.ZoomExtentsWhenLoaded = false;
            viewport.ZoomDistanceLimitNear = 2.2;
            viewport.ZoomDistanceLimitFar = 16;
            viewport.IsShadowMappingEnabled = true;
            viewport.EnableSSAO = true;
            viewport.SSAOIntensity = .65;
            viewport.MSAA = MSAALevel.Eight;
            // The v3.1.2 renderer resolves MSAA before its FXAA pass, which
            // also smooths the single-sampled glass/depth-peeling edges.
            viewport.FXAALevel = FXAALevel.Medium;
            viewport.Items.Add(root);
            viewport.SizeChanged += OnSizeChanged;
            viewport.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnPointerPressed), true);
            timer.Tick += OnTick;
            SetCameraImmediately("front");
            positionCallback = camera.RegisterPropertyChangedCallback(ProjectionCamera.PositionProperty, OnCameraPositionChanged);
            lookCallback = camera.RegisterPropertyChangedCallback(ProjectionCamera.LookDirectionProperty, OnCameraLookChanged);
        }

        public async Task LoadAsync(string path, CancellationToken token)
        {
            if (disposed) throw new ObjectDisposedException(nameof(Model3HelixScene));
            if (loaded) return;
            if (!File.Exists(path)) throw new FileNotFoundException(Model3Text.Get("找不到 Model 3 模型"), path);
            var imported = await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                var result = Model3HelixSceneAssets.ImportModel(path);
                if (token.IsCancellationRequested)
                {
                    result.Root.Dispose();
                    token.ThrowIfCancellationRequested();
                }
                return result;
            }, token);
            if (disposed || token.IsCancellationRequested)
            {
                imported.Root.Dispose();
                token.ThrowIfCancellationRequested();
                throw new ObjectDisposedException(nameof(Model3HelixScene));
            }
            try
            {
                car = imported.Root;
                doors = new Model3DoorAnimation(car);
                trunk = new Model3TrunkAnimation(car);
                frunk = new Model3TrunkAnimation(car, front: true);
                windows = new Model3WindowAnimation(car);
                drivingScene = new Model3DrivingScene(car,Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path),"..","Traffic")));
                drivingLights = new Model3DrivingLights(car);
                var nodes = Model3HelixSceneAssets.Traverse(car).ToArray();
                port = nodes.Single(node => node.Name == "Charge_Port_Hinge");
                mirrorLeft = nodes.Single(node => node.Name == "Mirror_Left_Hinge");
                mirrorRight = nodes.Single(node => node.Name == "Mirror_Right_Hinge");
                socket = nodes.FirstOrDefault(node => node.Name == "Charge_Socket_Assembly");
                led = nodes.FirstOrDefault(node => node.Name == "Charge_Port_LED");
                portBase = port.ModelMatrix;
                mirrorLeftBase = mirrorLeft.ModelMatrix;
                mirrorRightBase = mirrorRight.ModelMatrix;
                foreach (var mesh in nodes.OfType<MeshNode>())
                {
                    if (mesh.Material is not PBRMaterialCore material) continue;
                    if (mesh.Name.StartsWith("Headlight_DRL", StringComparison.Ordinal) && !headlights.Contains(material)) headlights.Add(material);
                    if (material.Name == "Charge_Port_LED") portLight = material;
                }
                studio = Model3HelixSceneAssets.CreateStudio(car, out reflections, out environment);
                studio.AddChildNode(drivingScene.Road);
                SetLightTheme(lightTheme);
                root.AddNode(studio);
                loaded = true;
                ApplyTransforms();
                SetCameraImmediately(activeView);
                WakeAnimation();
            }
            catch
            {
                if (studio != null) studio.Dispose(); else imported.Root.Dispose();
                environment?.Dispose();
                throw;
            }
        }

        // Insets are the measured header/footer heights in the same native
        // viewport. Only the car presets are fitted, never the huge studio floor.
        public void SetVisibleArea(double top, double bottom)
        {
            if (disposed) return;
            topInset = Math.Max(0, top);
            bottomInset = Math.Max(0, bottom);
            UpdateFieldOfView();
            RefreshFraming();
        }

        public void Update(bool? isLocked, bool? isPortOpen, string action = null)
        {
            if (disposed) return;
            if (isLocked.HasValue) locked = isLocked.Value;
            if (isPortOpen.HasValue) portOpen = isPortOpen.Value;
            if (action == "lock" && isLocked == true) flashUntil = clock.Elapsed.TotalSeconds + .85;
            if (action == "unlock" && isLocked == false) flashUntil = clock.Elapsed.TotalSeconds + 1.1;
            if (action == "flashLights") headlightFlash.Start(clock.Elapsed.TotalSeconds);
            if (action == "openPort" && isPortOpen == true) ViewPort();
            if (loaded) WakeAnimation();
        }

        public void ViewPort() => AnimateCamera("port");

        public bool IsWindowLowered(string id) => windows?.IsLowered(id) == true;
        public bool IsDoorOpen(string id) => doors?.IsOpen(id) == true;
        public void ToggleDoor(string id)
        {
            if (disposed || !loaded) return;
            doors.SetOpen(id, !doors.IsOpen(id));
            WakeAnimation();
        }
        public void SetAllDoorsOpen(bool open)
        {
            if (disposed || !loaded) return;
            doors.SetAllOpen(open);
            WakeAnimation();
        }
        public bool IsFrunkFullyOpen => frunk?.IsFullyOpen == true;
        public void ToggleFrunkDemo()
        {
            if (disposed || !loaded) return;
            frunk.SetOpen(!frunk.IsFullyOpen);
            WakeAnimation();
        }
        public void UpdateFrunk(bool? open, string action = null, bool demo = false)
        {
            if (disposed || !loaded) return;
            if (open.HasValue && (open != lastFrunkState || action == "openFrunk"))
            {
                // Binary ft is not a measured lid angle. A real command releases
                // the latch; the user still lifts the bonnet by hand.
                frunk.SetOpening(open.Value ? demo ? 1 : .06f : 0);
                WakeAnimation();
            }
            lastFrunkState = open;
        }
        public void ResetFrunk()
        {
            if (disposed || !loaded) return;
            lastFrunkState = null;
            frunk.SetOpen(false);
            WakeAnimation();
        }
        public bool IsTrunkOpen => trunk?.IsOpen == true;
        public void ToggleTrunkDemo()
        {
            if (disposed || !loaded) return;
            trunk.SetOpen(!trunk.IsOpen);
            WakeAnimation();
        }
        public void UpdateTrunk(bool? open, string action = null)
        {
            if (disposed || !loaded) return;
            if (open.HasValue && (open != lastTrunkState || action == "openTrunk"))
            {
                trunk.SetOpen(open.Value);
                WakeAnimation();
            }
            lastTrunkState = open;
        }
        public void ResetTrunk()
        {
            if (disposed || !loaded) return;
            lastTrunkState = null;
            trunk.SetOpen(false);
            WakeAnimation();
        }
        public void UpdateWindows(Model3WindowStates state, string action = null)
        {
            if (disposed || !loaded) return;
            if (action is "ventWindows" or "closeWindows")
                windows.SetAllVented(action == "ventWindows");
            else if (state != lastWindowState && state != null)
            {
                if (state.FrontDriver.HasValue) windows.SetOpenFlag("lf", state.FrontDriver.Value);
                if (state.FrontPassenger.HasValue) windows.SetOpenFlag("rf", state.FrontPassenger.Value);
                if (state.RearDriver.HasValue) windows.SetOpenFlag("lr", state.RearDriver.Value);
                if (state.RearPassenger.HasValue) windows.SetOpenFlag("rr", state.RearPassenger.Value);
            }
            else return;
            lastWindowState = state;
            WakeAnimation();
        }
        public void ResetWindows()
        {
            if (disposed || !loaded) return;
            lastWindowState = null;
            windows.SetAllLowered(false);
            WakeAnimation();
        }
        public void ToggleWindow(string id)
        {
            if (disposed || !loaded) return;
            windows.SetLowered(id, !windows.IsLowered(id));
            WakeAnimation();
        }
        public void SetAllWindowsLowered(bool lowered)
        {
            if (disposed || !loaded) return;
            windows.SetAllLowered(lowered);
            WakeAnimation();
        }
        public void ResetView() => AnimateCamera(IsDriving ? "drive" : "front");
        public void ToggleShowroom()
        {
            if (showroom) { showroom = false; ViewChanged?.Invoke(); return; }
            StopDriving();
            AnimateCamera("front");
            showroomAngle = 0;
            showroom = true;
            manualView = true;
            WakeAnimation();
            ViewChanged?.Invoke();
        }
        public void ViewInterior()
        {
            if (disposed || !loaded) return;
            showroom = false;
            motion = null;
            SetCameraImmediately("interior");
            ViewChanged?.Invoke();
        }

        public void ToggleDriving()
        {
            if (disposed || !loaded) return;
            if (!IsDriving)
            {
                showroom = false;
                if (!IsInterior) AnimateCamera("drive");
                IsDriving = true;
                ApplyDriveAtmosphere();
                viewport.EnableSSAO = false;
                drivingScene.Road.Visible = true;
                driveMotion.Running = true;
                ApplyTransforms();
            }
            else driveMotion.Running = !driveMotion.Running;
            WakeAnimation();
            ViewChanged?.Invoke();
        }

        public void StopDriving()
        {
            if (!IsDriving) return;
            IsDriving = false;
            ApplyDriveAtmosphere();
            if (!movingQuality) viewport.EnableSSAO = true;
            driveMotion.Reset();
            driveDynamics.Reset();
            drivingLights.Apply(null, false);
            drivingScene.Road.Visible = false;
            drivingScene.Apply(0, 0);
            if (IsInterior) RefreshFraming();
            ApplyTransforms();
            if (activeView == "drive" && !manualView) AnimateCamera("front");
            WakeAnimation();
            ViewChanged?.Invoke();
        }

        private void ConfigureCamera(string view)
        {
            viewport.CameraMode = view == "interior" ? CameraMode.FixedPosition : CameraMode.Inspect;
            viewport.IsPanEnabled = viewport.IsZoomEnabled = view != "interior";
            camera.NearPlaneDistance = view == "interior" ? .015 : .05;
        }

        private void AnimateCamera(string view)
        {
            if (disposed) return;
            showroom = false;
            if (IsInterior) { SetCameraImmediately(view); ViewChanged?.Invoke(); return; }
            ConfigureCamera(view);
            activeView = view;
            manualView = false;
            ConfigureCamera(view);
            UpdateFieldOfView();
            FramedPreset(view, out var position, out var target);
            motion = new CameraMotion
            {
                Start = clock.Elapsed.TotalSeconds,
                FromPosition = camera.Position,
                FromTarget = camera.Position + camera.LookDirection,
                ToPosition = position,
                ToTarget = target
            };
            WakeAnimation();
        }

        private void SetCameraImmediately(string view)
        {
            activeView = view;
            manualView = false;
            ConfigureCamera(view);
            UpdateFieldOfView();
            FramedPreset(view, out var position, out var target);
            camera.Position = position;
            camera.LookDirection = target - camera.Position;
            camera.UpDirection = Vector3.UnitY;
            motion = null;
        }

        private void FramedPreset(string view, out Vector3 position, out Vector3 target)
        {
            if (view == "interior")
            {
                var pose = IsDriving ? driveDynamics.BodyPose * driveDynamics.TravelPose : Matrix4x4.Identity;
                position = Vector3.Transform(InteriorPosition, pose); target = Vector3.Transform(InteriorTarget, pose); return;
            }
            position = view == "drive" ? new Vector3(7.8f, 3.1f, IsDriving && drivingScene.RoadKind == Model3RoadKind.Tunnel ? 2.8f : 5.6f) : view == "port" ? PortPosition : FrontPosition;
            target = view == "drive" ? new Vector3(-2.5f, .7f, 0) : view == "port" ? PortTarget : FrontTarget;
            if (IsDriving && view == "drive") { position.Z += driveDynamics.LateralOffset; target.Z += driveDynamics.LateralOffset; }
            var forward = Vector3.Normalize(target - position);
            var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
            var up = Vector3.Normalize(Vector3.Cross(right, forward));
            var halfHeight = Vector3.Distance(position, target) * Math.Tan(camera.FieldOfView * Math.PI / 360);
            var offset = (float)((topInset - bottomInset) / Math.Max(1, viewport.ActualHeight) * halfHeight);
            position += up * offset;
            target += up * offset;
        }

        private void RefreshFraming()
        {
            if (manualView && !IsInterior) return;
            FramedPreset(activeView, out var position, out var target);
            if (motion != null)
            {
                motion.ToPosition = position;
                motion.ToTarget = target;
            }
            else
            {
                camera.Position = position;
                if (!manualView || !IsInterior) camera.LookDirection = target - position;
                camera.UpDirection = IsInterior && IsDriving ? Vector3.TransformNormal(Vector3.UnitY, driveDynamics.BodyPose * driveDynamics.TravelPose) : Vector3.UnitY;
            }
        }

        private void UpdateFieldOfView()
        {
            if (IsInterior)
            {
                camera.FieldOfView = 68;
                viewport.InvalidateRender();
                ViewChanged?.Invoke();
                return;
            }
            var height = Math.Max(1, viewport.ActualHeight);
            var width = Math.Max(1, viewport.ActualWidth);
            var usable = Math.Max(height * .22, height - topInset - bottomInset);
            var verticalFit = Math.Max(1, .64 * height / usable);
            var horizontalFit = Math.Max(1, 1.40 / (width / height));
            var multiplier = Math.Max(verticalFit, horizontalFit);
            var baseFov = activeView == "port" ? 34 : 38;
            camera.FieldOfView = Math.Clamp(2 * Math.Atan(Math.Tan(baseFov * Math.PI / 360) * multiplier) * 180 / Math.PI, baseFov, 94);
            viewport.InvalidateRender();
            ViewChanged?.Invoke();
        }

        private void WakeAnimation()
        {
            lastTick = clock.Elapsed.TotalSeconds;
            if (!timer.IsEnabled) timer.Start();
        }

        private void OnTick(object sender, object args)
        {
            if (disposed) return;
            var now = clock.Elapsed.TotalSeconds;
            var dt = Math.Clamp(now - lastTick, .001, .1);
            lastTick = now;
            var blend = (float)(1 - Math.Exp(-8 * dt));
            var targetPort = portOpen ? -1.92f : 0;
            var targetMirror = locked ? Model3HelixSceneAssets.MirrorFoldAngle : 0;
            var changing = Math.Abs(portAngle - targetPort) > .0005 || Math.Abs(mirrorAngle - targetMirror) > .0005;
            var windowsChanging = loaded && windows.Step(dt);
            var doorsChanging = loaded && doors.Step(dt);
            var trunkChanging = loaded && trunk.Step(dt);
            var frunkChanging = loaded && frunk.Step(dt);
            bool drivingChanged = IsDriving && ((driveMotion.Running && driveMotion.RequestedSpeed > 0) || driveMotion.Speed > 0 || driveDynamics.IsAnimating || driveMotion.BrakeHeld || drivingScene.WeatherAnimating);
            if (drivingChanged)
            {
                driveMotion.Step(dt);
                driveDynamics.Step(dt, driveMotion.Speed, driveMotion.Acceleration, driveMotion.BrakeHeld);
                drivingScene.Apply(driveMotion.WheelAngle, driveMotion.RoadOffset, driveMotion.Distance, driveDynamics, now);
                RefreshFraming();
                ViewChanged?.Invoke();
            }
            if (loaded)
            {
                portAngle += (targetPort - portAngle) * blend;
                mirrorAngle += (targetMirror - mirrorAngle) * blend;
                if (!changing) { portAngle = targetPort; mirrorAngle = targetMirror; }
                if (changing || windowsChanging || doorsChanging || trunkChanging || frunkChanging || drivingChanged) ApplyTransforms();
                if (portLight != null) portLight.EmissiveColor = portOpen
                    ? new Color4(.04f, .35f, .90f, 1)
                    : new Color4(0, 0, 0, 1);
                var flashing = now < flashUntil && (int)((flashUntil - now) / .18) % 2 == 0;
                drivingLights.Apply(IsDriving ? driveDynamics : null, flashing);
                Model3HelixSceneAssets.ApplyHeadlightEmission(headlights, locked, headlightFlash.IsOn(now));
            }
            if (motion != null)
            {
                var amount = (float)Math.Clamp((now - motion.Start) / .85, 0, 1);
                amount = amount * amount * (3 - 2 * amount);
                camera.Position = Vector3.Lerp(motion.FromPosition, motion.ToPosition, amount);
                var target = Vector3.Lerp(motion.FromTarget, motion.ToTarget, amount);
                camera.LookDirection = target - camera.Position;
                camera.UpDirection = Vector3.UnitY;
                if (amount >= 1) motion = null;
            }
            else if (showroom)
            {
                showroomAngle = (showroomAngle + (float)dt * .14f) % (MathF.PI * 2);
                FramedPreset("front", out var position, out var target);
                camera.Position = target + Vector3.Transform(position - target, Matrix4x4.CreateRotationY(showroomAngle));
                camera.LookDirection = target - camera.Position;
            }
            viewport.InvalidateRender();
            if (movingQuality && motion == null && now - cameraChangedAt > .20)
            {
                movingQuality = false;
                viewport.EnableSSAO = !IsDriving;
            }
            if (!changing && !windowsChanging && !doorsChanging && !trunkChanging && !frunkChanging && !drivingChanged && motion == null && !showroom && now >= flashUntil && !headlightFlash.IsActive(now) && !movingQuality) timer.Stop();
        }

        private void ApplyTransforms()
        {
            // System.Numerics uses row vectors: local hinge rotation precedes
            // its original translation, so the pivot never moves during rotation.
            port.ModelMatrix = Matrix4x4.CreateRotationX(portAngle) * portBase;
            mirrorLeft.ModelMatrix = Matrix4x4.CreateRotationY(mirrorAngle) * mirrorLeftBase;
            mirrorRight.ModelMatrix = Matrix4x4.CreateRotationY(-mirrorAngle) * mirrorRightBase;
            if (socket != null) socket.Visible = portOpen;
            if (led != null) led.Visible = portOpen;
            car.UpdateAllTransformMatrix();
            foreach (var pair in reflections)
            {
                pair.Reflection.ModelMatrix = pair.Source.TotalModelMatrix * Model3HelixSceneAssets.FloorMirror;
                var visible = true;
                for (var node = (SceneNode)pair.Source; node != null && node != studio; node = node.Parent)
                    visible &= node.Visible;
                pair.Reflection.Visible = visible && (!IsDriving || drivingScene.IsWet);
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs args)
        {
            UpdateFieldOfView();
            RefreshFraming();
        }
        private void OnPointerPressed(object sender, PointerRoutedEventArgs args)
        {
            manualView = true;
            motion = null;
            showroom = false;
            ViewChanged?.Invoke();
        }

        private void OnCameraLookChanged(DependencyObject sender, DependencyProperty property)
        {
            CameraMoved();
            ClampCamera();
        }
        private void CameraMoved()
        {
            if (disposed || !loaded) return;
            cameraChangedAt = clock.Elapsed.TotalSeconds;
            ViewChanged?.Invoke();
            if (!movingQuality)
            {
                movingQuality = true;
                viewport.EnableSSAO = false;
            }
            if (!timer.IsEnabled) WakeAnimation();
        }
        private void OnCameraPositionChanged(DependencyObject sender, DependencyProperty property)
        {
            CameraMoved();
            if (disposed || clampingCamera || clampQueued) return;
            // Native gestures update position and look as a pair. Check a
            // position-only pan/zoom after that pair has finished updating.
            clampQueued = true;
            viewport.DispatcherQueue.TryEnqueue(() => { clampQueued = false; ClampCamera(); });
        }
        private void ClampCamera()
        {
            if (disposed || clampingCamera) return;
            var position = camera.Position;
            var look = camera.LookDirection;
            if (IsInterior)
            {
                // Fixed-position rotation keeps the viewer in the driver's seat.
                FramedPreset("interior", out var seatPosition, out _);
                if (position == seatPosition) return;
                clampingCamera = true;
                try { camera.Position = seatPosition; }
                finally { clampingCamera = false; }
                return;
            }
            bool changed = Model3CameraBounds.Clamp(ref position, ref look);
            if (IsDriving && drivingScene.RoadKind == Model3RoadKind.Tunnel)
            {
                var bounded = new Vector3(position.X, Math.Min(position.Y, 7.5f), Math.Clamp(position.Z, -7.7f, 7.7f));
                if (bounded != position)
                {
                    look += position - bounded;
                    position = bounded;
                    changed = true;
                }
            }
            if (!changed) return;
            clampingCamera = true;
            try { camera.Position = position; camera.LookDirection = look; camera.UpDirection = Vector3.UnitY; }
            finally { clampingCamera = false; }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            ViewChanged = null;
            camera.UnregisterPropertyChangedCallback(ProjectionCamera.PositionProperty, positionCallback);
            camera.UnregisterPropertyChangedCallback(ProjectionCamera.LookDirectionProperty, lookCallback);
            timer.Stop();
            timer.Tick -= OnTick;
            viewport.SizeChanged -= OnSizeChanged;
            viewport.RemoveHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnPointerPressed));
            viewport.Items.Remove(root);
            root.Clear(true);
            environment?.Dispose();
            viewport.EffectsManager = null;
            effects.Dispose();
            reflections?.Clear();
            drivingLights = null;
            headlights.Clear();
            headlightFlash.Reset();
        }

        private sealed class CameraMotion
        {
            public double Start;
            public Vector3 FromPosition, FromTarget, ToPosition, ToTarget;
        }

        private sealed class GarageInputController : InputController
        {
            protected override bool IsStartRotate(Microsoft.UI.Input.PointerPointProperties properties)
                => properties.IsLeftButtonPressed;
            protected override bool IsStartPan(Microsoft.UI.Input.PointerPointProperties properties)
                => properties.IsRightButtonPressed;
        }
    }
}
