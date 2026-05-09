---
name: terminal-gui-v2
description: Apply Terminal.Gui v2 best practices when creating or editing UI code in this project. Trigger whenever you touch files under Spotify2MP3.NET/UI/, Program.cs, the .csproj's Terminal.Gui reference, or write any code that imports `Terminal.Gui.*` or uses View, Window, Dialog, Button, TextField, Label, CheckBox, ListView, Pos, Dim, Application, IApplication, Scheme, SchemeManager, KeyBindings, Command, Accepting/Activating events. Use this when planning a new dialog/window, refactoring layout, wiring keys, handling background work, or updating themes. Skip for pure-logic changes under Core/ or Models/ that have no Terminal.Gui types.
---

# Terminal.Gui v2 — Project Skill

This project targets `Terminal.Gui` **2.1.0** (`Spotify2MP3.NET/Spotify2MP3.NET.csproj`). Use this skill whenever you touch UI code so the result follows v2 idioms and stays consistent with the rest of the codebase.

## 0. Project conventions (read first)

The codebase uses the **instance-based v2 API**. Don't reach for v1-style static helpers (`Application.Init/Shutdown`, `Colors.ColorSchemes`, `ColorScheme`) — they don't exist here.

- Bootstrap pattern (see `Spotify2MP3.NET/Program.cs`): `using IApplication app = Application.Create().Init(); app.Run(mainWindow);` — `Dispose()` runs automatically.
- Schemes are registered once in `Program.RegisterSchemes()` under three names: **`"Base"`** (main windows), **`"Dialog"`** (modal dialogs), **`"Error"`** (error UIs). Views select one via `SchemeName = "Dialog";` — never construct ad-hoc `Scheme` instances inside views.
- File style: `_camelCase` `private readonly` fields, view tree built imperatively in the constructor. `MainWindow` uses a running `y` counter for vertical layout — keep it when extending that file. `SettingsDialog` uses chained `Pos.Bottom(prev)` — keep that style there.
- Quit key is the v2 default (**Esc**). Don't add a custom `QuitKey` setter — the v2 API for that is `Application.SetDefaultKeyBinding(Command.Quit, ...)` and we don't currently customize it.
- The default constructor on `Window`/`Dialog` takes no args; do **not** write `: base()`.

## 1. Namespaces (v2 split)

`using Terminal.Gui;` no longer brings everything in. Pick from these six sub-namespaces:

| Need | Namespace |
|---|---|
| `Application`, `IApplication`, `IRunnable`, `Runnable<T>` | `Terminal.Gui.App` |
| `View`, `Pos`, `Dim`, `Alignment`, adornments | `Terminal.Gui.ViewBase` |
| `Window`, `Dialog`, `Button`, `Label`, `TextField`, `CheckBox`, `CheckState`, `ListView`, `ProgressBar`, `OpenDialog`, `OpenMode`, `IAllowedType`, `AllowedType` | `Terminal.Gui.Views` |
| `Scheme`, `Attribute`, `Color`, `Thickness`, `Glyphs` | `Terminal.Gui.Drawing` |
| `Key`, `KeyBindings`, `Command`, `CommandEventArgs`, `Mouse`, `MouseBindings` | `Terminal.Gui.Input` |
| `SchemeManager`, `ConfigurationManager`, `ThemeManager` | `Terminal.Gui.Configuration` |

**Pitfall**: `Terminal.Gui.Drawing.Attribute` clashes with `System.Attribute`. When constructing one, fully qualify: `new Terminal.Gui.Drawing.Attribute(Color.Gray, Color.Black)`.

## 2. v1→v2 API renames (the ones that bit us)

| v1 | v2 |
|---|---|
| `ColorScheme` (class) | `Scheme` (immutable) |
| `Colors.ColorSchemes["Base"]` | `SchemeManager.GetScheme("Base")` / `SchemeName = "Base"` |
| `view.ColorScheme = …` | `view.Scheme = …` (typed) **or** `view.SchemeName = "Base"` (string, inherits) |
| `Application.Init()` / `Application.Shutdown()` | `using IApplication app = Application.Create().Init();` |
| `Application.Run(top)` (static) | `app.Run(top)` (instance) |
| `Application.RequestStop()` (static, from inside a view) | `App!.RequestStop()` |
| `Application.Invoke(...)` (static) | `App!.Invoke(...)` (capture `IApplication app = App!;` once if used many times) |
| `Application.QuitKey = key` | `Application.SetDefaultKeyBinding(Command.Quit, …)` — usually unnecessary |
| `CheckBox.CheckedState` | `CheckBox.Value` (type `CheckState`) |
| `ListView.AllowsMarking` | `ListView.ShowMarks` (default `false`) |
| `Pos.At(n)` | `Pos.Absolute(n)` |
| `Dim.Sized(n)` | `Dim.Absolute(n)` |
| `Button.Clicked` | `Button.Accepting` (CWP — set `e.Handled = true`) |
| `Rect` | `Rectangle` (BCL) |
| `Bounds` | `Viewport` |

