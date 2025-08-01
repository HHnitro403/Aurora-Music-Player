using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System;
using System.Threading.Tasks;
using Serilog;

namespace AuroraMusic
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();
                AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
                {
                    // Log the exception
                    Log.Error((Exception)args.ExceptionObject, "Unhandled exception");
                    // Optionally, show a user-friendly message
                    // This part will be handled by MainWindow's centralized error handling
                };

                TaskScheduler.UnobservedTaskException += (sender, args) =>
                {
                    // Log the exception
                    Log.Error(args.Exception, "Unobserved task exception");
                    // Optionally, show a user-friendly message
                    // This part will be handled by MainWindow's centralized error handling
                    args.SetObserved(); // Mark the exception as observed to prevent the process from terminating
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}