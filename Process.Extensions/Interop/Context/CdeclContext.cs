using ProcessExtensions.Enums;
using System.Diagnostics;
using Vanara.PInvoke;

namespace ProcessExtensions.Interop.Context
{
    public class CdeclContext(Process in_process, Kernel32.SafeHTHREAD? in_threadHandle) : BaseContext(in_process, in_threadHandle)
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

                context.SetGPR(EBaseRegister.RIP, in_ip);

                for (int i = in_args.Length - 1; i >= 0; i--)
                    context.StackWrite(in_args[i]);
            }
        }
    }
}