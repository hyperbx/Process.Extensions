using ProcessExtensions.Interop;
using ProcessExtensions.Logger;
using System.Diagnostics;

namespace ProcessExtensions.Tests
{
    internal class SignalExitTest : TestBase
    {
        private UnmanagedProcessFunctionPointer? signalExit;

        public SignalExitTest(Process in_process, SymbolResolver in_sr) : base(in_process)
        {
            signalExit = new UnmanagedProcessFunctionPointer(Process, Process.ToASLR(in_sr.GetProcedureAddress("signalExit")))
            {
                IsThrowOnProcessExit = false
            };
        }

        public bool signalExit_ShouldTerminateProcess()
        {
            signalExit!.Invoke();

            LoggerService.Warning("Waiting for process to exit...");

            Thread.Sleep(1000);

            return Process.HasExited;
        }

        public override Func<bool>[] GetTests()
        {
            LoggerService.Warning($"Exit Signal Tests (x{(Process.Is64Bit() ? "64" : "86")}) -------\n");

            return [signalExit_ShouldTerminateProcess];
        }
    }
}
