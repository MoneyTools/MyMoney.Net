//-----------------------------------------------------------------------
// <copyright file="EventTraceWatcher.cs" company="Microsoft">
//   (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------
// Author: Daniel Vasquez Lopez
//------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using Microsoft.VisualStudio.Diagnostics.PerformanceProvider.Interop;

namespace Microsoft.VisualStudio.Diagnostics.PerformanceProvider.Listener
{
    internal class EventRecordArgs : EventArgs
    {
        public EventRecordArgs(EventRecord r)
        {
            this.EventRecord = r;
        }
        public EventRecord EventRecord { get; set; }
    }

    public class EventTraceSession
    {
        bool stopProcessing;
        private string eventLogSessionName;
        private long sessionHandle;
        private ulong traceHandle = 0;
        private IntPtr traceProperties;
        private EventTraceLogfile logFile;
        private delegate void ProcessTraceDelegate(ulong traceHandle);
        private System.Action processEventsDelegate;

        internal event EventHandler<EventRecordArgs> EventArrived;

        public EventTraceSession(string eventLogSessionName)
        {
            this.eventLogSessionName = eventLogSessionName;
        }


        static Guid sessionGuid = Guid.NewGuid();

        private void CopyString(String s, IntPtr buffer)
        {
            for (int i = 0, n = s.Length; i < n; i++)
            {
                char c = s[i];
                Marshal.WriteInt16(buffer, c);
                buffer = new IntPtr((long)buffer + 1);
            }
            Marshal.WriteInt16(buffer, 0);
        }

