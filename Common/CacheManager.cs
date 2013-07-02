#region Namespaces

using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading;

#endregion

namespace ArdanStudios.Common
{
    #region CachedObject Class

    /// <summary> Encapsulates an object that is cached </summary>
    public class CachedObject
    {
        #region Properties

        /// <summary> The key used to store the data </summary>
        public string Key = null;

        /// <summary> A reference to the data cached in memory </summary>
        public object ObjectData = null;

        /// <summary> The time the object was cached </summary>
        public DateTime Timestamp = DateTime.Now;

        /// <summary> The time in seconds the object is allowed to stay in cache. Default 5 minutes. </summary>
        public int TimeToLive = 300;

        #endregion

        #region Constructor

        /// <summary> Constructor to use TimeToLive settings </summary>
        /// <param name="key"></param>
        /// <param name="objectData"></param>
        public CachedObject(string key, object objectData)
        {
            Key = key;
            ObjectData = objectData;
        }

        /// <summary> Constructor to set the TimeToLive settings </summary>
        /// <param name="key"></param>
        /// <param name="objectData"></param>
        /// <param name="timeToLive"> Time in seconds </param>
        public CachedObject(string key, object objectData, int timeToLive)
        {
            Key = key;
            ObjectData = objectData;
            TimeToLive = timeToLive;
        }

        #endregion
    }

    #endregion

    #region CacheManager Class

    /// <summary> This class manages the caching environment as a singleton </summary>
    public class CacheManager : IDisposable
    {
        #region Static Properties

        /// <summary> A reference to the cache manager singleton </summary>
        private static CacheManager This = null;

        #endregion

        #region Private Properties

        /// <summary> Flag to indicate if the object has been disposed </summary>
        private bool Disposed = false;

        /// <summary> Maintains all objects in cache </summary>
        private Hashtable CacheTable = new Hashtable();

        /// <summary> Timer thread that wakes up every 10 minutes to clean the cache </summary>
        private SynchronousTimer CleanupTimer = null;

        /// <summary> Provides synchronous access to the hash table </summary>
        private ReaderWriterLockSlim Lock = new ReaderWriterLockSlim();

        #endregion

        #region Constructor

        /// <summary> Called when the singleton is initialized </summary>
        private CacheManager()
        {
            // Initialize the timer - Fire every 10 minutes
            CleanupTimer = new SynchronousTimer(CleanupThread, null, 1000 * 60 * 10, "CacheMgr");
        }

        /// <summary> Called to create the singleton </summary>
        static CacheManager()
        {
            This = new CacheManager();
        }

        /// <summary> Called to init the singleton </summary>
        public static void Start()
        {
        }

        /// <summary> Called to dispose the manager </summary>
        public static void Close()
        {
            This.Dispose();
        }

        /// <summary> Called to dipose the log </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary> Called to dispose the log </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (!Disposed)
            {
                Disposed = true;

                if (disposing)
                {
                    // Did we already clean up the cache
                    if (CleanupTimer != null)
                    {
                        Lock.EnterWriteLock();
                        try
                        {
                            // Stop the timer
                            CleanupTimer.Dispose();

                            // Clear the cache
                            CacheTable.Clear();

                            // Release the timer
                            CleanupTimer = null;

                            // Release the hasttable
                            CacheTable = null;
                        }
                        finally
                        {
                            Lock.ExitWriteLock();
                        }
                    }
                }
            }
        }

        #endregion

        #region Private Methods

        /// <summary> Called by the CleanupTimer to keep the cache clean </summary>
        /// <param name="dataObject"></param>
        /// <param name="theTimer"></param>
        private static void CleanupThread(object dataObject, SynchronousTimer theTimer)
        {
            try
            {
                This.Lock.EnterWriteLock();
                try
                {
                    // Store keys to remove
                    ArrayList removeObjectKeys = new ArrayList();

                    // Find keys to remove
                    foreach (string key in This.CacheTable.Keys)
                    {
                        // Capature the object in cache
                        CachedObject cachedObject = (CachedObject)This.CacheTable[key];

                        // Validate that it is still good
                        TimeSpan timeDiff = DateTime.Now - cachedObject.Timestamp;
                        if (timeDiff.TotalSeconds > cachedObject.TimeToLive)
                        {
                            removeObjectKeys.Add(key);
                        }
                    }

                    // Remove these keys
                    foreach (string key in removeObjectKeys)
                    {
                        This.CacheTable.Remove(key);
                    }
                }
                finally
                {
                    This.Lock.ExitWriteLock();
                }
            }

            catch
            {
            }
        }

