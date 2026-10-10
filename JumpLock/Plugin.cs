using System;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using JumpLock.Windows;

namespace JumpLock;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInterop { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/jumplock";

    public Configuration Configuration { get; }
    public JumpBlocker Blocker { get; }

    public readonly WindowSystem WindowSystem = new("JumpLock");
    private MainWindow MainWindow { get; }

    // ---- Live state (not saved) ----
    public ZoneWatcher.Location Location { get; private set; }
    /// <summary>Name of the auto rule matching the current location, or null.</summary>
    public string? AutoRule { get; private set; }
    /// <summary>User chose "allow jumping here" while an auto rule was active. Clears on zone change.</summary>
    public bool AutoSnoozed { get; private set; }
    public bool IsLocked => Configuration.ManualLock || (AutoRule != null && !AutoSnoozed);

    private uint lastTerritory;
    private bool lastLocked;
    private DateTime nextZoneCheck = DateTime.MinValue;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Blocker = new JumpBlocker(Configuration);

        MainWindow = new MainWindow(this);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Jump Lock. Also: /jumplock on | off | toggle"
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleMainUi;
        Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();
        CommandManager.RemoveHandler(CommandName);

        // Unhook last so the game never sees a half-torn-down plugin.
        Blocker.Dispose();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!ClientState.IsLoggedIn)
        {
            Blocker.Active = false;
            return;
        }

        // Zone lookups are cheap, but there's no need to do them every frame.
        if (DateTime.UtcNow >= nextZoneCheck)
        {
            nextZoneCheck = DateTime.UtcNow.AddMilliseconds(250);
            RefreshLocation();
        }

        var locked = IsLocked;
        Blocker.Active = locked;

        if (locked != lastLocked)
        {
            lastLocked = locked;
            Notify(locked
                ? $"Jump locked{(Configuration.ManualLock ? "" : $" ({AutoRule})")}."
                : "Jump unlocked.");
        }
    }

    public void RefreshLocation()
    {
        Location = ZoneWatcher.Current();

        if (Location.TerritoryId != lastTerritory)
        {
            lastTerritory = Location.TerritoryId;
            AutoSnoozed = false; // a fresh zone gets a fresh decision
        }

        AutoRule = ZoneWatcher.MatchingRule(Configuration, Location);
    }

    /// <summary>One button / command that does the obvious thing from any state.</summary>
    public void Toggle()
    {
        if (IsLocked) Unlock();
        else Lock();
    }

    public void Lock()
    {
        if (AutoRule != null && AutoSnoozed)
        {
            AutoSnoozed = false; // re-arm the auto rule instead of forcing manual mode
            return;
        }

        Configuration.ManualLock = true;
        Configuration.Save();
    }

    public void Unlock()
    {
        if (Configuration.ManualLock)
        {
            Configuration.ManualLock = false;
            Configuration.Save();
        }

        if (AutoRule != null)
            AutoSnoozed = true; // allow jumping here until you leave the zone
    }

    private void Notify(string message)
    {
        if (Configuration.ChatNotifications)
            Chat.Print(message, "JumpLock");
    }

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "on":
                Lock();
                break;
            case "off":
                Unlock();
                break;
            case "toggle":
                Toggle();
                break;
            default:
                MainWindow.Toggle();
                break;
        }
    }

    public void ToggleMainUi() => MainWindow.Toggle();
}
