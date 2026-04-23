# Audio Toggle

Minimal Windows 11 tray app built with C# and .NET 8 that toggles between two saved audio output devices.

## Features

- Starts directly in the system tray with no main window on launch
- Single left-click toggles between two saved playback devices
- Double left-click opens a compact settings window
- Uses a 275 ms click delay so single-click toggle and double-click settings do not conflict
- Lists all active render/playback devices and shows current default-role status
- Saves up to two selected device endpoint IDs automatically to `%AppData%\AudioToggle\config.json`
- Sets the default output device for `Console`, `Multimedia`, and `Communications`
- Handles missing or unplugged devices without crashing
- Includes tray menu actions for settings, toggle, refresh, and exit
- Optional "Open on startup" checkbox backed by a shortcut in the current user's Startup folder
- Uses the app icon for the `.exe`, tray icon, and portable shell integration
- Creates a Start Menu shortcut for portable builds so Windows notifications can use the app identity instead of a generic executable icon

## Build

Requirements:

- Windows 11
- .NET 8 SDK or newer

Build from the project folder:

```powershell
dotnet build
```

Create a portable Release build:

```powershell
.\Installer\Build-Portable.ps1
```

The portable output will be under:

```text
Installer\Output\portable\
```

The script also creates a ready-to-share ZIP:

```text
Installer\Output\AudioToggle-portable.zip
```

## Installer

Create the installer:

```powershell
.\Installer\Build-Installer.ps1
```

The installer output will be under:

```text
Installer\Output\installer\
```

Notes:

- The installer script uses Inno Setup 6.
- If `ISCC.exe` is not installed yet, install Inno Setup 6 and rerun the script.
- The portable build step runs automatically before the installer is compiled.

## Run

Run from the project folder:

```powershell
dotnet run
```

Or launch the portable `AudioToggle.exe` from `Installer\Output\portable\`.

After launch:

1. Look for the tray icon in the Windows notification area.
2. Double-click the icon to open settings.
3. Select up to two output devices in the settings window.
4. Single-click the tray icon to toggle between them.

## Portable Integration

Portable builds register a Start Menu shortcut automatically on launch. That gives Windows a stable app identity for notifications and keeps the icon consistent without requiring a full installer.

If you want to repair the shell integration for an existing portable copy:

```powershell
.\Installer\Repair-PortableIntegration.ps1
```

Or point it at a specific portable executable:

```powershell
.\Installer\Repair-PortableIntegration.ps1 -ExecutablePath "C:\Path\To\AudioToggle.exe"
```

## Configuration

Config file location:

```text
%AppData%\AudioToggle\config.json
```

Example:

```json
{
  "selectedDeviceIds": [
    "{DEVICE-ID-A}",
    "{DEVICE-ID-B}"
  ],
  "openOnStartup": false
}
```

Saved device IDs are preserved even if a device is unplugged so the app can use it again if it returns later.

## Notes

- The app uses Windows Core Audio/MMDevice APIs to enumerate playback devices.
- Default-device switching uses the common `IPolicyConfig` COM interop approach.
- No registry editing is used for startup or audio switching.
- No admin rights are required for normal operation.
