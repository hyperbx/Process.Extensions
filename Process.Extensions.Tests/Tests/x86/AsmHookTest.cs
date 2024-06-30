using ProcessExtensions.Interop;
using ProcessExtensions.Logger;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ProcessExtensions.Tests.x86
{
    internal class AsmHookTest : TestBase
    {
        private nint _fastcallTestSumOfArgumentsAddr;

        private UnmanagedProcessFunctionPointer<int, int, int, int>? fastcallTestSumOfArguments;

        public AsmHookTest(Process in_process, SymbolResolver in_sr) : base(in_process)
        {
            _fastcallTestSumOfArgumentsAddr = Process.ToASLR(in_sr.GetProcedureAddress("fastcallTestSumOfArguments"));

            fastcallTestSumOfArguments = new(Process, _fastcallTestSumOfArgumentsAddr, CallingConvention.FastCall);
        }

        public bool fastcallTestSumOfArguments_ShouldReturnCorrectSubtraction()
        {
#if DEBUG
            Process.WriteAsmHook
            (
                $@"
                    sub eax, [ebp - 0x14]
                    sub eax, [ebp + 0x08]
                ",

                Process.ScanSignature("\x03\x45\xEC\x03\x45\x08", "xxxxxx", _fastcallTestSumOfArgumentsAddr)
            );
#else
            Process.WriteAsmHook
            (
                $@"
                    sub eax, [ebp - 0x08]
                    sub eax, [ebp + 0x08]
                ",

                Process.ScanSignature("\x03\x45\xF8\x03\x45\x08", "xxxxxx", _fastcallTestSumOfArgumentsAddr)
            );
#endif

            return fastcallTestSumOfArguments!.Invoke(1, 2, 3) == -4;
        }

        public override Func<bool>[] GetTests()
        {
            LoggerService.Warning("Mid-ASM Hook Tests (x86) ------\n");

            return [fastcallTestSumOfArguments_ShouldReturnCorrectSubtraction];
        }
    }
}
