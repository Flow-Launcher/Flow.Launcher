# Flow Launcher - Vim Edition

This is a specialized fork of [Flow Launcher](https://github.com/Flow-Launcher/Flow.Launcher), equipped with a fully featured, terminal-grade inline **Vim Mode** directly integrated into the search bar. 

### Why does this exist?
This fork was originally created to streamline journaling workflows. By using Flow Launcher to quickly input journal entries into a personal journaling system, typing out long strings quickly became tedious to edit. Standard text box navigation isn't fast enough. This fork adds native Vim motions directly to the search bar so that when you make a mistake typing a long entry or command, you can instantly drop into Normal mode, hop across word boundaries, and fix the typo without your hands ever leaving the home row.

---

## 🛠️ Advanced Vim Features

Vim Mode transforms the Flow Launcher search bar into a modal editor, offering **Insert**, **Normal**, **Visual**, and **Visual Line** modes, complete with an overlaid block caret that physically tracks to the text layout.

You can instantly toggle this feature on or off via the `General` tab in the Flow Launcher Settings.

### Modes & Intercepts
- **Mode Indicator**: A subtle, low-opacity indicator sits at the bottom-left of the search bar to clearly show your current mode (`-- NORMAL --`, `-- VISUAL --`, etc.) without interfering with plugins.
- **Insert Mode**: The default state. Works exactly like standard Flow Launcher (blinking text caret).
- **Normal Mode**: Replaces the blinking caret with a solid block caret, and intercepts alphanumeric keystrokes to execute text manipulation commands.
- **Visual Mode**: Char-wise selection. Movements extend the selection from a fixed anchor point; operators apply to the selected region.
- **Visual Line Mode**: Line-wise selection. Selects the entire query; operators apply to the whole line.
- **Escape**: Pressing `Escape` while typing will unconditionally kick you into Normal mode. 
- **Double-Escape**: Quickly double-tapping `Escape` (within 400ms) will immediately hide the Flow Launcher UI.

### Normal Mode Keybindings

#### Basic Navigation
- `h` / `l` : Move cursor left / right
- `j` / `k` : Navigate up / down through Flow Launcher search results
- `0` : Snap to the absolute beginning of the query
- `^` : Snap to the first non-blank character of the query
- `$` : Snap to the end of the query

#### Word Boundaries
- `w` / `W` (Shift) : Jump to the start of the next word / BIG word boundary
- `e` / `E` (Shift) : Jump to the end of the current word / BIG word boundary
- `b` / `B` (Shift) : Jump back to the start of the previous word / BIG word boundary

#### Character Lookup
- `f{char}` : Find the next occurrence of `{char}` forward
- `F{char}` : Find the previous occurrence of `{char}` backward
- `t{char}` : Jump till just before the next occurrence of `{char}`
- `T{char}` : Jump till just after the previous occurrence of `{char}`
- `;` : Repeat the last find lookup in the same direction
- `,` : Repeat the last find lookup in the opposite direction

#### Text Manipulation (Integrated with System Clipboard)
- `x` : Delete the character under the cursor
- `X` (Shift+X) : Delete the character before the cursor (Backspace equivalent)
- `s` : Substitute character (Deletes under cursor and enters Insert mode)
- `S` (Shift+S) : Substitute line (Clears query and enters Insert mode)
- `r{char}` : Replace the character under the cursor with `{char}`
- `~` : Toggle the casing of the character under the cursor
- `gu` / `gU` : Make lowercase / uppercase (operator pending, e.g., `guw` to lower a word)
- `dd` / `cc` : Delete or Change the entire query
- `D` / `C` : Delete or Change from the cursor to the end of the line
- `Y` (Shift+Y) : Yank (Copy) the entire query
- `p` : Paste from system clipboard
- `u` : Undo the last edit (hooks into WPF native undo stack)

#### Text Objects (For Operators & Visual Mode)
Use these immediately after an operator (like `d`, `c`, `y`, `gu`) to target specific text structures.
- **Modifiers**: `i` (inner), `a` (around)
- **Targets**: `w` (word), `"` (double quotes), `'` (single quotes), `(` (parentheses), `[` (brackets), `{` (braces)
- *Example*: `diw` (delete inner word), `ci"` (change inside quotes), `ya(` (yank around parentheses)

#### Mode Switching
- `i` : Enter Insert mode at the cursor
- `I` (Shift+I) : Enter Insert mode at the beginning of the query
- `a` : Enter Insert mode after the cursor
- `A` (Shift+A) : Enter Insert mode at the end of the query
- `v` : Enter Visual mode (char-wise selection)
- `V` (Shift+V) : Enter Visual Line mode (select entire query)

### Visual Mode Keybindings

#### Entering Visual Mode
- `v` : From Normal mode, enter char-wise Visual mode at the cursor
- `V` (Shift+V) : From Normal mode, enter Visual Line mode (selects entire query)

#### Extending the Selection (Visual mode only)
- `h` / `l` : Extend selection left / right
- `w` / `b` / `e` (and `W`/`B`/`E`) : Extend by word boundary
- `0` / `^` / `$` : Extend to beginning / first non-blank / end of query
- `f{char}` / `F{char}` / `t{char}` / `T{char}` : Extend to character lookup
- `;` / `,` : Repeat last character lookup

#### Operators (work on the selected region)
- `x` / `d` : Delete the selection (copies to clipboard)
- `y` : Yank (copy) the selection
- `c` / `s` : Change the selection (delete and enter Insert mode)
- `r{char}` : Replace every character in the selection with `{char}`
- `~` : Toggle casing of every character in the selection
- `gu` / `gU` : Make every character in the selection lowercase / uppercase

#### Mode Transitions
- `Esc` : Return to Normal mode (clears selection)
- `v` : In Visual Line mode, switch to char-wise Visual mode
- `V` : In Visual mode, switch to Visual Line mode (selects entire query)
- `j` / `k` : Navigate search results (same as Normal mode)

---

*(The below is the standard Flow Launcher README)*

# Flow Launcher
Flow Launcher is a quick file search and app launcher for Windows with community-made plugins.

[Website](https://flowlauncher.com) | [Documentation](https://flowlauncher.com/docs/) | [Plugin Store](https://flowlauncher.com/docs/plugins.html)
