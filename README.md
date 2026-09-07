# StayAwake / Не засыпай

Tray-first Windows utility that prevents sleep using the **Power Request API** (with `SetThreadExecutionState` fallback). No mouse/keyboard jiggling, no admin rights, no power-plan changes.

Утилита для Windows (трей), предотвращает сон через **Power Request API** (с запасным `SetThreadExecutionState`). Без эмуляции ввода, без прав администратора, без смены планов питания.

**Copyright © 2026 Vladislav Kravchenko**

## Requirements

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (to build)

## Build / Test / Publish

```bash
dotnet restore StayAwake.sln
dotnet build StayAwake.sln -c Release
dotnet test StayAwake.sln -c Release
dotnet publish StayAwake/StayAwake.csproj -c Release -p:PublishProfile=FolderProfile
dotnet publish StayAwake.Lite/StayAwake.Lite.csproj -c Release -p:PublishProfile=FolderProfile
```

Single-file outputs land in `dist/`:
- `StayAwake.exe` — full tray app
- `StayAwake.Lite.exe` — minimal window-only app

## StayAwake Lite

Minimal companion app (no tray, no rules, no CLI):

1. While the window is open — PC stays awake
2. Toggle **Keep display ON** — also prevent display sleep
3. Bottom timer shows start time and elapsed time
4. Closing the window releases the power request and exits

## CLI

```text
StayAwake --on
StayAwake --off
StayAwake --timer 120
StayAwake --until 23:30
StayAwake --display-on / --display-off
StayAwake --process "ffmpeg"
StayAwake --pid 1234
StayAwake --run "C:\path\tool.exe" --args "--fast"
StayAwake --pause / --resume / --toggle
StayAwake --status
StayAwake --quit
StayAwake --minimized
```

Second instance with **no args** opens the main window (`--show`).
Other args are forwarded via named pipe; `--status` returns a text reply (also written to `%TEMP%\StayAwake.status.txt`).

Hotkeys (defaults): `Ctrl+Alt+A` toggle awake, `Ctrl+Alt+D` toggle keep-display.

## Settings

`%AppData%\StayAwake\settings.json`  
Logs: `%AppData%\StayAwake\Logs\StayAwake.log` (rotated, 5 × 2 MB)

## Modes

| Mode | Behavior |
|------|----------|
| Off | Release power request |
| Indefinite | Keep system awake |
| Timed | Until duration ends |
| Until | Until local HH:mm (next day if past) |
| Rules | Process / CPU / Network / Schedule combined with ANY or AND |
| Pause | Temporarily release request |

Display keep-on is a separate checkbox. Battery: allow on battery; optionally disable below X%.

## Manual test checklist

- [ ] Start app → appears in tray; main window optional
- [ ] Indefinite ON → `powercfg /requests` shows StayAwake (or display/system required)
- [ ] Off / Pause → request cleared
- [ ] Timed 1 min → expires to Off
- [ ] Until past time → schedules next day
- [ ] Keep display on toggles display request independently
- [ ] Battery: set cutoff high while on battery → blocked
- [ ] Process rule: enable for running app → awake; close app → idle (ANY)
- [ ] CPU / Network grace: spike then idle → stays active for grace seconds
- [ ] Schedule overnight window (22:00–06:00)
- [ ] Combined ANY vs ALL
- [ ] Launch under awake (exe/bat/ps1)
- [ ] Hotkeys toggle/pause; conflict shows status
- [ ] Autostart HKCU Run
- [ ] Second instance CLI forwards (`--toggle`)
- [ ] Language RU/EN and theme Light/Dark/System
- [ ] Restart: expired timer not restored; dead PID cleared; name rules kept
- [ ] Exit from tray fully quits

## Architecture notes

- Preferred: `PowerCreateRequest` / `PowerSetRequest`
- Fallback: `SetThreadExecutionState`
- Single instance: named mutex + named pipe IPC
- Poll defaults: process 2s, CPU/net 3s, schedule 15s
