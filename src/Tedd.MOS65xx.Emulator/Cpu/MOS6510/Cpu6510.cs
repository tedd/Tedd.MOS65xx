#define OPCODE_DEBUGGING
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Tedd.MOS65xx.Emulator.Enums;
using Word = System.UInt16;
using OC = Tedd.MOS65xx.Emulator.Enums.OpCode;
using PC = Tedd.MOS65xx.Emulator.Enums.PlusCycles;

namespace Tedd.MOS65xx.Emulator.Cpu.MOS6510
{
    public class Cpu6510
    {
        public struct CpuOpcode
        {
            public readonly int Cycles;
            public readonly Action Action;

            public CpuOpcode(int cycles, Action action)
            {
                Cycles = cycles;
                Action = action;
            }
        }

        public struct CurrentExecutingOpcodeStruct
        {
            public OpCode Opcode;
            public Word Address;
            public byte Data;
            public MemoryAccessType MemoryAccessType;
            public Word PC;
            public byte B1;
            public byte B2;
            public CpuRegister RegBefore;
        }

        private CurrentExecutingOpcodeStruct CurrentExecutingOpcode;
        private readonly Memory.Memory _memory;
        public readonly CpuRegister Reg;
        private readonly CpuOpcode[] _opcodes = new CpuOpcode[256];
        private long Cycle = 0;

        public enum MemoryAccessType
        {
            None = 0,
            Immediate = 1,
            Zpg = 2,
            ZpgX = 3,
            ZpgY = 4,
            Abs = 5,
            AbsX = 6,
            AbsY = 7,
            Indirect = 8,
            IndirectY = 9,
            XIndirect = 19
        }
        #region OPCODE_DEBUGGING

        private string[] _opcodeStr = new string[256];
        private Computer _mb;

        #endregion
        public Cpu6510(Computer mb)
        {
            #region OPCODE_DEBUGGING

            foreach (var oc in Enum.GetValues(typeof(OpCode)) as OpCode[])
                _opcodeStr[(int)oc] = oc.ToString().Split('_')[0];
            #endregion

            _mb = mb;
            _memory = mb.MainMemory;
            Reg = new CpuRegister();
            SetupOpcodeDictionary();
        }

        public void ExecuteOne()
        {
            Cycle++;
            var opcodePos = Reg.ProgramCounter;
            var opcode = (OpCode)_memory[Reg.ProgramCounter++];
            var action = _opcodes[(int)opcode];
            if (action.Action == null)
                throw new Exception($"Unknown opcode: ${(int)opcode:X} ({opcode}) at pos ${opcodePos:X} ({opcodePos})");
            #region OPCODE_DEBUGGING
            CurrentExecutingOpcode = new CurrentExecutingOpcodeStruct();
            CurrentExecutingOpcode.RegBefore = Reg;
            CurrentExecutingOpcode.PC = (Word)opcodePos;
            CurrentExecutingOpcode.Opcode = opcode;
            if (Reg.ProgramCounter < _memory.Length)
                CurrentExecutingOpcode.B1 = _memory[Reg.ProgramCounter];
            if (Reg.ProgramCounter + 1 < _memory.Length)
                CurrentExecutingOpcode.B2 = _memory[Reg.ProgramCounter + 1];
            #endregion

            try
            {
                action.Action.Invoke();
                DebugPrintOpcode(CurrentExecutingOpcode);
            }
            catch (Exception exception)
            {
                DebugPrintOpcode(CurrentExecutingOpcode);
                throw;
            }

        }

