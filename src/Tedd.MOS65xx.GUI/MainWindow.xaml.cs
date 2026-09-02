using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Tedd.MOS65xx.Emulator.C64;
using Tedd.MOS65xx.Emulator.Drive;
using Tedd.MOS65xx.Emulator.Media;
using Tedd.MOS65xx.Emulator.Tools;
using Tedd.MOS65xx.Emulator.Video;

namespace Tedd.MOS65xx.GUI;

public partial class MainWindow : Window
{
    private EmulatorHost? _host;
    private C64? _c64;
    private WriteableBitmap? _bitmap;
    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly Dictionary<Key, List<C64Key>> _heldKeys = new();
    private MemoryViewerWindow? _memoryViewer;
    private string? _diskPath;
    private volatile bool _frameDirty;
    private uint[]? _latestFrame;
    private readonly object _frameLock = new();
    private bool _joystickPort2 = true;

    private static readonly (int X, int Y, int Width, int Height) Visible = VicII.VisibleArea;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var roms = RomSet.TryLoadDefault();
        if (roms is null)
        {
            MessageBox.Show(this,
                "No ROM images found.\n\nPut basic.901226-01.bin (or 64c.251913-01.bin), a KERNAL and a character ROM " +
                "into a 'roms' folder next to the executable, or point the C64_ROMS environment variable at them.",
                "ROMs missing", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
            return;
        }

        _c64 = new C64(roms);
        _bitmap = new WriteableBitmap(Visible.Width, Visible.Height, 96, 96, PixelFormats.Bgra32, null);
        Screen.Source = _bitmap;
        Screen.Width = Visible.Width;
        Screen.Height = Visible.Height;

        if (roms.Drive1541 is not null)
        {
            _c64.AttachDrive(8);
            DriveMenu.IsChecked = true;
        }
        else
        {
            DriveMenu.IsEnabled = false;
        }

        _host = new EmulatorHost(_c64, enableAudio: true);
        _host.FrameRendered += OnFrameRendered;
        CompositionTarget.Rendering += OnRendering;
        _statusTimer.Tick += (_, _) => UpdateStatus();
        _statusTimer.Start();
        _host.Start();
        MediaText.Text = roms.Description;
        Focus();
    }

    private void OnFrameRendered(uint[] frame)
    {
        lock (_frameLock)
        {
            _latestFrame = frame;
            _frameDirty = true;
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (!_frameDirty || _bitmap is null) return;
        uint[]? frame;
        lock (_frameLock)
        {
            frame = _latestFrame;
            _frameDirty = false;
        }
        if (frame is null) return;
        _bitmap.Lock();
        try
        {
            int stride = _bitmap.BackBufferStride;
            var buffer = _bitmap.BackBuffer;
            for (int y = 0; y < Visible.Height; y++)
            {
                int srcRow = (Visible.Y + y) * VicII.FrameWidth + Visible.X;
                System.Runtime.InteropServices.Marshal.Copy((int[])(object)frame, srcRow, buffer + y * stride, Visible.Width);
            }
            _bitmap.AddDirtyRect(new Int32Rect(0, 0, Visible.Width, Visible.Height));
        }
        finally
        {
            _bitmap.Unlock();
        }
    }

    private void UpdateStatus()
    {
        if (_host is null || _c64 is null) return;
        FpsText.Text = $"{_host.MeasuredFps:0.0} fps";
        StateText.Text = _host.Paused ? "Frozen" : _host.Warp ? "Warp" : "Running";
        var drive = _c64.Drive;
        if (drive is null)
        {
            DriveLed.Fill = Brushes.DimGray;
            DriveText.Text = "No drive";
        }
        else
        {
            DriveLed.Fill = drive.Led ? Brushes.Red : Brushes.DarkRed;
            string disk = drive.Disk.Disk is null ? "no disk" : Path.GetFileName(_diskPath ?? "disk");
            DriveText.Text = $"8: {disk}  T{drive.Disk.Track:0.#}{(drive.MotorOn ? " *" : "")}";
        }
    }

    #region Keyboard / joystick

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (_c64 is null) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (e.IsRepeat) { e.Handled = true; return; }

        if (System.Windows.Input.Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            switch (key)
            {
                case Key.W: WarpMenu.IsChecked = !WarpMenu.IsChecked; Warp_Click(this, e); e.Handled = true; return;
                case Key.M: MemoryViewer_Click(this, e); e.Handled = true; return;
            }
        }
        switch (key)
        {
            case Key.F9: AttachDisk_Click(this, e); e.Handled = true; return;
            case Key.F10: AttachTape_Click(this, e); e.Handled = true; return;
            case Key.F11: Reset_Click(this, e); e.Handled = true; return;
            case Key.F12: Screenshot_Click(this, e); e.Handled = true; return;
            case Key.Pause: PauseMenu.IsChecked = !PauseMenu.IsChecked; Pause_Click(this, e); e.Handled = true; return;
        }

        if (KeyMapper.TryMapJoystick(key, out bool up, out bool down, out bool left, out bool right, out bool fire))
        {
            var j = _joystickPort2 ? _c64.Joystick2 : _c64.Joystick1;
            if (up) j.Up = true;
            if (down) j.Down = true;
            if (left) j.Left = true;
            if (right) j.Right = true;
            if (fire) j.Fire = true;
            e.Handled = true;
            return;
        }
        if (KeyMapper.IsRestore(key))
        {
            _c64.Keyboard.SetRestore(true);
            e.Handled = true;
            return;
        }
        if (KeyMapper.TryMap(key, out var m) && !_heldKeys.ContainsKey(key))
        {
            var list = new List<C64Key> { m.Key };
            if (m.Shift) list.Add(C64Key.LeftShift);
            foreach (var k in list) _c64.Keyboard.Press(k);
            _heldKeys[key] = list;
            e.Handled = true;
        }
    }

