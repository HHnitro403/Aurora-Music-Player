using AuroraMusic.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AuroraMusic.ViewModels
{
    public class AlbumViewModel : ViewModelBase
    {
        public ObservableCollection<Album> Albums { get; set; }

        public AlbumViewModel()
        {
            Albums = new ObservableCollection<Album>();
        }
    }
}