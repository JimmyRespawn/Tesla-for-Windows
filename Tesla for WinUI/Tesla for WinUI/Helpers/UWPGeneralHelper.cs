using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.ViewManagement;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml;
using System.Net.Http;
using TeslaMurphy.Models;
using Windows.Storage;



namespace TeslaMurphy.Helpers
{
    public class UWPGeneralHelper
    {
        public static Task<bool> ThemeSwitchAsync(ThemeMode theme)
        {
            var dark = theme == ThemeMode.Dark || (theme == ThemeMode.Auto && new UISettings().GetColorValue(UIColorType.Background) == Microsoft.UI.Colors.Black);
            ((Frame)App.MainWindow.Content).RequestedTheme = dark ? ElementTheme.Dark : ElementTheme.Light;
            App.MainWindow.AppWindow.TitleBar.ButtonForegroundColor = dark ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black;
            return Task.FromResult(dark);
        }
        public async static Task<ThemeMode> GetCurrentTheme()
        {
            UISettings uiSettings = new UISettings();
            // Get the current theme
            var currentTheme = uiSettings.GetColorValue(UIColorType.Background).ToString();
            var theme = (currentTheme == "#FFFFFFFF") ? ThemeMode.Light : ThemeMode.Dark;
            await Task.Yield();
            return theme;
        }

        public async static Task<string> HttpWebRequestGetAsync(string url, bool isResult, CancellationTokenSource cts)
        {
            //To be edited in future
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/82.0.4045.0 Safari/537.36 Edg/82.0.418.0");
                try
                {
                    var response = await httpClient.GetAsync(url, cts.Token);
                    if (response.IsSuccessStatusCode)
                    {
                        string responseString = "";
                        if (isResult) responseString = response.Content.ReadAsStringAsync().Result;
                        else responseString = await response.Content.ReadAsStringAsync();
                        response.Dispose();
                        return responseString;
                    }
                    else
                    {
                        return response.StatusCode.ToString();
                    }
                }
                catch (WebException ex)
                {
                    return "errorcode:499" + ex.ToString();
                    // handle web exception
                }
                catch (TaskCanceledException ex)
                {
                    if (ex.CancellationToken == cts.Token)
                    {
                        return "errorcode:499" + ex.ToString();
                        // a real cancellation, triggered by the caller
                    }
                    else
                    {
                        // a web request timeout (possibly other things!?)
                        return "errorcode:408" + ex.ToString();
                    }
                }
            }
        }

        public static async void OpenInDefaultBrowser(string url)
        {
            try
            {
                string uri = string.Format(url);
                Uri targetUri = new Uri(String.Format("{0}", uri, UriKind.Relative));
                await Windows.System.Launcher.LaunchUriAsync(targetUri);
            }
            catch
            {

            }
        }

        public static T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is T)
                    return (T)child;
                else
                {
                    T childOfChild = FindVisualChild<T>(child);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            return null;
        }

        public static async Task<string> GetTestFileAsync(string filename)
        {
            // Get the Assets folder in the installation package
            StorageFolder assetsFolder = await Windows.ApplicationModel.Package.Current.InstalledLocation.GetFolderAsync("Assets");
            // Get the TestData folder in the Assets folder
            StorageFolder testDataFolder = await assetsFolder.GetFolderAsync("TestData");
            StorageFile jsonFile = await testDataFolder.GetFileAsync(filename);
            // Read the file content
            return await FileIO.ReadTextAsync(jsonFile) ?? string.Empty;
        }

        public async static Task<string> GetCacheFileStringAsync(string fileName)
        {
            try
            {
                Windows.Storage.StorageFolder storageFolder = Windows.Storage.ApplicationData.Current.TemporaryFolder;
                Windows.Storage.StorageFile File = await storageFolder.GetFileAsync(fileName);
                if (File == null)
                    return null;
                else
                {
                    string fileString = await Windows.Storage.FileIO.ReadTextAsync(File);
                    return fileString;
                }
            }
            catch
            {
                return null;
            }
        }

        public static async Task ComposeEmail(string emailAddress, string subject, string messageBody)
        {
            var uri = new Uri("mailto:" + Uri.EscapeDataString(emailAddress) + "?subject=" + Uri.EscapeDataString(subject) + "&body=" + Uri.EscapeDataString(messageBody));
            await Windows.System.Launcher.LaunchUriAsync(uri);
        }
        public static async Task SaveStringToCacheFileAsync(string filename, string stringToSave)
        {
            if(!string.IsNullOrEmpty(filename) && !string.IsNullOrEmpty(stringToSave))
            {
                Windows.Storage.StorageFolder storageFolder = Windows.Storage.ApplicationData.Current.TemporaryFolder;
                Windows.Storage.StorageFile newFile =
                    await storageFolder.CreateFileAsync(filename,
                        Windows.Storage.CreationCollisionOption.ReplaceExisting);
                await Windows.Storage.FileIO.WriteTextAsync(newFile, stringToSave);
            }
        }
        public static Task OpenNewWindowAsync(Type pageType, object parameter = null)
        {
            var window = new Microsoft.UI.Xaml.Window();
            var frame = new Frame();
            window.Content = frame;
            frame.Navigate(pageType, parameter);
            window.Activate();
            return Task.CompletedTask;
        }
    }
}