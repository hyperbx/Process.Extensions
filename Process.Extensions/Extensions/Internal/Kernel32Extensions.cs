using System.Runtime.InteropServices;
using static Vanara.PInvoke.Kernel32.CONTEXT64;

#pragma warning disable CA1416 // Validate platform compatibility

namespace ProcessExtensions.Extensions.Internal
{
    internal static class Kernel32Extensions
    {
        public static void SetXMMRegister<T>(this M128A[] in_m128, int in_index, int in_subIndex, T in_value) where T : struct
        {
            if (!typeof(T).Equals(typeof(float)) && !typeof(T).Equals(typeof(double)))
                throw new ArgumentException("Type must be float or double.");

            if (in_index < 0 || in_index >= in_m128.Length)
                throw new IndexOutOfRangeException("Invalid XMM register index.");

            if (typeof(T).Equals(typeof(float)) && (in_subIndex < 0 || in_subIndex > 3))
                throw new ArgumentOutOfRangeException("Sub-index for float must be between 0 and 3.");

            if (typeof(T).Equals(typeof(double)) && (in_subIndex < 0 || in_subIndex > 1))
                throw new ArgumentOutOfRangeException("Sub-index for double must be between 0 and 1.");

            var buffer = typeof(T).Equals(typeof(float))
                ? BitConverter.GetBytes(Convert.ToSingle(in_value))
                : BitConverter.GetBytes(Convert.ToDouble(in_value));

            var handle = GCHandle.Alloc(in_m128, GCHandleType.Pinned);

            try
            {
                var ptr = handle.AddrOfPinnedObject();
                var regPtr = IntPtr.Add(ptr, in_index * Marshal.SizeOf<M128A>());
                var subPtr = IntPtr.Add(regPtr, in_subIndex * Marshal.SizeOf<T>());

                Marshal.Copy(buffer, 0, subPtr, buffer.Length);
            }
            finally
            {
                handle.Free();
            }
        }
    }
}

#pragma warning restore CA1416 // Validate platform compatibility