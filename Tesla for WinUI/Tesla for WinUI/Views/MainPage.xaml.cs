using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI.ViewManagement;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using TeslaMurphy.Models;
using TeslaMurphy.Helpers;
using System.Threading.Tasks;

using Muxc = Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Services.Store;

namespace TeslaMurphy.Views
{
    public sealed partial class MainPage : Page
    {
        private bool isFirstLaunch = true;
        private static MainPage _instance;
        private DispatcherTimer toastTimer;
        public MainPage()
        {
            this.InitializeComponent();
            if (isFirstLaunch)
            {
                _instance = this;
                InitializeAppSetings();
                InitializeAppTitleBar(AppSettings.Instance.Device, AppSettings.Instance.CurrentTheme);
                isFirstLaunch = false;
            }
        }

        public static MainPage Instance
        {
            get
            {
                return _instance;
            }
        }

        private async void InitializeAppSetings()
        {
            _ = SetupSubscriptionInfoAsync();
            AppSettings.Instance.Device = DeviceTypeHelper.GetDeviceFormFactorType().ToString();
            AppSettings.Instance.CurrentTheme = await UWPGeneralHelper.GetCurrentTheme();
            Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            if (localSettings.Values.ContainsKey("accesstoken"))
            {
                AppSettings.Instance.Access_token = localSettings.Values["accesstoken"].ToString();
                AppSettings.Instance.IsLogedin = true;
            }
            if (localSettings.Values.ContainsKey("refreshtoken"))
                AppSettings.Instance.Refresh_token = localSettings.Values["refreshtoken"].ToString();
            if (localSettings.Values.ContainsKey("maincarvin"))
                AppSettings.Instance.Current_carvin = localSettings.Values["maincarvin"].ToString();
            if (localSettings.Values.ContainsKey("region"))
            {
                TeslaMurphy.Services.TeslaConfiguration.ApplyRegion(AppSettings.Instance, localSettings.Values["region"].ToString());
            }

            if (localSettings.Values.ContainsKey("lengthunit"))
                AppSettings.Instance.Length_unit = 1;

            if (localSettings.Values.ContainsKey("theme"))
            {
                if (localSettings.Values["theme"].ToString() == "Light")
                {
                    await UWPGeneralHelper.ThemeSwitchAsync(ThemeMode.Light);
                    AppSettings.Instance.CurrentTheme = ThemeMode.Light;
                }
                else if (localSettings.Values["theme"].ToString() == "Dark")
                {
                    await UWPGeneralHelper.ThemeSwitchAsync(ThemeMode.Dark);
                    AppSettings.Instance.CurrentTheme = ThemeMode.Dark;
                }
            }

            if(DateTime.Now.Date < new DateTime(2027, 2, 27))
                AppSettings.Instance.IsPro = true;
        }

        private void InitializeAppTitleBar(string device, ThemeMode theme)
        {
            Loaded += (s, e) => { App.MainWindow.ExtendsContentIntoTitleBar = true; App.MainWindow.SetTitleBar(AppTitleBar); };
        }
        public void NavigateToAuthentication(Uri uri)
        {
            ContentFrame.Navigate(typeof(CarPage), uri.Query.TrimStart('?'));
        }
        private void NavigationViewControl_SelectionChanged(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewSelectionChangedEventArgs args)
        {
            // Back navigation and the comparison button also update the selected
            // item. Reuse the page guard so that this does not navigate twice.
            string tag = args.IsSettingsSelected ? "settings"
                : args.SelectedItemContainer?.Tag?.ToString() ?? "car";
            NavView_Navigate(tag, new EntranceNavigationTransitionInfo());
        }

        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            // Add handler for ContentFrame navigation.
            ContentFrame.Navigated += On_Navigated;

            // NavView doesn't load any page by default, so load home page.
            //NavigationViewControl.SelectedItem = NavigationViewControl.MenuItems[0];
            // If navigation occurs on SelectionChanged, this isn't needed.
            // Because we use ItemInvoked to navigate, we need to call Navigate
            // here to load the home page.
            if (App.PendingAuthentication != null) NavigateToAuthentication(App.PendingAuthentication);
            else NavView_Navigate("car", new Microsoft.UI.Xaml.Media.Animation.EntranceNavigationTransitionInfo());

            var back = new Microsoft.UI.Xaml.Input.KeyboardAccelerator { Key = Windows.System.VirtualKey.Left, Modifiers = Windows.System.VirtualKeyModifiers.Menu };
            back.Invoked += (s, args) => args.Handled = TryGoBack();
            KeyboardAccelerators.Add(back);
            PointerPressed += (s, args) => { if (args.GetCurrentPoint(this).Properties.IsXButton1Pressed) args.Handled = TryGoBack(); };
        }
        private bool TryGoBack()
        {
            if (!ContentFrame.CanGoBack)
                return false;

            // Don't go back if the nav pane is overlayed.
            if (NavigationViewControl.IsPaneOpen &&
                (NavigationViewControl.DisplayMode == Muxc.NavigationViewDisplayMode.Compact ||
                 NavigationViewControl.DisplayMode == Muxc.NavigationViewDisplayMode.Minimal))
                return false;

            ContentFrame.GoBack();
            return true;
        }

