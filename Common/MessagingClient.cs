#region Namespaces

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Configuration;

#endregion

namespace ArdanStudios.Common
{
    #region ResponseTypes Enumeration

    /// <summary> A set of response types for the response data </summary>
    public enum ResponseTypes
    {
        /// <summary> The response is JSON notation </summary>
        JSON = 1,

        /// <summary> The response is XML notation </summary>
        XML = 2,

        /// <summary> The response is plain TEXT </summary>
        TEXT = 3,

        /// <summary> The response is HTML notation </summary>
        HTML = 4,

        /// <summary> The response respresents an ERROR </summary>
        ERROR = 5,
        
        /// <summary> The response respresents a balnk response or Void Method </summary>
        NONE = 6
    }

    #endregion

    #region CommandTypes Enumeration

    /// <summary> A set of execution types for the execution of the command</summary>
    public enum ExecutionTypes
    {
        /// <summary> The command does not require gaurenteed execution. Only for this call. </summary>
        Transient = 1,

        /// <summary> The command requires gaurenteed execution. Regardless of this call. </summary>
        Persistent = 2
    }

    #endregion

    #region CommandResponse Class

    /// <summary> This type is passed between the notify icon apps and the servers </summary>
    [DataContract]
    public class CommandResponse
    {
        #region Public Properties

        /// <summary> The unique that was set by the caller </summary>
        [DataMember]
        public long UniqueKey { get; set; }

        /// <summary> A code to determine if the state of the response </summary>
        [DataMember]
        public int ResponseCode { get; set; }

        /// <summary> The type of response such as JSON, XML, Text, HTML </summary>
        [DataMember]
        public ResponseTypes ResponseType { get; set; }

        /// <summary> The response message data </summary>
        [DataMember]
        public string Response { get; set; }

        /// <summary> When stored in a database, the unique identifer </summary>
        [DataMember]
        public long? DatabaseId { get; set; }

        /// <summary> The Company processing the command </summary>
        [DataMember]
        public int CompanyId { get; set; }

        #endregion

        #region Constructor

        /// <summary> Constructor </summary>
        /// <param name="commandMessage"></param>
        /// <param name="responseCode"></param>
        /// <param name="responseType"></param>
        /// <param name="response"></param>
        public CommandResponse(CommandMessage commandMessage, int responseCode, ResponseTypes responseType, string response)
        {
            UniqueKey = (commandMessage != null) ? commandMessage.UniqueKey : UniqueKeyGenerator.Next;
            DatabaseId = (commandMessage != null) ? commandMessage.DatabaseId : null;
            CompanyId = commandMessage.CompanyId;

            ResponseCode = responseCode;
            ResponseType = responseType;
            Response = response;
        }

        #endregion

        #region Public Methods

        /// <summary> Called to display a log string </summary>
        /// <returns>string </returns>
        public string ToLogString()
        {
            string result = null;

            try
            {
                result = string.Format("CompanyId[{0}] ResponseCode[{1}] ResponseType[{2}] Response[{3}]", CompanyId, ResponseCode, ResponseType, Response.Length);
            }

            catch
            {
            }

            return result;
        }

        #endregion
    }

    #endregion

    #region CommandMessage Class

    /// <summary> This type is passed between the notify icon apps and the servers </summary>
    [DataContract]
    public class CommandMessage
    {
        #region Public Properties

        /// <summary> A unique set by the caller for handle message returns </summary>
        [DataMember]
        public long UniqueKey { get; set; }

        /// <summary> The execution requirement of the command </summary>
        [DataMember]
        public ExecutionTypes ExecutionType { get; set; }

        /// <summary> Routing information </summary>
        [DataMember]
		public string Route { get; set; }

        /// <summary> Command to execute </summary>
        [DataMember]
		public string Command { get; set; }
		
        /// <summary> Key value pair of arguments for the command </summary>
		[DataMember]
		public Dictionary<string, string> Arguments { get; set; }

        /// <summary> When stored in a database, the unique identifer </summary>
        [DataMember]
        public long? DatabaseId { get; set; }

        /// <summary> The Company processing the command </summary>
        [DataMember]
        public int CompanyId { get; set; }

        #endregion

        #region Constructor

        /// <summary> Constructor </summary>
        /// <param name="companyId"></param>
        /// <param name="route"></param>
        /// <param name="command"></param>
        public CommandMessage(int companyId, string route, string command)
        {
            UniqueKey = UniqueKeyGenerator.Next;
            ExecutionType = ExecutionTypes.Transient;
            CompanyId = companyId;
            Route = route;
            Command = command;

            Arguments = new Dictionary<string,string>();

            DatabaseId = null;
        }

        /// <summary> Constructor </summary>
        /// <param name="executionType"></param>
        /// <param name="companyId"></param>
        /// <param name="route"></param>
        /// <param name="command"></param>
        public CommandMessage(ExecutionTypes executionType, int companyId, string route, string command)
        {
            UniqueKey = UniqueKeyGenerator.Next;
            ExecutionType = executionType;
            CompanyId = companyId;
            Route = route;
            Command = command;

            Arguments = new Dictionary<string,string>();

            DatabaseId = null;
        }

        #endregion

        #region Public Methods

        /// <summary> Called to display a log string </summary>
        /// <returns>string </returns>
        public string ToLogString()
        {
            string result = null;

            try
            {
                result = string.Format("UniqueKey[{0}] ExecutionType[{1}] CompanyId[{2}] Route[{3}] Command[{4}] Args[{5}]", UniqueKey, ExecutionType, CompanyId, Route, Command, Arguments.Count);
            }

            catch
            {
            }

            return result;
        }

        #endregion
    }

    #endregion

    #region UniqueKeyGenerator Class

    /// <summary> Generates unique keys </summary>
    internal static class UniqueKeyGenerator
    {
        #region Private Properties

        /// <summary> Sequence counter </summary>
        private static long Sequence = 0;

        /// <summary> Random number generator </summary>
        private static Random Rnd = new Random(DateTime.Now.Millisecond);

        #endregion

        #region Constructor

        /// <summary> Static Constructor </summary>
        static UniqueKeyGenerator()
        {
            Sequence = Rnd.Next();
        }

        #endregion

        #region Public Properties

        /// <summary> Called to return a unique key </summary>
        public static long Next
        {
            get
            {
                return Interlocked.Increment(ref Sequence);
            }
        }

        #endregion
    }

    #endregion

    #region MessagingProvider Class

    /// <summary> Provides the ability to send commands to the servers </summary>
    public class MessagingProvider : IDisposable
    {
        #region Delegate Types
        
        /// <summary> Delegate type for method called when a socket connection is accepted </summary>
        public delegate void CONNECTION_ACCEPTED(MessagingProvider provider);

        /// <summary> Delegate type for method called when a socket connection is closed </summary>
        public delegate void CONNECTION_CLOSED(MessagingProvider provider);

        /// <summary> Delegate type for method called when a socket connection has an error </summary>
        public delegate void CONNECTION_ERROR(MessagingProvider provider, Exception exception);

        /// <summary> Delegate type for method called so the client can log consumer code </summary>
        public delegate void CONNECTION_LOG(MessagingProvider provider, string logKey, string message);

        /// <summary> Delegate type for method called when an unsolicited message is received or the waiting thread moved on </summary>
        public delegate void CONNECTION_RESPONSE(MessagingProvider provider, CommandResponse commandResponse);
        
        #endregion

        #region Private Classes

        /// <summary> Maintained in queue waiting for a repsonse </summary>
        private class WaitResponse
        {
            public AutoResetEvent WaitEvent = null;
            public CommandResponse Response = null;
            public long UniqueKey = 0;

            public WaitResponse()
            {
                WaitEvent = new AutoResetEvent(false);
                Response = null;
            }
        }

        #endregion
        
        #region Private Properties
        
        /// <summary> Flag when disposed is called </summary>
        private bool Disposed = false;
        
        /// <summary> Handle to the socket client for hot connections </summary>
        private SocketClient SocketClient = null;

        /// <summary> The server to connect to </summary>
        private string _Server = null;
        
        /// <summary> The port on the server to connect to </summary>
        private int _Port;
        
        /// <summary> Set by the user to handle logging control </summary>
        private string LogKey = null;

        /// <summary> Handle to a thread that maintains a connection to the server </summary>
        private Thread ConnectionThread = null;

