using ProcessExtensions.Interop;
using ProcessExtensions.Interop.Generic;
using ProcessExtensions.Interop.Structures;
using ProcessExtensions.Logger;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ProcessExtensions.Tests.x86
{
    internal class FastCallTests : TestBase
    {
        private UnmanagedProcessFunctionPointer<byte>? fastcallTestNoArguments;
        private UnmanagedProcessFunctionPointer<int, int, int, int>? fastcallTestSumOfArguments;
        private UnmanagedProcessFunctionPointer<UnmanagedPointer, UnmanagedPointer>? fastcallTestReturnStruct;
        private UnmanagedProcessFunctionPointer<UnmanagedPointer>? fastcallTestReturnStructPtr;
        private UnmanagedProcessFunctionPointer<int, int, int, int>? fastcallTestStructAsArgument;
        private UnmanagedProcessFunctionPointer<int, UnmanagedPointer>? fastcallTestStructPtrAsArgument;

        public FastCallTests(Process in_process, SymbolResolver in_sr) : base(in_process)
        {
            fastcallTestNoArguments         = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("fastcallTestNoArguments")), CallingConvention.FastCall);
            fastcallTestSumOfArguments      = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("fastcallTestSumOfArguments")), CallingConvention.FastCall);
            fastcallTestReturnStruct        = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("fastcallTestReturnStruct")), CallingConvention.FastCall);
            fastcallTestReturnStructPtr     = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("fastcallTestReturnStructPtr")), CallingConvention.FastCall);
            fastcallTestStructAsArgument    = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("fastcallTestStructAsArgument")), CallingConvention.StdCall);
            fastcallTestStructPtrAsArgument = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("fastcallTestStructPtrAsArgument")), CallingConvention.FastCall);
        }

        public bool fastcallTestNoArguments_ShouldReturnTrue()
        {
            return fastcallTestNoArguments!.Invoke() == 1;
        }

        public bool fastcallTestSumOfArguments_ShouldReturnCorrectSum()
        {
            return fastcallTestSumOfArguments!.Invoke(1, 2, 3) == 6;
        }

        public bool fastcallTestReturnStruct_ShouldReturnCorrectStruct()
        {
            var in_ctx = new UnmanagedPointer(Process, new TestContext(1, 2, 3));
            var out_ctx = fastcallTestReturnStruct!.Invoke(in_ctx);

            var result = new TestContext(1, 2, 3).Equals(out_ctx.Get<TestContext>(Process));

            in_ctx.Free(Process);

            return result;
        }

        public bool fastcallTestReturnStructPtr_ShouldReturnCorrectStruct()
        {
            var ctx = fastcallTestReturnStructPtr!.Invoke();

            return new TestContext(1, 2, 3).Equals(ctx.Get<TestContext>(Process));
        }

        public bool fastcallTestStructAsArgument_ShouldReturnCorrectSum()
        {
            var ctx = new TestContext(1, 2, 3);

            // Compiler optimises original function down to __stdcall.
            return fastcallTestStructAsArgument!.Invoke(ctx.A, ctx.B, ctx.C) == 6;
        }

        public bool fastcallTestStructPtrAsArgument_ShouldReturnCorrectSum()
        {
            var in_ctx = new UnmanagedPointer(Process, new TestContext(1, 2, 3));

            var result = fastcallTestStructPtrAsArgument!.Invoke(in_ctx) == 6;

            in_ctx.Free(Process);

            return result;
        }

        public override Func<bool>[] GetTests()
        {
            LoggerService.Warning("__fastcall Tests (x86) --------\n");

            return
            [
                fastcallTestNoArguments_ShouldReturnTrue,
                fastcallTestSumOfArguments_ShouldReturnCorrectSum,
                fastcallTestReturnStruct_ShouldReturnCorrectStruct,
                fastcallTestReturnStructPtr_ShouldReturnCorrectStruct,
                fastcallTestStructAsArgument_ShouldReturnCorrectSum,
                fastcallTestStructPtrAsArgument_ShouldReturnCorrectSum
            ];
        }

        public override void Dispose()
        {
#if DEBUG
            LoggerService.Warning("__fastcall Cleanup (x86) ------\n");
#endif

            fastcallTestNoArguments?.Dispose();
            fastcallTestSumOfArguments?.Dispose();
            fastcallTestReturnStruct?.Dispose();
            fastcallTestReturnStructPtr?.Dispose();
            fastcallTestStructAsArgument?.Dispose();
            fastcallTestStructPtrAsArgument?.Dispose();

#if DEBUG
            LoggerService.WriteLine();
#endif
        }
    }
}
