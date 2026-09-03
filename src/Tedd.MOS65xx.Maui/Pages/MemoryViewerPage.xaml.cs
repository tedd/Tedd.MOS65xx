using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using Tedd.MOS65xx.Emulator.C64;
using Tedd.MOS65xx.Emulator.Cpu;
using Tedd.MOS65xx.Emulator.Media;
using Tedd.MOS65xx.Hosting;

namespace Tedd.MOS65xx.Maui.Pages;

/// <summary>
/// Hex viewer/editor for the C64 (and 1541) memory. Editing is done through the emulator runner so that the
/// machine is never touched from the UI thread while it is running; "Freeze" stops the machine so values are
/// stable and single stepping is possible.
/// </summary>
public partial class MemoryViewerPage : ContentPage
{
    private readonly EmulatorRunner _runner;
    private readonly Action<bool> _setPaused;
    private readonly ObservableCollection<Row> _rows = new();
    private readonly IDispatcherTimer _timer;
    private MemorySource _source = MemorySource.Cpu;
    private int _size = 0x10000;
    private bool _updating;
    private bool _ready;
    private int _editing;

    private enum MemorySource { Cpu, Ram, ColorRam, Vic, DriveRam, DriveCpu }

    /// <param name="runner">The runner that owns the machine.</param>
    /// <param name="setPaused">Freezes/resumes the machine and keeps the main window's menu in sync.</param>
    public MemoryViewerPage(EmulatorRunner runner, Action<bool> setPaused)
    {
        InitializeComponent();
        _runner = runner;
        _setPaused = setPaused;
        BuildColumnHeader();
        HexGrid.ItemsSource = _rows;
        BuildRows();
        Refresh();
        // Only now may Source_Changed run: it rebuilds the rows the refresh above just filled.
        _ready = true;
        SourceBox.SelectedIndex = 0;

        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(300);
        _timer.Tick += (_, _) => { if (AutoRefresh.IsChecked && !_runner.Paused) Refresh(); };
        _timer.Start();
        OnFreezeChanged();
    }

    private C64 Machine => _runner.Session.Machine;

    /// <summary>Called by the main window when the pause state changes elsewhere.</summary>
    public void OnFreezeChanged()
    {
        FreezeButton.Text = _runner.Paused ? "Resume" : "Freeze";
        StepCycleButton.IsEnabled = StepInstructionButton.IsEnabled = StepFrameButton.IsEnabled = _runner.Paused;
        Refresh();
    }

    private void Page_Unloaded(object? sender, EventArgs e) => _timer.Stop();

    /// <summary>The 00..0F column labels, laid out exactly like the cells below them.</summary>
    private void BuildColumnHeader()
    {
        ColumnHeader.Children.Add(new Label { Text = "Addr", WidthRequest = 56, FontFamily = "Consolas", FontAttributes = FontAttributes.Bold, FontSize = 12 });
        for (int i = 0; i < 16; i++)
            ColumnHeader.Children.Add(new Label
            {
                Text = i.ToString("X2"),
                WidthRequest = 34,
                FontFamily = "Consolas",
                FontAttributes = FontAttributes.Bold,
                FontSize = 12,
                HorizontalTextAlignment = TextAlignment.Center,
            });
        ColumnHeader.Children.Add(new Label { Text = "Text", Margin = new Thickness(8, 0, 0, 0), FontFamily = "Consolas", FontAttributes = FontAttributes.Bold, FontSize = 12 });
    }

    private void Freeze_Clicked(object? sender, EventArgs e)
    {
        _setPaused(!_runner.Paused);
        OnFreezeChanged();
    }

    private void Source_Changed(object? sender, EventArgs e)
    {
        if (!_ready) return;
        _source = (MemorySource)Math.Max(0, SourceBox.SelectedIndex);
        _size = _source switch
        {
            MemorySource.ColorRam => 0x400,
            MemorySource.Vic => 0x4000,
            MemorySource.DriveRam => 0x800,
            _ => 0x10000,
        };
        BuildRows();
        Refresh();
    }

    private void BuildRows()
    {
        _rows.Clear();
        int baseAddress = _source == MemorySource.ColorRam ? 0xD800 : 0;
        for (int a = 0; a < _size; a += 16)
            _rows.Add(new Row(a, baseAddress));
    }

    private void Refresh()
    {
        // Never move the ground under a cell that is being typed into.
        if (_updating || _editing > 0) return;
        _updating = true;
        try
        {
            var data = new byte[_size];
            string registers = "", disassembly = "", machine = "";
            _runner.Invoke(() =>
            {
                ReadAll(data);
                registers = FormatRegisters();
                disassembly = FormatDisassembly();
                machine = FormatMachine();
            });
            foreach (var row in _rows)
                row.Update(data);
            RegistersText.Text = registers;
            DisassemblyText.Text = disassembly;
            MachineText.Text = machine;
        }
        finally
        {
            _updating = false;
        }
    }

