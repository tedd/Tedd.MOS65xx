using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using Tedd.MOS65xx.Emulator;

namespace Tedd.MOS65xx.GUI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private Computer Computer;
    private Task _task;
    private Thread _runThread;
    private Tedd.WriteableBitmap Bitmap = new Tedd.WriteableBitmap(320, 200, PixelFormats.Bgra32);

    public MainWindow()
    {
        InitializeComponent();

        this.Show();

        Computer = new Computer();
        Computer.VICII.OnScreenUpdate += VICII_OnScreenUpdate;

        _runThread = new Thread(ComputerRunLoop)
        {
            IsBackground = true,
            Name = "ComputerRunThread"
        };
        _runThread.Start();


        //_task = Task.Factory.StartNew(() => Computer.Run());
    }

    private void ComputerRunLoop(object? obj)
    {
        //for(; ; )
        {
            Computer.Run();
        }
    }

    private void VICII_OnScreenUpdate(uint[] obj)
    {
        var src = (Span<uint>)obj;
        var dst = Bitmap.ToSpanUInt32();
        src.CopyTo(dst);
        Dispatcher.Invoke(() => Bitmap.Invalidate());
    }
}
