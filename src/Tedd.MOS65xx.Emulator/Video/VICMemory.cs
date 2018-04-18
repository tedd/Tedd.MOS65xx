namespace Tedd.MOS65xx.Emulator.Video
{
    public class VICMemory
    {
        private readonly Memory.Memory _mainMemory;
        public VICMemory(Memory.Memory mainMemory)
        {
            _mainMemory = mainMemory;
        }
    }
}