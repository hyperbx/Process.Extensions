using ProcessExtensions.Interop.Attributes;
using System.Diagnostics;

namespace ProcessExtensions.Interop.Generic
{
    [MarshalAsRegister]
    public struct UnmanagedPointer(Process in_process, object in_data)
    {
        public nint pData = in_process.Write(in_data, in_data.GetType());

        public readonly bool IsValid()
        {
            return pData != 0;
        }

        public readonly T Get<T>(Process in_process) where T : unmanaged
        {
            if (!IsValid())
                return default;

            return in_process.Read<T>(pData);
        }

        public readonly void Set<T>(Process in_process, T in_data) where T : unmanaged
        {
            if (!IsValid())
                return;

            in_process.Write(in_data);
        }

        public readonly void Free(Process in_process)
        {
            in_process.Free(pData);
        }

        public static implicit operator bool(UnmanagedPointer in_this)
        {
            return in_this.IsValid();
        }

        public override readonly string ToString()
        {
            return $"0x{((long)pData):X}";
        }
    }
}
