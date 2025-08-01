using Avalonia.Media.Imaging;
using System.IO;

namespace AuroraMusic.Models
{
    public class PlaylistItem
    {
        public int Id { get; set; }
        public int PlaylistId { get; set; }
        public Playlist Playlist { get; set; } = null!;
        public int SongId { get; set; }
        public Song Song { get; set; } = null!;

        // Properties from Song for direct access in UI/logic
        public string Title => Song?.Title ?? "Unknown Title";
        public string Artist => Song?.Album?.Artist?.Name ?? "Unknown Artist";
        public string Album => Song?.Album?.Title ?? "Unknown Album";
        public string FilePath => Song?.FilePath ?? string.Empty;
        public Bitmap? AlbumArt
        {
            get
            {
                if (Song?.Album?.AlbumArt != null)
                {
                    using var stream = new MemoryStream(Song.Album.AlbumArt);
                    return new Bitmap(stream);
                }
                return null;
            }
        }
    }
}