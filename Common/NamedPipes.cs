#region Namespaces

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Pipes;
using System.Threading;

#endregion

namespace ArdanStudios.Common
{
    #region Class - NamedPipeClient

    /// <summary> Provides named pipe client support </summary>
    public class NamedPipeClient : IDisposable
    {
        #region Delegate Function Types

        /// <summary> Called when a message is extracted from the namedPipe </summary>
        public delegate void MESSAGE_HANDLER(NamedPipeClient namedPipe);

        /// <summary> Called when a socket connection is namedPipe </summary>
        public delegate void CLOSE_HANDLER(NamedPipeClient namedPipe);

        /// <summary> Called when a namedPipe error occurs </summary>
        public delegate void ERROR_HANDLER(NamedPipeClient namedPipe, Exception exception);

        #endregion

        #region Private Properties

        /// <summary> Flag when disposed is called </summary>
        private bool Disposed = false;

        /// <summary> The server this pipe belongs to </summary>
        private NamedPipeServer NamedPipeServer = null;

        /// <summary> RetType: A callback object for processing recieved socket data </summary>	    
        private AsyncCallback CallbackReadFunction = null;

        /// <summary> RetType: A callback object for processing send socket data </summary>
        private AsyncCallback CallbackWriteFunction;

        /// <summary> A reference to a user supplied function to be called when a socket message arrives </summary>
        private MESSAGE_HANDLER MessageHandler = null;

        /// <summary> A reference to a user supplied function to be called when a socket connection is closed </summary>
        private CLOSE_HANDLER CloseHandler = null;

        /// <summary> A reference to a user supplied function to be called when a socket error occurs </summary>
        private ERROR_HANDLER ErrorHandler = null;

        #endregion

        #region Public Properties

        /// <summary> The name of the pipe </summary>
        public string PipeName = null;

        /// <summary> The named pipe stream for sendnig and receiving messages </summary>
        public PipeStream PipeStream = null;

        /// <summary> A raw buffer to capture data comming off the socket </summary>
        public byte[] RawBuffer = null;

        /// <summary> Size of the raw buffer for received socket data </summary>
        public int SizeOfRawBuffer = 0;

        /// <summary> The length of the message </summary>
        public int MessageLength = 0;

        /// <summary> A string buffer to be used by the application developer </summary>
        public StringBuilder StringBuffer = null;

        /// <summary> A byte buffer to be used by the application developer </summary>
        public byte[] ByteBuffer = null;

        /// <summary> The number of bytes that have been buffered </summary>
        public int BufferedBytes = 0;
        
        /// <summary> A memory stream buffer to be used by the application developer </summary>
        public MemoryStream MessageBuffer = null;

        #endregion

        #region User Defined Public Properties

        /// <summary> A reference to a user defined object to be passed through the handler functions </summary>
        public object UserArg = null;

        #endregion

        #region Constructor

        /// <summary> Constructor for client support </summary>
        /// <param name="sizeOfRawBuffer"> The size of the raw buffer </param>
        /// <param name="userArg"> A Reference to the Users arguments </param>
        /// <param name="messageHandler"> Reference to the user defined message handler function </param>
        /// <param name="closeHandler"> Reference to the user defined close handler function </param>
        /// <param name="errorHandler"> Reference to the user defined error handler function </param>
        public NamedPipeClient(int sizeOfRawBuffer, object userArg,
                               MESSAGE_HANDLER messageHandler, CLOSE_HANDLER closeHandler, ERROR_HANDLER errorHandler)
        {
            // Create the raw buffer
            SizeOfRawBuffer = sizeOfRawBuffer;
            RawBuffer = new byte[SizeOfRawBuffer];

            // Save the user argument
            UserArg = userArg;

            // Allocate a String Builder class for Application developer use
            StringBuffer = new StringBuilder();

            // Allocate a ByteBuffer class for Application developer use
            ByteBuffer = new byte[SizeOfRawBuffer];
            
            // Allocate a String Builder class for Application developer use
            MessageBuffer = new MemoryStream();

            // Set the handler functions
            MessageHandler = messageHandler;
            CloseHandler = closeHandler;
            ErrorHandler = errorHandler;

            // Set the async socket function handlers
            CallbackReadFunction = new AsyncCallback(ReceiveComplete);
            CallbackWriteFunction = new AsyncCallback(SendComplete);
        }

