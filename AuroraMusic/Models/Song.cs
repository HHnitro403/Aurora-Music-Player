using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuroraMusic.Models
{
    public class Song
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; }

        public uint TrackNumber { get; set; }

        public int AlbumId { get; set; }
        public Album Album { get; set; }

        [Required]
        public string FilePath { get; set; }

        public TimeSpan Duration { get; set; }
    }
}