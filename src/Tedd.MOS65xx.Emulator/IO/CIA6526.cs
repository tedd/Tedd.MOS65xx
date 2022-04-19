using System;

namespace Tedd.MOS65xx.Emulator.IO;

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
        return _mb.MainMemory.Raw[by].IsBitSet(bi);
    }
    public void SetBit(int index, bool value)
    {
        var by = (UInt16)(_baseAddress + (index >> 3));
        var bi = (int)(index & 0x07);
        _mb.MainMemory.Raw[by].SetBit(bi, value);
    }
}
