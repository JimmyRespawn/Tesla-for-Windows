using System.Collections.Generic;

namespace TeslaMurphy.Models
{
    public class ReleaseNotesModel
    {
        public ReleaseNotes response { get; set; }
    }

    public class ReleaseNotes
    {
        public List<ReleaseNote> release_notes { get; set; }
    }

    public class ReleaseNote
    {
        public string title { get; set; }
        public string customer_version { get; set; }
        public string image_url { get; set; }
        public string light_image_url { get; set; }
        public string description { get; set; }
    }
}
