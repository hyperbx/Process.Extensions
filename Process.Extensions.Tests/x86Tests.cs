﻿using ProcessExtensions.Interop;
using ProcessExtensions.Logger;
using ProcessExtensions.Tests;
using ProcessExtensions.Tests.x86;
using System.Diagnostics;

namespace ProcessExtensions
{
    internal class x86Tests
    {
        private const string _clientPath = @"..\..\..\..\Process.Extensions.Tests.Client\bin\Win32\" +
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
                LoggerService.Error("x86 client not found, aborting...\n");
                return false;
            }

            var process = Process.Start(_clientPath);
            var result = true;

            using (var sr = new SymbolResolver(_clientPath))
            {
                LoggerService.Warning("Initialising Tests (x86) ------\n");

                TestBase[] tests = [];

                try
                {
                    tests =
                    [
                        new CdeclTests(process, sr),
                        new StdCallTests(process, sr),
                        new FastCallTests(process, sr),
                        new ThisCallTests(process, sr),
                        new AsmHookTest(process, sr),
                        new SignalExitTest(process, sr)
                    ];

                    LoggerService.Utility("PASS\n");
                }
                catch (Exception out_ex)
                {
                    result = false;
                    LoggerService.Error(out_ex.Message);
                    LoggerService.Error("FAIL\n");
                    goto Abort;
                }

                foreach (var test in tests)
                {
                    if (!test.RunTests())
                        result = false;

                    test.Dispose();

                    if (!result)
                        break;
                }
            }

        Abort:
            process.Kill();

            return result;
        }
    }
}
