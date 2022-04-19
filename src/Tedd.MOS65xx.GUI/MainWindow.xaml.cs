using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    private Tedd.WriteableBitmap Bitmap = new Tedd.WriteableBitmap(320, 200, 96, 96, PixelFormats.Bgra32, null);

    public MainWindow()
    {
        InitializeComponent();
        Computer = new Computer();


        _task = Task.Factory.StartNew(() => Computer.Run());
    }
}
