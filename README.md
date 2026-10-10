# Jump Lock

A Dalamud plugin that disables jumping, either on demand or automatically in places you choose (venues, posing studios, FC houses).

## How it works

The game asks "is the Jump keybind pressed?" through `InputData.IsInputIdPressed / Down / Held`. While locked, the plugin answers "no" for the `JUMP` input (and optionally the controller's `PAD_JUMPANDCANCELCAST`). Jump used from a hotbar or `/generalaction Jump` is caught separately by hooking `ActionManager.UseAction` for GeneralAction 2. Nothing is sent to the server; it only filters local input. Your keybinds stay exactly as they are.

## Features

- **Manual lock**: button in the window, or `/jumplock on`, `/jumplock off`, `/jumplock toggle` (works in macros).
- **Auto lock** when you enter:
  - any housing interior (house, apartment, FC room, private chamber), or
  - specific houses you save with **Add this house** (matched by exact ward/plot/room), or
  - specific zones you save with **Add this zone**.
- **Allow jumping here**: pressing the button while an auto rule is active pauses it until you change zones.
- Optional chat message when the lock turns on or off.

Note: "Add this zone" while *inside* a house isn't offered on purpose, because every house of the same size in a district shares one zone ID. Use "Add this house" for venues.

## Building

Prerequisites: XIVLauncher with Dalamud run at least once, and the .NET SDK that Dalamud.NET.Sdk 15 requires (your IDE usually handles this).

1. Open `JumpLock.slnx` in Visual Studio or Rider (or run `dotnet build` in this folder).
2. The output lands in `JumpLock/bin/x64/Debug/JumpLock.dll`.

## Loading in game

1. `/xlsettings` → **Experimental** → add the full path to `JumpLock.dll` under **Dev Plugin Locations** → save.
2. `/xlplugins` → **Dev Tools → Installed Dev Plugins** → enable Jump Lock.
3. `/jumplock` opens the window.

## If something breaks after a game patch

- Check `/xllog` for `[JumpLock]` lines. The plugin logs which hook failed and keeps running without it.
- Update the `Dalamud.NET.Sdk/x.y.z` version in `JumpLock.csproj` to match the current goatcorp SamplePlugin.
- If hotbar blocking stops working, confirm the Jump row ID in the GeneralAction sheet (`JumpGeneralActionId` in `JumpBlocker.cs`).
