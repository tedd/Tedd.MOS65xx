using Tedd.MOS65xx.Emulator.Utils;

namespace Tedd.MOS65xx.Emulator.Memory
{
    public class MemoryBankSwitches
    {
        private readonly Memory _memory;

        public MemoryBankSwitches(Memory memory)
        {
            _memory = memory;
        }
        public bool EXROM
        {
            get { return BitUtils.IsBitSet(_memory.Raw[Memory.POS_PP_BANKSWITCHES], (int)MemoryBankSwitchEnum.EXROM); }
            set { BitUtils.SetBit(ref _memory.Raw[Memory.POS_PP_BANKSWITCHES], (int)MemoryBankSwitchEnum.EXROM, value); }
        }
        public bool GAME
        {
            get { return BitUtils.IsBitSet(_memory.Raw[Memory.POS_PP_BANKSWITCHES], (int)MemoryBankSwitchEnum.GAME); }
            set { BitUtils.SetBit(ref _memory.Raw[Memory.POS_PP_BANKSWITCHES], (int)MemoryBankSwitchEnum.GAME, value); }
        }
        public bool CHAREN
        {
            get { return BitUtils.IsBitSet(_memory.Raw[Memory.POS_PP_BANKSWITCHES], (int)MemoryBankSwitchEnum.CHAREN); }
            set { BitUtils.SetBit(ref _memory.Raw[Memory.POS_PP_BANKSWITCHES], (int)MemoryBankSwitchEnum.CHAREN, value); }
        }
        public bool HIRAM
        {
            get { return BitUtils.IsBitSet(_memory.Raw[Memory.POS_PP_BANKSWITCHES], (int)MemoryBankSwitchEnum.HIRAM); }
            set { BitUtils.SetBit(ref _memory.Raw[Memory.POS_PP_BANKSWITCHES], (int)MemoryBankSwitchEnum.HIRAM, value); }
        }
        public bool LORAM
        {
            get { return BitUtils.IsBitSet(_memory.Raw[Memory.POS_PP_BANKSWITCHES], (int)MemoryBankSwitchEnum.LORAM); }
            set { BitUtils.SetBit(ref _memory.Raw[Memory.POS_PP_BANKSWITCHES], (int)MemoryBankSwitchEnum.LORAM, value); }
        }
    }
}