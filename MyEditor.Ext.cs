namespace Miyu{

    partial class MyEditor {
        ScrollViewer EditScroll;
        Canvas EditCanvas;
        CanvasControl Win2DCanvas;
        RadioButton OverlappedButton;
        public void InitializeComponent(){
        }
    }

    partial class FindReplace {
        public void InitializeComponent(){
        }
    }

    partial class Rectangle {
    }

    partial class WindowTool {
        Rectangle CornerHandle;
        public void InitializeComponent() {
        }
    }

    partial class OutputWindow {
        ListBox OutputList;
        public void InitializeComponent() {
        }
    }

    partial class ScrollViewer {
        // public bool ChangeView(Nullable<double> horizontalOffset, Nullable<double> verticalOffset, Nullable<float> zoomFactor, bool disableAnimation) {
        public bool ChangeView(double horizontalOffset, double verticalOffset, float zoomFactor, bool disableAnimation) {
            return true;
        }
    }
}
