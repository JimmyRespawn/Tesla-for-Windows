using System;
using System.Threading.Tasks;
using TeslaMurphy.Controls;
using TeslaMurphy.Helpers;
using TeslaMurphy.Models;
using TeslaMurphy.Services;
using Windows.Foundation;
using Windows.Services.Store;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace TeslaMurphy.Views
{
    public sealed partial class SettingsPage : Page
    {
        Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
        public SettingsPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            Version.Text = GetAppVersion();
            //string code = e.Parameter as string;
            var resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();
            if (AppSettings.Instance.Region == 0)
                RegionTextBlock.Text = resourceLoader.GetString("RegionNA/Content");
            else if (AppSettings.Instance.Region == 1)
                RegionTextBlock.Text = resourceLoader.GetString("RegionEU/Content");
            else if (AppSettings.Instance.Region == 2)
                RegionTextBlock.Text = resourceLoader.GetString("RegionCN/Content");
            if (AppSettings.Instance.Length_unit == 1)
                LengthUnitComboBox.SelectedIndex = 1;
            else
                LengthUnitComboBox.SelectedIndex = 0;
            if (localSettings.Values.ContainsKey("theme"))
            {
                if(localSettings.Values["theme"].ToString() == "Dark")
                    ThemeComboBox.SelectedIndex = 2;
                else
                {
                    ThemeComboBox.SelectedIndex = 1;
                }
            }
            else
            {
                ThemeComboBox.SelectedIndex = 0;
            }

            if (!localSettings.Values.ContainsKey("accesstoken"))
            {
                LogoutTextBlock.Text = resourceLoader.GetString("Login/Text");
                LogoutTipTextBlock.Text = resourceLoader.GetString("LoginTip/Text"); 
            }
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeComboBox.SelectedIndex == 0)
            {
                if (localSettings.Values.ContainsKey("theme"))
                {
                    localSettings.Values.Remove("theme");
                    SwitchTheme(ThemeMode.Auto);
                }
            }
            else if (ThemeComboBox.SelectedIndex == 1)
            {
                if (!localSettings.Values.ContainsKey("theme") || localSettings.Values["theme"].ToString() != "Light")
                {
                    Windows.Storage.ApplicationData.Current.LocalSettings.Values["theme"] = "Light";
                    SwitchTheme(ThemeMode.Light);
                    AppSettings.Instance.CurrentTheme = ThemeMode.Light;
                }
            }
            else if (ThemeComboBox.SelectedIndex == 2)
            {
                if (!localSettings.Values.ContainsKey("theme") || localSettings.Values["theme"].ToString() != "Dark")
                {
                    Windows.Storage.ApplicationData.Current.LocalSettings.Values["theme"] = "Dark";
                    SwitchTheme(ThemeMode.Dark);
                    AppSettings.Instance.CurrentTheme = ThemeMode.Dark;
                }
            }
        }

        private async void SwitchTheme(ThemeMode theme)
        {
            AppSettings.Instance.CurrentTheme = theme;
            await UWPGeneralHelper.ThemeSwitchAsync(AppSettings.Instance.CurrentTheme);
        }

        private string GetAppVersion()
        {
            var ver = Windows.ApplicationModel.Package.Current.Id.Version;
            string version = ver.Major.ToString() + "." + ver.Minor.ToString() + "." + ver.Build.ToString();
            return version;
        }

        private async void EmailFeedbackButton_Click(object sender, RoutedEventArgs e)
        {
            var resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();
            var ver = Windows.ApplicationModel.Package.Current.Id.Version;
            string versionString = ver.Major.ToString() + "." + ver.Minor.ToString() + "." + ver.Build.ToString();
            await UWPGeneralHelper.ComposeEmail("jimmyrespawn@hotmail.com", "Link for Tesla Feedback", "\n\nVersion: " + versionString);
        }

        private void TwitterButton_Click(object sender, RoutedEventArgs e)
        {
            UWPGeneralHelper.OpenInDefaultBrowser("https://twitter.com/jimmyrespawn");
        }

        private async void ClearCache_Click(object sender, RoutedEventArgs e)
        {
            var resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();
            bool isClear = await DisplayPopout.dualButton( "Warning!", "All app settings and token will be deleted permanently.", "Ok", "Cancel");
            if (isClear)
            {
                await Windows.Storage.ApplicationData.Current.ClearAsync();
                await Task.Delay(1000);
                await Windows.ApplicationModel.Core.CoreApplication.RequestRestartAsync(string.Empty);
            }
        }

        private void Check4UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            CheckUpdateText.Text = "Checking...";
            CkechUpdateAsync();
        }

        private async Task CkechUpdateAsync()
        {
            //https://docs.microsoft.com/en-us/windows/uwp/packaging/self-install-package-updates
            StoreContext context = StoreContext.GetDefault();
            var updates = await context.GetAppAndOptionalStorePackageUpdatesAsync();
            var resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();

            if (updates.Count > 0)
            {
                //var resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();

                // Alert the user that updates are available and ask for their consent
                // to start the updates.
                //bool isUpdate = await DisplayPopout.dualButton(resourceLoader.GetString("UpdateDTitle"), resourceLoader.GetString("UpdateDContent"),
                //    resourceLoader.GetString("Yes"), resourceLoader.GetString("No"));
                bool isUpdate = await DisplayPopout.dualButton("Update available", "Do you wish to update the app right now?", "Ok", "Cancel");

                if (isUpdate)
                {
                    PB.Visibility = Visibility.Visible;

                    // Download and install the updates.
                    IAsyncOperationWithProgress<StorePackageUpdateResult, StorePackageUpdateStatus> downloadOperation =
                        context.RequestDownloadAndInstallStorePackageUpdatesAsync(updates);

                    // The Progress async method is called one time for each step in the download
                    // and installation process for each package in this request.
                    downloadOperation.Progress = async (asyncInfo, progress) =>
                    {
                        await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                        () =>
                        {
                            PB.Value = progress.PackageDownloadProgress;
                        });
                    };

                    StorePackageUpdateResult result = await downloadOperation.AsTask();
                }
            }
            else
            {
                CheckUpdateText.Text = "Up to date";//resourceLoader.GetString("UpToDate/Text");
                Check4UpdateButton.IsEnabled = false;
            }
        }

        private void PlusIntroButton_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(ProAdsPage));
        }

        private void ThmeButton_Click(object sender, RoutedEventArgs e)
        {
            this.ThemeComboBox.IsDropDownOpen = !this.ThemeComboBox.IsDropDownOpen;
        }

        private void LengthUnitButton_Click(object sender, RoutedEventArgs e)
        {
            this.LengthUnitComboBox.IsDropDownOpen = !this.LengthUnitComboBox.IsDropDownOpen;
        }

        private void LengthUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LengthUnitComboBox.SelectedIndex == 0)
            {
                AppSettings.Instance.Length_unit = 0;
                if (localSettings.Values.ContainsKey("lengthunit"))
                    localSettings.Values.Remove("lengthunit");
            }
            else
            {
                AppSettings.Instance.Length_unit = 1;
                if (!localSettings.Values.ContainsKey("lengthunit"))
                    Windows.Storage.ApplicationData.Current.LocalSettings.Values["lengthunit"] = "miles";
            }
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            if (localSettings.Values.ContainsKey("accesstoken"))
            {
                var resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();
                bool isSignedOut = await DisplayPopout.dualButton("Warning!", "Current signed-in token will be revoked.", "Ok", "Cancel");
                if (isSignedOut)
                {
                    //Logout
                    string revokeUrl = "https://auth." + AppSettings.Instance.Region_URL + "/user/revoke/consent?revoke_client_id=" + AppSettings.Instance.Client_id + "&back_url=" + "http://logedout";
                    this.Frame.Navigate(typeof(Webview2Page), revokeUrl);
                }
            }
            else
            {
                //Login
                ShowLoginDialog();
            }
        }

        public async void ShowLoginDialog()
        {
            LoginContentDialog dialog = new LoginContentDialog();
            dialog.DefaultButton = ContentDialogButton.Primary;
            ContentDialogResult result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                //Login or experience the app
                bool isTestMode = dialog.IsTestMode;
                if (isTestMode)
                {
                    AppSettings.Instance.IsTestMode = true;
                }
                else
                {
                    int region = dialog.Region;
                    if (region == 0)
                    {
                        Windows.Storage.ApplicationData.Current.LocalSettings.Values["region"] = "NA";
                        AppSettings.Instance.Client_id = "";//Replace with you own key
                        AppSettings.Instance.Client_secret = "";//Replace with you own key
                        AppSettings.Instance.Region_URL = "tesla.com";
                    }
                    else if (region == 2)
                    {
                        Windows.Storage.ApplicationData.Current.LocalSettings.Values["region"] = "CN";
                        AppSettings.Instance.Client_id = "";//Replace with you own key
                        AppSettings.Instance.Client_secret = "";//Replace with you own key
                        AppSettings.Instance.Region_URL = "tesla.cn";
                    }
                    else
                    {
                        Windows.Storage.ApplicationData.Current.LocalSettings.Values["region"] = "NA";
                        AppSettings.Instance.Client_id = "";//Replace with you own key
                        AppSettings.Instance.Client_secret = "";//Replace with you own key
                        AppSettings.Instance.Region_URL = "tesla.com";
                    }
                    string authRequestUrl = await TeslaFleetServices.GenerateAuthorizeUriAsync("https://auth." + AppSettings.Instance.Region_URL + "/oauth2/v3/authorize", AppSettings.Instance.Client_id);
                    this.Frame.Navigate(typeof(Webview2Page), authRequestUrl);
                }
            }
        }

        private async void PrivacyPolicyButton_Click(object sender, RoutedEventArgs e)
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri("https://app.linjimi.com/blog/teslawinprivacypolity/"));
        }
    }
}