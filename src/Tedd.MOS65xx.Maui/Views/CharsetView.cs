using Tedd.MOS65xx.Emulator.Tools;

namespace Tedd.MOS65xx.Maui.Views;

/// <summary>
/// Draws a whole character set as a grid of 8 x 8 glyphs in two C64 colors, with a hex gutter showing the first
/// character code of each row.
///
/// It is two stacked canvases: the glyphs themselves, which are only redrawn when the characters, colors or
/// zoom change, and a transparent overlay with the gutter, grid, hover box and selection box, which is redrawn
/// whenever the pointer moves. Each glyph row is emitted as merged horizontal runs, so even a 512 character ROM
/// image is a few thousand rectangles rather than 32768.
/// </summary>
public sealed class CharsetView : ContentView
{
    private readonly GlyphLayer _glyphs = new();
    private readonly OverlayLayer _overlay;
    private readonly GraphicsView _glyphView;
    private readonly GraphicsView _overlayView;

    private CharacterSet? _source;
    private int _columns = 32;
    private int _zoom = 4;
    private int _ink = 14;      // light blue on blue: the C64's own text colors
    private int _paper = 6;
    private bool _showGrid = true;
    private int _selectedIndex;
    private int _hoverIndex = -1;
    private double _labelSize = 11;

    public CharsetView()
    {
        _overlay = new OverlayLayer(this);
        _glyphView = new GraphicsView { Drawable = _glyphs, InputTransparent = true };
        _overlayView = new GraphicsView { Drawable = _overlay, BackgroundColor = Microsoft.Maui.Graphics.Colors.Transparent };

        var pointer = new PointerGestureRecognizer();
        pointer.PointerMoved += (_, e) => SetHover(IndexAt(e.GetPosition(_overlayView)));
        pointer.PointerExited += (_, _) => SetHover(-1);
        _overlayView.GestureRecognizers.Add(pointer);

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, e) =>
        {
            int index = IndexAt(e.GetPosition(_overlayView));
            if (index >= 0) SelectedIndex = index;
        };
        _overlayView.GestureRecognizers.Add(tap);

