#region Namespaces

using System;
using System.Runtime.InteropServices;
using System.Threading;

#endregion

namespace ArdanStudios.Common
{
	#region Structure - OVERLAPPED

	/// <summary> This is the WIN32 OVERLAPPED structure </summary>
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
	public unsafe struct OVERLAPPED
	{
		UInt32* ulpInternal;
		UInt32* ulpInternalHigh;
		Int32 lOffset;
		Int32 lOffsetHigh;
		UInt32 hEvent;
	}

	#endregion

	#region Class - IOCPThreadPool

	/// <summary> This class provides the ability to create a thread pool to manage work.  The
	///           class abstracts the Win32 IOCompletionPort API so it requires the use of
	///           unmanaged code.  Unfortunately the .NET framework does not provide this functionality </summary>
	public sealed class IOCPThreadPool
	{
		#region Win32 Function Prototypes

		/// <summary> Win32Func: Create an IO Completion Port Thread Pool </summary>
		[DllImport("Kernel32", CharSet = CharSet.Auto)]
		private unsafe static extern UInt32 CreateIoCompletionPort(UInt32 hFile, UInt32 hExistingCompletionPort, UInt32* puiCompletionKey, UInt32 uiNumberOfConcurrentThreads);

		/// <summary> Win32Func: Closes an IO Completion Port Thread Pool </summary>
		[DllImport("Kernel32", CharSet = CharSet.Auto)]
		private unsafe static extern Boolean CloseHandle(UInt32 hObject);

		/// <summary> Win32Func: Posts a context based event into an IO Completion Port Thread Pool </summary>
		[DllImport("Kernel32", CharSet = CharSet.Auto)]
		private unsafe static extern Boolean PostQueuedCompletionStatus(UInt32 hCompletionPort, UInt32 uiSizeOfArgument, UInt32* puiUserArg, OVERLAPPED* pOverlapped);

		/// <summary> Win32Func: Waits on a context based event from an IO Completion Port Thread Pool.
		///           All threads in the pool wait in this Win32 Function </summary>
		[DllImport("Kernel32", CharSet = CharSet.Auto)]
		private unsafe static extern Boolean GetQueuedCompletionStatus(UInt32 hCompletionPort, UInt32* pSizeOfArgument, UInt32* puiUserArg, OVERLAPPED** ppOverlapped, UInt32 uiMilliseconds);

		#endregion

		#region Constants

		/// <summary> SimTypeConst: This represents the Win32 Invalid Handle Value Macro </summary>
		private const UInt32 INVALID_HANDLE_VALUE = 0xffffffff;

		/// <summary> SimTypeConst: This represents the Win32 INFINITE Macro </summary>
		private const UInt32 INIFINITE = 0xffffffff;

		/// <summary> SimTypeConst: This tells the IOCP Function to shutdown </summary>
		private const Int32 SHUTDOWN_IOCPTHREAD = 0x7fffffff;

		#endregion

		#region Delegate Function Types

		/// <summary> DelType: This is the type of user function to be supplied for the thread pool </summary>
		public delegate void USER_FUNCTION(Int32 iValue);

		#endregion

		#region Private Properties

		/// <summary> SimType: Contains the IO Completion Port Thread Pool handle for this instance </summary>
		private UInt32 Handle;

		/// <summary> SimType: The maximum number of threads that may be running at the same time </summary>
		private Int32 MaxConcurrency;

		/// <summary> SimType: The minimal number of threads the thread pool maintains </summary>
		private Int32 MinThreadsInPool;

		/// <summary> SimType: The maximum number of threads the thread pool maintains </summary>
		private Int32 MaxThreadsInPool;

		/// <summary> RefType: A serialization object to protect the class state </summary>
		private Object CriticalSection;

		/// <summary> DelType: A reference to a user specified function to be call by the thread pool </summary>
		private USER_FUNCTION UserFunction;

		/// <summary> SimType: Flag to indicate if the class is disposing </summary>
		private Boolean IsDisposed;

		#endregion

		#region Public Properties

        private long _CurThreadsInPool = 0;
        
		/// <summary> SimType: The current number of threads in the thread pool </summary>
		public long CurThreadsInPool { get { return Interlocked.Read(ref _CurThreadsInPool); } }
		/// <summary> SimType: Increment current number of threads in the thread pool </summary>
		private long IncCurThreadsInPool() { return Interlocked.Increment(ref _CurThreadsInPool); }
		/// <summary> SimType: Decrement current number of threads in the thread pool </summary>
		private long DecCurThreadsInPool() { return Interlocked.Decrement(ref _CurThreadsInPool); }

        
        private long _ActThreadsInPool = 0;
        
