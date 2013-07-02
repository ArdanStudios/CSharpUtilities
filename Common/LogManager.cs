#region Namespaces

using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.IO;
using System.Net.Mail;
using System.Web;
using System.Threading;
using System.Runtime.Serialization;
using ArdanStudios.Common;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;

#endregion

namespace ArdanStudios.Common
{
    #region LogFile Enumeration

    /// <summary> This enum maintains a list of notification types </summary>
    public enum LogNotificationTypes
    {
        /// <summary> No notification required </summary>
        None = 0,

        /// <summary> Email notification required </summary>
        Email = 1
    }

    #endregion

    #region WriteData Class

    /// <summary> The data to be written to disk </summary>
    internal class WriteData
    {
        #region Public Properties

        internal DateTime LogDate = DateTime.Now;
        internal string LogKey;
        internal string LogSubject;
        internal string LogMessage;
        internal int ThreadId;
        internal string ThreadName;
        internal LogNotificationTypes NotificationType;
        internal bool KeepFile = true;

        #endregion

        #region Constructor

        /// <summary> Constructor </summary>
        /// <param name="logKey"></param>
        /// <param name="logSubject"></param>
        /// <param name="logMessage"></param>
        /// <param name="threadId"></param>
        /// <param name="threadName"></param>
        /// <param name="notificationType"></param>
        /// <param name="keepFile"></param>
        public WriteData(string logKey, string logSubject, string logMessage, int threadId, string threadName, LogNotificationTypes notificationType, bool keepFile)
        {
            LogKey = logKey;
            LogSubject = logSubject;
            LogMessage = logMessage;
            ThreadId = threadId;
            ThreadName = threadName;
            NotificationType = notificationType;
            KeepFile = keepFile;
        }

        #endregion
    }

    #endregion

    #region LogFile Class

    /// <summary> This class maintains state information for any given active log </summary>
    public class LogFile : IDisposable
    {
        #region Private Properties

        /// <summary> Flag to indicate if the object has been disposed </summary>
        private bool _disposed = false;

        /// <summary> The StreamWriter object to write to the file </summary>
        private StreamWriter StreamWriter = null;

        /// <summary> Provides synchronous access to the log for thread saftey </summary>
        private object SyncAccess = new object();

        /// <summary> Which data directory that is currently in use </summary>
        private DateTime DateDirectory;

        /// <summary> The path where the file is being written to disk </summary>
        private string FilePath = null;

        /// <summary> The base path where all the log files will be </summary>
        private string BaseDirectory = null;

        /// <summary> The base url where all the log files will be </summary>
        private string BaseUrl = null;

        /// <summary> The email server host port </summary>
        private int EmailPort = 25;

        /// <summary> The email server host name </summary>
        private string EmailHost = null;

        /// <summary> The email server host user name </summary>
        private string EmailUserName = null;

        /// <summary> The email server host user password </summary>
        private string EmailUserPassword = null;

        /// <summary> The email from address </summary>
        private string EmailFrom = null;

        /// <summary> The email to address </summary>
        private string EmailTo = null;

        /// <summary> Email authentication requires SSL </summary>
        private bool EmailSSL = false;

        /// <summary> The session key for the user id </summary>
        private string UserIdSessionKey = null;

        /// <summary> The session key for the user login </summary>
        private string UserLoginSessionKey = null;

        /// <summary> Flag to determine if log writes should go to the console </summary>
        private bool TurnOnConsole = false;

        #endregion

        #region Public Properties

        /// <summary> Maintains information about the last write to the log </summary>
        public DateTime LastWrite { get; private set; }

        /// <summary> A flag to indicate if the file should be kept or deleted </summary>
        public bool IsKeepFile = true;
        
        /// <summary> A flag to indicate if the keep file mark has been written </summary>
        public bool KeepFileMarkerWritten = false;

        #endregion

        #region Internal Properties

        /// <summary> The FileStream object to physically manage the file </summary>
        internal FileStream FileStream = null;

        /// <summary> If information is being stored in the database </summary>
        internal long? LogFileIndexId = null;

        /// <summary> This maintains the key for the log. This is used to identify the disk location for the file </summary>
        internal string LogKey = null;

        /// <summary> The name of the file being written to disk </summary>
        internal string FileName = null;

        /// <summary> The path and name of the file is being written to disk </summary>
        internal string FileFullName = null;

        /// <summary> The full web address for the log file </summary>
        internal string UrlFullName = null;

        #endregion

        #region Constructor

        /// <summary> Constructor </summary>
        /// <param name="baseDirectory"></param>
        /// <param name="baseUrl"></param>
        /// <param name="logKey"></param>
        /// <param name="emailHost"></param>
        /// <param name="emailPort"></param>
        /// <param name="emailSSL"></param>
        /// <param name="emailUserName"></param>
        /// <param name="emailUserPassword"></param>
        /// <param name="emailFrom"></param>
        /// <param name="emailTo"></param>
        /// <param name="userIdSessionKey"></param>
        /// <param name="userLoginSessionKey"></param>
        /// <param name="turnOnConsole"></param>
        /// <param name="keepAllFiles"></param>
        public LogFile(string baseDirectory, string baseUrl, string logKey, string emailHost, int emailPort, bool emailSSL,
                       string emailUserName, string emailUserPassword, string emailFrom, string emailTo,
                       string userIdSessionKey, string userLoginSessionKey, bool turnOnConsole, bool keepAllFiles)
        {
            BaseDirectory = baseDirectory.TrimEnd('\\');
            BaseUrl = (baseUrl != null) ? baseUrl.TrimEnd('/') : null;
            LogKey = logKey;
            EmailHost = emailHost;
            EmailPort = emailPort;
            EmailSSL = emailSSL;
            EmailUserName = emailUserName;
            EmailUserPassword = emailUserPassword;
            EmailFrom = emailFrom;
            EmailTo = emailTo;
            UserIdSessionKey = userIdSessionKey;
            UserLoginSessionKey = userLoginSessionKey;
            TurnOnConsole = turnOnConsole;
            IsKeepFile = keepAllFiles;

            // Create the new log file
            CreateFile();
        }

        /// <summary> Called to dipose the log </summary>
        public void Dispose()
        {
            WriteLog("***** Closing File *****", false);

            Dispose(true);

            // Use SupressFinalize in case a subclass of this type implements a finalizer
            GC.SuppressFinalize(this);
        }