        Content = new Grid { Children = { _glyphView, _overlayView } };
    }

    /// <summary>Raised when <see cref="SelectedIndex"/> changes, whatever changed it.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Raised when the character under the pointer changes; the argument is -1 when it leaves.</summary>
    public event EventHandler<int>? HoverChanged;

    /// <summary>The characters to draw. Setting it keeps the selection if it still exists.</summary>
    public CharacterSet? Source
    {
        get => _source;
        set
        {
            _source = value;
            if (_selectedIndex >= (value?.Count ?? 0)) SetSelected(0);
            Rebuild();
        }
    }

    /// <summary>Characters per row; 32 lays 256 characters out as $x0 columns and 8 rows.</summary>
    public int Columns
    {
        get => _columns;
        set { _columns = Math.Max(1, value); Rebuild(); }
    }

    /// <summary>Screen pixels per character pixel.</summary>
    public int Zoom
    {
        get => _zoom;
        set { _zoom = Math.Clamp(value, 1, 24); Rebuild(); }
    }

    /// <summary>C64 color index (0..15) for set bits.</summary>
    public int Ink
    {
        get => _ink;
        set { _ink = value & 0x0F; Rebuild(); }
    }

    /// <summary>C64 color index (0..15) for clear bits.</summary>
    public int Paper
    {
        get => _paper;
        set { _paper = value & 0x0F; Rebuild(); }
    }

    /// <summary>Draws separator lines between the characters.</summary>
    public bool ShowGrid
    {
        get => _showGrid;
        set { _showGrid = value; _overlayView.Invalidate(); }
    }

    /// <summary>The selected character, or 0 when the set is empty.</summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (_source is null || value < 0 || value >= _source.Count || value == _selectedIndex) return;
            SetSelected(value);
        }
    }

    /// <summary>Number of rows the current set needs.</summary>
    public int Rows => _source is null ? 0 : (_source.Count + _columns - 1) / _columns;

    /// <summary>Size of one character on screen.</summary>
    private double Cell => CharacterSet.CharacterWidth * _zoom;

    /// <summary>Width of the hex label column on the left.</summary>
    private double Gutter => Math.Ceiling(_labelSize * 2.6 + 8);

    /// <summary>Moves the selection the way the arrow keys do; returns true when it moved.</summary>
    public bool MoveSelection(string code)
    {
        if (_source is null) return false;
        int last = _source.Count - 1;
        int index = code switch
        {
            "ArrowLeft" => _selectedIndex - 1,
            "ArrowRight" => _selectedIndex + 1,
            "ArrowUp" => _selectedIndex - _columns,
            "ArrowDown" => _selectedIndex + _columns,
            "PageUp" => _selectedIndex - _columns * 8,
            "PageDown" => _selectedIndex + _columns * 8,
            "Home" => 0,
            "End" => last,
            _ => _selectedIndex,
        };
        if (index == _selectedIndex) return false;
        SelectedIndex = Math.Clamp(index, 0, last);
        return true;
    }

    /// <summary>Redraws everything from the source; call after the data behind a live set changed.</summary>
    public void Rebuild()
    {
        double cell = Cell;
        WidthRequest = Gutter + _columns * cell;
        HeightRequest = Math.Max(1, Rows * cell);
        _glyphs.Update(_source, _columns, cell, Gutter, _ink, _paper);
        _glyphView.Invalidate();
        _overlayView.Invalidate();
    }

    /// <summary>The character at a point in this view's coordinates, or -1 when there is none.</summary>
    public int IndexAt(Point? point)
    {
        if (_source is null || point is not { } p) return -1;
        double x = p.X - Gutter, y = p.Y;
        if (x < 0 || y < 0) return -1;
        int column = (int)(x / Cell), row = (int)(y / Cell);
        if (column >= _columns || row >= Rows) return -1;
        int index = row * _columns + column;
        return index < _source.Count ? index : -1;
    }

    private void SetSelected(int index)
    {
        _selectedIndex = index;
        _overlayView.Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetHover(int index)
    {
        if (index == _hoverIndex) return;
        _hoverIndex = index;
        _overlayView.Invalidate();
        HoverChanged?.Invoke(this, index);
    }

    /// <summary>The glyphs themselves, redrawn only when the characters, colors or zoom change.</summary>
    private sealed class GlyphLayer : IDrawable
    {
        private CharacterSet? _source;
        private int _columns = 32;
        private double _cell = 32;
        private double _gutter;
        private int _ink, _paper;

        public void Update(CharacterSet? source, int columns, double cell, double gutter, int ink, int paper)
        {
            _source = source;
            _columns = columns;
            _cell = cell;
            _gutter = gutter;
            _ink = ink;
            _paper = paper;
        }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var source = _source;
            if (source is null || source.Count == 0) return;
            int size = CharacterSet.CharacterWidth;
            float pixel = (float)(_cell / size);
            int rows = (source.Count + _columns - 1) / _columns;

            canvas.FillColor = C64Palette.Of(_paper);
            canvas.FillRectangle((float)_gutter, 0, (float)(_columns * _cell), (float)(rows * _cell));
            canvas.FillColor = C64Palette.Of(_ink);
            for (int index = 0; index < source.Count; index++)
            {
                var glyph = source.Glyph(index);
                float left = (float)(_gutter + index % _columns * _cell);
                float top = (float)(index / _columns * _cell);
                for (int row = 0; row < size; row++)
                {
                    int bits = glyph[row];
                    if (bits == 0) continue;
                    // Merge horizontal runs of set bits into one rectangle each.
                    for (int x = 0; x < size; x++)
                    {
                        if (((bits >> (7 - x)) & 1) == 0) continue;
                        int end = x;
                        while (end + 1 < size && ((bits >> (7 - (end + 1))) & 1) != 0) end++;
                        canvas.FillRectangle(left + x * pixel, top + row * pixel, (end - x + 1) * pixel, pixel);
                        x = end;
                    }
                }
            }
        }
    }

    /// <summary>Gutter labels, grid, set divider, hover and selection: cheap, and redrawn on every pointer move.</summary>
    private sealed class OverlayLayer : IDrawable
    {
        private static readonly Color GridColor = Color.FromRgba(0xFF, 0xFF, 0xFF, 0x50);
        private static readonly Color HoverColor = Color.FromRgba(0xFF, 0xFF, 0xFF, 0xA0);
        private static readonly Color DividerColor = Color.FromRgba(0xFF, 0xFF, 0xFF, 0xC0);
        private static readonly Color GutterColor = Color.FromRgb(0x88, 0x88, 0x88);

        private readonly CharsetView _owner;

        public OverlayLayer(CharsetView owner) => _owner = owner;

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var source = _owner._source;
            if (source is null || source.Count == 0) return;
            float cell = (float)_owner.Cell;
            float gutter = (float)_owner.Gutter;
            int columns = _owner._columns;
            int rows = _owner.Rows;
            float width = columns * cell, height = rows * cell;

            canvas.StrokeSize = 1;
            if (_owner._showGrid && _owner._zoom >= 3)
            {
                canvas.StrokeColor = GridColor;
                for (int c = 0; c <= columns; c++)
                    canvas.DrawLine(gutter + c * cell, 0, gutter + c * cell, height);
                for (int r = 0; r <= rows; r++)
                    canvas.DrawLine(gutter, r * cell, gutter + width, r * cell);
            }

            // A brighter rule where one 256 character set ends and the next begins.
            canvas.StrokeColor = DividerColor;
            canvas.StrokeSize = 2;
            for (int c = CharacterSet.CharactersPerSet; c < source.Count; c += CharacterSet.CharactersPerSet)
            {
                float y = c / columns * cell;
                canvas.DrawLine(gutter, y, gutter + width, y);
            }

            // Labelled by screen code, so the second set of a ROM image starts over at $00.
            canvas.FontColor = GutterColor;
            canvas.FontSize = (float)_owner._labelSize;
            for (int r = 0; r < rows; r++)
                canvas.DrawString($"${r * columns & 0xFF:X2}", 0, r * cell, gutter - 6, cell,
                    HorizontalAlignment.Right, VerticalAlignment.Center);

            int hover = _owner._hoverIndex, selected = _owner._selectedIndex;
            canvas.StrokeSize = 1;
            if (hover >= 0 && hover < source.Count && hover != selected)
            {
                canvas.StrokeColor = HoverColor;
                canvas.DrawRectangle(CellRect(hover, 0.5f, cell, gutter, columns));
            }
            if (selected < source.Count)
            {
                var rect = CellRect(selected, 1f, cell, gutter, columns);
                canvas.StrokeColor = Microsoft.Maui.Graphics.Colors.Black;
                canvas.StrokeSize = 4;
                canvas.DrawRectangle(rect);
                canvas.StrokeColor = Microsoft.Maui.Graphics.Colors.White;
                canvas.StrokeSize = 2;
                canvas.DrawRectangle(rect);
            }
        }

        private static RectF CellRect(int index, float inset, float cell, float gutter, int columns) =>
            new(gutter + index % columns * cell + inset, index / columns * cell + inset, cell - 2 * inset, cell - 2 * inset);
    }
}

