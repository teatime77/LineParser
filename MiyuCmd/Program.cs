using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Miyu {
    class Program {
        static void Main(string[] args) {
            TProject.MainClassName = "MkFn";
            TProject MainProject = new TProject();
            MainProject.Init();
            MainProject.Build();
        }
    }

    public class OutputWindow {
        public static OutputWindow theOutputWindow = null;
        public void AddOutputList(string s) { }
    }

    public class Color {
        public int R;
        public int G;
        public int B;
    }

    public class Colors {
        public static Color Black;
        public static Color Blue;
        public static Color Red;
        public static Color Green;
    }

    public struct TChar {
        // 文字
        public char Chr;

        // 下線
        public UnderlineType Underline;

        // 字句の型
        public ETokenType CharType;

        /*
            コンストラクタ
        */
        public TChar(char c) {
            Chr = c;
            Underline = UnderlineType.Undefined;
            CharType = ETokenType.Undefined;
        }
    }

    public class MyEditor {

    }

    public enum UnderlineType {
        Undefined
    }

}