        [Conditional("OPCODE_DEBUGGING")]
        private void DebugPrintOpcode(CurrentExecutingOpcodeStruct ceo)
        {
            string addr;
            var word = false;
            switch (ceo.MemoryAccessType)
            {
                case MemoryAccessType.None:
                    addr = "".PadRight(10, ' ') + "; ";
                    break;
                case MemoryAccessType.Immediate:
                    addr = ("#" + ceo.Data.ToString("X2")).PadRight(10, ' ') + "; ";
                    break;
                case MemoryAccessType.Abs:
                    addr = ("$" + ceo.Address.ToString("X4")).PadRight(10, ' ') + "; #" + ceo.Data.ToString("X2");
                    word = true;
                    break;
                case MemoryAccessType.AbsX:
                    addr = ("$" + ceo.Address.ToString("X4") + ",X").PadRight(10, ' ') + "; #" + ceo.Data.ToString("X2");
                    word = true;
                    break;
                case MemoryAccessType.AbsY:
                    addr = ("$" + ceo.Address.ToString("X4") + ",Y").PadRight(10, ' ') + "; #" + ceo.Data.ToString("X2");
                    word = true;
                    break;
                case MemoryAccessType.Zpg:
                    addr = ("$" + ceo.Address.ToString("X2")).PadRight(10, ' ') + "; #" + ceo.Data.ToString("X2");
                    break;
                case MemoryAccessType.ZpgX:
                    addr = ("$" + ceo.Address.ToString("X2") + ",X").PadRight(10, ' ') + "; #" + ceo.Data.ToString("X2");
                    break;
                case MemoryAccessType.ZpgY:
                    addr = ("$" + ceo.Address.ToString("X2") + ",Y").PadRight(10, ' ') + "; #" + ceo.Data.ToString("X2");
                    break;
                case MemoryAccessType.Indirect:
                    addr = ("($" + ceo.Address.ToString("X4") + ")").PadRight(10, ' ') + "; #" + ceo.Data.ToString("X2");
                    word = true;
                    break;
                case MemoryAccessType.XIndirect:
                    addr = ("($" + ceo.Address.ToString("X2") + ",X)").PadRight(10, ' ') + "; #" + ceo.Data.ToString("X2");
                    break;
                case MemoryAccessType.IndirectY:
                    addr = ("($" + ceo.Address.ToString("X2") + "),Y").PadRight(10, ' ') + "; #" + ceo.Data.ToString("X2");
                    break;
                default:
                    addr = "[UNKNOWN]";
                    break;
            }

            var str = ("$" + ceo.PC.ToString("X4")
                + " " + (
                    ((int)ceo.Opcode).ToString("X2")
                    + (ceo.MemoryAccessType != MemoryAccessType.None ? " " + ceo.B1.ToString("X2") : "")
                    + (word ? " " + ceo.B2.ToString("X2") : "")
                ).PadRight(9, ' ')
                + _opcodeStr[(int)ceo.Opcode]
                + " " + addr.PadRight(20, ' ')
                )
                + "A:" + Reg.A.ToString("X2") + (Reg.A != ceo.RegBefore.A ? "->" + ceo.RegBefore.A.ToString("X2") : "    ")
                + " X:" + Reg.X.ToString("X2") + (Reg.X != ceo.RegBefore.X ? "->" + ceo.RegBefore.X.ToString("X2") : "    ")
                + " Y:" + Reg.Y.ToString("X2") + (Reg.Y != ceo.RegBefore.Y ? "->" + ceo.RegBefore.Y.ToString("X2") : "    ")
                + " SP:" + Reg.SP.ToString("X2") + (Reg.SP != ceo.RegBefore.SP ? "->" + ceo.RegBefore.SP.ToString("X2") : "    ")
                + " "
                + (Reg.Flag.N ? (Reg.Flag.N == ceo.RegBefore.Flag.N ? "N" : "n") : ".")
                + (Reg.Flag.V ? (Reg.Flag.V == ceo.RegBefore.Flag.V ? "V" : "v") : ".")
                + " "
                + (Reg.Flag.B ? (Reg.Flag.B == ceo.RegBefore.Flag.B ? "B" : "b") : ".")
                + (Reg.Flag.D ? (Reg.Flag.D == ceo.RegBefore.Flag.D ? "D" : "d") : ".")
                + (Reg.Flag.I ? (Reg.Flag.I == ceo.RegBefore.Flag.I ? "I" : "i") : ".")
                + (Reg.Flag.Z ? (Reg.Flag.Z == ceo.RegBefore.Flag.Z ? "Z" : "z") : ".")
                + (Reg.Flag.C ? (Reg.Flag.C == ceo.RegBefore.Flag.C ? "C" : "c") : ".")
                + " " + ceo.Opcode
                ;

            Trace.WriteLine(str);
        }

        [Conditional("OPCODE_DEBUGGING")]
        private void LOR(MemoryAccessType mat, Word pos, byte b)
        {
            CurrentExecutingOpcode.MemoryAccessType = mat;
            CurrentExecutingOpcode.Address = pos;
            CurrentExecutingOpcode.Data = b;
        }
        #region Memory access methods
        /// <summary>
        /// Next byte directly
        /// </summary>
        /// <returns>Byte</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadImmediate()
        {
            var b = _memory[Reg.ProgramCounter++];
            LOR(MemoryAccessType.Immediate, 0, b);
            return b;
        }

        /// <summary>
        /// Next byte is address (in zpg)
        /// </summary>
        /// <returns>Byte from address calculated</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadZpg()
        {
            // Read next byte which describes address in zero page. Add offset (if any).
            var pos = _memory[Reg.ProgramCounter++];
            var b = _memory[pos];
            LOR(MemoryAccessType.Zpg, pos, b);
            return b;
        }

