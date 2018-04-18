namespace Tedd.MOS65xx.Emulator.Memory
{
    public class MemoryArea
    {
        private readonly Memory _memory;
        private readonly int _start;
        private readonly int _end;

        public MemoryArea(Memory memory, int start, int end)
        {
            this._memory = memory;
            this._start = start;
            this._end = end;
        }
        public byte this[int index]
        {
            get { return _memory[_start + index]; }
            set { _memory[_start + index] = value; }
        }
    }
}