    private void Window_KeyUp(object sender, KeyEventArgs e)
    {
        if (_c64 is null) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (KeyMapper.TryMapJoystick(key, out bool up, out bool down, out bool left, out bool right, out bool fire))
        {
            var j = _joystickPort2 ? _c64.Joystick2 : _c64.Joystick1;
            if (up) j.Up = false;
            if (down) j.Down = false;
            if (left) j.Left = false;
            if (right) j.Right = false;
            if (fire) j.Fire = false;
            e.Handled = true;
            return;
        }
        if (KeyMapper.IsRestore(key))
        {
            _c64.Keyboard.SetRestore(false);
            e.Handled = true;
            return;
        }
        if (_heldKeys.Remove(key, out var list))
        {
            foreach (var k in list)
            {
                if (k is C64Key.LeftShift && IsShiftHeldByOther(key)) continue;
                _c64.Keyboard.Release(k);
            }
            e.Handled = true;
        }
    }

    private bool IsShiftHeldByOther(Key except)
    {
        foreach (var (k, list) in _heldKeys)
            if (k != except && list.Contains(C64Key.LeftShift)) return true;
        return false;
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        _c64?.Keyboard.ReleaseAll();
        _c64?.Joystick1.Clear();
        _c64?.Joystick2.Clear();
        _heldKeys.Clear();
    }

    #endregion

    #region Menu handlers

    private void AttachDisk_Click(object sender, RoutedEventArgs e) => AttachDisk(autostart: false);
    private void AttachDiskAutostart_Click(object sender, RoutedEventArgs e) => AttachDisk(autostart: true);

