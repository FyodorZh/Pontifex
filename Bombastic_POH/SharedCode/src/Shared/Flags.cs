using System;

[Serializable]
public class CFlags
{
    // установка бита в выбраной позиции (zero-based)
    static public UInt32 GetBitMask(Byte bit)
    {
        return 1U << bit;
    }

    // Установка конкретного флага
    static public UInt32 SetBit(UInt32 flag, Byte bit)
    {
        return SetFlags(flag, GetBitMask(bit));
    }

    // Установка флагов по маске
    static public UInt32 SetFlags(UInt32 flag, UInt32 mask)
    {
        return ((flag) | (mask));
    }

    // Очистка конкретного флага
    static public UInt32 ClearBit(UInt32 flag, Byte bit)
    {
        return ClearFlags(flag, GetBitMask(bit));
    }

    // Очистка флагов по маске
    static public UInt32 ClearFlags(UInt32 flag, UInt32 mask)
    {
        return ((flag) & (~mask));
    }

    // Инвертирование флагов по маске
    static public UInt32 InvertBit(UInt32 flag, Byte bit)
    {
        return InvertFlags(flag, GetBitMask(bit));
    }

    // Инвертирование флагов по маске
    static public UInt32 InvertFlags(UInt32 flag, UInt32 mask)
    {
        return ((flag) ^ (mask));
    }

    // Установлен ли конкретный флаг
    static public Boolean IsBit(UInt32 flags, byte bit)
    {
        return HasFlags(flags, GetBitMask(bit));
    }

    // Есть ли совпадающие с маской биты 
    static public Boolean HasFlags(UInt32 flag, UInt32 mask)
    {
        return ((flag) & (mask)) != 0;
    }

    static public bool ChangeBit(ref UInt32 flag, Byte bit, bool set)
    {
        return ChangeFlags(ref flag, GetBitMask(bit), set);
    }

    static public bool ChangeFlags(ref UInt32 flag, uint mask, bool set)
    {
        bool res = HasFlags(flag, mask) != set;
        if (set)
        {
            flag = SetFlags(flag, mask);
        }
        else
        {
            flag = ClearFlags(flag, mask);
        }

        return res;
    }

    static public void GetWords(UInt32 data, ref UInt16 hiWord, ref UInt16 loWord)
    {
        hiWord = (UInt16)(data >> 16);
        loWord = (UInt16)(data & 0x0000FFFF);
    }

    static public UInt16 GetHiWord(UInt32 data)
    {
        return (UInt16)(data >> 16);
    }

    static public UInt16 GetLoWord(UInt32 data)
    {
        return (UInt16)(data & 0x0000FFFF);
    }

    static public void GetBytes(UInt16 data, ref Byte hiByte, ref Byte loByte)
    {
        hiByte = (Byte)(data >> 8);
        loByte = (Byte)(data & 0x00FF);
    }

    static public Byte GetHiByte(UInt16 data)
    {
        return (Byte)(data >> 8);
    }

    static public Byte GetLoByte(UInt16 data)
    {
        return (Byte)(data & 0x00FF);
    }

    private UInt32 mFlags;

    public CFlags() { }
    public CFlags(CFlags from)
    {
        mFlags = from.Flags;
    }
    public CFlags(UInt32 flags)
    {
        mFlags = flags;
    }
    // Значение флагов
    public UInt32 Flags
    {
        get { return mFlags; }
    }

    // Установка новых флагов по маске, другие не меняются
    public void SetFlags(UInt32 mask)
    {
        mFlags = SetFlags(mFlags, mask);
    }

    // Установка флага в указаной позиции (zero-based)
    public void SetBit(Byte bit)
    {
        mFlags = SetBit(mFlags, bit);
    }

    // Попытка изменить значение флага
    public bool ChangeBit(Byte bit, bool set)
    {
        return ChangeBit(ref mFlags, bit, set);
    }

    // Очистка флагов по маске
    public void Clear(UInt32 mask)
    {
        mFlags = ClearFlags(mFlags, mask);
    }

    // Очистка флага в указаной позиции (zero-based)
    public void ClearBit(Byte bit)
    {
        mFlags = ClearBit(mFlags, bit);
    }

    // Установка всех флагов
    public void Init(UInt32 mask)
    {
        mFlags = mask;
    }

    // Сброс всех флагов
    public void Clear()
    {
        mFlags = 0;
    }

    // Инвертирование значений флагов по маске
    public void Invert(UInt32 mask)
    {
        mFlags = InvertFlags(mFlags, mask);
    }

    // Инвертирование значения флага в указаной позиции (zero-based)
    public void InvertBit(Byte bit)
    {
        mFlags = InvertBit(mFlags, bit);
    }

    // Есть ли установленые флаги, сообветствующие маске
    public Boolean Is(UInt32 mask)
    {
        return HasFlags(mFlags, mask);
    }

    // Установлен ли флаг в указаной позиции (zero-based)
    public Boolean IsBit(Byte bit)
    {
        return IsBit(mFlags, bit);
    }
}
