using ProcessExtensions.Exceptions;
using ProcessExtensions.Helpers.Internal;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Vanara.PInvoke;

#pragma warning disable CA1416 // Validate platform compatibility

namespace ProcessExtensions
{
    public static class Kernel
    {
        private static int _procedureCacheProcessID = 0;
        private static Dictionary<string, Dictionary<string, nint>> _procedureCache = [];

        /// <summary>
        /// Gets the address of a named export function.
        /// </summary>
        /// <param name="in_process">The target process the export function is located in.</param>
        /// <param name="in_moduleName">The name of the module the export function comes from.</param>
        /// <param name="in_procedureName">The name of the export function.</param>
        /// <returns>A pointer in the target process' memory to the requested function.</returns>
        /// <exception cref="NotSupportedException"/>
        /// <exception cref="IndexOutOfRangeException"/>
        public static unsafe nint GetProcedureAddress(this Process in_process, string in_moduleName, string in_procedureName)
        {
            if (_procedureCacheProcessID != in_process.Id)
                _procedureCache.Clear();

            _procedureCacheProcessID = in_process.Id;

            if (in_process.HasExited)
                return 0;

            ArgumentException.ThrowIfNullOrEmpty(in_moduleName);
            ArgumentException.ThrowIfNullOrEmpty(in_procedureName);

            in_moduleName = in_moduleName.ToLower();

            if (in_moduleName.EndsWith(".exe") || in_moduleName.EndsWith(".dll"))
                in_moduleName = in_moduleName[..in_moduleName.LastIndexOf('.')];

            // Retrieve procedure address from cache.
            if (_procedureCache.TryGetValue(in_moduleName, out var out_moduleProcs) &&
                out_moduleProcs.TryGetValue(in_procedureName, out var out_procPtr))
            {
                return out_procPtr;
            }

            in_process.Refresh();

            foreach (ProcessModule module in in_process.Modules)
            {
                var name = module.ModuleName[..module.ModuleName.LastIndexOf('.')].ToLower();

                if (name != in_moduleName)
                    continue;

                var optionalHeaderAddr = module.BaseAddress + in_process.Read<uint>(module.BaseAddress + 0x3C) + 0x18;
                var ntHeaderMagic = in_process.Read<ushort>((nint)optionalHeaderAddr);

                if (ntHeaderMagic != NtDllHelper.IMAGE_NT_OPTIONAL_HDR32_MAGIC &&
                    ntHeaderMagic != NtDllHelper.IMAGE_NT_OPTIONAL_HDR64_MAGIC)
                {
                    throw new NotSupportedException("Invalid NT header.");
                }

                var exportTableHeaderOffset = ntHeaderMagic == NtDllHelper.IMAGE_NT_OPTIONAL_HDR32_MAGIC
                    ? 0x60
                    : 0x70;

                var exportDirAddr = module.BaseAddress +
                    in_process.Read<NtDllHelper.IMAGE_DATA_DIRECTORY>((nint)(optionalHeaderAddr + exportTableHeaderOffset)).VirtualAddress;

                var exportDir = in_process.Read<NtDllHelper.IMAGE_EXPORT_DIRECTORY>(exportDirAddr);
                var exportNames = in_process.Read<uint>((nint)(module.BaseAddress + exportDir.AddressOfNames), (int)exportDir.NumberOfNames);

                if (exportNames == null || exportNames.Length <= 0)
                    throw new VerboseWin32Exception("Failed to read export table.");

                for (int i = 0; i < exportNames.Length; i++)
                {
                    var exportName = in_process.ReadStringNullTerminated((nint)(module.BaseAddress + exportNames[i]));

                    if (exportName != in_procedureName)
                        continue;

                    var procOrdinal = in_process.Read<ushort>((nint)(module.BaseAddress + exportDir.AddressOfNameOrdinals + (sizeof(ushort) * i)));
                    var procAddress = module.BaseAddress + in_process.Read<uint>((nint)(module.BaseAddress + exportDir.AddressOfFunctions + (sizeof(uint) * procOrdinal)));

                    var memoryInfo = new Kernel32.MEMORY_BASIC_INFORMATION();

                    Kernel32.VirtualQueryEx(in_process, (nint)procAddress, (nint)(&memoryInfo), Marshal.SizeOf<Kernel32.MEMORY_BASIC_INFORMATION>());

                    if ((memoryInfo.Protect & (uint)(Kernel32.MEM_PROTECTION.PAGE_EXECUTE |
                        Kernel32.MEM_PROTECTION.PAGE_EXECUTE_READ                         |
                        Kernel32.MEM_PROTECTION.PAGE_EXECUTE_READWRITE                    |
                        Kernel32.MEM_PROTECTION.PAGE_EXECUTE_WRITECOPY)) == 0)
                    {
                        var redirect = in_process.ReadStringNullTerminated((nint)procAddress).Split('.', StringSplitOptions.RemoveEmptyEntries);

                        if (redirect.Length < 2)
                            throw new IndexOutOfRangeException($"Invalid redirect. Expected length: 2. Received length: {redirect.Length}.");

                        return GetProcedureAddress(in_process, redirect[0], redirect[1]);
                    }

                    if (_procedureCache.ContainsKey(in_moduleName))
                    {
                        _procedureCache[in_moduleName].Add(in_procedureName, (nint)procAddress);
                    }
                    else
                    {
                        _procedureCache.Add(in_moduleName, new() { { in_procedureName, (nint)procAddress } });
                    }

                    return (nint)procAddress;
                }
            }

            return 0;
        }

