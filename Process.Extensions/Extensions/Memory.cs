using ProcessExtensions.Helpers;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Vanara.PInvoke;

#pragma warning disable CA1416 // Validate platform compatibility

namespace ProcessExtensions
{
    public static class Memory
    {
        private static Dictionary<string, nint> _staticAllocations = [];

        /// <summary>
        /// Transforms a virtual address to the process' current base.
        /// <para>This should only be used for processes that use <see href="https://en.wikipedia.org/wiki/Address_space_layout_randomization">Address Space Layout Randomisation (ASLR)</see>.</para>
        /// </summary>
        /// <param name="in_process">The target process to get the base address from.</param>
        /// <param name="in_address">The address to transform.</param>
        public static nint ToASLR(this Process in_process, long in_address)
        {
            if (in_process.MainModule == null)
                return 0;

            return (nint)(in_process.MainModule.BaseAddress + (in_address - (in_process.Is64Bit() ? 0x140000000 : 0x400000)));
        }

        /// <summary>
        /// Transforms a virtual address from the process' current base.
        /// <para>This should only be used for processes that use <see href="https://en.wikipedia.org/wiki/Address_space_layout_randomization">Address Space Layout Randomisation (ASLR)</see>.</para>
        /// </summary>
        /// <param name="in_process">The target process to get the base address from.</param>
        /// <param name="in_address">The address to transform.</param>
        public static nint FromASLR(this Process in_process, long in_address)
        {
            if (in_process.MainModule == null)
                return 0;

            return (nint)(in_address + (in_process.Is64Bit() ? 0x140000000 : 0x400000) - in_process.MainModule.BaseAddress);
        }

        /// <summary>
        /// Allocates memory in the target process.
        /// </summary>
        /// <param name="in_process">The target process to allocate memory in.</param>
        /// <param name="in_size">The amount of memory to allocate.</param>
        /// <returns>A pointer in the target process' memory to the allocated memory.</returns>
        /// <exception cref="Win32Exception"/>
        public static nint Alloc(this Process in_process, int in_size)
        {
            var result = Kernel32.VirtualAllocEx(in_process.Handle, 0, in_size, Kernel32.MEM_ALLOCATION_TYPE.MEM_COMMIT, Kernel32.MEM_PROTECTION.PAGE_EXECUTE_READWRITE);

            if (result == 0)
                throw new Win32Exception($"Memory allocation failed ({Marshal.GetLastWin32Error()}).");

            return result;
        }

        /// <summary>
        /// Allocates memory in the target process with a name.
        /// <para>If this function is called again using the same name, a pointer to pre-allocated memory will be returned and no further allocations will be made.</para>
        /// </summary>
        /// <param name="in_process">The target process to allocate memory in.</param>
        /// <param name="in_name">The name of this allocation.</param>
        /// <param name="in_size">The amount of memory to allocate.</param>
        /// <returns>A pointer in the target process' memory to the allocated memory.</returns>
        /// <exception cref="Win32Exception"/>
        public static nint Alloc(this Process in_process, string in_name, int in_size)
        {
            if (_staticAllocations.TryGetValue(in_name, out var out_result))
                return out_result;

            var result = in_process.Alloc(in_size);

            _staticAllocations.Add(in_name, result);

            return result;
        }

        /// <summary>
        /// Frees memory in the target process.
        /// </summary>
        /// <param name="in_process">The target process to free memory in.</param>
        /// <param name="in_address">The address of the allocated memory to free.</param>
        /// <exception cref="Win32Exception"/>
        public static void Free(this Process in_process, nint in_address)
        {
            if (in_address == 0)
                return;

            var result = Kernel32.VirtualFreeEx(in_process.Handle, in_address, 0, Kernel32.MEM_ALLOCATION_TYPE.MEM_RELEASE);

            if (!result)
                throw new Win32Exception($"Memory release failed ({Marshal.GetLastWin32Error()}).");

            for (int i = 0; i < _staticAllocations.Count; i++)
            {
                var alloc = _staticAllocations.ElementAt(i);

                if (alloc.Value == in_address)
                    _staticAllocations.Remove(alloc.Key);
            }
        }

