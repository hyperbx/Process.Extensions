using ProcessExtensions.Interop.Generic;
using System.Diagnostics;
using Vanara.PInvoke;

namespace ProcessExtensions.Interop.Context
{
    public class BaseContext(Process in_process, Kernel32.SafeHTHREAD? in_threadHandle)
    {
        protected Process Process = in_process;

        protected Kernel32.SafeHTHREAD? ThreadHandle = in_threadHandle;

        protected List<nint> Allocations = [];

        protected List<int> BaseAllocIndices = [];

        public virtual void Set(nint in_ip, bool in_isVariadicArgs = false, params object[] in_args)
        {
            BaseAllocIndices.Clear();

            var is64Bit = Process.Is64Bit();

            var context = new ContextWrapper(Process, ThreadHandle);
            {
                context.AlignStackPointer();
            }

            for (int i = 0; i < in_args.Length; i++)
            {
                var arg = in_args[i];
                var argType = arg.GetType();

                /* Replace non-primitive types with a pointer to
                   themselves in the target process' memory. */
                if (argType.Equals(typeof(UnmanagedPointer)))
                {
                    in_args[i] = ((UnmanagedPointer)arg).pData;
                }
                else if (argType.Equals(typeof(UnmanagedString)))
                {
                    in_args[i] = ((UnmanagedString)arg).pData;
                }
                else if (argType.Equals(typeof(string)))
                {
                    in_args[i] = Process.WriteStringNullTerminated((string)arg);

                    Allocations.Add((nint)in_args[i]);

                    // Strings are always passed on the stack in 32-bit.
                    if (!is64Bit)
                        BaseAllocIndices.Add(i);
                }

                // Transform pointer into correct width.
                if (in_args[i].GetType().Equals(typeof(nint)))
                {
                    var ptr = ((nint)in_args[i]).ToInt64();

                    if (is64Bit)
                    {
                        in_args[i] = (ulong)Convert.ToUInt64(ptr);
                    }
                    else
                    {
                        in_args[i] = (uint)Convert.ToUInt32(ptr);
                    }
                }
            }
        }

        public virtual void Clean()
        {
            foreach (var addr in Allocations)
                Process.Free(addr);

            Allocations.Clear();
            BaseAllocIndices.Clear();
        }
    }
}