using System;

namespace Tedd.MOS65xx.Emulator.Memory
{
    [Flags]
    public enum MemoryBankModeEnum
    {
        RAM = 0,
        CARTROMLO = 1 << 1,
        CARTROMHI = 1 << 2,
        BASICROM = 1 << 3,
        IO = 1 << 4,
        CHARROM = 1 << 5,
        KERNAL = 1 << 6,
        KERNALROM = 1 << 6,
        ULTIMAX = 1 << 7
    }
}