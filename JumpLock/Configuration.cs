using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace JumpLock;

[Serializable]
public class SavedHouse
{
    /// <summary>Raw HouseId value from HousingManager.GetCurrentHouseId().</summary>
    public ulong HouseId { get; set; }
    public string Label { get; set; } = string.Empty;
}

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    // ---- Manual control ----
    /// <summary>Jump is blocked everywhere while this is on.</summary>
    public bool ManualLock { get; set; } = false;

    // ---- Automatic control ----
    /// <summary>Master switch for all automatic rules below.</summary>
    public bool AutoEnabled { get; set; } = true;

    /// <summary>Lock inside any house, apartment, FC room or private chamber.</summary>
    public bool AutoInAllHousing { get; set; } = false;

    /// <summary>Lock inside these specific houses/rooms only (exact address match).</summary>
    public List<SavedHouse> AutoHouses { get; set; } = new();

    /// <summary>Lock in these territories (zone IDs). Note: every house of the same size in a district shares one ID.</summary>
    public List<uint> AutoTerritories { get; set; } = new();

    // ---- What to block ----
    /// <summary>Also block the controller "Jump / Cancel Cast" binding.</summary>
    public bool BlockGamepadJump { get; set; } = true;

    /// <summary>Also block Jump used from a hotbar slot or a macro (/generalaction).</summary>
    public bool BlockHotbarJump { get; set; } = true;

    // ---- Feedback ----
    public bool ChatNotifications { get; set; } = true;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
