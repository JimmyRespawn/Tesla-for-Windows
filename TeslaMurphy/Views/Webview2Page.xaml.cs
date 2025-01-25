using System;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Microsoft.Web.WebView2.Core;

namespace TeslaMurphy.Views
{
    public sealed partial class Webview2Page : Page
    {
        public Webview2Page()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await InitiateWebview2Events();
            string loginUrl = e.Parameter as string;
            if (!string.IsNullOrEmpty(loginUrl))
                StartBrowsing2(loginUrl);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            this.Frame.BackStack.Clear();
            //Windows.UI.Core.SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = Windows.UI.Core.AppViewBackButtonVisibility.Collapsed;
        }

        private async Task<bool> InitiateWebview2Events()
        {
            //if(GetWebView2.CoreWebView2 == null)
            //    GetWebView2.CoreWebView2 = 
            //GetWebView2.CoreProcessFailed += GetWebView2_CoreProcessFailed;
            //var environment = await CoreWebView2Environment.CreateAsync();
            await GetWebView2.EnsureCoreWebView2Async();
            if (GetWebView2.CoreWebView2 != null)
            {
                //GetWebView2.NavigationStarting += GetWebView2_NavigationStarting;
                //Xbox Crash
                //GetWebView2.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
                GetWebView2.CoreWebView2.ContentLoading += CoreWebView2_ContentLoading;
                //GetWebView2.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
                GetWebView2.CoreWebView2.Settings.AreDevToolsEnabled = false;
                // 订阅 ContextMenuRequested 事件
                GetWebView2.CoreWebView2.ContextMenuRequested += CoreWebView2_ContextMenuRequested;
            }
            else
            {

            }
            return true;
        }

        private void CoreWebView2_ContextMenuRequested(CoreWebView2 sender, CoreWebView2ContextMenuRequestedEventArgs args)
        {
            // 禁用右键菜单
            args.Handled = true;
        }

        private void GetWebView2_NavigationCompleted(Microsoft.UI.Xaml.Controls.WebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs args)
        {
            //WebviewTitle.Text = GetWebView2.CoreWebView2.DocumentTitle.ToString();
            //if (GetWebView2.Source.AbsoluteUri != _lastInvokeUrl)
            //{
            //    _lastInvokeUrl = GetWebView2.Source.AbsoluteUri;
            //    InovokeSitesYoutube();
            //}
        }

        private void StartBrowsing2(string url)
        {
            try
            {
                GetWebView2.CoreWebView2.Settings.UserAgent = "Mozilla/5.0 (Windows NT 10.0) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/42.0.2311.135 Safari/537.36 Edge/12.10136";
                GetWebView2.Source = new Uri(url);
            }
            catch
            {

            }
        }

        private void GetWebView2_NavigationStarting(Microsoft.UI.Xaml.Controls.WebView2 sender, CoreWebView2NavigationStartingEventArgs args)
        {
            string url = "";
            try
            {
                url = args.Uri.ToString();

            }
            finally
            {

            }
        }

        private async void CoreWebView2_ContentLoading(CoreWebView2 sender, CoreWebView2ContentLoadingEventArgs args)
        {
            string newUrl = sender.Source;
            if (newUrl.StartsWith("http://logedout"))
            {
                MainPage.Instance.ShowToast("Loged out. Restart soon.", "\uE73E");
                //Loged out
                await Windows.Storage.ApplicationData.Current.ClearAsync();
                await Task.Delay(1000);
                await Windows.ApplicationModel.Core.CoreApplication.RequestRestartAsync(string.Empty);
            }
            else if (newUrl.Contains("code="))
            {
                newUrl = newUrl.Substring(newUrl.IndexOf("code="));
                Frame.Navigate(typeof(CarPage), newUrl);
            }
        }
    }
}