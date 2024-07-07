namespace ProcessExtensions.Extensions.Internal
{
    internal static class MemoryExtensions
    {
        public static T Align<T>(this T in_ptr, int in_alignment) where T : unmanaged, IConvertible
        {
            var ptr = in_ptr.ToInt64(null);

            return (T)Convert.ChangeType((ptr + (in_alignment - 1)) & ~(in_alignment - 1), typeof(T));
        }
    }
}
