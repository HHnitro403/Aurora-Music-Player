using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuroraMusic.Models
{
    public class PlaylistItem
    {
        public string Title { get; set; } = "Unknown Title";
        public string Artist { get; set; } = "Unknown Artist";
        public string FilePath { get; set; } = "";

        public override string ToString() => $"{Artist} - {Title}";
    }
}