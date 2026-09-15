using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TeslaMurphy.Models;
using TeslaMurphy.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TeslaMurphy.Controls
{
    public sealed partial class DriversContentDialog : ContentDialog
    {
        public string VehicleVin { get; set; }
        public string VehicleName { get; set; }
        public bool MembershipChanged { get; private set; }
        private DriverManagementService service;
        private bool busy, owner;
        private int nextPage;
        private readonly System.Collections.Generic.HashSet<string> invitationIds = new System.Collections.Generic.HashSet<string>();
        private Func<Task> pending;
        public DriversContentDialog() { InitializeComponent(); }

        private async void ContentDialog_Loaded(object sender, RoutedEventArgs e)
        {
            VehicleLabel.Text = (VehicleName ?? "Vehicle") + " · " + VehicleVin;
            service = new DriverManagementService(VehicleVin);
            if (AppSettings.Instance.IsTestMode)
            {
                Actions.IsEnabled = false;
                Status.Text = "Driver management is unavailable in demo mode.";
                return;
            }
            await Run(RefreshAll);
        }
        private void Dialog_Closing(ContentDialog sender, ContentDialogClosingEventArgs args) { args.Cancel = busy; }
        private async Task Run(Func<Task> action)
        {
            if (busy) return;
            busy = true;
            Actions.IsEnabled = Confirmation.IsEnabled = false;
            IsSecondaryButtonEnabled = false;
            Progress.IsActive = true;
            Status.Text = "";
            try { await action(); }
            catch (Exception ex) { Status.Text = ex.Message; }
            finally
            {
                busy = false;
                Actions.IsEnabled = pending == null;
                Confirmation.IsEnabled = true;
                IsSecondaryButtonEnabled = true;
                Progress.IsActive = false;
            }
        }
        private async Task RefreshAll()
        {
            owner = false;
            CreateButton.IsEnabled = false;
            DriverRows.Children.Clear();
            DriverStatus.Text = "Loading drivers…";
            try
            {
                var json = await service.Drivers();
                var drivers = json["response"] as JArray;
                if (drivers == null) throw new InvalidOperationException("Unexpected driver list.");
                owner = true; // Tesla documents this endpoint as owner-only.
                CreateButton.IsEnabled = !string.IsNullOrWhiteSpace(VehicleVin);
                foreach (var driver in drivers.OfType<JObject>())
                {
                    string id = (string)driver["user_id_s"] ?? (string)driver["user_id"];
                    string name = ((string)driver["driver_first_name"] + " " + (string)driver["driver_last_name"]).Trim();
                    if (name.Length == 0) name = "Driver";
                    var row = Row(name, "Remove", () => Ask(
                        "Remove " + name + " from " + VehicleLabel.Text + "? Their shared vehicle access will end.",
                        async () => { await service.Remove(id); MembershipChanged = true; await RefreshAll(); Status.Text = "Driver access removed."; }));
                    ((Button)row.Children[1]).IsEnabled = !string.IsNullOrWhiteSpace(id);
                    DriverRows.Children.Add(row);
                }
                DriverStatus.Text = drivers.Count == 0 ? "No shared drivers." : "";
            }
            catch (Exception ex) { DriverStatus.Text = ex.Message; }
            InviteRows.Children.Clear();
            invitationIds.Clear();
            nextPage = 1;
            await LoadInvitations();
        }
        private async Task LoadInvitations()
        {
            MoreButton.Visibility = Visibility.Collapsed;
            try
            {
                var json = await service.Invitations(nextPage);
                var invites = json["response"] as JArray;
                if (invites == null) throw new InvalidOperationException("Unexpected invitation list.");
                foreach (var invite in invites.OfType<JObject>()) AddInvitation(invite);
                int current = nextPage;
                var next = json["pagination"]?["next"];
                nextPage = next != null && next.Type == JTokenType.Integer ? (int)next : 0;
                // Do not loop on a malformed or repeated pagination cursor.
                if (nextPage <= current) nextPage = 0;
                MoreButton.Visibility = nextPage > 0 ? Visibility.Visible : Visibility.Collapsed;
                InviteStatus.Text = InviteRows.Children.Count == 0 ? "No active invitations." : "";
            }
            catch (Exception ex) { InviteStatus.Text = ex.Message; }
        }
        private void AddInvitation(JObject invite)
        {
            string id = (string)invite["id_s"] ?? (string)invite["id"];
            if (!string.IsNullOrEmpty(id) && !invitationIds.Add(id)) return;
            string link = (string)invite["share_link"];
            string state = (string)invite["state"] ?? "Unknown";
            DateTimeOffset expires;
            bool hasExpiry = DateTimeOffset.TryParse((string)invite["expires_at"], out expires);
            bool active = state == "pending" && (!hasExpiry || expires > DateTimeOffset.UtcNow)
                && (invite["revoked_at"] == null || invite["revoked_at"].Type == JTokenType.Null);
            var panel = new StackPanel { Spacing = 6 };
            panel.Children.Add(new TextBlock { Text = state + (hasExpiry ? " · Expires " + expires.ToLocalTime().ToString("g") : ""), TextWrapping = TextWrapping.Wrap });
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var copy = new Button { Content = "Copy link", IsEnabled = active && IsTeslaLink(link) };
            copy.Click += (s, e) => {
                try { var data = new DataPackage(); data.SetText(link); Clipboard.SetContent(data); Status.Text = "Invitation link copied."; }
                catch { Status.Text = "Unable to copy the link."; }
            };
            buttons.Children.Add(copy);
            var revoke = new Button { Content = "Revoke", IsEnabled = owner && active && !string.IsNullOrWhiteSpace(id) };
            revoke.Click += (s, e) => Ask("Revoke invitation " + id + " for " + VehicleLabel.Text + "? This link will stop working.",
                async () => { await service.Revoke(id); await RefreshAll(); Status.Text = "Invitation revoked."; });
            buttons.Children.Add(revoke);
            panel.Children.Add(buttons);
            InviteRows.Children.Add(panel);
        }
        private static bool IsTeslaLink(string link)
        {
            if (string.IsNullOrWhiteSpace(link) || !link.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return false;
            try { DriverManagementService.InvitationCode(link); return true; } catch { return false; }
        }
        private static Grid Row(string label, string action, Action click)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) });
            var button = new Button { Content = action };
            button.Click += (s, e) => click();
            Grid.SetColumn(button, 1); grid.Children.Add(button);
            return grid;
        }
        private void Ask(string message, Func<Task> action)
        {
            if (busy || pending != null) return;
            pending = action; ConfirmText.Text = message;
            Confirmation.Visibility = Visibility.Visible; Actions.IsEnabled = false;
        }
        private async void Confirm_Click(object sender, RoutedEventArgs e)
        {
            var action = pending;
            if (action == null || busy) return;
            pending = null; Confirmation.Visibility = Visibility.Collapsed;
            await Run(action);
        }
        private void Cancel_Click(object sender, RoutedEventArgs e)
        { pending = null; Confirmation.Visibility = Visibility.Collapsed; Actions.IsEnabled = true; }
        private async void Refresh_Click(object sender, RoutedEventArgs e) { await Run(RefreshAll); }
        private async void More_Click(object sender, RoutedEventArgs e) { if (nextPage > 0) await Run(LoadInvitations); }
        private void Create_Click(object sender, RoutedEventArgs e)
        {
            if (!owner) return;
            Ask("Create a driver invitation for " + VehicleLabel.Text + "? Anyone you give the link to can accept vehicle access.",
                async () => {
                    var json = await service.Create();
                    var invite = json["response"] as JObject;
                    if (invite == null) throw new InvalidOperationException("Unexpected invitation response. Refresh before creating another.");
                    await RefreshAll();
                    // Keep the created link accessible even if the subsequent list refresh fails.
                    AddInvitation(invite);
                    Status.Text = "Invitation created. Use Copy link to share it with the intended driver.";
                });
        }
        private void Redeem_Click(object sender, RoutedEventArgs e)
        {
            string code;
            try { code = DriverManagementService.InvitationCode(InviteInput.Text); }
            catch (Exception ex) { Status.Text = ex.Message; return; }
            Ask("Accept this invitation using the currently signed-in Tesla account? The invited vehicle may differ from the vehicle shown above.",
                async () => {
                    var json = await service.Redeem(code);
                    string vin = (string)json["response"]?["vin"];
                    if (string.IsNullOrWhiteSpace(vin)) throw new InvalidOperationException("Unexpected acceptance response. Refresh your vehicles.");
                    MembershipChanged = true; InviteInput.Text = "";
                    Status.Text = "Invitation accepted. Close this dialog and open the vehicle selector to refresh your vehicles.";
                });
        }
    }
}
