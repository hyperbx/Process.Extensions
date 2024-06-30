using System.Runtime.InteropServices;
using Vanara.PInvoke;

#pragma warning disable CA1416 // Validate platform compatibility

namespace ProcessExtensions.Helpers.Internal
{
    internal class Kernel32Helper
    {
        [DllImport("kernel32.dll", EntryPoint = "Wow64GetThreadContext", SetLastError = true)]
        private static extern bool GetThreadContext(nint in_hThread, ref Kernel32.CONTEXT in_lpContext);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetThreadContext(nint in_hThread, ref Kernel32.CONTEXT64 in_lpContext);

        [DllImport("kernel32.dll", EntryPoint = "Wow64SetThreadContext", SetLastError = true)]
        private static extern bool SetThreadContext(nint in_hThread, ref Kernel32.CONTEXT in_lpContext);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetThreadContext(nint in_hThread, ref Kernel32.CONTEXT64 in_lpContext);

        public static Kernel32.CONTEXT? GetThreadContext(Kernel32.SafeHTHREAD? in_handle)
        {
            if (in_handle == null)
                return null;

            var context = new Kernel32.CONTEXT(Kernel32.CONTEXT_FLAG.CONTEXT_ALL);

            GetThreadContext(in_handle.DangerousGetHandle(), ref context);

            return context;
        }

        public static Kernel32.CONTEXT64? GetThreadContext64(Kernel32.SafeHTHREAD? in_handle)
        {
            if (in_handle == null)
                return null;

            var context = new Kernel32.CONTEXT64()
            {
                ContextFlags = Kernel32.CONTEXT_FLAG.CONTEXT_ALL
            };

            GetThreadContext(in_handle.DangerousGetHandle(), ref context);

            return context;
        }

        public static void SetThreadContext(Kernel32.SafeHTHREAD? in_handle, Kernel32.CONTEXT in_context)
        {
            if (in_handle == null)
                return;

            SetThreadContext(in_handle.DangerousGetHandle(), ref in_context);
        }

        public static void SetThreadContext64(Kernel32.SafeHTHREAD? in_handle, Kernel32.CONTEXT64 in_context)
        {
            if (in_handle == null)
                return;

            SetThreadContext(in_handle.DangerousGetHandle(), ref in_context);
        }
    }
}

#pragma warning restore CA1416 // Validate platform compatibility