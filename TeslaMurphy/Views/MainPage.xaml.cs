using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using TeslaMurphy.Models;
using TeslaMurphy.Helpers;
using System.Threading.Tasks;
using Windows.UI.Core;
using muxc = Microsoft.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
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
            SetupSubscriptionInfoAsync();
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
                string regionCode = localSettings.Values["region"].ToString();
                if (regionCode == "NA")
                {
                    //North America, Asia Pacific (excluding China)
                    AppSettings.Instance.Region_URL = "tesla.com";
                    AppSettings.Instance.Region = 0;
                    AppSettings.Instance.Base_URL = "https://fleet-api.prd.na.vn.cloud.tesla.com";
                    AppSettings.Instance.Client_id = ""; //Replace with you own key
                    AppSettings.Instance.Client_secret = ""; //Replace with you own key
                }
                else if(regionCode == "EU")
                {
                    //Europe, mid-east, africa
                    AppSettings.Instance.Region_URL = "tesla.com";
                    AppSettings.Instance.Region = 1;
                    AppSettings.Instance.Base_URL = "https://fleet-api.prd.eu.vn.cloud.tesla.com";
                    AppSettings.Instance.Client_id = "";//Replace with you own key
                    AppSettings.Instance.Client_secret = "";//Replace with you own key
                }
                else
                {
                    //Mainland China
                    AppSettings.Instance.Region_URL = "tesla.cn";
                    AppSettings.Instance.Region = 2;
                    AppSettings.Instance.Base_URL = "https://fleet-api.prd.cn.vn.cloud.tesla.cn";
                    AppSettings.Instance.Client_id = "";//Replace with you own key
                    AppSettings.Instance.Client_secret = "";//Replace with you own key
                }
            }

            if (localSettings.Values.ContainsKey("lengthunit"))
                AppSettings.Instance.Length_unit = 1;

            if (localSettings.Values.ContainsKey("theme"))
            {
                if (localSettings.Values["theme"].ToString() == "Light")
                {
                    UWPGeneralHelper.ThemeSwitchAsync(ThemeMode.Light);
                    AppSettings.Instance.CurrentTheme = ThemeMode.Light;
                }
                else if (localSettings.Values["theme"].ToString() == "Dark")
                {
                    UWPGeneralHelper.ThemeSwitchAsync(ThemeMode.Dark);
                    AppSettings.Instance.CurrentTheme = ThemeMode.Dark;
                }
            }

            if(DateTime.Now.Date < new DateTime(2025, 2, 27))
                AppSettings.Instance.IsPro = true;
        }

        private void InitializeAppTitleBar(string device, ThemeMode theme)
        {
            switch (device)
            {
                default:
                    Windows.ApplicationModel.Core.CoreApplicationViewTitleBar coreTitleBar = Windows.ApplicationModel.Core.CoreApplication.GetCurrentView().TitleBar;
                    coreTitleBar.ExtendViewIntoTitleBar = true;
                    var ttb = ApplicationView.GetForCurrentView().TitleBar;
                    var backColor = Windows.UI.Colors.Transparent;

                    ttb.BackgroundColor = backColor;
                    ttb.ButtonBackgroundColor = backColor;
                    ttb.InactiveBackgroundColor = backColor;
                    ttb.ButtonInactiveBackgroundColor = backColor;

                    if (theme != ThemeMode.Auto)
                    {
                        var foreGroundColor = Windows.UI.Colors.Black;
                        if (theme == ThemeMode.Dark)
                            foreGroundColor = Windows.UI.Colors.White;
                        ttb.ForegroundColor = foreGroundColor;
                        ttb.ButtonForegroundColor = foreGroundColor;
                    }
                    //ttb.InactiveForegroundColor = foreGroundColor;
                    //ttb.ButtonInactiveForegroundColor = foreGroundColor;

                    //ttb.InactiveForegroundColor = StringtoColor.ConvertStringToColor("#f6f6f6");
                    //ttb.ButtonInactiveForegroundColor = StringtoColor.ConvertStringToColor("#f6f6f6");


                    //ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size { Width = 370, Height = 370 });
                    break;
                case "Xbox":
                    NavigationViewControl.PaneDisplayMode = Microsoft.UI.Xaml.Controls.NavigationViewPaneDisplayMode.Top;
                    break;
                case "Phone":
                    //Windows.UI.ViewManagement.StatusBar.GetForCurrentView().BackgroundColor = StringtoColor.ConvertStringToColor("#848693");
                    //Windows.UI.ViewManagement.StatusBar.GetForCurrentView().BackgroundOpacity = 1;
                    //Windows.UI.ViewManagement.StatusBar.GetForCurrentView().ForegroundColor = StringtoColor.ConvertStringToColor("#FFFFFF");
                    break;
            }
        }

        private void NavigationViewControl_SelectionChanged(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItemContainer != null)
            {
                if (args.IsSettingsSelected == true)
                    ContentFrame.Navigate(typeof(Views.SettingsPage));
                else if (args.SelectedItemContainer.Tag.ToString() == "car")
                    ContentFrame.Navigate(typeof(Views.CarPage));
                else if (args.SelectedItemContainer.Tag.ToString() == "charge")
                    ContentFrame.Navigate(typeof(Views.ChargePage));
                else if (args.SelectedItemContainer.Tag.ToString() == "web")
                    ContentFrame.Navigate(typeof(Views.Webview2Page));
                else if (args.SelectedItemContainer.Tag.ToString() == "ads")
                    ContentFrame.Navigate(typeof(Views.ProAdsPage));
            }
            else
            {
                ContentFrame.Navigate(typeof(Views.CarPage));
            }
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
            NavView_Navigate("car", new Windows.UI.Xaml.Media.Animation.EntranceNavigationTransitionInfo());

            // Listen to the window directly so the app responds
            // to accelerator keys regardless of which element has focus.
            Window.Current.CoreWindow.Dispatcher.AcceleratorKeyActivated +=
                CoreDispatcher_AcceleratorKeyActivated;

            Window.Current.CoreWindow.PointerPressed += CoreWindow_PointerPressed;

            SystemNavigationManager.GetForCurrentView().BackRequested += System_BackRequested;
        }

        private void CoreDispatcher_AcceleratorKeyActivated(CoreDispatcher sender, AcceleratorKeyEventArgs e)
        {
            // When Alt+Left are pressed navigate back
            if (e.EventType == CoreAcceleratorKeyEventType.SystemKeyDown
                && e.VirtualKey == Windows.System.VirtualKey.Left
                && e.KeyStatus.IsMenuKeyDown == true
                && !e.Handled)
            {
                e.Handled = TryGoBack();
            }
        }

        private void CoreWindow_PointerPressed(CoreWindow sender, PointerEventArgs e)
        {
            // Handle mouse back button.
            if (e.CurrentPoint.Properties.IsXButton1Pressed)
            {
                e.Handled = TryGoBack();
            }
        }

        private bool TryGoBack()
        {
            if (!ContentFrame.CanGoBack)
                return false;

            // Don't go back if the nav pane is overlayed.
            if (NavigationViewControl.IsPaneOpen &&
                (NavigationViewControl.DisplayMode == muxc.NavigationViewDisplayMode.Compact ||
                 NavigationViewControl.DisplayMode == muxc.NavigationViewDisplayMode.Minimal))
                return false;

            ContentFrame.GoBack();
            return true;
        }

        private void System_BackRequested(object sender, BackRequestedEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = TryGoBack();
            }
        }

        private void On_Navigated(object sender, NavigationEventArgs e)
        {
            NavigationViewControl.IsBackEnabled = ContentFrame.CanGoBack;


            if (ContentFrame.SourcePageType == typeof(Views.SettingsPage))
            {
                // SettingsItem is not part of NavView.MenuItems, and doesn't have a Tag.
                NavigationViewControl.SelectedItem = (muxc.NavigationViewItem)NavigationViewControl.SettingsItem;
                NavigationViewControl.Header = "Settings";
            }
            else if (ContentFrame.SourcePageType != null)
            {
                var item = _pages.FirstOrDefault(p => p.Page == e.SourcePageType);

                NavigationViewControl.SelectedItem = NavigationViewControl.MenuItems
                    .OfType<muxc.NavigationViewItem>()
                    .First(n => n.Tag.Equals(item.Tag));

                //NavigationViewControl.Header =
                //    ((muxc.NavigationViewItem)NavigationViewControl.SelectedItem)?.Content?.ToString();
            }
            NavigationViewControl.Header = "";
        }

        private void NavView_Navigate(string navItemTag, Windows.UI.Xaml.Media.Animation.NavigationTransitionInfo transitionInfo)
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
                //    NavigationViewControl.PaneDisplayMode = muxc.NavigationViewPaneDisplayMode.LeftMinimal;
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
            ("charge", typeof(Views.ChargePage)),
            ("web", typeof(Views.Webview2Page)),
            ("ads", typeof(Views.ProAdsPage))
        };

        private void NavView_BackRequested(muxc.NavigationView sender, muxc.NavigationViewBackRequestedEventArgs args)
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
        private readonly static string subscriptionStoreId = "";//ID to be updated
        // This is the entry point method for the example.
        public async Task SetupSubscriptionInfoAsync()
        {
            try
            {
                if (context == null)
                    context = StoreContext.GetDefault();

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