/// <summary>
/// One character blown up to fill the view, with a pixel grid: the bit pattern of the 8 bytes behind a
/// character, which is what you actually edit when designing a character set.
/// </summary>
internal sealed class GlyphDrawable : IDrawable
{
    private static readonly Color GridColor = Color.FromRgba(0xFF, 0xFF, 0xFF, 0x60);
    private static readonly Color BorderColor = Color.FromRgb(0x80, 0x80, 0x80);

    private readonly byte[] _rows = new byte[CharacterSet.CharacterHeight];
    private int _ink = 14;
    private int _paper = 6;

    /// <summary>Shows the 8 rows of a character in the given C64 colors.</summary>
    public void SetGlyph(ReadOnlySpan<byte> glyph, int ink, int paper)
    {
        Array.Clear(_rows);
        glyph.Slice(0, Math.Min(glyph.Length, _rows.Length)).CopyTo(_rows);
        _ink = ink & 0x0F;
        _paper = paper & 0x0F;
    }

    /// <summary>Clears the view (no character selected).</summary>
    public void Clear() => Array.Clear(_rows);

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        int size = CharacterSet.CharacterWidth;
        float side = (float)Math.Floor(Math.Min(dirtyRect.Width, dirtyRect.Height) / size) * size;
        if (side < size) return;
        float pixel = side / size;

        canvas.FillColor = C64Palette.Of(_paper);
        canvas.FillRectangle(0, 0, side, side);
        canvas.FillColor = C64Palette.Of(_ink);
        for (int row = 0; row < CharacterSet.CharacterHeight; row++)
            for (int x = 0; x < size; x++)
                if (((_rows[row] >> (7 - x)) & 1) != 0)
                    canvas.FillRectangle(x * pixel, row * pixel, pixel, pixel);

        canvas.StrokeSize = 1;
        canvas.StrokeColor = GridColor;
        for (int i = 1; i < size; i++)
        {
            canvas.DrawLine(i * pixel, 0, i * pixel, side);
            canvas.DrawLine(0, i * pixel, side, i * pixel);
        }
        canvas.StrokeColor = BorderColor;
        canvas.DrawRectangle(0.5f, 0.5f, side - 1, side - 1);
    }
}
