using System.Collections.Generic;
using System.Windows.Input;
using Tedd.MOS65xx.Emulator.C64;

namespace Tedd.MOS65xx.GUI;

/// <summary>
/// Maps PC keys to C64 matrix keys. Mostly positional (letters, digits, punctuation where it exists) with the
/// usual emulator conventions: Escape = RUN/STOP, Tab = Commodore, Backspace = INST/DEL, Page Up = RESTORE,
/// cursor keys map to the shifted cursor keys, F1-F8 map to F1/F3/F5/F7 (+shift).
/// </summary>
public static class KeyMapper
{
    public readonly record struct Mapping(C64Key Key, bool Shift = false);

    private static readonly Dictionary<Key, Mapping> Map = new()
    {
        [Key.A] = new(C64Key.A), [Key.B] = new(C64Key.B), [Key.C] = new(C64Key.C), [Key.D] = new(C64Key.D),
        [Key.E] = new(C64Key.E), [Key.F] = new(C64Key.F), [Key.G] = new(C64Key.G), [Key.H] = new(C64Key.H),
        [Key.I] = new(C64Key.I), [Key.J] = new(C64Key.J), [Key.K] = new(C64Key.K), [Key.L] = new(C64Key.L),
        [Key.M] = new(C64Key.M), [Key.N] = new(C64Key.N), [Key.O] = new(C64Key.O), [Key.P] = new(C64Key.P),
        [Key.Q] = new(C64Key.Q), [Key.R] = new(C64Key.R), [Key.S] = new(C64Key.S), [Key.T] = new(C64Key.T),
        [Key.U] = new(C64Key.U), [Key.V] = new(C64Key.V), [Key.W] = new(C64Key.W), [Key.X] = new(C64Key.X),
        [Key.Y] = new(C64Key.Y), [Key.Z] = new(C64Key.Z),
        [Key.D0] = new(C64Key.D0), [Key.D1] = new(C64Key.D1), [Key.D2] = new(C64Key.D2), [Key.D3] = new(C64Key.D3),
        [Key.D4] = new(C64Key.D4), [Key.D5] = new(C64Key.D5), [Key.D6] = new(C64Key.D6), [Key.D7] = new(C64Key.D7),
        [Key.D8] = new(C64Key.D8), [Key.D9] = new(C64Key.D9),
        [Key.Space] = new(C64Key.Space), [Key.Enter] = new(C64Key.Return), [Key.Back] = new(C64Key.Delete),
        [Key.Delete] = new(C64Key.Delete), [Key.Insert] = new(C64Key.Delete, true),
        [Key.Escape] = new(C64Key.RunStop), [Key.Tab] = new(C64Key.Commodore),
        [Key.LeftCtrl] = new(C64Key.Control), [Key.RightCtrl] = new(C64Key.Control),
        [Key.LeftShift] = new(C64Key.LeftShift), [Key.RightShift] = new(C64Key.RightShift),
        [Key.Home] = new(C64Key.Home), [Key.End] = new(C64Key.Home, true),
        [Key.Down] = new(C64Key.CursorDown), [Key.Up] = new(C64Key.CursorDown, true),
        [Key.Right] = new(C64Key.CursorRight), [Key.Left] = new(C64Key.CursorRight, true),
        [Key.F1] = new(C64Key.F1), [Key.F2] = new(C64Key.F1, true), [Key.F3] = new(C64Key.F3), [Key.F4] = new(C64Key.F3, true),
        [Key.F5] = new(C64Key.F5), [Key.F6] = new(C64Key.F5, true), [Key.F7] = new(C64Key.F7), [Key.F8] = new(C64Key.F7, true),
        [Key.OemPlus] = new(C64Key.Plus), [Key.OemMinus] = new(C64Key.Minus), [Key.Add] = new(C64Key.Plus), [Key.Subtract] = new(C64Key.Minus),
        [Key.OemComma] = new(C64Key.Comma), [Key.OemPeriod] = new(C64Key.Period), [Key.OemQuestion] = new(C64Key.Slash), [Key.Divide] = new(C64Key.Slash),
        [Key.OemSemicolon] = new(C64Key.Semicolon), [Key.OemQuotes] = new(C64Key.Colon), [Key.OemOpenBrackets] = new(C64Key.At),
        [Key.OemCloseBrackets] = new(C64Key.Asterisk), [Key.Multiply] = new(C64Key.Asterisk), [Key.OemTilde] = new(C64Key.ArrowLeft),
        [Key.OemBackslash] = new(C64Key.Pound), [Key.OemPipe] = new(C64Key.Pound), [Key.Oem8] = new(C64Key.ArrowUp),
        [Key.Decimal] = new(C64Key.Period),
    };

    public static bool TryMap(Key key, out Mapping mapping) => Map.TryGetValue(key, out mapping);

    /// <summary>Keys that are the RESTORE key (NMI).</summary>
    public static bool IsRestore(Key key) => key == Key.PageUp || key == Key.Pause;

    /// <summary>Numeric keypad joystick: 8/2/4/6 (and 7/9/1/3 diagonals), 0 / 5 / Right Ctrl = fire.</summary>
    public static bool TryMapJoystick(Key key, out bool up, out bool down, out bool left, out bool right, out bool fire)
    {
        up = down = left = right = fire = false;
        switch (key)
        {
            case Key.NumPad8: up = true; return true;
            case Key.NumPad2: down = true; return true;
            case Key.NumPad4: left = true; return true;
            case Key.NumPad6: right = true; return true;
            case Key.NumPad7: up = left = true; return true;
            case Key.NumPad9: up = right = true; return true;
            case Key.NumPad1: down = left = true; return true;
            case Key.NumPad3: down = right = true; return true;
            case Key.NumPad0:
            case Key.NumPad5:
            case Key.RightAlt:
                fire = true; return true;
            default:
                return false;
        }
    }
}
