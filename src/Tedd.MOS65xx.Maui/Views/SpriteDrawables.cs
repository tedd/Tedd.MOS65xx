using Tedd.MOS65xx.Emulator.Tools;

namespace Tedd.MOS65xx.Maui.Views;

/// <summary>
/// Draws one decoded sprite (24 x 21 color indices, <see cref="SpriteSnapshot.Transparent"/> where nothing is
/// drawn) as a zoomable pixel grid. X/Y expansion is applied by doubling the cell size, so the shape has the
/// same proportions as on screen; the grid marks every pixel and, more strongly, the three byte columns of
/// each row.
/// </summary>
internal sealed class SpriteDrawable : IDrawable
{
    private static readonly Color CheckerDark = Color.FromRgb(0x24, 0x24, 0x24);
    private static readonly Color CheckerLight = Color.FromRgb(0x30, 0x30, 0x30);
    private static readonly Color PixelGrid = Color.FromRgba(0xFF, 0xFF, 0xFF, 0x40);
    private static readonly Color ByteGrid = Color.FromRgba(0xFF, 0xFF, 0xFF, 0x90);

    private byte[] _pixels = Array.Empty<byte>();

    /// <summary>Size of one sprite pixel, before expansion.</summary>
    public double Zoom { get; set; } = 8;

    /// <summary>Draws the pixel/byte grid (only visible from about 4 units per pixel).</summary>
    public bool ShowGrid { get; set; } = true;

    /// <summary>Color index drawn behind the sprite, or -1 for a checkerboard marking transparency.</summary>
    public int BackgroundColor { get; set; } = -1;

    public bool ExpandX { get; private set; }
    public bool ExpandY { get; private set; }

    public double CellWidth => Zoom * (ExpandX ? 2 : 1);
    public double CellHeight => Zoom * (ExpandY ? 2 : 1);

    /// <summary>Total size the drawing needs; the hosting view is sized to it.</summary>
    public double Width => SpriteSnapshot.Width * CellWidth;
    public double Height => SpriteSnapshot.Height * CellHeight;

    /// <summary>Shows <paramref name="pixels"/> (the array is read while drawing, so it must stay 24 x 21).</summary>
    public void SetSprite(byte[] pixels, bool expandX, bool expandY)
    {
        _pixels = pixels;
        ExpandX = expandX;
        ExpandY = expandY;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float cw = (float)CellWidth, ch = (float)CellHeight;
        float width = SpriteSnapshot.Width * cw, height = SpriteSnapshot.Height * ch;
        if (BackgroundColor >= 0)
        {
            canvas.FillColor = C64Palette.Of(BackgroundColor);
            canvas.FillRectangle(0, 0, width, height);
        }
        else
        {
            DrawChecker(canvas, width, height);
        }
        if (_pixels.Length < SpriteSnapshot.Width * SpriteSnapshot.Height)
            return;

        // Horizontal runs of the same color become one rectangle: at most a few dozen fills per sprite.
        for (int y = 0; y < SpriteSnapshot.Height; y++)
        {
            for (int x = 0; x < SpriteSnapshot.Width; x++)
            {
                byte c = _pixels[y * SpriteSnapshot.Width + x];
                if (c == SpriteSnapshot.Transparent) continue;
                int runEnd = x;
                while (runEnd + 1 < SpriteSnapshot.Width && _pixels[y * SpriteSnapshot.Width + runEnd + 1] == c)
                    runEnd++;
                canvas.FillColor = C64Palette.Of(c);
                canvas.FillRectangle(x * cw, y * ch, (runEnd - x + 1) * cw, ch);
                x = runEnd;
            }
        }

        if (!ShowGrid || cw < 4) return;
        canvas.StrokeSize = 1;
        for (int x = 0; x <= SpriteSnapshot.Width; x++)
        {
            canvas.StrokeColor = x % 8 == 0 ? ByteGrid : PixelGrid;
            canvas.DrawLine(x * cw, 0, x * cw, height);
        }
        canvas.StrokeColor = PixelGrid;
        for (int y = 0; y <= SpriteSnapshot.Height; y++)
            canvas.DrawLine(0, y * ch, width, y * ch);
    }

    /// <summary>Fills the area with a checkerboard marking the transparent parts.</summary>
    public static void DrawChecker(ICanvas canvas, float width, float height, float cell = 8)
    {
        canvas.FillColor = CheckerDark;
        canvas.FillRectangle(0, 0, width, height);
        canvas.FillColor = CheckerLight;
        for (float y = 0, row = 0; y < height; y += cell, row++)
            for (float x = row % 2 == 0 ? 0 : cell; x < width; x += 2 * cell)
                canvas.FillRectangle(x, y, Math.Min(cell, width - x), Math.Min(cell, height - y));
    }
}

