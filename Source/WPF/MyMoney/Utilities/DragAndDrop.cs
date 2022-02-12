using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Walkabout.Utilities
{
    public class DragAndDrop
    {


        #region PROPERTIES

        bool isMouseDown = false;
        Point dragStartPoint;

        string formatName;
        bool isDragging = false;

        Window dragdropWindow;
        AdornerLayer adornerLayer;
        Adorner lastAdornerUsed;


        FrameworkElement mainControl;
        OnIsDragSourceValid calledBackForValidatingSource;
        OnIsValidDropTarget calledBackForValidatingTarget;
        OnApplyDragDrop calledBackFinalDropOperation;

        #endregion


        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="mainControl"></param>
        /// <param name="dragDropFormatName"></param>
        /// <param name="validateDragSource"></param>
        /// <param name="finalDragDropOperation"></param>
        public DragAndDrop(
            FrameworkElement mainControl,
            string dragDropFormatName,
            OnIsDragSourceValid validateDragSource,
            OnIsValidDropTarget validDropTarget,
            OnApplyDragDrop finalDragDropOperation
            )
        {
            this.mainControl = mainControl;
            this.formatName = dragDropFormatName;
            this.calledBackForValidatingSource = validateDragSource;
            this.calledBackForValidatingTarget = validDropTarget;
            this.calledBackFinalDropOperation = finalDragDropOperation;
            
            //-----------------------------------------------------------------
            // Drag Drop gesture hooks applied to the users supplied Framework Control
            this.mainControl.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(OnPreviewMouseLeftButtonDown);
            this.mainControl.PreviewMouseMove += new MouseEventHandler(OnPreviewMouseMove);
            this.mainControl.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(OnPreviewMouseLeftButtonUp);
            this.mainControl.PreviewDragEnter += new DragEventHandler(OnPreviewDragEnter);

            this.mainControl.Drop += new DragEventHandler(OnDrop);
            this.mainControl.PreviewKeyDown += MainControl_PreviewKeyDown;
        }

        private void MainControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && this.isDragging)
            {
                this.isDragging = false;
            }
        }

        void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.isMouseDown = true;
                this.dragStartPoint = e.GetPosition(this.mainControl);
                this.dragSourceStartedFrom = e.OriginalSource;
                this.captured = this.mainControl.CaptureMouse();
            }
        }

        object dragSourceStartedFrom;
        private bool captured;

        void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.isDragging = false;
            this.isMouseDown = false;
            DestroyDragDropWindow();
            if (this.captured)
            {
                this.mainControl.ReleaseMouseCapture();
            }
        }

        void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (this.isMouseDown == false)
            {
                return;
            }

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                // Get the current mouse position
                Point mousePos = e.GetPosition(this.mainControl);
                Vector diff = this.dragStartPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    // Find out if we have a acceptable object instance behind for the UIElement being dragged
                    DragDropSource validObjectToDrag = calledBackForValidatingSource(dragSourceStartedFrom);
                    if (validObjectToDrag != null)
                    {
                        StartDrag(e, validObjectToDrag);
                    }
                }
            }
        }

        void OnPreviewDragEnter(object sender, DragEventArgs e)
        {
            if (UpdateEffects(e) == true)
            {
                e.Handled = true;
            }
        }

        void OnDrop(object sender, DragEventArgs e)
        {
            if (this.isDragging && e.Data.GetDataPresent(formatName))
            {
                object dragSource = e.Data.GetData(formatName);
                if (dragSource != null)
                {
                    DragDropTarget dropTarget = this.calledBackForValidatingTarget(dragSource, e.OriginalSource, e.Effects);
                    if (dropTarget != null)
                    {
                        if (dragSource != dropTarget.DataSource)
                        {
                            e.Handled = true;
                            UpdateEffects(e);
                            DestroyDragDropWindow();
                            this.calledBackFinalDropOperation(dragSource, dropTarget.DataSource, e.Effects);
                        }
                    }
                }
            }

            DragDropRemoveAnyAdorner();
        }






        /// <summary>
        /// Update the position of the transparent drag/drop feedback window
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnDragSourceGiveFeedback(object sender, System.Windows.GiveFeedbackEventArgs e)
        {
            if (this.isDragging)
            {
                e.UseDefaultCursors = true;
                UpdateWindowLocation();
            }
        }

        void OnDragSourceQueryContinueDrag(object sender, QueryContinueDragEventArgs e)
        {
            if (this.isDragging)
            {
                UpdateWindowLocation();
            }
        }


        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "e")]
        private void StartDrag(MouseEventArgs e, DragDropSource objectBeenDragged)
        {
            DestroyDragDropWindow();

            this.isDragging = true;

            GiveFeedbackEventHandler feedbackHandler = new GiveFeedbackEventHandler(OnDragSourceGiveFeedback); ;
            this.mainControl.GiveFeedback += feedbackHandler;

            QueryContinueDragEventHandler queryContinueHandler = new QueryContinueDragEventHandler(OnDragSourceQueryContinueDrag);
            this.mainControl.QueryContinueDrag += queryContinueHandler;


            try
            {
                if (objectBeenDragged.VisualForDraginSource != null)
                {
                    CreateDragDropWindow(objectBeenDragged.VisualForDraginSource);
                }

                // Initialize the drag & drop operation
                DataObject dragData = new DataObject(formatName, objectBeenDragged.DataSource);
                DragDrop.DoDragDrop(this.mainControl, dragData, DragDropEffects.Move | DragDropEffects.Copy);                
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
            finally
            {

                this.isDragging = false;
                DestroyDragDropWindow();
                this.mainControl.GiveFeedback -= feedbackHandler;
                this.mainControl.QueryContinueDrag -= queryContinueHandler;
            }
        }


        void AttemptToScrollUp(int speed)
        {
            // Attemp to scroll up
            ScrollViewer sv = this.mainControl.FindFirstDescendantOfType<ScrollViewer>();
            if (sv != null)
            {
                for (int i = 1; i <= speed; i++)
                {
                    sv.LineUp();
                }
            }
        }

        void AttemptToScrollDown(int speed)
        {
            // Attemp to scroll up
            ScrollViewer sv = WpfHelper.FindFirstDescendantOfType<ScrollViewer>(this.mainControl);
            if (sv != null)
            {
                for (int i = 1; i <= speed; i++)
                {
                    sv.LineDown();
                }
            }
        }

        object previousPossibleDropTarget;

        private bool UpdateEffects(DragEventArgs e)
        {
            if (!this.isDragging)
            {
                DragDropRemoveAnyAdorner();
                e.Effects = DragDropEffects.None;
                return true;
            }

            object dragSource = e.Data.GetData(formatName);
            if (dragSource == null)
            {
                DragDropRemoveAnyAdorner();
                e.Effects = DragDropEffects.None;
                return false;
            }

            const int dragScrollMarginSpeedSlow = 30;
            const int dragScrollMarginFast = 10;

            Point pt = e.GetPosition(this.mainControl);
            HitTestResult result = VisualTreeHelper.HitTest(this.mainControl, pt);
            if (result != null)
            {
                if (pt.Y < dragScrollMarginSpeedSlow)
                {
                    // The user is close to the bottom of the container (ListBox or TreeView)
                    if (pt.Y < dragScrollMarginFast)
                    {
                        AttemptToScrollUp(4);
                    }
                    else
                    {
                        AttemptToScrollUp(1);
                    }
                }
                else if (pt.Y > this.mainControl.ActualHeight - dragScrollMarginSpeedSlow)
                {
                    // The user is close to the bottom of the container (ListBox or TreeView)
                    if (pt.Y > this.mainControl.ActualHeight - dragScrollMarginFast)
                    {
                        AttemptToScrollDown(4);
                    }
                    else
                    {
                        AttemptToScrollDown(1);
                    }
                }


                FrameworkElement ctrl = result.VisualHit as FrameworkElement;
                if (ctrl != null)
                {
                    DragDropTarget possibleDropTarget = this.calledBackForValidatingTarget(dragSource, ctrl, e.Effects);

                    if (possibleDropTarget != null && possibleDropTarget.DataSource != previousPossibleDropTarget)
                    {
                        if (possibleDropTarget.DataSource != dragSource)
                        {
                            DragDropRemoveAnyAdorner();
                            previousPossibleDropTarget = possibleDropTarget.DataSource;
                            this.adornerLayer = AdornerLayer.GetAdornerLayer(possibleDropTarget.TargetElement);
                            lastAdornerUsed = new AdornerDropTarget(possibleDropTarget.TargetElement);
                            this.adornerLayer.Add(lastAdornerUsed);
                        }
                    }
                } 
                else
                {
                    DragDropRemoveAnyAdorner();
                }
            }
            else
            {
                DragDropRemoveAnyAdorner();
            }



            if (((e.AllowedEffects & DragDropEffects.Copy) == DragDropEffects.Copy) ||
                ((e.AllowedEffects & DragDropEffects.Move) == DragDropEffects.Move))
            {
                if ((e.KeyStates & DragDropKeyStates.ControlKey) == DragDropKeyStates.ControlKey)
                {
                    e.Effects = DragDropEffects.Copy;
                }
                else
                {
                    e.Effects = DragDropEffects.Move;
                }
            }
            else
            {
                e.Effects = e.AllowedEffects & ((DragDropEffects.Copy | DragDropEffects.Move) ^ (DragDropEffects.All));
            }

            return true;
        }

        private void DragDropRemoveAnyAdorner()
        {
            if (this.adornerLayer != null)
            {
                this.adornerLayer.Remove(lastAdornerUsed);
            }
        }




        /// <summary>
        /// Build grid of selected elements for drag/drop feedback
        /// </summary>
        /// <param name="dragElement">The dragged elements</param>
        /// <returns>A visual for drag/drop transparent window</returns>
        static Visual BuildDragDropVisual(FrameworkElement dragElement)
        {
            dragElement.Measure(new Size(1000, 1000));
            dragElement.Arrange(new Rect(0, 0, 200, 50));
            Rect bounds = GetVisualBounds(dragElement);
            if (bounds == Rect.Empty)
            {
                bounds = new Rect(0, 0, 0, 0);
            }
            if (bounds.Width <= 0 || double.IsNaN(bounds.Width) || double.IsInfinity(bounds.Width))
            {
                bounds.Width = dragElement.ActualWidth;
            }
            if (bounds.Width == 0)
            {
//                bounds.Width = 40;
            }

            if (bounds.Height <= 0 || double.IsNaN(bounds.Height) || double.IsInfinity(bounds.Height))
            {
                bounds.Height = dragElement.ActualHeight;
            }
            if (bounds.Height == 0)
            {
   //             bounds.Height = 40;
            }

            Border border = new Border();
            border.Width = bounds.Width + 3;
            border.Height = bounds.Height + 3;
            border.BorderBrush = Brushes.Gray;
            border.BorderThickness = new Thickness(0.5, 0.5, 3, 3);
            border.CornerRadius = new CornerRadius(3,3,0,3);
            
            Canvas canvas = new Canvas();
            canvas.Opacity = 0.80;
            canvas.Background = new VisualBrush(dragElement);
            canvas.Width = bounds.Width;
            canvas.Height = bounds.Height;

            border.Child = canvas;
            return border;
        }

      

        /// <summary>
        /// Get the bounds of the given visual element.
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        static Rect GetVisualBounds(FrameworkElement e)
        {
            return VisualTreeHelper.GetDescendantBounds(e);
        }




        #region DRAG WINDOW

        public void CreateDragDropWindow(FrameworkElement dragVisual)
        {
            dragdropWindow = new Window();
            object style = this.mainControl.TryFindResource("DragDropWindowStyle");
            dragdropWindow.Style = (Style)style;
            

            dragdropWindow.SourceInitialized += new EventHandler(
                delegate(object sender, EventArgs args)
                {
                    PresentationSource windowSource = PresentationSource.FromVisual(this.dragdropWindow);
                    IntPtr handle = ((System.Windows.Interop.HwndSource)windowSource).Handle;

                    uint styles = NativeMethods.GetWindowLong(handle, NativeMethods.GWL_EXSTYLE);
                    uint hr = NativeMethods.SetWindowLong(handle, NativeMethods.GWL_EXSTYLE, styles | NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TRANSPARENT);
                    if (hr == 0) // failure for SetWindowLong is zero!
                    {
                        System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                    }

                });

            this.dragdropWindow.Content = dragVisual;
            this.dragdropWindow.UpdateLayout();

            // Show the window at the current mouse position
            UpdateWindowLocation();
            this.dragdropWindow.Show();
        }



        /// <summary>
        /// Place the drag/drop main window at location of the mouse
        /// </summary>
        void UpdateWindowLocation()
        {
            if (this.dragdropWindow != null)
            {
                Point pos = NativeMethods.GetMousePosition();
                this.dragdropWindow.Left = pos.X+10; 
                this.dragdropWindow.Top = pos.Y+10;
            }
        }


        /// <summary>
        /// Remove the nice transparent drag/drop feedback
        /// </summary>
        void DestroyDragDropWindow()
        {
            if (dragdropWindow != null)
            {
                this.dragdropWindow.Close();
                this.dragdropWindow = null;
            }
        }

        #endregion
    }
    
    public delegate DragDropSource OnIsDragSourceValid(object source);
    public delegate DragDropTarget OnIsValidDropTarget(object source, object target, DragDropEffects dropEfffect);
    public delegate void OnApplyDragDrop(object source, object target, DragDropEffects dropEfffect);


    public class DragDropSource
    {
        public object DataSource { get; set; }
        public FrameworkElement VisualForDraginSource { get; set; }
    }


    public class DragDropTarget
    {
        public object DataSource { get; set; }
        public FrameworkElement TargetElement { get; set; }
    }

}
