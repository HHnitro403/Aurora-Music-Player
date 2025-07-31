using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuroraMusic.Models
{
    public class Artist
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        public ICollection<Album> Albums { get; set; } = new List<Album>();
    }
}