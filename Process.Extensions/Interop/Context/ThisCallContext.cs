using ProcessExtensions.Exceptions;
using ProcessExtensions.Helpers.Internal;
using System.Diagnostics;
using Vanara.PInvoke;

namespace ProcessExtensions.Interop.Context
{
    internal class ThisCallContext(Process in_process, Kernel32.SafeHTHREAD? in_threadHandle) : BaseContext(in_process, in_threadHandle)
    {
        public override void Set(nint in_ip, bool in_isVariadicArgs = false, params object[] in_args)
        {
            base.Set(in_ip, in_isVariadicArgs, in_args);

            if (_process.Is64Bit())
            {
                new FastCallContext(_process, _threadHandle).Set(in_ip, in_isVariadicArgs, in_args);
            }
            else
            {
                var context = Kernel32Helper.GetThreadContext(_threadHandle)
                    ?? throw new VerboseWin32Exception($"Failed to get thread context.");

                // Store pointer to class in ECX.
                context.Ecx = (uint)in_args[0];

                Kernel32Helper.SetThreadContext(_threadHandle, context);

                new CdeclContext(_process, _threadHandle).Set(in_ip, in_isVariadicArgs, in_args.TakeLast(in_args.Length - 1).ToArray());
            }
        }
    }
}