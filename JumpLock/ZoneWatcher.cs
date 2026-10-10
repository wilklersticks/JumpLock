using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace JumpLock;

/// <summary>
/// Figures out whether the player is somewhere an automatic rule says jump should be locked.
/// </summary>
public static unsafe class ZoneWatcher
{
    public readonly record struct Location(
        uint TerritoryId,
        bool InHousing,
        ulong HouseId,
        string Label);

    public static Location Current()
    {
        uint territory = Plugin.ClientState.TerritoryType;
        var hm = HousingManager.Instance();

        if (hm != null && hm->IsInside())
        {
            var id = hm->GetCurrentHouseId();
            return new Location(territory, true, id.Id, DescribeHouse(id, territory));
        }

        return new Location(territory, false, 0, TerritoryName(territory));
    }

    /// <summary>Returns the name of the rule that matched, or null if none did.</summary>
    public static string? MatchingRule(Configuration config, Location loc)
    {
        if (!config.AutoEnabled || loc.TerritoryId == 0)
            return null;

        if (loc.InHousing)
        {
            if (config.AutoInAllHousing)
                return "any housing interior";

            foreach (var h in config.AutoHouses)
                if (h.HouseId == loc.HouseId)
                    return h.Label;
        }

        if (config.AutoTerritories.Contains(loc.TerritoryId))
            return TerritoryName(loc.TerritoryId);

        return null;
    }

    public static string TerritoryName(uint territoryId)
    {
        var row = Plugin.DataManager.GetExcelSheet<TerritoryType>().GetRowOrDefault(territoryId);
        if (row == null)
            return $"Zone {territoryId}";

        var name = row.Value.PlaceName.ValueNullable?.Name.ExtractText();
        return string.IsNullOrWhiteSpace(name) ? $"Zone {territoryId}" : name;
    }

    private static string DescribeHouse(HouseId id, uint territory)
    {
        // The interior's territory name is generic ("Private Cottage - Mist" etc.),
        // so add the address to tell houses apart.
        var zone = TerritoryName(territory);
        var ward = id.WardIndex + 1;

        if (id.IsApartment)
        {
            var wing = id.ApartmentDivision == 0 ? "Main" : "Subdivision";
            return $"{zone} - Ward {ward}, Apartment ({wing}) #{id.RoomNumber}";
        }

        var plot = id.PlotIndex + 1;
        if (id.IsWorkshop)
            return $"{zone} - Ward {ward}, Plot {plot} (Workshop)";
        if (id.RoomNumber > 0)
            return $"{zone} - Ward {ward}, Plot {plot}, Room {id.RoomNumber}";

        return $"{zone} - Ward {ward}, Plot {plot}";
    }
}
