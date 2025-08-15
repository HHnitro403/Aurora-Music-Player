using AuroraMusic.Services;
using AuroraMusic.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuroraMusic
{
    public static class AppServices
    {
        /// <summary>
        /// The configured service provider.
        /// </summary>
        public static IServiceProvider? Services { get; private set; }

        /// <summary>
        /// Configures the services for the application.
        /// </summary>
        public static void ConfigureServices()
        {
            var services = new ServiceCollection();

            // Register Services as Singletons (one instance for the entire app)
            services.AddSingleton<DatabaseService>();
            services.AddSingleton<PlaylistManager>();
            services.AddSingleton<PlaybackService>();
            services.AddSingleton<UIService>();

            // Register Views and MainWindow as Transient
            // A new view instance will be created upon navigation.
            services.AddTransient<MainWindow>();
            services.AddTransient<TracksView>();
            services.AddTransient<PlaylistsView>();
            services.AddTransient<SettingsView>();
            services.AddTransient<AlbumView>();
            services.AddTransient<ArtistView>();
            services.AddTransient<PlaylistView>();

            Services = services.BuildServiceProvider();
        }

        /// <summary>
        /// Gets a service of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of service to get.</typeparam>
        /// <returns>An instance of the requested service.</returns>
        public static T GetService<T>() where T : notnull
        {
            return Services!.GetRequiredService<T>();
        }
    }
}