        /// <summary> Constructor for server support </summary>
        /// <param name="namedPipeServer"></param>
        /// <param name="pipeStream"></param>
        /// <param name="sizeOfRawBuffer"></param>
        /// <param name="userArg"></param>
        /// <param name="messageHandler"></param>
        /// <param name="closeHandler"></param>
        /// <param name="errorHandler"></param>
        public NamedPipeClient(NamedPipeServer namedPipeServer, PipeStream pipeStream, int sizeOfRawBuffer, object userArg,
                               MESSAGE_HANDLER messageHandler, CLOSE_HANDLER closeHandler, ERROR_HANDLER errorHandler)
        {
            NamedPipeServer = namedPipeServer;
            PipeStream = pipeStream;

            // Create the raw buffer
            SizeOfRawBuffer = sizeOfRawBuffer;
            RawBuffer = new byte[SizeOfRawBuffer];

            // Save the user argument
            UserArg = userArg;

            // Allocate a String Builder class for Application developer use
            StringBuffer = new StringBuilder();

            // Allocate a ByteBuffer class for Application developer use
            ByteBuffer = new byte[SizeOfRawBuffer];
            
            // Allocate a String Builder class for Application developer use
            MessageBuffer = new MemoryStream();

            // Set the handler functions
            MessageHandler = messageHandler;
            CloseHandler = closeHandler;
            ErrorHandler = errorHandler;

            // Set the async socket function handlers
            CallbackReadFunction = new AsyncCallback(ReceiveComplete);
            CallbackWriteFunction = new AsyncCallback(SendComplete);
        }

        /// <summary> Dispose </summary>
        public void Dispose()
        {
            try
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            catch
            {
            }
        }
        
        /// <summary> Dispose the server </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (!Disposed)
            {
                // Note disposing has been done.
                Disposed = true;

                // If disposing equals true, dispose all managed
                // and unmanaged resources.
                if (disposing)
                {
                    try
                    {
                        // Remove the named pipe from the list
                        if (NamedPipeServer != null)
                        {
                            NamedPipeServer.RemoveNamedPipe(this);
                        }

                        Disconnect();
                    }

                    catch
                    {
                    }
                }
            }
        }

        #endregion

        #region Private Methods

        /// <summary> Called when a message arrives </summary>
        /// <param name="ar"> An async result interface </param>
        private void ReceiveComplete(IAsyncResult ar)
        {
            try
            {
                if (Thread.CurrentThread.Name == null)
                {
                    Thread.CurrentThread.Name = "NetThreadPool";
                }

                // Is the Network Stream object valid
                if ((PipeStream != null) && (PipeStream.CanRead))
                {
                    // Read the current bytes from the stream buffer
                    MessageLength = PipeStream.EndRead(ar);

                    // If there are bytes to process else the connection is lost
                    if (MessageLength > 0)
                    {
                        try
                        {
                            // A message came in send it to the MessageHandler
                            MessageHandler(this);
                        }

                        catch
                        {
                        }

                        // Wait for a new message
                        Receive();
                    }
                    else
                    {
                        if (PipeStream != null)
                        {
                            Dispose();
                        }
                        
                        // Call the close handler
                        CloseHandler(this);
                    }
                }
                else
                {
                    if (PipeStream != null)
                    {
                        Dispose();
                    }
                    
                    // Call the close handler
                    CloseHandler(this);
                }
            }

            catch (Exception exception)
            {
                if (PipeStream != null)
                {
                    Dispose();
                    
                    if ((!exception.Message.Contains("forcibly closed")) &&
                        (!exception.Message.Contains("thread exit")))
                    {
                        ErrorHandler(this, exception);
                    }
                }
                
                // Call the close handler
                CloseHandler(this);
            }

            ar.AsyncWaitHandle.Close();
        }

        /// <summary> Called when a message is sent </summary>
        /// <param name="ar"> An async result interface </param>
        private void SendComplete(IAsyncResult ar)
        {
            try
            {
                if (Thread.CurrentThread.Name == null)
                {
                    Thread.CurrentThread.Name = "NetThreadPool";
                }

                // Is the Network Stream object valid
                if ((PipeStream != null) && (PipeStream.CanWrite))
                {
                    PipeStream.EndWrite(ar);
                }
            }

            catch
            {
            }

            ar.AsyncWaitHandle.Close();
        }

