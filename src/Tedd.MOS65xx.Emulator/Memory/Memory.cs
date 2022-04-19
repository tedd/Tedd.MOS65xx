using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using Word = System.UInt16;
using MBM = Tedd.MOS65xx.Emulator.Memory.MemoryBankModeEnum;

namespace Tedd.MOS65xx.Emulator.Memory;

public class Memory
{
    // Useful info https://archive.org/stream/Commodore_64_1764_Ram_Expansion_Module_Users_Guide_1986_Commodore/Commodore_64_1764_Ram_Expansion_Module_Users_Guide_1986_Commodore_djvu.txt



    /*
     * Bits #0-#2: Configuration for memory areas $A000-$BFFF, $D000-$DFFF and $E000-$FFFF.
     * Values:
        %x00: RAM visible in all three areas.
        %x01: RAM visible at $A000-$BFFF and $E000-$FFFF.
        %x10: RAM visible at $A000-$BFFF; KERNAL ROM visible at $E000-$FFFF.
        %x11: BASIC ROM visible at $A000-$BFFF; KERNAL ROM visible at $E000-$FFFF.
        %0xx: Character ROM visible at $D000-$DFFF. (Except for the value %000, see above.)
        %1xx: I/O area visible at $D000-$DFFF. (Except for the value %100, see above.)
     */
    // From https://www.c64-wiki.com/wiki/Bank_Switching - we use a simple translation table for enum flags from "PLA Latch Bit States" column to translate into corresponding mappings in "Memory Configuration"
    public readonly MemoryBankModeEnum[] BankMapping = new MemoryBankModeEnum[]
    {
            /*0*/MBM.RAM, /*1*/MBM.RAM, /*2*/MBM.CARTROMHI|MBM.CHARROM|MBM.KERNALROM, /*3*/MBM.CARTROMLO|MBM.CARTROMHI|MBM.CHARROM|MBM.KERNALROM, /*4*/MBM.RAM,
            /*5*/MBM.IO, /*6*/MBM.CARTROMHI|MBM.IO|MBM.KERNALROM, /*7*/MBM.CARTROMLO|MBM.CARTROMHI|MBM.IO|MBM.KERNALROM, /*8*/MBM.RAM, /*9*/MBM.CHARROM,
            /*10*/MBM.CHARROM|MBM.KERNALROM, /*11*/MBM.CARTROMLO|MBM.BASICROM|MBM.CHARROM|MBM.KERNALROM, /*12*/MBM.RAM, /*13*/MBM.IO, /*14*/MBM.IO|MBM.KERNAL,
            /*15*/MBM.CARTROMLO|MBM.BASICROM|MBM.IO|MBM.KERNAL, /*16*/MBM.IO|MBM.ULTIMAX, /*17*/MBM.IO|MBM.ULTIMAX, /*18*/MBM.IO|MBM.ULTIMAX, /*19*/MBM.IO|MBM.ULTIMAX,
            /*20*/MBM.IO|MBM.ULTIMAX, /*21*/MBM.IO|MBM.ULTIMAX, /*22*/MBM.IO|MBM.ULTIMAX, /*23*/MBM.IO|MBM.ULTIMAX, /*24*/MBM.RAM, 
            /*25*/MBM.CHARROM, /*26*/MBM.CHARROM|MBM.KERNALROM, /*27*/MBM.BASICROM|MBM.CHARROM|MBM.KERNALROM, /*28*/MBM.RAM, /*29*/MBM.IO,
            /*30*/MBM.IO|MBM.KERNALROM, /*31*/MBM.BASICROM|MBM.IO|MBM.KERNALROM
    };

    public const int POS_WHITEPAGE_START = 0x0000;
    public const int POS_WHITEPAGE_END = 0x00FF;
    public const int POS_STACK_START = 0x0100;
    public const int POS_STACK_END = 0x01FF;
    public const int POS_SCREEN_START = 0x0400;
    public const int POS_SCREEN_END = 0x07FF;
    //public const int POS_COLORRAM_START = 0xD800;
    //public const int POS_COLORRAM_END = 0xDBE7;

