#region Namespaces

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

#endregion

namespace ArdanStudios.Common
{
    /// <summary> Maintains performance counters for the machine </summary>
    public class PerformanceCounters : IDisposable
    {
        #region Static Properties

        /// <summary> An instance to the singleton </summary>
        private static PerformanceCounters This = null;

        #endregion

        #region Public Classes

        /// <summary> Counter usage information </summary>
        public class CounterUsage
        {
            /// <summary> Process Usage Detail </summary>
            public struct ProcessUsage
            {
                /// <summary> The amount of CPU being used by the process as a percentage </summary>
                public int Processor;

                /// <summary> The operations / sec of data IO </summary>
                public float IO_DataOperations;

                /// <summary> The operations / sec of read IO </summary>
                public float IO_ReadOperations;

                /// <summary> The operations / sec of writes IO </summary>
                public float IO_WriteOperations;

                /// <summary> The amount of working memory used by the process in KBytes </summary>
                public long WorkingMemory;

                /// <summary> The number of handles being used by the process </summary>
                public int Handles;

                /// <summary> The number of threads in the process </summary>
                public int Threads;
            }

            /// <summary> Machine Usage Detail </summary>
            public struct Machineusage
            {
                /// <summary> The amount of CPU being used on the machine as a percentage </summary>
                public int Processor;

                /// <summary> The amount of memory available on the machine in KBytes </summary>
                public long AvailableMemory;
            }

            /// <summary> Process Usage Information </summary>
            public ProcessUsage Process = new ProcessUsage();

            /// <summary> Machine Usage Information </summary>
            public Machineusage Machine = new Machineusage();
        }

        #endregion

        #region Private Properties

        /// <summary> Flag to indicate if the class is disposing </summary>
        private bool Disposed = false;

        /// <summary> Provides processor usage information for the process </summary>
        private PerformanceCounter Processor = null;

        /// <summary> Provides processor usage information for the machine </summary>
        private PerformanceCounter MachineProcessor = null;

        /// <summary> Provides IO Data usage information for the process </summary>
        private PerformanceCounter IO_DataOperations = null;

        /// <summary> Provides IO Reads usage information for the process </summary>
        private PerformanceCounter IO_ReadOperations = null;

        /// <summary> Provides IO Writes usage information for the process </summary>
        private PerformanceCounter IO_WriteOperations = null;

        /// <summary> Provides Working Memory usage information for the process </summary>
        private PerformanceCounter ProcessMemory = null;

        /// <summary> Provides Available Memory usage information for the machine </summary>
        private PerformanceCounter MachineMemory = null;
        
        #endregion

        #region Constructor
        
        /// <summary> Static Constructor </summary>
        static PerformanceCounters()
        {
            // Create the singleton
            This = new PerformanceCounters();
        }

        /// <summary> Called to start the monitoring </summary>
        public static void Start()
        {
            string instanceName = Process.GetCurrentProcess().ProcessName;

            This.Processor = new PerformanceCounter();
            This.Processor.CategoryName = "Process";
            This.Processor.CounterName = "% Processor Time";
            This.Processor.InstanceName = instanceName;

            This.MachineProcessor = new PerformanceCounter();
            This.MachineProcessor.CategoryName = "Process";
            This.MachineProcessor.CounterName = "% Processor Time";
            This.MachineProcessor.InstanceName = "_Total";

            This.IO_DataOperations = new PerformanceCounter();
            This.IO_DataOperations.CategoryName = "Process";
            This.IO_DataOperations.CounterName = "IO Data Operations/sec";
            This.IO_DataOperations.InstanceName = instanceName;

            This.IO_ReadOperations = new PerformanceCounter();
            This.IO_ReadOperations.CategoryName = "Process";
            This.IO_ReadOperations.CounterName = "IO Read Operations/sec";
            This.IO_ReadOperations.InstanceName = instanceName;

            This.IO_WriteOperations = new PerformanceCounter();
            This.IO_WriteOperations.CategoryName = "Process";
            This.IO_WriteOperations.CounterName = "IO Write Operations/sec";
            This.IO_WriteOperations.InstanceName = instanceName;

            This.ProcessMemory = new PerformanceCounter();
            This.ProcessMemory.CategoryName = "Process";
            This.ProcessMemory.CounterName = "Working Set - Private";
            This.ProcessMemory.InstanceName = instanceName;

            This.MachineMemory = new PerformanceCounter();
            This.MachineMemory.CategoryName = "Memory";
            This.MachineMemory.CounterName = "Available KBytes";

            This.Processor.NextValue();
            This.MachineProcessor.NextValue();
            This.IO_DataOperations.NextValue();
            This.IO_ReadOperations.NextValue();
            This.IO_WriteOperations.NextValue();
            This.ProcessMemory.NextValue();
            This.MachineMemory.NextValue();
        }

        /// <summary> Called to close down the monitoring </summary>
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
        private void Dispose(bool disposing)
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
                    Processor.Dispose();
                    IO_DataOperations.Dispose();
                    IO_ReadOperations.Dispose();
                    IO_WriteOperations.Dispose();
                    ProcessMemory.Dispose();
                    MachineMemory.Dispose();
                }
            }
        }
        
        #endregion
        
        #region Public Methods

        /// <summary> Get the current usage </summary>
        /// <returns> CounterUsage </returns>
        public static CounterUsage GetUsage()
        {
            CounterUsage counterUsage = new CounterUsage();

            counterUsage.Process.Processor = (int) (This.Processor.NextValue() / 100);
            counterUsage.Process.IO_DataOperations = This.IO_DataOperations.NextValue();
            counterUsage.Process.IO_ReadOperations = This.IO_ReadOperations.NextValue();
            counterUsage.Process.IO_WriteOperations = This.IO_WriteOperations.NextValue();
            counterUsage.Process.WorkingMemory = (long) (This.ProcessMemory.NextValue() / 1024);
            counterUsage.Process.Handles = Process.GetCurrentProcess().HandleCount;
            counterUsage.Process.Threads = Process.GetCurrentProcess().Threads.Count;

            counterUsage.Machine.AvailableMemory = (long) This.MachineMemory.NextValue();
            counterUsage.Machine.Processor = (int) (This.MachineProcessor.NextValue() / 100);

            return counterUsage;
        }
        
        #endregion
    }
}