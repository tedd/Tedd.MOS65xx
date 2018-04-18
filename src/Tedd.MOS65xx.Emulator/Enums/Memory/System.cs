namespace Tedd.MOS65xx.Emulator.Enums.Memory
{
    public enum System
    {
        //HARDRESET = 0xFFFC, // Cold start machine
        HARDRESET = 0xFFF8, // Cold start machine
        SOFTRESET = 64738, // Warm start machine
        START = 0xFCE2, // Start address
        STACK = 0x0100, // Stack is stored from $0100 to $01FF
    }
}
