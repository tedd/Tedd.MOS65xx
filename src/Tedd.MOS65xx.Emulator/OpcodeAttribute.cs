using System;
using Tedd.MOS65xx.Emulator.Enums;

namespace Tedd.MOS65xx.Emulator
{
    public class OpcodeAttribute : Attribute
    {
        public OpCode Opcode;
        public int Cycles;
        public Param Param;

        public OpcodeAttribute(OpCode opcode, Param parameter, int cycles)
        {
            Opcode = opcode;
            Param = parameter;
            Cycles = cycles;
        }
    }
}