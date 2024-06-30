using ProcessExtensions.Exceptions;
using ProcessExtensions.Extensions.Internal;
using ProcessExtensions.Helpers.Internal;
using System.Diagnostics;
using Vanara.PInvoke;

#pragma warning disable CA1416 // Validate platform compatibility
#pragma warning disable CS9107 // Parameter is captured into the state of the enclosing type and its value is also passed to the base constructor. The value might be captured by the base class as well.

namespace ProcessExtensions.Interop.Context
{
    internal class CdeclContext(Process in_process, Kernel32.SafeHTHREAD? in_threadHandle) : BaseContext(in_process, in_threadHandle)
    {
        public override void Set(nint in_ip, bool in_isVariadicArgs = false, params object[] in_args)
        {
            if (_process.Is64Bit())
            {
                new FastCallContext(_process, _threadHandle).Set(in_ip, in_isVariadicArgs, in_args);
            }
            else
            {
                var context = Kernel32Helper.GetThreadContext(_threadHandle)
                    ?? throw new VerboseWin32Exception($"Failed to get thread context.");

                context.Eip = (uint)in_ip;

                for (int i = in_args.Length - 1; i >= 0; i--)
                {
                    var arg = in_args[i];
                    var argType = arg.GetType();

                    // Truncate pointer to 32-bit width.
                    if (argType.Equals(typeof(nint)))
                        arg = Convert.ToUInt32(((nint)arg).ToInt64());

                    var buffer = MemoryHelper.UnmanagedTypeToByteArray(arg, arg.GetType());

                    context.Esp -= (uint)buffer.Length.Align(4);

                    in_process.WriteBytes((nint)context.Esp, buffer);
                }

                Kernel32Helper.SetThreadContext(_threadHandle, context);
            }
        }
    }
}

#pragma warning restore CS9107 // Parameter is captured into the state of the enclosing type and its value is also passed to the base constructor. The value might be captured by the base class as well.
#pragma warning restore CA1416 // Validate platform compatibility