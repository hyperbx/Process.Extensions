using ProcessExtensions.Exceptions;
using ProcessExtensions.Helpers.Internal;
using ProcessExtensions.Logger;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Vanara.PInvoke;

#pragma warning disable CA1416 // Validate platform compatibility

namespace ProcessExtensions
{
    public static class Memory
    {
        private static int _staticAllocationsProcessID = 0;
        private static Dictionary<string, nint> _staticAllocations = [];

        /// <summary>
        /// Transforms a virtual address to the process' current base.
        /// <para>This should only be used for processes that use <see href="https://en.wikipedia.org/wiki/Address_space_layout_randomization">Address Space Layout Randomisation (ASLR)</see>.</para>
        /// </summary>
        /// <param name="in_process">The target process to get the base address from.</param>
        /// <param name="in_address">The address to transform.</param>
        public static nint ToASLR(this Process in_process, long in_address)
        {
            if (in_process.HasExited || in_process.MainModule == null)
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
            if (in_process.HasExited || in_process.MainModule == null)
                return 0;

            return (nint)(in_address + (in_process.Is64Bit() ? 0x140000000 : 0x400000) - in_process.MainModule.BaseAddress);
        }

        /// <summary>
        /// Allocates memory in the target process.
        /// </summary>
        /// <param name="in_process">The target process to allocate memory in.</param>
        /// <param name="in_size">The amount of memory to allocate.</param>
        /// <returns>A pointer in the target process' memory to the allocated memory.</returns>
        /// <exception cref="VerboseWin32Exception"/>
        public static nint Alloc(this Process in_process, int in_size)
        {
            if (in_process.HasExited)
                return 0;

            var result = Kernel32.VirtualAllocEx(in_process.Handle, 0, in_size, Kernel32.MEM_ALLOCATION_TYPE.MEM_COMMIT, Kernel32.MEM_PROTECTION.PAGE_EXECUTE_READWRITE);

            if (result == 0)
                throw new VerboseWin32Exception($"Memory allocation failed.");
#if DEBUG
            LoggerService.Utility($"Allocated {in_size} byte{(in_size == 1 ? string.Empty : "s")} at 0x{result:X}.");
#endif
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
        /// <exception cref="VerboseWin32Exception"/>
        public static nint Alloc(this Process in_process, string in_name, int in_size)
        {
            if (_staticAllocationsProcessID != in_process.Id)
                _staticAllocations.Clear();

            _staticAllocationsProcessID = in_process.Id;

            if (in_process.HasExited)
                return 0;

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
        /// <exception cref="VerboseWin32Exception"/>
        public static void Free(this Process in_process, nint in_address)
        {
            if (in_process.HasExited)
                return;

            if (in_address == 0)
                return;

            var moduleBaseStart = in_process.MainModule?.BaseAddress;
            var moduleBaseEnd = moduleBaseStart + in_process.MainModule?.ModuleMemorySize;

            if (in_address > moduleBaseStart && in_address < moduleBaseEnd)
                throw new AccessViolationException("Cannot free main module memory.");

            var result = Kernel32.VirtualFreeEx(in_process.Handle, in_address, 0, Kernel32.MEM_ALLOCATION_TYPE.MEM_RELEASE);

            if (!result)
                throw new VerboseWin32Exception($"Memory release failed.");
#if DEBUG
            LoggerService.Utility($"Freed memory at 0x{in_address:X}.");
#endif
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
        /// <exception cref="VerboseWin32Exception"/>
        public static void Free(this Process in_process, string in_name)
        {
            if (in_process.HasExited)
            {
                _staticAllocations.Clear();
                return;
            }

            if (!_staticAllocations.TryGetValue(in_name, out var out_result))
                return;

            in_process.Free(out_result);

            _staticAllocations.Remove(in_name);
        }

        /// <summary>
        /// Determines whether a memory location is accessible.
        /// </summary>
        /// <param name="in_process">The target process to read from.</param>
        /// <param name="in_address">The remote address to check.</param>
        /// <returns><c>true</c> if the location is accessible; otherwise <c>false</c>.</returns>
        public static unsafe bool IsMemoryAccessible(this Process in_process, nint in_address)
        {
            if (in_process.HasExited)
                return false;

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
        /// <exception cref="VerboseWin32Exception"/>
        public static unsafe byte[] ReadBytes(this Process in_process, nint in_address, int in_length)
        {
            if (in_process.HasExited)
                return [];

            var result = new byte[in_length];

            if (in_address == 0)
                return [];

            fixed (byte* pBuffer = result)
            {
                if (!Kernel32.ReadProcessMemory(in_process.Handle, in_address, (nint)pBuffer, in_length, out _))
                    throw new VerboseWin32Exception($"Failed to read memory at 0x{in_address:X} in process {in_process.Id}.");
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
            if (in_process.HasExited)
                return default;

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
            if (in_process.HasExited)
                return default;

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
            if (in_process.HasExited)
                return string.Empty;

            in_encoding ??= Encoding.UTF8;

            var addr = in_address;

            var buffer = new byte[1024];
            var bufferIndex = 0;

            const int chunkSize = 16;

            while (true)
            {
                var bytes = in_process.ReadBytes(addr, chunkSize);

                for (int i = 0; i < bytes.Length; i++)
                {
                    if (in_encoding == Encoding.Unicode || in_encoding == Encoding.BigEndianUnicode)
                    {
                        if (i + 1 >= bytes.Length)
                            break;

                        ushort us = BitConverter.ToUInt16(bytes, i);

                        if (us == 0)
                            return in_encoding.GetString(buffer, 0, bufferIndex);

                        if (in_encoding == Encoding.BigEndianUnicode)
                        {
                            buffer[bufferIndex++] = (byte)(us >> 8 & 0xFF);
                            buffer[bufferIndex++] = (byte)(us & 0xFF);
                        }
                        else
                        {
                            buffer[bufferIndex++] = (byte)(us & 0xFF);
                            buffer[bufferIndex++] = (byte)(us >> 8 & 0xFF);
                        }

                        i++;
                    }
                    else
                    {
                        if (bytes[i] == 0)
                            return in_encoding.GetString(buffer, 0, bufferIndex);

                        buffer[bufferIndex++] = bytes[i];
                    }

                    if (bufferIndex >= buffer.Length - chunkSize)
                        Array.Resize(ref buffer, buffer.Length * 2);
                }

                addr += chunkSize;
            }
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
        /// <param name="in_isPreserved">Determines whether the original code will be preserved so it can be restored using <see cref="MemoryPreserver.RestoreMemory(Process, nint, bool)"/> later.</param>
        /// <exception cref="VerboseWin32Exception"/>
        public static void WriteBytes(this Process in_process, nint in_address, byte[] in_data, bool in_isProtected = false, bool in_isPreserved = false)
        {
            if (in_process.HasExited)
                return;

            var oldProtect = Kernel32.MEM_PROTECTION.PAGE_NOACCESS;

            if (in_isPreserved)
                in_process.PreserveMemory(in_address, in_data.Length);

            if (in_isProtected)
                Kernel32.VirtualProtectEx(in_process.Handle, in_address, in_data.Length, Kernel32.MEM_PROTECTION.PAGE_EXECUTE_READWRITE, out oldProtect);

            if (!Kernel32.WriteProcessMemory(in_process.Handle, in_address, in_data, (uint)in_data.Length, out _))
                throw new VerboseWin32Exception($"Failed to write memory at 0x{in_address:X} in process {in_process.Id}.");

            if (in_isProtected)
                Kernel32.VirtualProtectEx(in_process.Handle, in_address, in_data.Length, oldProtect, out _);
        }

        /// <summary>
        /// Writes a buffer into the target process' memory at a new location.
        /// </summary>
        /// <param name="in_process">The target process to write to.</param>
        /// <param name="in_data">The buffer to write.</param>
        /// <returns>A pointer in the target process' memory to the buffer.</returns>
        /// <exception cref="VerboseWin32Exception"/>
        public static nint WriteBytes(this Process in_process, byte[] in_data)
        {
            if (in_process.HasExited)
                return 0;

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
        /// <param name="in_isPreserved">Determines whether the original code will be preserved so it can be restored using <see cref="MemoryPreserver.RestoreMemory(Process, nint, bool)"/> later.</param>
        /// <exception cref="VerboseWin32Exception"/>
        public static void WriteProtectedBytes(this Process in_process, nint in_address, byte[] in_data, bool in_isPreserved = false)
        {
            in_process.WriteBytes(in_address, in_data, true, in_isPreserved);
        }

        /// <summary>
        /// Writes an object to the target process' memory.
        /// </summary>
        /// <param name="in_process">The target process to write to.</param>
        /// <param name="in_address">The remote address to write to.</param>
        /// <param name="in_data">The object to write.</param>
        /// <param name="in_isProtected">
        ///     Determines whether the location being written to is protected and should be overridden.
        ///     <para>If the location is not at least <see cref="Kernel32.MEM_PROTECTION.PAGE_READWRITE"/>, this should be set to <c>true</c>.</para>
        ///     <para>When writing bytecode into memory, <see cref="Kernel32.MEM_PROTECTION.PAGE_EXECUTE_READWRITE"/> is required, which should also yield this argument being set to <c>true</c>.</para>
        ///     <para>Please verify the page protection of <paramref name="in_address"/> using an external debugger.</para>
        /// </param>
        /// <param name="in_isPreserved">Determines whether the original code will be preserved so it can be restored using <see cref="MemoryPreserver.RestoreMemory(Process, nint, bool)"/> later.</param>
        /// <exception cref="VerboseWin32Exception"/>
        public static void Write(this Process in_process, nint in_address, object in_data, bool in_isProtected = false, bool in_isPreserved = false)
        {
            if (in_process.HasExited)
                return;

            var data = MemoryHelper.UnmanagedTypeToByteArray(in_data);

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
        /// <param name="in_isPreserved">Determines whether the original code will be preserved so it can be restored using <see cref="MemoryPreserver.RestoreMemory(Process, nint, bool)"/> later.</param>
        /// <exception cref="VerboseWin32Exception"/>
        public static void Write<T>(this Process in_process, nint in_address, T in_data, bool in_isProtected = false, bool in_isPreserved = false) where T : unmanaged
        {
            if (in_process.HasExited)
                return;

            in_process.Write(in_address, (object)in_data, in_isProtected, in_isPreserved);
        }

        /// <summary>
        /// Writes an object to the target process' memory at a new location.
        /// </summary>
        /// <param name="in_process">The target process to write to.</param>
        /// <param name="in_data">The object to write.</param>
        /// <returns>A pointer in the target process' memory to the value.</returns>
        /// <exception cref="VerboseWin32Exception"/>
        public static nint Write(this Process in_process, object in_data)
        {
            if (in_process.HasExited)
                return 0;

            return in_process.WriteBytes(MemoryHelper.UnmanagedTypeToByteArray(in_data));
        }

        /// <summary>
        /// Writes an unmanaged type to the target process' memory at a new location.
        /// </summary>
        /// <typeparam name="T">The unmanaged type to write.</typeparam>
        /// <param name="in_process">The target process to write to.</param>
        /// <param name="in_data">The unmanaged value to write.</param>
        /// <returns>A pointer in the target process' memory to the value.</returns>
        /// <exception cref="VerboseWin32Exception"/>
        public static nint Write<T>(this Process in_process, T in_data) where T : unmanaged
        {
            if (in_process.HasExited)
                return 0;

            return in_process.Write((object)in_data);
        }

        /// <summary>
        /// Writes an array of unmanaged types to the target process' memory at a new location.
        /// </summary>
        /// <param name="in_process">The target process to write to.</param>
        /// <param name="in_data">The unmanaged values to write.</param>
        /// <returns>A pointer in the target process' memory to the array.</returns>
        /// <exception cref="VerboseWin32Exception"/>
        public static nint Write<T>(this Process in_process, T[] in_data) where T : unmanaged
        {
            if (in_process.HasExited || in_data == null)
                return 0;

            var size = Marshal.SizeOf<T>();
            var result = in_process.Alloc(size * in_data.Length);

            for (int i = 0; i < in_data.Length; i++)
                in_process.Write(result + (i * size), in_data[i]);

            return result;
        }

        /// <summary>
        /// Writes an unmanaged type to the target process' memory and temporarily changes the page protection to do so.
        /// </summary>
        /// <param name="in_process">The target process to write to.</param>
        /// <param name="in_address">The remote address to write to.</param>
        /// <param name="in_data">The buffer to write.</param>
        /// <param name="in_isPreserved">Determines whether the original code will be preserved so it can be restored using <see cref="MemoryPreserver.RestoreMemory(Process, nint, bool)"/> later.</param>
        /// <exception cref="VerboseWin32Exception"/>
        public static void WriteProtected<T>(this Process in_process, nint in_address, T in_data, bool in_isPreserved = false) where T : unmanaged
        {
            if (in_process.HasExited)
                return;

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
            if (in_process.HasExited)
                return;

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
            if (in_process.HasExited)
                return 0;

            var str = (in_encoding ?? Encoding.UTF8).GetBytes(in_str + '\0');
            var addr = in_process.Alloc(str.Length);

            in_process.WriteProtectedBytes(addr, str);

            return addr;
        }
    }
}

#pragma warning restore CA1416 // Validate platform compatibility