        string LogFilePath
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PerformanceGraph");
                System.IO.Directory.CreateDirectory(dir);
                return Path.Combine(dir, "LogFile.etl");
            }
        }

        private void StartSession()
        {
            int status = 0;

            string path = LogFilePath;

            // Allocate memory for the session properties. The memory must
            // be large enough to include the log file name and session name,
            // which get appended to the end of the session properties structure.

            uint structSize = (uint)Marshal.SizeOf(typeof(EVENT_TRACE_PROPERTIES));
            uint pathSize = (uint)(sizeof(char) * path.Length) + 1;
            uint sessionNameSize = (uint)(sizeof(char) * this.eventLogSessionName.Length) + 1;
            uint bufferSize = structSize + pathSize + sessionNameSize;
            EVENT_TRACE_PROPERTIES pSessionProperties = new EVENT_TRACE_PROPERTIES();
            this.traceProperties = Marshal.AllocCoTaskMem((int)bufferSize);
            if (this.traceProperties == IntPtr.Zero)
            {
                throw new OutOfMemoryException();
            }

            try
            {
                // Set the session properties. You only append the log file name
                // to the properties structure; the StartTrace function appends
                // the session name for you.
                const int WNODE_FLAG_TRACED_GUID = 0x00020000;
                const int EVENT_TRACE_REAL_TIME_MODE = 0x00000100;
                const int ERROR_ALREADY_EXISTS = 183;

                pSessionProperties.Wnode.BufferSize = bufferSize;
                pSessionProperties.Wnode.Flags = WNODE_FLAG_TRACED_GUID;
                pSessionProperties.Wnode.ClientContext = 1; //QPC clock resolution
                pSessionProperties.Wnode.Guid = sessionGuid;
                pSessionProperties.LogFileMode = EVENT_TRACE_REAL_TIME_MODE; //  EVENT_TRACE_FILE_MODE_SEQUENTIAL;
                pSessionProperties.MaximumFileSize = 1;  // 1 MB
                pSessionProperties.LoggerNameOffset = structSize;
                pSessionProperties.LogFileNameOffset = structSize + pathSize;

                // Copy the data to the buffer
                Marshal.StructureToPtr(pSessionProperties, this.traceProperties, false);
                CopyString(path, new IntPtr((long)this.traceProperties + structSize));
                CopyString(this.eventLogSessionName, new IntPtr((long)this.traceProperties + structSize + pathSize));

                this.sessionHandle = 0;
                // Create the trace session.
                status = NativeMethods.StartTrace(out sessionHandle, this.eventLogSessionName, this.traceProperties);
                if (status == ERROR_ALREADY_EXISTS)
                {
                    // close unclosed previous trace.
                    StopTrace();
                    status = NativeMethods.StartTrace(out sessionHandle, this.eventLogSessionName, this.traceProperties);
                }

                if (0 != status)
                {
                    throw new System.ComponentModel.Win32Exception(status);
                }

            }
            finally
            {

            }

        }

        public long SessionHandle { get { return sessionHandle; } }
        
        const uint RealTime = 0x00000100;
        const uint EventRecord = 0x10000000;
        const uint PrivateLoggerMode = 0x00000800;
        const ulong INVALID_HANDLE_VALUE = unchecked((ulong)(-1));
        private IAsyncResult asyncResult;

        public void OpenTraceLog(string logFileName)
        {
            stopProcessing = false; 
            logFile = new EventTraceLogfile();
            logFile.BufferCallback = TraceEventBufferCallback;
            logFile.EventRecordCallback = EventRecordCallback;
            logFile.ProcessTraceMode = EventRecord;
            logFile.LogFileName = logFileName;

            this.traceHandle = NativeMethods.OpenTrace(ref logFile);

            if (INVALID_HANDLE_VALUE == traceHandle)
            {
                int error = Marshal.GetLastWin32Error();
                if (error != 0)
                {
                    throw new System.ComponentModel.Win32Exception(error);
                }
            }

            processEventsDelegate = new System.Action(ProcessTraceInBackground);
            asyncResult = processEventsDelegate.BeginInvoke(null, this);
        }        

        private void EventRecordCallback([In] ref EventRecord eventRecord)
        {
            if (EventArrived != null)
            {
                EventArrived(this, new EventRecordArgs(eventRecord));
            }
        }

        int refCount;

        public void StartTracing()
        {
            refCount++;
            if (refCount > 1)
            {
                return;
            }
                
            stopProcessing = false;
            StartSession();

            logFile = new EventTraceLogfile();
            logFile.LoggerName = this.eventLogSessionName;
            logFile.EventRecordCallback = EventRecordCallback;

            logFile.ProcessTraceMode = EventRecord | RealTime;
            this.traceHandle = NativeMethods.OpenTrace(ref logFile);

            int error = Marshal.GetLastWin32Error();
            if (error != 0)
            {
                throw new System.ComponentModel.Win32Exception(error);
            }

            processEventsDelegate = new System.Action(ProcessTraceInBackground);
            asyncResult = processEventsDelegate.BeginInvoke(null, this);
        }

        // Return how many ticks per second.
        public long PerformanceFrequency
        {
            get
            {
                // Actually according to Vance, ETW uses 100ns frequency.
                return 10000000;
                //return logFile.LogfileHeader.PerfFreq;
            }
        }

        public void StopTracing()
        {
            refCount--;
            if (refCount != 0)
            {
                return;
            }

            stopProcessing = true;
            NativeMethods.CloseTrace(this.traceHandle);
            traceHandle = 0;
            processEventsDelegate.EndInvoke(asyncResult);
            StopTrace();
            Marshal.FreeCoTaskMem(this.traceProperties);
            this.traceProperties = IntPtr.Zero;
        }

        private void StopTrace()
        {
            if (this.traceProperties != IntPtr.Zero)
            {
                const int EVENT_TRACE_CONTROL_STOP = 1;
                int rc = NativeMethods.ControlTrace(this.sessionHandle, this.eventLogSessionName, this.traceProperties, EVENT_TRACE_CONTROL_STOP);
                if (rc != 0)
                {
                    System.Diagnostics.Trace.WriteLine("Error stopping trace: " + rc);
                }
            }
        }


        // Private data / methods 
        [System.Security.SecuritySafeCritical]
        [AllowReversePInvokeCalls]
        private bool TraceEventBufferCallback(IntPtr rawLogFile)
        {
            return !stopProcessing;
        }


        private void ProcessTraceInBackground()
        {
            Exception asyncException = null;

            try
            {
                ulong[] array = { this.traceHandle };
                int error = NativeMethods.ProcessTrace(array, 1, IntPtr.Zero, IntPtr.Zero);
                OnTraceComplete();

                if (error != 0)
                {
                    throw new System.ComponentModel.Win32Exception(error);
                }
            }
            catch (Exception exception)
            {
                asyncException = exception;
            }

            // Send exception to subscribers.
            if (asyncException != null )
            {                
                // todo...
            }
        }

        public event EventHandler TraceComplete;

        void OnTraceComplete()
        {
            if (TraceComplete != null)
            {
                TraceComplete(this, EventArgs.Empty);
            }
        }

    }

    public class EventTraceWatcher : IDisposable
    {
        private readonly Guid providerId;
        private volatile bool enabled;
        private ReaderWriterLockSlim rwlock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        private Dictionary<byte[], TraceEventInfoWrapper> traceEventInfoCache = new Dictionary<byte[], TraceEventInfoWrapper>();
        private HashSet<Guid> providers = new HashSet<Guid>();
        private EventTraceSession session;
        public event EventHandler<EventArrivedEventArgs> EventArrived;

        public EventTraceWatcher(Guid providerId, EventTraceSession session)
        {
            this.providerId = providerId;
            this.session = session;
            session.EventArrived += new EventHandler<EventRecordArgs>(InternalEventArrived);
        }

        void InternalEventArrived(object sender, EventRecordArgs e)
        {
            if (e.EventRecord.EventHeader.ProviderId == this.providerId && EventArrived != null)
            {
                try
                {
                    var args = this.CreateEventArgsFromEventRecord(e.EventRecord);
                    if (args != null)
                    {
                        EventArrived(this, args);
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine("EventArrived exception: " + ex.Message);
                }
            }
        }

        ~EventTraceWatcher()
        {
            Cleanup();
        }

        public bool Enabled
        {
            get
            {
                return enabled;
            }
            set
            {
                rwlock.EnterReadLock();
                if (enabled == value)
                {
                    rwlock.ExitReadLock();
                    return;
                }
                rwlock.ExitReadLock();

                rwlock.EnterWriteLock();
                try
                {
                    if (value)
                    {
                        StartTracing();
                    }
                    else
                    {
                        StopTracing();
                    }
                    enabled = value;
                }
                finally
                {
                    rwlock.ExitWriteLock();
                }
            }
        }

        void StartTracing()
        {
            session.StartTracing();

            // Enable the providers that you want to log events to your session.
            const int EVENT_CONTROL_CODE_ENABLE_PROVIDER = 1;
            const short TRACE_LEVEL_INFORMATION = 4;
            Guid id = providerId;
            int status = NativeMethods.EnableTraceEx2(session.SessionHandle, ref id,
                EVENT_CONTROL_CODE_ENABLE_PROVIDER,
                TRACE_LEVEL_INFORMATION,
                0,
                0,
                0,
                IntPtr.Zero);

            if (0 != status)
            {
                throw new InvalidOperationException("Unexpected error from EnableTraceEx2: " + status);
            }
        }

        public long PerformanceFrequency
        {
            get
            {
                return session != null ? session.PerformanceFrequency : 0;
            }
        }

        void StopTracing()
        {
            session.StopTracing();
        }

        private void Cleanup()
        {
            this.Enabled = false;
            foreach (TraceEventInfoWrapper value in traceEventInfoCache.Values)
            {
                value.Dispose();
            }
            traceEventInfoCache = null;
            rwlock.Dispose();
        }

        private static byte[] CreateComposedKey(Guid providerId, byte opcode)
        {
            const int GuidSizePlusOpcodeSize = 17;
            byte[] key = new byte[GuidSizePlusOpcodeSize];

            // Copy guid
            Buffer.BlockCopy(providerId.ToByteArray(), 0, key, 0, GuidSizePlusOpcodeSize - 1);

            // Copy opcode
            key[GuidSizePlusOpcodeSize - 1] = opcode;
            return key;
        }

        protected virtual EventArrivedEventArgs CreateEventArgs()
        {
            return new EventArrivedEventArgs();
        }

        private EventArrivedEventArgs CreateEventArgsFromEventRecord(EventRecord eventRecord)
        {
            if (traceEventInfoCache == null)
            {
                return null;
            }

            Guid providerId = eventRecord.EventHeader.ProviderId;
            TraceEventInfoWrapper traceEventInfo;
            byte[] key = CreateComposedKey(providerId, eventRecord.EventHeader.EventDescriptor.Opcode);
            bool shouldDispose = false;

            // Find the event information (schema).
            if (!traceEventInfoCache.TryGetValue(key, out traceEventInfo))
            {
                traceEventInfo = new TraceEventInfoWrapper(eventRecord);

                try
                {
                    traceEventInfoCache.Add(key, traceEventInfo);
                }
                catch (ArgumentException)
                {
                    // Someone other thread added this entry.
                    shouldDispose = true;
                }
            }

            // Get the properties using the current event information (schema).
            PropertyBag properties = traceEventInfo.GetProperties(eventRecord);

            EventArrivedEventArgs args = CreateEventArgs();
            args.Timestamp = eventRecord.EventHeader.TimeStamp;
            args.ProviderId = providerId;
            args.EventId = eventRecord.EventHeader.EventDescriptor.Id;
            args.EventName = traceEventInfo.EventName;
            args.Properties = properties;

            IntPtr addr = eventRecord.UserData;
            int length = eventRecord.UserDataLength;
            if (addr != IntPtr.Zero && length > 0)
            {
                byte[] buffer = new byte[length];
                for (int i = 0; i < length; i++)
                {
                    buffer[i] = Marshal.ReadByte(addr);
                    addr = new IntPtr((long)addr + 1);
                }

                using (System.IO.BinaryReader reader = new System.IO.BinaryReader(new System.IO.MemoryStream(buffer)))
                {
                    args.ParseUserData(args.EventId, reader);
                }
            }
           
            // Dispose the event information because it doesn't live in the cache
            if (shouldDispose)
            {
                traceEventInfo.Dispose();
            }

            return args;
        }

        public void Dispose()
        {
            Cleanup();
            GC.SuppressFinalize(this);
        }

        private void EventRecordCallback([In] ref EventRecord eventRecord)
        {
            if (providerId != eventRecord.EventHeader.ProviderId)
            {
                return;
            }

            if (EventArrived != null)
            {
                EventArrivedEventArgs e = CreateEventArgsFromEventRecord(eventRecord);
                EventArrived(this, e);
            }
        }

        public void ExportResource(string embeddedResourceName, string fileName)
        {
            using (FileStream fs = new FileStream(fileName, FileMode.Create))
            {
                using (Stream s = this.GetType().Assembly.GetManifestResourceStream(embeddedResourceName))
                {
                    byte[] buffer = new byte[100000];
                    int read = s.Read(buffer, 0, buffer.Length);
                    while (read > 0)
                    {
                        fs.Write(buffer, 0, read);
                        read = s.Read(buffer, 0, buffer.Length);
                    }
                }
            }
        }

        public void LoadManifest(string embeddedResourceName)
        {
            string path = Path.Combine(Path.GetTempPath(), embeddedResourceName);
            ExportResource(embeddedResourceName, path);
            LoadManifest(new Uri(path));
            File.Delete(path);            
        }

        public void LoadManifest(Uri uri)
        {
            string path = uri.LocalPath;
            string system = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string wevtutil = Path.Combine(system, "wevtutil.exe");
            StringBuilder sb = new StringBuilder();
            RunProcess(wevtutil, string.Format("um \"{0}\"", path), sb); // in case it has changed.
            RunProcess(wevtutil, string.Format("im \"{0}\"", path), sb);  
        }

        private static void RunProcess(string exe, string args, StringBuilder output)
        {
            Process p = Process.Start(new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            p.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
            {
                output.AppendLine(e.Data);
            });
            p.ErrorDataReceived += new DataReceivedEventHandler((sender, e) =>
            {
                output.AppendLine(e.Data);
            });
            p.Start();
            p.WaitForExit();
            if (p.ExitCode != 0)
            {
                Debug.WriteLine("Process returned " + p.ExitCode + " command line: " + exe + " " + args);
            }
        }
    }
}