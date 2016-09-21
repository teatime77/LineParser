using System.Diagnostics;

namespace Miyu {
public class TLog {
        public static void WriteLine(string format, params object[] args) {
            string s = string.Format(format, args);

            Debug.WriteLine(format, args);
            if(OutputWindow.theOutputWindow != null) {

                OutputWindow.theOutputWindow.AddOutputList(s);
            }
        }
    }
}