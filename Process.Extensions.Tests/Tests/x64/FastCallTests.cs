using ProcessExtensions.Interop;
using ProcessExtensions.Interop.Generic;
using ProcessExtensions.Interop.Structures;
using ProcessExtensions.Logger;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ProcessExtensions.Tests.x64
{
    internal class FastCallTests : TestBase
    {
        private UnmanagedProcessFunctionPointer<byte>? fastcallTestNoArguments;
        private UnmanagedProcessFunctionPointer<int, int, int, int>? fastcallTestSumOfArguments;
        private UnmanagedProcessFunctionPointer<UnmanagedPointer, UnmanagedPointer>? fastcallTestReturnStruct;
        private UnmanagedProcessFunctionPointer<UnmanagedPointer>? fastcallTestReturnStructPtr;
        private UnmanagedProcessFunctionPointer<int, UnmanagedPointer>? fastcallTestStructPtrAsArgument;

        public FastCallTests(Process in_process, SymbolResolver in_sr) : base(in_process)
        {
            fastcallTestNoArguments         = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("fastcallTestNoArguments")), CallingConvention.FastCall);
            fastcallTestSumOfArguments      = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("fastcallTestSumOfArguments")), CallingConvention.FastCall);
            fastcallTestReturnStruct        = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("fastcallTestReturnStruct")), CallingConvention.FastCall);
            fastcallTestReturnStructPtr     = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("fastcallTestReturnStructPtr")), CallingConvention.FastCall);
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

        public bool fastcallTestStructPtrAsArgument_ShouldReturnCorrectSum()
        {
            return fastcallTestStructPtrAsArgument!.Invoke(new UnmanagedPointer(Process, new TestContext(1, 2, 3))) == 6;
        }

        public override Func<bool>[] GetTests()
        {
            LoggerService.Warning("__fastcall Tests (x64) --------\n");

            return
            [
                fastcallTestNoArguments_ShouldReturnTrue,
                fastcallTestSumOfArguments_ShouldReturnCorrectSum,
                fastcallTestReturnStruct_ShouldReturnCorrectStruct,
                fastcallTestReturnStructPtr_ShouldReturnCorrectStruct,
                fastcallTestStructPtrAsArgument_ShouldReturnCorrectSum
            ];
        }
    }
}
