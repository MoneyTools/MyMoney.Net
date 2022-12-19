//-----------------------------------------------------------------------
// <copyright file="NativeMethods.cs" company="Microsoft">
//   (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------
// Author: Daniel Vasquez Lopez
//------------------------------------------------------------------------------
using System;
using System.Runtime.InteropServices;

namespace Microsoft.VisualStudio.Diagnostics.PerformanceProvider.Interop
{
    internal static class NativeMethods
    {
        [DllImport("advapi32.dll", ExactSpelling = true, EntryPoint = "OpenTraceW", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern ulong OpenTrace(ref EventTraceLogfile logfile);

        [DllImport("advapi32.dll", ExactSpelling = true, EntryPoint = "ProcessTrace")]
        internal static extern int ProcessTrace(ulong[] HandleArray,
                                                 uint HandleCount,
                                                 IntPtr StartTime,
                                                 IntPtr EndTime);

        [DllImport("advapi32.dll", ExactSpelling = true, EntryPoint = "CloseTrace")]
        internal static extern int CloseTrace(ulong traceHandle);

        [DllImport("advapi32.dll", EntryPoint = "StartTrace", CharSet = CharSet.Unicode)]
        internal static extern int StartTrace(out long handle, string sessionName, IntPtr properties); // ref EVENT_TRACE_PROPERTIES properties);

        [DllImport("advapi32.dll", EntryPoint = "ControlTrace", CharSet = CharSet.Unicode)]
        internal static extern int ControlTrace(long sessionHandle, string sessionName, IntPtr properties, int ControlCode);

        [DllImport("advapi32.dll", EntryPoint = "EnableTraceEx2", CharSet = CharSet.Unicode)]
        internal static extern int EnableTraceEx2(long handle, ref Guid providerId, int controlCode, short level, long matchAnyKeyword, long matchAllkeyword, int timeout, IntPtr ptr); // ref ENABLE_TRACE_PARAMETERS enableparameters);

        [DllImport("tdh.dll", ExactSpelling = true, EntryPoint = "TdhGetEventInformation")]
        internal static extern int TdhGetEventInformation(
            ref EventRecord Event,
            uint TdhContextCount,
            IntPtr TdhContext,
            [Out] IntPtr eventInfoPtr,
            ref int BufferSize);

    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct ENABLE_TRACE_PARAMETERS
    {
        int Version;
        int EnableProperty;
        int ControlFlags;
        Guid SourceId;
        IntPtr EnableFilterDesc; // EVENT_FILTER_DESCRIPTOR 
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct EVENT_FILTER_DESCRIPTOR
    {
        long Ptr;
        int Size;
        int Type;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WNODE_HEADER
    {
        internal uint BufferSize;
        internal uint ProviderId;
        internal long HistoricalContext;
        internal long TimeStamp;
        internal Guid Guid;
        internal uint ClientContext;
        internal uint Flags;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct EVENT_TRACE_PROPERTIES
    {
        internal WNODE_HEADER Wnode;
        internal uint BufferSize;
        internal uint MinimumBuffers;
        internal uint MaximumBuffers;
        internal uint MaximumFileSize;
        internal uint LogFileMode;
        internal uint FlushTimer;
        internal uint EnableFlags;
        internal int AgeLimit;
        internal uint NumberOfBuffers;
        internal uint FreeBuffers;
        internal uint EventsLost;
        internal uint BuffersWritten;
        internal uint LogBuffersLost;
        internal uint RealTimeBuffersLost;
        internal int LoggerThreadId;
        internal uint LogFileNameOffset;
        internal uint LoggerNameOffset;

    }

    //	Delegates for use with ETW EVENT_TRACE_LOGFILEW struct.
    //	These are the callbacks that ETW will call while processing a moduleFile
    //	so that we can process each line of the trace moduleFile.
    internal delegate bool EventTraceBufferCallback([In] IntPtr logfile);
    internal delegate void EventRecordCallback([In] ref EventRecord eventRecord);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct Win32TimeZoneInfo
    {
        internal int Bias;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        internal char[] StandardName;
        internal SystemTime StandardDate;
        internal int StandardBias;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        internal char[] DaylightName;
        internal SystemTime DaylightDate;
        internal int DaylightBias;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SystemTime
    {
        internal short Year;
        internal short Month;
        internal short DayOfWeek;
        internal short Day;
        internal short Hour;
        internal short Minute;
        internal short Second;
        internal short Milliseconds;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct TraceLogfileHeader
    {
        internal uint BufferSize;
        internal uint Version;
        internal uint ProviderVersion;
        internal uint NumberOfProcessors;
        internal long EndTime;
        internal uint TimerResolution;
        internal uint MaximumFileSize;
        internal uint LogFileMode;
        internal uint BuffersWritten;
        internal Guid LogInstanceGuid;
        internal IntPtr LoggerName;
        internal IntPtr LogFileName;
        internal Win32TimeZoneInfo TimeZone;
        internal long BootTime;
        internal long PerfFreq;
        internal long StartTime;
        internal uint ReservedFlags;
        internal uint BuffersLost;
    }

    internal enum PropertyFlags
    {
        PropertyStruct = 0x1,
        PropertyParamLength = 0x2,
        PropertyParamCount = 0x4,
        PropertyWBEMXmlFragment = 0x8,
        PropertyParamFixedLength = 0x10
    }

    internal enum TdhInType : ushort
    {
        Null,
        UnicodeString,
        AnsiString,
        Int8,
        UInt8,
        Int16,
        UInt16,
        Int32,
        UInt32,
        Int64,
        UInt64,
        Float,
        Double,
        Boolean,
        Binary,
        Guid,
        Pointer,
        FileTime,
        SystemTime,
        SID,
        HexInt32,
        HexInt64,  // End of winmeta intypes
        CountedString = 300, // Start of TDH intypes for WBEM
        CountedAnsiString,
        ReversedCountedString,
        ReversedCountedAnsiString,
        NonNullTerminatedString,
        NonNullTerminatedAnsiString,
        UnicodeChar,
        AnsiChar,
        SizeT,
        HexDump,
        WbemSID
    }

    internal enum TdhOutType : ushort
    {
        Null,
        String,
        DateTime,
        Byte,
        UnsignedByte,
        Short,
        UnsignedShort,
        Int,
        UnsignedInt,
        Long,
        UnsignedLong,
        Float,
        Double,
        Boolean,
        Guid,
        HexBinary,
        HexInt8,
        HexInt16,
        HexInt32,
        HexInt64,
        PID,
        TID,
        PORT,
        IPV4,
        IPV6,
        SocketAddress,
        CimDateTime,
        EtwTime,
        Xml,
        ErrorCode,              // End of winmeta outtypes
        ReducedString = 300,    // Start of TDH outtypes for WBEM
        NoPrint
    }

    [StructLayout(LayoutKind.Explicit)]
    internal sealed class EventPropertyInfo
    {
        [FieldOffset(0)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.StyleCop", "SA1401")]
        internal PropertyFlags Flags;
        [FieldOffset(4)]
        internal uint NameOffset;

        [StructLayout(LayoutKind.Sequential)]
        internal struct NonStructType
        {
            internal TdhInType InType;
            internal TdhOutType OutType;
            internal uint MapNameOffset;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct StructType
        {
            internal ushort StructStartIndex;
            internal ushort NumOfStructMembers;
            private uint Padding;
        }

        [FieldOffset(8)]
        internal NonStructType NonStructTypeValue;
        [FieldOffset(8)]
        internal StructType StructTypeValue;

        [FieldOffset(16)]
        internal ushort CountPropertyIndex;
        [FieldOffset(18)]
        internal ushort LengthPropertyIndex;
        [FieldOffset(20)]
        private uint Reserved;
    }

    internal enum TemplateFlags
    {
        TemplateEventDdata = 1,
        TemplateUserData = 2
    }

    internal enum DecodingSource
    {
#pragma warning disable CA1712 // Do not prefix enum values with type name
        DecodingSourceXmlFile,
        DecodingSourceWbem,
        DecodingSourceWPP
#pragma warning restore CA1712 // Do not prefix enum values with type name
    }

    [StructLayout(LayoutKind.Sequential)]
    internal sealed class TraceEventInfo
    {
        internal Guid ProviderGuid;
        internal Guid EventGuid;
        internal EtwEventDescriptor EventDescriptor;
        internal DecodingSource DecodingSource;
        internal uint ProviderNameOffset;
        internal uint LevelNameOffset;
        internal uint ChannelNameOffset;
        internal uint KeywordsNameOffset;
        internal uint TaskNameOffset;
        internal uint OpcodeNameOffset;
        internal uint EventMessageOffset;
        internal uint ProviderMessageOffset;
        internal uint BinaryXmlOffset;
        internal uint BinaryXmlSize;
        internal uint ActivityIDNameOffset;
        internal uint RelatedActivityIDNameOffset;
        internal uint PropertyCount;
        internal uint TopLevelPropertyCount;
        internal TemplateFlags Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct EventTraceHeader
    {
        internal ushort Size;
        internal ushort FieldTypeFlags;
        internal uint Version;
        internal uint ThreadId;
        internal uint ProcessId;
        internal long TimeStamp;
        internal Guid Guid;
        internal uint KernelTime;
        internal uint UserTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct EventTrace
    {
        internal EventTraceHeader Header;
        internal uint InstanceId;
        internal uint ParentInstanceId;
        internal Guid ParentGuid;
        internal IntPtr MofData;
        internal uint MofLength;
        internal uint ClientContext;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct EventTraceLogfile
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        internal string LogFileName;
        [MarshalAs(UnmanagedType.LPWStr)]
        internal string LoggerName;
        internal long CurrentTime;
        internal uint BuffersRead;
        internal uint ProcessTraceMode;
        internal EventTrace CurrentEvent;
        internal TraceLogfileHeader LogfileHeader;
        internal EventTraceBufferCallback BufferCallback;
        internal uint BufferSize;
        internal uint Filled;
        internal uint EventsLost;
        [MarshalAs(UnmanagedType.FunctionPtr)]
        internal EventRecordCallback EventRecordCallback;
        internal uint IsKernelTrace;
        internal IntPtr Context;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct EtwEventDescriptor
    {
        internal ushort Id;
        internal byte Version;
        internal byte Channel;
        internal byte Level;
        internal byte Opcode;
        internal ushort Task;
        internal ulong Keyword;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct EventHeader
    {
        internal ushort Size;
        internal ushort HeaderType;
        internal ushort Flags;
        internal ushort EventProperty;
        internal uint ThreadId;
        internal uint ProcessId;
        internal long TimeStamp;
        internal Guid ProviderId;
        internal EtwEventDescriptor EventDescriptor;
        internal ulong ProcessorTime;
        internal Guid ActivityId;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct EventRecord
    {
        internal EventHeader EventHeader;
        internal EtwBufferContext BufferContext;
        internal ushort ExtendedDataCount;
        internal ushort UserDataLength;
        internal IntPtr ExtendedData;
        internal IntPtr UserData;
        internal IntPtr UserContext;

        [StructLayout(LayoutKind.Sequential)]
        internal struct EtwBufferContext
        {
            internal byte ProcessorNumber;
            internal byte Alignment;
            internal ushort LoggerId;
        }
    }
}
