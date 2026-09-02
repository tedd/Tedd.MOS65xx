using System;
using System.Collections.Generic;

namespace Tedd.MOS65xx.Emulator.C64;

/// <summary>
/// Keys of the C64 keyboard identified by their position in the 8x8 matrix: value = row * 8 + column, where
/// rows are driven by CIA1 port A (PA0-PA7) and columns are read on CIA1 port B (PB0-PB7).
/// (C64 Programmer's Reference Guide, keyboard matrix table.)
/// </summary>
public enum C64Key : byte
{
    // Row 0 (PA0)
    Delete = 0, Return = 1, CursorRight = 2, F7 = 3, F1 = 4, F3 = 5, F5 = 6, CursorDown = 7,
    // Row 1 (PA1)
    D3 = 8, W = 9, A = 10, D4 = 11, Z = 12, S = 13, E = 14, LeftShift = 15,
    // Row 2 (PA2)
    D5 = 16, R = 17, D = 18, D6 = 19, C = 20, F = 21, T = 22, X = 23,
    // Row 3 (PA3)
    D7 = 24, Y = 25, G = 26, D8 = 27, B = 28, H = 29, U = 30, V = 31,
    // Row 4 (PA4)
    D9 = 32, I = 33, J = 34, D0 = 35, M = 36, K = 37, O = 38, N = 39,
    // Row 5 (PA5)
    Plus = 40, P = 41, L = 42, Minus = 43, Period = 44, Colon = 45, At = 46, Comma = 47,
    // Row 6 (PA6)
    Pound = 48, Asterisk = 49, Semicolon = 50, Home = 51, RightShift = 52, Equals = 53, ArrowUp = 54, Slash = 55,
    // Row 7 (PA7)
    D1 = 56, ArrowLeft = 57, Control = 58, D2 = 59, Space = 60, Commodore = 61, Q = 62, RunStop = 63,
}

/// <summary>
/// The keyboard matrix. Any number of keys can be held. The matrix is passive, so it can be read in both
/// directions (rows driven → columns read, and columns driven → rows read); both are computed exactly like the
/// wiring does it, including "ghosting" when several keys are held.
/// </summary>
public sealed class Keyboard
{
    private readonly bool[] _pressed = new bool[64];
    private readonly byte[] _rowMasks = new byte[8];    // for each row (PA line) the columns (PB bits) pressed
    private readonly byte[] _columnMasks = new byte[8]; // for each column (PB line) the rows (PA bits) pressed

    /// <summary>The RESTORE key is not part of the matrix; it pulses the NMI line.</summary>
    public bool RestorePressed { get; private set; }

    public bool IsPressed(C64Key key) => _pressed[(int)key];

    public void Press(C64Key key) => Set(key, true);
    public void Release(C64Key key) => Set(key, false);
    public void Set(C64Key key, bool pressed)
    {
        int k = (int)key;
        if (_pressed[k] == pressed) return;
        _pressed[k] = pressed;
        int row = k >> 3, col = k & 7;
        if (pressed)
        {
            _rowMasks[row] |= (byte)(1 << col);
            _columnMasks[col] |= (byte)(1 << row);
        }
        else
        {
            _rowMasks[row] &= (byte)~(1 << col);
            _columnMasks[col] &= (byte)~(1 << row);
        }
    }

    public void SetRestore(bool pressed) => RestorePressed = pressed;

    public void ReleaseAll()
    {
        Array.Clear(_pressed);
        Array.Clear(_rowMasks);
        Array.Clear(_columnMasks);
        RestorePressed = false;
    }

    /// <summary>
    /// Column levels (CIA1 port B input) given the row levels driven on CIA1 port A: a pressed key connects its
    /// row line to its column line, so a column reads low when any of its pressed keys sits on a row that is low.
    /// </summary>
    public byte ReadColumns(byte rowLevels)
    {
        int result = 0xFF;
        int low = ~rowLevels & 0xFF;
        while (low != 0)
        {
            int row = System.Numerics.BitOperations.TrailingZeroCount(low);
            low &= low - 1;
            result &= ~_rowMasks[row];
        }
        return (byte)result;
    }

    /// <summary>Row levels (CIA1 port A input) given the column levels driven on CIA1 port B (the matrix is symmetric).</summary>
    public byte ReadRows(byte columnLevels)
    {
        int result = 0xFF;
        int low = ~columnLevels & 0xFF;
        while (low != 0)
        {
            int col = System.Numerics.BitOperations.TrailingZeroCount(low);
            low &= low - 1;
            result &= ~_columnMasks[col];
        }
        return (byte)result;
    }

