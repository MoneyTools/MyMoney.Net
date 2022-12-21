//-----------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//   (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using Microsoft.VisualStudio.Diagnostics.PerformanceProvider;
using Microsoft.VisualStudio.Diagnostics.PerformanceProvider.Listener;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Microsoft.VisualStudio.PerformanceGraph
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private long count;
        private PerformanceData data = new PerformanceData();
        private bool filter;

        public MainWindow()
        {
            this.data.HistoryLength = TimeSpan.FromSeconds(60); // keep no more than 60 seconds history.
            this.InitializeComponent();
            this.Categories.SelectionChanged += new SelectionChangedEventHandler(this.OnSelectionChanged);
            this.Components.SelectionChanged += new SelectionChangedEventHandler(this.OnSelectionChanged);
            this.Measurements.SelectionChanged += new SelectionChangedEventHandler(this.OnSelectionChanged);

            bool isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            if (!isAdmin)
            {
                System.Windows.MessageBox.Show("This app must be able to elevate to administrator permission, please make sure 'performancegraph.exe.manifest' has been deployed correctly",
                    "Permissions Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            this.Chart.Legend = this.LegendControl;
            this.Bars.Legend = this.LegendControl;
        }

        private void OnLineGraph(object sender, RoutedEventArgs e)
        {
            this.BarGraphCheckBox.IsChecked = false;
            this.Chart.Visibility = System.Windows.Visibility.Visible;
            this.LegendControl.Visibility = System.Windows.Visibility.Collapsed;
            this.Bars.Visibility = System.Windows.Visibility.Collapsed;
            this.changed = true;
            this.UpdateGraph();
        }

        private void OnBarGraph(object sender, RoutedEventArgs e)
        {
            this.LineGraphCheckBox.IsChecked = false;
            this.Bars.Visibility = System.Windows.Visibility.Visible;
            this.LegendControl.Visibility = System.Windows.Visibility.Visible;
            this.Chart.Visibility = System.Windows.Visibility.Collapsed;
            this.changed = true;
            this.UpdateGraph();
        }

        private void OnZoomIn(object sender, RoutedEventArgs e)
        {
            if (this.Bars.Visibility == System.Windows.Visibility.Visible)
            {
                this.Bars.ZoomIn();
            }
            else
            {
                this.Chart.Zoom *= 2;
            }
            this.ZoomToFitButton.IsChecked = false;
        }

        private void OnZoomOut(object sender, RoutedEventArgs e)
        {
            if (this.Bars.Visibility == System.Windows.Visibility.Visible)
            {
                this.Bars.ZoomOut();
            }
            else
            {
                this.Chart.Zoom *= 0.5;
            }
            this.ZoomToFitButton.IsChecked = false;
        }

        private void OnZoomToFit(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = (CheckBox)sender;
            if (this.Bars.Visibility == System.Windows.Visibility.Visible)
            {
                this.Bars.ZoomToFit = checkBox.IsChecked == true;
            }
            else
            {
                this.Chart.ZoomToFit = checkBox.IsChecked == true;
            }
        }

        private DispatcherTimer timer;
        private bool recording = false;
        private bool changed = false;

        private void OnRecord(object sender, RoutedEventArgs e)
        {
            CheckBox box = (CheckBox)sender;
            if (box.IsChecked == true)
            {
                this.StartRecording();
            }
            else
            {
                this.OnStop(sender, e);
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (!this.paused)
            {
                this.UpdateGraph();
            }
            else
            {
                this.Events.Text = this.count.ToString("N0");
            }
        }

        private void OnStop(object sender, RoutedEventArgs e)
        {
            this.Stop();
            this.UpdateGraph();
        }

        private void Stop()
        {
            this.RecordCheckBox.IsChecked = false;

            if (this.watcher != null)
            {
                this.watcher.Enabled = false;
            }

            using (this.watcher)
            {
                this.watcher = null;
            }
            if (this.wpfWatcher != null)
            {
                this.wpfWatcher.Enabled = false;
            }
            using (this.wpfWatcher)
            {
                this.wpfWatcher = null;
            }
            if (this.measurementWatcher != null)
            {
                this.measurementWatcher = null;
            }
            using (this.measurementWatcher)
            {
                this.measurementWatcher = null;
            }
            if (this.timer != null)
            {
                this.timer.Stop();
            }
            this.timer = null;
            this.recording = false;
        }

        bool paused;
        private void OnPause(object sender, RoutedEventArgs e)
        {
            CheckBox box = (CheckBox)sender;
            this.paused = box.IsChecked == true;
            this.UpdateGraph();
        }

        private void OnClear(object sender, RoutedEventArgs e)
        {
            this.Clear();
        }

        private void OnTrend(object sender, RoutedEventArgs e)
        {
            CheckBox box = (CheckBox)sender;
            this.Chart.ShowTrendLine = box.IsChecked == true;
        }

        bool removeSpikes;
        private void OnRemoveSpikes(object sender, RoutedEventArgs e)
        {
            CheckBox box = (CheckBox)sender;
            this.removeSpikes = box.IsChecked == true;
            this.changed = true;
            this.UpdateGraph();
        }

        private void Clear()
        {
            this.count = 0;
            this.changed = true;
            this.data.Clear();
            this.UpdateGraph();
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.changed = true;

            if (this.Components.SelectedItem is string)
            {
                this.ComponentFilter = (string)this.Components.SelectedItem;
            }
            else
            {
                this.ComponentFilter = null;
            }

            if (this.Categories.SelectedItem is string)
            {
                this.CategoryFilter = (string)this.Categories.SelectedItem;
            }
            else
            {
                this.CategoryFilter = null;
            }

            if (this.Measurements.SelectedItem is string)
            {
                this.MeasurementFilter = (string)this.Measurements.SelectedItem;
            }
            else
            {
                this.MeasurementFilter = null;
            }

            if (!this.inUpdate)
            {
                this.UpdateGraph();
            }
        }

        private void FillList<T>(ComboBox list, IEnumerable<T> items)
        {
            T selected = list.SelectedItem is T ? (T)list.SelectedItem : default(T);
            object first = list.Items.Count > 0 ? list.Items[0] : null;
            list.Items.Clear();
            if (first != null)
            {
                list.Items.Add(first);
            }
            foreach (T v in items)
            {
                list.Items.Add(v);
                if (v != null && v.Equals(selected))
                {
                    list.SelectedItem = v;
                }
            }
            if (list.SelectedItem == null)
            {
                list.SelectedIndex = 0;
            }
        }

        private bool inUpdate;

        private string ComponentFilter
        {
            get;
            set;
        }

        private string CategoryFilter
        {
            get;
            set;
        }

        private string MeasurementFilter
        {
            get;
            set;
        }

        private void UpdateGraph()
        {
            this.inUpdate = true;
            this.Events.Text = this.count.ToString("N0");
            if (this.changed)
            {
                this.FillList(this.Components, new List<string>(this.data.Components));
                this.FillList(this.Categories, new List<string>(this.data.Categoryies));
                this.FillList(this.Measurements, new List<string>(this.data.Measurements));

                bool zoomToFit = false;
                if (this.LineGraphCheckBox.IsChecked == true)
                {
                    this.Chart.RemoveSpikes = this.removeSpikes;
                    this.Chart.PerformanceFrequency = this.performanceFrequency;
                    this.Chart.Data = this.data.GetMatchingEvents(this.ComponentFilter, this.CategoryFilter, this.MeasurementFilter);
                    this.Chart.Units = this.data.Units;
                    this.Bars.Data = null;
                    zoomToFit = this.Chart.ZoomToFit;
                    this.UpdateAverage();
                }
                else
                {
                    this.Bars.PerformanceFrequency = this.performanceFrequency;
                    this.Bars.Data = this.data.GetMatchingEvents(this.ComponentFilter, this.CategoryFilter, this.MeasurementFilter);
                    zoomToFit = this.Bars.ZoomToFit;
                    this.Chart.Data = null;
                }
                this.UpdateLayout();
                if (!zoomToFit)
                {
                    double to = this.Scroller.ScrollableWidth;
                    double from = this.Scroller.HorizontalOffset;
                    this.BeginAnimation(HorizontalScrollProperty, new DoubleAnimation(from, to, new Duration(TimeSpan.FromSeconds(0.5)), FillBehavior.HoldEnd));
                }
                this.ZoomToFitButton.IsChecked = zoomToFit;
            }
            this.changed = false;
            this.inUpdate = false;
        }

        private void UpdateAverage()
        {
            ulong sum = 0;
            ulong count = 0;
            foreach (PerformanceEventArrivedEventArgs e in this.Chart.Data)
            {
                if (e.EventId == PerformanceData.EndEvent)
                {
                    sum += e.Ticks;
                    count++;
                }
            }

            if (count > 0)
            {
                double avg = sum / (double)count;
                avg /= this.Chart.UnitConversion;
                this.AverageStatus.Text = "Average: " + avg.ToString("G2") + this.Chart.Units;
            }
        }

        public double HorizontalScroll
        {
            get { return (double)this.GetValue(HorizontalScrollProperty); }
            set { this.SetValue(HorizontalScrollProperty, value); }
        }

        // Using a DependencyProperty as the backing store for HorizontalScroll.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty HorizontalScrollProperty =
            DependencyProperty.Register("HorizontalScroll", typeof(double), typeof(MainWindow), new UIPropertyMetadata((double)0, OnHorizontalScrollChanged));

        static void OnHorizontalScrollChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            MainWindow window = (MainWindow)sender;
            double v = (double)e.NewValue;
            window.Scroller.ScrollToHorizontalOffset(v);
        }

        private long performanceFrequency;
        private EventTraceSession session = new EventTraceSession("Visual Studio Performance");
        private PerformanceEventTraceWatcher watcher = null;
        private WpfEventTraceWatcher wpfWatcher = null;
        private MeasurementBlockEventTraceWatcher measurementWatcher = null;

        private void StartRecording()
        {
            try
            {
                this.RecordCheckBox.IsChecked = true;
                this.Clear();
                this.recording = true;
                if (this.watcher == null)
                {
                    this.CreateWatchers();
                }

                // Start listening 
                this.watcher.Enabled = true;
                this.data.PerformanceFrequency = this.performanceFrequency = this.watcher.PerformanceFrequency;
                if (this.WpfEvents.IsChecked)
                {
                    this.wpfWatcher.Enabled = true;
                }
                this.measurementWatcher.Enabled = true;
            }
            catch (Win32Exception we)
            {
                if (we.NativeErrorCode == 5)
                {
                    // Access denied
                    bool isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
                    System.Windows.MessageBox.Show("This app must be able to elevate to administrator permission, please make sure 'performancegraph.exe.manifest' has been deployed correctly",
                            "Permissions Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {

                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void CreateWatchers()
        {
            Guid providerId = typeof(PerformanceBlock).GUID;
            this.watcher = new PerformanceEventTraceWatcher(this.session);

            this.watcher.EventArrived += delegate (object sender, EventArrivedEventArgs e)
            {
                if (e.EventException != null)
                {
                    // Handle the exception 
                    Trace.WriteLine(e.EventException);
                    return;
                }
                PerformanceEventArrivedEventArgs pe = e as PerformanceEventArrivedEventArgs;
                if (this.recording && pe != null)
                {
                    if (!this.filter || ((this.ComponentFilter == null || pe.ComponentName == this.ComponentFilter) &&
                                    (this.CategoryFilter == null || pe.CategoryName == this.CategoryFilter) &&
                                    (this.MeasurementFilter == null || pe.MeasurementName == this.MeasurementFilter)))
                    {
                        this.data.Add(pe);
                        this.count++;
                        this.changed = true;
                    }
                }
            };

            this.wpfWatcher = new WpfEventTraceWatcher(this.session);

            this.wpfWatcher.EventArrived += delegate (object sender, EventArrivedEventArgs e)
            {
                if (e.EventException != null)
                {
                    // Handle the exception 
                    Trace.WriteLine(e.EventException);
                    return;
                }
                WpfEventArrivedEventArgs we = e as WpfEventArrivedEventArgs;

                // find the WPF render events
                if (this.recording && we != null && we.Event != null)
                {
                    WpfEvent wev = we.Event;
                    if (!this.filter || ((this.ComponentFilter == null || ComponentId.WPF.ToString() == this.ComponentFilter) &&
                                    (this.CategoryFilter == null || CategoryId.View.ToString() == this.CategoryFilter) &&
                                    (this.MeasurementFilter == null || wev.Task == this.MeasurementFilter)))
                    {
                        this.data.Add(new PerformanceEventArrivedEventArgs()
                        {
                            EventId = we.EventId,
                            EventName = we.EventName,
                            ProviderId = we.ProviderId,
                            Timestamp = we.Timestamp,
                            Category = CategoryId.View,
                            Component = ComponentId.WPF,
                            MeasurementName = wev.Task
                        });
                        this.count++;
                        this.changed = true;
                    }
                }
            };

            this.measurementWatcher = new MeasurementBlockEventTraceWatcher(this.session);

            this.measurementWatcher.EventArrived += delegate (object sender, EventArrivedEventArgs e)
            {
                if (e.EventException != null)
                {
                    // Handle the exception 
                    Trace.WriteLine(e.EventException);
                    return;
                }
                MeasurementBlockEventArgs me = e as MeasurementBlockEventArgs;

                if (this.recording && me != null)
                {
                    if (!this.filter || (this.CategoryFilter == null || me.Category == this.CategoryFilter))
                    {
                        string cat = me.Category;
                        string comp = null;
                        int i = cat.IndexOf(':');
                        if (i > 0)
                        {
                            comp = cat.Substring(0, i);
                            cat = cat.Substring(i + 1).Trim();
                        }

                        this.data.Add(new PerformanceEventArrivedEventArgs()
                        {
                            EventId = me.EventId, // fortunately the event id's are the same.
                            EventName = me.EventName,
                            ProviderId = me.ProviderId,
                            Timestamp = me.Timestamp,
                            CategoryName = cat,
                            ComponentName = comp,
                            Size = me.Size,
                            Ticks = me.Ticks
                        });
                        this.count++;
                        this.changed = true;
                    }
                }
            };

            // start timer so we can update graph when events arrive.
            if (this.timer == null)
            {
                this.timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background,
                    new EventHandler(this.OnTick), this.Dispatcher);
                this.timer.Start();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            this.Stop();
            base.OnClosed(e);

        }

        void OnFilterChanged(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            this.filter = cb.IsChecked == true;
        }

        private void OnFileOpen(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            OpenFileDialog fd = new OpenFileDialog();
            fd.Filter = "ETL Files (*.etl)|*.etl";
            fd.CheckFileExists = true;
            if (fd.ShowDialog() == true)
            {
                this.Stop();
                this.PauseCheckBox.IsChecked = false;
                this.paused = true;
                this.recording = true;
                this.session = new EventTraceSession("Visual Studio Performance");
                this.session.TraceComplete += new EventHandler(this.OnTraceComplete);
                this.CreateWatchers();
                this.session.OpenTraceLog(fd.FileName);
                this.performanceFrequency = this.session.PerformanceFrequency;
            }
        }

        void OnTraceComplete(object sender, EventArgs e)
        {
            this.paused = false;
        }

        private void MenuAddWpf_Click(object sender, RoutedEventArgs e)
        {
            MenuItem item = (MenuItem)sender;
            item.IsChecked = !item.IsChecked;

            if (this.wpfWatcher != null)
            {
                this.wpfWatcher.Enabled = item.IsChecked;
            }
        }


        private void OnFileSave(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            SaveFileDialog sd = new SaveFileDialog();
            sd.Filter = "XML Files (*.xml)|*.xml";
            if (sd.ShowDialog() == true)
            {
                var events = this.data.GetMatchingEvents(this.ComponentFilter, this.CategoryFilter, this.MeasurementFilter);
                this.data.WriteXml(sd.FileName, events);
            }
        }


    }
}
