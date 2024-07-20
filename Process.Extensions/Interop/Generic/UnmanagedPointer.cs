using ProcessExtensions.Interop.Attributes;
using System.Diagnostics;

namespace ProcessExtensions.Interop.Generic
{
    /// <summary>
    /// A container for a pointer to an object in a remote process.
    /// </summary>
    /// <param name="in_process">The target process to write to.</param>
    /// <param name="in_data">The object to write.</param>
    [MarshalAsRegister]
    public struct UnmanagedPointer<T>(Process in_process, T in_data) where T : unmanaged
    {
        public nint pData = in_process.Write(in_data);

        /// <summary>
        /// Determines whether this pointer is null.
        /// </summary>
        /// <returns><b>false</b> if this is a null pointer; otherwise <b>true</b>.</returns>
        public bool IsValid()
        {
            return pData != 0;
        }

        /// <summary>
        /// Gets the value of the object at this pointer.
        /// </summary>
        /// <typeparam name="T">The unmanaged type to get.</typeparam>
        /// <param name="in_process">The target process this pointer pertains to.</param>
        public T Get(Process in_process)
        {
            if (!IsValid())
                return default;

            return in_process.Read<T>(pData);
        }

        /// <summary>
        /// Sets the value of the object at this pointer.
        /// </summary>
        /// <typeparam name="T">The unmanaged type to set.</typeparam>
        /// <param name="in_process">The target process this pointer pertains to.</param>
        /// <param name="in_data">The value to write.</param>
        public void Set(Process in_process, T in_data)
        {
            if (!IsValid())
                return;

            in_process.Write(in_data);
        }

        /// <summary>
        /// Frees the memory associated with this pointer.
        /// </summary>
        /// <param name="in_process">The target process this pointer pertains to.</param>
        public void Free(Process in_process)
        {
            in_process.Free(pData);
        }

        public static implicit operator bool(UnmanagedPointer<T> in_this)
        {
            return in_this.IsValid();
        }

        public override string ToString()
        {
            return $"0x{((long)pData):X}";
        }
    }
}
