using System.Diagnostics;
using System.Runtime.InteropServices;
using Vanara.PInvoke;

namespace ProcessExtensions.Interop.Context
{
    internal class ContextFactory(Process in_process, Kernel32.SafeHTHREAD? in_threadHandle, CallingConvention in_callingConvention = CallingConvention.FastCall)
    {
        public BaseContext Get()
        {
            switch (in_callingConvention)
            {
                case CallingConvention.Cdecl:
                case CallingConvention.StdCall:
                    return new CdeclContext(in_process, in_threadHandle);

                case CallingConvention.ThisCall:
                    return new ThisCallContext(in_process, in_threadHandle);

                case CallingConvention.FastCall:
                    return new FastCallContext(in_process, in_threadHandle);
            }

            throw new NotImplementedException($"The calling convention \"{in_callingConvention}\" is not implemented.");
        }

        public void Set(nint in_ip, bool in_isVariadicArgs, params object[] in_args)
        {
            switch (in_callingConvention)
            {
                case CallingConvention.Cdecl:
                case CallingConvention.StdCall:
                    (Get() as CdeclContext)?.Set(in_ip, in_isVariadicArgs, in_args);
                    break;

                case CallingConvention.ThisCall:
                    (Get() as ThisCallContext)?.Set(in_ip, in_isVariadicArgs, in_args);
                    break;

                case CallingConvention.FastCall:
                    (Get() as FastCallContext)?.Set(in_ip, in_isVariadicArgs, in_args);
                    break;
            }
        }

        public void Clean()
        {
            switch (in_callingConvention)
            {
                case CallingConvention.Cdecl:
                case CallingConvention.StdCall:
                    (Get() as CdeclContext)?.Clean();
                    break;

                case CallingConvention.ThisCall:
                    (Get() as ThisCallContext)?.Clean();
                    break;

                case CallingConvention.FastCall:
                    (Get() as FastCallContext)?.Clean();
                    break;
            }
        }
    }
}
