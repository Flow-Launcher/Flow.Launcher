using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure;
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
    private static readonly string ClassName = nameof(PreviewContentBlockViewModel);
    private const long MaxPreviewFileSizeBytes = 1024 * 1024;
    // 8 KiB chunks avoid many read calls while keeping the per-load allocation modest.
    private const int FileReadBufferSize = 8192;
    private readonly Func<string, CancellationToken, Task<string>> _readFileAsync;
    private int _loadGeneration;
    private object _renderedContent;
    private string _loadErrorMessage;
    private PreviewContentLoadState _loadState;

    public PreviewContentBlockViewModel(PreviewContentBlock inputBlock) : this(inputBlock, ReadPreviewFileAsync)
    {
    }

    internal PreviewContentBlockViewModel(PreviewContentBlock inputBlock, Func<string, CancellationToken, Task<string>> readFileAsync)
    {
        InputBlock = inputBlock;
        _readFileAsync = readFileAsync;
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

    public string LoadErrorMessage
    {
        get => _loadErrorMessage;
        private set
        {
            _loadErrorMessage = value;
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
        // Ready and Failed are terminal: loaded content is cached and a failed load is not retried.
        if (!RequiresFileLoad || LoadState is PreviewContentLoadState.Ready or PreviewContentLoadState.Failed)
        {
            return;
        }

        // A newer call supersedes any earlier attempt that has not finished unwinding yet.
        // The generation marks the newest attempt, so a superseded one backs off when it resumes.
        var generation = ++_loadGeneration;
        LoadState = PreviewContentLoadState.Loading;

        string content = string.Empty;
        string errorMessage = string.Empty;
        string logMessage = string.Empty;
        var nextState = PreviewContentLoadState.NotLoaded;

        try
        {
            var filePath = ResolveFilePath(pluginDirectory);
            content = await _readFileAsync(filePath, cancellationToken);
            nextState = PreviewContentLoadState.Ready;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            nextState = PreviewContentLoadState.NotLoaded; // already the default but being explicit here
        }
        catch (PreviewFileTooLargeException e)
        {
            errorMessage = Localize.previewContentLoadErrorTooLarge();
            logMessage = e.Message;
            nextState = PreviewContentLoadState.Failed;
        }
        catch (Exception e)
        {
            errorMessage = Localize.previewContentLoadError();
            logMessage = $"Failed to load preview file: {e.Message}";
            nextState = PreviewContentLoadState.Failed;
        }

        // A superseded attempt must not update the state of the newer one.
        if (generation != _loadGeneration)
        {
            return;
        }

        if (nextState == PreviewContentLoadState.Ready)
        {
            RenderedContent = content;
        }
        else if (nextState == PreviewContentLoadState.Failed)
        {
            LoadErrorMessage = errorMessage;
            App.API.LogError(ClassName, logMessage);
        }

        LoadState = nextState;
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

    private static async Task<string> ReadPreviewFileAsync(string filePath, CancellationToken cancellationToken)
    {
        // The file can grow after being opened, so cap the bytes actually read.
        using var content = new MemoryStream();
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, FileReadBufferSize, FileOptions.Asynchronous);
        var buffer = new byte[FileReadBufferSize];

        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (content.Length + read > MaxPreviewFileSizeBytes)
            {
                throw new PreviewFileTooLargeException(filePath);
            }

            await content.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        // Decode the same way File.ReadAllTextAsync does, including byte order mark detection.
        content.Position = 0;
        using var reader = new StreamReader(content, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private sealed class PreviewFileTooLargeException : Exception
    {
        public PreviewFileTooLargeException(string filePath)
            : base($"Preview file '{filePath}' is over the {MaxPreviewFileSizeBytes} byte limit.")
        {
        }
    }
}