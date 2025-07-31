using Avalonia.Media.Imaging;

namespace AuroraMusic.Models
{
    public class PlaylistItem
    {
        public string Title { get; set; } = "Unknown Title";
        public string Artist { get; set; } = "Unknown Artist";
        public string FilePath { get; set; } = "";

        // New property to hold the loaded album art image.
        public Bitmap? AlbumArt { get; set; }

        public override string ToString() => $"{Artist} - {Title}";
    }
}