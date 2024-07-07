using ProcessExtensions.Extensions.Internal;
using ProcessExtensions.Helpers.Internal;
using ProcessExtensions.Interop.Generic;
using System.Diagnostics;
using Vanara.PInvoke;

namespace ProcessExtensions.Interop.Context
{
    internal class BaseContext(Process in_process, Kernel32.SafeHTHREAD? in_threadHandle)
    {
        protected Process _process = in_process;

        protected Kernel32.SafeHTHREAD? _threadHandle = in_threadHandle;

        protected List<nint> _allocations = [];

        protected List<int> _nonPrimitiveIndices = [];

        public virtual void Set(nint in_ip, bool in_isVariadicArgs = false, params object[] in_args)
        {
            _nonPrimitiveIndices.Clear();

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
                    in_args[i] = _process.WriteStringNullTerminated((string)arg);

                    _allocations.Add((nint)in_args[i]);
                    _nonPrimitiveIndices.Add(i);
                }

                // Transform pointer into correct width.
                if (in_args[i].GetType().Equals(typeof(nint)))
                {
                    var ptr64 = ((nint)in_args[i]).ToInt64();

                    if (_process.Is64Bit())
                    {
                        in_args[i] = (ulong)Convert.ToUInt64(ptr64);
                    }
                    else
                    {
                        in_args[i] = (uint)Convert.ToUInt32(ptr64);
                    }
                }
            }
        }

        public virtual void Clean()
        {
            foreach (var addr in _allocations)
                _process.Free(addr);

            _allocations.Clear();
            _nonPrimitiveIndices.Clear();
        }
    }
}