		/// <summary> SimType: The current number of active threads in the thread pool </summary>
		public long ActThreadsInPool { get { return Interlocked.Read(ref _ActThreadsInPool); } }
		/// <summary> SimType: Increment current number of active threads in the thread pool </summary>
		private long IncActThreadsInPool() { return Interlocked.Increment(ref _ActThreadsInPool); }
		/// <summary> SimType: Decrement current number of active threads in the thread pool </summary>
		private long DecActThreadsInPool() { return Interlocked.Decrement(ref _ActThreadsInPool); }

        
        private long _CurWorkInPool = 0;
        
		/// <summary> SimType: The current number of Work posted in the thread pool </summary>
		public long CurWorkInPool { get { return Interlocked.Read(ref _CurWorkInPool); } }
		/// <summary> SimType: Increment current number of Work posted in the thread pool </summary>
		private long IncCurWorkInPool() { return Interlocked.Increment(ref _CurWorkInPool); }
		/// <summary> SimType: Decrement current number of Work posted in the thread pool </summary>
		private long DecCurWorkInPool() { return Interlocked.Decrement(ref _CurWorkInPool); }

		#endregion

		#region Constructor

		/// <summary> Constructor </summary>
		/// <param name = "iMaxConcurrency"> SimType: Max number of running threads allowed </param>
		/// <param name = "iMinThreadsInPool"> SimType: Min number of threads in the pool </param>
		/// <param name = "iMaxThreadsInPool"> SimType: Max number of threads in the pool </param>
		/// <param name = "pfnUserFunction"> DelType: Reference to a function to call to perform work </param>
        /// <param name="threadPriority"></param>
		/// <exception cref = "Exception"> Unhandled Exception </exception>
		public IOCPThreadPool(Int32 iMaxConcurrency, Int32 iMinThreadsInPool, Int32 iMaxThreadsInPool, USER_FUNCTION pfnUserFunction, ThreadPriority threadPriority=ThreadPriority.Normal)
		{
			try
			{
				// Set initial class state
				MaxConcurrency = iMaxConcurrency;
				MinThreadsInPool = iMinThreadsInPool;
				MaxThreadsInPool = iMaxThreadsInPool;
				UserFunction = pfnUserFunction;

				// Initialize the Monitor Object
				CriticalSection = new Object();

				// Set the disposing flag to false
				IsDisposed = false;

				unsafe
				{
					// Create an IO Completion Port for Thread Pool use
					Handle = CreateIoCompletionPort(INVALID_HANDLE_VALUE, 0, null, (UInt32)MaxConcurrency);
				}

				// Test to make sure the IO Completion Port was created
				if (Handle == 0)
					throw new Exception("Unable To Create IO Completion Port");

				// Allocate and start the Minimum number of threads specified
				long iStartingCount = CurThreadsInPool;

				ThreadStart tsThread = new ThreadStart(IOCPFunction);
				for (long iThread = 0; iThread < MinThreadsInPool; ++iThread)
				{
					// Create a thread and start it
					Thread thThread = new Thread(tsThread);
					thThread.Name = "IOCP";
                    thThread.Priority = threadPriority;
					thThread.Start();

					// Increment the thread pool count
					IncCurThreadsInPool();
				}
			}

			catch
			{
				throw new Exception("Unhandled Exception");
			}
		}

		//********************************************************************
		/// <summary> Called when the object will be shutdown.  This
		///           function will wait for all of the work to be completed
		///           inside the queue before completing </summary>
		public void Dispose()
		{
			try
			{
				// Flag that we are disposing this object
				IsDisposed = true;

				// Get the current number of threads in the pool
				long iCurThreadsInPool = CurThreadsInPool;

				// Shutdown all thread in the pool
				for (long iThread = 0; iThread < iCurThreadsInPool; ++iThread)
				{
					unsafe
					{
						bool bret = PostQueuedCompletionStatus(Handle, 4, (UInt32*)SHUTDOWN_IOCPTHREAD, null);
					}
				}

				// Wait here until all the threads are gone
				while (CurThreadsInPool != 0) Thread.Sleep(100);

				unsafe
				{
					// Close the IOCP Handle
					CloseHandle(Handle);
				}
			}

			catch
			{
			}
		}

