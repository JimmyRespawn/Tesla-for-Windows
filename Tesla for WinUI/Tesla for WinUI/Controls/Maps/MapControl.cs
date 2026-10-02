using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Windows.ApplicationModel.Resources;
using Newtonsoft.Json;
using Windows.Devices.Geolocation;
using Windows.System;

namespace TeslaMurphy.Controls.Maps;

public enum MapColorScheme { Light, Dark }
public enum MapIconKind { Charger, Supercharger, Vehicle }

public sealed class MapIcon
{
    public Geopoint Location { get; set; }
    public string Title { get; set; }
    public int ZIndex { get; set; }
    public MapIconKind Kind { get; set; }
}

// MapLibre is bundled with the app; OpenFreeMap supplies the key-free basemap.
public sealed class MapControl : UserControl
{
    private const string MapHost = "tesla-map.local";
    private readonly WebView2 browser = new();
    private bool initialized;
    private bool initializing;
    private bool ready;
    private bool updating;
    private bool updatePending;
    private Geopoint center;
    private double zoomLevel = 13;
    private int cameraRevision;

    public ObservableCollection<MapIcon> MapElements { get; } = new();
    public static readonly DependencyProperty ColorSchemeProperty = DependencyProperty.Register(
        nameof(ColorScheme), typeof(MapColorScheme), typeof(MapControl),
        new PropertyMetadata(MapColorScheme.Light, (s, e) => ((MapControl)s).UpdateMap()));
    public MapColorScheme ColorScheme { get => (MapColorScheme)GetValue(ColorSchemeProperty); set => SetValue(ColorSchemeProperty, value); }
    public Geopoint Center { get => center; set { center = value; cameraRevision++; UpdateMap(); } }
    public double ZoomLevel { get => zoomLevel; set { zoomLevel = Math.Clamp(value, 2, 19); cameraRevision++; UpdateMap(); } }

    public MapControl()
    {
        Content = browser;
        MapElements.CollectionChanged += (s, e) => UpdateMap();
        Loaded += InitializeMap;
    }

    private async void InitializeMap(object sender, RoutedEventArgs e)
    {
        if (initializing) return;
        if (initialized) { UpdateMap(); return; }
        initializing = true;
        try
        {
            Content = browser;
            await browser.EnsureCoreWebView2Async();
            browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            browser.CoreWebView2.SetVirtualHostNameToFolderMapping(MapHost,
                System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Maps"),
                CoreWebView2HostResourceAccessKind.DenyCors);
            browser.CoreWebView2.WebMessageReceived -= MapMessageReceived;
            browser.CoreWebView2.WebMessageReceived += MapMessageReceived;
            browser.CoreWebView2.NavigationStarting -= MapNavigationStarting;
            browser.CoreWebView2.NavigationStarting += MapNavigationStarting;
            browser.CoreWebView2.NewWindowRequested -= MapNewWindowRequested;
            browser.CoreWebView2.NewWindowRequested += MapNewWindowRequested;
            browser.Source = new Uri("https://" + MapHost + "/map.html");
            initialized = true;
        }
        catch
        {
            var panel = new StackPanel { Margin = new Thickness(24), Spacing = 12 };
            panel.Children.Add(new TextBlock { Text = Text("MapUnavailable"), TextWrapping = TextWrapping.Wrap });
            var retry = new Button { Content = Text("MapRetry") };
            retry.Click += InitializeMap;
            panel.Children.Add(retry);
            Content = panel;
        }
        finally { initializing = false; }
    }

    private void MapMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (!Uri.TryCreate(args.Source, UriKind.Absolute, out var source) || source.Host != MapHost) return;
        if (args.TryGetWebMessageAsString() == "ready") { ready = true; UpdateMap(); }
    }

    private static async void MapNavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs args)
    {
        if (Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri) && uri.Host == MapHost) return;
        args.Cancel = true;
        await OpenAttributionAsync(args.Uri);
    }

    private static async void MapNewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs args)
    {
        args.Handled = true;
        await OpenAttributionAsync(args.Uri);
    }

    private static async System.Threading.Tasks.Task OpenAttributionAsync(string address)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return;
        if (uri.Host != "openfreemap.org" && uri.Host != "openmaptiles.org" && uri.Host != "www.openmaptiles.org"
            && uri.Host != "www.openstreetmap.org" && uri.Host != "openstreetmap.org" && uri.Host != "maplibre.org") return;
        try { await Launcher.LaunchUriAsync(uri); } catch { }
    }

    private static string Text(string key) => new ResourceLoader().GetString(key);

    private async void UpdateMap()
    {
        if (!ready) return;
        updatePending = true;
        if (updating) return;
        updating = true;
        try
        {
            // Serialize delivery so an older snapshot cannot overwrite newer pins or theme.
            while (updatePending && ready)
            {
                updatePending = false;
                var state = new
                {
                    center = center == null ? null : new[] { center.Position.Longitude, center.Position.Latitude },
                    zoom = zoomLevel, cameraRevision, dark = ColorScheme == MapColorScheme.Dark,
                    pins = MapElements.Where(p => p.Location != null).OrderBy(p => p.ZIndex).Select(p => new
                    {
                        position = new[] { p.Location.Position.Longitude, p.Location.Position.Latitude },
                        title = p.Title, kind = p.Kind.ToString().ToLowerInvariant(), zIndex = p.ZIndex
                    }).ToArray(),
                    strings = new
                    {
                        loading = Text("MapLoading"), unavailable = Text("MapUnavailable"), retry = Text("MapRetry"),
                        mapLabel = Text("MapLabel"), zoomIn = Text("MapZoomIn"), zoomOut = Text("MapZoomOut"),
                        resetBearing = Text("MapResetBearing")
                    }
                };
                await browser.ExecuteScriptAsync("updateMap(" + JsonConvert.SerializeObject(state,
                    new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeHtml }) + ")");
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Map update failed: " + ex.Message); }
        finally { updating = false; }
    }
}
