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
        private readonly Grid _popupOverlay;
        private readonly UpdatePopupView _updatePopup;
        private readonly TracksView _tracksView;

        public UIService(Window mainWindow, TracksView tracksView)
        {
            _mainWindow = mainWindow;
            _tracksView = tracksView;
            _mainContentArea = _mainWindow.FindControl<ContentControl>("MainContentArea") ?? throw new Exception("MainContentArea not found");
            _popupOverlay = _mainWindow.FindControl<Grid>("PopupOverlay") ?? throw new Exception("PopupOverlay not found");
            _updatePopup = _mainWindow.FindControl<UpdatePopupView>("UpdatePopup") ?? throw new Exception("UpdatePopup not found");

            _updatePopup.OkClicked += HidePopup;
        }

        public void ShowPopup(string message, bool showOkButton)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                _updatePopup.SetMessage(message, showOkButton);
                _popupOverlay.IsVisible = true;
            });
        }

        public void HidePopup()
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                _popupOverlay.IsVisible = false;
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