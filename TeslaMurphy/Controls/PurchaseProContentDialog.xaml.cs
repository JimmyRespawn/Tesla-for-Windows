using Windows.UI.Xaml.Controls;

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
