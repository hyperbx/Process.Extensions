using System.Diagnostics;

namespace ProcessExtensions
{
    public static class Signature
    {
        private static nint ScanSignature(this Process in_process, string in_pattern, string in_mask, nint in_startAddress = 0, nint in_endAddress = 0)
        {
            if (in_process.MainModule == null)
                return 0;

            var maskLength = in_mask.Length;

            if (in_startAddress == 0)
                in_startAddress = in_process.MainModule.BaseAddress;

            for (nint i = 0; i < in_endAddress; i++)
            {
                var addr = in_startAddress + i;
                int maskIndex;

                for (maskIndex = 0; maskIndex < maskLength; maskIndex++)
                {
                    var maskSubIndex = addr + maskIndex;

                    if (maskSubIndex >= in_endAddress)
                        break;

                    var b = in_process.Read<byte>(maskSubIndex);

                    if (in_mask[maskIndex] != '?' && in_pattern[maskIndex] != b)
                        break;
                }

                if (maskIndex == maskLength)
                    return addr;
            }

            return 0;
        }

        /// <summary>
        /// Scans the main module of the target process for a pattern.
        /// </summary>
        /// <param name="in_process">The target process to scan.</param>
        /// <param name="in_pattern">The pattern to scan for.</param>
        /// <param name="in_mask">The mask of the pattern.</param>
        /// <param name="in_startAddress">
        ///     The address to start scanning from.
        ///     <para>If set to zero, the scanner will start from the base of the main module.</para>
        ///     <para>Otherwise, the scanner will start at the specified address. If nothing is found, it will restart from the beginning and stop once the address is reached again.</para>
        /// </param>
        /// <returns>A pointer to the specified pattern in the target process' memory, if found; otherwise, returns zero.</returns>
        public static nint ScanSignature(this Process in_process, string in_pattern, string in_mask, nint in_startAddress = 0)
        {
            if (in_process.HasExited || in_process.MainModule == null)
                return 0;

            var result = in_process.ScanSignature(in_pattern, in_mask, in_startAddress, in_process.MainModule.BaseAddress + in_process.MainModule.ModuleMemorySize);

            if (result == 0 && in_startAddress != 0)
                result = in_process.ScanSignature(in_pattern, in_mask, 0, in_startAddress);

            return result;
        }
    }
}