        /// <summary>
        /// Frees memory in the target process by name.
        /// <para>Used for freeing memory allocated using <see cref="Alloc(Process, string, int)"/>.</para>
        /// </summary>
        /// <param name="in_process">The target process to free memory in.</param>
        /// <param name="in_name">The name of the allocated memory to free.</param>
        /// <exception cref="Win32Exception"/>
        public static void Free(this Process in_process, string in_name)
        {
            if (!_staticAllocations.TryGetValue(in_name, out var out_result))
                return;

            in_process.Free(out_result);

            _staticAllocations.Remove(in_name);
        }

        /// <summary>
        /// Gets the pointer to a thread's local storage.
        /// </summary>
        /// <param name="in_process">The target process the thread is associated with.</param>
        /// <param name="in_thread">The thread to get the pointer to local storage from.</param>
        /// <returns>A pointer in the target process' memory to the thread's local storage.</returns>
        /// <exception cref="Win32Exception"/>
        public static nint GetThreadLocalStoragePointer(this Process in_process, ProcessThread in_thread)
        {
            var handle = Kernel32.OpenThread((int)Kernel32.ThreadAccess.THREAD_ALL_ACCESS, false, (uint)in_thread.Id);

            if (handle == 0)
                throw new Win32Exception($"Failed to open thread {in_thread.Id} ({Marshal.GetLastWin32Error()}).");

            var threadInfo = NtDllHelper.GetThreadInformation(handle);

            handle.Close();

            if (threadInfo == null)
                throw new Win32Exception($"Failed to get thread info ({Marshal.GetLastWin32Error()}).");

            if (threadInfo.TebBaseAddress == 0)
                throw new Win32Exception($"Invalid environment block in thread {in_thread.Id} ({Marshal.GetLastWin32Error()}).");

            return in_process.Read<nint>(in_process.Read<nint>(threadInfo.TebBaseAddress + 0x58));
        }

        /// <summary>
        /// Determines whether a memory location is accessible.
        /// </summary>
        /// <param name="in_process">The target process to read from.</param>
        /// <param name="in_address">The remote address to check.</param>
        /// <returns><c>true</c> if the location is accessible; otherwise <c>false</c>.</returns>
        public static unsafe bool IsAccessible(this Process in_process, nint in_address)
        {
            var buffer = new byte[1];

            fixed (byte* pBuffer = buffer)
                return Kernel32.ReadProcessMemory(in_process.Handle, in_address, (nint)pBuffer, 1, out _);
        }

        /// <summary>
        /// Reads memory from the target process into a buffer.
        /// </summary>
        /// <param name="in_process">The target process to read from.</param>
        /// <param name="in_address">The remote address to read from.</param>
        /// <param name="in_length">The amount of bytes to read.</param>
        /// <returns>A byte array containing the requested memory.</returns>
        /// <exception cref="Win32Exception"/>
        public static unsafe byte[] ReadBytes(this Process in_process, nint in_address, int in_length)
        {
            var result = new byte[in_length];

            if (in_address == 0)
                return [];

            fixed (byte* pBuffer = result)
            {
                if (!Kernel32.ReadProcessMemory(in_process.Handle, in_address, (nint)pBuffer, in_length, out _))
                    throw new Win32Exception($"Failed to read memory at 0x{in_address:X} in process {in_process.Id} ({Marshal.GetLastWin32Error()}).");
            }

            return result;
        }

        /// <summary>
        /// Reads an unmanaged type from the target process' memory.
        /// </summary>
        /// <typeparam name="T">The unmanaged type to read.</typeparam>
        /// <param name="in_process">The target process to read from.</param>
        /// <param name="in_address">The remote address to read from.</param>
        public static T Read<T>(this Process in_process, nint in_address) where T : unmanaged
        {
            var data = in_process.ReadBytes(in_address, Marshal.SizeOf<T>());

            if (data.Length <= 0)
                return default;

            return MemoryHelper.ByteArrayToUnmanagedType<T>(data);
        }

