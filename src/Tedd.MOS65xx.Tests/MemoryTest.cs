using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tedd.MOS65xx.Emulator;
using Tedd.MOS65xx.Emulator.Memory;

namespace Tedd.MOS65xx.Tests
{
    [TestClass]
    public class MemoryTest
    {
        [TestMethod]
        public void Stack()
        {
            //var memory = new Memory();
            //memory.MemoryMode= Memory.MemoryModeEnum.DRAM;
            
            //for (int i = 0; i < 256; i++)
            //{
            //    memory.Stack[i] = (byte)i;
            //}

            //// Verify
            //for (int i = 0; i < memory.Length; i++)
            //{
            //    if (i < Memory.POS_STACK_START || i > Memory.POS_STACK_END)
            //    {
            //        Assert.IsTrue(memory[i] == 0, "Memory is 0");
            //        continue;
            //    }
            //    Assert.IsTrue(memory[i] == i - Memory.POS_STACK_START, "Raw memory counts up from start pos");
            //    Assert.IsTrue(memory[i] == memory.Stack[i - Memory.POS_STACK_START], "Raw memory equals stack memory, given offset");
            //}
        }
    }
}
