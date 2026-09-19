using System;
using System.Globalization;
using Newtonsoft.Json.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TeslaMurphy.Controls
{
    public sealed partial class ServiceContentDialog : ContentDialog
    {
        public ServiceContentDialog(string json, bool inService, string vehicleName)
        {
            InitializeComponent();
            VehicleLabel.Text = string.IsNullOrWhiteSpace(vehicleName) ? "Your vehicle" : vehicleName;
            StatusTitle.Text = inService ? "In service" : "Not in service";
            StatusDescription.Text = inService ? "Your vehicle is currently in service." : "Your vehicle is not currently marked as in service.";
            StatusIcon.Glyph = inService ? "\uE90F" : "\uE73E";
            if (!inService) return;
            VisitDetails.Visibility = Visibility.Visible;
            try
            {
                var response = JObject.Parse(json ?? "{}")["response"] as JObject;
                if (response == null) throw new InvalidOperationException();
                string status = (string)response["service_status"];
                if (!string.IsNullOrWhiteSpace(status) && status != "in_service")
                    StatusTitle.Text = status.Replace('_', ' ');
                VisitNumber.Text = string.IsNullOrWhiteSpace((string)response["service_visit_number"]) ? "Not provided" : (string)response["service_visit_number"];
                DateTimeOffset completion;
                if (DateTimeOffset.TryParse((string)response["service_etc"], CultureInfo.InvariantCulture, DateTimeStyles.None, out completion))
                {
                    CompletionTime.Text = completion.ToLocalTime().ToString("MMM d, yyyy\nHH:mm", CultureInfo.GetCultureInfo("en-US"));
                    TimeNote.Visibility = Visibility.Visible;
                }
            }
            catch
            {
                StatusDescription.Text = "Your vehicle is marked as in service, but visit details could not be loaded. Please try again later.";
            }
        }
    }
}