**Default change to remember**: `View.CanFocus` defaults to `false` in v2. If you build a custom view that takes keyboard input, opt in explicitly. Built-in views (Button, TextField, etc.) already opt in.

## 3. Lifecycle

- Bootstrap belongs in `Program.cs` only. Don't call `Application.Create()` from anywhere else.
- The top-level window is constructed by the caller and runs via `app.Run(window)`. Wrap it in `using` so disposal is deterministic — the framework only auto-disposes runnables it constructs (i.e. the generic `app.Run<T>()` overload).
- For dialogs (`OpenDialog`, custom `Dialog` subclasses) created inside a view: `using var dlg = new Foo(); App!.Run(dlg);` — caller-owned ⇒ explicit dispose.
- To close a dialog from inside a button handler: `App!.RequestStop();` (the `App` property is set once `BeginInit` runs — by the time any event handler fires, it is non-null, so `App!` is safe).

## 4. Threading

- The input thread is owned by `IApplication` and started in `Init()`.
- **All view mutation must happen on the UI thread.** Background work re-enters via `App!.Invoke(() => …)`. See `MainWindow.StartConversion` for the canonical pattern: capture `IApplication app = App!;` at the top of the async method, then close over `app` in callbacks passed to `Downloader`.
- Long-running work (Spotify scrape, ffmpeg, downloads) belongs in `Core/`. Services emit progress via plain delegates/events; the view supplies callbacks that wrap their body in `app.Invoke(...)`.
- Modality is enforced by the session stack: only the top runnable receives input. Don't manually disable parent controls when opening a dialog.
- Subscribed events on long-lived services must be unsubscribed before the view is disposed — otherwise the service holds a dead view alive.

## 5. Layout — never hardcode coordinates

Use `Pos` / `Dim` for everything that should reflow. Acceptable absolute values: `1` for inset, `0` for edge.

| Need | Use |
|---|---|
| Field next to a label | `X = Pos.Right(label) + 1` |
| Stretch field to last button | `Width = Dim.Fill(to: btn)` or `Dim.Fill() - <reserved>` |
| Stack rows | `Y = Pos.Bottom(prevView)` (or `+1` for a blank line) |
| Right-anchored button | `X = Pos.AnchorEnd()` (needs `Width` set) |
| Center a single control | `X = Pos.Center()` |
| Aligned button row | `X = Pos.Align(Alignment.Center)` (or `End`) on every button |
| Auto-size a dialog to content | `Width = Dim.Auto(minimumContentDim: N, maximumContentDim: Dim.Percent(80))` |
| Anchor to bottom edge | `Y = Pos.AnchorEnd(1)` |

In v2 `Dim.Fill()` returns a non-nullable `Dim`, so write `Dim.Fill() - 15`, not `(Dim.Fill() ?? 0) - 15`.

**Adornments** (`Margin`, `Border`, `Padding`) consume Frame space and offset the Viewport. When a `Dim.Auto` window centers itself, subtract `GetAdornmentsThickness().Horizontal/Vertical` from the max so borders don't get clipped.

## 6. Keyboard & commands

- Hotkeys come from `_` in `Text` / `Title`. Examples here: `"_Convert Playlist"` (Alt+C), `"_Browse"` / `"Br_owse"` (Alt+B / Alt+O — different letters because two browse buttons), `"_Settings"` (Alt+S), `"Generate _M3U"` (Alt+M), `"Sa_fe Mode"` (Alt+F). **Pick distinct letters within the same window** — collisions mean only the first one fires.
- Set `IsDefault = true` on the primary action button so `Enter` activates it from anywhere in the form (`_convertBtn`, `saveBtn`, dialog `_OK`).
- For non-default actions prefer `KeyBindings.Add(key, Command.X)` + `AddCommand(Command.X, ctx => { ...; return true; })` over a raw `KeyDown` handler.
- "If it's hot, it works": if a hotkey is visible, the binding must function whenever the view is visible — never gate it behind focus.

## 7. Events — Cancellable Work Pattern (CWP)

v2's protocol everywhere is:

```
1. virtual OnXxx(args)   ← subclass cancels first
2. Xxx event             ← subscriber cancels next
3. default behavior      ← only if neither cancelled
```

**Naming is not optional:**

| Phase | Event | Virtual |
|---|---|---|
| Pre-change | `TextChanging` | `OnTextChanging` |
| Post-change | `TextChanged` | `OnTextChanged` |
| Pre-action | `Accepting`, `Activating` | `OnAccepting`, `OnActivating` |
| Post-action | `Accepted`, `Activated` | `OnAccepted`, `OnActivated` |

Inside a subscriber, set `e.Handled = true` to short-circuit. Never invent a `Cancel` flag. Every `Accepting` handler in this codebase sets `e.Handled = true` — match that.

When raising your own CWP event in a custom view:

```csharp
protected void RaiseSomething(SomeEventArgs args)
{
    if (OnSomething(args)) return;          // virtual cancels
    Something?.Invoke(this, args);
    if (args.Handled) return;               // subscriber cancels
    DoDefault();
}
```

## 8. Focus & navigation