		#endregion

		#region Private Methods

		/// <summary> IOCP Worker Function that calls the specified user function </summary>
		private void IOCPFunction()
		{
			UInt32 uiNumberOfBytes;
			Int32 iValue;

			try
			{
				while (true)
				{
					unsafe
					{
						OVERLAPPED* pOv;

						// Wait for an event
						GetQueuedCompletionStatus(Handle, &uiNumberOfBytes, (UInt32*)&iValue, &pOv, INIFINITE);
					}

					// Decrement the number of events in queue
					DecCurWorkInPool();

					// Should this thread shutdown
					if ((IsDisposed == true) || (iValue == SHUTDOWN_IOCPTHREAD))
						break;

					// Increment the number of active threads
					IncActThreadsInPool();

					try
					{
						// Call the user function
						UserFunction(iValue);
					}

					catch
					{
					}

					// Get a lock
					lock (CriticalSection)
					{
						// If we have less than max threads currently in the pool
						if (CurThreadsInPool < MaxThreadsInPool)
						{
							// Should we add a new thread to the pool
							if (ActThreadsInPool == CurThreadsInPool)
							{
								if (IsDisposed == false)
								{
									// Create a thread and start it
									ThreadStart tsThread = new ThreadStart(IOCPFunction);
									Thread thThread = new Thread(tsThread);
									thThread.Name = string.Format("IOCP {0}", thThread.GetHashCode());
									thThread.Start();

									// Increment the thread pool count
									IncCurThreadsInPool();
								}
							}
						}
					}

					// Increment the number of active threads
					DecActThreadsInPool();
				}
			}

			catch
			{
			}

			// Decrement the thread pool count
			DecCurThreadsInPool();
		}

		#endregion

		#region Public Methods

		/// <summary> IOCP Worker Function that calls the specified user function </summary>
		/// <exception cref = "Exception"> Unhandled Exception </exception>
		public void PostEvent()
		{
			try
			{
				// Only add work if we are not disposing
				if (IsDisposed == false)
				{
					unsafe
					{
						// Post an event into the IOCP Thread Pool
						PostQueuedCompletionStatus(Handle, 0, null, null);
					}

					// Increment the number of item of work
					IncCurWorkInPool();

					// Get a lock
					lock (CriticalSection)
					{
						// If we have less than max threads currently in the pool
						if (CurThreadsInPool < MaxThreadsInPool)
						{
							// Should we add a new thread to the pool
							if (ActThreadsInPool == CurThreadsInPool)
							{
								if (IsDisposed == false)
								{
									// Create a thread and start it
									ThreadStart tsThread = new ThreadStart(IOCPFunction);
									Thread thThread = new Thread(tsThread);
									thThread.Name = string.Format("IOCP {0}", thThread.GetHashCode());
									thThread.Start();

									// Increment the thread pool count
									IncCurThreadsInPool();
								}
							}
						}
					}
				}
			}

			catch (Exception e)
			{
				throw e;
			}
		}

		//********************************************************************
		/// <summary> IOCP Worker Function that calls the specified user function </summary>
		/// <param name="iValue"> SimType: A value to be passed with the event </param>
		/// <exception cref = "Exception"> Unhandled Exception </exception>
		public void PostEvent(Int32 iValue)
		{
			try
			{
				// Only add work if we are not disposing
				if (IsDisposed == false)
				{
					unsafe
					{
						// Post an event into the IOCP Thread Pool
						PostQueuedCompletionStatus(Handle, 4, (UInt32*)iValue, null);
					}

					// Increment the number of item of work
					IncCurWorkInPool();

					// Get a lock
					lock (CriticalSection)
					{
						// If we have less than max threads currently in the pool
						if (CurThreadsInPool < MaxThreadsInPool)
						{
							// Should we add a new thread to the pool
							if (ActThreadsInPool == CurThreadsInPool)
							{
								if (IsDisposed == false)
								{
									// Create a thread and start it
									ThreadStart tsThread = new ThreadStart(IOCPFunction);
									Thread thThread = new Thread(tsThread);
									thThread.Name = string.Format("IOCP {0}", thThread.GetHashCode());
									thThread.Start();

									// Increment the thread pool count
									IncCurThreadsInPool();
								}
							}
						}
					}
				}
			}

			catch (Exception e)
			{
				throw e;
			}
		}

		#endregion
	}

	#endregion
}