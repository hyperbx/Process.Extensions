using ProcessExtensions.Exceptions;
using ProcessExtensions.Helpers.Internal;
using System.Diagnostics;
using Vanara.PInvoke;

#pragma warning disable CA1416 // Validate platform compatibility

namespace ProcessExtensions.Interop.Context
{
    internal class ThisCallContext(Process in_process, Kernel32.SafeHTHREAD? in_threadHandle) : BaseContext(in_process, in_threadHandle)
    {
        public override void Set(nint in_ip, bool in_isVariadicArgs = false, params object[] in_args)
        {
            var @this = in_args[0];

            if (!@this.GetType().Equals(typeof(nint)))
            {
                var buffer = MemoryHelper.UnmanagedTypeToByteArray(@this, @this.GetType());

                // Write mapped structure to process memory, use as "this" pointer.
                @this = in_args[0] = _process.WriteBytes(buffer);

                _allocations.Add((nint)@this);
            }

            if (_process.Is64Bit())
            {
                new FastCallContext(_process, _threadHandle).Set(in_ip, in_isVariadicArgs, in_args);
            }
            else
            {
                var context = Kernel32Helper.GetThreadContext(_threadHandle)
                    ?? throw new VerboseWin32Exception($"Failed to get thread context.");

                // Truncate pointer to 32-bit width.
                if (@this.GetType().Equals(typeof(nint)))
                    @this = Convert.ToUInt32(((nint)@this).ToInt64());

                // Store pointer to class in ECX.
                context.Ecx = (uint)@this;

                Kernel32Helper.SetThreadContext(_threadHandle, context);

                new CdeclContext(_process, _threadHandle).Set(in_ip, in_isVariadicArgs, in_args.TakeLast(in_args.Length - 1).ToArray());
            }
        }
    }
}

#pragma warning restore CA1416 // Validate platform compatibility