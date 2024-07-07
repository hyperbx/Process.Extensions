using ProcessExtensions.Interop.Attributes;
using System.Diagnostics;
using System.Text;

namespace ProcessExtensions.Interop.Generic
{
    /// <summary>
    /// A container for a pointer to a string in a remote process.
    /// </summary>
    /// <param name="in_process">The target process to write to.</param>
    /// <param name="in_str">The string to write.</param>
    /// <param name="in_encoding">The encoding of the string to write.</param>
    [MarshalAsRegister]
    public struct UnmanagedString(Process in_process, string in_str, Encoding? in_encoding = null)
    {
        public nint pData = in_process.WriteStringNullTerminated(in_str, in_encoding);

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
        public string Get(Process in_process)
        {
            if (!IsValid())
                return string.Empty;

            return in_process.ReadStringNullTerminated(pData);
        }

        /// <summary>
        /// Sets the value of the object at this pointer.
        /// </summary>
        /// <typeparam name="T">The unmanaged type to set.</typeparam>
        /// <param name="in_process">The target process this pointer pertains to.</param>
        /// <param name="in_str">The string to write.</param>
        public void Set(Process in_process, string in_str, Encoding? in_encoding = null)
        {
            if (!IsValid())
                return;

            in_process.WriteStringNullTerminated(in_str, in_encoding);
        }

        /// <summary>
        /// Frees the memory associated with this pointer.
        /// </summary>
        /// <param name="in_process">The target process this pointer pertains to.</param>
        public void Free(Process in_process)
        {
            in_process.Free(pData);
        }

        public static implicit operator bool(UnmanagedString in_this)
        {
            return in_this.IsValid();
        }

        public override string ToString()
        {
            return $"0x{((long)pData):X}";
        }
    }
}
