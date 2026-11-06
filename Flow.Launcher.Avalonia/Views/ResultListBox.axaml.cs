using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Flow.Launcher.Avalonia.ViewModel;
using Flow.Launcher.Infrastructure.Logger;

namespace Flow.Launcher.Avalonia.Views;

/// <summary>
/// Result list (results, context menu and history views). Mouse behaviour follows the WPF ResultListBox:
/// hover selects, left click opens, right click toggles the context menu and dragging a file result drops the file.
/// </summary>
public partial class ResultListBox : UserControl
{
    private static readonly string ClassName = nameof(ResultListBox);

    /// <summary>
    /// 0-based index of a result container, kept up to date for the open-result hotkey badge (HOTKEY+1 ... HOTKEY+0).
    /// </summary>
    public static readonly AttachedProperty<int> ItemIndexProperty =
        AvaloniaProperty.RegisterAttached<ResultListBox, Control, int>("ItemIndex", -1);

    public static int GetItemIndex(Control container) => container.GetValue(ItemIndexProperty);

    public static void SetItemIndex(Control container, int value) => container.SetValue(ItemIndexProperty, value);

    /// <summary>Hover selection is ignored this long after the selection moved by keyboard or a result update.</summary>
    private const long HoverSuppressMilliseconds = 250;

    /// <summary>Pointer travel (DIP) that turns a press on a file result into a drag.</summary>
    private const double DragThreshold = 4;

    private readonly ListBox _listBox;

    private bool _selectingFromPointer;
    private long _lastNonPointerSelectionTick;
    private Point? _lastPointerPosition;

    // Press state for click / drag handling
    private ResultViewModel? _pressedItem;
    private PointerPressedEventArgs? _pressedArgs;
    private Point _pressedPosition;
    private Task<IStorageItem?>? _dragItemTask;
    private string? _dragTrimmedQuery;

    public ResultListBox()
    {
        InitializeComponent();
        _listBox = this.FindControl<ListBox>("ResultsList")!;

        _listBox.AddHandler(PointerPressedEvent, OnItemPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        _listBox.AddHandler(PointerReleasedEvent, OnItemPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        _listBox.AddHandler(PointerMovedEvent, OnItemPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        _listBox.SelectionChanged += OnSelectionChanged;

        _listBox.ContainerPrepared += (_, e) => SetItemIndex(e.Container, e.Index);
        _listBox.ContainerIndexChanged += (_, e) => SetItemIndex(e.Container, e.NewIndex);
        _listBox.ContainerClearing += (_, e) => e.Container.ClearValue(ItemIndexProperty);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private MainViewModel? MainViewModel => TopLevel.GetTopLevel(this)?.DataContext as MainViewModel;

    private static ResultViewModel? ResultFromSource(object? source) =>
        (source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext as ResultViewModel;

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_selectingFromPointer)
            _lastNonPointerSelectionTick = Environment.TickCount64;
    }

    private void SelectFromPointer(ResultViewModel item)
    {
        if (ReferenceEquals(_listBox.SelectedItem, item))
            return;

        _selectingFromPointer = true;
        try
        {
            _listBox.SelectedItem = item;
        }
        finally
        {
            _selectingFromPointer = false;
        }
    }

    private void OnItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var item = ResultFromSource(e.Source);
        if (item == null)
            return;

        var properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsRightButtonPressed)
        {
            ResetPressState();
            MainViewModel?.ShowContextMenuFor(item);
            e.Handled = true;
            return;
        }

        if (!properties.IsLeftButtonPressed)
            return;

        SelectFromPointer(item);
        _pressedItem = item;
        _pressedArgs = e;
        _pressedPosition = e.GetPosition(this);
        PrepareDrag(item);

        // Handled so the window border doesn't start a window move drag from a result row.
        e.Handled = true;
    }

    private void OnItemPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left || _pressedItem == null)
            return;

        var pressedItem = _pressedItem;
        ResetPressState();

        if (!ReferenceEquals(ResultFromSource(e.Source), pressedItem))
            return;

        e.Handled = true;
        if (MainViewModel is { } mainViewModel)
            _ = mainViewModel.OpenResultWithModifiersAsync(pressedItem, e.KeyModifiers);
    }

    private void OnItemPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pressedItem != null)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                TryStartDrag(e);
            else
                ResetPressState();
            return;
        }

        // Hover selection only follows real pointer movement: results scrolling under a resting pointer or
        // recent arrow-key navigation must not steal the selection.
        var position = e.GetPosition(TopLevel.GetTopLevel(this));
        var moved = _lastPointerPosition is { } last && last != position;
        _lastPointerPosition = position;
        if (!moved || Environment.TickCount64 - _lastNonPointerSelectionTick < HoverSuppressMilliseconds)
            return;

        if (ResultFromSource(e.Source) is { } item)
            SelectFromPointer(item);
    }

    private void PrepareDrag(ResultViewModel item)
    {
        _dragItemTask = null;
        _dragTrimmedQuery = null;

        var path = item.PluginResult?.CopyText;
        if (string.IsNullOrEmpty(path) || (!File.Exists(path) && !Directory.Exists(path)))
            return;

        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider == null)
            return;

        _dragTrimmedQuery = item.PluginResult?.OriginQuery?.TrimmedQuery;
        _dragItemTask = ResolveStorageItemAsync(storageProvider, path);
    }

    private static async Task<IStorageItem?> ResolveStorageItemAsync(IStorageProvider storageProvider, string path)
    {
        try
        {
            if (Directory.Exists(path))
                return await storageProvider.TryGetFolderFromPathAsync(path);

            return await storageProvider.TryGetFileFromPathAsync(path);
        }
        catch (Exception e)
        {
            Log.Exception(ClassName, $"Failed to resolve <{path}> for dragging", e);
            return null;
        }
    }

    private void TryStartDrag(PointerEventArgs e)
    {
        if (_pressedArgs is not { } pressedArgs || _dragItemTask is not { IsCompletedSuccessfully: true, Result: { } storageItem })
            return;

        var delta = e.GetPosition(this) - _pressedPosition;
        if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold)
            return;

        var trimmedQuery = _dragTrimmedQuery;
        ResetPressState();
        _ = DragFileAsync(pressedArgs, storageItem, trimmedQuery);
    }

    private async Task DragFileAsync(PointerPressedEventArgs pressedArgs, IStorageItem storageItem, string? trimmedQuery)
    {
        var mainViewModel = MainViewModel;
        try
        {
            var data = new DataTransfer();
            data.Add(DataTransferItem.CreateFile(storageItem));

            // The drag session outlives the launcher window, which gets out of the way of the drop target.
            var dragTask = DragDrop.DoDragDropAsync(pressedArgs, data, DragDropEffects.Copy | DragDropEffects.Move);
            mainViewModel?.RequestHide();

            var effect = await dragTask;
            if (effect == DragDropEffects.Move && trimmedQuery != null)
                mainViewModel?.ChangeQueryText(trimmedQuery, true);
        }
        catch (Exception e)
        {
            Log.Exception(ClassName, "Failed to drag a result file", e);
        }
    }

    private void ResetPressState()
    {
        _pressedItem = null;
        _pressedArgs = null;
        _dragItemTask = null;
        _dragTrimmedQuery = null;
    }
}
