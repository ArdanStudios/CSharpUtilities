using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ArdanStudios.Common;

namespace Samples
{
    /// <summary>
    /// The ThreadPool using IOCP underneath to provide concurrency controlled thread pools.
    /// 
    /// The .NET Thread Pool also uses IOCP underneath but each application is only given a single thead pool.
    /// To learn more please read: http://www.theukwebdesigncompany.com/articles/iocp-thread-pooling.php
    /// 
    /// </summary>
    public static class ThreadPoolSample
    {
        /// <summary> An example queue of work </summary>
        private static Queue<string> queue = new Queue<string>();

        public static void SampleApp()
        {
            // Create the thread pool
            IOCPThreadPool pool = new IOCPThreadPool(0, // Concurrency Level
                                                     1, // Min Thread
                                                     8, // Max Threads
                                                     new IOCPThreadPool.USER_FUNCTION(PerformWork),
                                                    ThreadPriority.Normal);
            
            // Push 100 messages into the pool
            for (int item = 0; item < 100; ++item)
            {
                lock (queue)
                {
                    queue.Enqueue(string.Format("Hello {1}", item));
                }
                pool.PostEvent();
            }

            // Wait for the user to hit enter
            string waitForEnter = Console.ReadLine();
        }

        private static void PerformWork(int index)
        {
            string message = null;
            lock (queue)
            {
                // Pop the message off the queue
                message = queue.Dequeue();
            }

            // Display to the screen
            Console.WriteLine(message);
        }
    }
}