        /// <summary>
        /// Inherits a handle.
        /// </summary>
        /// <param name="in_process">The target process to duplicate the handle into.</param>
        /// <param name="in_handle">The handle to duplicate.</param>
        /// <returns>The value of the handle in the target process.</returns>
        /// <exception cref="VerboseWin32Exception"/>
        public static nint InheritHandle(this Process in_process, nint in_handle)
        {
            if (in_process.HasExited)
                return 0;

            if (!Kernel32.DuplicateHandle(Kernel32.GetCurrentProcess(), in_handle, in_process.Handle, out var out_handle, 0, false, Kernel32.DUPLICATE_HANDLE_OPTIONS.DUPLICATE_SAME_ACCESS))
                throw new VerboseWin32Exception($"Failed to duplicate handle.");

            return out_handle;
        }

        /// <summary>
        /// Inherits a handle.
        /// </summary>
        /// <param name="in_process">The target process to duplicate the handle into.</param>
        /// <param name="in_process">The source process to duplicate the handle from.</param>
        /// <param name="in_handle">The handle to duplicate.</param>
        /// <returns>The value of the handle in the target process.</returns>
        /// <exception cref="VerboseWin32Exception"/>
        public static nint InheritHandle(this Process in_process, Process in_sourceProcess, nint in_handle)
        {
            if (in_process.HasExited)
                return 0;

            if (!Kernel32.DuplicateHandle(in_sourceProcess.Handle, in_handle, in_process.Handle, out var out_handle, 0, false, Kernel32.DUPLICATE_HANDLE_OPTIONS.DUPLICATE_SAME_ACCESS))
                throw new VerboseWin32Exception($"Failed to duplicate handle.");

            return out_handle;
        }

        /// <summary>
        /// Determines whether the target process is 64-bit or a WoW64 process.
        /// </summary>
        /// <param name="in_process">The target process to check.</param>
        /// <returns><c>true</c> if the target process is 64-bit; otherwise, <c>false</c>.</returns>
        /// <exception cref="VerboseWin32Exception"/>
        public static bool Is64Bit(this Process in_process)
        {
            if (in_process.HasExited)
                return false;

            if (!Kernel32.IsWow64Process(in_process.Handle, out var isWoW64))
                throw new VerboseWin32Exception($"Failed to determine process architecture.");

            return !isWoW64;
        }
    }
}

#pragma warning restore CA1416 // Validate platform compatibility