        #endregion

        #region Public Methods

        /// <summary> Function used to connect to a server </summary>
        /// <param name="pipeName"> The address to connect to </param>
        public void Connect(string pipeName)
        {
            if (PipeStream == null)
            {
                // Set the pipe name
                PipeName = pipeName;
                    
                // Create the client stream
                PipeStream = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

                // Try to connect within 5 seconds
                ((NamedPipeClientStream) PipeStream).Connect(5000);

                // Did we connect
                if (PipeStream.IsConnected == false)
                {
                    throw new Exception(string.Format("Unable To Connect To Named Pipe [{0}]", pipeName));
                }

                // Start to receive messages
                Receive();
            }
        }

        /// <summary> Called to disconnect the named pipe </summary>
        public void Disconnect()
        {
            // Set when this socket came from a SocketServer Accept
            if (PipeStream != null)
            {
                PipeStream.Dispose();
            }

            // Clean up the connection state
            PipeStream = null;
        }

        /// <summary> Function to send a string </summary>
        /// <param name="message"> A string to send </param>
        public void Send(string message)
        {
            try
            {
                if ((PipeStream != null) && (PipeStream.CanWrite))
                {
                    // Convert the string into a Raw Buffer
                    byte[] rawBuffer = System.Text.Encoding.ASCII.GetBytes(message);

                    // Issue an asynchronus write
                    PipeStream.BeginWrite(rawBuffer, 0, rawBuffer.Length, CallbackWriteFunction, null);
                }
                else
                {
                    throw new Exception("No Connection");
                }
            }
            
            catch
            {
                Dispose();
                
                throw;
            }
        }

        /// <summary> Method to send a byte </summary>
        /// <param name="rawBuffer"></param>
        public void Send(byte[] rawBuffer)
        {
            try
            {
                if ((PipeStream != null) && (PipeStream.CanWrite))
                {
                    // Issue an asynchronus write
                    PipeStream.BeginWrite(rawBuffer, 0, rawBuffer.Length, CallbackWriteFunction, null);
                }
                else
                {
                    throw new Exception("No Connection");
                }
            }
            
            catch
            {
                Dispose();
                
                throw;
            }
        }

        /// <summary> Method to send a char </summary>
        /// <param name="charValue"></param>
        public void Send(char charValue)
        {
            try
            {
                if ((PipeStream != null) && (PipeStream.CanWrite))
                {
                    // Convert the character to a byte
                    byte[] pRawBuffer = { Convert.ToByte(charValue) };

                    // Issue an asynchronus write
                    PipeStream.BeginWrite(pRawBuffer, 0, pRawBuffer.Length, CallbackWriteFunction, null);
                }
                else
                {
                    throw new Exception("No Connection");
                }
            }
            
            catch
            {
                Dispose();
                
                throw;
            }
        }

        /// <summary> Wait for a message to arrive </summary>
        public void Receive()
        {
            if ((PipeStream != null) && (PipeStream.CanRead))
            {
                // Issue an asynchronous read
                PipeStream.BeginRead(RawBuffer, 0, SizeOfRawBuffer, CallbackReadFunction, null);
            }
            else
            {
                throw new Exception("Unable To Read From Stream");
            }
        }

