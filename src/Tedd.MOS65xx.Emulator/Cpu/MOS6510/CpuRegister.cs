namespace Tedd.MOS65xx.Emulator.Cpu.MOS6510
{
    public class CpuRegister
    {
        public class CpuFlags
        {
            private readonly CpuRegister _reg;

            public CpuFlags(CpuRegister cpuRegister)
            {
                _reg = cpuRegister;
            }

            public bool C
            {
                get { return _reg.ProcessorStatusFlags.IsBitSet((int)Enums.ProcessorStatusFlags.C); }
                set { _reg.ProcessorStatusFlags.SetBit((int)Enums.ProcessorStatusFlags.C, value); }
            }
            public bool Z
            {
                get { return _reg.ProcessorStatusFlags.IsBitSet((int)Enums.ProcessorStatusFlags.Z); }
                set { _reg.ProcessorStatusFlags.SetBit((int)Enums.ProcessorStatusFlags.Z, value); }
            }
            public bool I
            {
                get { return _reg.ProcessorStatusFlags.IsBitSet((int)Enums.ProcessorStatusFlags.I); }
                set { _reg.ProcessorStatusFlags.SetBit((int)Enums.ProcessorStatusFlags.I, value); }
            }
            public bool D
            {
                get { return _reg.ProcessorStatusFlags.IsBitSet((int)Enums.ProcessorStatusFlags.D); }
                set { _reg.ProcessorStatusFlags.SetBit((int)Enums.ProcessorStatusFlags.D, value); }
            }
            public bool B
            {
                get { return _reg.ProcessorStatusFlags.IsBitSet((int)Enums.ProcessorStatusFlags.B); }
                set { _reg.ProcessorStatusFlags.SetBit((int)Enums.ProcessorStatusFlags.B, value); }
            }
            public bool Unused
            {
                get { return _reg.ProcessorStatusFlags.IsBitSet((int)Enums.ProcessorStatusFlags.Unused); }
                set { _reg.ProcessorStatusFlags.SetBit((int)Enums.ProcessorStatusFlags.Unused, value); }
            }
            public bool V
            {
                get { return _reg.ProcessorStatusFlags.IsBitSet((int)Enums.ProcessorStatusFlags.V); }
                set { _reg.ProcessorStatusFlags.SetBit((int)Enums.ProcessorStatusFlags.V, value); }
            }
            public bool N
            {
                get { return _reg.ProcessorStatusFlags.IsBitSet((int)Enums.ProcessorStatusFlags.N); }
                set { _reg.ProcessorStatusFlags.SetBit((int)Enums.ProcessorStatusFlags.N, value); }
            }
        }

        public readonly CpuFlags Flag ;

        // CPU Registers
        public int ProgramCounter = (int)Enums.Memory.System.START;
        public byte ProcessorStatusFlags = 0;
        public byte A = 0; // Accumulator
        public byte X = 0; // X Register
        public byte Y = 0; // Y Register
        public byte SP = 255; // StackPointer

        public CpuRegister()
        {
            Flag = new CpuFlags(this);
        }

        //public bool GetFlag(Enums.ProcessorStatusFlags flag)
        //{
        //    return Utils.BitUtils.IsBitSet(ProcessorStatusFlags, (int)flag);
        //}
        //public void SetFlag(Enums.ProcessorStatusFlags flag, bool value)
        //{
        //    Utils.BitUtils.SetBit(ref ProcessorStatusFlags, (int)flag, value);
        //}

    }
}