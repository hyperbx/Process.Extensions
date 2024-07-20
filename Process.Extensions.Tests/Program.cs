using ProcessExtensions.Logger;

namespace ProcessExtensions
{
    static class Program
    {
        public static void Main()
        {
            LoggerService.Log("Process.Extensions Tests\n");

            var start = DateTime.Now;

            LoggerService.Log($"Start:   {DateTime.Now:dd/MM/yyyy hh:mm:ss.fff tt}\n");

            var result = true;

            if (!x86Tests.RunTests())
                result = false;

            if (!x64Tests.RunTests())
                result = false;

            if (result)
            {
                LoggerService.Utility("------------ TEST PASSED ------------\n");
            }
            else
            {
                LoggerService.Error("------------ TEST FAILED ------------\n");
            }

            LoggerService.Log($"End:      {DateTime.Now:dd/MM/yyyy hh:mm:ss.fff tt}");
            LoggerService.Log($"Duration: {(DateTime.Now - start).TotalMilliseconds} ms");

            Environment.ExitCode = result ? 0 : -1;
        }
    }
}