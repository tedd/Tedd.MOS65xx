
namespace Tedd.MOS65xx.Emulator.Memory;

public class MemoryBankSwitches
{
    private readonly Memory _memory;

    public MemoryBankSwitches(Memory memory)
    {
        _memory = memory;
    }
    public bool EXROM
    {
        get { return _memory.Raw[Memory.POS_PP_BANKSWITCHES].IsBitSet((int)MemoryBankSwitchEnum.EXROM); }
        set { _memory.Raw[Memory.POS_PP_BANKSWITCHES].SetBit((int)MemoryBankSwitchEnum.EXROM, value); }
    }
    public bool GAME
    {
        get { return _memory.Raw[Memory.POS_PP_BANKSWITCHES].IsBitSet((int)MemoryBankSwitchEnum.GAME); }
        set { _memory.Raw[Memory.POS_PP_BANKSWITCHES].SetBit((int)MemoryBankSwitchEnum.GAME, value); }
    }
    public bool CHAREN
    {
        get { return _memory.Raw[Memory.POS_PP_BANKSWITCHES].IsBitSet((int)MemoryBankSwitchEnum.CHAREN); }
        set { _memory.Raw[Memory.POS_PP_BANKSWITCHES].SetBit((int)MemoryBankSwitchEnum.CHAREN, value); }
    }
    public bool HIRAM
    {
        get { return _memory.Raw[Memory.POS_PP_BANKSWITCHES].IsBitSet((int)MemoryBankSwitchEnum.HIRAM); }
        set { _memory.Raw[Memory.POS_PP_BANKSWITCHES].SetBit((int)MemoryBankSwitchEnum.HIRAM, value); }
    }
    public bool LORAM
    {
        get { return _memory.Raw[Memory.POS_PP_BANKSWITCHES].IsBitSet((int)MemoryBankSwitchEnum.LORAM); }
        set { _memory.Raw[Memory.POS_PP_BANKSWITCHES].SetBit((int)MemoryBankSwitchEnum.LORAM, value); }
    }
}