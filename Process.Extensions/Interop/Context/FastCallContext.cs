using ProcessExtensions.Enums;
using ProcessExtensions.Extensions.Internal;
using ProcessExtensions.Helpers.Internal;
using System.Diagnostics;
using Vanara.PInvoke;

#pragma warning disable CS9107 // Parameter is captured into the state of the enclosing type and its value is also passed to the base constructor. The value might be captured by the base class as well.

namespace ProcessExtensions.Interop.Context
{
    public class FastCallContext(Process in_process, Kernel32.SafeHTHREAD? in_threadHandle) : BaseContext(in_process, in_threadHandle)
    {
        public override void Set(nint in_ip, bool in_isVariadicArgs = false, params object[] in_args)
        {
            base.Set(in_ip, in_isVariadicArgs, in_args);

            var context = new ContextWrapper(in_process, in_threadHandle);

            context.SetGPR(EBaseRegister.RIP, in_ip);

            if (in_process.Is64Bit())
            {
                // Reserve space for arguments.
                context.SetStackPointer(-(in_args.GetTotalSize(8) - 8).Align(8));

                for (int i = in_args.Length - 1; i >= 0; i--)
                {
                    var arg = in_args[i];
                    var argType = arg.GetType();

                    var isImmediateValue = !BaseAllocIndices.Contains(i);

                    if (i < 4 && isImmediateValue)
                    {
                        /* Write struct to the stack and replace current
                           argument with a pointer to itself to be stored
                           in one of the first four registers. */
                        if (argType.IsStruct())
                        {
                            arg = context.StackWrite(arg);
                            argType = typeof(ulong);
                        }

                        if (argType.Equals(typeof(float)) || argType.Equals(typeof(double)))
                        {
                            context.SetFPR(i, 0, arg);
                        }
                        else
                        {
                            var register = i switch
                            {
                                1 => EBaseRegister.RDX,
                                2 => EBaseRegister.R8,
                                3 => EBaseRegister.R9,
                                _ => EBaseRegister.RCX
                            };

                            context.SetGPR(register, MemoryHelper.UnmanagedTypeToRegisterValue<ulong>(arg));
                        }
                    }
                    else
                    {
                        context.StackWrite(arg);
                    }
                }

                // Reserve shadow space.
                context.SetStackPointer(-0x20);
            }
            else
            {
                for (int i = in_args.Length - 1; i >= 0; i--)
                {
                    var arg = in_args[i];
                    var argType = arg.GetType();

                    var isFloat = argType.Equals(typeof(float)) || argType.Equals(typeof(double));
                    var isStruct = argType.IsStruct();
                    var isImmediateValue = !BaseAllocIndices.Contains(i);

                    if (i < 2 && !isFloat && !isStruct && isImmediateValue && !in_isVariadicArgs)
                    {
                        var register = i == 1
                            ? EBaseRegister.RDX
                            : EBaseRegister.RCX;

                        context.SetGPR(register, MemoryHelper.UnmanagedTypeToRegisterValue<uint>(arg));
                    }
                    else
                    {
                        context.StackWrite(arg);
                    }
                }
            }
        }
    }
}

#pragma warning restore CS9107 // Parameter is captured into the state of the enclosing type and its value is also passed to the base constructor. The value might be captured by the base class as well.