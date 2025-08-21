using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuroraMusic.Models
{
    public class AppSettings
    {
        // We use a fixed ID to treat our settings as a single row in the database.
        public int Id { get; set; } = 1;

        
        public double Volume { get; set; } = 100;
        public int RepeatMode { get; set; } = 0; // Storing the RepeatMode enum as an integer
        public bool IsFirstLaunch { get; set; } = true;
        public string DatabaseVersion { get; set; } = "0.0.0";
        public int SortMode { get; set; } = (int)Models.SortMode.ArtistAlbum;
        public bool IsPaneFixed { get; set; } = false;
        public bool IsPaneOpen { get; set; } = false;
    }
}