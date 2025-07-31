using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuroraMusic.Models
{
    public class Album
    {
        public int Id { get; set; }

        [Required]
        public string? Title { get; set; }

        public int ArtistId { get; set; }
        public Artist? Artist { get; set; }

        // We will store the raw image data for the album art in the database.
        public byte[]? AlbumArt { get; set; }

        public ICollection<Song> Songs { get; set; } = new List<Song>();
    }
}