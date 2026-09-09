namespace Crystal.Assets;

/// <summary>
/// Expected Client <c>Libraries</c> / <c>Settings</c> Data-tree slots.
/// Used to report gaps against a real corpus without inventing art.
/// </summary>
public static class CrystalCatalog
{
    public static readonly string[] RootLibraries =
    {
        "ChrSel.Lib", "Prguse.Lib", "Prguse2.Lib", "Prguse3.Lib", "UI_32bit.Lib",
        "BuffIcon.Lib", "Help.Lib", "MMap.Lib", "MapLinkIcon.Lib", "Title.Lib",
        "MagIcon.Lib", "MagIcon2.Lib", "Magic.Lib", "Magic2.Lib", "Magic3.Lib",
        "Effect.Lib", "MagicC.Lib", "GuildSkill.Lib", "Weather.Lib", "Background.Lib",
        "Dragon.Lib", "Items.Lib", "StateItem.Lib", "DNItems.Lib", "Items_Tooltip_32bit.Lib",
        "Deco.Lib"
    };

    public static readonly (string Folder, string Prefix, int Width)[] NumberedFolders =
    {
        ("CArmour", "00", 2),
        ("CHair", "00", 2),
        ("CWeapon", "00", 2),
        ("CWeaponEffect", "00", 2),
        ("CHumEffect", "00", 2),
        ("AArmour", "00", 2),
        ("AHair", "00", 2),
        ("AHumEffect", "00", 2),
        ("ARArmour", "00", 2),
        ("ARHair", "00", 2),
        ("ARWeapon", "00", 2),
        ("ARHumEffect", "00", 2),
        ("Monster", "000", 3),
        ("Gate", "00", 2),
        ("Flag", "00", 2),
        ("Siege", "00", 2),
        ("NPC", "00", 2),
        ("Mount", "00", 2),
        ("Fishing", "00", 2),
        ("Pet", "00", 2),
        ("Transform", "00", 2),
        ("TransformRide2", "00", 2),
        ("TransformEffect", "00", 2),
        ("TransformWeaponEffect", "00", 2)
    };

    public static IReadOnlyList<string> MapLibraryRelativePaths()
    {
        var list = new List<string>();

        list.Add("Map/WemadeMir2/Tiles.Lib");
        list.Add("Map/WemadeMir2/Smtiles.Lib");
        list.Add("Map/WemadeMir2/Objects.Lib");
        for (int i = 2; i < 28; i++)
            list.Add($"Map/WemadeMir2/Objects{i}.Lib");
        list.Add("Map/WemadeMir2/Objects_32bit.Lib");

        list.Add("Map/ShandaMir2/Tiles.Lib");
        for (int i = 1; i < 10; i++)
            list.Add($"Map/ShandaMir2/Tiles{i + 1}.Lib");
        list.Add("Map/ShandaMir2/SmTiles.Lib");
        for (int i = 1; i < 10; i++)
            list.Add($"Map/ShandaMir2/SmTiles{i + 1}.Lib");
        list.Add("Map/ShandaMir2/Objects.Lib");
        for (int i = 1; i < 31; i++)
            list.Add($"Map/ShandaMir2/Objects{i + 1}.Lib");
        list.Add("Map/ShandaMir2/AniTiles1.Lib");

        string[] wemadeMir3 = { "", "wood/", "sand/", "snow/", "forest/" };
        string[] names =
        {
            "Tilesc", "Tiles30c", "Tiles5c", "Smtilesc", "Housesc", "Cliffsc",
            "Dungeonsc", "Innersc", "Furnituresc", "Wallsc", "smObjectsc",
            "Animationsc", "Object1c", "Object2c"
        };
        foreach (string state in wemadeMir3)
        {
            foreach (string name in names)
                list.Add($"Map/WemadeMir3/{state}{name}.Lib");
        }

        string[] shandaMir3 = { "", "wood", "sand", "snow", "forest" };
        foreach (string state in shandaMir3)
        {
            foreach (string name in names)
                list.Add($"Map/ShandaMir3/{name}{state}.Lib");
        }

        return list;
    }

    /// <summary>
    /// Static root + map slots. Numbered folders are measured from files that actually exist
    /// (Client sizes those arrays from the last numeric file on disk).
    /// </summary>
    public static IReadOnlyList<string> ExpectedRelativePaths()
    {
        var list = new List<string>(RootLibraries);
        list.AddRange(MapLibraryRelativePaths());
        return list;
    }
}
