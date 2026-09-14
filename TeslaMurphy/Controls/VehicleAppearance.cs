using System;
using System.Linq;
using TeslaMurphy.Models;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;

namespace TeslaMurphy.Controls
{
    // Side-view atlas: 2x2 cells, left-front=1 and left-rear=2. Door details remain available to screen readers.
    public sealed class VehicleAppearance : UserControl
    {
        public static readonly DependencyProperty VehicleProperty = DependencyProperty.Register(
            nameof(Vehicle), typeof(CarData), typeof(VehicleAppearance),
            new PropertyMetadata(null, OnVehicleChanged));
        public CarData Vehicle { get => (CarData)GetValue(VehicleProperty); set => SetValue(VehicleProperty, value); }
        private readonly Canvas groundShadow = CreateGroundShadow();
        private readonly Canvas previous = CreateFrame();
        private readonly Canvas current = CreateFrame();
        private string caption;
        private string stateLabel;
        private Storyboard transition;
        private Storyboard chargingPulse;
        private readonly Canvas chargingLayer = new Canvas { Width = 400, Height = 267, IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Top };
        private readonly TextBlock chargingLabel;
        private string lastVin, lastModel;
        private int lastMask = -1;

        public VehicleAppearance()
        {
            var scene = new Grid { Width = 400, Height = 305, Background = new SolidColorBrush(Colors.Transparent) };
            previous.VerticalAlignment = current.VerticalAlignment = VerticalAlignment.Top;
            scene.Children.Add(groundShadow);
            scene.Children.Add(previous);
            scene.Children.Add(current);
            scene.Children.Add(chargingLayer);
            var labels = new StackPanel { Margin = new Thickness(20, 0, 20, 14), VerticalAlignment = VerticalAlignment.Bottom };
            chargingLabel = new TextBlock { FontSize = 12, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 5, 0, 0), Visibility = Visibility.Collapsed };
            labels.Children.Add(chargingLabel); scene.Children.Add(labels);
            Content = new Viewbox { Stretch = Stretch.Uniform, Child = scene };
            Unloaded += (s, e) => { transition?.Stop(); chargingPulse?.Stop(); previous.Opacity = 0; current.Opacity = 1; };
        }

