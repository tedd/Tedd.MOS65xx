using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Tedd.MOS65xx.Emulator.Utils
{
    public class BitUtils
    {
        ///// <summary>
        ///// Returns whether the bit at the specified position is set.
        ///// </summary>
        ///// <typeparam name="T">Any integer type.</typeparam>
        ///// <param name="t">The value to check.</param>
        ///// <param name="pos">
        ///// The position of the bit to check, 0 refers to the least significant bit.
        ///// </param>
        ///// <returns>true if the specified bit is on, otherwise false.</returns>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public static bool IsBitSet<T>(T t, int pos) where T : struct, IConvertible
        //{
        //    var value = t.ToInt64(CultureInfo.CurrentCulture);
        //    return (value & (1 << pos)) != 0;
        //}
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBitSet(int value, int pos)
        {
            return (value & (1 << pos)) != 0;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBitSet(UInt16 value, int pos)
        {
            return (value & (1 << pos)) != 0;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBitSet(byte value, int pos)
        {
            return (value & (1 << pos)) != 0;
        }
      
        #region SetBit
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetBit(ref Byte value, int pos, bool state)
        {
            var mask = (int)(1 << pos);
            if (state)
                value = (Byte)((int)value | mask);
            else
                value = (Byte)((int)value & ~mask);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetBit(ref  UInt16 value, int pos, bool state)
        {
            var mask = (int)(1 << pos);
            if (state)
                value = (UInt16)((int)value | mask);
            else
                value = (UInt16)((int)value & ~mask);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetBit(ref UInt32 value, int pos, bool state)
        {
            var mask = (UInt32)(1 << pos);
            if (state)
                value |= mask;
            else
                value &= ~mask;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetBit(ref Int32 value, int pos, bool state)
        {
            var mask = (Int32)(1 << pos);
            if (state)
                value |= mask;
            else
                value &= ~mask;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetBit(ref UInt64 value, int pos, bool state)
        {
            var mask = (UInt64)(1 << pos);
            if (state)
                value |= mask;
            else
                value &= ~mask;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetBit(ref Int64 value, int pos, bool state)
        {
            var mask = (Int64)(1 << pos);
            if (state)
                value |= mask;
            else
                value &= ~mask;
        }
        #endregion

        #region EndianFix
        public static void EndianFix(ref byte[] data)
        {
            if (BitConverter.IsLittleEndian)
                Array.Reverse(data);
        }
        #endregion

        #region Pack
        public static void Pack(ref uint packed, int offset, int length, uint value)
        {
            var lm = 32 - length;
            value = (value << lm) >> lm;
            var u = lm - offset;
            if (u < 0)
                throw new Exception("Bit out of bounds: Offset " + offset + " + length " + length + " > 32");
            //packed &= ~((uint.MaxValue << u + offset) >> u);
            //packed |= value << offset;
            var clearMask = ~((uint.MaxValue >> offset << lm) >> lm - offset);

            packed &= clearMask;
            packed |= value << offset;
        }
        
        public static void Pack(ref UInt16 packed, int offset, int length, uint value)
        {
            var lm = 16 - length;
            value = (value << lm) >> lm;
            var u = lm - offset;
            if (u < 0)
                throw new Exception("Bit out of bounds: Offset " + offset + " + length " + length + " > 16");
            //packed &= (UInt16)(~((UInt16.MaxValue << u + offset) >> u));
            var clearMask = (UInt16)(~((UInt16.MaxValue >> offset << lm) >> lm - offset));

            packed &= clearMask;
            packed |= (UInt16)(value << offset);
        }
        #endregion

        #region Unpack
        public static uint Unpack(uint packed, int offset, int length)
        {
            var v = packed >> offset;
            v = v & (((uint)1 << length) - 1);
            return v;
        }
        #endregion
    }
}