        /// <summary>
        /// Reads an array of unmanaged values from the target process' memory.
        /// </summary>
        /// <typeparam name="T">The unmanaged type to read.</typeparam>
        /// <param name="in_process">The target process to read from.</param>
        /// <param name="in_address">The remote address to read from.</param>
        /// <param name="in_length">The amount of values to read.</param>
        public static T[]? Read<T>(this Process in_process, nint in_address, int in_length) where T : unmanaged
        {
            var size = Marshal.SizeOf<T>();
            var data = in_process.ReadBytes(in_address, in_length * size);

            if (data.Length <= 0)
                return default;

            var result = new T[data.Length / size];

            Buffer.BlockCopy(data, 0, result, 0, data.Length);

            return result;
        }

        /// <summary>
        /// Reads a null-terminated string from the target process' memory.
        /// </summary>
        /// <param name="in_process">The target process to read from.</param>
        /// <param name="in_address">The remote address of the string to read.</param>
        /// <param name="in_encoding">The encoding of the string to read.</param>
        public static string ReadStringNullTerminated(this Process in_process, nint in_address, Encoding? in_encoding = null)
        {
            var data = new List<byte>();
            var encoding = in_encoding ?? Encoding.UTF8;

            var addr = in_address;

            if (encoding == Encoding.Unicode ||
                encoding == Encoding.BigEndianUnicode)
            {
                ushort us;

                while ((us = in_process.Read<ushort>(addr)) != 0)
                {
                    data.Add((byte)(us & 0xFF));
                    data.Add((byte)(us >> 8 & 0xFF));
                    addr += 2;
                }
            }
            else
            {
                byte b;

                while ((b = in_process.Read<byte>(addr)) != 0)
                {
                    data.Add(b);
                    addr++;
                }
            }

            return encoding.GetString(data.ToArray());
        }

        /// <summary>
        /// Writes a buffer into the target process' memory.
        /// </summary>
        /// <param name="in_process">The target process to write to.</param>
        /// <param name="in_address">The remote address to write to.</param>
        /// <param name="in_data">The buffer to write.</param>
        /// <param name="in_isProtected">
        ///     Determines whether the location being written to is protected and should be overridden.
        ///     <para>If the location is not at least <see cref="Kernel32.MEM_PROTECTION.PAGE_READWRITE"/>, this should be set to <c>true</c>.</para>
        ///     <para>When writing bytecode into memory, <see cref="Kernel32.MEM_PROTECTION.PAGE_EXECUTE_READWRITE"/> is required, which should also yield this argument being set to <c>true</c>.</para>
        ///     <para>Please verify the page protection of <paramref name="in_address"/> using an external debugger.</para>
        /// </param>
        /// <param name="in_isPreserved">Determines whether the original code will be preserved so it can be restored using <see cref="MemoryPreserver.RestoreMemory(Process, nint)"/> later.</param>
        /// <exception cref="Win32Exception"/>
        public static void WriteBytes(this Process in_process, nint in_address, byte[] in_data, bool in_isProtected = false, bool in_isPreserved = false)
        {
            var oldProtect = Kernel32.MEM_PROTECTION.PAGE_NOACCESS;

            if (in_isPreserved)
                in_process.PreserveMemory(in_address, in_data.Length);

            if (in_isProtected)
                Kernel32.VirtualProtectEx(in_process.Handle, in_address, in_data.Length, Kernel32.MEM_PROTECTION.PAGE_EXECUTE_READWRITE, out oldProtect);

            if (!Kernel32.WriteProcessMemory(in_process.Handle, in_address, in_data, (uint)in_data.Length, out _))
                throw new Win32Exception($"Failed to write memory at 0x{in_address:X} in process {in_process.Id} ({Marshal.GetLastWin32Error()}).");

            if (in_isProtected)
                Kernel32.VirtualProtectEx(in_process.Handle, in_address, in_data.Length, oldProtect, out _);
        }

