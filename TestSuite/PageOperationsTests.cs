using Indrex;

namespace TestSuite;

[TestClass]
public class PageOperationsTests
{
    static String DecodeKeyValue(Span<Byte> key, Span<Byte> value)
        => $"{key.DecodeUtf8()}={value.DecodeUtf8()}";

    public ref struct Tester
    {
        public Byte[] pageBytes;

        public Span<Byte> pageSpan;

        public PageOperations ops;

        void DebugPrintState()
        {
            ops.PruneFree();

            Console.WriteLine(pageSpan.GetDebugRepresentation());
        }

        public Tester(Int32 pageSize)
        {
            pageBytes = new Byte[pageSize];

            pageSpan = pageBytes.AsSpan();

            ops = new PageOperations(pageSpan);

            ops.Wipe();
        }

        public Boolean Insert(String key, String value, Boolean assertSuccess = true)
        {
            DebugPrintState();

            var slotNo = ops.ScanSearchSlot(key.EncodeUtf8());

            Assert.IsTrue(slotNo < 0);

            var success = ops.TryInsert(~slotNo, key.EncodeUtf8(), value.EncodeUtf8());

            if (assertSuccess) Assert.IsTrue(success);

            return success;
        }

        public void Remove(String key)
        {
            DebugPrintState();

            var slotNo = ops.ScanSearchSlot(key.EncodeUtf8());

            Assert.IsTrue(slotNo >= 0);

            ops.Remove(slotNo);
        }

        public void Check(String expected)
        {
            DebugPrintState();

            var actual = String.Join(",", ops.MapForDiagnostics(DecodeKeyValue).ToArray());

            Assert.AreEqual(expected, actual);
        }
    }

    [TestMethod]
    public void TestSimpleCrud()
    {
        var tester = new Tester(64);

        tester.Insert("foo", "1");

        tester.Check("foo=1");

        tester.Insert("bar", "22");

        tester.Check("bar=22,foo=1");

        tester.Insert("baz", "333");

        tester.Check("bar=22,baz=333,foo=1");

        tester.Insert("bat", "4444");

        tester.Check("bar=22,bat=4444,baz=333,foo=1");

        tester.Remove("bat");

        tester.Check("bar=22,baz=333,foo=1");

        tester.Remove("bar");

        tester.Check("baz=333,foo=1");

        tester.Remove("baz");

        tester.Check("foo=1");

        tester.Remove("foo");

        tester.Check("");
    }

    [TestMethod]
    public void TestSizeBoundary()
    {
        var pageSize = PageOperations.headerSize + PageOperations.slotEntrySize + 6;

        var tester = new Tester(pageSize);

        Assert.IsFalse(tester.Insert("foo", "abcd", assertSuccess: false));

        tester.Insert("foo", "abc");

        tester.Check("foo=abc");
    }
}