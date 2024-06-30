using System.Diagnostics;

namespace ProcessExtensions
{
    public static class MemoryPreserver
    {
        private static Dictionary<nint, byte[]> _preservedMemory = [];

        /// <summary>
        /// Preserves a chunk of memory to be restored later.
        /// </summary>
        /// <param name="in_process">The target process to read from.</param>
        /// <param name="in_address">The remote address to read from.</param>
        /// <param name="in_length">The amount of memory to preserve.</param>
        /// <param name="in_isPreservedOnce">Determines whether the memory should be preserved once (meaning further calls to preserve memory at <paramref name="in_address"/> will be ignored).</param>
        public static void PreserveMemory(this Process in_process, nint in_address, int in_length, bool in_isPreservedOnce = true)
        {
            if (in_address == 0)
                return;

            if (_preservedMemory.ContainsKey(in_address))
            {
                if (in_isPreservedOnce)
                    return;

                _preservedMemory.Remove(in_address);
            }

            _preservedMemory.Add(in_address, in_process.ReadBytes(in_address, in_length));
        }

        /// <summary>
        /// Restores a preserved chunk of memory.
        /// </summary>
        /// <param name="in_process">The target process to write to.</param>
        /// <param name="in_address">The address the memory was preserved from.</param>
        /// <param name="in_isDeleteOnWrite">Determines whether the preserved memory should be deleted once restored.</param>
        public static void RestoreMemory(this Process in_process, nint in_address, bool in_isDeleteOnWrite = false)
        {
            if (in_address == 0)
                return;

            if (!_preservedMemory.ContainsKey(in_address))
                return;

            in_process.WriteProtectedBytes(in_address, _preservedMemory[in_address]);

            if (in_isDeleteOnWrite)
                _preservedMemory.Remove(in_address);
        }
    }
}
