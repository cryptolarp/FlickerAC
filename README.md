# FlickerAC

A cutting‑edge, lightweight auto‑clicker for Windows.

## Features

- Left / right click
- Adjustable delay with random offset
- Global hotkey toggle
- Border control (pauses near screen edges)
- Time limit (auto‑stop after X seconds)
- Fixed position clicking (with screen picker)
- Modern dark UI with animations

## Download

Get the latest `.exe` from the [Releases page](../../releases).

## Usage

1. Run `FlickerAC.exe`.
2. Set your preferences.
3. Press **Ctrl+F6** (or your custom hotkey) to start/stop.
4. The app runs completely in the background – no install needed.

## Build from source

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet restore
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
