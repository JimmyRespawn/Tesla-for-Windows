using System.Collections.ObjectModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using TeslaMurphy.Models;
using Newtonsoft.Json;

namespace TeslaMurphy.Controls
{
    public sealed partial class ReleaseNotesContentDialog : ContentDialog
    {
        public string ReleaseNotesString { get; set; }
        public ObservableCollection<ReleaseNote> ReleaseNotesCollection;
        public ReleaseNotesContentDialog()
        {
            this.InitializeComponent();
        }
        private void ContentDialog_Loaded(object sender, RoutedEventArgs e)
        {
            LoadReleaseNotes();
        }

        private void LoadReleaseNotes()
        {
            if (!string.IsNullOrEmpty(ReleaseNotesString))
            {
                if(ReleaseNotesCollection == null)
                    ReleaseNotesCollection = new ObservableCollection<ReleaseNote>();
                var notes = JsonConvert.DeserializeObject<ReleaseNotesModel>(ReleaseNotesString);
                if(notes != null)
                {
                    var themeMode =  AppSettings.Instance.CurrentTheme.ToString();
                    if (themeMode =="Dark")
                        this.RequestedTheme = ElementTheme.Dark;
                    else
                        this.RequestedTheme = ElementTheme.Light;
                    foreach (var note in notes.response.release_notes)
                    {
                        if (themeMode == "Dark")
                            note.light_image_url = "1";
                        else
                            note.image_url = "1";
                        ReleaseNotesCollection.Add(note);
                    }
                }
                if(ReleaseNotesCollection.Count != 0)
                    ListNotesListView.ItemsSource = ReleaseNotesCollection;
            }
        }
    }
}
