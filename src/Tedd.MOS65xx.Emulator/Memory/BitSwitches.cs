using System;

namespace Tedd.MOS65xx.Emulator.Memory;

public class BitSwitches
{
    private readonly Memory _memory;
    private int _baseAddress;
    private readonly int _maxMask;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="memory"></param>
    /// <param name="baseAddress"></param>
    /// <param name="maxMask">And mask for index, a mask of 0x07 will limit positioning to 8 bits and in effect roll around if over that.</param>
    public BitSwitches(Memory memory, int baseAddress, int maxMask = 0x07)
    {
        _memory = memory;
        _baseAddress = baseAddress;
        _maxMask = maxMask;
    }

    public bool this[int index]
    {
        get
        {
            _baseAddress &= _maxMask;
            var by = (UInt16)(_baseAddress + (index >> 3));
            var bi = (int)(index & 0x07);
            return _memory.Raw[by].IsBitSet(bi);
        }
        set
        {
            _baseAddress &= _maxMask;
            var by = (UInt16)(_baseAddress + (index >> 3));
            var bi = (int)(index & 0x07);
            _memory.Raw[by].SetBit(bi, value);
        }
    }
}