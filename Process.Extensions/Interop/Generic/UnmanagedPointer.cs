using ProcessExtensions.Interop.Attributes;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ProcessExtensions.Interop.Generic
{
    [MarshalAsRegister]
    public struct UnmanagedPointer(Process in_process, object in_data)
    {
        public nint pData = in_process.Write(in_data, in_data.GetType());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValid()
        {
            return pData != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Get<T>(Process in_process) where T : unmanaged
        {
            if (!IsValid())
                return default;

            return in_process.Read<T>(pData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set<T>(Process in_process, T in_data) where T : unmanaged
        {
            if (!IsValid())
                return;

            in_process.Write(in_data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator bool(UnmanagedPointer in_this) => in_this.IsValid();

        public override string ToString()
        {
            return $"0x{((long)pData):X}";
        }
    }
}
