﻿using ProcessExtensions.Enums;
using ProcessExtensions.Interop;
using System.Diagnostics;

namespace ProcessExtensions.Extensions
{
    public static class Utility
    {
        /// <summary>
        /// Gets the name of the target process' CPU architecture.
        /// </summary>
        /// <param name="in_process">The target process to determine the architecture.</param>
        /// <param name="in_isShortened">
        ///     Determines whether the name will use a shortened variant.
        ///     <para>If the archiecture is 64-bit, this will return "x64" if true; otherwise, it'll return "x86-64".</para>
        /// </param>
        public static string GetArchitectureName(this Process in_process, bool in_isShortened = true)
        {
            return in_process.Is64Bit()
                ? in_isShortened
                    ? "x64"
                    : "x86-64"
                : "x86";
        }

        /// <summary>
        /// Gets the last Win32 error code reported by the target process.
        /// </summary>
        /// <param name="in_process">The target process to get the error code from.</param>
        public static int GetLastWin32Error(this Process in_process)
        {
            var GetLastError = new UnmanagedProcessFunctionPointer<int>(in_process,
                in_process.GetProcedureAddress("kernel32", "GetLastError"), ECallingConvention.Windows);

            var result = GetLastError.Invoke();

            GetLastError.Dispose();

            return result;
        }

        /// <summary>
        /// Gets the size of a pointer for the target process' CPU architecture.
        /// </summary>
        /// <param name="in_process">The target process to determine the architecture.</param>
        /// <returns>8, if the process is 64-bit; otherwise, 4.</returns>
        public static int GetPointerSize(this Process in_process)
        {
            return in_process.Is64Bit() ? 8 : 4;
        }
    }
}
