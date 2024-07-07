using ProcessExtensions.Exceptions;
using ProcessExtensions.Helpers.Internal;
using System.Diagnostics;
using Vanara.PInvoke;

#pragma warning disable CA1416 // Validate platform compatibility

namespace ProcessExtensions.Extensions
{
    public static class Context
    {
        /// <summary>
        /// Gets the pointer to a thread's local storage.
        /// </summary>
        /// <param name="in_process">The target process the thread is associated with.</param>
        /// <param name="in_thread">The thread to get the pointer to local storage from.</param>
        /// <returns>A pointer in the target process' memory to the thread's local storage.</returns>
        /// <exception cref="VerboseWin32Exception"/>
        public static nint GetThreadLocalStoragePointer(this Process in_process, ProcessThread in_thread)
        {
            if (in_process.HasExited)
                return 0;

            var handle = Kernel32.OpenThread((int)Kernel32.ThreadAccess.THREAD_ALL_ACCESS, false, (uint)in_thread.Id);

            if (handle == 0)
                throw new VerboseWin32Exception($"Failed to open thread {in_thread.Id}.");

            var threadInfo = NtDllHelper.GetThreadInformation(handle);

            handle.Close();

            if (threadInfo == null)
                throw new VerboseWin32Exception($"Failed to get information about thread {in_thread.Id}.");

            if (threadInfo.TebBaseAddress == 0)
                throw new VerboseWin32Exception($"Invalid environment block in thread {in_thread.Id}.");

            return in_process.Read<nint>(in_process.Read<nint>(threadInfo.TebBaseAddress + 0x58));
        }
    }
}

#pragma warning restore CA1416 // Validate platform compatibility