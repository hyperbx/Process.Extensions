using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ProcessExtensions.Exceptions
{
    public class VerboseWin32Exception(string in_message)
        : Win32Exception(string.Format(_format, GetExceptionMessage(in_message), GetWin32ErrorMessage()))
    {
        private const string _format = "{0} Reason: {1}";

        private static string GetExceptionMessage(string in_message)
        {
            // Ensure user message ends with a period.
            return !in_message.EndsWith('.')
                ? in_message + '.'
                : in_message;
        }

        private static string GetWin32ErrorMessage()
        {
            var msg = Marshal.GetPInvokeErrorMessage(Marshal.GetLastWin32Error());

            // Make first char lowercase.
            if (char.IsUpper(msg[0]))
                msg = char.ToLower(msg[0]) + msg[1..];

            return msg;
        }
    }
}