        #endregion

        #region Cache Methods

        /// <summary> Called to add an object to the cache </summary>
        /// <param name="id"></param>
        /// <param name="objectData"></param>
        public static void Add(string id, object objectData)
        {
            This.Lock.EnterWriteLock();
            try
            {
                // Create a CachedObject to store in cache
                CachedObject cachedObject = new CachedObject(id, objectData);

                // Remove the object from the cache
                if (This.CacheTable.ContainsKey(id))
                {
                    This.CacheTable.Remove(id);
                }

                // Add the object to the cache
                This.CacheTable.Add(id, cachedObject);
            }
            finally
            {
                This.Lock.ExitWriteLock();
            }
        }
        
        /// <summary> Called to add an object to the cache </summary>
        /// <param name="id"></param>
        /// <param name="objectData"></param>
        /// <param name="timeToLive"></param>
        public static void Add(string id, object objectData, int timeToLive)
        {
            This.Lock.EnterWriteLock();
            try
            {
                // Create a CachedObject to store in cache
                CachedObject cachedObject = new CachedObject(id, objectData, timeToLive);

                // Remove the object from the cache
                if (This.CacheTable.ContainsKey(id))
                {
                    This.CacheTable.Remove(id);
                }

                // Add the object to the cache
                This.CacheTable.Add(id, cachedObject);
            }
            finally
            {
                This.Lock.ExitWriteLock();
            }
        }

        /// <summary> Called to remove an object from the cache </summary>
        /// <param name="id"></param>
        public static void Remove(string id)
        {
            This.Lock.EnterWriteLock();
            try
            {
                // Remove the object from the cache
                if (This.CacheTable.ContainsKey(id))
                {
                    This.CacheTable.Remove(id);
                }
            }
            finally
            {
                This.Lock.ExitWriteLock();
            }
        }

        /// <summary> Called to check the cache for an object and return it if found </summary>
        /// <param name="id"></param>
        /// <returns> The object in cache else null </returns>
        public static object Check(string id)
        {
            object objectData = null;

            This.Lock.EnterUpgradeableReadLock();
            try
            {
                // Check if the object is in the cache. If it is, pull the data
                if (This.CacheTable.ContainsKey(id))
                {
                    // Capature the object in cache
                    CachedObject cachedObject = (CachedObject)This.CacheTable[id];

                    // Validate that it is still good
                    TimeSpan timeDiff = DateTime.Now - cachedObject.Timestamp;
                    if (timeDiff.TotalSeconds <= cachedObject.TimeToLive)
                    {
                        // If the time to live is acceptable return the object
                        objectData = ((CachedObject)This.CacheTable[id]).ObjectData;
                    }
                    else
                    {
                        This.Lock.EnterWriteLock();
                        try
                        {
                            // The object is old, remove it
                            This.CacheTable.Remove(id);
                        }
                        finally
                        {
                           This.Lock.ExitWriteLock(); 
                        }
                    }
                }
            }
            finally
            {
                This.Lock.ExitUpgradeableReadLock();
            }

            return objectData;
        }

        /// <summary> Called to clear the current cache </summary>
        public static void Clear()
        {
            This.Lock.EnterWriteLock();
            try
            {
                This.CacheTable.Clear();
            }
            finally
            {
                This.Lock.ExitWriteLock();
            }
        }

        #endregion

        #region Generate Key Methods

        /// <summary> Called to generate a key </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string GenerateKey(string name, int value)
        {
            return string.Format(name, value.ToString());
        }

        /// <summary> Called to generate a key </summary>
        /// <param name="name"></param>
        /// <param name="value1"></param>
        /// <param name="value2"></param>
        /// <returns></returns>
        public static string GenerateKey(string name, int value1, int value2)
        {
            return string.Format(name, value1.ToString(), value2.ToString());
        }

        /// <summary> Called to generate a key </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string GenerateKey(string name, string value)
        {
            return string.Format(name, value);
        }

        /// <summary> Called to generate a key </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string GenerateKey(string name, decimal value)
        {
            return string.Format(name, value.ToString());
        }

        #endregion
    }

    #endregion
}
