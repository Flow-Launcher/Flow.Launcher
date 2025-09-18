using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.ViewModel;

public partial class SelectFileManagerViewModel : BaseModel
{
    private readonly Settings _settings;

    private int selectedCustomExplorerIndex;

    public int SelectedCustomExplorerIndex
    {
        get => selectedCustomExplorerIndex;
        set
        {
            // When one custom file manager is selected and removed, the index will become -1, so we need to ignore this change
            if (value < 0) return;
            if (selectedCustomExplorerIndex != value)
            {
                selectedCustomExplorerIndex = value;
                OnPropertyChanged(nameof(CustomExplorer));
            }
        }
    }

    public ObservableCollection<CustomExplorerViewModel> CustomExplorers { get; }

    public CustomExplorerViewModel CustomExplorer => CustomExplorers[SelectedCustomExplorerIndex];

    public SelectFileManagerViewModel(Settings settings)
    {
        _settings = settings;
        CustomExplorers = new ObservableCollection<CustomExplorerViewModel>(_settings.CustomExplorerList.Select(x => x.Copy()));
        SelectedCustomExplorerIndex = _settings.CustomExplorerIndex;
    }

    public bool SaveSettings()
    {
        // Check if the selected file manager path is valid
        if (!IsFileManagerValid(CustomExplorer.Path))
        {
            var result = App.API.ShowMsgBox(
                string.Format(App.API.GetTranslation("fileManagerPathNotFound"),
                    CustomExplorer.Name, CustomExplorer.Path),
                    App.API.GetTranslation("fileManagerPathError"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.No)
            {
                return false;
            }
        }

        _settings.CustomExplorerList = CustomExplorers.ToList();
        _settings.CustomExplorerIndex = SelectedCustomExplorerIndex;
        return true;
    }

    private static bool IsFileManagerValid(string path)
    {
        if (string.Equals(path, "explorer", StringComparison.OrdinalIgnoreCase))
            return true;

        if (Path.IsPathRooted(path))
        {
            return File.Exists(path);
        }

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = path,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return !string.IsNullOrEmpty(output);
        }
        catch
        {
            return false;
        }
    }

    [RelayCommand]
    private void Add()
    {
        CustomExplorers.Add(new()
        {
            Name = "New Profile"
        });
        SelectedCustomExplorerIndex = CustomExplorers.Count - 1;
    }

    [RelayCommand]
    private void Delete()
    {
        var currentIndex = SelectedCustomExplorerIndex;
        if (currentIndex >= 0 && currentIndex < CustomExplorers.Count)
        {
            CustomExplorers.RemoveAt(currentIndex);
            SelectedCustomExplorerIndex = currentIndex > 0 ? currentIndex - 1 : 0;
        }
    }
}
