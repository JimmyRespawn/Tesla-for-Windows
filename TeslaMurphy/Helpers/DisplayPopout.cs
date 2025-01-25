using System;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

namespace TeslaMurphy.Helpers
{
    public class DisplayPopout
    {
        public static async Task<bool> dualButton(string title, string content, string primaryButtonText, string secondaryButtonText)
        {
            try
            {
                ContentDialog deleteFileDialog = new ContentDialog
                {
                    Title = title,
                    Content = content,
                    PrimaryButtonText = primaryButtonText,
                    SecondaryButtonText = secondaryButtonText
                };

                ContentDialogResult result = await deleteFileDialog.ShowAsync();

                // Delete the file if the user clicked the primary button.
                /// Otherwise, do nothing.
                if (result == ContentDialogResult.Primary)
                {
                    return true;
                }
                else
                {
                    // The user clicked the CLoseButton, pressed ESC, Gamepad B, or the system back button.
                    // Do nothing.
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