        private void On_Navigated(object sender, NavigationEventArgs e)
        {
            NavigationViewControl.IsBackEnabled = ContentFrame.CanGoBack;
            bool garage = e.SourcePageType == typeof(Model3HelixPage);
            NavigationViewControl.IsPaneVisible = !garage;
            NavigationViewControl.IsBackButtonVisible = garage ? Muxc.NavigationViewBackButtonVisible.Collapsed : Muxc.NavigationViewBackButtonVisible.Auto;


            if (ContentFrame.SourcePageType == typeof(Views.SettingsPage))
            {
                // SettingsItem is not part of NavView.MenuItems, and doesn't have a Tag.
                NavigationViewControl.SelectedItem = (Muxc.NavigationViewItem)NavigationViewControl.SettingsItem;
                NavigationViewControl.Header = "Settings";
            }
            else if (ContentFrame.SourcePageType != null)
            {
                var item = _pages.FirstOrDefault(p => p.Page == e.SourcePageType);

                NavigationViewControl.SelectedItem = NavigationViewControl.MenuItems
                    .OfType<Muxc.NavigationViewItem>()
                    .First(n => n.Tag.Equals(item.Tag));

                //NavigationViewControl.Header =
                //    ((Muxc.NavigationViewItem)NavigationViewControl.SelectedItem)?.Content?.ToString();
            }
            NavigationViewControl.Header = "";
        }

        private void NavView_Navigate(string navItemTag, Microsoft.UI.Xaml.Media.Animation.NavigationTransitionInfo transitionInfo)
        {
            Type _page = null;

            if (navItemTag == "settings")
            {
                _page = typeof(Views.SettingsPage);
            }
            else
            {
                var item = _pages.FirstOrDefault(p => p.Tag.Equals(navItemTag));
                _page = item.Page;
                //if (navItemTag == "Video")
                //    NavigationViewControl.PaneDisplayMode = Muxc.NavigationViewPaneDisplayMode.LeftMinimal;
            }
            // Get the page type before navigation so you can prevent duplicate
            // entries in the backstack.
            var preNavPageType = ContentFrame.CurrentSourcePageType;


            // Only navigate if the selected page isn't currently loaded.
            if (!(_page is null) && !Type.Equals(preNavPageType, _page))
            {
                ContentFrame.Navigate(_page, null, transitionInfo);
            }
        }

        private readonly List<(string Tag, Type Page)> _pages = new List<(string Tag, Type Page)>
        {
            ("car", typeof(Views.CarPage)),
            ("model3helix", typeof(Views.Model3HelixPage)),
            ("charge", typeof(Views.ChargePage)),
            ("web", typeof(Views.Webview2Page)),
            ("ads", typeof(Views.ProAdsPage))
        };

        private void NavView_BackRequested(Muxc.NavigationView sender, Muxc.NavigationViewBackRequestedEventArgs args)
        {
            TryGoBack();
        }

        public void ShowToast(string title, string icon = "\uE7BA", int durationInSeconds = 5)
        {
            try
            {
                ToastTitle.Text = title;
                ToastIcon.Text = icon;
                ToastContainer.Visibility = Visibility.Visible;

                var showToastStoryboard = (Storyboard)this.Resources["ShowToastGrid"];
                showToastStoryboard.Begin();

                if (toastTimer != null)
                {
                    toastTimer.Stop();
                }

                toastTimer = new DispatcherTimer();
                toastTimer.Interval = TimeSpan.FromSeconds(durationInSeconds);
                toastTimer.Tick += (sender, args) =>
                {
                    var hideToastStoryboard = (Storyboard)this.Resources["HideToastGrid"];
                    hideToastStoryboard.Completed += (s, e) =>
                    {
                        ToastContainer.Visibility = Visibility.Collapsed;
                        var resetToastStoryboard = (Storyboard)this.Resources["ResetToastGrid"];
                        resetToastStoryboard.Begin();
                        //toastTimer.Tick -= this.toastTimer.Tick;
                        toastTimer = null;
                    };
                    hideToastStoryboard.Begin();
                    toastTimer.Stop();
                };
                toastTimer.Start();
            }
            catch
            {

            }
        }


