//-----------------------------------------------------------------------
// <copyright file="EventTraceWatcher.cs" company="Microsoft">
//   (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------
// Author: Daniel Vasquez Lopez
//------------------------------------------------------------------------------
using Microsoft.VisualStudio.Diagnostics.PerformanceProvider.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

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
        private bool stopProcessing;
        private readonly string eventLogSessionName;
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

        private static Guid sessionGuid = Guid.NewGuid();

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

        private string LogFilePath
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

            string path = this.LogFilePath;

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
                this.CopyString(path, new IntPtr((long)this.traceProperties + structSize));
                this.CopyString(this.eventLogSessionName, new IntPtr((long)this.traceProperties + structSize + pathSize));

                this.sessionHandle = 0;
                // Create the trace session.
                status = NativeMethods.StartTrace(out this.sessionHandle, this.eventLogSessionName, this.traceProperties);
                if (status == ERROR_ALREADY_EXISTS)
                {
                    // close unclosed previous trace.
                    this.StopTrace();
                    status = NativeMethods.StartTrace(out this.sessionHandle, this.eventLogSessionName, this.traceProperties);
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

        public long SessionHandle { get { return this.sessionHandle; } }

        private const uint RealTime = 0x00000100;
        private const uint EventRecord = 0x10000000;
        private const uint PrivateLoggerMode = 0x00000800;
        private const ulong INVALID_HANDLE_VALUE = unchecked((ulong)-1);
        private IAsyncResult asyncResult;

        public void OpenTraceLog(string logFileName)
        {
            this.stopProcessing = false;
            this.logFile = new EventTraceLogfile();
            this.logFile.BufferCallback = this.TraceEventBufferCallback;
            this.logFile.EventRecordCallback = this.EventRecordCallback;
            this.logFile.ProcessTraceMode = EventRecord;
            this.logFile.LogFileName = logFileName;

            this.traceHandle = NativeMethods.OpenTrace(ref this.logFile);

            if (INVALID_HANDLE_VALUE == this.traceHandle)
            {
                int error = Marshal.GetLastWin32Error();
                if (error != 0)
                {
                    throw new System.ComponentModel.Win32Exception(error);
                }
            }

            this.processEventsDelegate = new System.Action(this.ProcessTraceInBackground);
            this.asyncResult = this.processEventsDelegate.BeginInvoke(null, this);
        }

        private void EventRecordCallback([In] ref EventRecord eventRecord)
        {
            if (EventArrived != null)
            {
                EventArrived(this, new EventRecordArgs(eventRecord));
            }
        }

        private int refCount;

        public void StartTracing()
        {
            this.refCount++;
            if (this.refCount > 1)
            {
                return;
            }

            this.stopProcessing = false;
            this.StartSession();

            this.logFile = new EventTraceLogfile();
            this.logFile.LoggerName = this.eventLogSessionName;
            this.logFile.EventRecordCallback = this.EventRecordCallback;

            this.logFile.ProcessTraceMode = EventRecord | RealTime;
            this.traceHandle = NativeMethods.OpenTrace(ref this.logFile);

            int error = Marshal.GetLastWin32Error();
            if (error != 0)
            {
                throw new System.ComponentModel.Win32Exception(error);
            }

            this.processEventsDelegate = new System.Action(this.ProcessTraceInBackground);
            this.asyncResult = this.processEventsDelegate.BeginInvoke(null, this);
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
            this.refCount--;
            if (this.refCount != 0)
            {
                return;
            }

            this.stopProcessing = true;
            NativeMethods.CloseTrace(this.traceHandle);
            this.traceHandle = 0;
            this.processEventsDelegate.EndInvoke(this.asyncResult);
            this.StopTrace();
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
            return !this.stopProcessing;
        }


        private void ProcessTraceInBackground()
        {
            Exception asyncException = null;

            try
            {
                ulong[] array = { this.traceHandle };
                int error = NativeMethods.ProcessTrace(array, 1, IntPtr.Zero, IntPtr.Zero);
                this.OnTraceComplete();

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
            if (asyncException != null)
            {
                // todo...
            }
        }

        public event EventHandler TraceComplete;

        private void OnTraceComplete()
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
        private readonly ReaderWriterLockSlim rwlock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        private Dictionary<byte[], TraceEventInfoWrapper> traceEventInfoCache = new Dictionary<byte[], TraceEventInfoWrapper>();
        private readonly HashSet<Guid> providers = new HashSet<Guid>();
        private readonly EventTraceSession session;
        public event EventHandler<EventArrivedEventArgs> EventArrived;

        public EventTraceWatcher(Guid providerId, EventTraceSession session)
        {
            this.providerId = providerId;
            this.session = session;
            session.EventArrived += new EventHandler<EventRecordArgs>(this.InternalEventArrived);
        }

        private void InternalEventArrived(object sender, EventRecordArgs e)
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
            this.Cleanup();
        }

        public bool Enabled
        {
            get
            {
                return this.enabled;
            }
            set
            {
                this.rwlock.EnterReadLock();
                if (this.enabled == value)
                {
                    this.rwlock.ExitReadLock();
                    return;
                }
                this.rwlock.ExitReadLock();

                this.rwlock.EnterWriteLock();
                try
                {
                    if (value)
                    {
                        this.StartTracing();
                    }
                    else
                    {
                        this.StopTracing();
                    }
                    this.enabled = value;
                }
                finally
                {
                    this.rwlock.ExitWriteLock();
                }
            }
        }

        private void StartTracing()
        {
            this.session.StartTracing();

            // Enable the providers that you want to log events to your session.
            const int EVENT_CONTROL_CODE_ENABLE_PROVIDER = 1;
            const short TRACE_LEVEL_INFORMATION = 4;
            Guid id = this.providerId;
            int status = NativeMethods.EnableTraceEx2(this.session.SessionHandle, ref id,
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
                return this.session != null ? this.session.PerformanceFrequency : 0;
            }
        }

        private void StopTracing()
        {
            this.session.StopTracing();
        }

        private void Cleanup()
        {
            this.Enabled = false;
            foreach (TraceEventInfoWrapper value in this.traceEventInfoCache.Values)
            {
                value.Dispose();
            }
            this.traceEventInfoCache = null;
            this.rwlock.Dispose();
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
            if (this.traceEventInfoCache == null)
            {
                return null;
            }

            Guid providerId = eventRecord.EventHeader.ProviderId;
            TraceEventInfoWrapper traceEventInfo;
            byte[] key = CreateComposedKey(providerId, eventRecord.EventHeader.EventDescriptor.Opcode);
            bool shouldDispose = false;

            // Find the event information (schema).
            if (!this.traceEventInfoCache.TryGetValue(key, out traceEventInfo))
            {
                traceEventInfo = new TraceEventInfoWrapper(eventRecord);

                try
                {
                    this.traceEventInfoCache.Add(key, traceEventInfo);
                }
                catch (ArgumentException)
                {
                    // Someone other thread added this entry.
                    shouldDispose = true;
                }
            }

            // Get the properties using the current event information (schema).
            PropertyBag properties = traceEventInfo.GetProperties(eventRecord);

            EventArrivedEventArgs args = this.CreateEventArgs();
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
            this.Cleanup();
            GC.SuppressFinalize(this);
        }

        private void EventRecordCallback([In] ref EventRecord eventRecord)
        {
            if (this.providerId != eventRecord.EventHeader.ProviderId)
            {
                return;
            }

            if (EventArrived != null)
            {
                EventArrivedEventArgs e = this.CreateEventArgsFromEventRecord(eventRecord);
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
            this.ExportResource(embeddedResourceName, path);
            this.LoadManifest(new Uri(path));
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