# ShowHideOpen (Open‑Source Replacement for ZardsSoftware ShowHide 2.2)

A tiny open‑source Windows tray app that lets you **show/hide desktop icons by double‑clicking on the empty desktop**—
just like the original ShowHide 2.2. It also includes a tray icon with quick actions.

## Features

- Double‑click on the empty desktop to toggle desktop icons
- Tray menu: **Toggle Now**, **Start with Windows** (toggle), **Exit**
- Lightweight, no installer, no admin required
- Written in C# (.NET, WinForms), MIT‑licensed

> Note: This only toggles the **desktop icons** (the `SysListView32` of the desktop). It does **not** change Explorer “hidden files” settings.

## Build

1. Open the solution in Visual Studio 2022+ or run:

   ```powershell
   cd src/ShowHideOpen
   dotnet restore
   dotnet build -c Release
   ```

2. The output binary will be in `bin/Release/net8.0-windows` (or your selected TFM).

## Run at Startup

Use the tray menu **Start with Windows** to add/remove a `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` entry.

## Notes

- Double‑click detection ignores icons: it only toggles when you double‑click a *blank* area of the desktop.
- If you use third‑party desktop managers, behavior may vary.