    private void ReadAll(byte[] data)
    {
        var m = Machine;
        switch (_source)
        {
            case MemorySource.Cpu:
                for (int i = 0; i < data.Length; i++) data[i] = m.Memory.Peek((ushort)i);
                break;
            case MemorySource.Ram:
                Array.Copy(m.Memory.Ram, data, data.Length);
                break;
            case MemorySource.ColorRam:
                Array.Copy(m.Memory.ColorRam, data, data.Length);
                break;
            case MemorySource.Vic:
                for (int i = 0; i < data.Length; i++) data[i] = m.Memory.ReadVic(i);
                break;
            case MemorySource.DriveRam:
                if (m.Drive is { } d) Array.Copy(d.Memory.Ram, data, data.Length);
                break;
            case MemorySource.DriveCpu:
                if (m.Drive is { } d2)
                    for (int i = 0; i < data.Length; i++) data[i] = d2.Memory.Peek((ushort)i);
                break;
        }
    }

    private void WriteByte(int address, byte value)
    {
        _runner.Invoke(() =>
        {
            var m = Machine;
            switch (_source)
            {
                case MemorySource.Cpu: m.Memory.Write((ushort)address, value); break;
                case MemorySource.Ram: m.Memory.Ram[address & 0xFFFF] = value; break;
                case MemorySource.ColorRam: m.Memory.ColorRam[address & 0x3FF] = (byte)(value & 0x0F); break;
                case MemorySource.Vic: m.Memory.Ram[((m.Memory.VicBank << 14) | (address & 0x3FFF)) & 0xFFFF] = value; break;
                case MemorySource.DriveRam: if (m.Drive is { } d) d.Memory.Ram[address & 0x7FF] = value; break;
                case MemorySource.DriveCpu: if (m.Drive is { } d2) d2.Memory.Write((ushort)address, value); break;
            }
        });
    }

    private string FormatRegisters()
    {
        var c = _source is MemorySource.DriveRam or MemorySource.DriveCpu ? Machine.Drive?.Cpu : Machine.Cpu;
        if (c is null) return "(no drive)";
        var sb = new StringBuilder();
        sb.Append($"PC ${c.PC:X4}  A ${c.A:X2}  X ${c.X:X2}  Y ${c.Y:X2}  S ${c.S:X2}\n");
        sb.Append("P  ").Append((c.P & 0x80) != 0 ? 'N' : '.').Append((c.P & 0x40) != 0 ? 'V' : '.').Append('-')
          .Append((c.P & 0x10) != 0 ? 'B' : '.').Append((c.P & 0x08) != 0 ? 'D' : '.').Append((c.P & 0x04) != 0 ? 'I' : '.')
          .Append((c.P & 0x02) != 0 ? 'Z' : '.').Append((c.P & 0x01) != 0 ? 'C' : '.').Append($"  (${c.P:X2})\n");
        sb.Append($"Cycles {c.Cycles:N0}  IRQ {(c.Irq ? "1" : "0")}  NMI {(c.Nmi ? "1" : "0")}  RDY {(c.Rdy ? "1" : "0")}");
        if (c.Jammed) sb.Append("  JAMMED");
        return sb.ToString();
    }

    private string FormatDisassembly()
    {
        var sb = new StringBuilder();
        bool drive = _source is MemorySource.DriveRam or MemorySource.DriveCpu;
        Func<ushort, byte> read;
        ushort pc;
        if (drive)
        {
            if (Machine.Drive is not { } d) return "";
            read = a => d.Memory.Peek(a);
            pc = d.Cpu.PC;
        }
        else
        {
            read = a => Machine.Memory.Peek(a);
            pc = Machine.Cpu.PC;
        }
        for (int i = 0; i < 12; i++)
        {
            var (line, len) = Disassembler.FormatLine(read, pc);
            sb.Append(i == 0 ? "> " : "  ").AppendLine(line);
            pc = (ushort)(pc + len);
        }
        return sb.ToString();
    }

    private string FormatMachine()
    {
        var m = Machine;
        var sb = new StringBuilder();
        sb.Append($"Frame {m.Frames}  Raster {m.Vic.RasterLine}/{m.Vic.RasterCycle}  Cycles {m.Cycles:N0}\n");
        sb.Append($"$01 = ${m.Memory.PortData:X2} (DDR ${m.Memory.PortDdr:X2})  VIC bank {m.Memory.VicBank}  Screen ${m.ScreenAddress:X4}\n");
        sb.Append($"IEC ATN {(m.Iec.AtnLow ? "L" : "H")} CLK {(m.Iec.ClkLow ? "L" : "H")} DATA {(m.Iec.DataLow ? "L" : "H")}\n");
        if (m.Drive is { } d)
            sb.Append($"1541: track {d.Disk.Track:0.#} motor {(d.MotorOn ? "on" : "off")} LED {(d.Led ? "on" : "off")} PC ${d.Cpu.PC:X4}\n");
        return sb.ToString();
    }