        private static Canvas CreateGroundShadow()
        {
            var layer = new Canvas { Width = 400, Height = 267,
                VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
            // Layered translucent ellipses create a soft contact shadow without an opaque floor.
            for (int i = 0; i < 20; i++)
            {
                double width = 340 - i * 4, height = 72 - i * 2;
                var ellipse = new Windows.UI.Xaml.Shapes.Ellipse
                {
                    Width = width, Height = height,
                    Fill = new SolidColorBrush(Color.FromArgb(5, 0, 0, 0)),
                    RenderTransform = new RotateTransform { Angle = -22, CenterX = width / 2, CenterY = height / 2 }
                };
                Canvas.SetLeft(ellipse, 202 - width / 2);
                Canvas.SetTop(ellipse, 189 - height / 2);
                layer.Children.Add(ellipse);
            }
            return layer;
        }

        private static Canvas CreateFrame()
        {
            var frame = new Canvas { Width = 400, Height = 267,
                Clip = new RectangleGeometry { Rect = new Rect(0, 0, 400, 267) } };
            frame.Children.Add(new Image { Width = 800, Height = 534, Stretch = Stretch.Fill, IsHitTestVisible = false });
            return frame;
        }
        private static void SetFrame(Canvas frame, string model, int mask)
        {
            int cell = mask & 3;
            var image = (Image)frame.Children[0];
            var uri = new Uri("ms-appx:///Assets/Images/Vehicles/" + model + "-side.png");
            if (!(image.Source is BitmapImage bitmap) || bitmap.UriSource != uri) image.Source = new BitmapImage(uri);
            Canvas.SetLeft(image, -(cell % 2) * 400);
            Canvas.SetTop(image, -(cell / 2) * 267);
        }
        private static void OnVehicleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((VehicleAppearance)d).Update();
        private void Update()
        {
            var car = Vehicle;
            string model = car?.vehicle_config?.car_type?.Trim().ToLowerInvariant();
            if (model == "cyber_truck" || model == "cyber truck") model = "cybertruck";
            bool supported = new[] { "model3", "modely", "models", "modelx", "cybertruck" }.Contains(model);
            groundShadow.Visibility = supported ? Visibility.Visible : Visibility.Collapsed;
            transition?.Stop();
            UpdateCharging(car?.charge_state, model);
            if (!supported)
            {
                previous.Opacity = current.Opacity = 0;
                chargingLayer.Visibility = chargingLabel.Visibility = Visibility.Collapsed;
                caption = car?.vehicle_state?.vehicle_name ?? "Vehicle";
                stateLabel = "Vehicle illustration unavailable";
                AutomationProperties.SetName(this, caption + ". " + stateLabel);
                lastMask = -1;
                return;
            }
            var doors = car.vehicle_state;
            bool known = doors?.df != null && doors.dr != null && doors.pf != null && doors.pr != null;
            bool rhd = car.vehicle_config.rhd;
            int? lf = rhd ? doors?.pf : doors?.df, lr = rhd ? doors?.pr : doors?.dr;
            int? rf = rhd ? doors?.df : doors?.pf, rr = rhd ? doors?.dr : doors?.pr;
            // The left side faces the camera. Keep far-side state for the status text.
            int mask = (lf > 0 ? 1 : 0) | (lr > 0 ? 2 : 0) | (rf > 0 ? 4 : 0) | (rr > 0 ? 8 : 0);
            caption = model == "model3" ? "Model 3" : model == "modely" ? "Model Y" : model == "models" ? "Model S" : model == "modelx" ? "Model X" : "Cybertruck";
            var open = new[] { lf > 0 ? "Front left" : null, lr > 0 ? "Rear left" : null, rf > 0 ? "Front right" : null, rr > 0 ? "Rear right" : null }.Where(x => x != null);
            stateLabel = !known ? "Door state unavailable" : mask == 0 ? "All doors closed" : string.Join(" · ", open) + " open";
            AutomationProperties.SetName(this, caption + ". " + stateLabel);
            bool animate = known && lastMask >= 0 && lastVin == car.vin && lastModel == model && (lastMask & 3) != (mask & 3)
                && new Windows.UI.ViewManagement.UISettings().AnimationsEnabled;
            SetFrame(current, model, mask);
            current.Opacity = known ? 1 : 0.45;
            previous.Opacity = 0;
            if (animate)
            {
                SetFrame(previous, model, lastMask); previous.Opacity = 1; current.Opacity = 0;
                transition = new Storyboard();
                AddFade(current, 0, 1); AddFade(previous, 1, 0);
                transition.Begin();
            }
            lastVin = car.vin; lastModel = model; lastMask = known ? mask : -1;
        }
        private void UpdateCharging(ChargeStateData charge, string model)
        {
            chargingPulse?.Stop();
            chargingLayer.Children.Clear();
            string state = charge?.charging_state;
            bool connected = state == "Charging" || state == "Complete" || state == "Stopped" || state == "Starting" || state == "NoPower";
            chargingLayer.Visibility = connected ? Visibility.Visible : Visibility.Collapsed;
            chargingLabel.Visibility = connected ? Visibility.Visible : Visibility.Collapsed;
            if (!connected) return;
            bool active = state == "Charging";
            double portX = model == "cybertruck" ? 369 : model == "modely" ? 375 : model == "modelx" ? 381 : model == "models" ? 379 : 366;
            double portY = model == "cybertruck" ? 109 : model == "model3" ? 67 : model == "modely" ? 64 : model == "models" ? 64 : 69;
            var accent = new SolidColorBrush(active ? Color.FromArgb(255, 40, 190, 126) : Color.FromArgb(255, 121, 142, 164));
            chargingLabel.Text = active ? "Charging" + (charge.charger_power > 0 ? " · " + charge.charger_power + " kW" : "")
                : state == "Complete" ? "Plugged in · Charge complete" : state == "NoPower" ? "Plugged in · No power" : "Plugged in · Waiting";
            // A schematic charger and cable: connector is at vehicle left rear
            // (image right in this front-left view). It does not identify charger brand.
            var stand = new Border { Width = 36, Height = 76, CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromArgb(255, 234, 238, 242)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 120, 131, 146)), BorderThickness = new Thickness(1) };
            stand.Child = new Border { Margin = new Thickness(6, 9, 6, 19), CornerRadius = new CornerRadius(5), Background = accent,
                Child = new TextBlock { Text = "\uE945", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 17, Foreground = new SolidColorBrush(Colors.White), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            Canvas.SetLeft(stand, 357); Canvas.SetTop(stand, 173); chargingLayer.Children.Add(stand);
            var figure = new PathFigure { StartPoint = new Point(357, 213), IsClosed = false };
            figure.Segments.Add(new BezierSegment { Point1 = new Point(322, 263), Point2 = new Point(397, 140), Point3 = new Point(portX, portY) });
            var geometry = new PathGeometry(); geometry.Figures.Add(figure);
            chargingLayer.Children.Add(new Windows.UI.Xaml.Shapes.Path { Data = geometry, Stroke = new SolidColorBrush(Color.FromArgb(255, 37, 47, 60)), StrokeThickness = 5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
            var connector = new Border { Width = 15, Height = 8, Background = accent, CornerRadius = new CornerRadius(3), RenderTransform = new RotateTransform { Angle = 35, CenterX = 7, CenterY = 4 } };
            Canvas.SetLeft(connector, portX - 8); Canvas.SetTop(connector, portY - 4); chargingLayer.Children.Add(connector);
            if (active && new Windows.UI.ViewManagement.UISettings().AnimationsEnabled)
            {
                chargingPulse = new Storyboard();
                var pulse = new DoubleAnimation { From = 1, To = 0.35, Duration = new Duration(TimeSpan.FromSeconds(1.2)), AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
                Storyboard.SetTarget(pulse, connector); Storyboard.SetTargetProperty(pulse, "Opacity"); chargingPulse.Children.Add(pulse); chargingPulse.Begin();
            }
        }
        private void AddFade(DependencyObject target, double from, double to)
        {
            var animation = new DoubleAnimation { From = from, To = to, Duration = new Duration(TimeSpan.FromMilliseconds(420)), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } };
            Storyboard.SetTarget(animation, target); Storyboard.SetTargetProperty(animation, "Opacity"); transition.Children.Add(animation);
        }
    }
}
