using System;
using System.Collections.Generic;
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

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“内容对话框”项模板

namespace TeslaMurphy.Controls
{
    public sealed partial class ClimateContentDialog : ContentDialog
    {
        public ClimateState climateStateData { get; set; }
        public ClimateContentDialog()
        {
            this.InitializeComponent();
        }

        private void ContentDialog_Loaded(object sender, RoutedEventArgs e)
        {
            if(climateStateData != null)
            {
                InteriorTemperatureTextBlock.Text = climateStateData.inside_temp.ToString() + "°";
                ExteriorTemperatureTextBlock.Text = climateStateData.outside_temp.ToString() + "°";
                ACModeSwitch.IsOn = climateStateData.is_climate_on;
                AutoACModeSwitch.IsOn = climateStateData.is_auto_conditioning_on;
            }
        }
    }
}
