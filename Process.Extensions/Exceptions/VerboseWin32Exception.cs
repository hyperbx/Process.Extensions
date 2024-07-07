using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ProcessExtensions.Exceptions
{
    public class VerboseWin32Exception : Win32Exception
    {
        private const string _format = "{0} Reason: {1} ({2})";

        public VerboseWin32Exception(string in_message) : base(GetFormatted(in_message)) { }

        public VerboseWin32Exception(string in_message, int in_err) : base(GetFormatted(in_message, in_err)) { }

        private static string GetFormatted(string in_message, int in_err = -1)
        {
            if (in_err == -1)
                in_err = Marshal.GetLastWin32Error();

            return string.Format(_format, GetExceptionMessage(in_message), GetWin32ErrorMessage(in_err), in_err);
        }

        private static string GetExceptionMessage(string in_message)
        {
            // Ensure user message ends with a period.
            return !in_message.EndsWith('.')
                ? in_message + '.'
                : in_message;
        }

        private static string GetWin32ErrorMessage(int in_err)
        {
            var msg = Marshal.GetPInvokeErrorMessage(in_err);

            // Make first char lowercase.
            if (char.IsUpper(msg[0]))
                msg = char.ToLower(msg[0]) + msg[1..];

            return msg;
        }
    }
}