    private void Goto_Clicked(object? sender, EventArgs e) => GoTo();

    private void Goto_Completed(object? sender, EventArgs e) => GoTo();

    private void GoTo()
    {
        var text = (GotoBox.Text ?? "").Trim().TrimStart('$');
        if (!int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int address)) return;
        if (_source == MemorySource.ColorRam) address -= 0xD800;
        int rowIndex = Math.Clamp(address / 16, 0, _rows.Count - 1);
        HexGrid.ScrollTo(rowIndex, position: ScrollToPosition.Start, animate: false);
    }

    private void Refresh_Clicked(object? sender, EventArgs e) => Refresh();

    #region Cell editing

    private void Cell_Focused(object? sender, FocusEventArgs e) => _editing++;

    private void Cell_Unfocused(object? sender, FocusEventArgs e)
    {
        if (_editing > 0) _editing--;
        Commit(sender as Entry);
    }

    private void Cell_Completed(object? sender, EventArgs e) => Commit(sender as Entry);

    /// <summary>
    /// Writes the cell's hex value to memory. The column comes from the entry's position in its row, which is
    /// how the template lays the 16 bytes out.
    /// </summary>
    private void Commit(Entry? entry)
    {
        if (entry?.BindingContext is not Row row) return;
        if (entry.Parent is not HorizontalStackLayout line) return;
        int column = line.Children.IndexOf(entry) - 1;   // the address label is child 0
        if (column < 0 || column > 15) return;

        var text = (entry.Text ?? "").Trim().TrimStart('$');
        if (byte.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte value))
        {
            WriteByte(row.Offset + column, value);
            Dispatcher.Dispatch(Refresh);
        }
        else
        {
            entry.Text = row.Byte(column);   // put the old value back
        }
    }

    #endregion

    private void StepCycle_Clicked(object? sender, EventArgs e)
    {
        _runner.Invoke(_runner.Session.StepCycle);
        Refresh();
    }

    private void StepInstruction_Clicked(object? sender, EventArgs e)
    {
        _runner.Invoke(_runner.Session.StepInstruction);
        Refresh();
    }

    private void StepFrame_Clicked(object? sender, EventArgs e)
    {
        // Session.RunFrame also presents the frame, so the screen follows the stepping.
        _runner.Invoke(_runner.Session.RunFrame);
        Refresh();
    }

    /// <summary>One line of 16 bytes. The write happens when the cell is committed, not when the property is set.</summary>
    public sealed class Row : INotifyPropertyChanged
    {
        private readonly string[] _hex = new string[16];
        private string _text = "";

        public Row(int offset, int baseAddress)
        {
            Offset = offset;
            Address = (baseAddress + offset).ToString("X4");
            Array.Fill(_hex, "..");
        }

        public int Offset { get; }
        public string Address { get; }
        public string Text => _text;

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>The displayed value of one byte of the row.</summary>
        public string Byte(int index) => _hex[index];

        public void Update(byte[] data)
        {
            var sb = new StringBuilder(16);
            for (int i = 0; i < 16; i++)
            {
                byte b = data[Offset + i];
                var h = b.ToString("X2");
                if (_hex[i] != h)
                {
                    _hex[i] = h;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("B" + i));
                }
                char c = Petscii.ScreenCodeToAscii((byte)(b & 0x7F));
                sb.Append(c < ' ' || c > '~' ? '.' : c);
            }
            var t = sb.ToString();
            if (t != _text)
            {
                _text = t;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
            }
        }

        private string Get(int i) => _hex[i];
        private void Set(int i, string value) => _hex[i] = value; // the actual write happens on commit

        public string B0 { get => Get(0); set => Set(0, value); }
        public string B1 { get => Get(1); set => Set(1, value); }
        public string B2 { get => Get(2); set => Set(2, value); }
        public string B3 { get => Get(3); set => Set(3, value); }
        public string B4 { get => Get(4); set => Set(4, value); }
        public string B5 { get => Get(5); set => Set(5, value); }
        public string B6 { get => Get(6); set => Set(6, value); }
        public string B7 { get => Get(7); set => Set(7, value); }
        public string B8 { get => Get(8); set => Set(8, value); }
        public string B9 { get => Get(9); set => Set(9, value); }
        public string B10 { get => Get(10); set => Set(10, value); }
        public string B11 { get => Get(11); set => Set(11, value); }
        public string B12 { get => Get(12); set => Set(12, value); }
        public string B13 { get => Get(13); set => Set(13, value); }
        public string B14 { get => Get(14); set => Set(14, value); }
        public string B15 { get => Get(15); set => Set(15, value); }
    }
}