        /// <summary> Event to signal the connection thread the connection is down </summary>
        private ManualResetEvent ConnectionEvent = new ManualResetEvent(true);

        /// <summary> A stack of event used to wait for responses </summary>
        private Stack<WaitResponse> WaitResponseStack = new Stack<WaitResponse>();

        /// <summary> Client connections waiting for responses </summary>
        private Dictionary<long, WaitResponse> WaitingResponses = new Dictionary<long, WaitResponse>();
        
        #endregion

        #region Public Properties

        /// <summary> Event method called when a socket connection is accepted </summary>
        public event CONNECTION_ACCEPTED ConnectionAcceptedEvent = null;

        /// <summary> Event method called when a socket connection is closed </summary>
        public event CONNECTION_CLOSED ConnectionClosedEvent = null;

        /// <summary> Event method called when a socket connection has an error </summary>
        public event CONNECTION_ERROR ConnectionErrorEvent = null;

        /// <summary> Event method called so the client can log consumer code </summary>
        public event CONNECTION_LOG ConnectionLogEvent = null;

        /// <summary> Delegate type for method called when an unsolicited response is received or the waiting thread moved on </summary>
        public event CONNECTION_RESPONSE ConnectionResponseEvent = null;

        /// <summary> Read only access to the server </summary>
        public string Server { get { return _Server; } }

        /// <summary> Read only access to the port </summary>
        public int Port { get { return _Port; } }

        #endregion
        
        #region Constructor
        
        /// <summary> Constructor </summary>
        public MessagingProvider()
        {
            // Add ten wait response object to the stack to start
            for (int item = 0; item < 10; ++item)
            {
                WaitResponseStack.Push(new WaitResponse());
            }
        }
        
        /// <summary> Constructor </summary>
        /// <param name="server"></param>
        /// <param name="port"></param>
        /// <param name="logKey"></param>
        public MessagingProvider(string server, int port, string logKey)
        {
            // Set server and port information
            _Server = server;
            _Port = port;
            LogKey = logKey;

            // Add ten wait response object to the stack to start
            for (int item = 0; item < 10; ++item)
            {
                WaitResponseStack.Push(new WaitResponse());
            }

            // Create the socket client
            SocketClient = new SocketClient(1048576, 0, this,
                new SocketClient.MESSAGE_HANDLER(MessageHandler),
                new SocketClient.CLOSE_HANDLER(CloseHandler),
                new SocketClient.ERROR_HANDLER(ErrorHandler));
            
            // Indicate we are not connected yet
            SocketClient.IsAvailable = false;
            
            // Start a thread to get this connection
            ConnectionThread = new Thread(new ThreadStart(PerformConnectClient));
            ConnectionThread.Name = string.Format("MessProv-{0}", server);
            ConnectionThread.Start();
        }
        
        /// <summary> Called to connect to the server </summary>
        private void PerformConnectClient()
        {
            for (;;)
            {
                try
                {
                    // Wait to be told to connect
                    ConnectionEvent.WaitOne();

                    FireConnectionLogEvent(string.Format("ArdanStudios.Servers.MessagingClient.MessagingProvider : PerformConnectClient : Info : Wake Up : Server [{0}] Port [{1}]", _Server, _Port));
                    
                    // Are we shutting down
                    if (Disposed == true)
                    {
                        // Set the handle to null
                        ConnectionThread = null;
                        FireConnectionLogEvent(string.Format("ArdanStudios.Servers.MessagingClient.MessagingProvider : PerformConnectClient : Completed : System Disposing : Server [{0}] Port [{1}]", _Server, _Port));
                        return;
                    }

                    FireConnectionLogEvent(string.Format("ArdanStudios.Servers.MessagingClient.MessagingProvider : PerformConnectClient : Info : Attempting To Connect : Server [{0}] Port [{1}]", _Server, _Port));
                    
                    // Connect to the server
                    SocketClient.Connect(_Server, _Port);
                    
                    // We are now connected
                    SocketClient.IsAvailable = true;

                    // Let the client know the connection is hot                    
                    FireConnectionAcceptedEvent(SocketClient);
                    
                    // We are good to go
                    ConnectionEvent.Reset();
                }
                
                catch (Exception exception)
                {
                    FireConnectionLogEvent(string.Format("ArdanStudios.Servers.MessagingClient.MessagingProvider : PerformConnectClient : ERROR : Server [{0}] Port [{1}] : {2}", _Server, _Port, exception.Message));
                    FireConnectionErrorEvent(SocketClient, exception);
                    
                    // Wait five second an try again
                    Thread.Sleep(5000);
                }
            }
        }
        
        /// <summary> Destructor </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary> Dispose the server </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : Dispose : Started : Server[{0}] Port[{1}]", _Server, _Port);

            // Check to see if Dispose has already been called.
            if (!Disposed)
            {
                // Note disposing has been done
                Disposed = true;

                // If disposing equals true, dispose all managed
                // and unmanaged resources.
                if (disposing)
                {
                    // Tell the thread to wake up
                    if (ConnectionEvent != null)
                    {
                        ConnectionEvent.Set();
                    }
                    
                    if (SocketClient != null)
                    {
                        SocketClient.IsAvailable = false;
                        SocketClient.Dispose();
                    }
                }
            }

            FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : Dispose : Completed : Server[{0}] Port[{1}]", _Server, _Port);
        }
        
        #endregion

        #region Event Methods

        /// <summary> Called to fire an event back to the client when a socket connection is accepted </summary>
        /// <param name="socket"></param>
        private void FireConnectionAcceptedEvent(SocketClient socket)
        {
            try
            {
                if (ConnectionAcceptedEvent != null)
                {
                    ConnectionAcceptedEvent(this);
                }
            }

            catch (Exception exception)
            {
                FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : FireConnectionAcceptedEvent : ERROR : {0}", exception.ToString());
            }
        }

        /// <summary> Called to fire an event back to the client when a socket connection is closed </summary>
        /// <param name="socket"></param>
        private void FireConnectionClosedEvent(SocketClient socket)
        {
            try
            {
                if (ConnectionClosedEvent != null)
                {
                    ConnectionClosedEvent(this);
                }
            }

            catch (Exception exception)
            {
                FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : FireConnectionClosedEvent : ERROR : {0}", exception.ToString());
            }
        }

        /// <summary> Called to fire an event back to the client when a socket connection has an error </summary>
        /// <param name="socket"></param>
        /// <param name="errorException"></param>
        private void FireConnectionErrorEvent(SocketClient socket, Exception errorException)
        {
            try
            {
                if (ConnectionErrorEvent != null)
                {
                    ConnectionErrorEvent(this, errorException);
                }
            }

            catch (Exception exception)
            {
                FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : FireConnectionErrorEvent : ERROR : {0}", exception.ToString());
            }
        }

        /// <summary> Called to fire an event back to the client for logging </summary>
        /// <param name="message"></param>
        private void FireConnectionLogEvent(string message)
        {
            try
            {
                if (ConnectionLogEvent != null)
                {
                    ConnectionLogEvent(this, LogKey, message);
                }
            }

            catch
            {
            }
        }

        /// <summary> Called to fire an event back to the client for logging </summary>
        /// <param name="message"></param>
        /// <param name="parameters"></param>
        private void FireConnectionLogEvent(string message, params object[] parameters)
        {
            try
            {
                if (ConnectionLogEvent != null)
                {
                    ConnectionLogEvent(this, LogKey, string.Format(message, parameters));
                }
            }

            catch
            {
            }
        }

        /// <summary> Called to fire an event back to the client when a response is recieved </summary>
        /// <param name="socket"></param>
        /// <param name="commandResponse"></param>
        private void FireConnectionResponseEvent(SocketClient socket, CommandResponse commandResponse)
        {
            try
            {
                if (ConnectionResponseEvent != null)
                {
                    ConnectionResponseEvent(this, commandResponse);
                }
            }

            catch (Exception exception)
            {
                FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : FireConnectionMessageEvent : ERROR : {0}", exception.ToString());
            }
        }

        #endregion

        #region Handler Methods

