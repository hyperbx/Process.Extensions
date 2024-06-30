using ProcessExtensions.Interop;
using ProcessExtensions.Logger;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ProcessExtensions.Tests.x64
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
            _fastcallTestSumOfArgumentsAddr = Process.ScanSignature("\x8B\x44\x24\x10\x8B\x4C\x24\x08\x03\xC8\x8B\xC1\x03\x44\x24\x18", "xxxxxxxxxxxxxxxx", _fastcallTestSumOfArgumentsAddr);

            Process.WriteAsmHook
            (
                $@"
                    mov eax, [rsp + 0x10]
                    mov ecx, [rsp + 0x08]
                    sub ecx, eax
                    mov eax, ecx
                    sub eax, [rsp + 0x18]
                ",

                _fastcallTestSumOfArgumentsAddr
            );

            return fastcallTestSumOfArguments!.Invoke(1, 2, 3) == -4;
        }

        public override Func<bool>[] GetTests()
        {
            LoggerService.Warning("Mid-ASM Hook Tests (x64) ------\n");

            return [fastcallTestSumOfArguments_ShouldReturnCorrectSubtraction];
        }

        public override void Dispose()
        {
#if DEBUG
            LoggerService.Warning("Mid-ASM Hook Cleanup (x64) ----\n");
#endif

            Process.RemoveAsmHook(_fastcallTestSumOfArgumentsAddr);

            fastcallTestSumOfArguments?.Dispose();

#if DEBUG
            LoggerService.WriteLine();
#endif
        }
    }
}
