namespace Crystal.Assets.Maps;

/// <summary>
/// Crystal <c>.map</c> loader. Logic matches <c>Client.MirObjects.MapReader</c>
/// (types 0–7 and 100) without WinForms / <c>MapObject</c> dependencies.
/// </summary>
public sealed class MapReader
{
    public int Width { get; private set; }
    public int Height { get; private set; }
    public MapCell[,] Cells { get; private set; } = new MapCell[0, 0];
    public string FileName { get; }
    public bool LoadedFromFile { get; private set; }

    public MapReader(string fileName)
    {
        FileName = fileName;
        Initiate();
    }

    void Initiate()
    {
        if (!File.Exists(FileName))
        {
            Width = 0;
            Height = 0;
            Cells = new MapCell[0, 0];
            return;
        }

        byte[] bytes = File.ReadAllBytes(FileName);
        if (bytes.Length < 4)
            return;

        LoadedFromFile = true;

        if (bytes[2] == 0x43 && bytes[3] == 0x23)
        {
            LoadMapType100(bytes);
            return;
        }

        if (bytes[0] == 0)
        {
            LoadMapType5(bytes);
            return;
        }

        if (bytes[0] == 0x0F && bytes.Length > 14 && bytes[5] == 0x53 && bytes[14] == 0x33)
        {
            LoadMapType6(bytes);
            return;
        }

        if (bytes[0] == 0x15 && bytes.Length > 19 && bytes[4] == 0x32 && bytes[6] == 0x41 && bytes[19] == 0x31)
        {
            LoadMapType4(bytes);
            return;
        }

        if (bytes[0] == 0x10 && bytes.Length > 14 && bytes[2] == 0x61 && bytes[7] == 0x31 && bytes[14] == 0x31)
        {
            LoadMapType1(bytes);
            return;
        }

        if (bytes.Length > 19 && (bytes[4] == 0x0F || bytes[4] == 0x03) && bytes[18] == 0x0D && bytes[19] == 0x0A)
        {
            int w = bytes[0] + (bytes[1] << 8);
            int h = bytes[2] + (bytes[3] << 8);
            if (bytes.Length > 52 + (w * h * 14))
                LoadMapType3(bytes);
            else
                LoadMapType2(bytes);
            return;
        }

        if (bytes[0] == 0x0D && bytes.Length > 11 && bytes[1] == 0x4C && bytes[7] == 0x20 && bytes[11] == 0x6D)
        {
            LoadMapType7(bytes);
            return;
        }

        LoadMapType0(bytes);
    }

    static void MarkFishing(MapCell cell)
    {
        if (cell.Light is >= 100 and <= 119)
            cell.FishingCell = true;
    }