- Focusable iff `Visible && Enabled && CanFocus`. For keyboard reach also need `TabStop != NoStop`.
- `TabBehavior.TabGroup` on container panels you want users to jump between with **F6 / Shift+F6**. `TabStop` on individual interactive widgets.
- `SetFocus()` returns `bool` — check it if the focus change matters; don't blindly assume success.
- Don't call `SetFocus` in a constructor — use `Initialized` so the view tree is ready.

## 9. Theming

The codebase has three registered schemes — `"Base"`, `"Dialog"`, `"Error"` — built in `Program.RegisterSchemes()`. `Scheme` is **immutable**, so use the object initializer with the visual-role properties:

```csharp
new Scheme
{
    Normal     = new Terminal.Gui.Drawing.Attribute(Color.Gray, Color.Black),
    Focus      = new Terminal.Gui.Drawing.Attribute(Color.White, Color.DarkGray),
    HotNormal  = new Terminal.Gui.Drawing.Attribute(Color.BrightCyan, Color.Black),
    HotFocus   = new Terminal.Gui.Drawing.Attribute(Color.BrightCyan, Color.DarkGray),
    Disabled   = new Terminal.Gui.Drawing.Attribute(Color.DarkGray, Color.Black),
}
```

Other available roles (use only if needed): `Active`, `Highlight`, `Editable`, `ReadOnly`, `HotActive`, `Code`. Unset roles are derived from `Normal`.

Views select a scheme by name: `SchemeName = "Dialog";` — string-based, inherits to subviews unless they override.

If a new role is genuinely needed (e.g. `"Success"`), add it to `Program.RegisterSchemes()` and reference by name. Don't add `ConfigurationManager` — see §10.

## 10. Configuration (do NOT use `ConfigurationManager`)

The project has its own `Models/Config.cs` that handles persistence. Do **not** declare `[ConfigurationProperty]` statics — that creates two competing config systems. New settings extend the existing `Config` class. Theme persistence and user-overridable keybindings would be the trigger to revisit this; treat that as a separate, deliberate task.

## 11. Project structure rules

```
Spotify2MP3.NET/
├─ Program.cs            # IApplication bootstrap + scheme registration
├─ Core/                 # Pure logic. NO Terminal.Gui references allowed.
├─ Models/               # DTOs / records. NO Terminal.Gui references allowed.
└─ UI/                   # Windows, dialogs, custom views ONLY here.
```

- **No `using Terminal.Gui.*;` in `Core/` or `Models/`.** If you need it there, surface a service interface or callback that the UI subscribes to.
- Background services emit progress via `Action<…>`, never reach into a `View`.
- Dialogs that produce a result expose it via a public property read by the caller after `App!.Run(dialog)` returns. If you need the framework's `Runnable<TResult>` + `app.GetResult<T>()` pattern, that's also fine but currently unused — keep new code consistent with what's there unless you migrate everything.

## 12. Common pitfalls — check before submitting

- [ ] No background-thread callback writes to a `View` without `App!.Invoke`.
- [ ] No hardcoded widths/heights for resizable content; `Pos`/`Dim` used.
- [ ] Hotkeys (`_` letters) don't collide within the same window.
- [ ] Primary button has `IsDefault = true`.
- [ ] Dialogs have `SchemeName = "Dialog"`, not a freshly-built `Scheme`.
- [ ] `Accepting`/`Activating` handlers set `e.Handled = true`.
- [ ] Caller-created dialogs are wrapped in `using var` and run with `App!.Run(...)`.
- [ ] No `Application.Init()` / `Application.Run(...)` static calls outside `Program.cs`.
- [ ] No `ColorScheme` / `Colors.ColorSchemes` — use `Scheme` / `SchemeManager` / `SchemeName`.
- [ ] No `using Terminal.Gui.*;` leaked into `Core/` or `Models/`.
- [ ] No `Terminal.Gui.Attribute` ambiguity — fully qualify as `Terminal.Gui.Drawing.Attribute`.
- [ ] No `(Dim.Fill() ?? 0)` legacy idiom — `Dim.Fill()` is non-nullable in v2.
- [ ] Check renamed members: `CheckBox.Value` (not `CheckedState`), `ListView.ShowMarks` (not `AllowsMarking`).

## Reference

Authoritative docs (fetch when a detail isn't covered above):

- [Application](https://gui-cs.github.io/Terminal.Gui/docs/application.html)
- [View](https://gui-cs.github.io/Terminal.Gui/docs/View.html)
- [Layout](https://gui-cs.github.io/Terminal.Gui/docs/layout.html)
- [Keyboard](https://gui-cs.github.io/Terminal.Gui/docs/keyboard.html)
- [Events / CWP](https://gui-cs.github.io/Terminal.Gui/docs/events.html)
- [Navigation](https://gui-cs.github.io/Terminal.Gui/docs/navigation.html)
- [Configuration](https://gui-cs.github.io/Terminal.Gui/docs/config.html)
- [v1→v2 Migration](https://gui-cs.github.io/Terminal.Gui/docs/migratingfromv1.html)
