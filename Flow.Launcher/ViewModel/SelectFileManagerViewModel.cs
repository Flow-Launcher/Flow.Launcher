using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using ModernWpf.Controls;

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
    private async Task OpenFilesTipsAsync(Button button)
    {
        var tipText = App.API.GetTranslation("fileManager_files_tips");
        var url = "https://files.community/docs/contributing/updates";

        var textBlock = new TextBlock
        {
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 0)
        };

        textBlock.Inlines.Add(tipText);

        var hyperlink = new Hyperlink
        {
            NavigateUri = new Uri(url)
        };
        hyperlink.Inlines.Add(url);
        hyperlink.RequestNavigate += (s, args) =>
        {
            App.API.OpenUrl(args.Uri.AbsoluteUri);
            args.Handled = true;
        };

        textBlock.Inlines.Add(hyperlink);

        var tipsDialog = new ContentDialog()
        {
            Owner = Window.GetWindow(button),
            Title = (string)Application.Current.Resources["fileManager_files_btn"],
            Content = textBlock,
            PrimaryButtonText = (string)Application.Current.Resources["commonOK"],
            CornerRadius = new CornerRadius(8),
            Style = (Style)Application.Current.Resources["ContentDialog"]
        };

        await tipsDialog.ShowAsync();
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
        CustomExplorers.RemoveAt(SelectedCustomExplorerIndex--);
    }
}