        /// <summary> Called when a message is extracted from the socket </summary>
        /// <param name="socket"> The SocketClient object the message came from </param>
        private void MessageHandler(SocketClient socket)
        {
            const int headerLength = 12;

            try
            {
                FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : MessageHandler : Started : Server[{0}] Port[{1}]", _Server, _Port);

                // Extract the data we currently received
                byte[] currentMessage = new byte[socket.MessageLength];
                Array.Copy(socket.RawBuffer, currentMessage, socket.MessageLength);

                // Add this new data to the buffer
                socket.ListBuffer.AddRange(currentMessage);

                // Set the offset and offset data indexes
                int offset = 0;
                int offsetData = headerLength;

                // Process messages that we have
                while (offset < socket.ListBuffer.Count)
                {
                    // Do we have enough bytes left for the header
                    if ((socket.ListBuffer.Count - offset) < headerLength)
                    {
                        // Clear out the bytes in the ListBuffer we used
                        socket.ListBuffer.RemoveRange(0, offset);
                        FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : MessageHandler : Info : ListBuffer[{0}] Offset[{1}] HeaderLength[{2}] : Not Enough Bytes For Header", socket.ListBuffer.Count, offset, headerLength);
                        return;
                    }

                    // Capture the size of this message
                    int marker1 = System.BitConverter.ToInt32(socket.ListBuffer.GetRange(offset, 4).ToArray(), 0);
                    int sizeOfMessage = System.BitConverter.ToInt32(socket.ListBuffer.GetRange(offset + 4, 4).ToArray(), 0);
                    int marker2 = System.BitConverter.ToInt32(socket.ListBuffer.GetRange(offset + 8, 4).ToArray(), 0);

                    FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : MessageHandler : Info : Marker1[{0}] SizeOfMessage[{1}] Marker2[{2}]", marker1, sizeOfMessage, marker2);

                    // Check the markers
                    if ((marker1 != int.MinValue) || (marker2 != int.MaxValue))
                    {
                        // Clear out the bytes in the ListBuffer we used
                        socket.ListBuffer.Clear();
                        FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : MessageHandler : ERROR : OUT OF SYNC : Clear buffer");
                        return;
                    }

                    // Do we have all the data for this next message in the buffer
                    if ((sizeOfMessage + offsetData) > socket.ListBuffer.Count)
                    {
                        // Clear out the bytes in the ListBuffer we used
                        socket.ListBuffer.RemoveRange(0, offset);
                        FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : MessageHandler : Info : SizeOfMessage[{0}] OffsetData[{1}] ListBuffer[{2}] : Not Enough Bytes For Data", sizeOfMessage, offsetData, socket.ListBuffer.Count);
                        return;
                    }

                    // Capture the data
                    string message = System.Text.Encoding.UTF8.GetString(socket.ListBuffer.GetRange(offsetData, sizeOfMessage).ToArray(), 0, sizeOfMessage);

                    FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : MessageHandler : Info : Message[{0}]", message);

                    // Decrypt the message
                    message = CryptoProvider.DecryptText(message);

                    // Create the command response object from the message
                    CommandResponse commandResponse = (CommandResponse) Serializer.toObject<CommandResponse>(message);

                    if (commandResponse == null)
                    {
                        FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : MessageHandler : ERROR : Unable To Deserialize Command Response");
                    }
                    else
                    {
                        // Find the wait repsonse object for this response
                        WaitResponse waitResponse = null;
                        lock (WaitingResponses)
                        {
                            if (WaitingResponses.ContainsKey(commandResponse.UniqueKey) == true)
                            {
                                waitResponse = WaitingResponses[commandResponse.UniqueKey];

                                // Set the command response object
                                waitResponse.Response = commandResponse;
                        
                                // Signal the waiting thread to wake up
                                waitResponse.WaitEvent.Set();
                            }
                        }

                        // The thread is no longer waiting or this is an unsolicited response
                        if (waitResponse == null)
                        {
                            FireConnectionResponseEvent(socket, commandResponse);
                        }
                    }

                    // Update the offset to the next message
                    offset += (headerLength + sizeOfMessage);
                    offsetData = offset + headerLength;

                    FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : MessageHandler : Info : Offset[{0}] OffsetData[{1}] ListBuffer[{2}]", offset, offsetData, socket.ListBuffer.Count);
                }

                // Clear out the bytes in the ListBuffer we used
                socket.ListBuffer.Clear();
            }

            catch (Exception exception)
            {
                // Clear out the bytes in the ListBuffer we used
                socket.ListBuffer.Clear();

                FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : MessageHandler : ERROR : Server[{0}] Port[{1}] : {2}", _Server, _Port, exception.ToString());
            }

            FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : MessageHandler : Completed : Server[{0}] Port[{1}]", _Server, _Port);
        }

        /// <summary> Called when a socket connection is closed </summary>
        /// <param name="socket"> The SocketClient object the message came from </param>
        private void CloseHandler(SocketClient socket)
        {
            string ipAddress = "None";
            if (socket != null)
            {
                ipAddress = socket.IpAddress;
            }
            
            FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : CloseHandler : Started : Server[{0}] Port[{1}] : IpAddress[{2}]", _Server, _Port, ipAddress);

            try
            {
                // Are we using a hot connection
                if (SocketClient == socket)
                {
                    SocketClient.IsAvailable = false;

                    FireConnectionClosedEvent(socket);

                    // Tell the connection thread to re-connect
                    ConnectionEvent.Set();
                }
                else
                {
                    // We are using an on demand connection
                    socket.Dispose();
                    socket = null;
                }
            }

            catch (Exception exception)
            {
                FireConnectionLogEvent(exception.ToString());
            }

            FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : CloseHandler : Completed : Server[{0}] Port[{1}] : IpAddress[{2}]", _Server, _Port, ipAddress);
        }

        /// <summary> Called when a socket error occurs </summary>
        /// <param name="socket"> The SocketClient object the message came from </param>
        /// <param name="errorException"> The reason for the error </param>
        private void ErrorHandler(SocketClient socket, Exception errorException)
        {
            string ipAddress = "None";
            if (socket != null)
            {
                ipAddress = socket.IpAddress;
            }

            FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : ErrorHandler : Started : Server[{0}] Port[{1}] : IpAddress[{2}]", _Server, _Port, ipAddress);

            try
            {
                FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : ErrorHandler : ERROR : Server[{0}] Port[{1}] : {2}", _Server, _Port, errorException.ToString());
                FireConnectionErrorEvent(socket, errorException);
            }

            catch (Exception exception)
            {
                FireConnectionLogEvent(exception.ToString());
            }

            FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : ErrorHandler : Completed : Server[{0}] Port[{1}] : IpAddress[{2}]", _Server, _Port, ipAddress);
        }

        #endregion
        
        #region Send Methods
        
        /// <summary> Called to send a message to the server with the hot connection </summary>
        /// <param name="commandMessage"></param>
        /// <param name="waitInMilliseconds"></param>
        public CommandResponse SendMessage(CommandMessage commandMessage, int waitInMilliseconds)
        {
            FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : SendMessage : Started : Server[{0}] Port[{1}] WaitInMilliseconds[{2}] Route[{3}] Command[{4}]", _Server, _Port, waitInMilliseconds, commandMessage.Route, commandMessage.Command);

            CommandResponse commandResponse = null;

            try
            {
                // Verify the connection is up
                if ((SocketClient == null) || (SocketClient.IsConnected == false) || (SocketClient.IsAvailable == false))
                {
                    FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : SendMessage : WARNING : Server[{0}] Port[{1}] : IpAddress[{2}] : Socket Client not longer is active", _Server, _Port, SocketClient.IpAddress);
                    return new CommandResponse(commandMessage, 900101, ResponseTypes.TEXT, "Socket Client not longer is active"); 
                }

                // Send the message and wait for the response
                commandResponse = SendMessage(SocketClient, commandMessage, waitInMilliseconds);
            }
            
            catch (Exception exception)
            {
                FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : SendMessage : ERROR : Server[{0}] Port[{1}] : {2}", _Server, _Port, exception.ToString());
                
                commandResponse = new CommandResponse(commandMessage, 900101, ResponseTypes.TEXT, exception.Message); 
            }

            FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : SendMessage : Completed : Server[{0}] Port[{1}] ResponseCode[{2}]", _Server, _Port, commandResponse.ResponseCode);

            return commandResponse;
        }

