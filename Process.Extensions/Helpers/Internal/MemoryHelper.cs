using System.Runtime.InteropServices;

namespace ProcessExtensions.Helpers.Internal
{
    internal class MemoryHelper
    {
        public static object? ByteArrayToUnmanagedType(byte[] in_data, Type in_type, bool in_isBigEndian = false)
        {
            if (in_data == null || in_data.Length <= 0)
                return null;

            if (in_isBigEndian)
                in_data = in_data.Reverse().ToArray();

            var handle = GCHandle.Alloc(in_data, GCHandleType.Pinned);

            try
            {
                return Marshal.PtrToStructure(handle.AddrOfPinnedObject(), in_type)!;
            }
            finally
            {
                handle.Free();
            }
        }

        public static T? ByteArrayToUnmanagedType<T>(byte[] in_data, bool in_isBigEndian = false)
        {
            return (T?)ByteArrayToUnmanagedType(in_data, typeof(T), in_isBigEndian);
        }

        public static byte[] UnmanagedTypeToByteArray(object in_structure, bool in_isBigEndian = false)
        {
            byte[] data = new byte[Marshal.SizeOf(in_structure.GetType())];

            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);

            try
            {
                Marshal.StructureToPtr(in_structure, handle.AddrOfPinnedObject(), false);
            }
            finally
            {
                handle.Free();
            }

            return in_isBigEndian ? data.Reverse().ToArray() : data;
        }

        public static T UnmanagedTypeToRegisterValue<T>(object in_structure, bool in_isBigEndian = false) where T : unmanaged
        {
            var structBuffer = UnmanagedTypeToByteArray(in_structure, in_isBigEndian);

            var unmanagedType = typeof(T);
            var unmanagedSize = Marshal.SizeOf<T>();

            var resultBuffer = new byte[unmanagedSize];

            Array.Copy(structBuffer, resultBuffer, Math.Min(structBuffer.Length, resultBuffer.Length));

            if (unmanagedType.Equals(typeof(short)))
                return (T)(object)BitConverter.ToInt16(resultBuffer);
            else if (unmanagedType.Equals(typeof(ushort)))
                return (T)(object)BitConverter.ToUInt16(resultBuffer);
            else if (unmanagedType.Equals(typeof(int)))
                return (T)(object)BitConverter.ToInt32(resultBuffer);
            else if (unmanagedType.Equals(typeof(uint)))
                return (T)(object)BitConverter.ToUInt32(resultBuffer);
            else if (unmanagedType.Equals(typeof(long)))
                return (T)(object)BitConverter.ToInt64(resultBuffer);
            else if (unmanagedType.Equals(typeof(ulong)))
                return (T)(object)BitConverter.ToUInt64(resultBuffer);

            return default;
        }
    }
}
