using System.Text;

public static class SpanExtensions
{
    public static unsafe Int32 SizeOf<T>()
        => sizeof(T);

    public static unsafe Span<T> InterpretAs<T>(this Span<Byte> bytes)
        where T : unmanaged
    {
        fixed (byte* ptr = &bytes.GetPinnableReference())
            return new Span<T>(ptr, bytes.Length / sizeof(T));
    }

    public static unsafe Span<Byte> Deinterpret<T>(this Span<T> bytes)
        where T : unmanaged
    {
        fixed (T* ptr = &bytes.GetPinnableReference())
            return new Span<Byte>(ptr, bytes.Length * sizeof(T));
    }

    public static unsafe ReadOnlySpan<T> InterpretAs<T>(this ReadOnlySpan<Byte> bytes)
        where T : unmanaged
    {
        fixed (byte* ptr = &bytes.GetPinnableReference())
            return new Span<T>(ptr, bytes.Length / sizeof(T));
    }

    public static unsafe ReadOnlySpan<Byte> Deinterpret<T>(this ReadOnlySpan<T> bytes)
        where T : unmanaged
    {
        fixed (T* ptr = &bytes.GetPinnableReference())
            return new Span<Byte>(ptr, bytes.Length * sizeof(T));
    }

    public static ReadOnlySpan<Byte> Truncate(this ReadOnlySpan<Byte> bytes, Int32 length)
        => bytes.Length > length ? bytes[..length] : bytes;

    public static ReadOnlySpan<Byte> TrimZeroTermination(this ReadOnlySpan<Byte> bytes)
    {
        var i = bytes.IndexOf((byte)0);

        return bytes[..(i < 0 ? ^0 : i)];
    }

    public static ArraySegment<Byte> TrimZeroTermination(this ArraySegment<Byte> bytes)
    {
        var i = bytes.AsSpan().IndexOf((byte)0);

        return bytes[..(i < 0 ? ^0 : i)];
    }

    public static String DecodeUtf8(this Span<Byte> bytes)
    {
        var i = bytes.IndexOf((byte)0);
        return Encoding.UTF8.GetString(bytes[..(i < 0 ? ^0 : i)]);
    }

    public static String DecodeUtf8(this ReadOnlySpan<Byte> bytes)
    {
        var i = bytes.IndexOf((byte)0);
        return Encoding.UTF8.GetString(bytes[..(i < 0 ? ^0 : i)]);
    }

    public static Byte[] EncodeUtf8(this String text)
        => Encoding.UTF8.GetBytes(text);
}
