CSharpUtilities
===============

Copyright 2011-2013 Ardan Studios. All rights reserved.<br />
Use of this source code is governed by a BSD-style license that can be found in the LICENSE handle.

This project contains base utility classes required for most application.

<b>CacheManager</b><br />
This class provides a inproc caching system. You can store any data with a ket and set the time to expire.

<b>CryptoProvider</b><br />
Provides crypto support for all the common protocols

<b>Impersonate</b><br />
Used to allow your application to impersonate a user

<b>LogManager</b> (Sample Code Provided)<br />
The Log Manager has been built for heavy use in all types of applications.

Key Features</b><br />
1. Write to multiple files using the log key<br />
2. Uses TLS to help writing to log keys deep within libraries without the need to pass the log key<br />
3. If HTTP Session exists, it will create log files for each unique session<br />
4. Can handle high capacity log writes<br />
5. Monitors open files and closes files not written to after 2 minutes<br />
6. Maintains directories for each day and will clean up old folders<br />
7. Email notification for exceptions or events. Will provide session state and url to log file

Directory Structure<br />
C:\Logs\Test\06-27-2013\0000\0000\00000000Global

Sample Log

A          B            C    D  E                F<br />
06-27-2013 09:11:34.352 0.49 10 THREAD UNKNOWN : Starting test application

A: Date the log write was issued<br />
B: Time the log write was issued<br />
C: The duration in milliseconds between the log write request and actual write<br />
D: Thread Id<br />
E: Thread Name<br />
F: The messages

<b>MessagingClient</b>
A generic socket based message client and server endpoint

<b>NamedPipes</b>
Basic named pipes support

<b>PerformanceCounters</b>
Collection performance counters for your running application

<b>Serialize</b>
Serial C# objects to JSON. Use JSON.net instead

<b>SynchronousTimer</b>
A timer that fires based on a set interval. The time will not fire again while executing work

<b>UtilSockets</b>
A fully functional implementation for socket clients and servers.

<b>UtilThreading</b> (Sample Code Provided)<br />
Thread Pooling using the IOCP framework. Create multiple thread pools for your application

The ThreadPool using IOCP underneath to provide concurrency controlled thread pools.

The .NET Thread Pool also uses IOCP underneath but each application is only given a single thead pool.<br />
To learn more please read: http://www.theukwebdesigncompany.com/articles/iocp-thread-pooling.php