        /// <summary> Called to send a message to the server with an on demand connection </summary>
        /// <param name="server"></param>
        /// <param name="port"></param>
        /// <param name="commandMessage"></param>
        /// <param name="waitInMilliseconds"></param>
        public CommandResponse SendMessage(string server, int port, CommandMessage commandMessage, int waitInMilliseconds)
        {
            FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : SendMessage : Started : Server[{0}] Port[{1}] WaitInMilliseconds[{2}] Route[{3}] Command[{4}]", _Server, _Port, waitInMilliseconds, commandMessage.Route, commandMessage.Command);

            CommandResponse commandResponse = null;

            try
            {
                // Create a socket client
                SocketClient socket = new SocketClient(1048576, 0, this,
                                            new SocketClient.MESSAGE_HANDLER(MessageHandler),
                                            new SocketClient.CLOSE_HANDLER(CloseHandler),
                                            new SocketClient.ERROR_HANDLER(ErrorHandler));

                // Connect to the specified server and port
                SocketClient.Connect(server, port);

                // Send the message and wait for the response
                commandResponse = SendMessage(socket, commandMessage, waitInMilliseconds);
            }
            
            catch (Exception exception)
            {
                FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : SendMessage : ERROR : Server[{0}] Port[{1}] : {2}", server, port, exception.ToString());

                commandResponse = new CommandResponse(commandMessage, 900101, ResponseTypes.TEXT, exception.Message); 
            }

            FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : SendMessage : Completed : Server[{0}] Port[{1}] ResponseCode[{2}]", server, port, commandResponse.ResponseCode);

            return commandResponse;
        }

        /// <summary> Sends the message and waits for the response </summary>
        /// <param name="socket"></param>
        /// <param name="commandMessage"></param>
        /// <param name="waitInMilliseconds"></param>
        /// <returns> CommandResponse </returns>
        private CommandResponse SendMessage(SocketClient socket, CommandMessage commandMessage, int waitInMilliseconds)
        {
            CommandResponse commandResponse = null;
            WaitResponse waitResponse = null;

            try
            {
                // Convert the response object to JSON
                string message = Serializer.toJSON(commandMessage);

                // Encrypt the message
                message = CryptoProvider.EncryptText(message);

                // Build byte array for the call
                List<byte> dataPacket = new List<byte>();

                // Add the length
                dataPacket.AddRange(BitConverter.GetBytes(int.MinValue));
                dataPacket.AddRange(BitConverter.GetBytes(message.Length));
                dataPacket.AddRange(BitConverter.GetBytes(int.MaxValue));

                // Add the data
                dataPacket.AddRange(Encoding.UTF8.GetBytes(message));

                if (waitInMilliseconds > 0)
                {
                    // Acquire an event object
                    waitResponse = AcquireWaitResponse(commandMessage);

                    // Send the message to the provider
                    SocketClient.Send(dataPacket.ToArray());

                    // Wait for the response
                    bool signaled = waitResponse.WaitEvent.WaitOne(waitInMilliseconds);

                    // Did we get a response back
                    if (signaled == false)
                    {
                        commandResponse = new CommandResponse(commandMessage, 900109, ResponseTypes.TEXT, "Timed Out Waiting For Response"); 
                    }
                    else
                    {
                        // Capture the response
                        commandResponse = waitResponse.Response;
                    }
                }
                else
                {
                    // Send the message to the provider
                    SocketClient.Send(dataPacket.ToArray());

                    commandResponse = new CommandResponse(commandMessage, 0, ResponseTypes.TEXT, "Command Message Sent. Not Waiting For Response"); 
                }
            }

            catch (Exception exception)
            {
                FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : SendMessage : ERROR : {0}", exception.ToString());
                commandResponse = new CommandResponse(commandMessage, 900101, ResponseTypes.TEXT, exception.Message); 
            }

            if (waitInMilliseconds > 0)
            {
                ReleaseWaitResponse(waitResponse);
            }
            
            return commandResponse;
        }

        #endregion

        #region Private Methods

        /// <summary> Called to acquire a wait response object from the stack </summary>
        /// <param name="commandMessage"></param>
        /// <returns> WaitResponse </returns>
        private WaitResponse AcquireWaitResponse(CommandMessage commandMessage)
        {
            WaitResponse waitResponse = null;

            lock (WaitingResponses)
            {
                if (WaitResponseStack.Count > 0)
                {
                    waitResponse = WaitResponseStack.Pop();
                    
                    waitResponse.WaitEvent.Reset();
                    waitResponse.Response = null;
                    waitResponse.UniqueKey = commandMessage.UniqueKey;
                }
                else
                {
                    waitResponse = new WaitResponse();
                    waitResponse.UniqueKey = commandMessage.UniqueKey;
                }

                // Place the socket in the waiting queue
                WaitingResponses.Add(waitResponse.UniqueKey, waitResponse);
            }

            return waitResponse;
        }

        /// <summary> Called to release a response event back into the stack </summary>
        private void ReleaseWaitResponse(WaitResponse waitResponse)
        {
            if (waitResponse == null)
            {
                return;
            }

            lock (WaitingResponses)
            {
                WaitingResponses.Remove(waitResponse.UniqueKey);
                WaitResponseStack.Push(waitResponse);
            }
        }

