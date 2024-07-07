using ProcessExtensions.Enums;
using ProcessExtensions.Extensions;
using ProcessExtensions.Interop;
using ProcessExtensions.Logger;
using System.Diagnostics;
using Vanara.PInvoke;

namespace ProcessExtensions.Tests.Shared
{
    internal class ModuleInjectTest : TestBase
    {
        private const string _modulePath = @"..\..\..\..\Process.Extensions.Tests.Client.DllExport\bin\{0}\" +
#if DEBUG
        @"Debug\" +
#else
        @"Release\" +
#endif
        "Process.Extensions.Tests.Client.DllExport.dll";

        private Kernel32.SafeHINSTANCE? _moduleHandle;

        private UnmanagedProcessFunctionPointer<int, int, int, int> dllexportTestSumOfArguments;

        public ModuleInjectTest(Process in_process) : base(in_process)
        {
            var modulePath = string.Format(_modulePath, Process.Is64Bit() ? "x64" : "Win32");
            var moduleName = Path.GetFileName(modulePath);

            _moduleHandle = in_process.LoadLibrary(Path.GetFullPath(modulePath));

            dllexportTestSumOfArguments = new(in_process, in_process.GetProcedureAddress(moduleName, "dllexportTestSumOfArguments"), ECallingConvention.Cdecl);
        }

        public bool dllexportTestSumOfArguments_ShouldReturnCorrectSum()
        {
            return dllexportTestSumOfArguments.Invoke(1, 2, 3) == 6;
        }

        public override Func<bool>[] GetTests()
        {
            LoggerService.Warning($"DLL Injection Tests ({Process.GetArchitectureName()}) -----------\n");

            return [dllexportTestSumOfArguments_ShouldReturnCorrectSum];
        }

        public override void Dispose()
        {
#if DEBUG
            LoggerService.Warning($"DLL Injection Cleanup ({Process.GetArchitectureName()}) ---------\n");
#endif

            Process?.FreeLibrary(_moduleHandle);
            _moduleHandle?.Close();

            dllexportTestSumOfArguments?.Dispose();

#if DEBUG
            LoggerService.WriteLine();
#endif
        }
    }
}
