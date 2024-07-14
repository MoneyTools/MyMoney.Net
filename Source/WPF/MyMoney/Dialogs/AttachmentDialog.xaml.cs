using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Walkabout.Attachments;
using Walkabout.Configuration;
using Walkabout.Controls;
using Walkabout.Data;
using Walkabout.Utilities;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for ScanDialog.xaml
    /// </summary>
    [CLSCompliantAttribute(false)]
    public partial class AttachmentDialog : BaseDialog
    {
        private Resizer resizer;
        private bool closed;
        private bool loaded;
        private bool dirty;
        private AttachmentDialogItem selected;
        private string storage;
        private readonly Brush resizerBrush;
        private const double ResizerThumbSize = 12;
        private Transaction transaction;
        private readonly DelayedActions actions = new DelayedActions();

        public static readonly RoutedUICommand CommandRotateRight = new RoutedUICommand("Rotate Right", "CommandRotateRight", typeof(MainWindow));
        public static readonly RoutedUICommand CommandRotateLeft = new RoutedUICommand("Rotate Left", "CommandRotateLeft", typeof(MainWindow));
        public static readonly RoutedUICommand CommandCropImage = new RoutedUICommand("Crop", "CommandCropImage", typeof(MainWindow));

        public AttachmentDialog()
        {
            this.InitializeComponent();

            Loaded += new RoutedEventHandler(this.OnLoaded);

            string path = Settings.TheSettings.AttachmentDirectory;
            if (string.IsNullOrEmpty(path) || !System.IO.Directory.Exists(path))
            {
                throw new Exception("Please ensure the AttachmentDirectory property is set to existing location");
            }
            this.Directory = path;

            this.CanvasGrid.PreviewMouseDown += new MouseButtonEventHandler(this.CanvasGrid_MouseDown);

            this.resizerBrush = (Brush)this.Resources["ResizerThumbBrush"];
        }

        protected override void OnDragEnter(DragEventArgs e)
        {
            if (this.transaction == null)
            {
                return;
            }
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }

        protected override void OnDrop(DragEventArgs e)
        {
            if (this.transaction == null)
            {
                return;
            }
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (var file in files)
                {
                    var extension = Path.GetExtension(file);
                    StringBuilder sb = new StringBuilder();
                    try
                    {
                        string attachmentFullPath = this.Manager.GetUniqueFileName(transaction, extension);
                        File.Copy(files[0], attachmentFullPath, true);
                        transaction.HasAttachment = true;
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine(ex.Message);
                    }
                    if (sb.Length > 0)
                    {
                        MessageBoxEx.Show(sb.ToString(), "Add Attachments Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                this.LoadAttachments();
            }
        }

        private void OnDoubleClickItem(AttachmentDialogItem item)
        {
            if (!string.IsNullOrEmpty(item.FileName))
            {
                InternetExplorer.OpenUrl(0, item.FileName);
            }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.Close();
            }
            base.OnPreviewKeyDown(e);
        }

        private void CanvasGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Point pos = e.GetPosition(this.CanvasGrid);
            HitTestResult result = VisualTreeHelper.HitTest(this.CanvasGrid, pos);

            DependencyObject hit = result.VisualHit;
            if (hit != null)
            {
                AttachmentDialogItem item = WpfHelper.FindAncestor<AttachmentDialogItem>(hit);
                if (item != null)
                {
                    this.SelectItem(item);
                    if (e.ClickCount > 1)
                    {
                        this.OnDoubleClickItem(item);
                    }
                    return;
                }
                if (hit == this.resizer)
                {
                    return;
                }
            }

            this.RemoveResizer();
            this.selected = null;
        }

        public AttachmentManager Manager { get; set; }

        public Settings Settings { get; set; }

        public bool IsClosed { get { return this.closed; } }

        public Transaction Transaction
        {
            get => this.transaction;
            set
            {
                this.transaction = value;
                this.actions.StartDelayedAction("Sync", this.LoadAttachments, TimeSpan.FromMilliseconds(30));
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            this.loaded = true;
            this.LoadAttachments();
        }

        private void LoadAttachments()
        {
            if (this.loaded)
            {
                this.selected = null;
                this.RemoveResizer();
                this.Canvas.Children.Clear();

                if (this.Transaction == null)
                {
                    return;
                }

                foreach (string filePath in this.Manager.GetAttachments(this.Transaction))
                {
                    try
                    {
                        string ext = System.IO.Path.GetExtension(filePath);
                        if (string.Compare(ext, ".jpg", StringComparison.OrdinalIgnoreCase) == 0 ||
                            string.Compare(ext, ".png", StringComparison.OrdinalIgnoreCase) == 0 ||
                            string.Compare(ext, ".gif", StringComparison.OrdinalIgnoreCase) == 0 ||
                            string.Compare(ext, ".bmp", StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            this.AddItem(new AttachmentDialogImageItem(filePath));
                        }
                        else if (string.Compare(ext, ".xamlpackage", StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            // load an xamlpackage document.
                            this.AddItem(new AttachmentDialogDocumentItem(filePath));
                        }
                        else
                        {
                            this.AddItem(new AttachmentDialogFileItem(filePath));
                        }
                    }
                    catch
                    {
                        // todo?
                    }
                }
                this.LayoutContent();
            }
        }

        private int ItemCount
        {
            get
            {
                int i = 0;
                foreach (UIElement e in this.Canvas.Children)
                {
                    AttachmentDialogItem item = e as AttachmentDialogItem;
                    if (item != null)
                    {
                        i++;
                    }
                }
                return i;
            }
        }

        private void LayoutContent()
        {
            // Let the wrap panel do it's thing.
            this.Canvas.UpdateLayout();

            if (this.resizer != null)
            {
                this.MoveResizer(this.selected, this.GetUnscaledBounds(this.resizer.Bounds));
            }
        }

        private void SelectItem(AttachmentDialogItem item)
        {
            this.selected = item;
            this.AddResizer(item, this.GetItemContentBounds(item));
        }

        private void ClearSelection()
        {
            this.selected = null;
            this.RemoveResizer();
        }

        public string Directory
        {
            get { return this.storage; }
            set
            {
                this.storage = value;
                this.actions.StartDelayedAction("Sync", this.LoadAttachments, TimeSpan.FromMilliseconds(30));
            }
        }

        //private void GetScannerAsync(Action completionHandler)
        //{
        //    if (device == null)
        //    {
        //        Task task = new Task(new Action(() =>
        //        {
        //            if (globalDialog == null)
        //            {
        //                globalDialog = new WIA.CommonDialog();
        //            }
        //            try
        //            {
        //                device = globalDialog.ShowSelectDevice(DeviceType: WIA.WiaDeviceType.ScannerDeviceType, AlwaysSelectDevice: true);
        //            }
        //            catch (Exception ex)
        //            {
        //                Debug.WriteLine(ex.Message);
        //            }
        //        }));

        //        task.Start();
        //        Task.WaitAll(task);

        //    }
        //    completionHandler();
        //}

        ////private static WIA.Device device;
        ////private static WIA.ICommonDialog globalDialog;

        //private void Scan(object sender, RoutedEventArgs e)
        //{
        //    try
        //    {
        //        AppBarButton button = sender as AppBarButton;
        //        // hmmm, reusing the device doesn't work because it somehow resets itself to capture higher resolution images.
        //        bool hasDevice = false; //  device != null;

        //        this.GetScannerAsync(new Action(() =>
        //        {
        //            WIA.ImageFile imageFile = null;

        //            if (!hasDevice)
        //            {
        //                imageFile = globalDialog.ShowAcquireImage(DeviceType: WIA.WiaDeviceType.ScannerDeviceType,
        //                        Bias: WIA.WiaImageBias.MinimizeSize, Intent: WIA.WiaImageIntent.TextIntent, AlwaysSelectDevice: false);
        //            }
        //            else
        //            {
        //                WIA.Item scannerItem = device.Items[1];
        //                const string wiaFormatPNG = "{B96B3CAF-0728-11D3-9D7B-0000F81EF32E}";
        //                object scanResult = globalDialog.ShowTransfer(scannerItem, wiaFormatPNG, false);
        //                imageFile = (WIA.ImageFile)scanResult;
        //            }

        //            if (imageFile != null)
        //            {
        //                string temp = System.IO.Path.GetTempFileName();
        //                if (File.Exists(temp))
        //                {
        //                    File.Delete(temp);
        //                }
        //                imageFile.SaveFile(temp);
        //                TempFilesManager.AddTempFile(temp);

        //                AttachmentDialogImageItem image = new AttachmentDialogImageItem(temp);
        //                this.AddItem(image);
        //                this.LayoutContent();
        //                this.SelectItem(image);
        //                this.AutoCrop(image);
        //            }
        //        }));

        //    }
        //    catch (Exception ex)
        //    {
        //        device = null;
        //        string message = this.GetWiaErrorMessage(ex);
        //        MessageBoxEx.Show(this, message, "Scan Error", MessageBoxButton.OK, MessageBoxImage.Error);
        //    }
        //}

        private void SetDirty()
        {
            this.dirty = true;
            CommandManager.InvalidateRequerySuggested();
        }

        private string GetWiaErrorMessage(Exception ex)
        {
            COMException ce = ex as COMException;
            if (ce != null)
            {
                int hresult = ce.ErrorCode;
                int wiaRange = (1 << 31) | (33 << 16);

                if ((int)(hresult & 0xffff0000) == wiaRange)
                {
                    WiaErrorCode code = (WiaErrorCode)((hresult) & 0xffff);
                    var mgr = Walkabout.Properties.Resources.ResourceManager;
                    string msg = mgr.GetString(code.ToString());
                    if (!string.IsNullOrEmpty(msg))
                    {
                        return msg;
                    }
                }
            }
            return "Unexpected error with scanner: " + ex.Message;
        }

        private void AddItem(AttachmentDialogItem item)
        {
            item.Margin = new Thickness(10);
            item.ContentChanged -= this.OnContentChanged;
            item.ContentChanged += this.OnContentChanged;
            this.Canvas.Children.Add(item);
        }

        private void OnContentChanged(object sender, EventArgs e)
        {
            var item = (AttachmentDialogItem)sender;
            this.OnItemChanged(item);
        }

        private void OnItemChanged(AttachmentDialogItem item)
        {
            if (item != null)
            {
                this.selected.InvalidateArrange();
                this.LayoutContent();
                this.SelectItem(this.selected); // fix up the resizer.
                this.SetDirty();
            }
        }

        private void OnItemRemoved(AttachmentDialogItem item)
        {
            this.Canvas.Children.Remove(item);
            if (item == this.selected)
            {
                this.RemoveResizer();
                this.selected = null;
            }
        }

        /// <summary>
        /// Add or more the resizer so it is aligned with the given image.
        /// </summary>
        private void AddResizer(AttachmentDialogItem item, Rect cropBounds)
        {
            if (this.resizer == null)
            {
                this.resizer = new Resizer();
                this.resizer.BorderBrush = this.resizer.ThumbBrush = this.resizerBrush;
                this.resizer.ThumbSize = ResizerThumbSize; this.resizer.Resized += this.OnResized;
                this.resizer.Resizing += this.OnResizing;
                this.Adorners.Children.Add(this.resizer);
            }

            this.MoveResizer(item, cropBounds);
        }

        private void MoveResizer(AttachmentDialogItem item, Rect resizerBounds)
        {
            // Transform resizer so it is anchored around the selected image.
            if (this.resizer != null && item != null)
            {
                this.resizer.LimitBounds = this.GetScaledBounds(item.ResizeLimit);
                this.resizer.Bounds = this.GetScaledBounds(resizerBounds);
                this.resizer.IsEnabled = item.LiveResizable;
                this.resizer.InvalidateArrange();
            }
        }

        private void OnResized(object sender, EventArgs e)
        {
            this.SetDirty();
        }

        private void OnResizing(object sender, EventArgs e)
        {
            if (this.selected != null && this.selected.LiveResizable)
            {
                this.selected.Resize(this.resizer.Bounds);
            }
        }

        private void RemoveResizer()
        {
            if (this.resizer != null)
            {
                this.Adorners.Children.Remove(this.resizer);
                this.resizer = null;
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);

            this.actions.CancelAll();
            if (this.dirty)
            {
                this.Save(this, null);
            }

            this.Manager.FindAttachments(this.Transaction);

            this.closed = true;
            TempFilesManager.Cleanup();
        }

        private void ZoomIn(object sender, RoutedEventArgs e)
        {
            ScaleTransform scale = this.Canvas.LayoutTransform as ScaleTransform;
            if (scale == null)
            {
                this.Canvas.LayoutTransform = scale = new ScaleTransform(1, 1);
            }
            if (this.resizer != null)
            {
                Rect bounds = this.GetUnscaledBounds(this.resizer.Bounds);
                scale.ScaleX = scale.ScaleY *= 1.1;
                this.CanvasGrid.UpdateLayout();
                this.MoveResizer(this.selected, bounds);
            }
        }

        private void ZoomOut(object sender, RoutedEventArgs e)
        {
            ScaleTransform scale = this.Canvas.LayoutTransform as ScaleTransform;
            if (scale == null)
            {
                this.Canvas.LayoutTransform = scale = new ScaleTransform(1, 1);
            }
            if (this.resizer != null)
            {
                Rect bounds = this.GetUnscaledBounds(this.resizer.Bounds);
                scale.ScaleX = scale.ScaleY /= 1.1;
                this.CanvasGrid.UpdateLayout();
                this.MoveResizer(this.selected, bounds);
            }
        }

        private void Browse(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog fd = new System.Windows.Forms.FolderBrowserDialog();
            fd.SelectedPath = this.Directory;
            var rc = fd.ShowDialog();
            if (rc == System.Windows.Forms.DialogResult.OK)
            {
                this.Directory = fd.SelectedPath;
            }
        }

        private void Save(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                int index = 0;
                foreach (UIElement element in this.Canvas.Children)
                {
                    AttachmentDialogItem item = element as AttachmentDialogItem;
                    if (item != null)
                    {
                        string fileName = this.Manager.GetUniqueFileName(this.Transaction, item.FileExtension);
                        string existing = item.FileName;
                        if (!string.IsNullOrEmpty(existing) && string.Compare(fileName, existing, StringComparison.OrdinalIgnoreCase) != 0)
                        {
                            // file name is different, so delete the old file.
                            TempFilesManager.DeleteFile(existing);
                        }

                        AttachmentDialogImageItem img = item as AttachmentDialogImageItem;
                        if (img != null)
                        {
                            if (item == this.selected && this.resizer != null)
                            {
                                // crop it.
                                Rect bounds = this.GetUnscaledBounds(this.resizer.Bounds);
                                img.Resize(bounds);
                            }
                        }

                        // in case an item was deleted in the middle, we need to re-index the items.
                        item.Save(fileName);
                        item.FileName = fileName;
                        index++;
                    }
                }

                this.ClearSelection();
                this.LoadAttachments();
                this.dirty = false;
            }
            catch (Exception ex)
            {
                MessageBoxEx.Show("Unexpected error saving new image: " + ex.Message, "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Get the bounds of the content of this item in unscaled coordinates, relative
        /// to the top left of the item.
        /// </summary>
        /// <param name="item">The item whose content bounds we want</param>
        /// <returns>The bounds</returns>
        private Rect GetItemContentBounds(AttachmentDialogItem item)
        {
            Size size = item.Content.DesiredSize;
            return new Rect(0, 0, size.Width, size.Height);
        }

        private Rect GetScaledBounds(Rect unscaledBounds)
        {
            Rect bounds = unscaledBounds;
            return this.selected.Content.TransformToAncestor(this.CanvasGrid).TransformBounds(unscaledBounds);
        }

        private Rect GetUnscaledBounds(Rect scaledBounds)
        {
            Rect bounds = this.CanvasGrid.TransformToDescendant(this.selected.Content).TransformBounds(scaledBounds);
            if (bounds.Left < 0)
            {
                bounds.Width += bounds.Left;
                bounds.X = 0;
            }
            if (bounds.Top < 0)
            {
                bounds.Height += bounds.Top;
                bounds.Y = 0;
            }

            return bounds;
        }

        private void Delete(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                AttachmentDialogItem item = this.selected;
                if (item != null)
                {
                    string filePath = item.FileName;
                    TempFilesManager.DeleteFile(filePath);
                    this.OnItemRemoved(item);
                    this.SetDirty();
                }
            }
            catch (Exception ex)
            {
                MessageBoxEx.Show("Error deleting file: " + ex.Message, "Delete Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cut(object sender, ExecutedRoutedEventArgs e)
        {
            this.Copy(sender, e);
            this.Delete(sender, e);
        }

        private void Copy(object sender, ExecutedRoutedEventArgs e)
        {
            if (this.selected != null)
            {
                this.selected.Copy();
            }
        }

        private void Paste(object sender, ExecutedRoutedEventArgs e)
        {
            AttachmentDialogItem newItem = null;

            if (Clipboard.ContainsImage())
            {
                var image = Clipboard.GetImage();
                if (image != null)
                {
                    // for some reason this bitmap doesn't paint unless I save and reload it
                    // the in-memory copy from the clipboard is a bit touchy, probably comes from
                    // another process and so on, so persistence is better strategy here...
                    string path = Path.GetTempFileName();

                    AttachmentDialogImageItem item = new AttachmentDialogImageItem(image);
                    item.Save(path);

                    TempFilesManager.AddTempFile(path);

                    newItem = new AttachmentDialogImageItem(path);
                }
            }
            else if (Clipboard.ContainsData(DataFormats.XamlPackage) ||
                Clipboard.ContainsData(DataFormats.Rtf) ||
                Clipboard.ContainsData(DataFormats.Text))
            {
                newItem = new AttachmentDialogDocumentItem(Clipboard.GetDataObject());
            }

            if (newItem != null)
            {
                this.AddItem(newItem);
                this.LayoutContent();
                this.SelectItem(newItem);
                this.SetDirty();
            }
        }

        private void HasSelectedItem(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.selected != null;
        }

        private void HasSelectedImage(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.selected is AttachmentDialogImageItem;
        }

        private void CanSave(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.dirty && this.ItemCount > 0;
        }

        private static AttachmentDialog dialog = null;

        private static void OnAppClosed(object sender, EventArgs e)
        {
            if (dialog != null)
            {
                dialog.Close();
            }
        }

        private static void OnAppWindowStateChanged(object sender, EventArgs e)
        {
            if (dialog != null)
            {
                if (App.Current.MainWindow.WindowState == WindowState.Minimized)
                {
                    dialog.WindowState = WindowState.Minimized;
                }
                else if (dialog.WindowState == WindowState.Minimized)
                {
                    dialog.WindowState = WindowState.Normal;
                    dialog.Activate();
                }
            }
        }

        public static void OnSelectionChanged(Transaction t)
        {
            if (dialog != null)
            {
                dialog.Transaction = t;
            }
        }

        public static void ScanAttachments(Transaction t, AttachmentManager attachmentManager, Settings settings)
        {
            if (dialog == null)
            {
                dialog = new AttachmentDialog();
                dialog.Settings = settings;
                dialog.Directory = settings.AttachmentDirectory;

                dialog.Closing += new System.ComponentModel.CancelEventHandler((s, args) =>
                {
                    if (dialog.WindowState != WindowState.Minimized)
                    {
                        settings.AttachmentDialogSize = new Size(dialog.Width, dialog.Height);
                    }
                    if (App.Current != null && App.Current.MainWindow != null)
                    {
                        App.Current.MainWindow.Closed -= new EventHandler(OnAppClosed);
                    }
                    App.Current.MainWindow.Activate();
                    AttachmentDialog.dialog = null;
                });
                App.Current.MainWindow.Closed -= new EventHandler(OnAppClosed);
                App.Current.MainWindow.Closed += new EventHandler(OnAppClosed);
                App.Current.MainWindow.StateChanged -= new EventHandler(OnAppWindowStateChanged);
                App.Current.MainWindow.StateChanged += new EventHandler(OnAppWindowStateChanged);
            }

            string path = settings.AttachmentDirectory;
            if (string.IsNullOrEmpty(path))
            {
                string backups = settings.BackupPath;
                if (!string.IsNullOrEmpty(backups))
                {
                    Uri uri = new Uri(new Uri(backups), "..\\Attachment");
                    path = uri.LocalPath;
                    settings.AttachmentDirectory = path;
                }
            }
            Size size = settings.AttachmentDialogSize;
            if (size.Width > 100 && size.Height > 100)
            {
                dialog.Width = size.Width;
                dialog.Height = size.Height;
            }

            dialog.Initialize(attachmentManager, t, path);
            dialog.Owner = App.Current.MainWindow;
            dialog.ShowInTaskbar = false;
            dialog.Show();
            dialog.Activate();
        }

        private void Initialize(AttachmentManager attachmentManager, Data.Transaction t, string path)
        {
            this.Manager = attachmentManager;
            this.Transaction = t;
            this.Directory = path;
        }

        private void RotateRight(object sender, ExecutedRoutedEventArgs e)
        {
            AttachmentDialogImageItem img = this.selected as AttachmentDialogImageItem;
            if (img == null)
            {
                return;
            }
            img.RotateImage(90);
            this.OnItemChanged(img);
        }

        private void RotateLeft(object sender, ExecutedRoutedEventArgs e)
        {
            AttachmentDialogImageItem img = this.selected as AttachmentDialogImageItem;
            if (img == null)
            {
                return;
            }
            img.RotateImage(-90.0);
            this.OnItemChanged(img);
        }

        private const double PrintMargin = 10;

        private void Print(object sender, ExecutedRoutedEventArgs e)
        {
            if (this.selected == null)
            {
                return;
            }

            Rect bounds = this.GetItemContentBounds(this.selected);
            double w = bounds.Width;
            double h = bounds.Height;

            FrameworkElement visual = this.selected.CloneContent();
            visual.Margin = new Thickness(PrintMargin);
            visual.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            visual.Arrange(new Rect(0, 0, w + (2 * PrintMargin), h + (2 * PrintMargin)));

            PrintDialog pd = new PrintDialog();
            // pd.Owner = this; // BugBug, print dialog is missing this?
            if (pd.ShowDialog() == true)
            {
                pd.PrintVisual(visual, "Attachment");
            }
        }

        private void OnCropImage(object sender, ExecutedRoutedEventArgs e)
        {
            AttachmentDialogImageItem img = this.selected as AttachmentDialogImageItem;
            if (img == null)
            {
                return;
            }
            this.AutoCrop(img);
        }

        private void AutoCrop(AttachmentDialogImageItem img)
        {
            CannyEdgeDetector edgeDetector = new CannyEdgeDetector(img.Bitmap, 20, 80, 30);
            edgeDetector.DetectEdges();
            Rect bounds = edgeDetector.EdgeBounds;
            this.AddResizer(img, bounds);
            this.SetDirty();
        }

    }

    /// <summary>
    ///  This class wraps an item in the attachment dialog and keeps track of it's file name
    ///  and file type.
    /// </summary>
    internal abstract class AttachmentDialogItem : FrameworkElement
    {
        /// <summary>
        /// The content being rendered in this item.
        /// </summary>
        public abstract FrameworkElement Content { get; }

        /// <summary>
        /// The current file name
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// The default file extension for this kind of item.
        /// </summary>
        public abstract string FileExtension { get; }

        /// <summary>
        /// Whether content supports live resizing.
        /// If this is true Resize is called while user is resizing rather than after resizing gesture is done.
        /// </summary>
        public abstract bool LiveResizable { get; }

        /// <summary>
        /// Return the allowable limit for new bounds sent to Resize method.
        /// </summary>
        public abstract Rect ResizeLimit { get; }

        /// <summary>
        /// Resize the content to this new size
        /// </summary>
        /// <param name="newBounds">The new bounds for the content</param>
        public abstract void Resize(Rect newBounds);

        /// <summary>
        /// Copy content to clipboard.
        /// </summary>
        public abstract void Copy();

        /// <summary>
        /// Create a copy of the content (for printing)
        /// </summary>
        /// <returns></returns>
        public abstract FrameworkElement CloneContent();

        /// <summary>
        /// Save the content to the given file.
        /// </summary>
        /// <param name="filePath"></param>
        public abstract void Save(string filePath);

        /// <summary>
        /// This event is raised if the content is changed.
        /// </summary>
        public event EventHandler ContentChanged;

        protected void OnContentChanged()
        {
            if (ContentChanged != null)
            {
                ContentChanged(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// This class implements the abstract AttachmentDialogItem by
    /// wrapping an Image as the content
    /// </summary>
    internal class AttachmentDialogImageItem : AttachmentDialogItem
    {
        private readonly Image image;

        public AttachmentDialogImageItem(BitmapSource source)
        {
            this.image = new Image();
            this.image.Source = source;
            this.AddVisualChild(this.image);
        }

        public AttachmentDialogImageItem(string filePath)
        {
            this.FileName = filePath;

            BitmapSource frame = LoadImage(filePath);

            if (frame.Format != PixelFormats.Pbgra32)
            {
                frame = this.ConvertImageFormat(frame);
            }

            this.image = new Image();
            this.image.Source = frame;
            this.AddVisualChild(this.image);
        }

        private static BitmapFrame LoadImage(string filePath)
        {
            // Load it into memory first so we stop the BitmapDecoder from locking the file.
            MemoryStream ms = new MemoryStream();
            byte[] buffer = new byte[16000];
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                int len = fs.Read(buffer, 0, buffer.Length);
                while (len > 0)
                {
                    ms.Write(buffer, 0, len);
                    len = fs.Read(buffer, 0, buffer.Length);
                }
            }
            ms.Seek(0, SeekOrigin.Begin);

            BitmapDecoder decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.IgnoreImageCache, BitmapCacheOption.None);
            BitmapFrame frame = decoder.Frames[0];

            return frame;
        }

        public override void Save(string fileName)
        {
            this.FileName = fileName;
            BitmapSource source = this.Bitmap;
            var frame = BitmapFrame.Create(source);
            JpegBitmapEncoder encoder = new JpegBitmapEncoder();
            encoder.Frames.Add(frame);

            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(this.FileName));

            using (FileStream fs = new FileStream(this.FileName, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                encoder.Save(fs);
            }

            TempFilesManager.RemoveTempFile(this.FileName);
        }

        private BitmapSource ConvertImageFormat(BitmapSource image)
        {
            int width = image.PixelWidth;
            int height = image.PixelHeight;
            if (image.Format != PixelFormats.Pbgra32)
            {
                // then copy the image to a format we can handle.
                RenderTargetBitmap copy = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                Image img = new Image();
                img.Source = image;
                img.Width = width;
                img.Height = height;
                img.Arrange(new Rect(0, 0, width, height));
                copy.Render(img);
                return copy;
            }
            return image;
        }

        public override FrameworkElement Content
        {
            get { return this.image; }
        }

        public BitmapSource Bitmap
        {
            get { return (BitmapSource)this.image.Source; }
            set { this.image.Source = value; }
        }

        public override void Copy()
        {
            Clipboard.SetImage((BitmapSource)this.image.Source);
        }


        public override FrameworkElement CloneContent()
        {
            return new Image()
            {
                Source = this.image.Source,
                Width = this.image.Source.Width,
                Height = this.image.Source.Height
            };
        }

        public override string FileExtension
        {
            get { return ".jpg"; }
        }

        public override bool LiveResizable
        {
            get { return false; }
        }

        protected override Visual GetVisualChild(int index)
        {
            if (index == 0)
            {
                return this.image;
            }
            return null;
        }

        protected override int VisualChildrenCount
        {
            get
            {
                return 1;
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            this.image.Measure(availableSize);
            return new Size(this.image.Source.Width, this.image.Source.Height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            this.image.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
            return finalSize;
        }

        public override Rect ResizeLimit
        {
            get { return new Rect(0, 0, this.image.Source.Width, this.image.Source.Height); }
        }

        public override void Resize(Rect bounds)
        {
            BitmapSource bitmap = this.Bitmap;

            double dpiX = bitmap.DpiX / 96;
            double dpiY = bitmap.DpiY / 96;

            Rect adjusted = new Rect(bounds.Left * dpiX, bounds.Top * dpiY, bounds.Width * dpiX, bounds.Height * dpiY);

            if (adjusted.Height == 0)
            {
                adjusted.Height = bitmap.Height;
            }
            if (adjusted.Width == 0)
            {
                adjusted.Width = bitmap.Width;
            }
            if (adjusted.Left + adjusted.Width > bitmap.Width)
            {
                adjusted.Width = bitmap.Width - adjusted.Left;
            }
            if (adjusted.Top + adjusted.Height > bitmap.Height)
            {
                adjusted.Height = bitmap.Height - adjusted.Top;
            }

            var cropped = new CroppedBitmap(bitmap, new Int32Rect((int)adjusted.Left, (int)adjusted.Top, (int)adjusted.Width, (int)adjusted.Height));
            this.Bitmap = cropped;
        }

        internal void RotateImage(double degrees)
        {
            ImageSource source = this.image.Source;

            // then copy the image to a format we can handle.
            double w = source.Width;
            double h = source.Height;

            RenderTargetBitmap copy = new RenderTargetBitmap((int)h, (int)w, 96, 96, PixelFormats.Pbgra32);
            Image img = new Image();
            img.Source = source;
            img.Width = w;
            img.Height = h;
            // rotate the angle about 0,0
            RotateTransform rotate = new RotateTransform(degrees, 0, 0);

            Rect bounds = new Rect(0, 0, w, h);
            // figure out where the bounds ends up with this rotation.
            Rect transformed = rotate.TransformBounds(bounds);

            // keep the bounds in non-negative territory by adjusting with a translate.
            TransformGroup group = new TransformGroup();
            group.Children.Add(rotate);
            group.Children.Add(new TranslateTransform(transformed.X < 0 ? -transformed.X : 0,
                transformed.Y < 0 ? -transformed.Y : 0));

            img.RenderTransform = group;
            img.Arrange(bounds);
            copy.Render(img);

            this.image.Source = copy;
        }

    }

    internal class AttachmentDialogFileItem : AttachmentDialogItem
    {
        private Image image;

        public AttachmentDialogFileItem(string fileName)
        {
            this.image = new Image();
            this.FileName = fileName;
            image.Source = FileIcons.Extract(fileName); ;
            this.AddVisualChild(this.image);
            this.Loaded += this.OnItemLoaded;
        }

        private void OnItemLoaded(object sender, RoutedEventArgs e)
        {
            if (image.Source == null)
            {
                var drawing = (DrawingImage)this.FindResource("DefaultFileImage");
                image.Source = drawing;
            }
            if (image.Source != null)
            {
                image.Width = this.image.Source.Width;
                image.Height = this.image.Source.Height;
            }
        }

        public override FrameworkElement Content => this.image;

        public override string FileExtension => System.IO.Path.GetExtension(this.FileName);

        public override bool LiveResizable => false;

        public override Rect ResizeLimit
        {
            get { return new Rect(0, 0, this.image.Source.Width, this.image.Source.Height); }
        }

        public override FrameworkElement CloneContent()
        {
            return new Image() { Source = image.Source };
        }

        public override void Copy()
        {
        }

        public override void Resize(Rect newBounds)
        {
        }

        public override void Save(string filePath)
        {
        }

        protected override Visual GetVisualChild(int index)
        {
            if (index == 0)
            {
                return this.image;
            }
            return null;
        }

        protected override int VisualChildrenCount
        {
            get
            {
                return 1;
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            this.image.Measure(availableSize);
            return new Size(this.image.Source.Width, this.image.Source.Height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            this.image.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
            return finalSize;
        }
    }

    /// <summary>
    /// This class implements the abstract AttachmentDialogItem by
    /// wrapping a RichTextBox as the content
    /// </summary>
    internal class AttachmentDialogDocumentItem : AttachmentDialogItem
    {
        private readonly RichTextBox richText;

        public AttachmentDialogDocumentItem()
        {
            this.richText = new RichTextBox();
            this.richText.MinWidth = 600;
            this.AddVisualChild(this.richText);
            this.RegisterEvents();
        }

        public AttachmentDialogDocumentItem(string fileName)
        {
            this.FileName = fileName;

            this.richText = new RichTextBox();
            this.richText.MinWidth = 600;

            using (FileStream fs = new FileStream(fileName, FileMode.Open))
            {
                this.Load(fs, DataFormats.XamlPackage);
            }

            this.AddVisualChild(this.richText);
            this.RegisterEvents();
        }

        public AttachmentDialogDocumentItem(IDataObject clipboard)
        {
            this.richText = new RichTextBox();
            this.richText.MinWidth = 600;

            using (MemoryStream ms = new MemoryStream())
            {
                string dataFormat = null;
                string text = null;

                if (clipboard.GetDataPresent(DataFormats.XamlPackage))
                {
                    dataFormat = DataFormats.XamlPackage;
                    byte[] data = (byte[])clipboard.GetData(dataFormat);
                    ms.Write(data, 0, data.Length);
                }
                else if (clipboard.GetDataPresent(DataFormats.Rtf))
                {
                    dataFormat = DataFormats.Rtf;
                    string rtf = (string)clipboard.GetData(dataFormat);
                    byte[] data = Encoding.UTF8.GetBytes(rtf);
                    ms.Write(data, 0, data.Length);
                }
                else if (clipboard.GetDataPresent(DataFormats.UnicodeText))
                {
                    dataFormat = DataFormats.UnicodeText;
                    text = (string)clipboard.GetData(dataFormat);
                }
                else if (clipboard.GetDataPresent(DataFormats.Text))
                {
                    dataFormat = DataFormats.Text;
                    text = (string)clipboard.GetData(dataFormat);
                }
                ms.Seek(0, SeekOrigin.Begin);

                if (text != null)
                {
                    var content = new TextRange(this.richText.Document.ContentStart, this.richText.Document.ContentEnd);
                    content.Text = text;
                }
                else
                {
                    this.Load(ms, dataFormat);
                }
            }

            this.AddVisualChild(this.richText);
            this.RegisterEvents();
        }

        private void RegisterEvents()
        {
            this.richText.TextChanged += this.richText_TextChanged;
        }

        private void richText_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.OnContentChanged();
        }

        private void Load(Stream stream, string dataFormat)
        {
            var content = new TextRange(this.richText.Document.ContentStart, this.richText.Document.ContentEnd);
            content.Load(stream, dataFormat);
        }

        public override FrameworkElement Content
        {
            get { return this.richText; }
        }

        public override string FileExtension
        {
            get { return ".xamlpackage"; }
        }

        public override bool LiveResizable
        {
            get { return true; }
        }

        public override Rect ResizeLimit
        {
            get { return new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight); }
        }

        public override void Resize(Rect newBounds)
        {
            // all we want to resize actually is the width of the richText box
            // the rest will flow from there...
            this.richText.Width = newBounds.Width;
            this.richText.ClearValue(RichTextBox.MinWidthProperty);
            this.InvalidateArrange();
            this.OnContentChanged();
        }

        public override void Copy()
        {
            try
            {
                var content = new TextRange(this.richText.Document.ContentStart, this.richText.Document.ContentEnd);
                using (MemoryStream ms = new MemoryStream())
                {
                    content.Save(ms, DataFormats.XamlPackage, true);
                    byte[] data = ms.ToArray();

                    DataObject clip = new DataObject(DataFormats.XamlPackage, data);
                    var formats = clip.GetFormats();
                    clip.SetText(content.Text);

                    ms.Seek(0, SeekOrigin.Begin);
                    content.Save(ms, DataFormats.Rtf, true);
                    byte[] rtfdata = ms.ToArray();

                    formats = clip.GetFormats();
                    clip.SetData(DataFormats.Rtf, rtfdata);
                }
            }
            catch
            {
            }
        }

        public override FrameworkElement CloneContent()
        {
            RichTextBox copy = new RichTextBox();
            copy.MinWidth = this.richText.MinWidth;

            var content = new TextRange(this.richText.Document.ContentStart, this.richText.Document.ContentEnd);
            using (MemoryStream ms = new MemoryStream())
            {
                content.Save(ms, DataFormats.XamlPackage, true);
                ms.Seek(0, SeekOrigin.Begin);

                var copyContent = new TextRange(copy.Document.ContentStart, copy.Document.ContentEnd);
                copyContent.Load(ms, DataFormats.XamlPackage);
            }
            return copy;
        }

        public override void Save(string fileName)
        {
            this.FileName = fileName;

            var content = new TextRange(this.richText.Document.ContentStart, this.richText.Document.ContentEnd);
            using (MemoryStream ms = new MemoryStream())
            {
                content.Save(ms, DataFormats.XamlPackage, true);
                byte[] data = ms.ToArray();

                using (FileStream fs = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                {
                    fs.Write(data, 0, data.Length);
                }
            }

            TempFilesManager.RemoveTempFile(this.FileName);
        }

        protected override Visual GetVisualChild(int index)
        {
            if (index == 0)
            {
                return this.richText;
            }
            return null;
        }

        protected override int VisualChildrenCount
        {
            get
            {
                return 1;
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            this.richText.Measure(availableSize);
            return this.richText.DesiredSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            this.richText.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
            return finalSize;
        }

    }

    internal enum WiaErrorCode
    {
        NONE,
        WIA_ERROR_GENERAL_ERROR,
        WIA_ERROR_PAPER_JAM,
        WIA_ERROR_PAPER_EMPTY,
        WIA_ERROR_PAPER_PROBLEM,
        WIA_ERROR_OFFLINE,
        WIA_ERROR_BUSY,
        WIA_ERROR_WARMING_UP,
        WIA_ERROR_USER_INTERVENTION,
        WIA_ERROR_ITEM_DELETED,
        WIA_ERROR_DEVICE_COMMUNICATION,
        WIA_ERROR_INVALID_COMMAND,
        WIA_ERROR_INCORRECT_HARDWARE_SETTING,
        WIA_ERROR_DEVICE_LOCKED,
        WIA_ERROR_EXCEPTION_IN_DRIVER,
        WIA_ERROR_INVALID_DRIVER_RESPONSE,
        WIA_ERROR_COVER_OPEN,
        WIA_ERROR_LAMP_OFF,
        WIA_ERROR_DESTINATION,
        WIA_ERROR_NETWORK_RESERVATION_FAILED,
        WIA_ERROR_MULTI_FEED,
        WIA_ERROR_MAXIMUM_PRINTER_ENDORSER_COUNTER
    }
}

