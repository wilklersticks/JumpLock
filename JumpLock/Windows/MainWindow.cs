using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;

namespace JumpLock.Windows;

public class MainWindow : Window
{
    private readonly Plugin plugin;
    private Configuration Config => plugin.Configuration;

    public MainWindow(Plugin plugin) : base("Jump Lock###JumpLockMain")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(360, 300),
            MaximumSize = new Vector2(900, 1200),
        };
        Size = new Vector2(420, 520);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        DrawStatus();
        ImGui.Separator();

        if (ImGui.BeginTabBar("##jumplocktabs"))
        {
            if (ImGui.BeginTabItem("Auto zones"))
            {
                DrawAutoTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Options"))
            {
                DrawOptionsTab();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    // ---------------------------------------------------------------- status

    private void DrawStatus()
    {
        var locked = plugin.IsLocked;

        ImGui.TextUnformatted("Jump is");
        ImGui.SameLine();
        ImGui.TextColored(locked ? ImGuiColors.HealerGreen : ImGuiColors.DalamudGrey,
            locked ? "LOCKED" : "allowed");

        if (locked)
        {
            ImGui.SameLine();
            ImGui.TextDisabled(Config.ManualLock ? "(manual)" : $"(auto: {plugin.AutoRule})");
        }
        else if (plugin.AutoSnoozed)
        {
            ImGui.SameLine();
            ImGui.TextDisabled("(auto paused until you leave this zone)");
        }

        var buttonLabel = locked ? "Allow jumping" : "Lock jump";
        if (ImGui.Button(buttonLabel, new Vector2(-1, 32 * ImGuiHelpers_Scale())))
            plugin.Toggle();

        if (!plugin.Blocker.InputHooksOk)
        {
            ImGui.TextColored(ImGuiColors.DalamudRed,
                "Could not hook the game's input functions. This usually means a game patch moved them; check /xllog.");
        }

        ImGui.TextDisabled("Tip: /jumplock toggle works in macros too.");
    }

    // ---------------------------------------------------------------- auto tab

    private void DrawAutoTab()
    {
        var auto = Config.AutoEnabled;
        if (ImGui.Checkbox("Lock automatically in the places below", ref auto))
        {
            Config.AutoEnabled = auto;
            Save();
        }

        using var disabled = new DisabledScope(!Config.AutoEnabled);

        var allHousing = Config.AutoInAllHousing;
        if (ImGui.Checkbox("Inside any house, apartment, FC room or chamber", ref allHousing))
        {
            Config.AutoInAllHousing = allHousing;
            Save();
        }

        ImGui.Spacing();
        var loc = plugin.Location;
        ImGui.TextDisabled("You are in:");
        ImGui.SameLine();
        ImGui.TextWrapped(loc.Label);

        // Add-current buttons
        if (loc.InHousing)
        {
            var already = Config.AutoHouses.Any(h => h.HouseId == loc.HouseId);
            using (new DisabledScope(already))
            {
                if (ImGui.Button(already ? "This house is saved" : "Add this house"))
                {
                    Config.AutoHouses.Add(new SavedHouse { HouseId = loc.HouseId, Label = loc.Label });
                    Save();
                }
            }
            ImGui.SameLine();
            ImGui.TextDisabled("(this exact address only)");
        }
        else if (loc.TerritoryId != 0)
        {
            var already = Config.AutoTerritories.Contains(loc.TerritoryId);
            using (new DisabledScope(already))
            {
                if (ImGui.Button(already ? "This zone is saved" : "Add this zone"))
                {
                    Config.AutoTerritories.Add(loc.TerritoryId);
                    Save();
                }
            }
        }

        ImGui.Spacing();
        ImGui.Separator();

        // Saved houses
        ImGui.TextUnformatted($"Saved houses ({Config.AutoHouses.Count})");
        if (Config.AutoHouses.Count == 0)
            ImGui.TextDisabled("  Walk into a venue or studio and press \"Add this house\".");

        for (var i = 0; i < Config.AutoHouses.Count; i++)
        {
            ImGui.PushID($"house{i}");
            if (ImGui.SmallButton("Remove"))
            {
                Config.AutoHouses.RemoveAt(i);
                Save();
                ImGui.PopID();
                break;
            }
            ImGui.SameLine();
            ImGui.TextWrapped(Config.AutoHouses[i].Label);
            ImGui.PopID();
        }

        ImGui.Spacing();

        // Saved zones
        ImGui.TextUnformatted($"Saved zones ({Config.AutoTerritories.Count})");
        if (Config.AutoTerritories.Count == 0)
            ImGui.TextDisabled("  Useful for open-world spots or instanced areas.");

        for (var i = 0; i < Config.AutoTerritories.Count; i++)
        {
            ImGui.PushID($"zone{i}");
            if (ImGui.SmallButton("Remove"))
            {
                Config.AutoTerritories.RemoveAt(i);
                Save();
                ImGui.PopID();
                break;
            }
            ImGui.SameLine();
            var id = Config.AutoTerritories[i];
            ImGui.TextWrapped($"{ZoneWatcher.TerritoryName(id)}  [{id}]");
            ImGui.PopID();
        }
    }

    // ---------------------------------------------------------------- options tab

    private void DrawOptionsTab()
    {
        var pad = Config.BlockGamepadJump;
        if (ImGui.Checkbox("Also block controller jump", ref pad))
        {
            Config.BlockGamepadJump = pad;
            Save();
        }
        ImGui.TextDisabled("  On controller, this button also cancels casts; that's blocked too while locked.");

        var hotbar = Config.BlockHotbarJump;
        if (ImGui.Checkbox("Also block Jump from hotbars and macros", ref hotbar))
        {
            Config.BlockHotbarJump = hotbar;
            Save();
        }
        if (!plugin.Blocker.ActionHookOk)
            ImGui.TextColored(ImGuiColors.DalamudOrange, "  Hotbar blocking is unavailable (hook failed).");

        var chat = Config.ChatNotifications;
        if (ImGui.Checkbox("Print a chat message when the lock turns on or off", ref chat))
        {
            Config.ChatNotifications = chat;
            Save();
        }
    }

    // ---------------------------------------------------------------- helpers

    private void Save()
    {
        Config.Save();
        plugin.RefreshLocation(); // apply rule changes immediately
    }

    private static float ImGuiHelpers_Scale() => Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;

    /// <summary>Greys out everything drawn inside a using-block.</summary>
    private readonly struct DisabledScope : IDisposable
    {
        private readonly bool active;
        public DisabledScope(bool disabled)
        {
            active = disabled;
            if (active) ImGui.BeginDisabled();
        }
        public void Dispose()
        {
            if (active) ImGui.EndDisabled();
        }
    }
}