    public const int POS_BASIC_START = 0xA000;
    public const int POS_BASIC_END = 0xBFFF;
    public const int POS_CHARROM_START = 0xD000;
    public const int POS_CHARROM_END = 0xDFFF;
    public const int POS_KERNAL_START = 0xE000;
    public const int POS_KERNAL_END = 0xFFFF;

    public const int POS_INTERRUPT = 0xFF48;
    public const int POS_PP_DATAREGISTER = 0x00;
    public const int POS_PP_BANKSWITCHES = 0x01;


    public readonly byte[] Raw;
    private readonly byte[] BasicRom;
    private readonly byte[] KernalRom;
    private readonly byte[] CharRom;
    private readonly byte[] CartRomLO;
    private readonly byte[] CartRomHI;
    ///// <summary>
    ///// Access Raw memory per memory-bank
    ///// </summary>
    //private readonly MemoryArea[] MemoryBank;


    public readonly MemoryBankSwitches BankSwitches;
    public readonly BitSwitches IOSwitches;

    /// <summary>
    /// Access stack area of Raw memory
    /// </summary>
    public readonly MemoryArea Stack;
    /// <summary>
    /// Access Whitepages of Raw memory
    /// </summary>
    public readonly MemoryArea Whitepages;

    public Memory()
    {
        Raw = new byte[1024 * 64];
        //MemoryBank = new MemoryArea[] { new MemoryArea(this, 0, 0x3FFF), new MemoryArea(this, 0x4000, 0x7FFF), new MemoryArea(this, 0x8000, 0xCFFF), new MemoryArea(this, 0xD000, 0xFFFF) };
        // Fill initial RAM with every 0 and 255 alternating every 64 bytes
        for (int i = 0; i < Raw.Length; i++)
            Raw[i] = (i >> 6) % 2 == 1 ? (byte)255 : (byte)0;
        BankSwitches = new MemoryBankSwitches(this);
        IOSwitches = new BitSwitches(this, 0x00, 0x07);
        WriteByte(0x0001, 31); // Default memory mode
        WriteByte(0x0001, 0xEF); // Default IO Data Direction (111101)


        CartRomLO = new byte[8192];
        CartRomHI = new byte[8192];

        Stack = new MemoryArea(this, POS_STACK_START, POS_STACK_END);
        Whitepages = new MemoryArea(this, POS_WHITEPAGE_START, POS_WHITEPAGE_END);

        // Load default C64 into memory
        BasicRom = LoadFile("basic.901226-01.bin", POS_BASIC_END - POS_BASIC_START + 1);
        KernalRom = LoadFile("kernal.901227-03.bin", POS_KERNAL_END - POS_KERNAL_START + 1);
        CharRom = LoadFile("characters.901225-01.bin", POS_CHARROM_END - POS_CHARROM_START + 1);

    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteWord(int pos, ushort data)
    {
        var bytes = BitConverter.GetBytes(data);
        Raw[pos + 0] = bytes[1];
        Raw[pos + 1] = bytes[0];
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteByte(int pos, byte data)
    {
        Raw[pos] = data;
    }

    public byte[] LoadFile(string filename, int length)
    {
        using (var stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            length = Math.Min(length, (int)stream.Length);
            var data = new byte[length];
            stream.Read(data, 0, length);
            return data;
        }
    }

    public int Length { get { return Raw.Length; } }
    public const int POS_MCBANK1_START = 0x0;
    public const int POS_MCBANK1_END = 0x0FFF;
    public const int POS_MCBANK2_START = 0x1000;
    public const int POS_MCBANK2_END = 0x7FFF;
    public const int POS_MCBANK3_START = 0x8000;
    public const int POS_MCBANK3_END = 0x9FFF;
    public const int POS_MCBANK4_START = 0xA000;
    public const int POS_MCBANK4_END = 0xBFFF;
    public const int POS_MCBANK5_START = 0xC000;
    public const int POS_MCBANK5_END = 0xCFFF;
    public const int POS_MCBANK6_START = 0xD000;
    public const int POS_MCBANK6_END = 0xDFFF;
    public const int POS_MCBANK7_START = 0xE000;
    public const int POS_MCBANK7_END = 0xFFFF;
    public const int POS_COLORRAM_START = 0xD800;
    public const int POS_COLORRAM_END = 0xDBFF;

    public byte this[int index]
    {
        get
        {
            //
            // This is basically an implementation of the C64 PLA
            // From https://www.c64-wiki.com/wiki/Bank_Switching
            //

            if (index <= POS_MCBANK1_END)  // Page 000-015 $0000-$0FFF: Always available
                return Raw[index];

            // Translate from memory bits to functionality table
            var mm = BankMapping[Raw[POS_PP_BANKSWITCHES] & 0x001F];

            if (index >= POS_MCBANK2_START && index <= POS_MCBANK2_END && (mm & MBM.ULTIMAX) != 0) // Page 016-127 $1000-$7FFF: RAM. Only available outside of Ultimax-mode
                return 0;

            if (index >= POS_MCBANK3_START && index <= POS_MCBANK3_END) // Page 128-159 $8000-$9FFF: RAM, CART LOW
            {
                if ((mm & MBM.CARTROMLO) != 0)
                    return CartRomLO[index - POS_MCBANK3_START];
                return Raw[index];
            }
            if (index >= POS_MCBANK4_START && index <= POS_MCBANK4_END) // Page 160-191 $A000-$BFFF: RAM, BASIC ROM, CART HIGH
            {
                if ((mm & MBM.BASICROM) != 0)
                    return BasicRom[index - POS_MCBANK4_START];
                if ((mm & MBM.CARTROMHI) != 0)
                    return CartRomHI[index - POS_MCBANK4_START];
                return Raw[index];
            }
            if (index >= POS_MCBANK5_START && index <= POS_MCBANK5_END && (mm & MBM.ULTIMAX) != 0) // Page 192-207 C000-$CFFF: RAM. Only available outside of Ultimax-mode
                return 0;
            if (index >= POS_MCBANK6_START && index <= POS_MCBANK6_END) // Page 208-223 $D000-$DFFF: RAM, IO, CHAR ROM
            {
                if ((mm & MBM.CHARROM) != 0)
                    return CharRom[index - POS_MCBANK6_START];
                if ((mm & MBM.IO) != 0)
                {
                    Trace.WriteLine($"IO-R {0:X2}<-${index}");
                    return 0; // SPECIAL CASE - READING IO
                }
                return Raw[index];
            }
            if (index >= POS_MCBANK7_START && index <= POS_MCBANK7_END) // Page 208-223 $D000-$DFFF: RAM, IO, CHAR ROM
            {
                if ((mm & MBM.KERNALROM) != 0)
                    return KernalRom[index - POS_MCBANK7_START];
                if ((mm & MBM.ULTIMAX) != 0)
                    return CartRomHI[index - POS_MCBANK7_START];
                return Raw[index];
            }
            return 0;

            //// TODO: Missing CART and IO
            //// "I/O" includes Color RAM as well ($D800-$DBFF)

            //// Outside index, return RAM
            //return Raw[index];
        }
        set
        {
            var mm = BankMapping[Raw[POS_PP_BANKSWITCHES] & 0x001F];
            if (index >= POS_MCBANK6_START && index <= POS_MCBANK6_END) // Page 208-223 $D000-$DFFF: IO
            {
                if ((mm & MBM.IO) != 0)
                {
                    //                        if (index >= POS_COLORRAM_START && index <= POS_COLORRAM_END) // Color RAM
                    //                            Event trigger?
                    Trace.WriteLine($"IO-W ${index}->{value:X2}");
                    return;
                }
            }
            if (index >= POS_MCBANK2_START && index <= POS_MCBANK2_END && (mm & MBM.ULTIMAX) != 0) // Page 016-127 $1000-$7FFF: RAM. Only available outside of Ultimax-mode
                return;
            if (index >= POS_MCBANK5_START && index <= POS_MCBANK5_END && (mm & MBM.ULTIMAX) != 0) // Page 192-207 C000-$CFFF: RAM. Only available outside of Ultimax-mode
                return;
            //Should we always write? Like when writing to color ram too?
            Raw[index] = value;
        }
    }
}