        #region Subscription
        private static StoreContext context = null;
        public static StoreProduct subscriptionStoreProduct;
        private static string subscriptionStoreId => TeslaMurphy.Services.StoreConfiguration.SubscriptionStoreId;
        // This is the entry point method for the example.
        public async Task SetupSubscriptionInfoAsync()
        {
            try
            {
                if (context == null)
                    { context = StoreContext.GetDefault(); WinRT.Interop.InitializeWithWindow.Initialize(context, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow)); }

                bool userOwnsSubscription = await CheckIfUserHasSubscriptionAsync();
                if (userOwnsSubscription)
                {
                    AppSettings.Instance.IsPro = true;
                    // Unlock all the subscription add-on features here.
                    return;
                }

                // Get the StoreProduct that represents the subscription add-on.
                subscriptionStoreProduct = await GetSubscriptionProductAsync();
                if (subscriptionStoreProduct == null)
                {
                    return;
                }

                // Check if the first SKU is a trial and notify the customer that a trial is available.
                // If a trial is available, the Skus array will always have 2 purchasable SKUs and the
                // first one is the trial. Otherwise, this array will only have one SKU.
                StoreSku sku = subscriptionStoreProduct.Skus[0];
                if (sku.SubscriptionInfo.HasTrialPeriod)
                {
                    // You can display the subscription trial info to the customer here. You can use 
                    // sku.SubscriptionInfo.TrialPeriod and sku.SubscriptionInfo.TrialPeriodUnit 
                    // to get the trial details.
                }
                else
                {
                    // You can display the subscription purchase info to the customer here. You can use 
                    // sku.SubscriptionInfo.BillingPeriod and sku.SubscriptionInfo.BillingPeriodUnit
                    // to provide the renewal details.
                }
            }
            catch
            {

            }
        }

        public static async Task<bool> PromptUserToPurchaseAsync()
        {
            try
            {
                if (subscriptionStoreProduct == null)
                {
                    // Get the StoreProduct that represents the subscription add-on.
                    subscriptionStoreProduct = await GetSubscriptionProductAsync();
                    if (subscriptionStoreProduct == null)
                    {
                        return false;
                    }
                }

                // Request a purchase of the subscription product. If a trial is available it will be offered 
                // to the customer. Otherwise, the non-trial SKU will be offered.
                StorePurchaseResult result = await subscriptionStoreProduct.RequestPurchaseAsync();

                // Capture the error message for the operation, if any.
                string extendedError = string.Empty;
                if (result.ExtendedError != null)
                {
                    extendedError = result.ExtendedError.Message;
                }

                switch (result.Status)
                {
                    case StorePurchaseStatus.Succeeded:
                        return true;
                    // Show a UI to acknowledge that the customer has purchased your subscription 
                    // and unlock the features of the subscription. 
                    case StorePurchaseStatus.NotPurchased:
                        System.Diagnostics.Debug.WriteLine("The purchase did not complete. " +
                            "The customer may have cancelled the purchase. ExtendedError: " + extendedError);
                        break;

                    case StorePurchaseStatus.ServerError:
                        break;
                    case StorePurchaseStatus.NetworkError:
                        System.Diagnostics.Debug.WriteLine("The purchase was unsuccessful due to a server or network error. " +
                            "ExtendedError: " + extendedError);
                        break;

                    case StorePurchaseStatus.AlreadyPurchased:
                        System.Diagnostics.Debug.WriteLine("The customer already owns this subscription." +
                                "ExtendedError: " + extendedError);
                        break;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<StoreProduct> GetSubscriptionProductAsync()
        {
            if (string.IsNullOrWhiteSpace(subscriptionStoreId)) return null;
            try
            {
                // Load the sellable add-ons for this app and check if the trial is still 
                // available for this customer. If they previously acquired a trial they won't 
                // be able to get a trial again, and the StoreProduct.Skus property will 
                // only contain one SKU.
                StoreProductQueryResult result =
                    await context.GetAssociatedStoreProductsAsync(new string[] { "Durable" });

                if (result.ExtendedError != null)
                {
                    System.Diagnostics.Debug.WriteLine("Something went wrong while getting the add-ons. " +
                        "ExtendedError:" + result.ExtendedError);
                    return null;
                }

                // Look for the product that represents the subscription.
                foreach (var item in result.Products)
                {
                    StoreProduct product = item.Value;
                    if (product.StoreId == subscriptionStoreId)
                    {
                        return product;
                    }
                }

                System.Diagnostics.Debug.WriteLine("The subscription was not found.");
            }
            catch
            {

            }
            return null;
        }

        private async Task<bool> CheckIfUserHasSubscriptionAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(subscriptionStoreId)) return false;
                StoreAppLicense appLicense = await context.GetAppLicenseAsync();

                // Check if the customer has the rights to the subscription.
                foreach (var addOnLicense in appLicense.AddOnLicenses)
                {
                    StoreLicense license = addOnLicense.Value;
                    if (license.SkuStoreId.StartsWith(subscriptionStoreId))
                    {
                        if (license.IsActive)
                        {
                            // The expiration date is available in the license.ExpirationDate property.
                            return true;
                        }
                    }
                }
            }
            catch
            {

            }
            // The customer does not have a license to the subscription.
            return false;
        }
        #endregion
    }
}
