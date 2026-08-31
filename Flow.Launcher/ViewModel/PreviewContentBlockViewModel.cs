using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.ViewModel;

public enum PreviewContentLoadState
{
    NotLoaded,
    Loading,
    Ready,
    Failed
}

public sealed class PreviewContentBlockViewModel : BaseModel
{
    private bool _loadStarted;
    private object _renderedContent;
    private PreviewContentLoadState _loadState;

    public PreviewContentBlockViewModel(PreviewContentBlock inputBlock)
    {
        InputBlock = inputBlock;
        _loadState = RequiresFileLoad ? PreviewContentLoadState.NotLoaded : PreviewContentLoadState.Ready;
        _renderedContent = GetInlineContent(inputBlock);
    }

    public PreviewContentBlock InputBlock { get; }

    public object RenderedContent
    {
        get => _renderedContent;
        private set
        {
            _renderedContent = value;
            OnPropertyChanged();
        }
    }

    public PreviewContentLoadState LoadState
    {
        get => _loadState;
        private set
        {
            _loadState = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsContentVisible));
            OnPropertyChanged(nameof(IsLoading));
            OnPropertyChanged(nameof(IsErrorVisible));
        }
    }

    public bool IsContentVisible => LoadState == PreviewContentLoadState.Ready;

    public bool IsLoading => LoadState == PreviewContentLoadState.Loading;

    public bool IsErrorVisible => LoadState == PreviewContentLoadState.Failed;

    public bool RequiresFileLoad => InputBlock switch
    {
        MarkdownPreviewBlock markdown => string.IsNullOrEmpty(markdown.InlineMarkdown) && !string.IsNullOrEmpty(markdown.FilePath),
        TextPreviewBlock text => string.IsNullOrEmpty(text.Text) && !string.IsNullOrEmpty(text.FilePath),
        _ => false
    };

    public async Task LoadAsync(string pluginDirectory, CancellationToken cancellationToken)
    {
        if (!RequiresFileLoad || _loadStarted)
        {
            return;
        }

        _loadStarted = true;
        LoadState = PreviewContentLoadState.Loading;

        try
        {
            var filePath = ResolveFilePath(pluginDirectory);
            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            RenderedContent = content;
            LoadState = PreviewContentLoadState.Ready;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _loadStarted = false;
            LoadState = PreviewContentLoadState.NotLoaded;
        }
        catch (Exception)
        {
            LoadState = PreviewContentLoadState.Failed;
        }
    }

    private string ResolveFilePath(string pluginDirectory)
    {
        var filePath = InputBlock switch
        {
            MarkdownPreviewBlock markdown => markdown.FilePath,
            TextPreviewBlock text => text.FilePath,
            _ => string.Empty
        };

        return Path.IsPathRooted(filePath)
            ? filePath
            : Path.Combine(pluginDirectory ?? string.Empty, filePath);
    }

    private static object GetInlineContent(PreviewContentBlock block)
    {
        return block switch
        {
            MarkdownPreviewBlock markdown => markdown.InlineMarkdown,
            TextPreviewBlock text => text.Text,
            _ => null
        };
    }
}