/// <summary>A 24 x 21 sprite at one screen pixel per sprite pixel, for the list on the left.</summary>
internal sealed class SpriteThumbnailDrawable : IDrawable
{
    private byte[] _pixels = Array.Empty<byte>();

    public void SetSprite(byte[] pixels) => _pixels = pixels;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float cw = dirtyRect.Width / SpriteSnapshot.Width;
        float ch = dirtyRect.Height / SpriteSnapshot.Height;
        // Transparent pixels get a checkerboard so an empty sprite is not just a blank box.
        SpriteDrawable.DrawChecker(canvas, dirtyRect.Width, dirtyRect.Height, cw * 3);
        if (_pixels.Length < SpriteSnapshot.Width * SpriteSnapshot.Height) return;
        for (int y = 0; y < SpriteSnapshot.Height; y++)
        {
            for (int x = 0; x < SpriteSnapshot.Width; x++)
            {
                byte c = _pixels[y * SpriteSnapshot.Width + x];
                if (c == SpriteSnapshot.Transparent) continue;
                int runEnd = x;
                while (runEnd + 1 < SpriteSnapshot.Width && _pixels[y * SpriteSnapshot.Width + runEnd + 1] == c)
                    runEnd++;
                canvas.FillColor = C64Palette.Of(c);
                canvas.FillRectangle(x * cw, y * ch, (runEnd - x + 1) * cw, ch);
                x = runEnd;
            }
        }
    }
}

/// <summary>
/// Where the sprites sit relative to the display window: the 320 x 200 display area inside the visible border
/// area, with one labelled box per enabled sprite at its MxX/MxY position and expanded size. Sprites that are
/// (partly) outside the visible area are visibly clipped, which is usually the reason one cannot be seen.
/// </summary>
internal sealed class SpriteLayoutDrawable : IDrawable
{
    // The visible PAL picture in sprite coordinates: X $1E..$15E, Y 16..300 (3.9 plus the blanking margins).
    private const int VisibleLeft = 0x1E - 24, VisibleTop = 16 - 50, VisibleWidth = 0x158 - 0x18 + 40, VisibleHeight = 284;

    private static readonly Color Back = Color.FromRgb(0x14, 0x14, 0x14);
    private static readonly Color WindowEdge = Color.FromRgb(0x60, 0x60, 0x60);

    private IReadOnlyList<SpriteInfo> _sprites = Array.Empty<SpriteInfo>();
    private int _selected = -1;

    /// <summary>Shows these sprites; invalidate the hosting view afterwards.</summary>
    public void SetSprites(IReadOnlyList<SpriteInfo> sprites, int selected)
    {
        _sprites = sprites;
        _selected = selected;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float w = dirtyRect.Width, h = dirtyRect.Height;
        if (w <= 0 || h <= 0) return;
        canvas.FillColor = Back;
        canvas.FillRectangle(0, 0, w, h);

        float scale = Math.Min(w / VisibleWidth, h / VisibleHeight);
        float ox = (w - VisibleWidth * scale) / 2, oy = (h - VisibleHeight * scale) / 2;
        PointF Map(float x, float y) => new(ox + (x - VisibleLeft) * scale, oy + (y - VisibleTop) * scale);

        // The 40 x 25 character display window (sprite coordinates 0,0 .. 320,200).
        var topLeft = Map(0, 0);
        var bottomRight = Map(320, 200);
        canvas.FillColor = C64Palette.Of(11);
        canvas.FillRectangle(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
        canvas.StrokeSize = 1;
        canvas.StrokeColor = WindowEdge;
        canvas.DrawRectangle(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);

        canvas.SaveState();
        canvas.ClipRectangle(0, 0, w, h);
        canvas.FontSize = 11;
        foreach (var s in _sprites)
        {
            if (!s.Enabled) continue;
            var p0 = Map(s.DisplayX, s.DisplayY);
            var rect = new RectF(p0.X, p0.Y, s.PixelWidth * scale, s.PixelHeight * scale);
            canvas.FillColor = C64Palette.Translucent[s.Color & 15];
            canvas.FillRectangle(rect);
            canvas.StrokeColor = s.Index == _selected ? Microsoft.Maui.Graphics.Colors.White : C64Palette.Of(s.Color);
            canvas.StrokeSize = s.Index == _selected ? 2 : 1;
            canvas.DrawRectangle(rect);
            canvas.FontColor = Microsoft.Maui.Graphics.Colors.White;
            canvas.DrawString(s.Index.ToString(), rect.X + 2, rect.Y + 12, HorizontalAlignment.Left);
        }
        canvas.RestoreState();
    }
}
