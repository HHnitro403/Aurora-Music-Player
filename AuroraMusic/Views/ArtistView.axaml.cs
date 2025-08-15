using AuroraMusic.Services;
using Avalonia.Controls;
using System.Threading.Tasks;
using AuroraMusic.ViewModels;

namespace AuroraMusic.Views
{
    public partial class ArtistView : UserControl
    {
        private DatabaseService? _dbService;
        private ArtistViewModel? _viewModel;

        public ArtistView()
        {
            InitializeComponent();
            _dbService = AppServices.GetService<DatabaseService>();
            _viewModel = new ArtistViewModel();
            DataContext = _viewModel;
        }

        public ArtistView(DatabaseService dbService) : this()
        {
            _dbService = dbService;
        }

        public async Task LoadArtistsAsync()
        {
            var artists = await _dbService!.GetArtistsAsync();
            _viewModel!.Artists.Clear();
            foreach (var artist in artists)
            {
                _viewModel.Artists.Add(artist);
            }
        }
    }
}
