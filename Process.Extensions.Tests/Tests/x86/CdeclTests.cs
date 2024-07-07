using ProcessExtensions.Enums;
using ProcessExtensions.Interop;
using ProcessExtensions.Interop.Generic;
using ProcessExtensions.Interop.Structures;
using ProcessExtensions.Logger;
using System.Diagnostics;

namespace ProcessExtensions.Tests.x86
{
    internal class CdeclTests : TestBase
    {
        private UnmanagedProcessFunctionPointer<byte>? cdeclTestNoArguments;
        private UnmanagedProcessFunctionPointer<int, int, int, int>? cdeclTestSumOfArguments;
        private UnmanagedProcessFunctionPointer<UnmanagedPointer, UnmanagedPointer>? cdeclTestReturnStruct;
        private UnmanagedProcessFunctionPointer<UnmanagedPointer>? cdeclTestReturnStructPtr;
        private UnmanagedProcessFunctionPointer<int, TestContext>? cdeclTestStructAsArgument;
        private UnmanagedProcessFunctionPointer<int, TestContext, TestContext, TestContext>? cdeclTestStructsAsArguments;
        private UnmanagedProcessFunctionPointer<int, UnmanagedPointer>? cdeclTestStructPtrAsArgument;
        private UnmanagedProcessFunctionPointer<int, UnmanagedPointer, UnmanagedPointer, UnmanagedPointer>? cdeclTestStructPtrsAsArguments;

        public CdeclTests(Process in_process, SymbolResolver in_sr) : base(in_process)
        {
            cdeclTestNoArguments = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("cdeclTestNoArguments")), ECallingConvention.Cdecl);
            cdeclTestSumOfArguments = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("cdeclTestSumOfArguments")), ECallingConvention.Cdecl);
            cdeclTestReturnStruct = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("cdeclTestReturnStruct")), ECallingConvention.Cdecl);
            cdeclTestReturnStructPtr = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("cdeclTestReturnStructPtr")), ECallingConvention.Cdecl);
            cdeclTestStructAsArgument = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("cdeclTestStructAsArgument")), ECallingConvention.Cdecl);
            cdeclTestStructsAsArguments = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("cdeclTestStructsAsArguments")), ECallingConvention.Cdecl);
            cdeclTestStructPtrAsArgument = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("cdeclTestStructPtrAsArgument")), ECallingConvention.Cdecl);
            cdeclTestStructPtrsAsArguments = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("cdeclTestStructPtrsAsArguments")), ECallingConvention.Cdecl);
        }

        public bool cdeclTestNoArguments_ShouldReturnTrue()
        {
            return cdeclTestNoArguments!.Invoke() == 1;
        }

        public bool cdeclTestSumOfArguments_ShouldReturnCorrectSum()
        {
            return cdeclTestSumOfArguments!.Invoke(1, 2, 3) == 6;
        }

        public bool cdeclTestReturnStruct_ShouldReturnCorrectStruct()
        {
            var ctx = cdeclTestReturnStruct!.Invoke(new UnmanagedPointer(Process, new TestContext(1, 2, 3)));
            var result = new TestContext(1, 2, 3).Equals(ctx.Get<TestContext>(Process));

            ctx.Free(Process);

            return result;
        }

        public bool cdeclTestReturnStructPtr_ShouldReturnCorrectStruct()
        {
            var ctx = cdeclTestReturnStructPtr!.Invoke();

            return new TestContext(1, 2, 3).Equals(ctx.Get<TestContext>(Process));
        }

        public bool cdeclTestStructAsArgument_ShouldReturnCorrectSum()
        {
            return cdeclTestStructAsArgument!.Invoke(new TestContext(1, 2, 3)) == 6;
        }

        public bool cdeclTestStructsAsArguments_ShouldReturnCorrectSum()
        {
            return cdeclTestStructsAsArguments!.Invoke(new TestContext(1, 2, 3), new TestContext(1, 2, 3), new TestContext(1, 2, 3)) == 18;
        }

        public bool cdeclTestStructPtrAsArgument_ShouldReturnCorrectSum()
        {
            var ctx = new UnmanagedPointer(Process, new TestContext(1, 2, 3));
            var result = cdeclTestStructPtrAsArgument!.Invoke(ctx) == 6;

            ctx.Free(Process);

            return result;
        }

        public bool cdeclTestStructPtrsAsArguments_ShouldReturnCorrectSum()
        {
            var ctx = new UnmanagedPointer(Process, new TestContext(1, 2, 3));
            var result = cdeclTestStructPtrsAsArguments!.Invoke(ctx, ctx, ctx) == 18;

            ctx.Free(Process);

            return result;
        }

        public override Func<bool>[] GetTests()
        {
            LoggerService.Warning("__cdecl Tests (x86) -----------------\n");

            return
            [
                cdeclTestNoArguments_ShouldReturnTrue,
                cdeclTestSumOfArguments_ShouldReturnCorrectSum,
                cdeclTestReturnStruct_ShouldReturnCorrectStruct,
                cdeclTestReturnStructPtr_ShouldReturnCorrectStruct,
                cdeclTestStructAsArgument_ShouldReturnCorrectSum,
                cdeclTestStructsAsArguments_ShouldReturnCorrectSum,
                cdeclTestStructPtrAsArgument_ShouldReturnCorrectSum,
                cdeclTestStructPtrsAsArguments_ShouldReturnCorrectSum
            ];
        }

        public override void Dispose()
        {
#if DEBUG
            LoggerService.Warning("__cdecl Cleanup (x86) ---------------\n");
#endif

            cdeclTestNoArguments?.Dispose();
            cdeclTestSumOfArguments?.Dispose();
            cdeclTestReturnStruct?.Dispose();
            cdeclTestReturnStructPtr?.Dispose();
            cdeclTestStructAsArgument?.Dispose();
            cdeclTestStructsAsArguments?.Dispose();
            cdeclTestStructPtrAsArgument?.Dispose();
            cdeclTestStructPtrsAsArguments?.Dispose();

#if DEBUG
            LoggerService.WriteLine();
#endif
        }
    }
}
