using System.Diagnostics;
using Vanara.PInvoke;

namespace ProcessExtensions.Interop.Context
{
    internal class BaseContext(Process in_process, Kernel32.SafeHTHREAD? in_threadHandle)
    {
        protected Process _process = in_process;

        protected Kernel32.SafeHTHREAD? _threadHandle = in_threadHandle;

        protected List<nint> _allocations = [];

        protected List<int> _stringIndices = [];

        public virtual void Set(nint in_ip, bool in_isVariadicArgs = false, params object[] in_args)
        {
            _stringIndices.Clear();

            for (int i = 0; i < in_args.Length; i++)
            {
                var arg = in_args[i];

                // Replace string literal with pointer to string in process memory.
                if (arg.GetType().Equals(typeof(string)))
                {
                    var strAlloc = _process.WriteStringNullTerminated((string)arg);

                    _allocations.Add(strAlloc);

                    in_args[i] = strAlloc;

                    _stringIndices.Add(i);
                }
            }
        }

        public virtual void Clean()
        {
            foreach (var addr in _allocations)
                _process.Free(addr);

            _allocations.Clear();
            _stringIndices.Clear();
        }
    }
}
