namespace Crystal.Assets.Maps;

/// <summary>
/// <c>Libraries.MapLibs[index]</c> → Data-tree relative path, matching
/// <c>Client/MirGraphics/MLibrary.cs</c>. Used to resolve bake-catalog sprites
/// or runtime <c>.Lib</c> files. Does not invent art.
/// </summary>
public static class MapLibraryIndex
{
    public static string? RelativePath(int index)
    {
        if (index < 0 || index >= 400)
            return null;

        if (index == 0) return "Map/WemadeMir2/Tiles.Lib";
        if (index == 1) return "Map/WemadeMir2/Smtiles.Lib";
        if (index == 2) return "Map/WemadeMir2/Objects.Lib";
        if (index is >= 3 and <= 28)
            return $"Map/WemadeMir2/Objects{index - 1}.Lib";
        if (index == 90) return "Map/WemadeMir2/Objects_32bit.Lib";

        if (index == 100) return "Map/ShandaMir2/Tiles.Lib";
        if (index is >= 101 and <= 109)
            return $"Map/ShandaMir2/Tiles{index - 99}.Lib";
        if (index == 110) return "Map/ShandaMir2/SmTiles.Lib";
        if (index is >= 111 and <= 119)
            return $"Map/ShandaMir2/SmTiles{index - 109}.Lib";
        if (index == 120) return "Map/ShandaMir2/Objects.Lib";
        if (index is >= 121 and <= 150)
            return $"Map/ShandaMir2/Objects{index - 119}.Lib";
        if (index == 190) return "Map/ShandaMir2/AniTiles1.Lib";

        if (index is >= 200 and <= 274)
        {
            int state = (index - 200) / 15;
            int slot = (index - 200) % 15;
            string[] folders = { "", "wood/", "sand/", "snow/", "forest/" };
            string[] names =
            {
                "Tilesc", "Tiles30c", "Tiles5c", "Smtilesc", "Housesc", "Cliffsc",
                "Dungeonsc", "Innersc", "Furnituresc", "Wallsc", "smObjectsc",
                "Animationsc", "Object1c", "Object2c"
            };
            if (state >= folders.Length || slot >= names.Length)
                return null;
            return $"Map/WemadeMir3/{folders[state]}{names[slot]}.Lib";
        }

        if (index is >= 300 and <= 374)
        {
            int state = (index - 300) / 15;
            int slot = (index - 300) % 15;
            string[] suffixes = { "", "wood", "sand", "snow", "forest" };
            string[] names =
            {
                "Tilesc", "Tiles30c", "Tiles5c", "Smtilesc", "Housesc", "Cliffsc",
                "Dungeonsc", "Innersc", "Furnituresc", "Wallsc", "smObjectsc",
                "Animationsc", "Object1c", "Object2c"
            };
            if (state >= suffixes.Length || slot >= names.Length)
                return null;
            return $"Map/ShandaMir3/{names[slot]}{suffixes[state]}.Lib";
        }

        return null;
    }
}