        #endregion
    }

    #endregion

    #region MessagingConsumer Class

    /// <summary> Class provides server support for receiving notify requests </summary>
    public class MessagingConsumer : IDisposable
    {
        #region Delegate Types
        
        /// <summary> Delegate type for method called when a socket connection is accepted </summary>
        public delegate void CONNECTION_ACCEPTED(MessagingConsumer consumer, SocketClient socket);

        /// <summary> Delegate type for method called when a socket connection is closed </summary>
        public delegate void CONNECTION_CLOSED(MessagingConsumer consumer, SocketClient socket);

        /// <summary> Delegate type for method called when a socket connection has an error </summary>
        public delegate void CONNECTION_ERROR(MessagingConsumer consumer, SocketClient socket, Exception exception);

        /// <summary> Delegate type for method called so the client can log consumer code </summary>
        public delegate void CONNECTION_LOG(MessagingConsumer consumer, string logKey, string message);

        /// <summary> Delegate type for method called when a message is received </summary>
        public delegate void CONNECTION_MESSAGE(MessagingConsumer consumer, SocketClient socket, CommandMessage commandMessage);
        
        #endregion

        #region Private Properties
        
        /// <summary> Flag when disposed is called </summary>
        private bool Disposed = false;
        
        /// <summary> Handle to the socket server </summary>
        private SocketServer SocketServer = null;

        /// <summary> The server to connect to </summary>
        private string _Server = null;
        
        /// <summary> The port on the server to connect to </summary>
        private int _Port = 0;
        
        /// <summary> Handle to any client connection </summary>
        private Dictionary<long, SocketClient> ClientConnections = new Dictionary<long, SocketClient>();

        /// <summary> Provides synchronous access to the client connections list </summary>
        private ReaderWriterLockSlim ClientConnectionsLock = new ReaderWriterLockSlim();

        /// <summary> Set by the user to handle logging control </summary>
        private string LogKey = null;
        
        #endregion

        #region Public Properties

        /// <summary> Event method called when a socket connection is accepted </summary>
        public event CONNECTION_ACCEPTED ConnectionAcceptedEvent = null;

        /// <summary> Event method called when a socket connection is closed </summary>
        public event CONNECTION_CLOSED ConnectionClosedEvent = null;

        /// <summary> Event method called when a socket connection has an error </summary>
        public event CONNECTION_ERROR ConnectionErrorEvent = null;

        /// <summary> Event method called so the client can log consumer code </summary>
        public event CONNECTION_LOG ConnectionLogEvent = null;

        /// <summary> Delegate type for method called when a message is received </summary>
        public event CONNECTION_MESSAGE ConnectionMessageEvent = null;

        /// <summary> Read only access to the server </summary>
        public string Server { get { return _Server; } }

        /// <summary> Read only access to the port </summary>
        public int Port { get { return _Port; } }

        #endregion
        
        #region Constructor
        
        /// <summary> Constructor </summary>
        public MessagingConsumer(string logKey)
        {
            LogKey = logKey;
        }
        
        /// <summary> Destructor </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary> Dispose the server </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : Dispose : Started : Server[{0}] Port[{1}]", _Server, _Port);

            // Check to see if Dispose has already been called
            if (!Disposed)
            {
                // Note disposing has been started
                Disposed = true;

                // If disposing equals true, dispose all managed
                // and unmanaged resources.
                if (disposing)
                {
                    // Shutdown the thread listening on the pipe
                    SocketServer.Dispose();
                }

                // Dispose the lock
                ClientConnectionsLock.Dispose();
            }

            FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : Dispose : Completed : Server[{0}] Port[{1}]", _Server, _Port);
        }
        
        #endregion

        #region Event Methods

        /// <summary> Called to fire an event back to the client when a socket connection is accepted </summary>
        /// <param name="socket"></param>
        private void FireConnectionAcceptedEvent(SocketClient socket)
        {
            try
            {
                if (ConnectionAcceptedEvent != null)
                {
                    ConnectionAcceptedEvent(this, socket);
                }
            }

            catch (Exception exception)
            {
                FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : FireConnectionAcceptedEvent : ERROR : Server[{0}] Port[{1}] Client[{2}] : {3}", _Server, _Port, socket.IpAddress, exception.ToString());
            }
        }

        /// <summary> Called to fire an event back to the client when a socket connection is closed </summary>
        /// <param name="socket"></param>
        private void FireConnectionClosedEvent(SocketClient socket)
        {
            try
            {
                if (ConnectionClosedEvent != null)
                {
                    ConnectionClosedEvent(this, socket);
                }
            }

            catch (Exception exception)
            {
                FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : FireConnectionClosedEvent : ERROR : Server[{0}] Port[{1}] Client[{2}]: {3}", _Server, _Port, socket.IpAddress, exception.ToString());
            }
        }

        /// <summary> Called to fire an event back to the client when a socket connection has an error </summary>
        /// <param name="socket"></param>
        /// <param name="errorException"></param>
        private void FireConnectionErrorEvent(SocketClient socket, Exception errorException)
        {
            try
            {
                if (ConnectionErrorEvent != null)
                {
                    ConnectionErrorEvent(this, socket, errorException);
                }
            }

            catch (Exception exception)
            {
                string ipAddress = (socket != null) ? socket.IpAddress : null;
                FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : FireConnectionErrorEvent : ERROR : Server[{0}] Port[{1}] Client[{3}] : {4}", _Server, _Port, ipAddress, exception.ToString());
            }
        }

        /// <summary> Called to fire an event back to the client for logging </summary>
        /// <param name="message"></param>
        private void FireConnectionLogEvent(string message)
        {
            try
            {
                if (ConnectionLogEvent != null)
                {
                    ConnectionLogEvent(this, LogKey, message);
                }
            }

            catch
            {
            }
        }

        /// <summary> Called to fire an event back to the client for logging </summary>
        /// <param name="message"></param>
        /// <param name="parameters"></param>
        private void FireConnectionLogEvent(string message, params object[] parameters)
        {
            try
            {
                if (ConnectionLogEvent != null)
                {
                    ConnectionLogEvent(this, LogKey, string.Format(message, parameters));
                }
            }

            catch
            {
            }
        }

        /// <summary> Called to fire an event back to the client when a message is recieved </summary>
        /// <param name="socket"></param>
        /// <param name="commandMessage"></param>
        private void FireConnectionMessageEvent(SocketClient socket, CommandMessage commandMessage)
        {
            try
            {
                if (ConnectionMessageEvent != null)
                {
                    ConnectionMessageEvent(this, socket, commandMessage);
                }
            }

            catch (Exception exception)
            {
                FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : FireConnectionMessageEvent : ERROR : Server[{0}] Port[{1}] Client[{3}] : {4}", _Server, _Port, socket, exception.ToString());
            }
        }

        #endregion
        
        #region Public Methods
        
        /// <summary> Called to enable access to the server </summary>
        /// <param name="server"></param>
        /// <param name="port"></param>
        /// <param name="rawBufferSize">Default Value: 1 Meg</param>
        /// <param name="dataBufferSize">Default Value: 0 Bytes</param>
        /// <param name="userArg"> Default Value: null</param>
        public void Start(string server, short port, int rawBufferSize = 1048576, int dataBufferSize = 0, object userArg = null)
        {
            FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : Start : Started : Server[{0}] Port[{1}]", server, port);

            // Start the socket server
            if (SocketServer == null)
            {
                _Server = server;
                _Port = port;

                SocketServer = new SocketServer();
                
                // Start listening for connections
                SocketServer.Start(server, port, rawBufferSize, dataBufferSize, userArg,
                                   new SocketServer.MESSAGE_HANDLER(MessageHandler),
                                   new SocketServer.ACCEPT_HANDLER(AcceptHandler),
                                   new SocketServer.CLOSE_HANDLER(CloseHandler),
                                   new SocketServer.ERROR_HANDLER(ErrorHandler));
            }
            else
            {
                FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : Start : WARINING : Server[{0}] Port[{1}] : Socket Server Is Active", _Server, _Port);
            }

            FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : Start : Completed : Server[{0}] Port[{1}]", _Server, _Port);
        }
        
        /// <summary> Called to disable access to the server </summary>
        public void Stop()
        {
            FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : Stop : Started : Server[{0}] Port[{1}]", _Server, _Port);

            if (SocketServer != null)
            {
                // Close the server
                SocketServer.Dispose();
                SocketServer = null;
            }
            {
                FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : Stop : WARINING : Server[{0}] Port[{1}] : Socket Server Is Not Active", _Server, _Port);
            }

            FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : Stop : Completed : Server[{0}] Port[{1}]", _Server, _Port);
        }
        
        #endregion

        #region Handler Methods

        /// <summary> Called when a message is extracted from the socket </summary>
        /// <param name="socket"> The SocketClient object the message came from </param>
        private void MessageHandler(SocketClient socket)
        {
            const int headerLength = 12;

            try
            {
                FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : MessageHandler : Started : Server[{0}] Port[{1}]", _Server, _Port);

                // Extract the data we currently received
                byte[] currentMessage = new byte[socket.MessageLength];
                Array.Copy(socket.RawBuffer, currentMessage, socket.MessageLength);

                // Add this new data to the buffer
                socket.ListBuffer.AddRange(currentMessage);

                // Set the offset and offset data indexes
                int offset = 0;
                int offsetData = headerLength;

                // Process messages that we have
                while (offset < socket.ListBuffer.Count)
                {
                    // Do we have enough bytes left for a new message size
                    if ((socket.ListBuffer.Count - offset) < headerLength)
                    {
                        // Clear out the bytes in the ListBuffer we used
                        socket.ListBuffer.RemoveRange(0, offset);
                        FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : MessageHandler : Info : ListBuffer[{0}] Offset[{1}] HeaderLength[{2}] : Not Enough Bytes For Header", socket.ListBuffer.Count, offset, headerLength);
                        return;
                    }

                    // Capture the size of this message
                    int marker1 = System.BitConverter.ToInt32(socket.ListBuffer.GetRange(offset, 4).ToArray(), 0);
                    int sizeOfMessage = System.BitConverter.ToInt32(socket.ListBuffer.GetRange(offset + 4, 4).ToArray(), 0);
                    int marker2 = System.BitConverter.ToInt32(socket.ListBuffer.GetRange(offset + 8, 4).ToArray(), 0);

                    FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : MessageHandler : Info : Marker1[{0}] SizeOfMessage[{1}] Marker2[{2}]", marker1, sizeOfMessage, marker2);

                    // Check the markers
                    if ((marker1 != int.MinValue) || (marker2 != int.MaxValue))
                    {
                        // Clear out the bytes in the ListBuffer we used
                        socket.ListBuffer.Clear();
                        FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : MessageHandler : ERROR : OUT OF SYNC : Clear buffer");
                        return;
                    }

                    // Do we have all the data for this next message in the buffer
                    if ((sizeOfMessage + offsetData) > socket.ListBuffer.Count)
                    {
                        // Clear out the bytes in the ListBuffer we used
                        socket.ListBuffer.RemoveRange(0, offset);
                        FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : MessageHandler : Info : SizeOfMessage[{0}] OffsetData[{1}] ListBuffer[{2}] : Not Enough Bytes For Data", sizeOfMessage, offsetData, socket.ListBuffer.Count);
                        return;
                    }

                    // Capture the data
                    string message = System.Text.Encoding.UTF8.GetString(socket.ListBuffer.GetRange(offsetData, sizeOfMessage).ToArray(), 0, sizeOfMessage);

                    FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : MessageHandler : Info : Message[{0}]", message);

                    // Decrypt the message
                    message = CryptoProvider.DecryptText(message);

                    // Create the command message object
                    CommandMessage commandMessage = (CommandMessage) Serializer.toObject<CommandMessage>(message);

                    if (commandMessage == null)
                    {
                        FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : MessageHandler : ERROR : Unable To Deserialize Command Response");
                    }

                    // Fire the command message to the client for processing
                    FireConnectionMessageEvent(socket, commandMessage);

                    // Update the offset to the next message
                    offset += (headerLength + sizeOfMessage);
                    offsetData = offset + headerLength;

                    FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : MessageHandler : Info : Offset[{0}] OffsetData[{1}] ListBuffer[{2}]", offset, offsetData, socket.ListBuffer.Count);
                }

                // Clear out the bytes in the ListBuffer we used
                socket.ListBuffer.Clear();
            }

            catch (Exception exception)
            {
                // Clear out the bytes in the ListBuffer we used
                socket.ListBuffer.Clear();

                FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : MessageHandler : ERROR : Server[{0}] Port[{1}] : {2}", _Server, _Port, exception.ToString());
            }

            FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : MessageHandler : Completed : Server[{0}] Port[{1}]", _Server, _Port);
        }

        /// <summary> Called when a socket connection is accepted </summary>
        /// <param name="socket"> The SocketClient object the message came from </param>
        private void AcceptHandler(SocketClient socket)
        {
            FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : AcceptHandler : Started : Server[{0}] Port[{1}] : IpAddress[{2}]", _Server, _Port, socket.IpAddress);

            try
            {
                bool exists = true;

                ClientConnectionsLock.EnterWriteLock();
                try
                {
                    exists = ClientConnections.ContainsKey(socket.UniqueKey);
                    if (exists == false)
                    {
                        ClientConnections.Add(socket.UniqueKey, socket);
                    }
                }
                finally
                {
                    ClientConnectionsLock.ExitWriteLock();
                }

                if (exists == false)
                {
                    FireConnectionAcceptedEvent(socket);
                }
            }

            catch (Exception exception)
            {
                FireConnectionLogEvent(exception.ToString());
            }

            FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : AcceptHandler : Completed : Server[{0}] Port[{1}] : IpAddress[{2}]", _Server, _Port, socket.IpAddress);
        }

        /// <summary> Called when a socket connection is closed </summary>
        /// <param name="socket"> The SocketClient object the message came from </param>
        private void CloseHandler(SocketClient socket)
        {
            string ipAddress = "None";
            if (socket != null)
            {
                ipAddress = socket.IpAddress;
            }
            
            FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : CloseHandler : Started : Server[{0}] Port[{1}] : IpAddress[{2}]", _Server, _Port, ipAddress);

            // If the MessagingConsumer has been disposed don't run this code
            if (Disposed != true)
            {
                try
                {
                    bool exists = true;

                    ClientConnectionsLock.EnterWriteLock();
                    try
                    {
                        if (socket != null)
                        {
                            exists = ClientConnections.ContainsKey(socket.UniqueKey);
                            if (exists == true)
                            {
                                ClientConnections.Remove(socket.UniqueKey);
                            }
                        }
                    }
                    finally
                    {
                        ClientConnectionsLock.ExitWriteLock();
                    }

                    if (exists == true)
                    {
                        FireConnectionClosedEvent(socket);
                    }
                }

                catch (Exception exception)
                {
                    FireConnectionLogEvent(exception.ToString());
                }
            }

            FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : CloseHandler : Completed : Server[{0}] Port[{1}] : IpAddress[{2}]", _Server, _Port, ipAddress);
        }

        /// <summary> Called when a socket error occurs </summary>
        /// <param name="socket"> The SocketClient object the message came from </param>
        /// <param name="errorException"> The reason for the error </param>
        private void ErrorHandler(SocketClient socket, Exception errorException)
        {
            if (errorException.Message != "Shutting Down Accept Thread")
            {
                string ipAddress = "None";
                if (socket != null)
                {
                    ipAddress = socket.IpAddress;
                }

                FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : ErrorHandler : Started : Server[{0}] Port[{1}] : IpAddress[{2}]", _Server, _Port, ipAddress);

                try
                {
                    FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : ErrorHandler : ERROR : Server[{0}] Port[{1}] : {2}", _Server, _Port, errorException.ToString());
                    FireConnectionErrorEvent(socket, errorException);
                }

                catch (Exception exception)
                {
                    FireConnectionLogEvent(exception.ToString());
                }

                FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : ErrorHandler : Completed : Server[{0}] Port[{1}] : IpAddress[{2}]", _Server, _Port, ipAddress);
            }
        }

        #endregion
        
        #region Send Methods

        /// <summary> Called to send a response back to the client via the socket unique connection key </summary>
        /// <param name="connectionKey"></param>
        /// <param name="commandResponse"></param>
        public void SendResponse(long connectionKey, CommandResponse commandResponse)
        {
            FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : SendResponse : Started : Server[{0}] Port[{1}] : ConnectionKey[{2}]", _Server, _Port, connectionKey);

            SocketClient socket = null;

            if (connectionKey == 0)
            {
                FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : SendResponse : Completed : NOT SENDING : Connect Key is 0");
                return;
            }

            ClientConnectionsLock.EnterReadLock();
            try
            {
                if (ClientConnections.ContainsKey(connectionKey))
                {
                    socket = ClientConnections[connectionKey];
                }
            }
            finally
            {
                ClientConnectionsLock.ExitReadLock();
            }

            if (socket != null)
            {
                SendResponse(socket, commandResponse);
            }
            else
            {
                FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : SendResponse : WARNING : Server[{0}] Port[{1}] : ConnectionKey[{2}] : Socket Client not longer is active", _Server, _Port, connectionKey);
            }

            FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : SendResponse : Completed : Server[{0}] Port[{1}] : ConnectionKey[{2}]", _Server, _Port, connectionKey);
        }
        
        /// <summary> Called to send a response back to the client </summary>
        /// <param name="socket"></param>
        /// <param name="commandResponse"></param>
        public void SendResponse(SocketClient socket, CommandResponse commandResponse)
        {
            FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : SendResponse : Started : Server[{0}] Port[{1}] : IpAddress[{2}]", _Server, _Port, socket.IpAddress);

            ClientConnectionsLock.EnterReadLock();
            try
            {
                // Verify the socket client is still active
                if ((ClientConnections.ContainsKey(socket.UniqueKey) == false) || (socket.IsConnected == false))
                {
                    FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : SendResponse : WARNING : Server[{0}] Port[{1}] : IpAddress[{2}] : Socket Client not longer is active", _Server, _Port, socket.IpAddress);
                    return;
                }

                // Convert the response object to JSON
                string message = Serializer.toJSON(commandResponse);

                // Encrypt the message
                message = CryptoProvider.EncryptText(message);

                // Build byte array for the call
                List<byte> dataPacket = new List<byte>();

                // Add the length
                dataPacket.AddRange(BitConverter.GetBytes(int.MinValue));
                dataPacket.AddRange(BitConverter.GetBytes(message.Length));
                dataPacket.AddRange(BitConverter.GetBytes(int.MaxValue));

                // Add the data
                dataPacket.AddRange(Encoding.UTF8.GetBytes(message));

                // Send the response to the provider
                socket.Send(dataPacket.ToArray());
            }
            finally
            {
                ClientConnectionsLock.ExitReadLock();
            }

            FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : SendResponse : Completed : Server[{0}] Port[{1}] : IpAddress[{2}]", _Server, _Port, socket.IpAddress);
        }

        /// <summary> Send a message to all know providers </summary>
        /// <param name="commandResponse"></param>
        public void SendResponseToAll(CommandResponse commandResponse)
        {
            FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : SendResponseToAll : Started : Server[{0}] Port[{1}]", _Server, _Port);

            ClientConnectionsLock.EnterReadLock();
            try
            {
                foreach (SocketClient socket in ClientConnections.Values)
                {
                    // Send it out
                    SendResponse(socket, commandResponse);
                }
            }
            finally
            {
                ClientConnectionsLock.ExitReadLock();
            }

            FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingConsumer : SendResponseToAll : Completed : Server[{0}] Port[{1}]", _Server, _Port);
        }

        #endregion
    }

    #endregion

    #region ConsumerInfo Class

    /// <summary> Maintains Consumer information </summary>
    public class ConsumerInfo
    {
        #region Public Properties
            
        /// <summary> The ipaddress or server of the consumer </summary>
        public string Server;

        /// <summary> The port of the consumer </summary>
        public int Port;

        /// <summary> A list of services this consumer supports </summary>
        public List<string> Services;

        /// <summary> The number of messages sent to this consumer for load balancing </summary>
        public long MessageCount { get { return Interlocked.Read(ref Messages); } }

        /// <summary> A reference to the provider </summary>
        public MessagingProvider Provider = null;

        #endregion

        #region Private Properties

        /// <summary> The number of messages sent to this consumer for load balancing </summary>
        private long Messages = 0;

        #endregion

        #region Constructor

        /// <summary> Constructor </summary>
        /// <param name="server"></param>
        /// <param name="port"></param>
        /// <param name="services"> A delmited list of services </param>
        public ConsumerInfo(string server, int port, string services)
        {
            Server = server;
            Port = port;
            
            if (services != null)
            {
                // Split on any non word character
                Services = Regex.Split(services, "\\W", RegexOptions.Compiled).ToList();
            }
            else
            {
                Services = new List<string>();
            }
        }

        #endregion

        #region Public Methods

        /// <summary> Called to increment the message count </summary>
        public void IncrementMessageCount()
        {
            Interlocked.Increment(ref Messages);
        }

        #endregion
    }

    #endregion

    #region MessagingProviderManager Class

    /// <summary> Class provides load balancing and routing to multiple Consumers </summary>
    public class MessagingProviderManager : IDisposable
    {
        #region Delegate Types
        
        /// <summary> Delegate type for method called when a socket connection is accepted </summary>
        public delegate void CONNECTION_ACCEPTED(ConsumerInfo consumerInfo);

        /// <summary> Delegate type for method called when a socket connection is closed </summary>
        public delegate void CONNECTION_CLOSED(ConsumerInfo consumerInfo);

        /// <summary> Delegate type for method called when a socket connection has an error </summary>
        public delegate void CONNECTION_ERROR(ConsumerInfo consumerInfo, Exception exception);

        /// <summary> Delegate type for method called so the client can log consumer code </summary>
        public delegate void CONNECTION_LOG(string logKey, string message);

        /// <summary> Delegate type for method called when an unsolicited message is received or the waiting thread moved on </summary>
        public delegate void CONNECTION_RESPONSE(ConsumerInfo consumerInfo, CommandResponse commandResponse);
        
        #endregion

        #region Public Static Properties

        /// <summary> Event method called when a socket connection is accepted </summary>
        public static event CONNECTION_ACCEPTED ConnectionAcceptedEvent = null;

        /// <summary> Event method called when a socket connection is closed </summary>
        public static event CONNECTION_CLOSED ConnectionClosedEvent = null;

        /// <summary> Event method called when a socket connection has an error </summary>
        public static event CONNECTION_ERROR ConnectionErrorEvent = null;

        /// <summary> Event method called so the client can log consumer code </summary>
        public static event CONNECTION_LOG ConnectionLogEvent = null;

        /// <summary> Delegate type for method called when an unsolicited response is received or the waiting thread moved on </summary>
        public static event CONNECTION_RESPONSE ConnectionResponseEvent = null;

        #endregion

        #region Private Static Properties

        /// <summary> Singleton pointer </summary>
        private static MessagingProviderManager This = null;

        #endregion

        #region Private Properties

        /// <summary> Flag when disposed is called </summary>
        private bool Disposed = false;

        /// <summary> Set by the user to handle logging control </summary>
        private string LogKey = null;
        
        /// <summary> A list of available consumers </summary>
        private List<ConsumerInfo> Consumers = new List<ConsumerInfo>();

        /// <summary> Provides synchronous access to the Consumers list </summary>
        private ReaderWriterLockSlim ConsumersLock = new ReaderWriterLockSlim();
        
        #endregion

        #region Constructor

        /// <summary> Static constructor to create the singleton </summary>
        static MessagingProviderManager()
        {
            This = new MessagingProviderManager();
        }

        /// <summary> Called to init the singleton </summary>
        /// <param name="logKey"></param>
        public static void Init(string logKey)
        {
            This.LogKey = logKey;
        }

        /// <summary> Called to shutdown the singleton </summary>
        public static void Close()
        {
            This.Dispose();
        }

        /// <summary> Destructor </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary> Dispose the server </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProviderManager : Dispose : Started");

            // Check to see if Dispose has already been called.
            if (!Disposed)
            {
                // Note disposing has been done
                Disposed = true;

                // If disposing equals true, dispose all managed
                // and unmanaged resources.
                if (disposing)
                {
                    ConsumersLock.EnterWriteLock();
                    try
                    {
                        // Dispose of each active provider connection to these consumers
                        foreach (ConsumerInfo consumerInfo in Consumers)
                        {
                            consumerInfo.Provider.Dispose();
                        }
                    }
                    finally
                    {
                        ConsumersLock.ExitWriteLock();
                    }

                    ConsumersLock.Dispose();
                }
            }

            FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProviderManager : Dispose : Completed");
        }

        #endregion

        #region Event Methods

        /// <summary> Called to fire an event back to the client when a socket connection is accepted </summary>
        /// <param name="consumerInfo"></param>
        private void FireConnectionAcceptedEvent(ConsumerInfo consumerInfo)
        {
            try
            {
                if (ConnectionAcceptedEvent != null)
                {
                    ConnectionAcceptedEvent(consumerInfo);
                }
            }

            catch (Exception exception)
            {
                FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : FireConnectionAcceptedEvent : ERROR : Server[{0}] Port[{1}] {2}", consumerInfo.Server, consumerInfo.Port, exception.ToString());
            }
        }

        /// <summary> Called to fire an event back to the client when a socket connection is closed </summary>
        /// <param name="consumerInfo"></param>
        private void FireConnectionClosedEvent(ConsumerInfo consumerInfo)
        {
            try
            {
                if (ConnectionClosedEvent != null)
                {
                    ConnectionClosedEvent(consumerInfo);
                }
            }

            catch (Exception exception)
            {
                FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : FireConnectionClosedEvent : ERROR : Server[{0}] Port[{1}] {2}", consumerInfo.Server, consumerInfo.Port, exception.ToString());
            }
        }

        /// <summary> Called to fire an event back to the client when a socket connection has an error </summary>
        /// <param name="consumerInfo"></param>
        /// <param name="errorException"></param>
        private void FireConnectionErrorEvent(ConsumerInfo consumerInfo, Exception errorException)
        {
            try
            {
                if (ConnectionErrorEvent != null)
                {
                    ConnectionErrorEvent(consumerInfo, errorException);
                }
            }

            catch (Exception exception)
            {
                FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : FireConnectionErrorEvent : ERROR : Server[{0}] Port[{1}] {2}", consumerInfo.Server, consumerInfo.Port, exception.ToString());
            }
        }

        /// <summary> Called to fire an event back to the client for logging </summary>
        /// <param name="message"></param>
        private void FireConnectionLogEvent(string message)
        {
            try
            {
                if (ConnectionLogEvent != null)
                {
                    ConnectionLogEvent(LogKey, message);
                }
            }

            catch
            {
            }
        }

        /// <summary> Called to fire an event back to the client for logging </summary>
        /// <param name="message"></param>
        /// <param name="parameters"></param>
        private void FireConnectionLogEvent(string message, params object[] parameters)
        {
            try
            {
                if (ConnectionLogEvent != null)
                {
                    ConnectionLogEvent(LogKey, string.Format(message, parameters));
                }
            }

            catch
            {
            }
        }

        /// <summary> Called to fire an event back to the client when a response is recieved </summary>
        /// <param name="consumerInfo"></param>
        /// <param name="commandResponse"></param>
        private void FireConnectionResponseEvent(ConsumerInfo consumerInfo, CommandResponse commandResponse)
        {
            try
            {
                if (ConnectionResponseEvent != null)
                {
                    ConnectionResponseEvent(consumerInfo, commandResponse);
                }
            }

            catch (Exception exception)
            {
                FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : FireConnectionMessageEvent : ERROR : Server[{0}] Port[{1}] {2}", consumerInfo.Server, consumerInfo.Port, exception.ToString());
            }
        }

        #endregion

        #region Private Event Methods

        /// <summary> Called when a socket connection is accepted </summary>
        /// <param name="provider"></param>
        private void InternalConnectionAcceptedEvent(MessagingProvider provider)
        {
            try
            {
                ConsumerInfo consumerInfo = null;

                // If this is true we are disposing
                if ((Disposed == true) && (ConsumersLock.IsWriteLockHeld))
                {
                    return;
                }

                ConsumersLock.EnterReadLock();
                try
                {
                    // Find the CosumerInfo object for the socket
                    consumerInfo = Consumers.FirstOrDefault(k => k.Provider == provider);
                }
                finally
                {
                    ConsumersLock.ExitReadLock();
                }

                // Fire the event to the client
                FireConnectionAcceptedEvent(consumerInfo);
            }

            catch (Exception exception)
            {
                FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : InternalConnectionAcceptedEvent : ERROR : Server[{0}] Port[{1}] {2}", provider.Server, provider.Port, exception.ToString());
            }
        }

        /// <summary> Called when a socket connection is closed </summary>
        /// <param name="provider"></param>
        private void InternalConnectionClosedEvent(MessagingProvider provider)
        {
            try
            {
                ConsumerInfo consumerInfo = null;

                // If this is true we are disposing
                if ((Disposed == true) && (ConsumersLock.IsWriteLockHeld))
                {
                    return;
                }

                ConsumersLock.EnterReadLock();
                try
                {
                    // Find the CosumerInfo object for the socket
                    consumerInfo = Consumers.FirstOrDefault(k => k.Provider == provider);
                }
                finally
                {
                    ConsumersLock.ExitReadLock();
                }

                // Fire the event to the client
                FireConnectionClosedEvent(consumerInfo);
            }

            catch (Exception exception)
            {
                FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : InternalConnectionAcceptedEvent : ERROR : Server[{0}] Port[{1}] {2}", provider.Server, provider.Port, exception.ToString());
            }
        }

        /// <summary> Called when a socket connection has an error </summary>
        /// <param name="provider"></param>
        /// <param name="errorException"></param>
        private void InternalConnectionErrorEvent(MessagingProvider provider, Exception errorException)
        {
            try
            {
                ConsumerInfo consumerInfo = null;

                // If this is true we are disposing
                if ((Disposed == true) && (ConsumersLock.IsWriteLockHeld))
                {
                    return;
                }

                ConsumersLock.EnterReadLock();
                try
                {
                    // Find the CosumerInfo object for the socket
                    consumerInfo = Consumers.FirstOrDefault(k => k.Provider == provider);
                }
                finally
                {
                    ConsumersLock.ExitReadLock();
                }

                // Fire the event to the client
                FireConnectionErrorEvent(consumerInfo, errorException);
            }

            catch (Exception exception)
            {
                FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : InternalConnectionErrorEvent : ERROR : Server[{0}] Port[{1}] {2}", provider.Server, provider.Port, exception.ToString());
            }
        }

        /// <summary> Called so the client can log consumer code </summary>
        /// <param name="provider"></param>
        /// <param name="logKey"></param>
        /// <param name="message"></param>
        private void InternalConnectionLogEvent(MessagingProvider provider, string logKey, string message)
        {
            try
            {
                // Fire the event to the client
                FireConnectionLogEvent(message);
            }

            catch (Exception exception)
            {
                FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : InternalConnectionLogEvent : ERROR : {0}", exception.ToString());
            }
        }

        /// <summary> Called when an unsolicited message is received or the waiting thread moved on </summary>
        /// <param name="provider"></param>
        /// <param name="commandResponse"></param>
        private void InternalConnectionResponseEvent(MessagingProvider provider, CommandResponse commandResponse)
        {
            try
            {
                ConsumerInfo consumerInfo = null;

                // If this is true we are disposing
                if ((Disposed == true) && (ConsumersLock.IsWriteLockHeld))
                {
                    return;
                }

                ConsumersLock.EnterReadLock();
                try
                {
                    // Find the CosumerInfo object for the socket
                    consumerInfo = Consumers.FirstOrDefault(k => k.Provider == provider);
                }
                finally
                {
                    ConsumersLock.ExitReadLock();
                }

                // Fire the event to the client
                FireConnectionResponseEvent(consumerInfo, commandResponse);
            }

            catch (Exception exception)
            {
                FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProvider : InternalConnectionResponseEvent : ERROR : Server[{0}] Port[{1}] {2}", provider.Server, provider.Port, exception.ToString());
            }
        }

        #endregion

        #region Private Methods

        /// <summary> Called to find a consumer for the specified service </summary>
        /// <param name="service"></param>
        /// <returns> ConsumerInfo </returns>
        private ConsumerInfo FindConsumer(string service)
        {
            FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProviderManager : FindConsumer : Started : Service[{0}]", service);

            ConsumerInfo consumerInfo = null;

            try
            {
                ConsumersLock.EnterReadLock();
                try
                {
                    consumerInfo = Consumers.FindAll(k => k.Services.Exists(s => s == service)).OrderBy(o => o.MessageCount).First();
                }
                finally
                {
                    ConsumersLock.ExitReadLock();
                }
            }

            catch (Exception exception)
            {
                FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProviderManager : FindConsumer : ERROR : {0}", exception.ToString());
            }

            FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProviderManager : FindConsumer : Completed");

            return consumerInfo;
        }

        #endregion

        #region Public Methods

        /// <summary> Called to add a consumer that messages can be sent to </summary>
        /// <param name="server"></param>
        /// <param name="port"></param>
        /// <param name="services"></param>
        public static void AddConsumer(string server, int port, string services)
        {
            This.FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProviderManager : AddConsumer : Started : Server[{0}] Port[{1}] Services[{2}]", server, port, services);

            // Create a consumer Info object
            ConsumerInfo consumerInfo = new ConsumerInfo(server, port, services);

            // Start up a provider for this consumer
            consumerInfo.Provider = new MessagingProvider(server, port, null);
            consumerInfo.Provider.ConnectionAcceptedEvent += new MessagingProvider.CONNECTION_ACCEPTED(This.InternalConnectionAcceptedEvent);
            consumerInfo.Provider.ConnectionClosedEvent += new MessagingProvider.CONNECTION_CLOSED(This.InternalConnectionClosedEvent);
            consumerInfo.Provider.ConnectionErrorEvent += new MessagingProvider.CONNECTION_ERROR(This.InternalConnectionErrorEvent);
            consumerInfo.Provider.ConnectionLogEvent += new MessagingProvider.CONNECTION_LOG(This.InternalConnectionLogEvent);
            consumerInfo.Provider.ConnectionResponseEvent += new MessagingProvider.CONNECTION_RESPONSE(This.InternalConnectionResponseEvent);

            This.ConsumersLock.EnterWriteLock();
            try
            {
                This.Consumers.Add(consumerInfo);
            }
            finally
            {
                This.ConsumersLock.ExitWriteLock();
            }

            This.FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProviderManager : AddConsumer : Completed");
        }

        /// <summary> Called to send a message to a consumer who is managing the specified service </summary>
        /// <param name="service"></param>
        /// <param name="commandMessage"></param>
        /// <param name="waitInMilliseconds"></param>
        /// <returns> CommandResponse </returns>
        public static CommandResponse SendMessage(string service, CommandMessage commandMessage, int waitInMilliseconds)
        {
            This.FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProviderManager : SendMessage : Started : Service[{0}] Route[{1}] Command[{2}] WaitInMilliseconds[{3}]", service, commandMessage.Route, commandMessage.Command, waitInMilliseconds);

            CommandResponse commandResponse = null;

            // Locate a Consumer for this service
            ConsumerInfo consumerInfo = This.FindConsumer(service);

            // We could not locate a consumer for the specified service
            if (consumerInfo == null)
            {
                throw new Exception(string.Format("Unable To Find Consumer For The Specified Service: {0}", service));
            }
            
            // Send the message to this consumer    
            commandResponse = consumerInfo.Provider.SendMessage(commandMessage, waitInMilliseconds);

            This.FireConnectionLogEvent("ArdanStudios.Servers.MessagingClient.MessagingProviderManager : SendMessage : Completed");

            return commandResponse;
        }

        #endregion
    }

    #endregion
}
