using ProcessExtensions.Enums;
using ProcessExtensions.Interop;
using ProcessExtensions.Interop.Generic;
using ProcessExtensions.Interop.Structures;
using ProcessExtensions.Logger;
using System.Diagnostics;

namespace ProcessExtensions.Tests.x86
{
    internal class StdCallTests : TestBase
    {
        private UnmanagedProcessFunctionPointer<byte>? stdcallTestNoArguments;
        private UnmanagedProcessFunctionPointer<int, int, int, int>? stdcallTestSumOfArguments;
        private UnmanagedProcessFunctionPointer<UnmanagedPointer<TestContext>, UnmanagedPointer<TestContext>>? stdcallTestReturnStruct;
        private UnmanagedProcessFunctionPointer<UnmanagedPointer<TestContext>>? stdcallTestReturnStructPtr;
        private UnmanagedProcessFunctionPointer<int, TestContext>? stdcallTestStructAsArgument;
        private UnmanagedProcessFunctionPointer<int, TestContext, TestContext, TestContext>? stdcallTestStructsAsArguments;
        private UnmanagedProcessFunctionPointer<int, UnmanagedPointer<TestContext>>? stdcallTestStructPtrAsArgument;
        private UnmanagedProcessFunctionPointer<int, UnmanagedPointer<TestContext>, UnmanagedPointer<TestContext>, UnmanagedPointer<TestContext>>? stdcallTestStructPtrsAsArguments;

        public StdCallTests(Process in_process, SymbolResolver in_sr) : base(in_process)
        {
            stdcallTestNoArguments = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("stdcallTestNoArguments")), ECallingConvention.StdCall);
            stdcallTestSumOfArguments = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("stdcallTestSumOfArguments")), ECallingConvention.StdCall);
            stdcallTestReturnStruct = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("stdcallTestReturnStruct")), ECallingConvention.StdCall);
            stdcallTestReturnStructPtr = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("stdcallTestReturnStructPtr")), ECallingConvention.StdCall);
            stdcallTestStructAsArgument = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("stdcallTestStructAsArgument")), ECallingConvention.StdCall);
            stdcallTestStructsAsArguments = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("stdcallTestStructsAsArguments")), ECallingConvention.StdCall);
            stdcallTestStructPtrAsArgument = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("stdcallTestStructPtrAsArgument")), ECallingConvention.StdCall);
            stdcallTestStructPtrsAsArguments = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("stdcallTestStructPtrsAsArguments")), ECallingConvention.StdCall);
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
            var in_ctx = new UnmanagedPointer<TestContext>(Process, new(1, 2, 3));
            var out_ctx = stdcallTestReturnStruct!.Invoke(in_ctx);

            var result = new TestContext(1, 2, 3).Equals(out_ctx.Get(Process));

            in_ctx.Free(Process);

            return result;
        }

        public bool stdcallTestReturnStructPtr_ShouldReturnCorrectStruct()
        {
            var ctx = stdcallTestReturnStructPtr!.Invoke();

            return new TestContext(1, 2, 3).Equals(ctx.Get(Process));
        }

        public bool stdcallTestStructAsArgument_ShouldReturnCorrectSum()
        {
            return stdcallTestStructAsArgument!.Invoke(new TestContext(1, 2, 3)) == 6;
        }

        public bool stdcallTestStructsAsArguments_ShouldReturnCorrectSum()
        {
            return stdcallTestStructsAsArguments!.Invoke(new TestContext(1, 2, 3), new TestContext(1, 2, 3), new TestContext(1, 2, 3)) == 18;
        }

        public bool stdcallTestStructPtrAsArgument_ShouldReturnCorrectSum()
        {
            var ctx = new UnmanagedPointer<TestContext>(Process, new(1, 2, 3));
            var result = stdcallTestStructPtrAsArgument!.Invoke(ctx) == 6;

            ctx.Free(Process);

            return result;
        }

        public bool stdcallTestStructPtrsAsArguments_ShouldReturnCorrectSum()
        {
            var ctx = new UnmanagedPointer<TestContext>(Process, new(1, 2, 3));
            var result = stdcallTestStructPtrsAsArguments!.Invoke(ctx, ctx, ctx) == 18;

            ctx.Free(Process);

            return result;
        }

        public override Func<bool>[] GetTests()
        {
            LoggerService.Warning("__stdcall Tests (x86) ---------------\n");

            return
            [
                stdcallTestNoArguments_ShouldReturnTrue,
                stdcallTestSumOfArguments_ShouldReturnCorrectSum,
                stdcallTestReturnStruct_ShouldReturnCorrectStruct,
                stdcallTestReturnStructPtr_ShouldReturnCorrectStruct,
                stdcallTestStructAsArgument_ShouldReturnCorrectSum,
                stdcallTestStructsAsArguments_ShouldReturnCorrectSum,
                stdcallTestStructPtrAsArgument_ShouldReturnCorrectSum,
                stdcallTestStructPtrsAsArguments_ShouldReturnCorrectSum
            ];
        }

        public override void Dispose()
        {
#if DEBUG
            LoggerService.Warning("__stdcall Cleanup (x86) -------------\n");
#endif

            stdcallTestNoArguments?.Dispose();
            stdcallTestSumOfArguments?.Dispose();
            stdcallTestReturnStruct?.Dispose();
            stdcallTestReturnStructPtr?.Dispose();
            stdcallTestStructAsArgument?.Dispose();
            stdcallTestStructsAsArguments?.Dispose();
            stdcallTestStructPtrAsArgument?.Dispose();
            stdcallTestStructPtrsAsArguments?.Dispose();

#if DEBUG
            LoggerService.WriteLine();
#endif
        }
    }
}
