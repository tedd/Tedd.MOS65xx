using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tedd.MOS65xx.Emulator.Utils;

namespace Tedd.MOS65xx.Emulator.IO
{
    public class CIA6526
    {
        private readonly Computer _mb;
        private readonly ushort _baseAddress;

        public CIA6526(Computer mb, UInt16 baseAddress)
        {
            _mb = mb;
            _baseAddress = baseAddress;
        }

        public bool GetBit(int index)
        {
            var by = (UInt16)(_baseAddress + (index >> 3));
            var bi = (int)(index & 0x07);
            return BitUtils.IsBitSet(_mb.MainMemory.Raw[by], bi);
        }
        public void SetBit(int index, bool value)
        {
            var by = (UInt16)(_baseAddress + (index >> 3));
            var bi = (int)(index & 0x07);
            BitUtils.SetBit(ref _mb.MainMemory.Raw[by], bi, value);
        }
    }
}
