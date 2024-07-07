using ProcessExtensions.Enums;
using System.Diagnostics;
using Vanara.PInvoke;

namespace ProcessExtensions.Interop.Context
{
    public class ContextFactory(Process in_process, Kernel32.SafeHTHREAD? in_threadHandle, ECallingConvention in_callingConvention = ECallingConvention.FastCall)
    {
        public BaseContext Get()
        {
            switch (in_callingConvention)
            {
                case ECallingConvention.Cdecl:
                case ECallingConvention.StdCall:
                    return new CdeclContext(in_process, in_threadHandle);

                case ECallingConvention.ThisCall:
                    return new ThisCallContext(in_process, in_threadHandle);

                case ECallingConvention.FastCall:
                    return new FastCallContext(in_process, in_threadHandle);
            }

            throw new NotImplementedException($"The calling convention \"{in_callingConvention}\" is not implemented.");
        }

        public void Set(nint in_ip, bool in_isVariadicArgs, params object[] in_args)
        {
            switch (in_callingConvention)
            {
                case ECallingConvention.Cdecl:
                case ECallingConvention.StdCall:
                    (Get() as CdeclContext)?.Set(in_ip, in_isVariadicArgs, in_args);
                    break;

                case ECallingConvention.ThisCall:
                    (Get() as ThisCallContext)?.Set(in_ip, in_isVariadicArgs, in_args);
                    break;

                case ECallingConvention.FastCall:
                    (Get() as FastCallContext)?.Set(in_ip, in_isVariadicArgs, in_args);
                    break;
            }
        }

        public void Clean()
        {
            switch (in_callingConvention)
            {
                case ECallingConvention.Cdecl:
                case ECallingConvention.StdCall:
                    (Get() as CdeclContext)?.Clean();
                    break;

                case ECallingConvention.ThisCall:
                    (Get() as ThisCallContext)?.Clean();
                    break;

                case ECallingConvention.FastCall:
                    (Get() as FastCallContext)?.Clean();
                    break;
            }
        }
    }
}
