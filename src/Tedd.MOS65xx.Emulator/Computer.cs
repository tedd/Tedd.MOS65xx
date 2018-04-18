using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tedd.MOS65xx.Emulator.Cpu.MOS6510;
using Tedd.MOS65xx.Emulator.IO;
using Tedd.MOS65xx.Emulator.Utils;
using Tedd.MOS65xx.Emulator.Video;

namespace Tedd.MOS65xx.Emulator
{
    public class Computer
    {
        public readonly Memory.Memory MainMemory;
        public readonly Cpu6510 Cpu;
        public readonly VICII VICII;
        public readonly CIA6526 CIA1;
        public readonly CIA6526 CIA2;

    public bool Running { get; private set; }
        private bool _pausing = false;

        public Computer()
        {
            MainMemory = new Memory.Memory();
            CIA1 = new CIA6526(this, 0xDC00);
            CIA2 = new CIA6526(this, 0xDD00);
            Cpu = new Cpu6510(this);
            VICII = new VICII(this);
            

            Run();
        }


        public void Run()
        {
            _pausing = false;
            Running = true;
            // Bootstrapper();
            try
            {
                for (;;)
                {
                    if (_pausing)
                        return;
                    Cpu.ExecuteOne();
                }
            }
            finally
            {
                Running = false;
            }
        }


        public void Pause()
        {
            _pausing = true;
        }


   



    }
}
