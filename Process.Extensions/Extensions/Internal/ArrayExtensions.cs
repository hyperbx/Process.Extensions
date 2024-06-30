using System.Runtime.InteropServices;

namespace ProcessExtensions.Extensions.Internal
{
    internal static class ArrayExtensions
    {
        public static int GetTotalSize(this object[] in_array, int in_alignment = 0)
        {
            var result = 0;

            foreach (var element in in_array)
            {
                var size = Marshal.SizeOf(element);

                if (in_alignment > 0)
                    size = ((size + in_alignment - 1) / in_alignment) * in_alignment;

                result += size;
            }

            return result;
        }
    }
}
