using ProcessExtensions.Extensions.Internal;
using ProcessExtensions.Helpers;
﻿using ProcessExtensions.Exceptions;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Vanara.PInvoke;

#pragma warning disable CA1416 // Validate platform compatibility
#pragma warning disable CS9107 // Parameter is captured into the state of the enclosing type and its value is also passed to the base constructor. The value might be captured by the base class as well.

namespace ProcessExtensions.Interop.Context
{
    internal class FastCallContext(Process in_process, Kernel32.SafeHTHREAD? in_threadHandle) : BaseContext(in_process, in_threadHandle)
    {
        public override void Set(nint in_ip, bool in_isVariadicArgs = false, params object[] in_args)
        {
            base.Set(in_ip, in_isVariadicArgs, in_args);

            if (in_process.Is64Bit())
            {
                var context = Kernel32Helper.GetThreadContext64(_threadHandle)
                    ?? throw new VerboseWin32Exception($"Failed to get thread context.");

                context.Rip = (ulong)in_ip;

                // Reserve space for arguments.
                context.Rsp -= (ulong)(in_args.GetTotalSize(8) - 8);

                for (int i = in_args.Length - 1; i >= 0; i--)
                {
                    var arg = in_args[i];
                    var argType = arg.GetType();

                    if (i < 4 && !argType.IsStruct())
                    {
                        if (argType.Equals(typeof(float)))
                        {
                            context.DUMMYUNIONNAME.XmmRegisters.SetXMMRegister(i, 0, (float)arg);
                        }
                        else if (argType.Equals(typeof(double)))
                        {
                            context.DUMMYUNIONNAME.XmmRegisters.SetXMMRegister(i, 0, (double)arg);
                        }
                        else
                        {
                            var val = MemoryHelper.UnmanagedTypeToRegisterValue<ulong>(arg, argType);

                            switch (i)
                            {
                                case 0: context.Rcx = val; break;
                                case 1: context.Rdx = val; break;
                                case 2: context.R8  = val; break;
                                case 3: context.R9  = val; break;
                            }
                        }
                    }
                    else
                    {
                        var buffer = MemoryHelper.UnmanagedTypeToByteArray(arg, arg.GetType());

                        context.Rsp -= (ulong)buffer.Length.Align(8);

                        in_process.WriteBytes((nint)context.Rsp, buffer);
                    }
                }

                // Reserve shadow space.
                context.Rsp -= 0x20;

                Kernel32Helper.SetThreadContext64(_threadHandle, context);
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

                    var isFloat = argType.Equals(typeof(float)) || argType.Equals(typeof(double));
                    var isString = _stringIndices.Contains(i);
                    var isStruct = argType.IsStruct();

                    if (i < 2 && !in_isVariadicArgs && !isFloat && !isString && !isStruct)
                    {
                        var val = MemoryHelper.UnmanagedTypeToRegisterValue<uint>(arg, argType);

                        switch (i)
                        {
                            case 0: context.Ecx = val; break;
                            case 1: context.Edx = val; break;
                        }
                    }
                    else
                    {
                        var buffer = MemoryHelper.UnmanagedTypeToByteArray(arg, arg.GetType());

                        context.Esp -= (uint)buffer.Length.Align(4);

                        in_process.WriteBytes((nint)context.Esp, buffer);
                    }
                }

                Kernel32Helper.SetThreadContext(_threadHandle, context);
            }
        }
    }
}

#pragma warning restore CS9107 // Parameter is captured into the state of the enclosing type and its value is also passed to the base constructor. The value might be captured by the base class as well.
#pragma warning restore CA1416 // Validate platform compatibility