namespace Crystal.Assets.Maps;

/// <summary>
/// One map cell. Fields match <c>Client.MirObjects.CellInfo</c> (no MapObject list —
/// Linux draws objects from Shared packets, not the WinForms scene graph).
/// </summary>
public sealed class MapCell
{
    public short BackIndex;
    public int BackImage;
    public short MiddleIndex;
    public int MiddleImage;
    public short FrontIndex;
    public int FrontImage;

    public byte DoorIndex;
    public byte DoorOffset;

    public byte FrontAnimationFrame;
    public byte FrontAnimationTick;

    public byte MiddleAnimationFrame;
    public byte MiddleAnimationTick;

    public short TileAnimationImage;
    public short TileAnimationOffset;
    public byte TileAnimationFrames;

    public byte Light;
    public byte Unknown;
    public bool FishingCell;
}