        /// <summary>
        /// Writes a buffer into the target process' memory at a new location.
        /// </summary>
        /// <param name="in_process">The target process to write to.</param>
        /// <param name="in_data">The buffer to write.</param>
        /// <returns>A pointer in the target process' memory to the buffer.</returns>
        /// <exception cref="Win32Exception"/>
        public static nint WriteBytes(this Process in_process, byte[] in_data)
        {
            var addr = in_process.Alloc(in_data.Length);

            in_process.WriteBytes(addr, in_data);

            return addr;
        }

        /// <summary>
        /// Writes a buffer into the target process' memory and temporarily changes the page protection to do so.
        /// </summary>
        /// <param name="in_process">The target process to write to.</param>
        /// <param name="in_address">The remote address to write to.</param>
        /// <param name="in_data">The buffer to write.</param>
        /// <param name="in_isPreserved">Determines whether the original code will be preserved so it can be restored using <see cref="MemoryPreserver.RestoreMemory(Process, nint)"/> later.</param>
        /// <exception cref="Win32Exception"/>
        public static void WriteProtectedBytes(this Process in_process, nint in_address, byte[] in_data, bool in_isPreserved = false)
        {
            in_process.WriteBytes(in_address, in_data, true, in_isPreserved);
        }

        /// <summary>
        /// Writes an unmanaged type to the target process' memory.
        /// </summary>
        /// <param name="in_process">The target process to write to.</param>
        /// <param name="in_address">The remote address to write to.</param>
        /// <param name="in_data">The unmanaged value to write.</param>
        /// <param name="in_type">The unmanaged type to write.</param>
        /// <param name="in_isProtected">
        ///     Determines whether the location being written to is protected and should be overridden.
        ///     <para>If the location is not at least <see cref="Kernel32.MEM_PROTECTION.PAGE_READWRITE"/>, this should be set to <c>true</c>.</para>
        ///     <para>When writing bytecode into memory, <see cref="Kernel32.MEM_PROTECTION.PAGE_EXECUTE_READWRITE"/> is required, which should also yield this argument being set to <c>true</c>.</para>
        ///     <para>Please verify the page protection of <paramref name="in_address"/> using an external debugger.</para>
        /// </param>
        /// <param name="in_isPreserved">Determines whether the original code will be preserved so it can be restored using <see cref="MemoryPreserver.RestoreMemory(Process, nint)"/> later.</param>
        /// <exception cref="Win32Exception"/>
        public static void Write(this Process in_process, nint in_address, object in_data, Type in_type, bool in_isProtected = false, bool in_isPreserved = false)
        {
            var data = MemoryHelper.UnmanagedTypeToByteArray(in_data, in_type);

            if (data.Length <= 0)
                return;

            if (in_isProtected)
            {
                in_process.WriteProtectedBytes(in_address, data, in_isPreserved);
            }
            else
            {
                in_process.WriteBytes(in_address, data, in_isPreserved);
            }
        }

        /// <summary>
        /// Writes an unmanaged type to the target process' memory.
        /// </summary>
        /// <typeparam name="T">The unmanaged type to write.</typeparam>
        /// <param name="in_process">The target process to write to.</param>
        /// <param name="in_address">The remote address to write to.</param>
        /// <param name="in_data">The unmanaged value to write.</param>
        /// <param name="in_isProtected">
        ///     Determines whether the location being written to is protected and should be overridden.
        ///     <para>If the location is not at least <see cref="Kernel32.MEM_PROTECTION.PAGE_READWRITE"/>, this should be set to <c>true</c>.</para>
        ///     <para>When writing bytecode into memory, <see cref="Kernel32.MEM_PROTECTION.PAGE_EXECUTE_READWRITE"/> is required, which should also yield this argument being set to <c>true</c>.</para>
        ///     <para>Please verify the page protection of <paramref name="in_address"/> using an external debugger.</para>
        /// </param>
        /// <param name="in_isPreserved">Determines whether the original code will be preserved so it can be restored using <see cref="MemoryPreserver.RestoreMemory(Process, nint)"/> later.</param>
        /// <exception cref="Win32Exception"/>
        public static void Write<T>(this Process in_process, nint in_address, T in_data, bool in_isProtected = false, bool in_isPreserved = false) where T : unmanaged
        {
            in_process.Write(in_address, in_data, in_data.GetType(), in_isProtected, in_isPreserved);
        }