    void Alloc(int width, int height)
    {
        Width = width;
        Height = height;
        Cells = new MapCell[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                Cells[x, y] = new MapCell();
    }

    void LoadMapType0(byte[] bytes)
    {
        try
        {
            int offset = 0;
            int w = BitConverter.ToInt16(bytes, offset);
            offset += 2;
            int h = BitConverter.ToInt16(bytes, offset);
            Alloc(w, h);
            offset = 52;
            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
            {
                var cell = Cells[x, y];
                cell.BackIndex = 0;
                cell.MiddleIndex = 1;
                cell.BackImage = BitConverter.ToInt16(bytes, offset);
                offset += 2;
                cell.MiddleImage = BitConverter.ToInt16(bytes, offset);
                offset += 2;
                cell.FrontImage = BitConverter.ToInt16(bytes, offset);
                offset += 2;
                cell.DoorIndex = (byte)(bytes[offset++] & 0x7F);
                cell.DoorOffset = bytes[offset++];
                cell.FrontAnimationFrame = bytes[offset++];
                cell.FrontAnimationTick = bytes[offset++];
                cell.FrontIndex = (short)(bytes[offset++] + 2);
                cell.Light = bytes[offset++];
                if ((cell.BackImage & 0x8000) != 0)
                    cell.BackImage = (cell.BackImage & 0x7FFF) | 0x20000000;
                MarkFishing(cell);
            }
        }
        catch
        {
            /* same as Client: leave whatever cells were allocated */
        }
    }

    void LoadMapType1(byte[] bytes)
    {
        try
        {
            int offSet = 21;
            int w = BitConverter.ToInt16(bytes, offSet);
            offSet += 2;
            int xor = BitConverter.ToInt16(bytes, offSet);
            offSet += 2;
            int h = BitConverter.ToInt16(bytes, offSet);
            Alloc(w ^ xor, h ^ xor);
            offSet = 54;

            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
            {
                var cell = Cells[x, y];
                cell.BackIndex = 0;
                cell.BackImage = BitConverter.ToInt32(bytes, offSet) ^ unchecked((int)0xAA38AA38);
                cell.MiddleIndex = 1;
                cell.MiddleImage = (short)(BitConverter.ToInt16(bytes, offSet += 4) ^ xor);
                cell.FrontImage = (short)(BitConverter.ToInt16(bytes, offSet += 2) ^ xor);
                cell.DoorIndex = (byte)(bytes[offSet += 2] & 0x7F);
                cell.DoorOffset = bytes[++offSet];
                cell.FrontAnimationFrame = bytes[++offSet];
                cell.FrontAnimationTick = bytes[++offSet];
                cell.FrontIndex = (short)(bytes[++offSet] + 2);
                cell.Light = bytes[++offSet];
                cell.Unknown = bytes[++offSet];
                offSet++;

                if (cell.FrontIndex == 102)
                    cell.FrontIndex = 90;
                if (cell.FrontIndex >= 255)
                    cell.FrontIndex = -1;
                MarkFishing(cell);
            }
        }
        catch
        {
        }
    }

    void LoadMapType2(byte[] bytes)
    {
        try
        {
            int offset = 0;
            int w = BitConverter.ToInt16(bytes, offset);
            offset += 2;
            int h = BitConverter.ToInt16(bytes, offset);
            Alloc(w, h);
            offset = 52;
            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
            {
                var cell = Cells[x, y];
                cell.BackImage = BitConverter.ToInt16(bytes, offset);
                offset += 2;
                cell.MiddleImage = BitConverter.ToInt16(bytes, offset);
                offset += 2;
                cell.FrontImage = BitConverter.ToInt16(bytes, offset);
                offset += 2;
                cell.DoorIndex = (byte)(bytes[offset++] & 0x7F);
                cell.DoorOffset = bytes[offset++];
                cell.FrontAnimationFrame = bytes[offset++];
                cell.FrontAnimationTick = bytes[offset++];
                cell.FrontIndex = (short)(bytes[offset++] + 120);
                cell.Light = bytes[offset++];
                cell.BackIndex = (short)(bytes[offset++] + 100);
                cell.MiddleIndex = (short)(bytes[offset++] + 110);
                if ((cell.BackImage & 0x8000) != 0)
                    cell.BackImage = (cell.BackImage & 0x7FFF) | 0x20000000;
                MarkFishing(cell);
            }
        }
        catch
        {
        }
    }

    void LoadMapType3(byte[] bytes)
    {
        try
        {
            int offset = 0;
            int w = BitConverter.ToInt16(bytes, offset);
            offset += 2;
            int h = BitConverter.ToInt16(bytes, offset);
            Alloc(w, h);
            offset = 52;
            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
            {
                var cell = Cells[x, y];
                cell.BackImage = BitConverter.ToInt16(bytes, offset);
                offset += 2;
                cell.MiddleImage = BitConverter.ToInt16(bytes, offset);
                offset += 2;
                cell.FrontImage = BitConverter.ToInt16(bytes, offset);
                offset += 2;
                cell.DoorIndex = (byte)(bytes[offset++] & 0x7F);
                cell.DoorOffset = bytes[offset++];
                cell.FrontAnimationFrame = bytes[offset++];
                cell.FrontAnimationTick = bytes[offset++];
                cell.FrontIndex = (short)(bytes[offset++] + 120);
                cell.Light = bytes[offset++];
                cell.BackIndex = (short)(bytes[offset++] + 100);
                cell.MiddleIndex = (short)(bytes[offset++] + 110);
                cell.TileAnimationImage = BitConverter.ToInt16(bytes, offset);
                offset += 7;
                cell.TileAnimationFrames = bytes[offset++];
                cell.TileAnimationOffset = BitConverter.ToInt16(bytes, offset);
                offset += 14;
                if ((cell.BackImage & 0x8000) != 0)
                    cell.BackImage = (cell.BackImage & 0x7FFF) | 0x20000000;
                MarkFishing(cell);
            }
        }
        catch
        {
        }
    }

    void LoadMapType4(byte[] bytes)
    {
        try
        {
            int offset = 31;
            int w = BitConverter.ToInt16(bytes, offset);
            offset += 2;
            int xor = BitConverter.ToInt16(bytes, offset);
            offset += 2;
            int h = BitConverter.ToInt16(bytes, offset);
            Alloc(w ^ xor, h ^ xor);
            offset = 64;
            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
            {
                var cell = Cells[x, y];
                cell.BackIndex = 0;
                cell.MiddleIndex = 1;
                cell.BackImage = (short)(BitConverter.ToInt16(bytes, offset) ^ xor);
                offset += 2;
                cell.MiddleImage = (short)(BitConverter.ToInt16(bytes, offset) ^ xor);
                offset += 2;
                cell.FrontImage = (short)(BitConverter.ToInt16(bytes, offset) ^ xor);
                offset += 2;
                cell.DoorIndex = (byte)(bytes[offset++] & 0x7F);
                cell.DoorOffset = bytes[offset++];
                cell.FrontAnimationFrame = bytes[offset++];
                cell.FrontAnimationTick = bytes[offset++];
                cell.FrontIndex = (short)(bytes[offset++] + 2);
                cell.Light = bytes[offset++];
                if ((cell.BackImage & 0x8000) != 0)
                    cell.BackImage = (cell.BackImage & 0x7FFF) | 0x20000000;
                MarkFishing(cell);
            }
        }
        catch
        {
        }
    }

    void LoadMapType5(byte[] bytes)
    {
        try
        {
            int offset = 20;
            offset += 2;
            int w = BitConverter.ToInt16(bytes, offset);
            int h = BitConverter.ToInt16(bytes, offset += 2);
            Alloc(w, h);
            offset = 28;
            for (int x = 0; x < Width / 2; x++)
            for (int y = 0; y < Height / 2; y++)
            {
                for (int i = 0; i < 4; i++)
                {
                    var cell = Cells[(x * 2) + (i % 2), (y * 2) + (i / 2)];
                    cell.BackIndex = (short)(bytes[offset] != 255 ? bytes[offset] + 200 : -1);
                    cell.BackImage = BitConverter.ToUInt16(bytes, offset + 1) + 1;
                }
                offset += 3;
            }

            offset = 28 + (3 * ((Width / 2) + (Width % 2)) * (Height / 2));
            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
            {
                var cell = Cells[x, y];
                byte flag = bytes[offset++];
                cell.MiddleAnimationFrame = bytes[offset++];
                cell.FrontAnimationFrame = bytes[offset] == 255 ? (byte)0 : bytes[offset];
                cell.FrontAnimationFrame &= 0x8F;
                offset++;
                cell.FrontIndex = (short)(bytes[offset] != 255 ? bytes[offset] + 200 : -1);
                offset++;
                cell.MiddleIndex = (short)(bytes[offset] != 255 ? bytes[offset] + 200 : -1);
                offset++;
                cell.MiddleImage = BitConverter.ToUInt16(bytes, offset) + 1;
                offset += 2;
                cell.FrontImage = BitConverter.ToUInt16(bytes, offset) + 1;
                if (cell.FrontImage == 1 && cell.FrontIndex == 200)
                    cell.FrontIndex = -1;
                offset += 2;
                offset += 3;
                cell.Light = (byte)(bytes[offset] & 0x0F);
                offset += 2;
                if ((flag & 0x01) != 1) cell.BackImage |= 0x20000000;
                if ((flag & 0x02) != 2) cell.FrontImage = (ushort)((ushort)cell.FrontImage | 0x8000);
                if (cell.Light is >= 100 and <= 119)
                    cell.FishingCell = true;
                else
                    cell.Light *= 2;
            }
        }
        catch
        {
        }
    }

    void LoadMapType6(byte[] bytes)
    {
        try
        {
            int offset = 16;
            int w = BitConverter.ToInt16(bytes, offset);
            offset += 2;
            int h = BitConverter.ToInt16(bytes, offset);
            Alloc(w, h);
            offset = 40;
            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
            {
                var cell = Cells[x, y];
                byte flag = bytes[offset++];
                cell.BackIndex = (short)(bytes[offset] != 255 ? bytes[offset] + 300 : -1);
                offset++;
                cell.MiddleIndex = (short)(bytes[offset] != 255 ? bytes[offset] + 300 : -1);
                offset++;
                cell.FrontIndex = (short)(bytes[offset] != 255 ? bytes[offset] + 300 : -1);
                offset++;
                cell.BackImage = (short)(BitConverter.ToInt16(bytes, offset) + 1);
                offset += 2;
                cell.MiddleImage = (short)(BitConverter.ToInt16(bytes, offset) + 1);
                offset += 2;
                cell.FrontImage = (short)(BitConverter.ToInt16(bytes, offset) + 1);
                offset += 2;
                if (cell.FrontImage == 1 && cell.FrontIndex == 200)
                    cell.FrontIndex = -1;
                cell.MiddleAnimationFrame = bytes[offset++];
                cell.FrontAnimationFrame = bytes[offset] == 255 ? (byte)0 : bytes[offset];
                if (cell.FrontAnimationFrame > 0x0F)
                    cell.FrontAnimationFrame = (byte)(cell.FrontAnimationFrame & 0x0F);
                offset++;
                cell.MiddleAnimationTick = 1;
                cell.FrontAnimationTick = 1;
                cell.Light = (byte)((bytes[offset] & 0x0F) * 4);
                offset += 8;
                if ((flag & 0x01) != 1) cell.BackImage |= 0x20000000;
                if ((flag & 0x02) != 2) cell.FrontImage = (short)((ushort)cell.FrontImage | 0x8000);
            }
        }
        catch
        {
        }
    }

    void LoadMapType7(byte[] bytes)
    {
        try
        {
            int offset = 21;
            int w = BitConverter.ToInt16(bytes, offset);
            offset += 4;
            int h = BitConverter.ToInt16(bytes, offset);
            Alloc(w, h);
            offset = 54;
            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
            {
                var cell = Cells[x, y];
                cell.BackIndex = 0;
                cell.BackImage = BitConverter.ToInt32(bytes, offset);
                cell.MiddleIndex = 1;
                cell.MiddleImage = BitConverter.ToInt16(bytes, offset += 4);
                cell.FrontImage = BitConverter.ToInt16(bytes, offset += 2);
                cell.DoorIndex = (byte)(bytes[offset += 2] & 0x7F);
                cell.DoorOffset = bytes[++offset];
                cell.FrontAnimationFrame = bytes[++offset];
                cell.FrontAnimationTick = bytes[++offset];
                cell.FrontIndex = (short)(bytes[++offset] + 2);
                cell.Light = bytes[++offset];
                cell.Unknown = bytes[++offset];
                if ((cell.BackImage & 0x8000) != 0)
                    cell.BackImage = (cell.BackImage & 0x7FFF) | 0x20000000;
                offset++;
                MarkFishing(cell);
            }
        }
        catch
        {
        }
    }

    void LoadMapType100(byte[] bytes)
    {
        try
        {
            if (bytes[0] != 1 || bytes[1] != 0)
                return;
            int offset = 4;
            int w = BitConverter.ToInt16(bytes, offset);
            offset += 2;
            int h = BitConverter.ToInt16(bytes, offset);
            Alloc(w, h);
            offset = 8;
            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
            {
                var cell = Cells[x, y];
                cell.BackIndex = BitConverter.ToInt16(bytes, offset);
                offset += 2;
                cell.BackImage = BitConverter.ToInt32(bytes, offset);
                offset += 4;
                cell.MiddleIndex = BitConverter.ToInt16(bytes, offset);
                offset += 2;
                cell.MiddleImage = BitConverter.ToInt16(bytes, offset);
                offset += 2;
                cell.FrontIndex = BitConverter.ToInt16(bytes, offset);
                offset += 2;
                cell.FrontImage = BitConverter.ToInt16(bytes, offset);
                offset += 2;
                cell.DoorIndex = (byte)(bytes[offset++] & 0x7F);
                cell.DoorOffset = bytes[offset++];
                cell.FrontAnimationFrame = bytes[offset++];
                cell.FrontAnimationTick = bytes[offset++];
                cell.MiddleAnimationFrame = bytes[offset++];
                cell.MiddleAnimationTick = bytes[offset++];
                cell.TileAnimationImage = BitConverter.ToInt16(bytes, offset);
                offset += 2;
                cell.TileAnimationOffset = BitConverter.ToInt16(bytes, offset);
                offset += 2;
                cell.TileAnimationFrames = bytes[offset++];
                cell.Light = bytes[offset++];
                MarkFishing(cell);
            }
        }
        catch
        {
        }
    }
}
