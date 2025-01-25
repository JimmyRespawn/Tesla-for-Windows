using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TeslaMurphy.Models;
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
    public sealed partial class DriversContentDialog : ContentDialog
    {
        public string DriversJsonString { get; set; }
        public ObservableCollection<Driver> DriversCollection;
        public DriversContentDialog()
        {
            this.InitializeComponent();
        }

        private void ContentDialog_Loaded(object sender, RoutedEventArgs e)
        {
            LoadDrivers();
        }

        private void LoadDrivers()
        {
            if (!string.IsNullOrEmpty(DriversJsonString))
            {
                if (DriversCollection == null)
                    DriversCollection = new ObservableCollection<Driver>();
                var drivers = JsonConvert.DeserializeObject<DriversResponse>(DriversJsonString);
                if (drivers != null)
                {
                    var themeMode = AppSettings.Instance.CurrentTheme.ToString();
                    if (themeMode == "Dark")
                        this.RequestedTheme = ElementTheme.Dark;
                    else
                        this.RequestedTheme = ElementTheme.Light;
                    foreach (var driver in drivers.response)
                        DriversCollection.Add(driver);
                }
                if (DriversCollection.Count != 0)
                    DriversListView.ItemsSource = DriversCollection;
                else
                    NullPlaceHolderPanel.Visibility = Visibility.Visible;
            }
        }
    }
}
