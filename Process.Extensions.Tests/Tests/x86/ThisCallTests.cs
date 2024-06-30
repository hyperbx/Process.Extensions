using ProcessExtensions.Interop;
using ProcessExtensions.Interop.Generic;
using ProcessExtensions.Interop.Structures;
using ProcessExtensions.Logger;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ProcessExtensions.Tests.x86
{
    internal class ThisCallTests : TestBase
    {
        private UnmanagedPointer? _ctx;

        private UnmanagedProcessFunctionPointer<byte, UnmanagedPointer>? thiscallTestNoArguments;
        private UnmanagedProcessFunctionPointer<int, UnmanagedPointer>? thiscallTestSumOfFields;
        private UnmanagedProcessFunctionPointer<int, UnmanagedPointer, int, int, int>? thiscallTestSumOfFieldsAndArguments;
        private UnmanagedProcessFunctionPointer<int, UnmanagedPointer, int, int, int>? thiscallTestSumOfFieldsAndArgumentsNested;
        private UnmanagedProcessFunctionPointer<UnmanagedPointer, UnmanagedPointer>? thiscallTestReturnStruct;
        private UnmanagedProcessFunctionPointer<int, UnmanagedPointer, int, int, int>? thiscallTestStructAsArgument;
        private UnmanagedProcessFunctionPointer<int, UnmanagedPointer, UnmanagedPointer>? thiscallTestStructPtrAsArgument;

        public ThisCallTests(Process in_process, SymbolResolver in_sr) : base(in_process)
        {
            _ctx = new UnmanagedPointer(Process, new TestContext(1, 2, 3));
            
            thiscallTestNoArguments                   = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("testContext::thiscallTestNoArguments")), CallingConvention.ThisCall);
            thiscallTestSumOfFields                   = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("testContext::thiscallTestSumOfFields")), CallingConvention.ThisCall);
            thiscallTestSumOfFieldsAndArguments       = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("testContext::thiscallTestSumOfFieldsAndArguments")), CallingConvention.ThisCall);
            thiscallTestSumOfFieldsAndArgumentsNested = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("testContext::thiscallTestSumOfFieldsAndArgumentsNested")), CallingConvention.ThisCall);
            thiscallTestReturnStruct                  = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("testContext::thiscallTestReturnStruct")), CallingConvention.ThisCall);
            thiscallTestStructAsArgument              = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("testContext::thiscallTestStructAsArgument")), CallingConvention.ThisCall);
            thiscallTestStructPtrAsArgument           = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("testContext::thiscallTestStructPtrAsArgument")), CallingConvention.ThisCall);
        }

        public bool thiscallTestNoArguments_ShouldReturnTrue()
        {
            return thiscallTestNoArguments!.Invoke(_ctx!) == 1;
        }

        public bool thiscallTestSumOfFields_ShouldReturnCorrectSum()
        {
            return thiscallTestSumOfFields!.Invoke(_ctx!) == 6;
        }

        public bool thiscallTestSumOfFieldsAndArguments_ShouldReturnCorrectSum()
        {
            return thiscallTestSumOfFieldsAndArguments!.Invoke(_ctx!, 4, 5, 6) == 21;
        }

        public bool thiscallTestSumOfFieldsAndArgumentsNested_ShouldReturnCorrectSum()
        {
            return thiscallTestSumOfFieldsAndArgumentsNested!.Invoke(_ctx!, 4, 5, 6) == 21;
        }

        public bool thiscallTestReturnStruct_ShouldReturnCorrectStruct()
        {
            var in_ctx = new UnmanagedPointer(Process, new TestContext());
            var out_ctx = thiscallTestReturnStruct!.Invoke(_ctx!, in_ctx);

            var result = new TestContext(1, 2, 3).Equals(out_ctx.Get<TestContext>(Process));

            in_ctx.Free(Process);

            return result;
        }

        public bool thiscallTestStructAsArgument_ShouldReturnCorrectSum()
        {
            return thiscallTestStructAsArgument!.Invoke(_ctx!, 1, 2, 3) == 6;
        }

        public bool thiscallTestStructPtrAsArgument_ShouldReturnCorrectSum()
        {
            return thiscallTestStructPtrAsArgument!.Invoke(_ctx!, _ctx!) == 6;
        }

        public override Func<bool>[] GetTests()
        {
            LoggerService.Warning("__thiscall Tests (x86) --------\n");

            return
            [
                thiscallTestNoArguments_ShouldReturnTrue,
                thiscallTestSumOfFields_ShouldReturnCorrectSum,
                thiscallTestSumOfFieldsAndArguments_ShouldReturnCorrectSum,
                thiscallTestSumOfFieldsAndArgumentsNested_ShouldReturnCorrectSum,
                thiscallTestReturnStruct_ShouldReturnCorrectStruct,
                thiscallTestStructAsArgument_ShouldReturnCorrectSum,
                thiscallTestStructPtrAsArgument_ShouldReturnCorrectSum
            ];
        }

        public override void Dispose()
        {
#if DEBUG
            LoggerService.Warning("__thiscall Cleanup (x86) ------\n");
#endif

            _ctx?.Free(Process);

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
