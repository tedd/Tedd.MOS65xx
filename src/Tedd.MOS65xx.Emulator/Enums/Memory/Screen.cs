using System;
namespace Tedd.MOS65xx.Emulator.Enums.Memory
{
    public enum Screen : UInt16
    {
        SPRITE1 = 0x07F8, // value*64 = start address
        SPRITE2 = 0x07F9, // value*64 = start address
        SPRITE3 = 0x07FA, // value*64 = start address
        SPRITE4 = 0x07FB, // value*64 = start address
        SPRITE5 = 0x07FC, // value*64 = start address
        SPRITE6 = 0x07FD, // value*64 = start address
        SPRITE7 = 0x07FE, // value*64 = start address
        SPRITE8 = 0x07FF, // value*64 = start address

    }
}
