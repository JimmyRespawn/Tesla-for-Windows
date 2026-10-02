using System;
using System.IO;
using Newtonsoft.Json;

namespace TeslaMurphy.Services;

internal static class StoreConfiguration
{
    private sealed class Configuration
    {
        public string SubscriptionStoreId { get; set; }
        public string AppStoreProductId { get; set; }
    }

    private static readonly Lazy<Configuration> configuration = new(() =>
    {
        using var stream = typeof(StoreConfiguration).Assembly.GetManifestResourceStream("TeslaMurphy.StoreConfiguration.json");
        if (stream == null) return new Configuration();
        using var reader = new StreamReader(stream);
        return JsonConvert.DeserializeObject<Configuration>(reader.ReadToEnd()) ?? new Configuration();
    });

    public static string SubscriptionStoreId => configuration.Value.SubscriptionStoreId?.Trim() ?? "";
    public static string AppStoreProductId => configuration.Value.AppStoreProductId?.Trim() ?? "";
}
