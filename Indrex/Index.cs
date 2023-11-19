using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Indrex;

public class Cursor
{
    Page page;
    Int32 slot;
}

public class Index
{

}

[StructLayout(LayoutKind.Sequential)]
public struct PageHeader
{
    public UInt16 slotCount;
    public UInt16 freeBytes;
}

[StructLayout(LayoutKind.Sequential)]
public struct SlotEntry
{
    public UInt16 offset0;
    public UInt16 offset1;

    public override String ToString() => $"{offset0:x}-{offset1:x}";
}

public ref struct PageOperations
{
    public static Int32 headerSize = SpanExtensions.SizeOf<PageHeader>();
    public static Int32 slotEntrySize = SpanExtensions.SizeOf<SlotEntry>();

    ref PageHeader header;

    Span<Byte> page;

    Span<SlotEntry> slots;

    public Int32 SlotCount => header.slotCount;

    public Int32 FreeBytes => header.freeBytes;

    public PageOperations(Span<Byte> page)
    {
        this.page = page;

        Debug.Assert(page.Length <= UInt16.MaxValue + 1);

        header = ref page.InterpretAs<PageHeader>()[0];

        slots = page[headerSize..].InterpretAs<SlotEntry>();
    }

    public void Wipe()
    {
        page.Clear();

        header.freeBytes = (UInt16)(page.Length - headerSize);
    }

    public void GetSlot(Int32 slotNo, out Span<Byte> key, out Span<Byte> value)
    {
        ref var slot = ref slots[slotNo];
        
        var offset2 = GetKeyValueOffset(slotNo);

        key = page[slot.offset0..slot.offset1];
        value = page[slot.offset1..offset2];
    }

    public Int32 Compare(Int32 slotNo, Span<Byte> key)
    {
        ref var slot = ref slots[slotNo];

        var slotKeyRange = page[slot.offset0..slot.offset1];

        return slotKeyRange.SequenceCompareTo(key);
    }

    public Boolean TryInsert(Int32 slotNo, Span<Byte> key, Span<Byte> value)
    {
        var length = key.Length + value.Length;

        if (length + slotEntrySize > header.freeBytes) return false;

        InsertSlots(slotNo, 1, length, out var offset0, out var offset2);

        ref var slot = ref slots[slotNo];

        var offset1 = offset0 + key.Length;

        slot.offset0 = (UInt16)offset0;
        slot.offset1 = (UInt16)offset1;

        key.CopyTo(page[offset0..offset1]);
        value.CopyTo(page[offset1..offset2]);

        return true;
    }

    public void Remove(Int32 slotNo)
    {
        RemoveSlots(slotNo, slotNo + 1);
    }

    void InsertSlots(Int32 slotGapNo, Int32 slotCount, Int32 length, out Int32 offset0, out Int32 offset2)
    {
        offset2 = GetKeyValueOffset(slotGapNo);
        offset0 = offset2 - length;

        GetMainDivisions(out _, out var kvEndOffset);

        page[kvEndOffset..offset2].CopyTo(page[(kvEndOffset - length)..(offset2 - length)]);

        var newSlotCount = header.slotCount + slotCount;

        var slotsToMove = slots[slotGapNo..header.slotCount];

        foreach (ref var s in slotsToMove)
        {
            s.offset0 -= (UInt16)length;
            s.offset1 -= (UInt16)length;
        }

        slotsToMove.CopyTo(slots[(slotGapNo + slotCount)..newSlotCount]);

        header.slotCount = (UInt16)newSlotCount;
        header.freeBytes -= (UInt16)(length + slotEntrySize * slotCount);
    }

    void RemoveSlots(Int32 slotGapNo0, Int32 slotGapNo1)
    {
        var offset1 = GetKeyValueOffset(slotGapNo0);
        var offset0 = GetKeyValueOffset(slotGapNo1);

        var slotCount = slotGapNo1 - slotGapNo0;

        var length = offset1 - offset0;

        Debug.Assert(length >= 0);

        GetMainDivisions(out _, out var kvEndOffset);

        page[kvEndOffset..offset0].CopyTo(page[(kvEndOffset + length)..(offset0 + length)]);

        var newSlotCount = header.slotCount - slotCount;

        var slotsToMove = slots[slotGapNo1..header.slotCount];

        foreach (ref var s in slotsToMove)
        {
            s.offset0 += (UInt16)length;
            s.offset1 += (UInt16)length;
        }

        slotsToMove.CopyTo(slots[slotGapNo0..(slotGapNo0 + slotsToMove.Length)]);

        header.slotCount = (UInt16)newSlotCount;
        header.freeBytes += (UInt16)(length + slotEntrySize * slotCount);
    }

    public void PruneFree()
    {
        GetMainDivisions(out var slotsEnd, out var keyValueEnd);

        page[slotsEnd..keyValueEnd].Clear();
    }

    Int32 GetKeyValueOffset(Int32 slotGapNo)
    {
        if (--slotGapNo < 0)
        {
            return page.Length;
        }
        else if (slotGapNo == header.slotCount)
        {
            throw new ArgumentException("Invalid slotGapNo");
        }
        else
        {
            return slots[slotGapNo].offset0;
        }
    }

    void GetMainDivisions(out Int32 slotsEnd, out Int32 keyValueEnd)
    {
        slotsEnd = headerSize + header.slotCount * slotEntrySize;
        keyValueEnd = slotsEnd + header.freeBytes;
    }

    public Int32 ScanSearchSlot(Span<Byte> key)
    {
        var n = header.slotCount;

        for (var i = 0; i < n; ++i)
        {
            var result = Compare(i, key);

            if (result > 0)
            {
                return ~i;
            }
            else if (result == 0)
            {
                return i;
            }
        }

        return ~n;
    }

    public Int32 BinarySearchSlot(Span<Byte> key)
    {
        Int32 l = 0, r = key.Length - 1;

        while (l < r)
        {
            var m = (r - l) / 2;

            var result = Compare(m, key);

            if (result < 0)
            {
                l = m + 1;
            }
            else if (result > 0)
            {
                r = m - 1;
            }
            else
            {
                return m;
            }
        }

        return ~l;
    }
}

public static class Extensions
{
    public delegate T Decoder<T>(Span<Byte> key, Span<Byte> value);

    public static T[] MapForDiagnostics<T>(this PageOperations ops, Decoder<T> decode)
    {
        var result = new T[ops.SlotCount];

        for (var i = 0; i < ops.SlotCount; i++)
        {
            ops.GetSlot(i, out var key, out var value);

            result[i] = decode(key, value);
        }

        return result;
    }
}