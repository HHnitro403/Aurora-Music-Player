using AuroraMusic.Views;
using AuroraMusic.Views.Modals;
using Avalonia.Controls;
using Avalonia.Threading;
using System;

namespace AuroraMusic.Services
{
    public class UIService
    {
        private readonly Window _mainWindow;
        private readonly ContentControl _mainContentArea;
        private readonly TracksView _tracksView;
        private UpdatePopupView? _currentPopup;

        public UIService(Window mainWindow, TracksView tracksView)
        {
            _mainWindow = mainWindow;
            _tracksView = tracksView;
            _mainContentArea = _mainWindow.FindControl<ContentControl>("MainContentArea") ?? throw new Exception("MainContentArea not found");
        }

        public void ShowPopup(string message, bool showOkButton)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                _currentPopup = UpdatePopupView.Show(_mainWindow, message, showOkButton);
            });
        }

        public void ClosePopup()
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                _currentPopup?.Close();
                _currentPopup = null;
            });
        }

        public void ShowMainContent()
        {
            _mainContentArea.Content = _tracksView;
        }

        public void ShowView(Control view)
        {
            _mainContentArea.Content = view;
        }
    }
}