        /// <summary> Called to dispose the log </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    lock (SyncAccess)
                    {
                        try
                        {
                            // Close the File
                            if ((FileStream != null) && (FileStream.CanWrite == true))
                            {
                                StreamWriter.Dispose();
                                FileStream.Dispose();
                            }

                            // Mark everything as disposed
                            _disposed = true;
                            StreamWriter = null;
                            FileStream = null;

                            // Do we want to remove this file
                            if ((!IsKeepFile) && (LogKey != LogManager.GlobalLogKey))
                            {
                                // Remove the file
                                File.Delete(FileFullName);

                                // Can we remove any of the directory structure
                                DirectoryInfo directoryInfo = new DirectoryInfo(FilePath);
                                DirectoryInfo removeDirectory = null;
                                
                                // Check this directory and two levels up
                                for (int checkPath = 0; checkPath < 3; ++checkPath)
                                {
                                    if (directoryInfo.GetFiles().Length != 0)
                                    {
                                        break;
                                    }
                                    
                                    // We can remove this directory and children
                                    removeDirectory = directoryInfo;

                                    // Look at the parent directory
                                    directoryInfo = directoryInfo.Parent;
                                }
                                
                                // Can we remove any directory structure
                                if (removeDirectory != null)
                                {
                                    removeDirectory.Delete(true);
                                }
                            }
                        }

                        catch
                        {
                        }
                    }
                }
            }
        }

        #endregion

        #region File Create Methods

        /// <summary> Called to create the file </summary>
        public void CreateFile()
        {
            // Get the current time
            DateTime dtDateTime = DateTime.Now;

            // Save the date directory
            DateDirectory = DateTime.Today;

            // Create a file name for this file
            FileName = String.Format("{0}-{1}-{2}.txt", LogKey, System.Environment.MachineName, dtDateTime.ToString("yyyy_MM_dd_HH_mm_ss"));

            // Create the file path for this file
            FilePath = String.Format("{0}\\{1}\\{2}\\{3}\\{4}", BaseDirectory, DateTime.Now.ToString("MM-dd-yyyy"), LogKey.Substring(0, 4), LogKey.Substring(4, 4), LogKey);

            // Create the full file name
            FileFullName = String.Format("{0}\\{1}", FilePath, FileName);

            // Create the full web address
            if (BaseUrl != null)
                UrlFullName = String.Format("{0}/{1}/{2}/{3}/{4}/{5}", BaseUrl, DateTime.Now.ToString("MM-dd-yyyy"), LogKey.Substring(0, 4), LogKey.Substring(4, 4), LogKey, FileName);

            // Get the directory info
            DirectoryInfo diDirectory = new DirectoryInfo(FilePath);

            // Check to see if the directory exist
            if (diDirectory.Exists == false)
            {
                diDirectory.Create();
            }
            
            // Create the new FileStream object
            FileStream = new FileStream(FileFullName, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite);

            // Set the StreamWriter to the FileStream
            StreamWriter = new StreamWriter(FileStream);

            // Write to the file everytime a writeline is called.
            StreamWriter.AutoFlush = true;
        }

        #endregion

        #region File Write Methods

        /// <summary> Called to write a message to the log </summary>
        /// <param name="writeData"></param>
        /// <param name="bMarkTime"></param>
        internal void WriteLog(WriteData writeData, bool bMarkTime)
        {
            lock (SyncAccess)
            {
                // Write the message to the log
                if ((StreamWriter != null) && (FileStream.CanWrite == true))
                {
                    // Did we cross over on the day
                    if (DateDirectory.CompareTo(DateTime.Today) != 0)
                    {
                        // Close the file
                        StreamWriter.Dispose();
                        FileStream.Dispose();

                        // Release the existing stream and writer
                        StreamWriter = null;
                        FileStream = null;

                        // Open a new file
                        CreateFile();
                        
                        // So we can log the flag
                        KeepFileMarkerWritten = false;
                    }
                    else if (FileStream.Length > 10485760)
                    {
                        // Check if the file is 10 meg yet

                        // Close the file
                        StreamWriter.Dispose();
                        FileStream.Dispose();

                        // Release the existing stream and writer
                        StreamWriter = null;
                        FileStream = null;

                        // Open a new file
                        CreateFile();
                        
                        // So we can log the flag
                        KeepFileMarkerWritten = false;
                    }
                    
                    // Set the file as a keeper
                    if ((IsKeepFile == false) && (writeData.KeepFile == true))
                    {
                        IsKeepFile = true;
                    }

                    DateTime currentTime = DateTime.Now;
                    TimeSpan diffTime = currentTime - writeData.LogDate;

                    // Capture the name of the thread
                    string threadName = (writeData.ThreadName == null) ? "THREAD UNKNOWN" : writeData.ThreadName;

                    // Write to the log
                    string message = string.Format("{0} {1} {2} {3} : {4}", writeData.LogDate.ToString("MM-dd-yyyy HH:mm:ss.fff"), diffTime.TotalSeconds.ToString("F"), writeData.ThreadId, threadName, writeData.LogMessage);
                    StreamWriter.WriteLine(message);
                    
                    // Write the keep file marker
                    if ((IsKeepFile == true) && (KeepFileMarkerWritten == false))
                    {
                        KeepFileMarkerWritten = true;
                        
                        message = string.Format("{0} {1} {2} {3} : {4}", writeData.LogDate.ToString("MM-dd-yyyy HH:mm:ss.fff"), diffTime.TotalSeconds.ToString("F"), writeData.ThreadId, threadName, "***** Keeping File *****");
                        StreamWriter.WriteLine(message);
                    }

                    // Write to console
                    if (TurnOnConsole)
                    {
                        Console.WriteLine(message);
                    }
                    
                    // Change the last write time
                    if (bMarkTime)
                    {
                        LastWrite = DateTime.Now;
                    }
                }
            }
        }

        /// <summary> Called to write a message to the log </summary>
        /// <param name="logMessage"></param>
        /// <param name="bMarkTime"></param>
        public void WriteLog(string logMessage, bool bMarkTime)
        {
            lock (SyncAccess)
            {
                // Write the message to the log
                if ((StreamWriter != null) && (FileStream.CanWrite == true))
                {
                    // Did we cross over on the day
                    if (DateDirectory.CompareTo(DateTime.Today) != 0)
                    {
                        // Close the file
                        StreamWriter.Dispose();
                        FileStream.Dispose();

                        // Release the existing stream and writer
                        StreamWriter = null;
                        FileStream = null;

                        // Open a new file
                        CreateFile();
                    }
                    else if (FileStream.Length > 10485760)
                    {
                        // Check if the file is 10 meg yet

                        // Close the file
                        StreamWriter.Dispose();
                        FileStream.Dispose();

                        // Release the existing stream and writer
                        StreamWriter = null;
                        FileStream = null;

                        // Open a new file
                        CreateFile();
                    }

                    // Write to the log
                    string message = string.Format("{0} {1} {2} {3}", DateTime.Now.ToString("MM-dd-yyyy HH:mm:ss.fff"), Thread.CurrentThread.ManagedThreadId, Thread.CurrentThread.Name, logMessage);
                    StreamWriter.WriteLine(message);

                    // Write to console
                    if (TurnOnConsole)
                    {
                        Console.WriteLine(message);
                    }
                    
                    // Change the last write time
                    if (bMarkTime)
                    {
                        LastWrite = DateTime.Now;
                    }
                }
            }
        }

        /// <summary> Called to write a message to the log and send an email </summary>
        /// <param name="logSubject"></param>
        /// <param name="logMessage"></param>
        public void SendEmail(string logSubject, string logMessage)
        {
            try
            {
                // We need to keep this file now
                IsKeepFile = true;
                
                if (EmailHost == null)
                    return;

                SmtpClient mailClient = new SmtpClient(EmailHost);
                mailClient.Port = EmailPort;
                mailClient.EnableSsl = EmailSSL;

                if (EmailUserName != null)
                {
                    mailClient.UseDefaultCredentials = false;
                    mailClient.Credentials = new System.Net.NetworkCredential(EmailUserName, EmailUserPassword);
                }

                // Create the mail message to be sent.
                MailMessage mail = new MailMessage();

                // From Address
                String[] parseAddress = EmailFrom.Split(';');
                mail.From = new MailAddress(parseAddress[0]);

                // To Address
                parseAddress = EmailTo.Split(';');
                for (Int32 iAddress = 0; iAddress < parseAddress.Length; ++iAddress)
                    if (parseAddress[iAddress].Length > 0)
                        mail.To.Add(parseAddress[iAddress]);

                // Build the user information
                StringBuilder userInformation = new StringBuilder();
                if ((HttpContext.Current != null) && (HttpContext.Current.Session != null))
                {
                    if (UserIdSessionKey != null)
                        userInformation.AppendFormat("<br /><br />{0}", HttpContext.Current.Session[UserIdSessionKey].ToString());

                    if (UserLoginSessionKey != null)
                        userInformation.AppendFormat("<br /><br />{0}", HttpContext.Current.Session[UserLoginSessionKey].ToString());

                    userInformation.AppendFormat("<br /><br />{0}" + HttpContext.Current.Request.UserAgent);
                }

                mail.Subject = logSubject;
                mail.Priority = MailPriority.Normal;
                mail.IsBodyHtml = true;

                if (UrlFullName != null)
                    mail.Body = string.Format("{0}<br /><br /><a href=\"{1}\">{2}</a>{3}", logMessage, UrlFullName, FileName, userInformation);
                else
                    mail.Body = string.Format("{0}<br /><br />{1}{2}", logMessage, FileFullName, userInformation);

                // Send the user a message
                mailClient.Send(mail);
            }

            catch (Exception exception)
            {
                WriteLog(exception.ToString(), true);
            }
        }

        #endregion
    }

    #endregion

    #region LogManager Class

    /// <summary> This class manages the logging environment as a singleton </summary>
    public class LogManager : IDisposable
    {
        #region Static Properties

        /// <summary> A reference to the log manager singleton </summary>
        private static LogManager Log = null;
        
        /// <summary> The global log key </summary>
        public static string GlobalLogKey = "00000000Global";

        #endregion

        #region Private Properties

        /// <summary> The interval of milliseconds the timer should fire (50 milliseconds) </summary>
        private const int WRITE_CHECK_TIME = 50;

        /// <summary> The interval of milliseconds the timer should fire (2 minutes) </summary>
        private const int CLOSE_CHECK_TIME = 1000 * 60 * 2;

        /// <summary> The amount of seconds when the log should be closed from no activity </summary>
        private const int TIME_TO_CLOSE = 160;
        
        /// <summary> The number of file to write at every iteration </summary>
        private const int WRITES_PER_ITERATION = 1500;

        /// <summary> Flag to indicate if the object has been disposed </summary>
        private bool _disposed = false;

        /// <summary> This maintains a set of open log files </summary>
        private Hashtable LogFiles = new Hashtable();

        /// <summary> Used to provide thread safety </summary>
        private object LogLock = new object();

        /// <summary> Timer thread that wakes up every 10 minutes to close log files </summary>
        private SynchronousTimer CloseTimer = null;

        /// <summary> RefType: Local Data Store Slot for thread local storage to maintain thread level log keys </summary>
        private LocalDataStoreSlot LocalDataStoreSlot = null;

        /// <summary> The base directory for the log </summary>
        private string BaseDirectory = string.Format("{0}\\Logs\\", System.AppDomain.CurrentDomain.BaseDirectory);

        /// <summary> The base url for the log </summary>
        private string BaseUrl = string.Empty;
        
        /// <summary> The email host to use when re-creating a log </summary>
        private string EmailHost = null;

        /// <summary> The email host port to use when re-creating a log </summary>
        private int EmailPort = 25;

        /// <summary> The email from to use when re-creating a log </summary>
        private string EmailFrom = null;

        /// <summary> The email server host user name </summary>
        private string EmailUserName = null;

        /// <summary> The email server host user password </summary>
        private string EmailUserPassword = null;

        /// <summary> The email to to use when re-creating a log </summary>
        private string EmailTo = null;

        /// <summary> Email authentication requires SSL </summary>
        private bool EmailSSL = false;

        /// <summary> The session key for the user id </summary>
        private string UserIdSessionKey = null;

        /// <summary> The session key for the user login </summary>
        private string UserLoginSessionKey = null;

        /// <summary> Flag to determine if log writes should go to the console </summary>
        private bool TurnOnConsole = false;

        /// <summary> Flag to identify if all the files should be kept by default </summary>
        private bool KeepAllFiles = false;

        /// <summary> The maximum number of days to keys log files for </summary>
        private int MaxHistoricalDays = 5;

        /// <summary> The name of the machine to perform the cleanup </summary>
        private string CleanupMachine = null;

        /// <summary> The last time the log folders were cleaned up </summary>
        private DateTime LastLogCleanup = DateTime.MinValue;

        /// <summary> A queue of items to be written </summary>
        private Queue<WriteData> WriteQueue = new Queue<WriteData>(1024*1024);

        /// <summary> Timer thread that wakes up every half of second to write data to the logs </summary>
        private SynchronousTimer WriteTimer = null;
        
        /// <summary> A list of items to log </summary>
        private WriteData[] WriteLogs = new WriteData[WRITES_PER_ITERATION];

        #endregion

        #region Static Public Properties

        /// <summary> When set to true no logging is performed </summary>
        public static bool StopLogging = false;

        #endregion

        #region Constructor

        /// <summary> Called when the singleton is initialized </summary>
        private LogManager()
        {
            // Initialize the write timer
            WriteTimer = new SynchronousTimer(WriteLogFilesThread, null, WRITE_CHECK_TIME, ThreadPriority.AboveNormal, "Log-WriteTimer");

            // Initialize the close timer
            CloseTimer = new SynchronousTimer(CloseLogFilesThread, null, CLOSE_CHECK_TIME, ThreadPriority.AboveNormal, "Log-CloseTimer");
        }

        /// <summary> Called to create the singleton </summary>
        static LogManager()
        {
            Log = new LogManager();
        }

        /// <summary> Destructor </summary>
        ~LogManager()
        {
            // If the object cache exists, then dispose everything
            if (LogFiles != null)
                Dispose();
        }

        /// <summary> Called to dipose the log </summary>
        public void Dispose()
        {
            Dispose(true);

            // Use SupressFinalize in case a subclass of this type implements a finalizer
            GC.SuppressFinalize(this);
        }

        /// <summary> Called to dispose the log </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Give the system 10 seconds to flush what it has
                    for (int wait = 0; wait < 10; ++wait)
                    {
                        int count = 0;
                        lock (Log.WriteQueue)
                        {
                            count = WriteQueue.Count;
                        }
                        
                        if (count > 0)
                        {
                            Thread.Sleep(1000);
                            continue;
                        }

                        break;
                    }
                    
                    lock (Log.LogLock)
                    {
                        // Stop the timer
                        if (WriteTimer != null)
                        {
                            WriteTimer.Dispose();
                            WriteTimer = null;
                        }

                        // Stop the timer
                        if (CloseTimer != null)
                        {
                            CloseTimer.Dispose();
                            CloseTimer = null;
                        }

                        if (Log.LogFiles != null)
                        {
                            // Dispose of each log file
                            foreach (DictionaryEntry logFileEntry in Log.LogFiles)
                            {
                                LogFile logFile = (LogFile)logFileEntry.Value;

                                // Do we update a record in the database
                                if (LogDatabaseManager.IsActive == true)
                                {
                                    LogDatabaseManager.SaveLogFile(logFile);
                                }

                                logFile.Dispose();
                            }

                            // Clear the hashtable
                            Log.LogFiles.Clear();

                            // Release the reference
                            Log.LogFiles = null;
                        }

                        if (Log.LocalDataStoreSlot != null)
                        {
                            Thread.FreeNamedDataSlot("ArdanStudiosLog");
                        }
                    }
                }

                // Mark everything as disposed
                _disposed = true;
            }
        }

        /// <summary> Called to close down all open files </summary>
        public static void Close()
        {
            Log.Dispose();
        }

        #endregion

        #region Write Timer Methods

        /// <summary> Called to perform an actual write and bypass queuing </summary>
        /// <param name="writeData"></param>
        private void PerformWrite(WriteData writeData)
        {
            LogFile fileLog = null;

            lock (Log.LogLock)
            {
                // Capture the file log
                fileLog = (LogFile)Log.LogFiles[writeData.LogKey];

                // If it does not exist, then re-create it
                if (fileLog == null)
                {
                    fileLog = CreateFile(writeData.LogKey);
                }
            }

            // Did we find the log file, write the message to the log
            if (fileLog != null)
            {
                switch (writeData.NotificationType)
                {
                    case LogNotificationTypes.Email:
                        fileLog.SendEmail(writeData.LogSubject, writeData.LogMessage);
                        break;
                }
                
                fileLog.WriteLog(writeData, true);
            }
        }

        /// <summary> Called by the WriteTimer to write the logs </summary>
        /// <param name="dataObject"></param>
        /// <param name="theTimer"></param>
        private static void WriteLogFilesThread(object dataObject, SynchronousTimer theTimer)
        {
            try
            {
                int count = 0;
                
                for (;;)
                {
                    // Are we shutting down
                    if (theTimer.ShuttingDown == true)
                    {
                        break;
                    }
                    
                    // Pull log entries to write
                    lock (Log.WriteQueue)
                    {
                        count = (Log.WriteQueue.Count > WRITES_PER_ITERATION) ? WRITES_PER_ITERATION : Log.WriteQueue.Count;
                        for (int data = 0; data < count; ++data)
                        {
                            Log.WriteLogs[data] = Log.WriteQueue.Dequeue();
                        }
                    }
                    
                    if (count == 0)
                    {
                        break;
                    }
                    
                    for (int data = 0; data < count; ++data)
                    {
                        // Write to disk
                        Log.PerformWrite(Log.WriteLogs[data]);
                    }
                }
            }

            catch
            {
            }
        }

        #endregion

        #region Close Timer Methods

        /// <summary> Called by the CloseTimer to keep the log system clean </summary>
        /// <param name="dataObject"></param>
        /// <param name="theTimer"></param>
        private static void CloseLogFilesThread(object dataObject, SynchronousTimer theTimer)
        {
            try
            {
                lock (Log.LogLock)
                {
                    // Store logs to remove
                    ArrayList removeObjectKeys = new ArrayList();

                    // Capture the curren time
                    DateTime currentTime = DateTime.Now;

                    // Capture the global file log
                    LogFile globalFileLog = (LogFile)Log.LogFiles[LogManager.GlobalLogKey];

                    // If it does not exist, then re-create it
                    if (globalFileLog == null)
                    {
                        globalFileLog = CreateFile(LogManager.GlobalLogKey);
                    }
                    
                    globalFileLog.WriteLog(string.Format("********** Starting Open Log Files [{0}]", Log.LogFiles.Count), true);

                    // Find logs to remove
                    foreach (string key in Log.LogFiles.Keys)
                    {
                        if (key != LogManager.GlobalLogKey)
                        {
                            // Capature the object in cache
                            LogFile logFile = (LogFile)Log.LogFiles[key];

                            // Is this log File still valid
                            TimeSpan diff = currentTime - logFile.LastWrite;

                            logFile.WriteLog(string.Format("********** Test If Log Should Be Closed: LastWrite[{0}] Diff Sec[{1}]", logFile.LastWrite.ToLongTimeString(), diff.TotalSeconds), false);
                            if (diff.TotalSeconds >= TIME_TO_CLOSE)
                            {
                                // Capture the key
                                removeObjectKeys.Add(key);

                                // Do we update a record in the database
                                if (LogDatabaseManager.IsActive == true)
                                {
                                    LogDatabaseManager.SaveLogFile(logFile);
                                }

                                // Close the log
                                logFile.Dispose();
                            }
                        }
                    }

                    globalFileLog.WriteLog(string.Format("********** Log Files To Closed [{0}]", removeObjectKeys.Count), true);

                    // Remove these logs
                    foreach (string key in removeObjectKeys)
                    {
                        Log.LogFiles.Remove(key);
                    }
                    
                    globalFileLog.WriteLog(string.Format("********** Completed Open Log Files [{0}]", Log.LogFiles.Count), true);
                }

                // We need to clean up old log file folders
                if ((Log.CleanupMachine == null) || (Log.CleanupMachine == System.Environment.MachineName))
                {
                    // Capture the global file log
                    LogFile globalFileLog = (LogFile)Log.LogFiles[LogManager.GlobalLogKey];

                    // If it does not exist, then re-create it
                    if (globalFileLog == null)
                    {
                        globalFileLog = CreateFile(LogManager.GlobalLogKey);
                    }
                    
                    // The day has changed, so let's clean up the logs
                    globalFileLog.WriteLog(string.Format("********** Daily Log History Cleanup Process - Now[{0}] Last[{1}]", DateTime.Now.Date.ToString("MM-dd-yyyy"), Log.LastLogCleanup.ToString("MM-dd-yyyy")), true);

                    if (DateTime.Now.Date != Log.LastLogCleanup)
                    {
                        // Do it again tomorrow
                        Log.LastLogCleanup = DateTime.Today;

                        // The day has changed, so let's clean up the logs
                        globalFileLog.WriteLog("********** Daily Log History Cleanup Started", true);

                        // Iterate through all the date folders
                        DirectoryInfo directoryInfo = new DirectoryInfo(Log.BaseDirectory);
                        foreach (DirectoryInfo di in directoryInfo.GetDirectories())
                        {
                            DateTime directory = DateTime.ParseExact(di.Name, "MM-dd-yyyy", System.Globalization.CultureInfo.InvariantCulture);
                            TimeSpan diff = DateTime.Today - directory;
                            if (diff.Days >= Log.MaxHistoricalDays)
                            {
                                try
                                {
                                    di.Delete(true);
                                }

                                catch (Exception exception)
                                {
                                    globalFileLog.WriteLog(string.Format("********** ERROR Deleting Directories [{0}]", exception.ToString()), true);
                                }
                            }
                        }

                        // The day has changed, so let's clean up the logs
                        globalFileLog.WriteLog("********** Daily Log History Cleanup Completed", true);
                    }
                    else
                    {
                        globalFileLog.WriteLog("********** Daily Log History Cleanup NOT REQUIRED", true);
                    }
                }
            }

            catch
            {
            }
        }

        #endregion

        #region Support Methods

        /// <summary> Called to create a new log file based on the logKey </summary>
        /// <param name="logKey"></param>
        /// <returns> The LogFile object for use </returns>
        private static LogFile CreateFile(string logKey)
        {
            // We need to create a new file
            LogFile logFile = new LogFile(Log.BaseDirectory, Log.BaseUrl, logKey, Log.EmailHost, Log.EmailPort, Log.EmailSSL,
                                          Log.EmailUserName, Log.EmailUserPassword, Log.EmailFrom, Log.EmailTo,
                                          Log.UserIdSessionKey, Log.UserLoginSessionKey, Log.TurnOnConsole, Log.KeepAllFiles);

            // Do we save a record in the database
            if (LogDatabaseManager.IsActive == true)
            {
                LogDatabaseManager.SaveLogFile(logFile);
            }

            // Add the new log file to the hashtable
            Log.LogFiles.Add(logKey, logFile);

            return logFile;
        }

        /// <summary> Called to set the attributes </summary>
        /// <param name="baseDirectory"></param>
        /// <param name="baseUrl"></param>
        /// <param name="emailHost"></param>
        /// <param name="emailPort"></param>
        /// <param name="emailSSL"></param>
        /// <param name="emailUserName"></param>
        /// <param name="emailUserPassword"></param>
        /// <param name="emailFrom"></param>
        /// <param name="emailTo"></param>
        /// <param name="userIdSessionKey"></param>
        /// <param name="userLoginSessionKey"></param>
        /// <param name="turnOnConsole"></param>
        /// <param name="keepAllFiles"></param>
        /// <param name="useThreadLocalStorage"></param>
        /// <param name="maxHistoricalDays"></param>
        /// <param name="cleanupMachine"></param>
        /// <param name="dbConnectionString"></param>
        public static void SetAttributes(string baseDirectory, string baseUrl,
                                         string emailHost, int emailPort, bool emailSSL, string emailUserName, string emailUserPassword, string emailFrom, string emailTo,
                                         string userIdSessionKey, string userLoginSessionKey, bool turnOnConsole, bool keepAllFiles,
                                         bool useThreadLocalStorage, int maxHistoricalDays, string cleanupMachine,
                                         string dbConnectionString = null)
        {
            Log.BaseDirectory = string.Format("{0}\\", baseDirectory.TrimEnd('\\'));
            Log.BaseUrl = (baseUrl != null) ? string.Format("{0}/", baseUrl.TrimEnd('/')) : null;
            Log.EmailHost = emailHost;
            Log.EmailPort = emailPort;
            Log.EmailSSL = emailSSL;
            Log.EmailUserName = emailUserName;
            Log.EmailUserPassword = emailUserPassword;
            Log.EmailFrom = emailFrom;
            Log.EmailTo = emailTo;
            Log.UserIdSessionKey = userIdSessionKey;
            Log.UserLoginSessionKey = userLoginSessionKey;
            Log.TurnOnConsole = turnOnConsole;
            Log.KeepAllFiles = keepAllFiles;
            Log.MaxHistoricalDays = maxHistoricalDays;
            Log.CleanupMachine = cleanupMachine;

            if (useThreadLocalStorage)
            {
                Log.LocalDataStoreSlot = Thread.AllocateNamedDataSlot("ArdanStudiosLog");
            }

            // Capture the application name from the base directory
            string[] parts = Log.BaseDirectory.Split(new char[1] {'\\'} );

            // Init the database manager
            LogDatabaseManager.Init(dbConnectionString, parts[parts.Length - 2]);
        }
        
        /// <summary> Called to tell the system to keep the file once closed. Only for session based implementations </summary>
        public static void KeepFile()
        {
            try
            {
                HttpContext.Current.Session["LogManagerKeep"] = "yes";
            }

            catch
            {
            }
        }
        
        /// <summary> Called to tell the system to keep the file once closed. For non session based implementations </summary>
        /// <param name="logKey"></param>
        /// <param name="logMessage"></param>
        public static void KeepFile(string logKey, string logMessage)
        {
            try
            {
                // Use the global log if null
                logKey = (logKey == null) ? LogManager.GlobalLogKey : logKey;
                
                lock (Log.WriteQueue)
                {
                    Log.WriteQueue.Enqueue(new WriteData(logKey, null, logMessage, Thread.CurrentThread.ManagedThreadId, Thread.CurrentThread.Name, LogNotificationTypes.None, true));
                }
            }
            
            catch
            {
            }
        }
        
        /// <summary> Called to store the key in thread local storage for later use in writing logs </summary>
        /// <param name="logKey"></param>
        public static void SetLogKeyInTLS(string logKey)
        {
            Thread.SetData(Log.LocalDataStoreSlot, logKey);
        }

        /// <summary> Called to get the key in thread local storage for later use in writing logs </summary>
        public static string GetLogKeyInTLS()
        {
            return (string) Thread.GetData(Log.LocalDataStoreSlot);
        }

        /// <summary> Called to know if the LogKey has been set in TLS </summary>
        /// <returns> bool </returns>
        public static bool IsLogKeyInTLSSet()
        {
            return (Log.LocalDataStoreSlot != null) ? (Thread.GetData(Log.LocalDataStoreSlot) == null) ? false : true : false;
        }

        /// <summary> Called to write to the file log </summary>
        /// <param name="logKey"></param>
        /// <param name="logSubject"></param>
        /// <param name="logMessage"></param>
        /// <param name="notificationType"></param>
        private static void PerformLogAction(string logKey, string logSubject, string logMessage, LogNotificationTypes notificationType)
        {
            // Only if we don't want any logging
            if (StopLogging == true)
            {
                return;
            }

            try
            {
                // Set the Keep File flag
                bool isKeepFile = Log.KeepAllFiles;
                
                // If we are not keeping all files
                if (!Log.KeepAllFiles)
                {
                    // We may need to reset the keep file flag
                    if ((HttpContext.Current != null) && (HttpContext.Current.Session != null))
                    {
                        if (HttpContext.Current.Session["LogManagerKeep"] != null)
                        {
                            isKeepFile = true;
                        }
                    }
                }
                
                lock (Log.WriteQueue)
                {
                    Log.WriteQueue.Enqueue(new WriteData(logKey, logSubject, logMessage, Thread.CurrentThread.ManagedThreadId, Thread.CurrentThread.Name, notificationType, isKeepFile));
                }
            }

            catch
            {
            }
        }

        #endregion

        #region Public Write Methods
        
        /// <summary> Called to perform an actual write and bypass queuing. Only to be used by log server </summary>
        /// <param name="writeData"></param>
        internal static void PerformDirectWrite(WriteData writeData)
        {
            try
            {
                Log.PerformWrite(writeData);
            }
            
            catch
            {
            }
        }

        /// <summary> Called to write to the file log with notifications or without notifications </summary>
        /// <param name="notificationType"></param>
        /// <param name="logSubject"></param>
        /// <param name="logMessage"></param>
        public static void WriteLog(LogNotificationTypes notificationType, string logSubject, string logMessage)
        {
            if (IsLogKeyInTLSSet())
            {
                PerformLogAction((string) Thread.GetData(Log.LocalDataStoreSlot), logSubject, logMessage, notificationType);
            }
            else if ((HttpContext.Current != null) && (HttpContext.Current.Session != null))
            {
                PerformLogAction(HttpContext.Current.Session.SessionID, logSubject, logMessage, notificationType);
            }
            else
                PerformLogAction(LogManager.GlobalLogKey, logSubject, logMessage, notificationType);
        }

        /// <summary> Called to write to the file log </summary>
        /// <param name="notificationType"></param>
        /// <param name="logSubject"></param>
        /// <param name="logMessage"></param>
        /// <param name="logValues"></param>
        public static void WriteLog(LogNotificationTypes notificationType, string logSubject, string logMessage, params object[] logValues)
        {
            WriteLog(notificationType, logSubject, string.Format(logMessage, logValues));
        }

        /// <summary> Called to write to the file log </summary>
        /// <param name="logMessage"></param>
        public static void WriteLog(string logMessage)
        {
            WriteLog(LogNotificationTypes.None, null, logMessage);
        }

        /// <summary> Called to write to the file log </summary>
        /// <param name="logMessage"></param>
        /// <param name="logValues"></param>
        public static void WriteLog(string logMessage, params object[] logValues)
        {
            WriteLog(LogNotificationTypes.None, null, string.Format(logMessage, logValues));
        }

        /// <summary> Called to write to the file log with notifications or without notifications </summary>
        /// <param name="logKey"></param>
        /// <param name="notificationType"></param>
        /// <param name="logSubject"></param>
        /// <param name="logMessage"></param>
        public static void WriteLogKey(string logKey, LogNotificationTypes notificationType, string logSubject, string logMessage)
        {
            if (IsLogKeyInTLSSet())
            {
                PerformLogAction((string) Thread.GetData(Log.LocalDataStoreSlot), logSubject, logMessage, notificationType);
            }
            else if ((HttpContext.Current != null) && (HttpContext.Current.Session != null))
            {
                PerformLogAction(HttpContext.Current.Session.SessionID, logSubject, logMessage, notificationType);
            }
            else
                PerformLogAction(logKey, logSubject, logMessage, notificationType);
        }

        /// <summary> Called to write to the file log specifing the log key </summary>
        /// <param name="logKey"></param>
        /// <param name="logMessage"></param>
        public static void WriteLogKey(string logKey, string logMessage)
        {
            WriteLogKey(logKey, LogNotificationTypes.None, null, logMessage);
        }

        /// <summary> Called to write to the file log specifing the log key </summary>
        /// <param name="logKey"></param>
        /// <param name="logMessage"></param>
        /// <param name="logValues"></param>
        public static void WriteLogKey(string logKey, string logMessage, params object[] logValues)
        {
            WriteLogKey(logKey, LogNotificationTypes.None, null, string.Format(logMessage, logValues));
        }

        /// <summary> Called to write to the file log with notifications specifing the log key </summary>
        /// <param name="logKey"></param>
        /// <param name="notificationType"></param>
        /// <param name="logSubject"></param>
        /// <param name="logMessage"></param>
        /// <param name="logValues"></param>
        public static void WriteLogKey(string logKey, LogNotificationTypes notificationType, string logSubject, string logMessage, params object[] logValues)
        {
            WriteLogKey(logKey, notificationType, logSubject, string.Format(logMessage, logValues));
        }

        #endregion

        #region Find Methods

        /// <summary> Called to find existing logs </summary>
        /// <param name="loginDate"></param>
        /// <param name="logKey"></param>
        /// <returns> A datatable of log files </returns>
        public static DataTable FindLogs(DateTime loginDate, string logKey)
        {
            DataTable dataTable = null;

            try
            {
                // Create a data table to store these files
                dataTable = new DataTable();

                // Create the columns
                DataColumn dataColumn = dataTable.Columns.Add();
                dataColumn.ColumnName = "LongFileName";
                dataColumn.DataType = typeof(String);

                dataColumn = dataTable.Columns.Add();
                dataColumn.ColumnName = "ShortFileName";
                dataColumn.DataType = typeof(String);

                dataColumn = dataTable.Columns.Add();
                dataColumn.ColumnName = "ServerName";
                dataColumn.DataType = typeof(String);

                dataColumn = dataTable.Columns.Add();
                dataColumn.ColumnName = "CreateDt";
                dataColumn.DataType = typeof(DateTime);

                dataColumn = dataTable.Columns.Add();
                dataColumn.ColumnName = "LastModifiedDt";
                dataColumn.DataType = typeof(DateTime);

                dataColumn = dataTable.Columns.Add();
                dataColumn.ColumnName = "Size";
                dataColumn.DataType = typeof(Int64);

                // The directory Info CANNOT have the ending \
                string logPhysicalDirectory = string.Format("{0}{1}\\{2}\\{3}\\{4}", Log.BaseDirectory, loginDate.ToString("MM-dd-yyyy"), logKey.Substring(0, 4), logKey.Substring(4, 4), logKey);
                string logWebDirectory = string.Format("{0}{1}/{2}/{3}/{4}", Log.BaseUrl, loginDate.ToString("MM-dd-yyyy"), logKey.Substring(0, 4), logKey.Substring(4, 4), logKey);

                if (Directory.Exists(logPhysicalDirectory))
                {
                    // Locate the files
                    DirectoryInfo directoryInfo = new DirectoryInfo(logPhysicalDirectory);

                    // Get all the log files
                    FileInfo[] fileInfos = directoryInfo.GetFiles("*.txt");

                    // Iterate through the directory of files
                    foreach (FileInfo fileInfo in fileInfos)
                    {
                        // Split the log file name into session, server and date
                        String[] fileNameInfo = fileInfo.Name.Split('-');

                        // Create a new DataRow and add the values and 
                        DataRow fileDataRow = dataTable.NewRow();
                        fileDataRow["LongFileName"] = logWebDirectory + fileInfo.Name.ToLower();
                        fileDataRow["ShortFileName"] = fileInfo.Name.ToLower();
                        fileDataRow["ServerName"] = fileNameInfo[1];
                        fileDataRow["CreateDt"] = fileInfo.CreationTime;
                        fileDataRow["LastModifiedDt"] = fileInfo.LastWriteTime;
                        fileDataRow["Size"] = fileInfo.Length;

                        // Add the new row to the table
                        dataTable.Rows.Add(fileDataRow);
                    }

                    // Sort the table in ascending order

                    // Clone the table
                    DataTable dataSortTable = dataTable.Clone();

                    if (dataTable.Rows.Count > 1)
                    {
                        // Perform the sort on the createdDt field
                        DataRow[] dataRows = dataTable.Select(String.Empty, "CreateDt");

                        // Copy those records into the internal table
                        foreach (DataRow dataRow in dataRows)
                            dataSortTable.ImportRow(dataRow);

                        // Reset the link
                        dataTable = dataSortTable;
                    }
                }
            }

            catch
            {
            }

            return dataTable;
        }

        #endregion
    }

    #endregion

    #region LogDatabaseManager Class

    /// <summary> Provides support for database integration </summary>
    public static class LogDatabaseManager
    {
        #region Private Static Properties

        /// <summary> Database coonection string if required </summary>
        private static string DBConnectionString = null;

        /// <summary> Take from the last directory of the base directory. For SQL </summary>
        private static string ApplicationName = null;

        /// <summary> Used to protect access to the cache </summary>
        private static object LockCache = new object();

        /// <summary> List of known applications from the logs </summary>
        private static Dictionary<string, List<string>> Applications = new Dictionary<string, List<string>>();

        /// <summary> The time the cache was set </summary>
        private static DateTime CacheDateTime = DateTime.MinValue;

        /// <summary> Keep the cache for 5 minutes </summary>
        private static double CacheTimeToLive = 300;

        /// <summary> The value when no time is given </summary>
        private static TimeSpan NoTime = new TimeSpan(0, 0, 0);

        #endregion

        #region Public Static Properties

        /// <summary> Indicates the database manager is active </summary>
        public static bool IsActive { get { return (DBConnectionString == null) ? false : true; } }

        #endregion

        #region Data Class

        /// <summary> Contains log file information </summary>
        public class LogFileInfo
        {
            /// <summary> The unique index for the log file </summary>
            public long LogFileIndexId { get; set; }

            /// <summary> The application that the log was written under </summary>
            public string Application { get; set; }

            /// <summary> The machine that the log was written on </summary>
            public string MachineName { get; set; }

            /// <summary> The log key of the log file </summary>
            public string LogKey { get; set; }

            /// <summary> The date the log was created </summary>
            public DateTime CreatedDate { get; set; }

            /// <summary> The name of the log file </summary>
            public string FileName { get; set; }

            /// <summary> The full file name of the log file </summary>
            public string PhysicalPath { get; set; }

            /// <summary> The url to access the log file </summary>
            public string WebPath { get; set; }

            /// <summary> The size of the log file if it has been closed </summary>
            public int? FileSize { get; set; }

            /// <summary> The date the log was closed </summary>
            public DateTime? LastModifiedDate { get; set; }
        }

        #endregion

        #region Internal Static Methods

        /// <summary> Called to init the database manager </summary>
        /// <param name="dbConnectionString"></param>
        /// <param name="applicationName"></param>
        internal static void Init(string dbConnectionString, string applicationName)
        {
            DBConnectionString = dbConnectionString;
            ApplicationName = applicationName;
        }

        /// <summary> Called to save or update a log file index entry in the database </summary>
        /// <param name="logFile"></param>
        internal static void SaveLogFile(LogFile logFile)
        {
            SqlParameter newLogFileIndexId = null;

            try
            {
                // Create a connection
                using (SqlConnection sqlConnection = new SqlConnection(DBConnectionString))
                {
                    sqlConnection.Open();

                    using (SqlCommand sqlCommand = new SqlCommand("spCommon_LogFileIndex_Save", sqlConnection))
                    {
                        sqlCommand.CommandType = CommandType.StoredProcedure;

                        newLogFileIndexId = sqlCommand.Parameters.Add(new SqlParameter("NewLogFileIndexID", SqlDbType.BigInt) { Direction = ParameterDirection.Output });

                        if (logFile.LogFileIndexId == null)
                        {
                            sqlCommand.Parameters.Add(new SqlParameter("Application", SqlDbType.NVarChar, 50) { Value = ApplicationName, Direction = ParameterDirection.Input });
                            sqlCommand.Parameters.Add(new SqlParameter("MachineName", SqlDbType.NVarChar, 50) { Value = System.Environment.MachineName, Direction = ParameterDirection.Input });
                            sqlCommand.Parameters.Add(new SqlParameter("LogKey", SqlDbType.NVarChar, 50) { Value = logFile.LogKey, Direction = ParameterDirection.Input });
                            sqlCommand.Parameters.Add(new SqlParameter("FileName", SqlDbType.NVarChar, 500) { Value = logFile.FileName, Direction = ParameterDirection.Input });
                            sqlCommand.Parameters.Add(new SqlParameter("CreatedDate", SqlDbType.DateTime) { Value = DateTime.Now, Direction = ParameterDirection.Input });
                            sqlCommand.Parameters.Add(new SqlParameter("PhysicalPath", SqlDbType.NVarChar, 2000) { Value = logFile.FileFullName, Direction = ParameterDirection.Input });
                            sqlCommand.Parameters.Add(new SqlParameter("WebPath", SqlDbType.NVarChar, 2000) { Value = logFile.UrlFullName, Direction = ParameterDirection.Input });
                        }
                        else
                        {
                            sqlCommand.Parameters.Add(new SqlParameter("LogFileIndexID", SqlDbType.BigInt) { Value = logFile.LogFileIndexId, Direction = ParameterDirection.Input });
                            sqlCommand.Parameters.Add(new SqlParameter("FileSize", SqlDbType.Int) { Value = logFile.FileStream.Length, Direction = ParameterDirection.Input });
                            sqlCommand.Parameters.Add(new SqlParameter("LastModifiedDate", SqlDbType.DateTime) { Value = DateTime.Now, Direction = ParameterDirection.Input });
                        }

                        sqlCommand.ExecuteNonQuery();

                        // Do we need to update the new identifier
                        if (logFile.LogFileIndexId == null)
                        {
                            logFile.LogFileIndexId = Convert.ToInt64(newLogFileIndexId.Value);
                        }
                    }
                }
            }

            catch (Exception exception)
            {
                logFile.WriteLog(exception.Message, false);
            }
        }

        #endregion

        #region Public Static Methods

        /// <summary> Called to search for log files from the database log file index table </summary>
        /// <param name="application"></param>
        /// <param name="machineName"></param>
        /// <param name="startDateString"></param>
        /// <param name="endDateString"></param>
        /// <param name="logKey"></param>
        /// <param name="logFiles"></param>
        public static void SearchLogFiles(string application, string machineName, string startDateString, string endDateString, string logKey, out List<LogFileInfo> logFiles)
        {
            logFiles = new List<LogFileInfo>();

            // Fix all the strings
            object applicationValue = (string.IsNullOrWhiteSpace(application) == true) ? DBNull.Value : (object) application;
            object machineNameValue = (string.IsNullOrWhiteSpace(machineName) == true) ? DBNull.Value : (object) machineName;
            object logKeyValue = (string.IsNullOrWhiteSpace(logKey) == true) ? DBNull.Value : (object) logKey;
            startDateString = (string.IsNullOrWhiteSpace(startDateString) == true) ? DateTime.Now.ToString("MM/dd/yyyy") : startDateString;
            endDateString = (string.IsNullOrWhiteSpace(endDateString) == true) ? DateTime.Now.ToString("MM/dd/yyyy") : endDateString;

            // Create a connection
            using (SqlConnection sqlConnection = new SqlConnection(DBConnectionString))
            {
                sqlConnection.Open();

                using (SqlCommand sqlCommand = new SqlCommand("spCommon_LogFileIndex_Get", sqlConnection))
                {
                    sqlCommand.CommandType = CommandType.StoredProcedure;

                    sqlCommand.Parameters.Add(new SqlParameter("Application", SqlDbType.NVarChar, 50) { Value = applicationValue, Direction = ParameterDirection.Input });
                    sqlCommand.Parameters.Add(new SqlParameter("MachineName", SqlDbType.NVarChar, 50) { Value = machineNameValue, Direction = ParameterDirection.Input });
                    sqlCommand.Parameters.Add(new SqlParameter("CreatedDateStart", SqlDbType.DateTime) { Value = ParseDate(startDateString, false), Direction = ParameterDirection.Input });
                    sqlCommand.Parameters.Add(new SqlParameter("CreatedDateEnd", SqlDbType.DateTime) { Value = ParseDate(endDateString, true), Direction = ParameterDirection.Input });
                    sqlCommand.Parameters.Add(new SqlParameter("LogKey", SqlDbType.NVarChar, 50) { Value = logKeyValue, Direction = ParameterDirection.Input });
                        
                    using (SqlDataReader reader = sqlCommand.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            LogFileInfo logFileInfo = new LogFileInfo();

                            logFileInfo.LogFileIndexId = reader.GetInt64(0);
                            logFileInfo.Application = reader.GetString(1);
                            logFileInfo.MachineName = reader.GetString(2);
                            logFileInfo.LogKey = reader.GetString(3);
                            logFileInfo.CreatedDate = reader.GetDateTime(4);
                            logFileInfo.FileName = reader.GetString(5);
                            logFileInfo.PhysicalPath = reader.GetString(6);
                            logFileInfo.WebPath = reader.GetString(7);
                            logFileInfo.FileSize = reader.IsDBNull(8) ? null : (int?)reader.GetInt32(8);
                            logFileInfo.LastModifiedDate = reader.IsDBNull(9) ? null : (DateTime?)reader.GetDateTime(9);

                            logFiles.Add(logFileInfo);
                        }
                    }
                }
            }
        }

        /// <summary> Called to get search items </summary>
        /// <param name="applications"></param>
        /// <param name="refresh"></param>
        public static void GetSearchItems(out Dictionary<string, List<string>> applications, bool refresh)
        {
            applications = null;

            lock (LockCache)
            {
                if (refresh == false)
                {
                    // Validate that the cache is still good
                    TimeSpan timeDiff = DateTime.Now - CacheDateTime;
                    if (timeDiff.TotalSeconds <= CacheTimeToLive)
                    {
                        // Give them references to these
                        applications = Applications;

                        return;
                    }
                }

                // Create a connection
                using (SqlConnection sqlConnection = new SqlConnection(DBConnectionString))
                {
                    sqlConnection.Open();

                    using (SqlCommand sqlCommand = new SqlCommand("spCommon_LogFileIndex_GetDistinctSearchItems", sqlConnection))
                    {
                        sqlCommand.CommandType = CommandType.StoredProcedure;

                        using (SqlDataReader reader = sqlCommand.ExecuteReader())
                        {
                            Applications = new Dictionary<string, List<string>>();
                            while (reader.Read())
                            {
                                string application = reader.GetString(0);
                                string machine = reader.GetString(1);

                                if (Applications.ContainsKey(application) == false)
                                {
                                    Applications.Add(application, new List<string>());
                                }

                                Applications[application].Add(machine);
                            }

                            // Set the new time for the cache
                            CacheDateTime = DateTime.Now;
                        }
                    }
                }

                // Give them references to these
                applications = Applications;
            }
        }

        #endregion

        #region Private Static Methods

        /// <summary> Helper method to parse a date </summary>
        /// <param name="dateTime"></param>
        /// <param name="end"></param>
        /// <returns> DateTime? </returns>
        private static DateTime? ParseDate(string dateTime, bool end)
        {
            DateTime value = DateTime.MinValue;

            if (string.IsNullOrWhiteSpace(dateTime) == false)
            {
                DateTime.TryParse(dateTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
            }

            if (value != DateTime.MinValue)
            {
                // If this is the end time and no time was given provide tomorrow
                if ((end == true) && (value.TimeOfDay == NoTime))
                {
                    value = value.AddDays(1);
                }
            }

            return (value == DateTime.MinValue) ? null : (DateTime?) value;
        }

        #endregion
    }

    #endregion

    #region Database Code
    
    /*
        SET ANSI_NULLS ON
        GO

        SET QUOTED_IDENTIFIER ON
        GO

        CREATE TABLE [dbo].[LogFileIndex](
	        [LogFileIndexID] [bigint] IDENTITY(1,1) NOT NULL,
	        [Application] [nvarchar](50) NOT NULL,
	        [MachineName] [nvarchar](50) NOT NULL,
	        [LogKey] [nvarchar](50) NOT NULL,
	        [CreatedDate] [datetime] NOT NULL,
	        [FileName] [nvarchar](500) NOT NULL,
	        [PhysicalPath] [nvarchar](max) NOT NULL,
	        [WebPath] [nvarchar](max) NOT NULL,
	        [FileSize] [int] NULL,
	        [LastModifiedDate] [datetime] NULL
        ) ON [PRIMARY]
        GO

        ALTER TABLE [dbo].[LogFileIndex] ADD  CONSTRAINT [PK_LogFileIndex] PRIMARY KEY CLUSTERED 
        (
	        [LogFileIndexID] ASC
        )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
        GO

        CREATE NONCLUSTERED INDEX [IX_LogFileIndex_Application] ON [dbo].[LogFileIndex] 
        (
	        [Application] ASC,
	        [MachineName] ASC,
	        [LogKey] ASC,
	        [CreatedDate] ASC
        )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
        GO

        CREATE NONCLUSTERED INDEX [IX_LogFileIndex_LogKey] ON [dbo].[LogFileIndex] 
        (
	        [LogKey] ASC
        )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
        GO

        CREATE PROCEDURE dbo.spCommon_LogFileIndex_Save
        (
	        @LogFileIndexID     BIGINT = NULL,
            @Application        NVARCHAR(50) = NULL,
	        @MachineName        NVARCHAR(50) = NULL,
            @LogKey             NVARCHAR(50) = NULL,
	        @FileName           NVARCHAR(500) = NULL,
	        @CreatedDate        DATETIME = NULL,
	        @PhysicalPath       NVARCHAR(2000) = NULL,
	        @WebPath            NVARCHAR(2000) = NULL,
	        @FileSize           INT = NULL,
	        @LastModifiedDate   DATETIME = NULL,
            @NewLogFileIndexID  BIGINT OUTPUT
        )
        AS
        BEGIN

	        SET NOCOUNT ON

            IF (@LogFileIndexID IS NULL)
            BEGIN

                -- Save a new record
                INSERT LogFileIndex
                    (Application, MachineName, LogKey, CreatedDate, FileName, PhysicalPath, WebPath)
                VALUES
                    (@Application, @MachineName, @LogKey, @CreatedDate, @FileName, @PhysicalPath, @WebPath)

                SET @NewLogFileIndexID = SCOPE_IDENTITY()
    
            END
            ELSE
            BEGIN

                -- Update the file information about the file
                UPDATE
                    LogFileIndex
                SET
                    FileSize = @FileSize,
                    LastModifiedDate = @LastModifiedDate
                WHERE
                    LogFileIndexID = @LogFileIndexID

            END

        END
        GO

        CREATE PROCEDURE dbo.spCommon_LogFileIndex_Get
        (
            @Application        NVARCHAR(50),
	        @MachineName        NVARCHAR(50),
	        @CreatedDateStart   DATETIME,
            @CreatedDateEnd     DATETIME,
            @LogKey             NVARCHAR(50)
        )
        AS
        BEGIN

	        SET NOCOUNT ON

            IF (@LogKey IS NOT NULL)
            BEGIN

                SELECT TOP 100
                    LogFileIndexID,
                    Application,
                    MachineName,
                    LogKey,
                    CreatedDate,
                    FileName,
                    PhysicalPath,
                    WebPath,
                    FileSize,
                    LastModifiedDate
                FROM
                    LogFileIndex
                WHERE
                    Application = ISNULL(@Application, Application) AND
                    MachineName = ISNULL(@MachineName, MachineName) AND
                    CreatedDate BETWEEN @CreatedDateStart AND @CreatedDateEnd AND
                    LogKey LIKE @LogKey
                ORDER BY
                    CreatedDate DESC
    
            END
            ELSE
            BEGIN

                SELECT TOP 100
                    LogFileIndexID,
                    Application,
                    MachineName,
                    LogKey,
                    CreatedDate,
                    FileName,
                    PhysicalPath,
                    WebPath,
                    FileSize,
                    LastModifiedDate
                FROM
                    LogFileIndex
                WHERE
                    Application = ISNULL(@Application, Application) AND
                    MachineName = ISNULL(@MachineName, MachineName) AND
                    CreatedDate BETWEEN @CreatedDateStart AND @CreatedDateEnd
                ORDER BY
                    CreatedDate DESC

            END

        END
        GO

        CREATE PROCEDURE dbo.spCommon_LogFileIndex_GetDistinctSearchItems
        AS
        BEGIN

	        SET NOCOUNT ON

            SELECT DISTINCT
                Application,
                MachineName
            FROM
                LogFileIndex
            ORDER BY
                Application,
                MachineName

        END
        GO
     */

     #endregion
}