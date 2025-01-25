using System;
using TeslaMurphy.Models;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TeslaMurphy.Views
{
    public sealed partial class ProAdsPage : Page
    {
        public ProAdsPage()
        {
            this.InitializeComponent();
            //if(AppSettings.Instance.IsPro)
            //    EnableSubsFeature();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.GoBack();
        }

        private async void PurchaseButton_Click(object sender, RoutedEventArgs e)
        {
            if (!AppSettings.Instance.IsPro)
            {
                bool isPurchased = await MainPage.PromptUserToPurchaseAsync();
                if (isPurchased)
                    EnableSubsFeature();
            }
            else
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri("https://account.microsoft.com/services"));
            }
        }

        private void EnableSubsFeature()
        {
            //PurchaseButton.Content = "Manage Subscription";
            //BasicButton.Content = "Unlocked";
            //BasicButton.IsEnabled = false;
            //PurchaseButton.IsEnabled = true;
            AppSettings.Instance.IsPro = true;
        }
    }
}
