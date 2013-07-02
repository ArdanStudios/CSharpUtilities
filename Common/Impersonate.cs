using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Principal;
using System.Runtime.InteropServices;

namespace ArdanStudios.Common
{
	//=========================================================================================
	/// <summary> Abstracts access to the Win32 API so the application can impersonate a user
	/// with proper credentials to perform the required operations. </summary>
	public class Impersonate
	{
		private const int LOGON32_LOGON_INTERACTIVE = 2;
		private const int LOGON32_PROVIDER_DEFAULT = 0;

		private static WindowsImpersonationContext impersonationContext;

		[DllImport("advapi32.dll")]
		private static extern int LogonUserA(String lpszUserName,
		  String lpszDomain,
		  String lpszPassword,
		  int dwLogonType,
		  int dwLogonProvider,
		  ref IntPtr phToken);

		[DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern int DuplicateToken(IntPtr hToken,
		  int impersonationLevel,
		  ref IntPtr hNewToken);

		[DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern bool RevertToSelf();

		[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
		private static extern bool CloseHandle(IntPtr handle);

        /// <summary> Called to login the application as this user </summary>
        /// <param name="userName"></param>
        /// <param name="domain"></param>
        /// <param name="password"></param>
        /// <returns></returns>
		public static bool Login(String userName, String domain, String password)
		{
			WindowsIdentity tempWindowsIdentity;
			IntPtr token = IntPtr.Zero;
			IntPtr tokenDuplicate = IntPtr.Zero;

			if (RevertToSelf())
			{
				if (LogonUserA(userName, domain, password, LOGON32_LOGON_INTERACTIVE,
					LOGON32_PROVIDER_DEFAULT, ref token) != 0)
				{
					if (DuplicateToken(token, 2, ref tokenDuplicate) != 0)
					{
						tempWindowsIdentity = new WindowsIdentity(tokenDuplicate);
						impersonationContext = tempWindowsIdentity.Impersonate();
						if (impersonationContext != null)
						{
							CloseHandle(token);
							CloseHandle(tokenDuplicate);
							return true;
						}
					}
				}
			}

			if (token != IntPtr.Zero)
				CloseHandle(token);

			if (tokenDuplicate != IntPtr.Zero)
				CloseHandle(tokenDuplicate);

			return false;
		}

        /// <summary> Called to logout the application </summary>
		public static void Logout()
		{
			impersonationContext.Undo();
		}
	}
}
