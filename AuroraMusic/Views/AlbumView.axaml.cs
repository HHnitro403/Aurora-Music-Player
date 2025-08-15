using AuroraMusic.Services;
using Avalonia.Controls;
using System.Threading.Tasks;
using AuroraMusic.ViewModels;

namespace AuroraMusic.Views
{
    public partial class AlbumView : UserControl
    {
        private DatabaseService? _dbService;
        private AlbumViewModel? _viewModel;

        public AlbumView()
        {
            InitializeComponent();
            _dbService = AppServices.GetService<DatabaseService>();
            _viewModel = new AlbumViewModel();
            DataContext = _viewModel;
        }

        public AlbumView(DatabaseService dbService) : this()
        {
            _dbService = dbService;
        }

        public async Task LoadAlbumsAsync()
        {
            var albums = await _dbService!.GetAlbumsAsync();
            _viewModel!.Albums.Clear();
            foreach (var album in albums)
            {
                _viewModel.Albums.Add(album);
            }
        }
    }
}
