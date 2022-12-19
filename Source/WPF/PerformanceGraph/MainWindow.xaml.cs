//-----------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//   (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Security;
using System.Security.Principal;
using Microsoft.Win32;
using Microsoft.VisualStudio.Diagnostics.PerformanceProvider;
using Microsoft.VisualStudio.Diagnostics.PerformanceProvider.Listener;

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
            data.HistoryLength = TimeSpan.FromSeconds(60); // keep no more than 60 seconds history.
            InitializeComponent();
            Categories.SelectionChanged += new SelectionChangedEventHandler(OnSelectionChanged);
            Components.SelectionChanged += new SelectionChangedEventHandler(OnSelectionChanged);
            Measurements.SelectionChanged += new SelectionChangedEventHandler(OnSelectionChanged);

            bool isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            if (!isAdmin)
            {
                System.Windows.MessageBox.Show("This app must be able to elevate to administrator permission, please make sure 'performancegraph.exe.manifest' has been deployed correctly",
                    "Permissions Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            Chart.Legend = LegendControl;
            Bars.Legend = LegendControl;
        }

        private void OnLineGraph(object sender, RoutedEventArgs e)
        {
            BarGraphCheckBox.IsChecked = false;
            Chart.Visibility = System.Windows.Visibility.Visible;
            LegendControl.Visibility = System.Windows.Visibility.Collapsed;
            Bars.Visibility = System.Windows.Visibility.Collapsed;
            changed = true;
            UpdateGraph();
        }

        private void OnBarGraph(object sender, RoutedEventArgs e)
        {
            LineGraphCheckBox.IsChecked = false;
            Bars.Visibility = System.Windows.Visibility.Visible;
            LegendControl.Visibility = System.Windows.Visibility.Visible;
            Chart.Visibility = System.Windows.Visibility.Collapsed;
            changed = true;
            UpdateGraph();
        }

        private void OnZoomIn(object sender, RoutedEventArgs e)
        {
            if (Bars.Visibility == System.Windows.Visibility.Visible)
            {
                Bars.ZoomIn();
            }
            else
            {
                Chart.Zoom *= 2;
            }
            ZoomToFitButton.IsChecked = false;
        }

        private void OnZoomOut(object sender, RoutedEventArgs e)
        {
            if (Bars.Visibility == System.Windows.Visibility.Visible)
            {
                Bars.ZoomOut();
            }
            else
            {
                Chart.Zoom *= 0.5;
            }
            ZoomToFitButton.IsChecked = false;
        }

        private void OnZoomToFit(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = (CheckBox)sender;
            if (Bars.Visibility == System.Windows.Visibility.Visible)
            {
                Bars.ZoomToFit = checkBox.IsChecked == true;
            }
            else
            {
                Chart.ZoomToFit = checkBox.IsChecked == true;
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
                StartRecording();
            }
            else
            {
                OnStop(sender, e);
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (!paused)
            {
                UpdateGraph();
            }
            else
            {
                Events.Text = count.ToString("N0");
            }
        }

        private void OnStop(object sender, RoutedEventArgs e)
        {
            Stop();
            UpdateGraph();
        }

        private void Stop()
        {
            RecordCheckBox.IsChecked = false;

            if (watcher != null)
            {
                watcher.Enabled = false;
            }

            using (watcher)
            {
                watcher = null;
            }
            if (wpfWatcher != null)
            {
                wpfWatcher.Enabled = false;
            }
            using (wpfWatcher)
            {
                wpfWatcher = null;
            }
            if (measurementWatcher != null)
            {
                measurementWatcher = null;
            }
            using (measurementWatcher)
            {
                measurementWatcher = null;
            }
            if (timer != null)
            {
                timer.Stop();
            }
            timer = null;
            recording = false;
        }

        bool paused;
        private void OnPause(object sender, RoutedEventArgs e)
        {
            CheckBox box = (CheckBox)sender;
            paused = box.IsChecked == true;
            UpdateGraph();
        }

        private void OnClear(object sender, RoutedEventArgs e)
        {
            Clear();
        }

        private void OnTrend(object sender, RoutedEventArgs e)
        {
            CheckBox box = (CheckBox)sender;
            Chart.ShowTrendLine = box.IsChecked == true;
        }

        bool removeSpikes;
        private void OnRemoveSpikes(object sender, RoutedEventArgs e)
        {
            CheckBox box = (CheckBox)sender;
            removeSpikes = box.IsChecked == true;
            changed = true;
            UpdateGraph();
        }

        private void Clear()
        {
            count = 0;
            changed = true;
            data.Clear();
            UpdateGraph();
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            changed = true;

            if (this.Components.SelectedItem is string)
            {
                ComponentFilter = (string)this.Components.SelectedItem;
            }
            else
            {
                ComponentFilter = null;
            }

            if (this.Categories.SelectedItem is string)
            {
                CategoryFilter = (string)this.Categories.SelectedItem;
            }
            else
            {
                CategoryFilter = null;
            }

            if (this.Measurements.SelectedItem is string)
            {
                MeasurementFilter = (string)this.Measurements.SelectedItem;
            }
            else
            {
                MeasurementFilter = null;
            }

            if (!inUpdate)
            {
                UpdateGraph();
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
            inUpdate = true;
            Events.Text = count.ToString("N0");
            if (changed)
            {
                FillList<string>(this.Components, new List<string>(data.Components));
                FillList<string>(this.Categories, new List<string>(data.Categoryies));
                FillList<string>(this.Measurements, new List<string>(data.Measurements));

                bool zoomToFit = false;
                if (LineGraphCheckBox.IsChecked == true)
                {
                    Chart.RemoveSpikes = removeSpikes;
                    Chart.PerformanceFrequency = this.performanceFrequency;
                    Chart.Data = data.GetMatchingEvents(ComponentFilter, CategoryFilter, MeasurementFilter);
                    Chart.Units = data.Units;
                    Bars.Data = null;
                    zoomToFit = Chart.ZoomToFit;
                    UpdateAverage();
                }
                else
                {
                    Bars.PerformanceFrequency = this.performanceFrequency;
                    Bars.Data = data.GetMatchingEvents(ComponentFilter, CategoryFilter, MeasurementFilter);
                    zoomToFit = Bars.ZoomToFit;
                    Chart.Data = null;
                }
                UpdateLayout();
                if (!zoomToFit)
                {
                    double to = Scroller.ScrollableWidth;
                    double from = Scroller.HorizontalOffset;
                    this.BeginAnimation(HorizontalScrollProperty, new DoubleAnimation(from, to, new Duration(TimeSpan.FromSeconds(0.5)), FillBehavior.HoldEnd));
                }
                ZoomToFitButton.IsChecked = zoomToFit;
            }
            changed = false;
            inUpdate = false;
        }

        private void UpdateAverage()
        {
            ulong sum = 0;
            ulong count = 0;
            foreach (PerformanceEventArrivedEventArgs e in Chart.Data)
            {
                if (e.EventId == PerformanceData.EndEvent)
                {
                    sum += e.Ticks;
                    count++;
                }
            }

            if (count > 0)
            {
                double avg = (double)sum / (double)count;
                avg /= Chart.UnitConversion;
                AverageStatus.Text = "Average: " + avg.ToString("G2") + Chart.Units;
            }
        }

        public double HorizontalScroll
        {
            get { return (double)GetValue(HorizontalScrollProperty); }
            set { SetValue(HorizontalScrollProperty, value); }
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
                RecordCheckBox.IsChecked = true;
                Clear();
                recording = true;
                if (watcher == null)
                {
                    CreateWatchers();
                }

                // Start listening 
                watcher.Enabled = true;
                data.PerformanceFrequency = performanceFrequency = watcher.PerformanceFrequency;
                if (WpfEvents.IsChecked)
                {
                    wpfWatcher.Enabled = true;
                }
                measurementWatcher.Enabled = true;
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
            watcher = new PerformanceEventTraceWatcher(session);

            watcher.EventArrived += delegate (object sender, EventArrivedEventArgs e)
            {
                if (e.EventException != null)
                {
                    // Handle the exception 
                    Trace.WriteLine(e.EventException);
                    return;
                }
                PerformanceEventArrivedEventArgs pe = e as PerformanceEventArrivedEventArgs;
                if (recording && pe != null)
                {
                    if (!filter || ((ComponentFilter == null || pe.ComponentName == ComponentFilter) &&
                                    (CategoryFilter == null || pe.CategoryName == CategoryFilter) &&
                                    (MeasurementFilter == null || pe.MeasurementName == MeasurementFilter)))
                    {
                        data.Add(pe);
                        count++;
                        changed = true;
                    }
                }
            };

            wpfWatcher = new WpfEventTraceWatcher(session);

            wpfWatcher.EventArrived += delegate (object sender, EventArrivedEventArgs e)
            {
                if (e.EventException != null)
                {
                    // Handle the exception 
                    Trace.WriteLine(e.EventException);
                    return;
                }
                WpfEventArrivedEventArgs we = e as WpfEventArrivedEventArgs;

                // find the WPF render events
                if (recording && we != null && we.Event != null)
                {
                    WpfEvent wev = we.Event;
                    if (!filter || ((ComponentFilter == null || ComponentId.WPF.ToString() == ComponentFilter) &&
                                    (CategoryFilter == null || CategoryId.View.ToString() == CategoryFilter) &&
                                    (MeasurementFilter == null || wev.Task == MeasurementFilter)))
                    {
                        data.Add(new PerformanceEventArrivedEventArgs()
                        {
                            EventId = we.EventId,
                            EventName = we.EventName,
                            ProviderId = we.ProviderId,
                            Timestamp = we.Timestamp,
                            Category = CategoryId.View,
                            Component = ComponentId.WPF,
                            MeasurementName = wev.Task
                        });
                        count++;
                        changed = true;
                    }
                }
            };

            measurementWatcher = new MeasurementBlockEventTraceWatcher(session);

            measurementWatcher.EventArrived += delegate (object sender, EventArrivedEventArgs e)
            {
                if (e.EventException != null)
                {
                    // Handle the exception 
                    Trace.WriteLine(e.EventException);
                    return;
                }
                MeasurementBlockEventArgs me = e as MeasurementBlockEventArgs;

                if (recording && me != null)
                {
                    if (!filter || (CategoryFilter == null || me.Category == CategoryFilter))
                    {
                        string cat = me.Category;
                        string comp = null;
                        int i = cat.IndexOf(':');
                        if (i > 0)
                        {
                            comp = cat.Substring(0, i);
                            cat = cat.Substring(i + 1).Trim();
                        }

                        data.Add(new PerformanceEventArrivedEventArgs()
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
                        count++;
                        changed = true;
                    }
                }
            };

            // start timer so we can update graph when events arrive.
            if (timer == null)
            {
                timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background,
                    new EventHandler(OnTick), this.Dispatcher);
                timer.Start();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            Stop();
            base.OnClosed(e);

        }

        void OnFilterChanged(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            filter = cb.IsChecked == true;
        }

        private void OnFileOpen(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            OpenFileDialog fd = new OpenFileDialog();
            fd.Filter = "ETL Files (*.etl)|*.etl";
            fd.CheckFileExists = true;
            if (fd.ShowDialog() == true)
            {
                Stop();
                PauseCheckBox.IsChecked = false;
                paused = true;
                recording = true;
                this.session = new EventTraceSession("Visual Studio Performance");
                session.TraceComplete += new EventHandler(OnTraceComplete);
                CreateWatchers();
                session.OpenTraceLog(fd.FileName);
                this.performanceFrequency = session.PerformanceFrequency;
            }
        }

        void OnTraceComplete(object sender, EventArgs e)
        {
            paused = false;
        }

        private void MenuAddWpf_Click(object sender, RoutedEventArgs e)
        {
            MenuItem item = (MenuItem)sender;
            item.IsChecked = !item.IsChecked;

            if (wpfWatcher != null)
            {
                wpfWatcher.Enabled = item.IsChecked;
            }
        }


        private void OnFileSave(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            SaveFileDialog sd = new SaveFileDialog();
            sd.Filter = "XML Files (*.xml)|*.xml";
            if (sd.ShowDialog() == true)
            {
                var events = data.GetMatchingEvents(ComponentFilter, CategoryFilter, MeasurementFilter);
                data.WriteXml(sd.FileName, events);
            }
        }


    }
}
