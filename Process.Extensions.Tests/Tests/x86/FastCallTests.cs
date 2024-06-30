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
            var result = fastcallTestReturnStruct!.Invoke(new UnmanagedPointer(Process, new TestContext(1, 2, 3)));

            return new TestContext(1, 2, 3).Equals(result.Get<TestContext>(Process));
        }

        public bool fastcallTestReturnStructPtr_ShouldReturnCorrectStruct()
        {
            var result = fastcallTestReturnStructPtr!.Invoke();

            return new TestContext(1, 2, 3).Equals(result.Get<TestContext>(Process));
        }

        public bool fastcallTestStructAsArgument_ShouldReturnCorrectSum()
        {
            var ctx = new TestContext(1, 2, 3);

            // Compiler optimises original function down to __stdcall.
            return fastcallTestStructAsArgument!.Invoke(ctx.A, ctx.B, ctx.C) == 6;
        }

        public bool fastcallTestStructPtrAsArgument_ShouldReturnCorrectSum()
        {
            var pCtx = Process.Write(new TestContext(1, 2, 3));

            return fastcallTestStructPtrAsArgument!.Invoke(pCtx) == 6;
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
    }
}
