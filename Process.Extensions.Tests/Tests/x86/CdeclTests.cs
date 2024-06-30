using ProcessExtensions.Interop;
using ProcessExtensions.Interop.Generic;
using ProcessExtensions.Interop.Structures;
using ProcessExtensions.Logger;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ProcessExtensions.Tests.x86
{
    internal class CdeclTests : TestBase
    {
        private UnmanagedProcessFunctionPointer<byte>? cdeclTestNoArguments;
        private UnmanagedProcessFunctionPointer<int, int, int, int>? cdeclTestSumOfArguments;
        private UnmanagedProcessFunctionPointer<UnmanagedPointer, UnmanagedPointer>? cdeclTestReturnStruct;
        private UnmanagedProcessFunctionPointer<UnmanagedPointer>? cdeclTestReturnStructPtr;
        private UnmanagedProcessFunctionPointer<int, TestContext>? cdeclTestStructAsArgument;
        private UnmanagedProcessFunctionPointer<int, UnmanagedPointer>? cdeclTestStructPtrAsArgument;

        public CdeclTests(Process in_process, SymbolResolver in_sr) : base(in_process)
        {
            cdeclTestNoArguments         = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("cdeclTestNoArguments")), CallingConvention.Cdecl);
            cdeclTestSumOfArguments      = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("cdeclTestSumOfArguments")), CallingConvention.Cdecl);
            cdeclTestReturnStruct        = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("cdeclTestReturnStruct")), CallingConvention.Cdecl);
            cdeclTestReturnStructPtr     = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("cdeclTestReturnStructPtr")), CallingConvention.Cdecl);
            cdeclTestStructAsArgument    = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("cdeclTestSumOfArguments")), CallingConvention.Cdecl);
            cdeclTestStructPtrAsArgument = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("cdeclTestStructPtrAsArgument")), CallingConvention.Cdecl);
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
            var result = cdeclTestReturnStruct!.Invoke(new UnmanagedPointer(Process, new TestContext(1, 2, 3)));

            return new TestContext(1, 2, 3).Equals(result.Get<TestContext>(Process));
        }

        public bool cdeclTestReturnStructPtr_ShouldReturnCorrectStruct()
        {
            var result = cdeclTestReturnStructPtr!.Invoke();

            return new TestContext(1, 2, 3).Equals(result.Get<TestContext>(Process));
        }

        public bool cdeclTestStructAsArgument_ShouldReturnCorrectSum()
        {
            return cdeclTestStructAsArgument!.Invoke(new TestContext(1, 2, 3)) == 6;
        }

        public bool cdeclTestStructPtrAsArgument_ShouldReturnCorrectSum()
        {
            var pCtx = Process.Write(new TestContext(1, 2, 3));

            return cdeclTestStructPtrAsArgument!.Invoke(pCtx) == 6;
        }

        public override Func<bool>[] GetTests()
        {
            LoggerService.Warning("__cdecl Tests (x86) -----------\n");

            return
            [
                cdeclTestNoArguments_ShouldReturnTrue,
                cdeclTestSumOfArguments_ShouldReturnCorrectSum,
                cdeclTestReturnStruct_ShouldReturnCorrectStruct,
                cdeclTestReturnStructPtr_ShouldReturnCorrectStruct,
                cdeclTestStructAsArgument_ShouldReturnCorrectSum,
                cdeclTestStructPtrAsArgument_ShouldReturnCorrectSum
            ];
        }
    }
}
