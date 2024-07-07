using ProcessExtensions.Enums;
using ProcessExtensions.Extensions.Internal;
using System.Diagnostics;
using Vanara.PInvoke;

namespace ProcessExtensions.Interop.Context
{
    public class ThisCallContext(Process in_process, Kernel32.SafeHTHREAD? in_threadHandle) : BaseContext(in_process, in_threadHandle)
    {
        public override void Set(nint in_ip, bool in_isVariadicArgs = false, params object[] in_args)
        {
            base.Set(in_ip, in_isVariadicArgs, in_args);

            if (Process.Is64Bit())
            {
                new FastCallContext(Process, ThreadHandle).Set(in_ip, in_isVariadicArgs, in_args);
            }
            else
            {
                var context = new ContextWrapper(Process, ThreadHandle);

                // Write "this" struct to the stack.
                if (in_args[0].GetType().IsStruct())
                    in_args[0] = context.StackWrite(in_args[0]);

                // Store pointer to class in ECX.
                context.SetGPR(EBaseRegister.RCX, (uint)in_args[0]);

                new CdeclContext(Process, ThreadHandle).Set(in_ip, in_isVariadicArgs, in_args.TakeLast(in_args.Length - 1).ToArray());
            }
        }
    }
}