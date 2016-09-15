using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Miyu {
    public sealed partial class WindowTool : UserControl {
        bool PointerDown;
        Point DownPoint;
        double DownLeft;
        double DownTop;
        FrameworkElement UserWindow;
        Canvas ParentCanvas;

        public WindowTool() {
            this.InitializeComponent();
        }

        private void UserControl_PointerPressed(object sender, PointerRoutedEventArgs e) {
            PointerDown = true;
            CapturePointer(e.Pointer);

            UserWindow = (this.Parent as FrameworkElement).Parent as FrameworkElement;
            ParentCanvas = UserWindow.Parent as Canvas;
            DownPoint = e.GetCurrentPoint(ParentCanvas).Position;

            DownLeft = Canvas.GetLeft(UserWindow);
            DownTop  = Canvas.GetTop(UserWindow);

            Debug.WriteLine("Press {0}", DownPoint);
        }

        private void UserControl_PointerMoved(object sender, PointerRoutedEventArgs e) {
            if (PointerDown) {
                Point pt = e.GetCurrentPoint(ParentCanvas).Position;
                double dx = pt.X - DownPoint.X;
                double dy = pt.Y - DownPoint.Y;
                Debug.WriteLine("Drag {0},{1}", dx, dy);

                Canvas.SetLeft(UserWindow, DownLeft + dx);
                Canvas.SetTop(UserWindow, DownTop + dy);
            }
        }

        private void UserControl_PointerReleased(object sender, PointerRoutedEventArgs e) {
            if (PointerDown) {

                Debug.WriteLine("Release {0}", e.GetCurrentPoint(ParentCanvas).Position);
                PointerDown = false;

                ReleasePointerCapture(e.Pointer);
            }
        }
    }
}
