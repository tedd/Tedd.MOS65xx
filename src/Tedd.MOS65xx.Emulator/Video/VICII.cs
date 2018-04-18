using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tedd.MOS65xx.Emulator.Cpu.MOS6510;

namespace Tedd.MOS65xx.Emulator.Video
{
    public class VICII
    {
        // http://www.zimmers.net/cbmpics/cbm/c64/vic-ii.txt
        // http://hitmen.c02.at/temp/palstuff/

        public readonly VICRegister Reg = new VICRegister();
        public readonly VICMemory Memory;
        private readonly Computer _mb;

        public VICII(Computer mb)
        {
            _mb = mb;
            Memory= new VICMemory(mb.MainMemory);

        }
    }
}