        /// <summary>
        /// Writes an unmanaged type to the target process' memory at a new location.
        /// </summary>
        /// <param name="in_process">The target process to write to.</param>
        /// <param name="in_data">The unmanaged value to write.</param>
        /// <param name="in_type">The unmanaged type to write.</param>
        /// <returns>A pointer in the target process' memory to the value.</returns>
        /// <exception cref="Win32Exception"/>
        public static nint Write(this Process in_process, object in_data, Type in_type)
        {
            return in_process.WriteBytes(MemoryHelper.UnmanagedTypeToByteArray(in_data, in_type));
        }

        /// <summary>
        /// Writes an unmanaged type to the target process' memory at a new location.
        /// </summary>
        /// <typeparam name="T">The unmanaged type to write.</typeparam>
        /// <param name="in_process">The target process to write to.</param>
        /// <param name="in_data">The unmanaged value to write.</param>
        /// <returns>A pointer in the target process' memory to the value.</returns>
        /// <exception cref="Win32Exception"/>
        public static nint Write<T>(this Process in_process, T in_data) where T : unmanaged
        {
            return in_process.Write(in_data, in_data.GetType());
        }

        /// <summary>
        /// Writes an unmanaged type to the target process' memory and temporarily changes the page protection to do so.
        /// </summary>
        /// <param name="in_process">The target process to write to.</param>
        /// <param name="in_address">The remote address to write to.</param>
        /// <param name="in_data">The buffer to write.</param>
        /// <param name="in_isPreserved">Determines whether the original code will be preserved so it can be restored using <see cref="MemoryPreserver.RestoreMemory(Process, nint)"/> later.</param>
        /// <exception cref="Win32Exception"/>
        public static void WriteProtected<T>(this Process in_process, nint in_address, T in_data, bool in_isPreserved = false) where T : unmanaged
        {
            in_process.Write(in_address, in_data, true, in_isPreserved);
        }

        /// <summary>
        /// Writes a null-terminated string to the target process' memory.
        /// </summary>
        /// <param name="in_process">The target process to write to.</param>
        /// <param name="in_address">The remote address to write the string to.</param>
        /// <param name="in_str">The string to write.</param>
        /// <param name="in_encoding">The encoding of the string to write.</param>
        public static void WriteStringNullTerminated(this Process in_process, nint in_address, string in_str, Encoding? in_encoding = null)
        {
            in_process.WriteProtectedBytes(in_address, (in_encoding ?? Encoding.UTF8).GetBytes(in_str + '\0'));
        }

        /// <summary>
        /// Writes a null-terminated string to the target process' memory at a new location.
        /// </summary>
        /// <param name="in_process">The target process to write to.</param>
        /// <param name="in_str">The string to write.</param>
        /// <param name="in_encoding">The encoding of the string to write.</param>
        public static nint WriteStringNullTerminated(this Process in_process, string in_str, Encoding? in_encoding = null)
        {
            var str = (in_encoding ?? Encoding.UTF8).GetBytes(in_str + '\0');
            var addr = in_process.Alloc(str.Length);

            in_process.WriteProtectedBytes(addr, str);

            return addr;
        }
    }
}

#pragma warning restore CA1416 // Validate platform compatibility