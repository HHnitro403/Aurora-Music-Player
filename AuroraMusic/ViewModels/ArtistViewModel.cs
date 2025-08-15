using AuroraMusic.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AuroraMusic.ViewModels
{
    public class ArtistViewModel : ViewModelBase
    {
        public ObservableCollection<Artist> Artists { get; set; }

        public ArtistViewModel()
        {
            Artists = new ObservableCollection<Artist>();
        }
    }
}