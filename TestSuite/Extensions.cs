using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestSuite;

public static class Extensions
{
    public static Byte[] MakeTestArray(this Char ch, Int32 length)
        => new String(ch, length).Select(c => (Byte)c).ToArray();

    public static Byte[] EncodeUtf8(this String text)
        => Encoding.UTF8.GetBytes(text);

    public static String DecodeUtf8(this Span<Byte> bytes)
        => Encoding.UTF8.GetString(bytes);

    public static String GetDebugRepresentation(this Span<Byte> span)
    {
        var r = new Char[span.Length];

        for (var i = 0; i < span.Length; i++)
        {
            var b = span[i];

            if (b < 32 || b > 126)
            {
                r[i] = '.';
            }
            else
            {
                r[i] = (Char)b;
            }
        }

        return new String(r);
    }
}
