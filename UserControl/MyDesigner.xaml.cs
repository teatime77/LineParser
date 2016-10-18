using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Miyu {
    public sealed partial class MyDesigner : UserControl {

        // 描画した図形のリスト
        List<TShape> ShapeList = new List<TShape>();

        // マウスのイベントハンドラのコルーチン
        IEnumerator PointerEventLoop;

        // マウスのイベントハンドラで使うイベントのタイプ
        EEvent PointerEventType = EEvent.Undefined;

        // 現在のイベントオブジェクト
        PointerRoutedEventArgs CurrentPointerEvent;

        public Point CanvasSize = new Point(5000, 5000);

        int ToolIdx;

        // ズーム レベル
        public int ZoomLevel = 0;
        public double ZoomScale = 1;

        public MyDesigner() {
            this.InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e) {
            DesignerCanvas.Width = CanvasSize.X;
            DesignerCanvas.Height = CanvasSize.Y;
        }

        /*
            キャンバスを再描画する。 
        */
        public void InvalidateCanvas() {
            DesignerWin2D.Invalidate();
        }

        private void DesignerScroll_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e) {
            TLog.WriteLine("<<--- ViewChanged");

            // 再描画する。
            InvalidateCanvas();
        }

        private void DesignerWin2D_Draw(Microsoft.Graphics.Canvas.UI.Xaml.CanvasControl sender, Microsoft.Graphics.Canvas.UI.Xaml.CanvasDrawEventArgs args) {
            TLog.WriteLine("<<--- Designer Draw");

            TGraphics gr = new TGraphics(args.DrawingSession, ZoomScale, - DesignerScroll.HorizontalOffset, - DesignerScroll.VerticalOffset);

            foreach(TShape shape in ShapeList) {
                shape.Draw(gr);
            }

            // ビューの幅と高さ
            float view_w = (float)DesignerWin2D.ActualWidth;
            float view_h = (float)DesignerWin2D.ActualHeight;
        }

        private void DesignerWin2D_PointerPressed(object sender, PointerRoutedEventArgs e) {

            if (PointerEventLoop == null) {
                // ポインタのイベントハンドラがnullの場合

                PointerEventLoop = PointerHandler(e);
            }

            // ポインタのイベントハンドラのコルーチンを継続する。
            ContinuePointerEventLoop(EEvent.PointerPressed, e);

        }

        private void DesignerWin2D_PointerMoved(object sender, PointerRoutedEventArgs e) {
            // ポインタのイベントハンドラのコルーチンを継続する。
            ContinuePointerEventLoop(EEvent.PointerMoved, e);
        }

        private void DesignerWin2D_PointerReleased(object sender, PointerRoutedEventArgs e) {
            // ポインタのイベントハンドラのコルーチンを継続する。
            ContinuePointerEventLoop(EEvent.PointerReleased, e);
            MyEditor.WriteLine("<<--- CoreWindow PointerReleased");
        }

        private void DesignerWin2D_PointerWheelChanged(object sender, PointerRoutedEventArgs e) {
            e.Handled = true;

            // ビューの幅と高さ
            float view_w = (float)DesignerWin2D.ActualWidth;
            float view_h = (float)DesignerWin2D.ActualHeight;

            Point view_center = GetViewCenterReal();

            ZoomLevel += (e.GetCurrentPoint(DesignerWin2D).Properties.MouseWheelDelta < 0 ? -1 : 1);
            ZoomScale = Math.Pow(1.2, ZoomLevel);

            double px2 = Math.Max(0, view_center.X * ZoomScale - view_w / 2);
            double py2 = Math.Max(0, view_center.Y * ZoomScale - view_h / 2);

            DesignerCanvas.Width = ZoomScale * CanvasSize.X;
            DesignerCanvas.Height = ZoomScale * CanvasSize.Y;

            DesignerScroll.ChangeView(px2, py2, 1, true);

            // 再描画する。
            InvalidateCanvas();

            Debug.WriteLine("wheel {0} {1:0.000}", e.GetCurrentPoint(DesignerWin2D).Properties.MouseWheelDelta, ZoomScale);
        }

        public Point GetViewCenterReal() {
            // ビューの幅と高さ
            float view_w = (float)DesignerWin2D.ActualWidth;
            float view_h = (float)DesignerWin2D.ActualHeight;

            double px1 = DesignerScroll.HorizontalOffset + view_w / 2;
            double rx1 = px1 / ZoomScale;

            double py1 = DesignerScroll.VerticalOffset + view_h / 2;
            double ry1 = py1 / ZoomScale;

            return new Point(rx1, ry1);
        }

        /*
         * ポインタのイベントの実位置を得る。
         */
        public Point GetPointerEventPositionReal(PointerRoutedEventArgs e) {
            Point pt = e.GetCurrentPoint(DesignerWin2D).Position;

            double rx = (DesignerScroll.HorizontalOffset + pt.X) / ZoomScale;
            double ry = (DesignerScroll.VerticalOffset   + pt.Y) / ZoomScale;

            return new Point(rx, ry);
        }

        /*
            ポインターのイベントハンドラのコルーチン
        */
        public IEnumerator PointerHandler(PointerRoutedEventArgs e) {
            Debug.Assert(PointerEventType == EEvent.PointerPressed);

            // ポインタのイベントの実位置を得る。
            Point down_pt = GetPointerEventPositionReal(e);
            Debug.WriteLine("down {0}", down_pt);

            TShape new_shape = new TShape(down_pt, new Point(0,0));
            ShapeList.Add(new_shape);

            yield return 0;

            while (true) {

                // ポインタのイベントの実位置を得る。
                Point pt = GetPointerEventPositionReal(e);

                Point pos = new Point(Math.Min(down_pt.X, pt.X), Math.Min(down_pt.Y, pt.Y));
                Size sz = new Size(Math.Abs(pt.X - down_pt.X), Math.Abs(pt.Y - down_pt.Y));

                new_shape.Bounds = new Rect(pos, sz);

                switch (PointerEventType) {
                case EEvent.PointerMoved:
                    Debug.WriteLine("move  {0}", pt);

                    // 再描画する。
                    InvalidateCanvas();

                    yield return 0;
                    break;

                case EEvent.PointerReleased:
                    if(sz.Width * sz.Height < 100) {

                        ShapeList.Remove(new_shape);
                    }

                    // 再描画する。
                    InvalidateCanvas();

                    Debug.WriteLine("release  {0}", pt);

                    yield break;

                default:
                    Debug.Assert(false);
                    break;
                }
            }
        }

        /*
            ポインタのイベントハンドラのコルーチンを継続する。
        */
        void ContinuePointerEventLoop(EEvent event_type, PointerRoutedEventArgs e) {
            if (PointerEventLoop != null) {
                //  ポインタのイベントハンドラの実行中の場合

                // イベントの型とイベントをセットする。
                PointerEventType = event_type;
                CurrentPointerEvent = e;

                // コルーチンを継続する。
                bool running = PointerEventLoop.MoveNext();

                if (!running) {

                    PointerEventLoop = null;
                }
            }
        }

        private void ToolBox_Click(object sender, RoutedEventArgs e) {
            Button btn1 = ToolBoxPanel.Children[ToolIdx] as Button;
            btn1.BorderBrush = new SolidColorBrush(Colors.Black);

            ToolIdx = ToolBoxPanel.Children.IndexOf(e.OriginalSource as UIElement);

            Button btn2 = ToolBoxPanel.Children[ToolIdx] as Button;
            btn2.BorderBrush = new SolidColorBrush(Colors.Blue);

            btn1.InvalidateArrange();
            btn2.UpdateLayout();

            ToolBoxPanel.UpdateLayout();

            Debug.WriteLine("tool box : {0}", e.OriginalSource);
        }
    }

    partial class TShape {
        public virtual void Draw(TGraphics gr) {
            gr.DrawRectangle(Bounds, ForeColor, StrokeWidth);
        }
    }

    public class TGraphics {
        CanvasDrawingSession CDS;

        public TGraphics(CanvasDrawingSession canvas_drawing_session, double zoom_scale, double offset_x, double offset_y) {
            CDS = canvas_drawing_session;
            CDS.Transform = Matrix3x2.Multiply(Matrix3x2.CreateScale((float)zoom_scale), Matrix3x2.CreateTranslation((float)offset_x, (float)offset_y));
        }

        public void DrawRectangle(Rect rect, Color color, float strokeWidth) {
            CDS.DrawRectangle(rect, color, strokeWidth);
        }

        public void DrawRectangle(float x, float y, float w, float h, Color color, float strokeWidth) {
            CDS.DrawRectangle(x, y, w, h, color, strokeWidth);
        }

        public void FillRectangle(float x, float y, float w, float h, Color color) {
            CDS.FillRectangle(x, y, w, h, color);
        }

        public void DrawText(string text, float x, float y, Color color, CanvasTextFormat format) {
            CDS.DrawText(text, x, y, color, format);
        }

        public void DrawLine(float x0, float y0, float x1, float y1, Color color, float strokeWidth) {
            CDS.DrawLine(x0, y0, x1, y1, color, strokeWidth);
        }

        public CanvasActiveLayer CreateLayer(float opacity, Rect clipRectangle) {
            return CDS.CreateLayer(opacity, clipRectangle);
        }
    }
}
