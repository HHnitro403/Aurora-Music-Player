using AuroraMusic.Models;
using AuroraMusic.Services;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Microsoft.EntityFrameworkCore;
using MsBox.Avalonia.Enums;
using System;
using MsBox.Avalonia;
using System.Threading.Tasks;
using Avalonia.Styling;
using Avalonia;

namespace AuroraMusic.Views;

public partial class SettingsView : UserControl
{
    private AppSettings? _appSettings;
    private readonly DatabaseService _dbService;

    public event Action<bool>? FixedMenuSettingChanged;
    public event Action<string>? FolderSelected;
    public event Action? GoBackRequested;
    public event Action? FolderRemoved;

    public SettingsView()
    {
        InitializeComponent();
        _dbService = new DatabaseService();
        _ = LoadFoldersAsync();
    }

    public SettingsView(DatabaseService dbService) : this()
    {
        _dbService = dbService;
        _ = LoadFoldersAsync();
    }

    public void SetAppSettings(AppSettings appSettings)
        {
            _appSettings = appSettings;
            var fixedMenuToggle = this.FindControl<ToggleSwitch>("FixedMenuToggle");
            if (fixedMenuToggle != null)
            {
                fixedMenuToggle.IsChecked = _appSettings.IsPaneFixed;
            }
        }

    private void ThemeToggle_Toggled(object? sender, RoutedEventArgs e)
    {
        if (Application.Current != null && sender is ToggleSwitch toggleSwitch)
        {
            Application.Current.RequestedThemeVariant = toggleSwitch.IsChecked == true ? ThemeVariant.Dark : ThemeVariant.Light;
        }
    }

    private async void FixedMenuToggle_Toggled(object? sender, RoutedEventArgs e)
        {
            if (_appSettings == null || _dbService == null) return;

            if (sender is ToggleSwitch toggleSwitch)
            {
                _appSettings.IsPaneFixed = toggleSwitch.IsChecked == true;
                await _dbService.SaveSettingsAsync(_appSettings);
                FixedMenuSettingChanged?.Invoke(_appSettings.IsPaneFixed);
            }
        }

    private async Task LoadFoldersAsync()
    {
        FoldersListBox.ItemsSource = await _dbService.GetAllFoldersAsync();
    }

    private async void SelectFolderButton_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Music Folder",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            string folderPath = folders[0].Path.LocalPath;
            await _dbService.AddFolderAsync(folderPath);
            await LoadFoldersAsync();
            FolderSelected?.Invoke(folderPath);
        }
    }

    private async void DeleteFolderButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: Folder folder })
        {
            if (folder.Path != null)
            {
                // This check remains good practice.
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null) return;

                var MsBox = MessageBoxManager.GetMessageBoxStandard("Confirmation",
                  $"Are you sure you want to delete the folder '{folder.Path}' and all its associated songs?",
                  ButtonEnum.OkCancel,
                  Icon.Warning);

                // The line below is the only part that changes.
                var result = await MsBox.ShowAsync();

                if (result == ButtonResult.Ok)
                {
                    await _dbService.RemoveFolderAsync(folder.Path);
                    await LoadFoldersAsync();
                    FolderRemoved?.Invoke();
                }
            }
        }
    }

    private void BackButton_Click(object? sender, RoutedEventArgs e)
    {
        GoBackRequested?.Invoke();
    }
}