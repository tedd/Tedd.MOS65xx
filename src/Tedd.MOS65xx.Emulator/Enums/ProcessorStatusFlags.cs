namespace Tedd.MOS65xx.Emulator.Enums
{
    public enum ProcessorStatusFlags
    {
        /// <summary>
        /// Carry Flag
        /// Indicates when a bit of the result is to be carried to, or borrowed from, another byte.
        /// </summary>
        C = 0,
        /// <summary>
        /// Zero Flag
        /// Indicates when the result is equal, or not, to zero.
        /// 0 (false) = Result not zero, 1 (true) = Result zero
        /// </summary>
        Z = 1,
        /// <summary>
        /// Interrupt Request Disable Flag
        /// Indicates when preventing, or allowing, non-maskable interrupts (NMI).
        /// 0 (false) = Enable, 1 (true) = Disable
        /// </summary>
        I = 2,
        /// <summary>
        /// Decimal Mode Flag
        /// Indicates when switching between decimal/binary modes.
        /// </summary>
        D = 3,
        /// <summary>
        /// Break Command Flag
        /// Indicates when stopping the execution of machine code instructions.
        /// 0 (false) = No break, 1 (true) = Break
        /// </summary>
        B = 4,
        /// <summary>
        /// Cannot be changed.
        /// </summary>
        Unused = 5,
        /// <summary>
        /// Overflow Flag
        /// Indicates when the result is greater, or less, than can be stored in one byte (including sign).
        /// 0 (false) = False, 1 (true) = True
        /// </summary>
        V = 6,
        /// <summary>
        /// Negative Flag
        /// Indicates when the result is negative, or positive, in signed operations.
        /// 0 (false) = Positive, 1 (true) = Negative
        /// </summary>
        N = 7

    }
}