        #endregion
    }

    #endregion

    #region Class - NamedPipeServer

    /// <summary> Provides named pipe server support </summary>
    public class NamedPipeServer : IDisposable
    {
        #region Delagate Function Types

        /// <summary> Called when a message is extracted from the named pipe </summary>
        public delegate void MESSAGE_HANDLER(NamedPipeClient namedPipeClient);

        /// <summary> Called when a socket connection is named pipe </summary>
        public delegate void CLOSE_HANDLER(NamedPipeClient namedPipeClient);

        /// <summary> Called when a named pipe error occurs </summary>
        public delegate void ERROR_HANDLER(NamedPipeClient namedPipeClient, Exception exception);

        /// <summary> Called when a named pipe connection is accepted </summary>
        public delegate void ACCEPT_HANDLER(NamedPipeClient namedPipeClient);

        #endregion

        #region Private Properties

        /// <summary> Flag when disposed is called </summary>
        private bool Disposed = false;

        /// <summary> The next server stream for connections </summary>
        private NamedPipeServerStream NextServerStream = null;

        /// <summary> Size of the raw buffer for received socket data </summary>
        private int SizeOfRawBuffer;

        /// <summary> A thread to process accepting socket connections </summary>
        private Thread AcceptThread = null;

        /// <summary> A reference to a user supplied function to be called when a socket message arrives </summary>
        private MESSAGE_HANDLER MessageHandler = null;

        /// <summary> A reference to a user supplied function to be called when a socket connection is closed </summary>
        private CLOSE_HANDLER CloseHandler = null;

        /// <summary> A reference to a user supplied function to be called when a socket error occurs </summary>
        private ERROR_HANDLER ErrorHandler = null;

        /// <summary> A reference to a user supplied function to be called when a socket connection is accepted </summary>
        private ACCEPT_HANDLER AcceptHandler = null;
        
        /// <summary> An list of NamedPipeClient objects </summary>
        private List<NamedPipeClient> NamedPipeClientList = new List<NamedPipeClient>();

        /// <summary> Holds the accept thread when we reach the max number of server instances </summary>
        private AutoResetEvent MaxServerInstancesEvent = new AutoResetEvent(false);

        #endregion

        #region Public Properties

        /// <summary> The name of the pipe </summary>
        public string PipeName = null;

        /// <summary> A reference to a user defined object to be passed through the handler functions </summary>
        public object UserArg = null;

        #endregion

        #region Constructor

        /// <summary> Constructor </summary>
        public NamedPipeServer()
        {
        }

        /// <summary> Dispose function to shutdown the SocketManager </summary>
        public void Dispose()
        {
            try
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            catch
            {
            }
        }
        
        /// <summary> Dispose the server </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (!Disposed)
            {
                // Note disposing has been done.
                Disposed = true;

                // If disposing equals true, dispose all managed
                // and unmanaged resources.
                if (disposing)
                {
                    // Stop the server if the thread is running
                    if (AcceptThread != null)
                    {
                        Stop();
                    }
                }
            }
        }

        #endregion

        #region Private Methods

        /// <summary> Function to process and accept socket connection requests </summary>
        private void AcceptConnections()
        {
            try
            {
                for (;;)
                {
                    // Init the next reference
                    NextServerStream = null;

                    // How many server instances do we have right now
                    int serverInstances = 0;
                    lock (NamedPipeClientList)
                    {
                        serverInstances = NamedPipeClientList.Count;
                    }

                    // If we have a max number of server instances wait for one to open up
                    if (serverInstances == NamedPipeServerStream.MaxAllowedServerInstances)
                    {
                        // Wait for a server instance to be available
                        MaxServerInstancesEvent.WaitOne();
 
                        // Are we shutting down
                        if (Disposed == true)
                        {
                            break;
                        }
                    }

                    // Create the named pipe server stream
                    NextServerStream = new NamedPipeServerStream(PipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                    try
                    {
                        // If a client connects, accept the connection.
                        IAsyncResult iAsyncResult = NextServerStream.BeginWaitForConnection(null, null);
                        NextServerStream.EndWaitForConnection(iAsyncResult);

                        lock (NamedPipeClientList)
                        {
                            NamedPipeClient namedPipeClient = null;

                            try
                            {
                                // Create a client object for this connection
                                namedPipeClient = new NamedPipeClient(this, NextServerStream, 10240, UserArg,
                                                                      new NamedPipeClient.MESSAGE_HANDLER(MessageHandler),
                                                                      new NamedPipeClient.CLOSE_HANDLER(CloseHandler),
                                                                      new NamedPipeClient.ERROR_HANDLER(ErrorHandler));

                                // Add this client to the list
                                NamedPipeClientList.Add(namedPipeClient);
                            
                                // Called the user method
                                AcceptHandler(namedPipeClient);

                                // Wait for a message
                                namedPipeClient.Receive();
                            }

                            catch (Exception exception)
                            {
                                // Call the error handler
                                ErrorHandler(null, exception);
                                ErrorHandler(null, new Exception("Waiting for new connection 1"));

                                if (namedPipeClient != null)
                                {
                                    namedPipeClient.Dispose();
                                }
                            }
                        }
                    }

                    catch (Exception exception)
                    {
                        if (exception.Message.Contains("ended") != true)
                        {
                            // Call the error handler
                            ErrorHandler(null, exception);
                            ErrorHandler(null, new Exception("Waiting for new connection 2"));
                        }

                        break;
                    }
                }
            }

            catch (Exception exception)
            {
                // Call the error handler
                ErrorHandler(null, exception);
                ErrorHandler(null, new Exception("Shutting Down Accept Thread"));
            }
        }

        #endregion

        #region Public Methods

        /// <summary> Function to start the NamedPipeServer </summary>
        /// <param name="pipeName"> The name of the pipe </param>
        /// <param name="sizeOfRawBuffer"> Size of the Raw Buffer </param>
        /// <param name="userArg"> User supplied arguments </param>
        /// <param name="messageHandler"> Function pointer to the user MessageHandler function </param>
        /// <param name="acceptHandler"> Function pointer to the user AcceptHandler function </param>
        /// <param name="closeHandler"> Function pointer to the user CloseHandler function </param>
        /// <param name="errorHandler"> Function pointer to the user ErrorHandler function </param>
        public void Start(string pipeName, int sizeOfRawBuffer, object userArg,
                          MESSAGE_HANDLER messageHandler, ACCEPT_HANDLER acceptHandler, CLOSE_HANDLER closeHandler,
                          ERROR_HANDLER errorHandler)
        {
            // Is an AcceptThread currently running
            if (AcceptThread == null)
            {
                // Set connection values
                PipeName = pipeName;

                // Save the Handler Functions
                MessageHandler = messageHandler;
                AcceptHandler = acceptHandler;
                CloseHandler = closeHandler;
                ErrorHandler = errorHandler;

                // Save the buffer size and user arguments
                SizeOfRawBuffer = sizeOfRawBuffer;
                UserArg = userArg;

                // Start the listening thread if one is currently not running
                ThreadStart tsThread = new ThreadStart(AcceptConnections);
                AcceptThread = new Thread(tsThread);
                AcceptThread.Name = string.Format("NamedPipeAccept-{0}", pipeName);
                AcceptThread.Start();
            }
        }

        /// <summary> Function to stop the NamedPipeServer.  It can be restarted with Start </summary>
        public void Stop()
        {
            // Abort the accept thread
            if (AcceptThread != null)
            {
                // Wake up the thread if it is waiting for more server instances
                MaxServerInstancesEvent.Set();

                // Wake up the thread if it is waiting for a connection
                if (NextServerStream != null)
                {
                    NextServerStream.Dispose();
                }

                // Wait for the thread to die
                AcceptThread.Join();
                AcceptThread = null;
            }

            lock (NamedPipeClientList)
            {
                // Dispose of all of the named pipe connections
                foreach (NamedPipeClient namedPipeClient in NamedPipeClientList)
                {
                    namedPipeClient.Dispose();
                }
            }

            // Wait for all of the socket client objects to be destroyed
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Empty the Socket Client List
            NamedPipeClientList.Clear();

            // Clear the Handler Functions
            MessageHandler = null;
            AcceptHandler = null;
            CloseHandler = null;
            ErrorHandler = null;

            // Clear the buffer size and user arguments
            SizeOfRawBuffer = 0;
            UserArg = null;
        }

        /// <summary> Funciton to remove a named pipe client from the list of named pipe clients </summary>
        /// <param name="namedPipeClient"> A reference to a name pipe client to remove </param>
        public void RemoveNamedPipe(NamedPipeClient namedPipeClient)
        {
            try
            {
                bool resetEvent = false;

                lock (NamedPipeClientList)
                {
                    // Are we at capacity right now and need to start the accept thread back up
                    if (NamedPipeClientList.Count == NamedPipeServerStream.MaxAllowedServerInstances)
                    {
                        resetEvent = true;
                    }

                    // Remove ths client socket object from the list
                    NamedPipeClientList.Remove(namedPipeClient);
                }

                if (resetEvent == true)
                {
                    // Tell the accept thread to wait for a new connection
                    MaxServerInstancesEvent.Set();
                }
            }

            catch (Exception exception)
            {
                ErrorHandler(namedPipeClient, exception);
            }
        }

        /// <summary> Called to send a message to call socket clients </summary>
        /// <param name="message"></param>
        public void SendAll(string message)
        {
            lock (NamedPipeClientList)
            {
                // If the server index exists, return it
                foreach (NamedPipeClient namedPipeClient in NamedPipeClientList)
                {
                    namedPipeClient.Send(message);
                }
            }
        }
        
        #endregion
    }

    #endregion
}