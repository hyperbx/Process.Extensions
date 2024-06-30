using ProcessExtensions.Logger;

namespace ProcessExtensions
{
    static class Program
    {
        public static void Main()
        {
            LoggerService.Log("ProcessExtensions Tests\n");
            LoggerService.Log($"Start:  {DateTime.Now:dd/MM/yyyy hh:mm:ss.fff}\n");

            var result = true;

            if (!x86Tests.RunTests())
                result = false;

            if (!x64Tests.RunTests())
                result = false;

            if (result)
            {
                LoggerService.Utility("--------- TEST PASSED ---------\n");
            }
            else
            {
                LoggerService.Error("--------- TEST FAILED ---------\n");
            }

            LoggerService.Log($"End:    {DateTime.Now:dd/MM/yyyy hh:mm:ss.fff}");

            Environment.Exit(result ? 0 : -1);
        }
    }
}