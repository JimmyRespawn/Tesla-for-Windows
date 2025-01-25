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

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“内容对话框”项模板

namespace TeslaMurphy.Controls
{
    public sealed partial class PurchaseProContentDialog : ContentDialog
    {
        public PurchaseProContentDialog()
        {
            this.InitializeComponent();
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            TeslaMurphy.Helpers.UWPGeneralHelper.OpenInDefaultBrowser("ms-windows-store://pdp?ocid=storeweb-pdp-open-cta&hl=en-us&gl=us&referrer=appupdate&productid=9NL2Q935957M");
        }
    }
}
