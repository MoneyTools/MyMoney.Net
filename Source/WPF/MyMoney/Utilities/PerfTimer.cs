//---------------------------------------------------------------------------
// File: PerfTimer.cs
//
// A basic class for inheriting Tests that want to get timing information.
//---------------------------------------------------------------------------

using System;
using System.Runtime.InteropServices;

namespace Walkabout.Utilities
{

    internal class PerfTimer
    {
        long m_Start;
        long m_End;
        long m_Freq;
        long m_Min;
        long m_Max;
        long m_Count;
        long m_Sum;
        long m_Ticks;

        [DllImport("KERNEL32.DLL", EntryPoint = "QueryPerformanceCounter", SetLastError = true,
                    CharSet = CharSet.Unicode, ExactSpelling = true,
                    CallingConvention = CallingConvention.StdCall)]
        public static extern int QueryPerformanceCounter(ref long time);

        [DllImport("KERNEL32.DLL", EntryPoint = "QueryPerformanceFrequency", SetLastError = true,
             CharSet = CharSet.Unicode, ExactSpelling = true,
             CallingConvention = CallingConvention.StdCall)]
        public static extern int QueryPerformanceFrequency(ref long freq);

        public PerfTimer()
        {
            QueryPerformanceFrequency(ref this.m_Freq);
        }

        public void Start()
        {
            this.m_Start = GetTime();
            this.m_End = this.m_Start;
        }

        public void Stop()
        {
            this.m_End = GetTime();
            this.m_Ticks += this.m_End - this.m_Start;
        }

        public long GetDuration()
        { // in milliseconds.            
            return this.GetMilliseconds(this.GetTicks());
        }

        public long GetMilliseconds(long ticks)
        {
            return (ticks * 1000) / this.m_Freq;
        }

        public long GetTicks()
        {
            return this.m_Ticks;
        }

        public static long GetTime()
        { // in nanoseconds.
            long i = 0;
            QueryPerformanceCounter(ref i);
            return i;
        }

        // These methods allow you to count up multiple iterations and
        // then get the median, average and percent variation.
        public void Count(long ms)
        {
            if (this.m_Min == 0) this.m_Min = ms;
            if (ms < this.m_Min) this.m_Min = ms;
            if (ms > this.m_Max) this.m_Max = ms;
            this.m_Sum += ms;
            this.m_Count++;
        }

        public long Min()
        {
            return this.m_Min;
        }

        public long Max()
        {
            return this.m_Max;
        }

        public double Median()
        {
            return TwoDecimals(this.m_Min + ((this.m_Max - this.m_Min) / 2.0));
        }

        public double PercentError()
        {
            double spread = (this.m_Max - this.m_Min) / 2.0;
            double percent = TwoDecimals((double)(spread * 100.0) / this.m_Min);
            return percent;
        }

        static public double TwoDecimals(double i)
        {
            return Math.Round(i * 100) / 100;
        }

        public long Average()
        {
            if (this.m_Count == 0) return 0;
            return this.m_Sum / this.m_Count;
        }

        public void Clear()
        {
            this.m_Start = this.m_End = this.m_Min = this.m_Max = this.m_Sum = this.m_Count = this.m_Ticks = 0;
        }
    }
}