    private void AttachDisk(bool autostart)
    {
        if (_c64 is null || _host is null) return;
        if (_c64.Drive is null)
        {
            MessageBox.Show(this, "No 1541 ROM available, the drive cannot be enabled.", "Drive", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var dlg = new OpenFileDialog { Filter = "Disk images (*.d64)|*.d64|All files|*.*", Title = "Attach disk image" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var d64 = D64Image.Load(dlg.FileName);
            var gcr = GcrDisk.FromD64(d64);
            _diskPath = dlg.FileName;
            _host.Invoke(() =>
            {
                _c64.Drive!.InsertDisk(gcr, writeProtected: false);
                if (autostart)
                    _c64.AutostartFromDisk("*");
            });
            MediaText.Text = $"Disk: {Path.GetFileName(dlg.FileName)} ({d64.DiskName})";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Cannot attach disk", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void EjectDisk_Click(object sender, RoutedEventArgs e)
    {
        if (_c64?.Drive is null || _host is null) return;
        _host.Invoke(() => _c64.Drive!.InsertDisk(null));
        _diskPath = null;
    }

    private void SaveDisk_Click(object sender, RoutedEventArgs e)
    {
        if (_c64?.Drive?.Disk.Disk is null || _host is null) return;
        var dlg = new SaveFileDialog { Filter = "Disk images (*.d64)|*.d64", FileName = _diskPath is null ? "disk.d64" : Path.GetFileName(_diskPath) };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var d64 = _host.Invoke(() => _c64.Drive!.Disk.Disk!.ToD64());
            d64.Save(dlg.FileName);
            _host.Invoke(() => _c64.Drive!.Disk.MarkSaved());
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Cannot save disk", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AttachTape_Click(object sender, RoutedEventArgs e)
    {
        if (_c64 is null || _host is null) return;
        var dlg = new OpenFileDialog { Filter = "Tape images and programs (*.t64;*.prg)|*.t64;*.prg|All files|*.*", Title = "Attach tape image / program" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var data = File.ReadAllBytes(dlg.FileName);
            PrgFile program;
            string name;
            if (T64Image.IsT64(data))
            {
                var t64 = T64Image.Load(data);
                if (t64.Entries.Count == 0) throw new InvalidDataException("The tape image contains no files");
                int index = 0;
                if (t64.Entries.Count > 1)
                {
                    var chooser = new SelectEntryWindow(t64) { Owner = this };
                    if (chooser.ShowDialog() != true) return;
                    index = chooser.SelectedIndex;
                }
                program = t64.GetProgram(index);
                name = t64.Entries[index].Name;
            }
            else
            {
                program = PrgFile.FromBytes(data);
                name = Path.GetFileName(dlg.FileName);
            }
            _host.Invoke(() => _c64.InjectProgram(program, run: true));
            MediaText.Text = $"Program: {name} (${program.LoadAddress:X4}-${program.LoadAddress + program.Data.Length - 1:X4})";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Cannot load program", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AttachCartridge_Click(object sender, RoutedEventArgs e)
    {
        if (_c64 is null || _host is null) return;
        var dlg = new OpenFileDialog { Filter = "Cartridge images (*.crt;*.bin)|*.crt;*.bin|All files|*.*", Title = "Attach cartridge" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var cart = Cartridge.Load(dlg.FileName);
            _host.Invoke(() =>
            {
                _c64.AttachCartridge(cart);
                _c64.Reset(hard: false);
            });
            MediaText.Text = $"Cartridge: {cart.Name}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Cannot attach cartridge", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DetachCartridge_Click(object sender, RoutedEventArgs e)
    {
        if (_c64 is null || _host is null) return;
        _host.Invoke(() =>
        {
            _c64.AttachCartridge(null);
            _c64.Reset(hard: false);
        });
    }

    private void Screenshot_Click(object sender, RoutedEventArgs e)
    {
        if (_c64 is null || _host is null) return;
        var dlg = new SaveFileDialog { Filter = "PNG image (*.png)|*.png", FileName = $"c64-{DateTime.Now:yyyyMMdd-HHmmss}.png" };
        if (dlg.ShowDialog(this) != true) return;
        var frame = _host.Invoke(() => (uint[])_c64.Vic.Frame.Clone());
        var crop = new uint[Visible.Width * Visible.Height];
        for (int y = 0; y < Visible.Height; y++)
            Array.Copy(frame, (Visible.Y + y) * VicII.FrameWidth + Visible.X, crop, y * Visible.Width, Visible.Width);
        PngWriter.Save(dlg.FileName, Visible.Width, Visible.Height, crop);
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void Reset_Click(object sender, RoutedEventArgs e) => _host?.Invoke(() => _c64!.Reset(hard: false));
    private void HardReset_Click(object sender, RoutedEventArgs e) => _host?.Invoke(() => _c64!.Reset(hard: true));

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        if (_host is null) return;
        _host.Paused = PauseMenu.IsChecked;
        _memoryViewer?.OnFreezeChanged();
    }

    private void Warp_Click(object sender, RoutedEventArgs e)
    {
        if (_host is null) return;
        _host.Warp = WarpMenu.IsChecked;
    }

    private void Drive_Click(object sender, RoutedEventArgs e)
    {
        if (_c64 is null || _host is null) return;
        _host.Invoke(() =>
        {
            if (DriveMenu.IsChecked)
                _c64.AttachDrive(8);
            else
                _c64.DetachDrive();
        });
    }

    private void JoyPort_Click(object sender, RoutedEventArgs e)
    {
        _joystickPort2 = ReferenceEquals(sender, JoyPort2Menu);
        JoyPort1Menu.IsChecked = !_joystickPort2;
        JoyPort2Menu.IsChecked = _joystickPort2;
        _c64?.Joystick1.Clear();
        _c64?.Joystick2.Clear();
    }

    private void MemoryViewer_Click(object sender, RoutedEventArgs e)
    {
        if (_c64 is null || _host is null) return;
        if (_memoryViewer is null || !_memoryViewer.IsLoaded)
        {
            _memoryViewer = new MemoryViewerWindow(_host, PauseMenu) { Owner = this };
            _memoryViewer.Closed += (_, _) => _memoryViewer = null;
        }
        _memoryViewer.Show();
        _memoryViewer.Activate();
    }

    private void TypeText_Click(object sender, RoutedEventArgs e)
    {
        if (_c64 is null || _host is null) return;
        var w = new TypeTextWindow { Owner = this };
        if (w.ShowDialog() == true)
            _host.Invoke(() => _c64.TypeText(w.Text));
    }

    private void KeyboardHelp_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(this,
            "Keys are mapped by position where possible.\n\n" +
            "Escape = RUN/STOP, Tab = C=, Backspace = INST/DEL, Home/End = CLR/HOME, Page Up = RESTORE\n" +
            "Cursor keys = CRSR keys, F1-F8 = function keys, Ctrl = CTRL\n" +
            "[ = @, ] = *, ` = <-, \\ = £\n\n" +
            "Joystick: numeric keypad 8/2/4/6 (7/9/1/3 diagonals), 0 / 5 / Right Alt = fire (port selectable in Machine menu)\n\n" +
            "F9 attach disk, F10 attach program, F11 reset, F12 screenshot, Pause = freeze, Alt+W warp, Alt+M memory viewer",
            "Keyboard", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(this, "Tedd.MOS65xx\nA cycle-exact Commodore 64 emulator in C#.\n\nhttps://github.com/tedd/Tedd.MOS65xx",
            "About", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    #endregion

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        CompositionTarget.Rendering -= OnRendering;
        _statusTimer.Stop();
        _memoryViewer?.Close();
        _host?.Dispose();
        _host = null;
    }
}
