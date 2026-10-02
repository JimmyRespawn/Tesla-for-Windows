using System;
using Microsoft.UI.Xaml.Controls;

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
            var productId = TeslaMurphy.Services.StoreConfiguration.AppStoreProductId;
            if (!string.IsNullOrWhiteSpace(productId))
                TeslaMurphy.Helpers.UWPGeneralHelper.OpenInDefaultBrowser("ms-windows-store://pdp?productid=" + Uri.EscapeDataString(productId));
        }
    }
}
