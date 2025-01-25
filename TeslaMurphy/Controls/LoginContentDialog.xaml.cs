using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace TeslaMurphy.Controls
{
    public sealed partial class LoginContentDialog : ContentDialog
    {
        public bool IsTestMode = true;
        public int Region = 0;
        private bool IsContentDialogLoaded = false;
        public LoginContentDialog()
        {
            this.InitializeComponent();
        }

        private void ExperienceModeToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            var resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();
            if (ExperienceModeToggleSwitch.IsOn)
            {
                if (IsContentDialogLoaded)
                {
                    dialog.PrimaryButtonText = resourceLoader.GetString("LoginTitle/PrimaryButtonText");
                    RegionGrid.Visibility = Visibility.Collapsed;
                }
                IsTestMode = true;
            }
            else
            {
                if(IsContentDialogLoaded)
                {
                    dialog.PrimaryButtonText = resourceLoader.GetString("Login/Text");
                    RegionGrid.Visibility = Visibility.Visible;
                }
                IsTestMode = false;
            }
        }

        private void dialog_Loaded(object sender, RoutedEventArgs e)
        {
            IsContentDialogLoaded = true;
        }

        private void RegionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Region = RegionComboBox.SelectedIndex;
            if(Region == 1)
            {
                GDPRTextBlock.Text = "We value your privacy and are committed to protecting your personal data in accordance with the General Data Protection Regulation (GDPR). This app locally stores and processes minimal data necessary for its functionality, such as user login and vehicle information, to provide core services.\r\nYou have the right to access, modify, or delete your data at any time. For more details on how we process your data, or to exercise your rights, please refer to our privacy policy or contact us via email.\r\nBy using the app, you agree to the processing of your data in our privacy policy.";
            }
        }
    }
}
