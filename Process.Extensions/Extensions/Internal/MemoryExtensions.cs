namespace ProcessExtensions.Extensions.Internal
{
    internal static class MemoryExtensions
    {
        public static T Align<T>(this T in_ptr, int in_alignment) where T : unmanaged, IConvertible
        {
            long ptr = in_ptr.ToInt64(null);

            while (ptr % in_alignment != 0)
                ptr++;

            return (T)Convert.ChangeType(ptr, typeof(T));
        }
    }
}
