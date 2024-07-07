using ProcessExtensions.Interop;
using ProcessExtensions.Logger;
using ProcessExtensions.Tests;
using ProcessExtensions.Tests.Shared;
using ProcessExtensions.Tests.x64;
using System.Diagnostics;

namespace ProcessExtensions
{
    internal class x64Tests
    {
        private const string _clientPath = @"..\..\..\..\Process.Extensions.Tests.Client\bin\x64\" +
#if DEBUG
        @"Debug\" +
#else
        @"Release\" +
#endif
        "Process.Extensions.Tests.Client.exe";

        public static bool RunTests()
        {
            if (!File.Exists(_clientPath))
            {
                LoggerService.Error("x64 client not found, aborting...\n");
                return false;
            }

            var process = Process.Start(_clientPath);
            var result = true;

            using (var sr = new SymbolResolver(_clientPath))
            {
                LoggerService.Warning("Initialising Tests (x64) ------------\n");

                TestBase[] tests = [];
#if !DEBUG
                try
                {
#endif
                    tests =
                    [
                        new FastCallTests(process, sr),
                        new AsmHookTest(process, sr),
                        new ModuleInjectTest(process),
                        new SignalExitTest(process, sr)
                    ];
#if !DEBUG
                    LoggerService.Utility("PASS\n");
                }
                catch (Exception out_ex)
                {
                    result = false;
                    LoggerService.Error(out_ex.Message);
                    LoggerService.Error("FAIL\n");
                    goto Abort;
                }
#else
                LoggerService.WriteLine();
#endif
                foreach (var test in tests)
                {
                    if (!test.RunTests())
                        result = false;

                    test.Dispose();

                    if (!result)
                        break;
                }
            }
#if !DEBUG
        Abort:
#endif
            process.Kill();

            return result;
        }
    }
}
