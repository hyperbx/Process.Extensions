using ProcessExtensions.Enums;
using ProcessExtensions.Interop;
using ProcessExtensions.Interop.Generic;
using ProcessExtensions.Interop.Structures;
using ProcessExtensions.Logger;
using System.Diagnostics;

namespace ProcessExtensions.Tests.x86
{
    internal class ThisCallTests : TestBase
    {
        private UnmanagedPointer? _this;

        private UnmanagedProcessFunctionPointer<byte, UnmanagedPointer>? thiscallTestNoArguments;
        private UnmanagedProcessFunctionPointer<int, UnmanagedPointer>? thiscallTestSumOfFields;
        private UnmanagedProcessFunctionPointer<int, UnmanagedPointer, int, int, int>? thiscallTestSumOfFieldsAndArguments;
        private UnmanagedProcessFunctionPointer<int, UnmanagedPointer, int, int, int>? thiscallTestSumOfFieldsAndArgumentsNested;
        private UnmanagedProcessFunctionPointer<UnmanagedPointer, UnmanagedPointer>? thiscallTestReturnStruct;
        private UnmanagedProcessFunctionPointer<int, UnmanagedPointer, TestContext>? thiscallTestStructAsArgument;
        private UnmanagedProcessFunctionPointer<int, UnmanagedPointer, TestContext, TestContext, TestContext>? thiscallTestStructsAsArguments;
        private UnmanagedProcessFunctionPointer<int, UnmanagedPointer, UnmanagedPointer>? thiscallTestStructPtrAsArgument;
        private UnmanagedProcessFunctionPointer<int, UnmanagedPointer, UnmanagedPointer, UnmanagedPointer, UnmanagedPointer>? thiscallTestStructPtrsAsArguments;

        public ThisCallTests(Process in_process, SymbolResolver in_sr) : base(in_process)
        {
            _this = new UnmanagedPointer(Process, new TestContext(1, 2, 3));

            thiscallTestNoArguments = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("testContext::thiscallTestNoArguments")), ECallingConvention.ThisCall);
            thiscallTestSumOfFields = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("testContext::thiscallTestSumOfFields")), ECallingConvention.ThisCall);
            thiscallTestSumOfFieldsAndArguments = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("testContext::thiscallTestSumOfFieldsAndArguments")), ECallingConvention.ThisCall);
            thiscallTestSumOfFieldsAndArgumentsNested = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("testContext::thiscallTestSumOfFieldsAndArgumentsNested")), ECallingConvention.ThisCall);
            thiscallTestReturnStruct = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("testContext::thiscallTestReturnStruct")), ECallingConvention.ThisCall);
            thiscallTestStructAsArgument = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("testContext::thiscallTestStructAsArgument")), ECallingConvention.ThisCall);
            thiscallTestStructsAsArguments = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("testContext::thiscallTestStructsAsArguments")), ECallingConvention.ThisCall);
            thiscallTestStructPtrAsArgument = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("testContext::thiscallTestStructPtrAsArgument")), ECallingConvention.ThisCall);
            thiscallTestStructPtrsAsArguments = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("testContext::thiscallTestStructPtrsAsArguments")), ECallingConvention.ThisCall);
        }

        public bool thiscallTestNoArguments_ShouldReturnTrue()
        {
            return thiscallTestNoArguments!.Invoke(_this!) == 1;
        }

        public bool thiscallTestSumOfFields_ShouldReturnCorrectSum()
        {
            return thiscallTestSumOfFields!.Invoke(_this!) == 6;
        }

        public bool thiscallTestSumOfFieldsAndArguments_ShouldReturnCorrectSum()
        {
            return thiscallTestSumOfFieldsAndArguments!.Invoke(_this!, 4, 5, 6) == 21;
        }

        public bool thiscallTestSumOfFieldsAndArgumentsNested_ShouldReturnCorrectSum()
        {
            return thiscallTestSumOfFieldsAndArgumentsNested!.Invoke(_this!, 4, 5, 6) == 21;
        }

        public bool thiscallTestReturnStruct_ShouldReturnCorrectStruct()
        {
            var in_ctx = new UnmanagedPointer(Process, new TestContext());
            var out_ctx = thiscallTestReturnStruct!.Invoke(_this!, in_ctx);

            var result = new TestContext(1, 2, 3).Equals(out_ctx.Get<TestContext>(Process));

            in_ctx.Free(Process);

            return result;
        }

        public bool thiscallTestStructAsArgument_ShouldReturnCorrectSum()
        {
            return thiscallTestStructAsArgument!.Invoke(_this!, new TestContext(1, 2, 3)) == 6;
        }

        public bool thiscallTestStructsAsArguments_ShouldReturnCorrectSum()
        {
            var ctx = new TestContext(1, 2, 3);

            return thiscallTestStructsAsArguments!.Invoke(_this!, ctx, ctx, ctx) == 18;
        }

        public bool thiscallTestStructPtrAsArgument_ShouldReturnCorrectSum()
        {
            return thiscallTestStructPtrAsArgument!.Invoke(_this!, _this!) == 6;
        }

        public bool thiscallTestStructPtrsAsArguments_ShouldReturnCorrectSum()
        {
            return thiscallTestStructPtrsAsArguments!.Invoke(_this!, _this!, _this!, _this!) == 18;
        }

        public override Func<bool>[] GetTests()
        {
            LoggerService.Warning("__thiscall Tests (x86) --------------\n");

            return
            [
                thiscallTestNoArguments_ShouldReturnTrue,
                thiscallTestSumOfFields_ShouldReturnCorrectSum,
                thiscallTestSumOfFieldsAndArguments_ShouldReturnCorrectSum,
                thiscallTestSumOfFieldsAndArgumentsNested_ShouldReturnCorrectSum,
                thiscallTestReturnStruct_ShouldReturnCorrectStruct,
                thiscallTestStructAsArgument_ShouldReturnCorrectSum,
                thiscallTestStructsAsArguments_ShouldReturnCorrectSum,
                thiscallTestStructPtrAsArgument_ShouldReturnCorrectSum,
                thiscallTestStructPtrsAsArguments_ShouldReturnCorrectSum
            ];
        }

        public override void Dispose()
        {
#if DEBUG
            LoggerService.Warning("__thiscall Cleanup (x86) ------------\n");
#endif

            _this?.Free(Process);

            thiscallTestNoArguments?.Dispose();
            thiscallTestSumOfFields?.Dispose();
            thiscallTestSumOfFieldsAndArguments?.Dispose();
            thiscallTestSumOfFieldsAndArgumentsNested?.Dispose();
            thiscallTestReturnStruct?.Dispose();
            thiscallTestStructAsArgument?.Dispose();
            thiscallTestStructPtrAsArgument?.Dispose();

#if DEBUG
            LoggerService.WriteLine();
#endif
        }
    }
}