    /// <summary>
    /// Maps a printable character to the key (and shift requirement) that produces it on the C64.
    /// Returns false for characters without a key.
    /// </summary>
    public static bool TryMapChar(char c, out C64Key key, out bool shift)
    {
        shift = false;
        if (c >= 'a' && c <= 'z') c = char.ToUpperInvariant(c);
        else if (c >= 'A' && c <= 'Z') shift = true;
        if (Chars.TryGetValue(c, out var mapped))
        {
            key = mapped.Key;
            shift |= mapped.Shift;
            return true;
        }
        key = default;
        return false;
    }

    private static readonly Dictionary<char, (C64Key Key, bool Shift)> Chars = new()
    {
        ['A'] = (C64Key.A, false), ['B'] = (C64Key.B, false), ['C'] = (C64Key.C, false), ['D'] = (C64Key.D, false),
        ['E'] = (C64Key.E, false), ['F'] = (C64Key.F, false), ['G'] = (C64Key.G, false), ['H'] = (C64Key.H, false),
        ['I'] = (C64Key.I, false), ['J'] = (C64Key.J, false), ['K'] = (C64Key.K, false), ['L'] = (C64Key.L, false),
        ['M'] = (C64Key.M, false), ['N'] = (C64Key.N, false), ['O'] = (C64Key.O, false), ['P'] = (C64Key.P, false),
        ['Q'] = (C64Key.Q, false), ['R'] = (C64Key.R, false), ['S'] = (C64Key.S, false), ['T'] = (C64Key.T, false),
        ['U'] = (C64Key.U, false), ['V'] = (C64Key.V, false), ['W'] = (C64Key.W, false), ['X'] = (C64Key.X, false),
        ['Y'] = (C64Key.Y, false), ['Z'] = (C64Key.Z, false),
        ['0'] = (C64Key.D0, false), ['1'] = (C64Key.D1, false), ['2'] = (C64Key.D2, false), ['3'] = (C64Key.D3, false),
        ['4'] = (C64Key.D4, false), ['5'] = (C64Key.D5, false), ['6'] = (C64Key.D6, false), ['7'] = (C64Key.D7, false),
        ['8'] = (C64Key.D8, false), ['9'] = (C64Key.D9, false),
        [' '] = (C64Key.Space, false), ['\r'] = (C64Key.Return, false), ['\n'] = (C64Key.Return, false),
        ['+'] = (C64Key.Plus, false), ['-'] = (C64Key.Minus, false), ['.'] = (C64Key.Period, false),
        [':'] = (C64Key.Colon, false), ['@'] = (C64Key.At, false), [','] = (C64Key.Comma, false),
        ['£'] = (C64Key.Pound, false), ['*'] = (C64Key.Asterisk, false), [';'] = (C64Key.Semicolon, false),
        ['='] = (C64Key.Equals, false), ['/'] = (C64Key.Slash, false),
        ['!'] = (C64Key.D1, true), ['"'] = (C64Key.D2, true), ['#'] = (C64Key.D3, true), ['$'] = (C64Key.D4, true),
        ['%'] = (C64Key.D5, true), ['&'] = (C64Key.D6, true), ['\''] = (C64Key.D7, true), ['('] = (C64Key.D8, true),
        [')'] = (C64Key.D9, true), ['<'] = (C64Key.Comma, true), ['>'] = (C64Key.Period, true), ['?'] = (C64Key.Slash, true),
        ['['] = (C64Key.Colon, true), [']'] = (C64Key.Semicolon, true), ['^'] = (C64Key.ArrowUp, false), ['_'] = (C64Key.ArrowLeft, false),
    };
}

/// <summary>Digital joystick on a control port (active-low bits 0-4: up, down, left, right, fire).</summary>
public sealed class Joystick
{
    public bool Up, Down, Left, Right, Fire;

    /// <summary>Port pin levels: 1 = released, 0 = pressed.</summary>
    public byte Levels => (byte)(0xFF & ~((Up ? 1 : 0) | (Down ? 2 : 0) | (Left ? 4 : 0) | (Right ? 8 : 0) | (Fire ? 16 : 0)));

    public void Clear() => Up = Down = Left = Right = Fire = false;
}
