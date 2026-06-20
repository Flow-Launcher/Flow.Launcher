# Vim Mode

An **opt-in**, terminal-style Vim editing layer for the Flow Launcher search bar. It turns the query box
into a small modal editor so you can navigate and fix long queries without leaving the home row.

The feature is **disabled by default**. Enable it under **Settings → General → "Enable Advanced Vim Mode"**.
When off, the search bar behaves exactly as it always has.

## Design

The implementation is split into three pieces so the logic stays testable:

| Type | Responsibility |
| --- | --- |
| [`VimEngine`](VimEngine.cs) | The mode state machine (Insert / Normal / Visual / Visual Line) and the `ModeChanged` event. |
| [`VimMotionEngine`](VimMotionEngine.cs) | Pure, side-effect-free caret/range math (motions, text objects, operator ranges). Fully unit-tested. |
| [`VimManager`](VimManager.cs) | Wires the engines into the WPF `MainWindow`: key interception, the block-caret overlay, the mode indicator, and clipboard operations. Implements `IDisposable` and is torn down in `MainWindow.Dispose`. |

Unit tests live in [`Flow.Launcher.Test`](../../Flow.Launcher.Test) (`VimEngineTest`, `VimMotionEngineTest`).

## Modes

- **Mode indicator** — a small, color-coded dot at the left of the search bar shows the current mode:
  accent for **Normal**, purple for **Visual**, orange for **Visual Line**. In Insert mode the dot is hidden.
- **Insert** — the default. Works exactly like the standard search bar (blinking caret).
- **Normal** — a solid block caret; alphanumeric keys are interpreted as commands instead of text.
- **Visual** — character-wise selection; motions extend the selection from a fixed anchor.
- **Visual Line** — selects the whole query; operators apply to all of it.
- **`Esc`** — from Insert, switches to Normal. **Double-`Esc`** (within 400 ms) hides the launcher.
- **`Ctrl`/`Alt` chords pass through untouched**, so existing Flow Launcher hotkeys are unaffected.

## Normal mode

### Navigation
- `h` / `l` — move left / right
- `j` / `k` — move down / up through the search results
- `0` — start of the query · `^` — first non-blank · `$` — end of the query

### Word motions
- `w` / `W` — start of next word / WORD
- `e` / `E` — end of word / WORD
- `b` / `B` — start of previous word / WORD

### Character search
- `f{char}` / `F{char}` — to next / previous `{char}`
- `t{char}` / `T{char}` — till just before / after `{char}`
- `;` / `,` — repeat the last character search, same / opposite direction
- `%` — jump to the matching bracket

### Editing (integrated with the system clipboard)
- `x` / `X` — delete the character under / before the cursor
- `s` / `S` — substitute the character / whole query, then enter Insert
- `r{char}` — replace the character(s) under the cursor with `{char}`
- `~` — toggle the case of the character under the cursor
- `gu` / `gU` — lowercase / uppercase (operator + motion, e.g. `guw`)
- `dd` / `cc` — delete / change the whole query
- `D` / `C` — delete / change from the cursor to the end
- `Y` — yank the whole query · `p` — paste after the cursor
- `u` — undo the last operation · `Ctrl+R` — redo

### Operators + text objects
Use a text object after an operator (`d`, `c`, `y`, `gu`, …):
- modifiers: `i` (inner), `a` (around)
- targets: `w` (word), `"` `'` (quotes), `(` `[` `{` (brackets)
- examples: `diw` (delete inner word), `ci"` (change inside quotes), `ya(` (yank around parens)

### Repeat & counts
- `.` — repeat the last change (e.g. `x`, `dw`, `r{char}`, `p`).
- `{count}` — most motions and operators take a numeric prefix: `3w`, `5x`, `2p`, `d3w` / `3dw`, `3f,`, `2;`.

### Mode switches
- `i` / `I` — insert at the cursor / start of the query
- `a` / `A` — insert after the cursor / at the end of the query
- `v` / `V` — Visual / Visual Line mode

## Visual mode

- Extend the selection with any motion (`h` `l` `w` `b` `e` `0` `^` `$` `f`/`t`/`F`/`T`, `;` `,`).
- Operators on the selection: `d` / `x` (delete), `y` (yank), `c` / `s` (change), `r{char}` (replace),
  `~` (toggle case), `gu` / `gU` (lower / upper).
- `i` / `a` start a text object (e.g. `vi(`), `o` swaps the selection ends.
- `v` toggles between Visual and Visual Line; `Esc` returns to Normal; `j` / `k` navigate results.

## Known limitations

- **Keyboard layout** — character-pending commands (`f`/`F`/`t`/`T`, `r`, and the quote/bracket text
  objects) reconstruct the target character from the physical key, assuming a US-QWERTY layout. Letters and
  digits work on any layout; some symbols/punctuation may resolve incorrectly on others.
- **Dot-repeat of inserts** — `.` replays the operator/motion of the last change but not text typed in
  Insert mode, so `cwfoo<Esc>.` re-deletes a word without re-typing `foo`.
- **Single line** — the query is a single line, so line-wise commands (`dd`, `cc`, `V`, `0`/`$`) act on the
  whole query and `gg`/`G` are not bound.
