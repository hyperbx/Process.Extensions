using ProcessExtensions.Interop;
using ProcessExtensions.Interop.Generic;
using ProcessExtensions.Interop.Structures;
using ProcessExtensions.Logger;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ProcessExtensions.Tests.x86
{
    internal class StdCallTests : TestBase
    {
        private UnmanagedProcessFunctionPointer<byte>? stdcallTestNoArguments;
        private UnmanagedProcessFunctionPointer<int, int, int, int>? stdcallTestSumOfArguments;
        private UnmanagedProcessFunctionPointer<UnmanagedPointer, UnmanagedPointer>? stdcallTestReturnStruct;
        private UnmanagedProcessFunctionPointer<UnmanagedPointer>? stdcallTestReturnStructPtr;
        private UnmanagedProcessFunctionPointer<int, TestContext>? stdcallTestStructAsArgument;
        private UnmanagedProcessFunctionPointer<int, UnmanagedPointer>? stdcallTestStructPtrAsArgument;

        public StdCallTests(Process in_process, SymbolResolver in_sr) : base(in_process)
        {
            stdcallTestNoArguments         = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("stdcallTestNoArguments")), CallingConvention.StdCall);
            stdcallTestSumOfArguments      = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("stdcallTestSumOfArguments")), CallingConvention.StdCall);
            stdcallTestReturnStruct        = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("stdcallTestReturnStruct")), CallingConvention.StdCall);
            stdcallTestReturnStructPtr     = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("stdcallTestReturnStructPtr")), CallingConvention.StdCall);
            stdcallTestStructAsArgument    = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("stdcallTestStructAsArgument")), CallingConvention.StdCall);
            stdcallTestStructPtrAsArgument = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("stdcallTestStructPtrAsArgument")), CallingConvention.StdCall);
        }

        public bool stdcallTestNoArguments_ShouldReturnTrue()
        {
            return stdcallTestNoArguments!.Invoke() == 1;
        }

        public bool stdcallTestSumOfArguments_ShouldReturnCorrectSum()
        {
            return stdcallTestSumOfArguments!.Invoke(1, 2, 3) == 6;
        }

        public bool stdcallTestReturnStruct_ShouldReturnCorrectStruct()
        {
            var result = stdcallTestReturnStruct!.Invoke(new UnmanagedPointer(Process, new TestContext(1, 2, 3)));

            return new TestContext(1, 2, 3).Equals(result.Get<TestContext>(Process));
        }

        public bool stdcallTestReturnStructPtr_ShouldReturnCorrectStruct()
        {
            var result = stdcallTestReturnStructPtr!.Invoke();

            return new TestContext(1, 2, 3).Equals(result.Get<TestContext>(Process));
        }

        public bool stdcallTestStructAsArgument_ShouldReturnCorrectSum()
        {
            return stdcallTestStructAsArgument!.Invoke(new TestContext(1, 2, 3)) == 6;
        }

        public bool stdcallTestStructPtrAsArgument_ShouldReturnCorrectSum()
        {
            var pCtx = Process.Write(new TestContext(1, 2, 3));

            return stdcallTestStructPtrAsArgument!.Invoke(pCtx) == 6;
        }

        public override Func<bool>[] GetTests()
        {
            LoggerService.Warning("__stdcall Tests (x86) ---------\n");

            return
            [
                stdcallTestNoArguments_ShouldReturnTrue,
                stdcallTestSumOfArguments_ShouldReturnCorrectSum,
                stdcallTestReturnStruct_ShouldReturnCorrectStruct,
                stdcallTestReturnStructPtr_ShouldReturnCorrectStruct,
                stdcallTestStructAsArgument_ShouldReturnCorrectSum,
                stdcallTestStructPtrAsArgument_ShouldReturnCorrectSum
            ];
        }
    }
}