        /// <summary>
        /// Next byte + RegX is address (in zpg). Rolls over on overflow.
        /// </summary>
        /// <returns>Byte from address calculated</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadZpgX()
        {
            // Read next byte which describes address in zero page. Add offset (if any).
            var pos = (byte)(_memory[Reg.ProgramCounter++] + Reg.X);
            var b = _memory[pos];
            LOR(MemoryAccessType.ZpgX, pos, b);
            return b;
        }
        /// <summary>
        /// Next byte + RegY is address (in zpg). Rolls over on overflow.
        /// </summary>
        /// <returns>Byte from address calculated</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadZpgY()
        {
            // Read next byte which describes address in zero page. Add offset (if any).
            var pos = (byte)(_memory[Reg.ProgramCounter++] + Reg.Y);
            var b = _memory[pos];
            LOR(MemoryAccessType.ZpgY, pos, b);
            return b;
        }
        /// <summary>
        /// Next Word is absolute address, returned as address
        /// </summary>
        /// <returns>Byte from address</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Word ReadAbsAddr()
        {
            var pos = ReadWord();
            LOR(MemoryAccessType.Abs, pos, 0);
            return pos;
        }
        /// <summary>
        /// Next Word is absolute address for byte returned
        /// </summary>
        /// <returns>Byte from address</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadAbs()
        {
            var pos = ReadWord();
            var b = _memory[pos];
            LOR(MemoryAccessType.Abs, pos, b);
            return b;
        }
        /// <summary>
        /// Next Word+RegX is absolute address for byte returned. Rolls over on overflow.
        /// </summary>
        /// <returns>Byte from address calculated</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadAbsX()
        {
            var pos = ReadWord();
            var b = _memory[(Word)(pos + Reg.X)];
            LOR(MemoryAccessType.AbsX, pos, b);
            return b;
        }
        /// <summary>
        /// Next Word+RegY is absolute address for byte returned. Rolls over on overflow.
        /// </summary>
        /// <returns>Byte from address calculated</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadAbsY()
        {
            var pos = ReadWord();
            var b = _memory[(Word)(pos + Reg.Y)];
            LOR(MemoryAccessType.AbsY, pos, b);
            return b;

        }
        /// <summary>
        /// Double jump: Next (byte+RegX) points to memory location 1 that is address for location 2
        /// </summary>
        /// <returns>Byte of address calculated</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadXIndirect()
        {
            return _memory[ReadXIndirectAddr()];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Word ReadXIndirectAddr()
        {
            var pos = ReadWord((Word)(ReadImmediate() + Reg.X));
            LOR(MemoryAccessType.XIndirect, pos, _memory[pos]);
            return pos;
        }
        /// <summary>
        /// Read Indirect is the same as ReadAbs() with CPU bug in C64 added
        /// </summary>
        /// <returns>Byte of address calculated</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadIndirect()
        {
            var pos = ReadWord();
            // Low byte is as pos
            var newPos = ((int)_memory[pos]);
            // High byte is at pos+1, unless we crossed page boundary. See https://everything2.com/title/6502+indirect+JMP+bug
            if (IsSamePage(pos, pos + 1))
                newPos |= ((int)_memory[pos + 1] << 8); // Get from next pos
            else
                newPos |= ((int)_memory[pos & 0xFF00] << 8); // Get from beginning of same page

            // Return byte at new position
            var b = _memory[(Word)newPos];
            LOR(MemoryAccessType.Indirect, (Word)newPos, b);
            return b;
        }
        /// <summary>
        /// Double jump: Next byte points to memory location 1 that+RegY is address for location 2
        /// </summary>
        /// <returns>Byte of address calculated</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadIndirectY()
        {
            return _memory[ReadIndirectYAddr()];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Word ReadIndirectYAddr()
        {
            var pos = (Word)(ReadWord(ReadImmediate()) + Reg.Y);
            LOR(MemoryAccessType.IndirectY, pos, _memory[pos]);
            return pos;
        }
        /// <summary>
        /// Just return net byte
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadByte()
        {
#if OPCODE_DEBUGGING
            CurrentExecutingOpcode.MemoryAccessType = MemoryAccessType.Immediate;
            CurrentExecutingOpcode.B1 = CurrentExecutingOpcode.B2 = _memory[Reg.ProgramCounter];
#endif
            return _memory[Reg.ProgramCounter++];
        }
        /// <summary>
        /// Return next two bytes as 16-bit unsigned word
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Word ReadWord()
        {
            var val = (Word)(((Word)_memory[Reg.ProgramCounter++]) | ((Word)_memory[Reg.ProgramCounter++] << 8));
#if OPCODE_DEBUGGING
            CurrentExecutingOpcode.MemoryAccessType = MemoryAccessType.Abs;
            CurrentExecutingOpcode.B1 = _memory[Reg.ProgramCounter];
            CurrentExecutingOpcode.B2 = _memory[Reg.ProgramCounter + 1];
            CurrentExecutingOpcode.Address = val;
#endif
            return val;
        }
        /// <summary>
        /// Return next two bytes as 16-bit unsigned word
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Word ReadWord(Word pos)
        {
            return (Word)(((UInt16)_memory[pos]) | ((UInt16)_memory[pos]) << 8);
        }
        /// <summary>
        /// Write to absolut memory address
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(int pos, byte value)
        {
            _memory[pos] = value;
        }
        /// <summary>
        /// Read from absolute memory address
        /// </summary>
        /// <param name="pos"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte Read(int pos)
        {
            return _memory[pos];
        }
        #endregion

        #region Stack
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PushStack(byte pos)
        {
            _memory.Stack[Reg.SP--] = pos;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PushStack(Word pos)
        {
            var bytes = BitConverter.GetBytes(pos);
            _memory.Stack[Reg.SP--] = bytes[0];
            _memory.Stack[Reg.SP--] = bytes[1];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte PopStackByte()
        {
            return _memory.Stack[Reg.SP--];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Word PopStackWord()
        {
            var bytes = new byte[2];
            bytes[1] = _memory.Stack[++Reg.SP];
            bytes[0] = _memory.Stack[++Reg.SP];
            return BitConverter.ToUInt16(bytes, 0);
        }
        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsSamePage(int a, int b)
        {
            return (a & 0xFF00) == (b & 0xFF00);
        }

        private void SetupOpcodeDictionary()
        {
            // ADC  Add Memory to Accumulator with Carry
            AddOC(2, PC.NO, OC.ADC_immediate, () => { ADC(ReadImmediate()); });
            AddOC(3, PC.NO, OC.ADC_zpg, () => { ADC(ReadZpg()); });
            AddOC(4, PC.NO, OC.ADC_zpg_X, () => { ADC(ReadZpgX()); });
            AddOC(4, PC.NO, OC.ADC_abs, () => { ADC(ReadAbs()); });
            AddOC(4, PC.PC, OC.ADC_abs_X, () => { ADC(ReadAbsX()); });
            AddOC(4, PC.PC, OC.ADC_abs_Y, () => { ADC(ReadAbsY()); });
            AddOC(6, PC.NO, OC.ADC_X_ind, () => { ADC(ReadXIndirect()); });
            AddOC(5, PC.PC, OC.ADC_ind_Y, () => { ADC(ReadIndirectY()); });

            // AND  AND Memory with Accumulator
            AddOC(5, PC.NO, OC.AND_immediate, () => { Reg.A &= ReadImmediate(); SetFlagsNz(Reg.A); });
            AddOC(5, PC.NO, OC.AND_zpg, () => { Reg.A &= ReadZpg(); SetFlagsNz(Reg.A); });
            AddOC(5, PC.NO, OC.AND_zpg_X, () => { Reg.A &= ReadZpgX(); SetFlagsNz(Reg.A); });
            AddOC(5, PC.NO, OC.AND_abs, () => { Reg.A &= ReadAbs(); SetFlagsNz(Reg.A); });
            AddOC(5, PC.PC, OC.AND_abs_X, () => { Reg.A &= ReadAbsX(); SetFlagsNz(Reg.A); });
            AddOC(5, PC.PC, OC.AND_abs_Y, () => { Reg.A &= ReadAbsY(); SetFlagsNz(Reg.A); });
            AddOC(6, PC.NO, OC.AND_X_ind, () => { Reg.A &= ReadXIndirect(); SetFlagsNz(Reg.A); });
            AddOC(5, PC.PC, OC.AND_ind_Y, () => { Reg.A &= ReadIndirectY(); SetFlagsNz(Reg.A); });

            // ASL  Shift Left One Bit (Memory or Accumulator)
            AddOC(2, PC.NO, OC.ASL_A, () => { Reg.A = ASL(Reg.A); });
            AddOC(5, PC.NO, OC.ASL_zpg, () => { ASL_pos(ReadByte()); });
            AddOC(6, PC.NO, OC.ASL_zpg_X, () => { ASL_pos((byte)(ReadByte() + Reg.X)); });
            AddOC(6, PC.NO, OC.ASL_abs, () => { ASL_pos(ReadWord()); });
            AddOC(7, PC.NO, OC.ASL_abs_X, () => { ASL_pos((Word)(ReadWord() + Reg.X)); });

            // BCC  Branch on Carry Clear
            AddOC(2, PC.BC, OC.BCC_rel, () => { var im = (sbyte)ReadImmediate(); if (!Reg.Flag.C) Reg.ProgramCounter += im; });
            // BCS  Branch on Carry Set
            AddOC(2, PC.BC, OC.BCS_rel, () => { var im = (sbyte)ReadImmediate(); if (Reg.Flag.C) Reg.ProgramCounter += im; });
            // BEQ  Branch on Result Zero
            AddOC(2, PC.BC, OC.BEQ_rel, () => { var im = (sbyte)ReadImmediate(); if (Reg.Flag.Z) Reg.ProgramCounter += im; });
            // BIT  Test Bits in Memory with Accumulator
            // bits 7 and 6 of operand are transfered to bit 7 and 6 of SR (N,V); the zeroflag is set to the result of operand AND accumulator.
            AddOC(3, PC.NO, OC.BIT_zpg, () => { BIT(ReadImmediate()); });
            AddOC(4, PC.NO, OC.BIT_abs, () => { BIT(ReadAbs()); });

            // BMI  Branch on Result Minus
            AddOC(2, PC.BC, OC.BMI_rel, () => { var im = (sbyte)ReadImmediate(); if (Reg.Flag.N) Reg.ProgramCounter += im; });
            // BNE  Branch on Result not Zero
            AddOC(2, PC.BC, OC.BNE_rel, () => { var im = (sbyte)ReadImmediate(); if (!Reg.Flag.Z) Reg.ProgramCounter += im; });
            // BPL  Branch on Result Plus
            AddOC(2, PC.BC, OC.BPL_rel, () => { var im = (sbyte)ReadImmediate(); if (!Reg.Flag.N) Reg.ProgramCounter += im; });
            // BRK  Force Break
            AddOC(7, PC.NO, OC.BRK_impl, () => { BRK(); });

            // BVC  Branch on Overflow Clear
            AddOC(2, PC.BC, OC.BVC_rel, () => { var im = (sbyte)ReadImmediate(); if (!Reg.Flag.V) Reg.ProgramCounter += im; });
            // BVS  Branch on Overflow Set
            AddOC(2, PC.BC, OC.BVS_rel, () => { var im = (sbyte)ReadImmediate(); if (Reg.Flag.V) Reg.ProgramCounter += im; });

            // CL* Clear * Flag
            AddOC(2, PC.NO, OC.CLC_impl, () => { Reg.Flag.C = false; });
            AddOC(2, PC.NO, OC.CLD_impl, () => { Reg.Flag.D = false; });
            AddOC(2, PC.NO, OC.CLI_impl, () => { Reg.Flag.I = false; });
            AddOC(2, PC.NO, OC.CLV_impl, () => { Reg.Flag.V = false; });

            // CMP  Compare Memory with Accumulator
            AddOC(2, PC.NO, OC.CMP_immediate, () => { CMP(Reg.A, ReadImmediate()); });
            AddOC(3, PC.NO, OC.CMP_zpg, () => { CMP(Reg.A, ReadZpg()); });
            AddOC(4, PC.NO, OC.CMP_zpg_X, () => { CMP(Reg.A, ReadZpgX()); });
            AddOC(4, PC.NO, OC.CMP_abs, () => { CMP(Reg.A, ReadAbs()); });
            AddOC(4, PC.PC, OC.CMP_abs_X, () => { CMP(Reg.A, ReadAbsX()); });
            AddOC(4, PC.PC, OC.CMP_abs_Y, () => { CMP(Reg.A, ReadAbsY()); });
            AddOC(6, PC.NO, OC.CMP_X_ind, () => { CMP(Reg.A, ReadXIndirect()); });
            AddOC(5, PC.PC, OC.CMP_ind_Y, () => { CMP(Reg.A, ReadIndirectY()); });

            // CPX  Compare Memory and Index X
            AddOC(2, PC.NO, OC.CPX_immediate, () => { CMP(Reg.X, ReadImmediate()); });
            AddOC(3, PC.NO, OC.CPX_zpg, () => { CMP(Reg.X, ReadZpg()); });
            AddOC(4, PC.NO, OC.CPX_abs, () => { CMP(Reg.X, ReadAbs()); });

            // CPY  Compare Memory and Index Y
            AddOC(2, PC.NO, OC.CPY_immediate, () => { CMP(Reg.Y, ReadImmediate()); });
            AddOC(3, PC.NO, OC.CPY_zpg, () => { CMP(Reg.Y, ReadZpg()); });
            AddOC(4, PC.NO, OC.CPY_abs, () => { CMP(Reg.Y, ReadAbs()); });

            // DEC  Decrement Memory by One
            AddOC(5, PC.NO, OC.DEC_zpg, () => { DEC(ReadByte()); });
            AddOC(6, PC.NO, OC.DEC_zpg_X, () => { DEC((byte)(ReadByte() + Reg.X)); });
            AddOC(3, PC.NO, OC.DEC_abs, () => { DEC(ReadWord()); });
            AddOC(7, PC.NO, OC.DEC_abs_X, () => { DEC((Word)(ReadWord() + Reg.X)); });

            // DE* Decrement Index * by One
            AddOC(2, PC.NO, OC.DEX_impl, () => { Reg.X--; SetFlagsNz(Reg.X); });
            AddOC(2, PC.NO, OC.DEY_impl, () => { Reg.Y--; SetFlagsNz(Reg.Y); });

            // EOR  Exclusive-OR Memory with Accumulator
            AddOC(2, PC.NO, OC.EOR_immediate, () => { Reg.A ^= ReadImmediate(); SetFlagsNz(Reg.A); });
            AddOC(3, PC.NO, OC.EOR_zpg, () => { Reg.A ^= ReadZpg(); SetFlagsNz(Reg.A); });
            AddOC(4, PC.NO, OC.EOR_zpg_X, () => { Reg.A ^= ReadZpgX(); SetFlagsNz(Reg.A); });
            AddOC(4, PC.NO, OC.EOR_abs, () => { Reg.A ^= ReadAbs(); SetFlagsNz(Reg.A); });
            AddOC(4, PC.PC, OC.EOR_abs_X, () => { Reg.A ^= ReadAbsX(); SetFlagsNz(Reg.A); });
            AddOC(4, PC.PC, OC.EOR_abs_Y, () => { Reg.A ^= ReadAbsY(); SetFlagsNz(Reg.A); });
            AddOC(6, PC.NO, OC.EOR_X_ind, () => { Reg.A ^= ReadXIndirect(); SetFlagsNz(Reg.A); });
            AddOC(5, PC.PC, OC.EOR_ind_Y, () => { Reg.A ^= ReadIndirectY(); SetFlagsNz(Reg.A); });

            // INC  Increment Memory by One
            AddOC(5, PC.NO, OC.INC_zpg, () => { INC(ReadByte()); });
            AddOC(6, PC.NO, OC.INC_zpg_X, () => { INC((byte)(ReadByte() + Reg.X)); });
            AddOC(6, PC.NO, OC.INC_abs, () => { INC(ReadWord()); });
            AddOC(7, PC.NO, OC.INC_abs_X, () => { INC((Word)(ReadWord() + Reg.X)); });

            // IN*  Increment Index * by One
            AddOC(2, PC.NO, OC.INX_impl, () => { Reg.X++; SetFlagsNz(Reg.X); });
            AddOC(2, PC.NO, OC.INY_impl, () => { Reg.Y++; SetFlagsNz(Reg.Y); });

            // JMP  Jump to New Location
            AddOC(3, PC.NO, OC.JMP_abs, () => { Reg.ProgramCounter = ReadWord(); });
            AddOC(5, PC.NO, OC.JMP_ind, () => { Reg.ProgramCounter = ReadIndirect(); });

            // JSR Jump to New Location Saving Return Address
            AddOC(6, PC.NO, OC.JSR_abs, () => { PushStack((UInt16)(Reg.ProgramCounter + 1)); Reg.ProgramCounter = ReadAbsAddr(); });

            // LDA  Load Accumulator with Memory
            AddOC(2, PC.NO, OC.LDA_immediate, () => { Reg.A = ReadImmediate(); SetFlagsNz(Reg.A); });
            AddOC(3, PC.NO, OC.LDA_zpg, () => { Reg.A = ReadZpg(); SetFlagsNz(Reg.A); });
            AddOC(4, PC.NO, OC.LDA_zpg_X, () => { Reg.A = ReadZpgX(); SetFlagsNz(Reg.A); });
            AddOC(4, PC.NO, OC.LDA_abs, () => { Reg.A = ReadAbs(); SetFlagsNz(Reg.A); });
            AddOC(4, PC.PC, OC.LDA_abs_X, () => { Reg.A = ReadAbsX(); SetFlagsNz(Reg.A); });
            AddOC(4, PC.PC, OC.LDA_abs_Y, () => { Reg.A = ReadAbsY(); SetFlagsNz(Reg.A); });
            AddOC(6, PC.NO, OC.LDA_X_ind, () => { Reg.A = ReadXIndirect(); SetFlagsNz(Reg.A); });
            AddOC(5, PC.PC, OC.LDA_ind_Y, () => { Reg.A = ReadIndirectY(); SetFlagsNz(Reg.A); });

            // LDX  Load Index X with Memory
            AddOC(2, PC.NO, OC.LDX_immediate, () => { Reg.X = ReadImmediate(); SetFlagsNz(Reg.X); });
            AddOC(3, PC.NO, OC.LDX_zpg, () => { Reg.X = ReadZpg(); SetFlagsNz(Reg.X); });
            AddOC(4, PC.NO, OC.LDX_zpg_Y, () => { Reg.X = ReadZpgX(); SetFlagsNz(Reg.X); });
            AddOC(4, PC.NO, OC.LDX_abs, () => { Reg.X = ReadZpg(); SetFlagsNz(Reg.X); });
            AddOC(4, PC.PC, OC.LDX_abs_Y, () => { Reg.X = ReadAbsY(); SetFlagsNz(Reg.X); });

            // LDY  Load Index Y with Memory
            AddOC(2, PC.NO, OC.LDY_immediate, () => { Reg.Y = ReadImmediate(); SetFlagsNz(Reg.Y); });
            AddOC(3, PC.NO, OC.LDY_zpg, () => { Reg.Y = ReadZpg(); SetFlagsNz(Reg.Y); });
            AddOC(4, PC.NO, OC.LDY_zpg_X, () => { Reg.Y = ReadZpgX(); SetFlagsNz(Reg.Y); });
            AddOC(4, PC.NO, OC.LDY_abs, () => { Reg.Y = ReadZpg(); SetFlagsNz(Reg.Y); });
            AddOC(4, PC.PC, OC.LDY_abs_X, () => { Reg.Y = ReadAbsX(); SetFlagsNz(Reg.Y); });

            // LSR  Shift One Bit Right (Memory or Accumulator)
            AddOC(2, PC.NO, OC.LSR_A, () => { Reg.A = LSR(Reg.A); });
            AddOC(5, PC.NO, OC.LSR_zpg, () => { LSR_pos(ReadByte()); });
            AddOC(6, PC.NO, OC.LSR_zpg_X, () => { LSR_pos((byte)(ReadByte() + Reg.X)); });
            AddOC(6, PC.NO, OC.LSR_abs, () => { LSR_pos(ReadWord()); });
            AddOC(7, PC.NO, OC.LSR_abs_X, () => { LSR_pos((Word)(ReadWord() + Reg.X)); });

            // NOP  No Operation
            AddOC(2, PC.NO, OC.NOP_impl, () => { });

            // AND  AND Memory with Accumulator
            AddOC(2, PC.NO, OC.ORA_immediate, () => { Reg.A |= ReadImmediate(); SetFlagsNz(Reg.A); });
            AddOC(3, PC.NO, OC.ORA_zpg, () => { Reg.A |= ReadZpg(); SetFlagsNz(Reg.A); });
            AddOC(4, PC.NO, OC.ORA_zpg_X, () => { Reg.A |= ReadZpgX(); SetFlagsNz(Reg.A); });
            AddOC(4, PC.NO, OC.ORA_abs, () => { Reg.A |= ReadAbs(); SetFlagsNz(Reg.A); });
            AddOC(4, PC.PC, OC.ORA_abs_X, () => { Reg.A |= ReadAbsX(); SetFlagsNz(Reg.A); });
            AddOC(4, PC.PC, OC.ORA_abs_Y, () => { Reg.A |= ReadAbsY(); SetFlagsNz(Reg.A); });
            AddOC(6, PC.NO, OC.ORA_X_ind, () => { Reg.A |= ReadXIndirect(); SetFlagsNz(Reg.A); });
            AddOC(5, PC.PC, OC.ORA_ind_Y, () => { Reg.A |= ReadIndirectY(); SetFlagsNz(Reg.A); });

            // PHA Push Accumulator on Stack
            AddOC(3, PC.NO, OC.PHA_impl, () => { PushStack(Reg.A); });

            // PHP  Push Processor Status on Stack
            AddOC(3, PC.NO, OC.PHP_impl, () => { PushStack(Reg.ProcessorStatusFlags); });

            // PLA  Pull Accumulator from Stack
            AddOC(4, PC.NO, OC.PLA_impl, () => { Reg.A = PopStackByte(); });

            // PLP  Pull Processor Status from Stack
            AddOC(4, PC.NO, OC.PLP_impl, () => { Reg.ProcessorStatusFlags = PopStackByte(); });

            // ROL  Rotate One Bit Left (Memory or Accumulator)
            AddOC(2, PC.NO, OC.ROL_A, () => { Reg.A = ROL(Reg.A); });
            AddOC(5, PC.NO, OC.ROL_zpg, () => { ROL_pos(ReadByte()); });
            AddOC(6, PC.NO, OC.ROL_zpg_X, () => { ROL_pos((byte)(ReadByte() + Reg.X)); });
            AddOC(6, PC.NO, OC.ROL_abs, () => { ROL_pos(ReadWord()); });
            AddOC(7, PC.NO, OC.ROL_abs_X, () => { ROL_pos((Word)(ReadWord() + Reg.X)); });

            // ROR  Rotate One Bit Right (Memory or Accumulator)
            AddOC(2, PC.NO, OC.ROR_A, () => { Reg.A = ROR(Reg.A); });
            AddOC(5, PC.NO, OC.ROR_zpg, () => { ROR_pos(ReadByte()); });
            AddOC(6, PC.NO, OC.ROR_zpg_X, () => { ROR_pos((byte)(ReadByte() + Reg.X)); });
            AddOC(6, PC.NO, OC.ROR_abs, () => { ROR_pos(ReadWord()); });
            AddOC(7, PC.NO, OC.ROR_abs_X, () => { ROR_pos((Word)(ReadWord() + Reg.X)); });

            // RTI  Return from Interrupt
            AddOC(6, PC.NO, OC.RTI_impl, () => { Reg.ProcessorStatusFlags = PopStackByte(); Reg.ProgramCounter = PopStackWord(); });

            // RTS  Return from Subroutine
            AddOC(6, PC.NO, OC.RTS_impl, () => { Reg.ProgramCounter = PopStackWord() + 1; });

            // SBC  Subtract Memory from Accumulator with Borrow
            AddOC(2, PC.NO, OC.SBC_immediate, () => { SBC(ReadImmediate()); });
            AddOC(3, PC.NO, OC.SBC_zpg, () => { SBC(ReadZpg()); });
            AddOC(4, PC.NO, OC.SBC_zpg_X, () => { SBC(ReadZpgX()); });
            AddOC(4, PC.NO, OC.SBC_abs, () => { SBC(ReadAbs()); });
            AddOC(4, PC.PC, OC.SBC_abs_X, () => { SBC(ReadAbsX()); });
            AddOC(4, PC.PC, OC.SBC_abs_Y, () => { SBC(ReadAbsY()); });
            AddOC(6, PC.NO, OC.SBC_X_ind, () => { SBC(ReadXIndirect()); });
            AddOC(5, PC.PC, OC.SBC_ind_Y, () => { SBC(ReadIndirectY()); });


            // CL* Set * Flag
            AddOC(2, PC.NO, OC.SEC_impl, () => { Reg.Flag.C = true; });
            AddOC(2, PC.NO, OC.SED_impl, () => { Reg.Flag.D = true; });
            AddOC(2, PC.NO, OC.SEI_impl, () => { Reg.Flag.I = true; });

            // STA  Store Accumulator in Memory
            // TODO: Add ReadZpgAddr and LOR through that?
            AddOC(3, PC.NO, OC.STA_zpg, () => { var pos = ReadByte(); Write(pos, Reg.A); LOR(MemoryAccessType.Zpg, pos, _memory[pos]); });
            AddOC(4, PC.NO, OC.STA_zpg_X, () => { var pos = ReadByte(); Write((byte)(pos + Reg.X), Reg.A); LOR(MemoryAccessType.ZpgX, pos, _memory[(byte)(pos + Reg.X)]); });
            AddOC(4, PC.NO, OC.STA_abs, () => { var pos = ReadWord(); Write(pos, Reg.A); LOR(MemoryAccessType.Abs, pos, _memory[pos]); });
            AddOC(5, PC.NO, OC.STA_abs_X, () => { var pos = ReadWord(); Write((Word)(pos + Reg.X), Reg.A); LOR(MemoryAccessType.AbsX, pos, _memory[(byte)(pos + Reg.X)]); });
            AddOC(5, PC.NO, OC.STA_abs_Y, () => { var pos = ReadWord(); Write((Word)(pos + Reg.Y), Reg.A); LOR(MemoryAccessType.AbsY, pos, _memory[(byte)(pos + Reg.Y)]); });
            AddOC(6, PC.NO, OC.STA_X_ind, () => { Write(ReadXIndirectAddr(), Reg.A); });
            AddOC(6, PC.NO, OC.STA_ind_Y, () => { Write(ReadIndirectYAddr(), Reg.A); });

            // STX  Store Index X in Memory
            AddOC(3, PC.NO, OC.STX_zpg, () => { var zpg = ReadByte(); Write(zpg, Reg.X); LOR(MemoryAccessType.Zpg, zpg, _memory[zpg]); });
            AddOC(4, PC.NO, OC.STX_zpg_Y, () => { var zpg = ReadByte(); Write((byte)(zpg + Reg.Y), Reg.X); LOR(MemoryAccessType.ZpgY, zpg, _memory[(byte)(zpg + Reg.X)]); });
            AddOC(4, PC.NO, OC.STX_abs, () => { var abs = ReadWord(); Write(abs, Reg.X); LOR(MemoryAccessType.Abs, abs, _memory[abs]); });

            // STY  Sore Index Y in Memory
            AddOC(3, PC.NO, OC.STY_zpg, () => { var zpg = ReadByte(); Write(zpg, Reg.Y); LOR(MemoryAccessType.Zpg, zpg, _memory[zpg]); });
            AddOC(4, PC.NO, OC.STY_zpg_X, () => { var zpg = ReadByte(); Write((byte)(zpg + Reg.X), Reg.Y); LOR(MemoryAccessType.ZpgX, zpg, _memory[(byte)(zpg + Reg.X)]); });
            AddOC(4, PC.NO, OC.STY_abs, () => { var abs = ReadWord(); Write(abs, Reg.Y); LOR(MemoryAccessType.Abs, abs, _memory[abs]); });

            // TAX  Transfer Accumulator to Index X
            AddOC(2, PC.NO, OC.TAX_impl, () => { Reg.X = Reg.A; SetFlagsNz(Reg.X); });

            // TAY  Transfer Accumulator to Index Y
            AddOC(2, PC.NO, OC.TAY_impl, () => { Reg.Y = Reg.A; SetFlagsNz(Reg.Y); });

            // TSX  Transfer Stack Pointer to Index X
            AddOC(2, PC.NO, OC.TSX_impl, () => { Reg.X = Reg.SP; SetFlagsNz(Reg.X); });

            // TXA  Transfer Index X to Accumulator
            AddOC(2, PC.NO, OC.TXA_impl, () => { Reg.A = Reg.X; SetFlagsNz(Reg.A); });

            // TXS  Transfer Index X to Stack Register
            AddOC(2, PC.NO, OC.TXS_impl, () => { Reg.SP = Reg.X; SetFlagsNz(Reg.SP); });

            // TYA  Transfer Index Y to Accumulator
            AddOC(2, PC.NO, OC.TYA_impl, () => { Reg.A = Reg.Y; SetFlagsNz(Reg.A); });

        }

        #region Advanced opcode implementations
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddOC(int cycles, PlusCycles plusCycles, OpCode opcode, Action action)
        {
            _opcodes[(int)opcode] = new CpuOpcode(cycles, action);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte SetFlagsNz(byte val)
        {
            Reg.Flag.N = val.IsBitSet(7);
            Reg.Flag.Z = val == 0;
            return val;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ADC(byte val)
        {
            // Overflow flag: http://www.righto.com/2012/12/the-6502-overflow-flag-explained.html

            int overflowSum;

            // Not decimal is easy
            if (!Reg.Flag.D)
            {
                // Add all, including carry flag
                overflowSum = Reg.A + val + (Reg.Flag.C ? 1 : 0);

                // Set Carry flag if result overflowed
                Reg.Flag.C = overflowSum > 255;
                // Could be done by checking if AND of high byte yields zero
                //Reg.Flag.C = (result & 0xFF00) != 0;

                // Take only last byte for return
                Reg.A = (byte)overflowSum;
            }
            else
            {
                // Decimal calculations are a bit more tricky... 
                // US Patent 3991307 A: Integrated circuit microprocessor with parallel binary adder having on-the-fly correction to provide decimal results 

                // Add 4 least significant bits and carry
                var result = (Reg.A & 0x0F) + (val & 0x0F) + (Reg.Flag.C ? 1 : 0);

                // If result is above 9 add 6 (effectively moving it beyond F) 
                if (result > 0x09) 
                    result += 0x06; 

                // Add 4 most significant bits
                result += (Reg.A & 0xF0) + (val & 0xF0);

                // Keep for overflow check
                overflowSum = result;

                // Same, most significant
                if ((result & 0x1f0) > 0x90)
                    result += 0x60;

                // Carry if resulting value is above 127
                Reg.Flag.C = (result & 0xff0) > 0xf0; ;

                // Take only last byte for return
                Reg.A = (byte)result;
            }

            // Some weird shit going on here. I think this is correct. TODO: Should probably test.
            Reg.Flag.V = ((Reg.A ^ val) & 0x80) == 0 && ((Reg.A ^ overflowSum) & 0x80) != 0; 
            SetFlagsNz(Reg.A);

        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SBC(byte val)
        {
            int overflowCheck;
            if (!Reg.Flag.D)
            {
                // Not decimal is easy
                overflowCheck = Reg.A - val - (Reg.Flag.C ? 0 : 1);
                Reg.Flag.C = (overflowCheck & 0xff00) == 0;
                Reg.A = (byte)overflowCheck;
            }
            else
            {
                // Decimal calculations are a bit more tricky... 
                // US Patent 3991307 A: Integrated circuit microprocessor with parallel binary adder having on-the-fly correction to provide decimal results 

                // Subtract 4 least significant bits and carry
                var result = (Reg.A & 0x0f) - (val & 0x0f) - (Reg.Flag.C ? 0 : 1);

                // Reverse of ADC
                // If bit 5 is set (we overflowed)
                if ((result & 0x10) != 0)
                    result = ((result - 6) & 0x0f) | ((Reg.A & 0xf0) - (val & 0xf0) - 0x10);
                else
                    result = result | ((Reg.A & 0xf0) - (val & 0xf0));

                overflowCheck = result;

                if ((result & 0xff00) != 0)
                    result -= 0x60;

                Reg.Flag.C = (result & 0xff00) == 0;
            }

            // Some weird shit going on here (just doing what the manual says)
            Reg.Flag.V = ((Reg.A ^ val) & 0x80) != 0 && ((Reg.A ^ overflowCheck) & 0x80) != 0;
            SetFlagsNz(Reg.A);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte ASL(byte val)
        {
            //if (pos >= _memory.Length)
            //    throw new Exception("Address " + pos + " outside of memory " + _memory.Length);
            Reg.Flag.C = val.IsBitSet(7);
            val <<= 1;
            Reg.Flag.Z = val == 0;
            return val;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ASL_pos(int pos)
        {
            _memory[pos] = ASL(_memory[pos]);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DEC(int pos)
        {
            _memory[pos]--;
            SetFlagsNz(_memory[pos]);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void INC(int pos)
        {
            _memory[pos]++;
            SetFlagsNz(_memory[pos]);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte LSR(byte val)
        {
            //if (pos >= _memory.Length)
            //    throw new Exception("Address " + pos + " outside of memory " + _memory.Length);
            Reg.Flag.C = val.IsBitSet(0);
            val >>= 1;
            Reg.Flag.Z = val == 0;
            return val;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LSR_pos(int pos)
        {
            _memory[pos] = LSR(_memory[pos]);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void BIT(byte val)
        {
            Reg.Flag.N = val.IsBitSet(7);
            Reg.Flag.V = val.IsBitSet(6);
            var result = val & Reg.A;
            Reg.Flag.Z = result == 0;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CMP(byte a, byte b)
        {
            var r = (int)a - (int)b;
            // TODO: Verify
            Reg.Flag.C = r > 0;
            Reg.Flag.Z = r == 0;
            if (r == 0)
                Reg.Flag.C = true;
            Reg.Flag.C = r < 0;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void BRK()
        {
            // TODO: Verify
            Reg.Flag.I = true;
            Reg.Flag.B = true;
            Reg.ProgramCounter += 2;
            PushStack((UInt16)Reg.ProgramCounter);
            Reg.ProgramCounter = Memory.Memory.POS_INTERRUPT;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte ROL(byte value)
        {
            value = (byte)((value << 1) | (value >> 7));
            Reg.Flag.C = value.IsBitSet(7);
            SetFlagsNz(value);
            return value;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ROL_pos(Word pos)
        {
            Write(pos, ROL(Read(pos)));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte ROR(byte value)
        {
            value = (byte)((value >> 1) | (value << 7));
            Reg.Flag.C = value.IsBitSet(7);
            SetFlagsNz(value);
            return value;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ROR_pos(Word pos)
        {
            Write(pos, ROR(Read(pos)));
        }
        #endregion

    }
}