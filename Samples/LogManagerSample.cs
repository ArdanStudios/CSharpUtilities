using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArdanStudios.Common;

namespace Samples
{
    /// <summary>
    /// The Log Manager has been built for heavy use in all types of applications.
    /// 
    /// Key Features
    /// 1. Write to multiple files using the log key
    /// 2. Uses TLS to help writing to log keys deep within libraries without the need to pass the log key
    /// 3. If HTTP Session exists, it will create log files for each unique session
    /// 4. Can handle high capacity log writes
    /// 5. Monitors open files and closes files not written to after 2 minutes
    /// 6. Maintains directories for each day and will clean up old folders
    /// 7. Email notification for exceptions or events. Will provide session state and url to log file
    /// 
    /// Directory Structure
    /// C:\Logs\Test\06-27-2013\0000\0000\00000000Global
    /// 
    /// Sample Log
    /// 
    /// A          B            C    D  E                F
    /// 06-27-2013 09:11:34.352 0.49 10 THREAD UNKNOWN : Starting test application
    /// 
    /// A: Date the log write was issued
    /// B: Time the log write was issued
    /// C: The duration in milliseconds between the log write request and actual write
    /// D: Thread Id
    /// E: Thread Name
    /// F: The messages
    /// 
    /// </summary>
    public static class LogManagerSample
    {
        public static void SampleApp()
        {
            // Init the Log Manager for use
            LogManager.SetAttributes("C:\\Logs\\Test",
                                     "http://localhost",
                                     "emailhost",
                                     25,                    // emailPort
                                     false,                 // emailSSL,
                                     "emailUserName",
                                     "emailUserPassword",
                                     "emailFrom",
                                     "emailTo",
                                     null,                  // userIdSessionKey
                                     null,                  // userLoginSessionKey
                                     true,                  // turnOnConsole
                                     true,                  // keepAllFiles
                                     true,                  // useThreadLocalStorage
                                     7,                     // maxHistoricalDays
                                     null,                  // cleanupMachine
                                     null);                 // dbConnectionString

            // Log write to the Global Log file
            LogManager.WriteLog("Starting test application");

            // Log write to a file called 0000System. Log keys need to be at least 8 characters in length
            LogManager.WriteLogKey("0000System", "Opening System File Log");

            // Log write to the Global Log files that also sends an email
            LogManager.WriteLog(LogNotificationTypes.Email, "Email Subject", "Exception");

            // Log write to a file called 0000System with formatting
            LogManager.WriteLogKey("0000System", "Testing System File Log Write {0}", "Argument");

            // Store the log key in TLS and create and write to a file called 0000TestTLS
            LogManager.SetLogKeyInTLS("0000TestTLS");
            LogManager.WriteLog("Testing TLS");
            LogManager.SetLogKeyInTLS(null);

            string waitForEnter = Console.ReadLine();

            // Shut down the log manager
            LogManager.Close();
        }
    }
}
