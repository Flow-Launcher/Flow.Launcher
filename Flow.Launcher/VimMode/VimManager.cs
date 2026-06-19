using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher.VimMode
{
    /// <summary>
    /// Manages the integration of Vim keybindings and UI overlays into the application window.
    /// </summary>
    public class VimManager : IDisposable
    {
        private bool _disposed;
        private readonly MainWindow _mainWindow;
        private readonly MainViewModel _viewModel;
        private readonly TextBox _queryTextBox;

        private static void SetClipboardText(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                try { Clipboard.SetText(text); } catch (Exception ex) { Flow.Launcher.Infrastructure.Logger.Log.Exception("VimManager", "Clipboard operation failed", ex); }
            }
        }
        
        private static System.Windows.Media.SolidColorBrush CreateBrush(byte r, byte g, byte b)
        {
            var brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
        private readonly VimEngine _vimEngine;
        private readonly System.Windows.Shapes.Rectangle _vimBlockCaret;
        private readonly Border _vimModeIndicator;
        private string _pendingCommand = "";
        private string _awaitingCharCommand = "";
        private string _lastFindCmd = "";
        private char _lastFindChar = '\0';
        private DateTime _lastEscapeTime = DateTime.MinValue;
        private int _visualAnchor;
        private int _visualCaret;
        private int _count;
        private bool _gPending;
        private string _awaitingTextObject = "";
        private (int anchor, int caret)? _lastVisualRange;
        private string _lastChange = "";
        private int _lastChangeLen = 0;   // chars affected by last change (for . repeat)
        private char _lastReplaceChar = '\0'; // char used in last r{char} (for . repeat)
        private readonly Flow.Launcher.Infrastructure.UserSettings.Settings _settings;

        // Vim-style operation-level undo/redo stacks
        private readonly System.Collections.Generic.Stack<(string text, int caretIndex)> _undoStack = new();
        private readonly System.Collections.Generic.Stack<(string text, int caretIndex)> _redoStack = new();

        /// <summary>Snapshots the current text+caret state onto the undo stack and clears the redo stack.</summary>
        private void PushUndo()
        {
            _undoStack.Push((_queryTextBox.Text, _queryTextBox.CaretIndex));
            _redoStack.Clear();
        }

        private void VimUndo()
        {
            if (_undoStack.Count == 0) return;
            _redoStack.Push((_queryTextBox.Text, _queryTextBox.CaretIndex));
            var (text, caret) = _undoStack.Pop();
            _queryTextBox.SetCurrentValue(System.Windows.Controls.TextBox.TextProperty, text);
            _queryTextBox.CaretIndex = Math.Min(caret, text.Length);
        }

        private void VimRedo()
        {
            if (_redoStack.Count == 0) return;
            _undoStack.Push((_queryTextBox.Text, _queryTextBox.CaretIndex));
            var (text, caret) = _redoStack.Pop();
            _queryTextBox.SetCurrentValue(System.Windows.Controls.TextBox.TextProperty, text);
            _queryTextBox.CaretIndex = Math.Min(caret, text.Length);
        }

        /// <summary>
        /// Applies a text mutation: snapshots the current state onto the undo stack,
        /// then sets the TextBox text. All text-mutating Vim commands must use this
        /// instead of calling SetCurrentValue directly.
        /// </summary>
        private void SetText(string newText)
        {
            PushUndo();
            _queryTextBox.SetCurrentValue(System.Windows.Controls.TextBox.TextProperty, newText);
        }


        /// <summary>
        /// Initializes a new instance of the VimManager class.
        /// </summary>
        public VimManager(MainWindow mainWindow, MainViewModel viewModel, TextBox queryTextBox, System.Windows.Shapes.Rectangle vimBlockCaret, Border vimModeIndicator, Flow.Launcher.Infrastructure.UserSettings.Settings settings)
        {
            _mainWindow = mainWindow;
            _viewModel = viewModel;
            _queryTextBox = queryTextBox;
            _vimBlockCaret = vimBlockCaret;
            _vimModeIndicator = vimModeIndicator;
            _settings = settings;

            _vimEngine = new VimEngine();
            _vimEngine.ModeChanged += VimEngine_ModeChanged;
            _queryTextBox.PreviewTextInput += QueryTextBox_PreviewTextInput;
            _queryTextBox.SelectionChanged += QueryTextBox_SelectionChanged;
            _queryTextBox.TextChanged += QueryTextBox_TextChanged;
            _mainWindow.Loaded += MainWindow_Loaded;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Initial state
            UpdateIndicatorAsync(_vimEngine.CurrentMode);
        }

        private void QueryTextBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            UpdateCaretPosition();
        }

        private void QueryTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateCaretPosition();
        }

        private void UpdateCaretPosition()
        {
            if (_vimEngine.CurrentMode != VimModeType.Insert && _vimBlockCaret != null)
            {
                try
                {
                    int index = (_vimEngine.CurrentMode == VimModeType.Visual || _vimEngine.CurrentMode == VimModeType.VisualLine)
                        ? _visualCaret
                        : _queryTextBox.CaretIndex;
                    var rect = _queryTextBox.GetRectFromCharacterIndex(index);
                    if (!rect.IsEmpty)
                    {
                        var m = _queryTextBox.Margin;
                        _vimBlockCaret.Margin = new Thickness(rect.Left + m.Left, rect.Top + m.Top, 0, 0);
                        _vimBlockCaret.Width = Math.Max(rect.Width, 8);
                        _vimBlockCaret.Height = rect.Height;
                    }
                }
                catch (Exception ex) { Flow.Launcher.Infrastructure.Logger.Log.Exception("VimManager", "Layout exception in UpdateCaretPosition", ex); }
            }
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.MainWindowVisibilityStatus))
            {
                if (_viewModel.MainWindowVisibilityStatus)
                {
                    _mainWindow.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (_vimEngine.CurrentMode != VimModeType.Insert)
                        {
                            _queryTextBox.SelectionLength = 0;
                            _vimEngine.SwitchToInsert();
                        }
                    }));
                }
            }
        }

        private void VimEngine_ModeChanged(VimModeType mode)
        {
            UpdateIndicatorAsync(mode);
        }

        private void UpdateIndicatorAsync(VimModeType mode)
        {
            if (_mainWindow.Dispatcher.CheckAccess())
            {
                ApplyModeUI(mode);
            }
            else
            {
                _mainWindow.Dispatcher.BeginInvoke(new Action(() => ApplyModeUI(mode)));
            }
        }

        private void ApplyModeUI(VimModeType mode)
        {
            InputMethod.SetIsInputMethodSuspended(_queryTextBox, mode != VimModeType.Insert);

            if (_vimModeIndicator != null)
            {
                _vimModeIndicator.Visibility = _settings.EnableVimMode && mode != VimModeType.Insert ? Visibility.Visible : Visibility.Collapsed;
                
                _vimModeIndicator.Background = mode switch
                {
                    VimModeType.Normal => (System.Windows.Media.Brush)Application.Current.FindResource("BasicSystemAccentColor") ?? CreateBrush(0, 120, 215),
                    VimModeType.Visual => CreateBrush(153, 50, 204),
                    VimModeType.VisualLine => CreateBrush(255, 140, 0),
                    _ => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Transparent)
                };
            }

            if (_vimBlockCaret == null) return;

            if (mode == VimModeType.Insert)
            {
                _vimBlockCaret.Visibility = Visibility.Collapsed;
                _queryTextBox.ClearValue(System.Windows.Controls.TextBox.CaretBrushProperty);
            }
            else
            {
                _vimBlockCaret.Visibility = Visibility.Visible;
                _queryTextBox.CaretBrush = System.Windows.Media.Brushes.Transparent;
                UpdateCaretPosition();
            }
        }

        /// <summary>
        /// Intercepts and processes key presses before they reach the main window, applying Vim bindings if enabled.
        /// </summary>
        /// <param name="e">The key event arguments.</param>
        /// <returns>True if the key was handled by Vim mode, otherwise false.</returns>
        public bool HandlePreviewKeyDown(KeyEventArgs e)
        {
            if (!_settings.EnableVimMode)
            {
                if (_vimBlockCaret != null && _vimBlockCaret.Visibility == Visibility.Visible)
                {
                    _vimBlockCaret.Visibility = Visibility.Collapsed;
                }
                if (_vimModeIndicator != null && _vimModeIndicator.Visibility == Visibility.Visible)
                {
                    _vimModeIndicator.Visibility = Visibility.Collapsed;
                }
                return false;
            }

            if (_vimBlockCaret != null && _vimBlockCaret.Visibility == Visibility.Collapsed && _vimEngine.CurrentMode != VimModeType.Insert)
            {
                _vimBlockCaret.Visibility = Visibility.Visible;
            }

            var modifiers = e.KeyboardDevice.Modifiers;

            if (modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.R && _vimEngine.CurrentMode == VimModeType.Normal)
            {
                VimRedo();
                e.Handled = true;
                return true;
            }

            if (modifiers.HasFlag(ModifierKeys.Control) || modifiers.HasFlag(ModifierKeys.Alt))
            {
                return false;
            }

            if (e.Key == Key.Escape && e.KeyboardDevice.Modifiers == ModifierKeys.Shift)
            {
                _viewModel.Hide();
                e.Handled = true;
                return true;
            }

            if (_vimEngine.CurrentMode == VimModeType.Insert)
            {
                if (e.Key == Key.Escape)
                {
                    _vimEngine.SwitchToNormal();
                    _lastEscapeTime = DateTime.Now;
                    e.Handled = true;
                    return true;
                }
            }
            else
            {
                if (_vimEngine.CurrentMode == VimModeType.Visual || _vimEngine.CurrentMode == VimModeType.VisualLine)
                {
                    if (e.Key == Key.Escape)
                    {
                        SaveVisualRange();
                        _queryTextBox.SelectionLength = 0;
                        _vimEngine.SwitchToNormal();
                        e.Handled = true;
                        return true;
                    }
                }

                if (e.Key == Key.Escape)
                {
                    if ((DateTime.Now - _lastEscapeTime).TotalMilliseconds < 400)
                    {
                        _lastEscapeTime = DateTime.MinValue;
                        return false;
                    }
                    else
                    {
                        _lastEscapeTime = DateTime.Now;
                        _pendingCommand = "";
                        e.Handled = true;
                        return true;
                    }
                }

                if (HandleVimKey(e))
                {
                    e.Handled = true;
                    return true;
                }
            }

            return false;
        }

        private bool HandleVimKey(KeyEventArgs e)
        {
            var modifiers = e.KeyboardDevice.Modifiers;

            if (_vimEngine.CurrentMode == VimModeType.Normal || _vimEngine.CurrentMode == VimModeType.Visual)
            {
                if (_gPending)
                {
                    _gPending = false;
                    return HandleGKey(e, modifiers);
                }

                if (string.IsNullOrEmpty(_awaitingCharCommand) && string.IsNullOrEmpty(_awaitingTextObject))
                {
                    if (e.Key >= Key.D1 && e.Key <= Key.D9 && !modifiers.HasFlag(ModifierKeys.Shift))
                    {
                        _count = _count * 10 + (e.Key - Key.D0);
                        return true;
                    }
                    if (e.Key == Key.D0 && !modifiers.HasFlag(ModifierKeys.Shift) && _count > 0)
                    {
                        _count = _count * 10;
                        return true;
                    }
                }

                if (string.IsNullOrEmpty(_awaitingCharCommand) && _awaitingTextObject.Length > 0)
                {
                    return HandleTextObjectKey(e, modifiers);
                }
            }

            if (_vimEngine.CurrentMode == VimModeType.Normal || _vimEngine.CurrentMode == VimModeType.Visual || _vimEngine.CurrentMode == VimModeType.VisualLine)
            {
                if (!string.IsNullOrEmpty(_awaitingCharCommand))
                {
                    char c = GetCharFromKey(e.Key, modifiers);
                    if (c != '\0')
                    {
                        ExecuteCharCommand(_awaitingCharCommand, c);
                    }
                    else if (e.Key == Key.Escape)
                    {
                        _awaitingCharCommand = ""; // Cancel
                    }
                    e.Handled = true;
                    return true;
                }
            }

            switch (_vimEngine.CurrentMode)
            {
                case VimModeType.Normal:
                    switch (e.Key)
                    {
                        case Key.J:
                            _viewModel.SelectNextItemCommand.Execute(null);
                            return true;
                        case Key.K:
                            _viewModel.SelectPrevItemCommand.Execute(null);
                            return true;
                        case Key.H:
                            ExecuteMotion(ApplyCountMove(i => VimMotionEngine.MoveLeft(i)));
                            return true;
                        case Key.L:
                            ExecuteMotion(ApplyCountMove(i => VimMotionEngine.MoveRight(i, _queryTextBox.Text.Length)));
                            return true;
                        case Key.W when modifiers == ModifierKeys.None:
                            ExecuteMotion(ApplyCountMove(i => VimMotionEngine.MoveNextWord(_queryTextBox.Text, i)));
                            return true;
                        case Key.B when modifiers == ModifierKeys.None:
                            ExecuteMotion(ApplyCountMove(i => VimMotionEngine.MovePrevWord(_queryTextBox.Text, i)));
                            return true;
                        case Key.E when modifiers == ModifierKeys.None:
                            ExecuteMotion(ApplyCountMove(i => VimMotionEngine.MoveEndWord(_queryTextBox.Text, i)));
                            return true;
                        case Key.W when modifiers.HasFlag(ModifierKeys.Shift):
                            ExecuteMotion(ApplyCountMove(i => VimMotionEngine.MoveNextWordBig(_queryTextBox.Text, i)));
                            return true;
                        case Key.B when modifiers.HasFlag(ModifierKeys.Shift):
                            ExecuteMotion(ApplyCountMove(i => VimMotionEngine.MovePrevWordBig(_queryTextBox.Text, i)));
                            return true;
                        case Key.E when modifiers.HasFlag(ModifierKeys.Shift):
                            ExecuteMotion(ApplyCountMove(i => VimMotionEngine.MoveEndWordBig(_queryTextBox.Text, i)));
                            return true;
                        case Key.D5 when modifiers.HasFlag(ModifierKeys.Shift):
                            ExecuteMotion(VimMotionEngine.MoveToMatchingBracket(_queryTextBox.Text, _queryTextBox.CaretIndex));
                            return true;
                        case Key.G:
                            if (modifiers.HasFlag(ModifierKeys.Shift))
                            {
                                _gPending = true;
                                return true;
                            }
                            return true;
                        case Key.D0:
                            if (modifiers.HasFlag(ModifierKeys.Shift)) return false; // Handle ')' normally or ignore
                            ExecuteMotion(VimMotionEngine.MoveStartOfLine());
                            return true;
                        case Key.D6:
                            if (modifiers.HasFlag(ModifierKeys.Shift)) // ^
                            {
                                ExecuteMotion(VimMotionEngine.MoveFirstNonBlank(_queryTextBox.Text));
                                return true;
                            }
                            return false;
                        case Key.D4:
                            if (modifiers.HasFlag(ModifierKeys.Shift)) // $
                            {
                                ExecuteMotion(VimMotionEngine.MoveEndOfLine(_queryTextBox.Text.Length));
                                return true;
                            }
                            return false;
                        case Key.F:
                        case Key.T:
                            _awaitingCharCommand = modifiers.HasFlag(ModifierKeys.Shift) ? e.Key.ToString() : e.Key.ToString().ToLower();
                            return true;
                        case Key.R:
                            _awaitingCharCommand = "r";
                            return true;
                        case Key.OemSemicolon:
                            if (!modifiers.HasFlag(ModifierKeys.Shift) && _lastFindChar != '\0')
                            {
                                ExecuteFindCommand(_lastFindCmd, _lastFindChar);
                                return true;
                            }
                            return false;
                        case Key.OemComma:
                            if (!modifiers.HasFlag(ModifierKeys.Shift) && _lastFindChar != '\0')
                            {
                                string reverseCmd = _lastFindCmd == "f" ? "F" : _lastFindCmd == "F" ? "f" : _lastFindCmd == "t" ? "T" : "t";
                                ExecuteFindCommand(reverseCmd, _lastFindChar);
                                return true;
                            }
                            return false;
                        case Key.X:
                            if (modifiers.HasFlag(ModifierKeys.Shift)) // X
                            {
                                if (_queryTextBox.CaretIndex > 0)
                                {
                                    int c = _queryTextBox.CaretIndex - 1;
                                    SetClipboardText(_queryTextBox.Text.Substring(c, 1));
                                    SetText(_queryTextBox.Text.Remove(c, 1));
                                    _queryTextBox.CaretIndex = c;
                                }
                                _lastChange = "X";
                                _lastChangeLen = 1;
                            }
                            else // x
                            {
                                int n = GetCount();
                                if (_queryTextBox.CaretIndex < _queryTextBox.Text.Length)
                                {
                                    int c = _queryTextBox.CaretIndex;
                                    int len = Math.Min(n, _queryTextBox.Text.Length - c);
                                    SetClipboardText(_queryTextBox.Text.Substring(c, len));
                                    SetText(_queryTextBox.Text.Remove(c, len));
                                    _queryTextBox.CaretIndex = c;
                                }
                                _lastChange = "x";
                                _lastChangeLen = n;
                            }
                            return true;
                        case Key.S:
                            if (modifiers.HasFlag(ModifierKeys.Shift)) // S -> cc (substitute entire line)
                            {
                                _queryTextBox.CaretIndex = 0;
                                _pendingCommand = "c";
                                ExecuteMotion(VimMotionEngine.MoveEndOfLine(_queryTextBox.Text.Length));
                            }
                            else // s -> cl
                            {
                                if (_queryTextBox.CaretIndex < _queryTextBox.Text.Length)
                                {
                                    int c = _queryTextBox.CaretIndex;
                                    SetClipboardText(_queryTextBox.Text.Substring(c, 1));
                                    SetText(_queryTextBox.Text.Remove(c, 1));
                                    _queryTextBox.CaretIndex = c;
                                }
                                _lastChange = "s";
                                _lastChangeLen = 1;
                                _vimEngine.SwitchToInsert();
                            }
                            return true;
                        case Key.OemTilde:
                            if (modifiers.HasFlag(ModifierKeys.Shift))
                            {
                                int c = _queryTextBox.CaretIndex;
                                if (c < _queryTextBox.Text.Length)
                                {
                                    char ch = _queryTextBox.Text[c];
                                    ch = char.IsUpper(ch) ? char.ToLower(ch) : char.ToUpper(ch);
                                    SetText(_queryTextBox.Text.Remove(c, 1).Insert(c, ch.ToString()));
                                    _queryTextBox.CaretIndex = Math.Min(_queryTextBox.Text.Length, c + 1);
                                }
                                _lastChange = "~";
                                return true;
                            }
                            return false;

                        case Key.P:
                            try
                            {
                                string text = Clipboard.GetText();
                                if (!string.IsNullOrEmpty(text))
                                {
                                    int c = _queryTextBox.CaretIndex;
                                    if (c < _queryTextBox.Text.Length) c++;
                                    SetText(_queryTextBox.Text.Insert(c, text));
                                    _queryTextBox.CaretIndex = c;
                                }
                            }
                            catch (Exception ex) { Flow.Launcher.Infrastructure.Logger.Log.Exception("VimManager", "Clipboard/Redo operation failed", ex); }
                            return true;
                        case Key.U:
                            VimUndo();
                            return true;
                        case Key.D:
                        case Key.C:
                        case Key.Y:
                            string cmd = e.Key.ToString().ToLower();
                            if (modifiers.HasFlag(ModifierKeys.Shift)) // D, C, Y
                            {
                                _pendingCommand = cmd;
                                ExecuteMotion(VimMotionEngine.MoveEndOfLine(_queryTextBox.Text.Length));
                                _lastChange = cmd.ToUpper() + "_eol";
                                return true;
                            }

                            if (_pendingCommand == cmd) // dd, cc, yy
                            {
                                if (!string.IsNullOrEmpty(_queryTextBox.Text))
                                {
                                    SetClipboardText(_queryTextBox.Text);
                                }
                                if (cmd == "d" || cmd == "c")
                                {
                                    SetText("");
                                    _queryTextBox.CaretIndex = 0;
                                }
                                if (cmd == "c")
                                    _vimEngine.SwitchToInsert();
                                _lastChange = cmd + cmd;
                                _pendingCommand = "";
                            }
                            else if (_pendingCommand != "" && _pendingCommand != cmd)
                            {
                                _pendingCommand = "";
                                return true;
                            }
                            else
                            {
                                _pendingCommand = cmd;
                            }
                            return true;
                        case Key.A when _pendingCommand == "d" || _pendingCommand == "c" || _pendingCommand == "y":
                            _awaitingTextObject = "a";
                            return true;
                        case Key.I when _pendingCommand == "d" || _pendingCommand == "c" || _pendingCommand == "y":
                            _awaitingTextObject = "i";
                            return true;
                        case Key.I when modifiers == ModifierKeys.None && _pendingCommand == "":
                            _vimEngine.SwitchToInsert();
                            return true;
                        case Key.I when modifiers.HasFlag(ModifierKeys.Shift):
                            _vimEngine.SwitchToInsert();
                            _queryTextBox.CaretIndex = 0;
                            return true;
                        case Key.A when modifiers == ModifierKeys.None && _pendingCommand == "":
                            _vimEngine.SwitchToInsert();
                            if (_queryTextBox.CaretIndex < _queryTextBox.Text.Length)
                            {
                                _queryTextBox.CaretIndex++;
                            }
                            return true;
                        case Key.A when modifiers.HasFlag(ModifierKeys.Shift):
                            _vimEngine.SwitchToInsert();
                            _queryTextBox.CaretIndex = _queryTextBox.Text.Length;
                            return true;
                        case Key.V:
                            if (modifiers.HasFlag(ModifierKeys.Shift))
                                EnterVisualLineMode();
                            else
                                EnterVisualMode();
                            return true;
                        case Key.OemPeriod:
                            if (!string.IsNullOrEmpty(_lastChange))
                                RepeatLastChange();
                            return true;
                        case Key.Escape:
                            return true; 
                        default:
                            if (IsVimBlockedKey(e.Key))
                                return true;
                            return false;
                    }

                case VimModeType.Visual:
                    switch (e.Key)
                    {
                        case Key.V:
                            if (modifiers.HasFlag(ModifierKeys.Shift))
                                EnterVisualLineMode();
                            else
                            {
                                _queryTextBox.SelectionLength = 0;
                                _vimEngine.SwitchToNormal();
                            }
                            return true;
                        case Key.O when modifiers == ModifierKeys.None:
                            SwapVisualEnds();
                            return true;
                        case Key.I when modifiers == ModifierKeys.None:
                            {
                                int pos = Math.Min(_visualAnchor, _visualCaret);
                                _queryTextBox.SelectionLength = 0;
                                _queryTextBox.CaretIndex = pos;
                                _vimEngine.SwitchToInsert();
                            }
                            return true;
                        case Key.A when modifiers == ModifierKeys.None:
                            {
                                int pos = Math.Max(_visualAnchor, _visualCaret) + 1;
                                _queryTextBox.SelectionLength = 0;
                                _queryTextBox.CaretIndex = Math.Min(pos, _queryTextBox.Text.Length);
                                _vimEngine.SwitchToInsert();
                            }
                            return true;
                        case Key.H:
                            ExecuteVisualMotion(ApplyCountMove(i => VimMotionEngine.MoveLeft(i)));
                            return true;
                        case Key.L:
                            ExecuteVisualMotion(ApplyCountMove(i => VimMotionEngine.MoveRight(i, _queryTextBox.Text.Length)));
                            return true;
                        case Key.W when modifiers == ModifierKeys.None:
                            ExecuteVisualMotion(ApplyCountMove(i => VimMotionEngine.MoveNextWord(_queryTextBox.Text, i)));
                            return true;
                        case Key.B when modifiers == ModifierKeys.None:
                            ExecuteVisualMotion(ApplyCountMove(i => VimMotionEngine.MovePrevWord(_queryTextBox.Text, i)));
                            return true;
                        case Key.E when modifiers == ModifierKeys.None:
                            ExecuteVisualMotion(ApplyCountMove(i => VimMotionEngine.MoveEndWord(_queryTextBox.Text, i)));
                            return true;
                        case Key.W when modifiers.HasFlag(ModifierKeys.Shift):
                            ExecuteVisualMotion(ApplyCountMove(i => VimMotionEngine.MoveNextWordBig(_queryTextBox.Text, i)));
                            return true;
                        case Key.B when modifiers.HasFlag(ModifierKeys.Shift):
                            ExecuteVisualMotion(ApplyCountMove(i => VimMotionEngine.MovePrevWordBig(_queryTextBox.Text, i)));
                            return true;
                        case Key.E when modifiers.HasFlag(ModifierKeys.Shift):
                            ExecuteVisualMotion(ApplyCountMove(i => VimMotionEngine.MoveEndWordBig(_queryTextBox.Text, i)));
                            return true;
                        case Key.D0:
                            if (modifiers.HasFlag(ModifierKeys.Shift)) return true;
                            ExecuteVisualMotion(VimMotionEngine.MoveStartOfLine());
                            return true;
                        case Key.D5 when modifiers.HasFlag(ModifierKeys.Shift):
                            ExecuteVisualMotion(VimMotionEngine.MoveToMatchingBracket(_queryTextBox.Text, _visualCaret));
                            return true;
                        case Key.D6:
                            if (modifiers.HasFlag(ModifierKeys.Shift))
                            {
                                ExecuteVisualMotion(VimMotionEngine.MoveFirstNonBlank(_queryTextBox.Text));
                                return true;
                            }
                            return true;
                        case Key.D4:
                            if (modifiers.HasFlag(ModifierKeys.Shift))
                            {
                                ExecuteVisualMotion(VimMotionEngine.MoveEndOfLine(_queryTextBox.Text.Length));
                                return true;
                            }
                            return true;
                        case Key.F:
                        case Key.T:
                            _awaitingCharCommand = modifiers.HasFlag(ModifierKeys.Shift) ? e.Key.ToString() : e.Key.ToString().ToLower();
                            return true;
                        case Key.R:
                            _awaitingCharCommand = "r";
                            return true;
                        case Key.OemSemicolon:
                            if (!modifiers.HasFlag(ModifierKeys.Shift) && _lastFindChar != '\0')
                            {
                                ExecuteFindCommand(_lastFindCmd, _lastFindChar);
                                return true;
                            }
                            return true;
                        case Key.OemComma:
                            if (!modifiers.HasFlag(ModifierKeys.Shift) && _lastFindChar != '\0')
                            {
                                string reverseCmd = _lastFindCmd == "f" ? "F" : _lastFindCmd == "F" ? "f" : _lastFindCmd == "t" ? "T" : "t";
                                ExecuteFindCommand(reverseCmd, _lastFindChar);
                                return true;
                            }
                            return true;
                        case Key.A when modifiers == ModifierKeys.None:
                            _awaitingTextObject = "a";
                            return true;
                        case Key.I when modifiers == ModifierKeys.None:
                            _awaitingTextObject = "i";
                            return true;
                        case Key.X:
                        case Key.D:
                            {
                                int selStart = _queryTextBox.SelectionStart;
                                int selLength = _queryTextBox.SelectionLength;
                                if (selLength > 0)
                                {
                                    SetClipboardText(_queryTextBox.Text.Substring(selStart, selLength));
                                    SetText(_queryTextBox.Text.Remove(selStart, selLength));
                                    _queryTextBox.CaretIndex = selStart;
                                }
                                _vimEngine.SwitchToNormal();
                            }
                            return true;
                        case Key.Y:
                            {
                                int selStart = _queryTextBox.SelectionStart;
                                int selLength = _queryTextBox.SelectionLength;
                                if (selLength > 0)
                                {
                                    SetClipboardText(_queryTextBox.Text.Substring(selStart, selLength));
                                }
                                _queryTextBox.CaretIndex = selStart;
                                _queryTextBox.SelectionLength = 0;
                                _vimEngine.SwitchToNormal();
                            }
                            return true;
                        case Key.C:
                        case Key.S:
                            {
                                int selStart = _queryTextBox.SelectionStart;
                                int selLength = _queryTextBox.SelectionLength;
                                if (selLength > 0)
                                {
                                    SetClipboardText(_queryTextBox.Text.Substring(selStart, selLength));
                                    SetText(_queryTextBox.Text.Remove(selStart, selLength));
                                    _queryTextBox.CaretIndex = selStart;
                                }
                                _vimEngine.SwitchToInsert();
                            }
                            return true;
                        case Key.OemTilde:
                            if (modifiers.HasFlag(ModifierKeys.Shift))
                            {
                                int selStart = _queryTextBox.SelectionStart;
                                int selLength = _queryTextBox.SelectionLength;
                                if (selLength > 0)
                                {
                                    char[] chars = _queryTextBox.Text.ToCharArray();
                                    for (int i = selStart; i < selStart + selLength && i < chars.Length; i++)
                                        chars[i] = char.IsUpper(chars[i]) ? char.ToLower(chars[i]) : char.ToUpper(chars[i]);
                                    SetText(new string(chars));
                                    _queryTextBox.CaretIndex = selStart;
                                    _queryTextBox.SelectionLength = 0;
                                }
                                _vimEngine.SwitchToNormal();
                            }
                            return true;
                        case Key.J:
                            _viewModel.SelectNextItemCommand.Execute(null);
                            return true;
                        case Key.K:
                            _viewModel.SelectPrevItemCommand.Execute(null);
                            return true;
                        default:
                            return true;
                    }

                case VimModeType.VisualLine:
                    switch (e.Key)
                    {
                        case Key.V:
                            if (modifiers.HasFlag(ModifierKeys.Shift))
                            {
                                _queryTextBox.SelectionLength = 0;
                                _vimEngine.SwitchToNormal();
                            }
                            else
                            {
                                _vimEngine.SwitchToVisual();
                                UpdateVisualSelection();
                            }
                            return true;
                        case Key.X:
                        case Key.D:
                            SetClipboardText(_queryTextBox.Text);
                            SetText("");
                            _queryTextBox.CaretIndex = 0;
                            _vimEngine.SwitchToNormal();
                            return true;
                        case Key.Y:
                            SetClipboardText(_queryTextBox.Text);
                            _queryTextBox.CaretIndex = 0;
                            _queryTextBox.SelectionLength = 0;
                            _vimEngine.SwitchToNormal();
                            return true;
                        case Key.C:
                        case Key.S:
                            SetClipboardText(_queryTextBox.Text);
                            SetText("");
                            _queryTextBox.CaretIndex = 0;
                            _vimEngine.SwitchToInsert();
                            return true;
                        case Key.R:
                            _awaitingCharCommand = "r";
                            return true;
                        case Key.OemTilde:
                            if (modifiers.HasFlag(ModifierKeys.Shift))
                            {
                                if (_queryTextBox.Text.Length > 0)
                                {
                                    char[] chars = _queryTextBox.Text.ToCharArray();
                                    for (int i = 0; i < chars.Length; i++)
                                        chars[i] = char.IsUpper(chars[i]) ? char.ToLower(chars[i]) : char.ToUpper(chars[i]);
                                    SetText(new string(chars));
                                    _queryTextBox.CaretIndex = 0;
                                    _queryTextBox.SelectionLength = 0;
                                }
                                _vimEngine.SwitchToNormal();
                            }
                            return true;
                        case Key.J:
                            _viewModel.SelectNextItemCommand.Execute(null);
                            return true;
                        case Key.K:
                            _viewModel.SelectPrevItemCommand.Execute(null);
                            return true;
                        default:
                            return true;
                    }

                default:
                    return false;
            }
        }

        private bool HandleGKey(KeyEventArgs e, ModifierKeys modifiers)
        {
            switch (_vimEngine.CurrentMode)
            {
                case VimModeType.Normal:
                    switch (e.Key)
                    {
                        case Key.OemMinus when modifiers.HasFlag(ModifierKeys.Shift):
                            ExecuteMotion(VimMotionEngine.MoveLastNonBlank(_queryTextBox.Text));
                            return true;
                        case Key.T when modifiers.HasFlag(ModifierKeys.Shift):
                            _pendingCommand = "~";
                            return true;
                        case Key.U when modifiers == ModifierKeys.None:
                            _pendingCommand = "gu";
                            return true;
                        case Key.U when modifiers.HasFlag(ModifierKeys.Shift):
                            _pendingCommand = "gU";
                            return true;
                        case Key.V:
                            if (_lastVisualRange != null)
                            {
                                _visualAnchor = _lastVisualRange.Value.anchor;
                                _visualCaret = _lastVisualRange.Value.caret;
                                _vimEngine.SwitchToVisual();
                                UpdateVisualSelection();
                            }
                            return true;
                        case Key.OemTilde:
                            _pendingCommand = "~";
                            return true;
                        default:
                            return true;
                    }
                case VimModeType.Visual:
                    switch (e.Key)
                    {
                        case Key.V:
                            if (_lastVisualRange != null)
                            {
                                _visualAnchor = _lastVisualRange.Value.anchor;
                                _visualCaret = _lastVisualRange.Value.caret;
                                UpdateVisualSelection();
                            }
                            return true;
                        default:
                            return true;
                    }
                default:
                    return true;
            }
        }

        private bool HandleTextObjectKey(KeyEventArgs e, ModifierKeys modifiers)
        {
            string prefix = _awaitingTextObject;
            _awaitingTextObject = "";

            char delim = GetCharFromKey(e.Key, modifiers);
            if (delim == '\0' && e.Key != Key.W) return true;
            if (e.Key == Key.W) delim = 'w';

            string text = _queryTextBox.Text;
            int caret = (_vimEngine.CurrentMode == VimModeType.Visual) ? _visualCaret : _queryTextBox.CaretIndex;
            bool around = prefix == "a";
            (int start, int end) range = (0, 0);

            switch (delim)
            {
                case 'w':
                    range = VimMotionEngine.TextObjectWord(text, caret, around);
                    break;
                case '"':
                    range = VimMotionEngine.TextObjectQuote(text, caret, '"', around);
                    break;
                case '\'':
                    range = VimMotionEngine.TextObjectQuote(text, caret, '\'', around);
                    break;
                case '(':
                case ')':
                    range = VimMotionEngine.TextObjectDelimited(text, caret, '(', ')', around);
                    break;
                case '[':
                case ']':
                    range = VimMotionEngine.TextObjectDelimited(text, caret, '[', ']', around);
                    break;
                case '{':
                case '}':
                    range = VimMotionEngine.TextObjectDelimited(text, caret, '{', '}', around);
                    break;
                default:
                    return true;
            }

            if (range.start < 0) return true;

            if (_vimEngine.CurrentMode == VimModeType.Visual)
            {
                _visualAnchor = range.start;
                _visualCaret = range.end;
                UpdateVisualSelection();
                return true;
            }

            if (_pendingCommand == "d" || _pendingCommand == "c" || _pendingCommand == "y")
            {
                int len = range.end - range.start + 1;
                if (len > 0)
                {
                    SetClipboardText(text.Substring(range.start, len));
                    if (_pendingCommand != "y")
                    {
                        SetText(text.Remove(range.start, len));
                        _queryTextBox.CaretIndex = range.start;
                    }
                    if (_pendingCommand == "c")
                        _vimEngine.SwitchToInsert();
                }
                _pendingCommand = "";
                return true;
            }

            return true;
        }

        private int ApplyCountMove(Func<int, int> move)
        {
            int target = (_vimEngine.CurrentMode == VimModeType.Visual) ? _visualCaret : _queryTextBox.CaretIndex;
            int n = _count > 0 ? _count : 1;
            for (int i = 0; i < n; i++)
                target = move(target);
            _count = 0;
            return target;
        }

        private int GetCount()
        {
            int c = _count > 0 ? _count : 1;
            _count = 0;
            return c;
        }

        private void RepeatLastChange()
        {
            switch (_lastChange)
            {
                case "dd":
                    SetClipboardText(_queryTextBox.Text);
                    SetText("");
                    _queryTextBox.CaretIndex = 0;
                    break;
                case "cc":
                    SetClipboardText(_queryTextBox.Text);
                    SetText("");
                    _queryTextBox.CaretIndex = 0;
                    _vimEngine.SwitchToInsert();
                    break;
                case "D_eol":
                case "C_eol":
                    {
                        int c = _queryTextBox.CaretIndex;
                        if (c < _queryTextBox.Text.Length)
                        {
                            SetClipboardText(_queryTextBox.Text.Substring(c));
                            SetText(_queryTextBox.Text.Remove(c));
                            _queryTextBox.CaretIndex = c;
                        }
                        if (_lastChange == "C_eol") _vimEngine.SwitchToInsert();
                    }
                    break;
                case "Y_eol":
                    SetClipboardText(_queryTextBox.Text);
                    break;
                case "x":
                    {
                        int n = GetCount();
                        int c = _queryTextBox.CaretIndex;
                        int len = Math.Min(n, _queryTextBox.Text.Length - c);
                        if (len > 0)
                        {
                            SetClipboardText(_queryTextBox.Text.Substring(c, len));
                            SetText(_queryTextBox.Text.Remove(c, len));
                            _queryTextBox.CaretIndex = c;
                        }
                    }
                    break;
                case "~":
                    {
                        int c = _queryTextBox.CaretIndex;
                        if (c < _queryTextBox.Text.Length)
                        {
                            char ch = _queryTextBox.Text[c];
                            ch = char.IsUpper(ch) ? char.ToLower(ch) : char.ToUpper(ch);
                            SetText(_queryTextBox.Text.Remove(c, 1).Insert(c, ch.ToString()));
                            _queryTextBox.CaretIndex = Math.Min(_queryTextBox.Text.Length, c + 1);
                        }
                    }
                    break;
                case "d_motion":
                    {
                        int c = _queryTextBox.CaretIndex;
                        int len = Math.Min(_lastChangeLen, _queryTextBox.Text.Length - c);
                        if (len > 0)
                        {
                            SetClipboardText(_queryTextBox.Text.Substring(c, len));
                            SetText(_queryTextBox.Text.Remove(c, len));
                            _queryTextBox.CaretIndex = c;
                        }
                    }
                    break;
                case "c_motion":
                    {
                        int c = _queryTextBox.CaretIndex;
                        int len = Math.Min(_lastChangeLen, _queryTextBox.Text.Length - c);
                        if (len > 0)
                        {
                            SetClipboardText(_queryTextBox.Text.Substring(c, len));
                            SetText(_queryTextBox.Text.Remove(c, len));
                            _queryTextBox.CaretIndex = c;
                        }
                        _vimEngine.SwitchToInsert();
                    }
                    break;
                case "s":
                case "X":
                    {
                        int c = _queryTextBox.CaretIndex;
                        if (_lastChange == "X") c = Math.Max(0, c - 1);
                        int len = Math.Min(_lastChangeLen, _queryTextBox.Text.Length - c);
                        if (len > 0)
                        {
                            SetClipboardText(_queryTextBox.Text.Substring(c, len));
                            SetText(_queryTextBox.Text.Remove(c, len));
                            _queryTextBox.CaretIndex = c;
                        }
                        if (_lastChange == "s") _vimEngine.SwitchToInsert();
                    }
                    break;
                case "r":
                    {
                        int c = _queryTextBox.CaretIndex;
                        if (_lastReplaceChar != '\0' && c < _queryTextBox.Text.Length)
                        {
                            SetText(_queryTextBox.Text.Remove(c, 1).Insert(c, _lastReplaceChar.ToString()));
                            _queryTextBox.CaretIndex = c;
                        }
                    }
                    break;
            }
        }

        private void ExecuteCharCommand(string cmd, char c)
        {
            _awaitingCharCommand = "";
            string text = _queryTextBox.Text;
            int caret = _queryTextBox.CaretIndex;

            if (cmd == "r")
            {
                if (_vimEngine.CurrentMode == VimModeType.Visual || _vimEngine.CurrentMode == VimModeType.VisualLine)
                {
                    int selStart = _queryTextBox.SelectionStart;
                    int selLength = _queryTextBox.SelectionLength;
                    if (selLength > 0)
                    {
                        char[] chars = text.ToCharArray();
                        for (int i = selStart; i < selStart + selLength && i < chars.Length; i++)
                            chars[i] = c;
                        SetText(new string(chars));
                        _queryTextBox.CaretIndex = selStart;
                        _queryTextBox.SelectionLength = 0;
                        _vimEngine.SwitchToNormal();
                    }
                }
                else if (caret < text.Length)
                {
                    SetText(text.Remove(caret, 1).Insert(caret, c.ToString()));
                    _queryTextBox.CaretIndex = caret;
                    _lastChange = "r";
                    _lastReplaceChar = c;
                }
            }
            else if (cmd == "f" || cmd == "F" || cmd == "t" || cmd == "T")
            {
                _lastFindCmd = cmd;
                _lastFindChar = c;
                ExecuteFindCommand(cmd, c);
            }
        }

        private void ExecuteFindCommand(string cmd, char c)
        {
            string text = _queryTextBox.Text;
            int caret = (_vimEngine.CurrentMode == VimModeType.Visual || _vimEngine.CurrentMode == VimModeType.VisualLine)
                ? _visualCaret
                : _queryTextBox.CaretIndex;
            int target = caret;

            if (cmd == "f") target = VimMotionEngine.FindCharForward(text, caret, c, false);
            else if (cmd == "F") target = VimMotionEngine.FindCharBackward(text, caret, c, false);
            else if (cmd == "t") target = VimMotionEngine.FindCharForward(text, caret, c, true);
            else if (cmd == "T") target = VimMotionEngine.FindCharBackward(text, caret, c, true);

            if (_vimEngine.CurrentMode == VimModeType.Visual || _vimEngine.CurrentMode == VimModeType.VisualLine)
                ExecuteVisualMotion(target);
            else
                ExecuteMotion(target);
        }

        private void ExecuteMotion(int targetCaret)
        {
            if (_pendingCommand == "d" || _pendingCommand == "c" || _pendingCommand == "y")
            {
                int start = Math.Max(0, _queryTextBox.CaretIndex);
                int end = Math.Max(0, targetCaret);
                if (start > end) { var temp = start; start = end; end = temp; }
                
                if (end > start)
                {
                    SetClipboardText(_queryTextBox.Text.Substring(start, end - start));
                    if (_pendingCommand != "y")
                    {
                        SetText(_queryTextBox.Text.Remove(start, end - start));
                        _queryTextBox.CaretIndex = start;
                    }
                }
                
                if (_pendingCommand == "c")
                    _vimEngine.SwitchToInsert();
                _lastChange = _pendingCommand + "_motion";
                _lastChangeLen = end - start;
                _pendingCommand = "";
            }
            else if (_pendingCommand == "~")
            {
                int start = Math.Max(0, _queryTextBox.CaretIndex);
                int end = Math.Max(0, targetCaret);
                if (start > end) { var temp = start; start = end; end = temp; }
                if (end < _queryTextBox.Text.Length) end++;
                
                if (end > start)
                {
                    char[] chars = _queryTextBox.Text.ToCharArray();
                    for (int i = start; i < end && i < chars.Length; i++)
                        chars[i] = char.IsUpper(chars[i]) ? char.ToLower(chars[i]) : char.ToUpper(chars[i]);
                    SetText(new string(chars));
                    _queryTextBox.CaretIndex = Math.Min(start, _queryTextBox.Text.Length);
                }
                _lastChange = "~";
                _pendingCommand = "";
            }
            else if (_pendingCommand == "gu")
            {
                int start = Math.Max(0, _queryTextBox.CaretIndex);
                int end = Math.Max(0, targetCaret);
                if (start > end) { var temp = start; start = end; end = temp; }
                if (end < _queryTextBox.Text.Length) end++;
                
                if (end > start)
                {
                    char[] chars = _queryTextBox.Text.ToCharArray();
                    for (int i = start; i < end && i < chars.Length; i++)
                        chars[i] = char.ToLower(chars[i]);
                    SetText(new string(chars));
                    _queryTextBox.CaretIndex = start;
                }
                _pendingCommand = "";
            }
            else if (_pendingCommand == "gU")
            {
                int start = Math.Max(0, _queryTextBox.CaretIndex);
                int end = Math.Max(0, targetCaret);
                if (start > end) { var temp = start; start = end; end = temp; }
                if (end < _queryTextBox.Text.Length) end++;
                
                if (end > start)
                {
                    char[] chars = _queryTextBox.Text.ToCharArray();
                    for (int i = start; i < end && i < chars.Length; i++)
                        chars[i] = char.ToUpper(chars[i]);
                    SetText(new string(chars));
                    _queryTextBox.CaretIndex = start;
                }
                _pendingCommand = "";
            }
            else
            {
                _queryTextBox.CaretIndex = targetCaret;
            }
        }

        private void UpdateVisualSelection()
        {
            int start = Math.Min(_visualAnchor, _visualCaret);
            int end = Math.Max(_visualAnchor, _visualCaret);
            if (start < 0) start = 0;
            int length = Math.Min(end - start + 1, _queryTextBox.Text.Length - start);
            if (length < 0) length = 0;
            _queryTextBox.Select(start, length);
            UpdateCaretPosition();
        }

        private void ExecuteVisualMotion(int targetCaret)
        {
            _visualCaret = targetCaret;
            UpdateVisualSelection();
        }

        private void EnterVisualMode()
        {
            if (_queryTextBox.Text.Length == 0) return;
            if (_queryTextBox.CaretIndex >= _queryTextBox.Text.Length)
                _queryTextBox.CaretIndex = _queryTextBox.Text.Length - 1;
            _visualAnchor = _queryTextBox.CaretIndex;
            _visualCaret = _queryTextBox.CaretIndex;
            _vimEngine.SwitchToVisual();
            UpdateVisualSelection();
        }

        private void EnterVisualLineMode()
        {
            if (_queryTextBox.Text.Length == 0) return;
            _visualAnchor = 0;
            _visualCaret = _queryTextBox.Text.Length - 1;
            _vimEngine.SwitchToVisualLine();
            _queryTextBox.Select(0, _queryTextBox.Text.Length);
            UpdateCaretPosition();
        }

        private void SwapVisualEnds()
        {
            int temp = _visualAnchor;
            _visualAnchor = _visualCaret;
            _visualCaret = temp;
            UpdateVisualSelection();
        }

        private void SaveVisualRange()
        {
            _lastVisualRange = (_visualAnchor, _visualCaret);
        }

        private static bool IsVimBlockedKey(Key key)
        {
            if (key >= Key.A && key <= Key.Z) return true;
            if (key >= Key.D0 && key <= Key.D9) return true;
            if (key >= Key.NumPad0 && key <= Key.NumPad9) return true;
            if (key == Key.Space) return true;
            if (key == Key.OemTilde) return true;
            if (key == Key.OemMinus) return true;
            if (key == Key.OemPlus) return true;
            if (key >= Key.OemOpenBrackets && key <= Key.OemQuotes) return true;
            if (key == Key.OemPeriod || key == Key.OemComma) return true;
            if (key == Key.Back) return true;
            return false;
        }

        private static char GetCharFromKey(Key key, ModifierKeys modifiers)
        {
            bool shift = modifiers.HasFlag(ModifierKeys.Shift);
            if (key >= Key.A && key <= Key.Z)
                return shift ? key.ToString()[0] : key.ToString().ToLower()[0];
            if (key >= Key.D0 && key <= Key.D9)
            {
                if (!shift) return (char)('0' + (key - Key.D0));
                switch (key)
                {
                    case Key.D1: return '!'; case Key.D2: return '@'; case Key.D3: return '#';
                    case Key.D4: return '$'; case Key.D5: return '%'; case Key.D6: return '^';
                    case Key.D7: return '&'; case Key.D8: return '*'; case Key.D9: return '(';
                    case Key.D0: return ')';
                }
            }
            switch (key)
            {
                case Key.Space: return ' ';
                case Key.OemSemicolon: return shift ? ':' : ';';
                case Key.OemComma: return shift ? '<' : ',';
                case Key.OemPeriod: return shift ? '>' : '.';
                case Key.OemQuestion: return shift ? '?' : '/';
                case Key.OemQuotes: return shift ? '"' : '\'';
                case Key.OemOpenBrackets: return shift ? '{' : '[';
                case Key.OemCloseBrackets: return shift ? '}' : ']';
                case Key.OemPipe: return shift ? '|' : '\\';
                case Key.OemMinus: return shift ? '_' : '-';
                case Key.OemPlus: return shift ? '+' : '=';
                case Key.OemTilde: return shift ? '~' : '`';
            }
            return '\0';
        }

        private void QueryTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (_vimEngine.CurrentMode != VimModeType.Insert)
            {
                e.Handled = true;
            }
        }

        /// <summary>
        /// Gets a value indicating whether native text input is currently blocked by Vim mode.
        /// </summary>
        public bool IsInputBlocked => _vimEngine.CurrentMode != VimModeType.Insert;

        /// <summary>
        /// Disposes the Vim manager and detaches from window events.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _vimEngine.ModeChanged -= VimEngine_ModeChanged;
                    _mainWindow.Loaded -= MainWindow_Loaded;
                    _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                    _queryTextBox.PreviewTextInput -= QueryTextBox_PreviewTextInput;
                    _queryTextBox.SelectionChanged -= QueryTextBox_SelectionChanged;
                    _queryTextBox.TextChanged -= QueryTextBox_TextChanged;
                }

                _disposed = true;
            }
        }
    }
}

