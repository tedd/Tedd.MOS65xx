using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Tedd.MOS65xx.Emulator.Cpu.MOS6510;
using Tedd.MOS65xx.Emulator.Enums.Memory;

namespace Tedd.MOS65xx.Emulator.Video;

public class VICII
{
    // http://www.zimmers.net/cbmpics/cbm/c64/vic-ii.txt
    // http://hitmen.c02.at/temp/palstuff/

    public readonly VICRegister Reg = new VICRegister();
    public readonly VICMemory Memory;
    private readonly Computer _mb;
    public readonly UInt32[] ScreenMemory = new UInt32[320 * 200];
    public event Action<UInt32[]> OnScreenUpdate;

    public VICII(Computer mb)
    {
        _mb = mb;
        Memory = new VICMemory(mb.MainMemory);

    }

    internal void ExecuteOne()
    {
        var sb = new StringBuilder();
        for (var x = 0; x < 40; x++)
        {
            for (var y = 0; y < 25; y++)
            {
                var p = x + (y * 40);
                var c = _mb.MainMemory[p];
                sb.Append((char)c);
                var charAddr = (UInt16)c + (UInt16)Screen.DEFAULT_CHARACTER_MEMORY;
                for (var i = 0; i < 8; i++)
                {
                    var bmap = _mb.MainMemory[charAddr + i];
                    var bpos = x * 8 + i + (y * 40 * 8);
                    for (var b = 0; b < 8; b++)
                    {
                        var pos = bpos + b;
                        var isSet = bmap.IsBitSet(b);
                        if (isSet)
                            ScreenMemory[pos] = 0xFFFFFFFF;
                        else
                            ScreenMemory[pos] = 0xFF000000;
                    }
                }

                var charData = _mb.MainMemory[charAddr];
            }
            sb.AppendLine();
        }

        OnScreenUpdate?.Invoke(ScreenMemory);
        Debug.WriteLine(sb.ToString());
    }
}
