using System.ComponentModel;
using System.Runtime.InteropServices;
using Vanara.PInvoke;

#pragma warning disable CA1416 // Validate platform compatibility

namespace ProcessExtensions.Interop
{
    internal class SymbolResolver : IDisposable
    {
        private HPROCESS _self = Kernel32.GetCurrentProcess();

        public SymbolResolver(string in_modulePath)
        {
            if (!DbgHelp.SymInitialize(_self, null, false))
                throw new Win32Exception($"Failed to initialise symbol resolver ({Marshal.GetLastWin32Error()}).");

            DbgHelp.SymSetOptions(DbgHelp.SymGetOptions() | DbgHelp.SYMOPT.SYMOPT_LOAD_LINES | DbgHelp.SYMOPT.SYMOPT_UNDNAME);

            var moduleBase = DbgHelp.SymLoadModuleEx(_self, IntPtr.Zero, in_modulePath, null, 0, 0, 0, 0);

            if (moduleBase == 0)
                throw new Win32Exception($"Failed to load module ({Marshal.GetLastWin32Error()}).");
        }

        public nint GetProcedureAddress(string in_procedureName)
        {
            var symbolInfo = new DbgHelp.SYMBOL_INFO
            {
                MaxNameLen = DbgHelp.MAX_SYM_NAME,
                SizeOfStruct = (uint)Marshal.SizeOf(typeof(DbgHelp.SYMBOL_INFO))
            };

            if (DbgHelp.SymFromName(_self, in_procedureName, ref symbolInfo))
            {
                return (nint)symbolInfo.Address;
            }
            else
            {
                throw new Win32Exception($"Failed to resolve symbol \"{in_procedureName}\" ({Marshal.GetLastWin32Error()}).");
            }
        }

        public void Dispose()
        {
            DbgHelp.SymCleanup(_self);
        }
    }
}

#pragma warning restore CA1416 // Validate platform compatibility