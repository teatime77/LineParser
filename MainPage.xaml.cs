using MyEdit;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

// 空白ページのアイテム テンプレートについては、http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409 を参照してください

namespace LineParser {
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class MainPage : Page {
        public MainPage() {
            this.InitializeComponent();

            Debug.WriteLine("メイン開始");

            TProject prj = new TProject();

            TProject.Project = prj;

            TParser.theParser = new TParser(prj);
            TCSharpParser.CSharpParser = new TCSharpParser(prj);

            prj.ClearProject();

            prj.SetAssemblyList();

            prj.OpenProject();

            prj.RegisterClassNames();

            Debug.WriteLine("解析開始");
            foreach (TSourceFile src in prj.SourceFiles) {

                src.Parser.ParseFile(src);

                string file_name = Path.GetFileName(src.PathSrc);

                if(file_name == "Sys.cs") {

                    Dictionary<string, string> dic = new Dictionary<string, string>();

                    dic.Add("bool", "Boolean");
                    dic.Add("byte", "Byte");
                    dic.Add("char", "Char");
                    dic.Add("Dictionary", "Dictionary`2");
                    dic.Add("double", "Double");
                    dic.Add("float", "Single");
                    dic.Add("int", "Int32");
                    dic.Add("List", "List`1");
                    dic.Add("object", "Object");
                    dic.Add("short", "Int16");
                    dic.Add("string", "String");
                    dic.Add("void", "Void");

                    foreach (TType tp in prj.ClassTable.Values) {
                        string name;

                        if(! dic.TryGetValue(tp.ClassName, out name)) {
                            name = tp.ClassName;
                        }
                        var v = from a in prj.AssemblyList from tp2 in a.GetTypes() where tp2.Name == name select tp2;
                        if (v.Any()) {
                            tp.Info = v.First().GetTypeInfo();
                        }
                        else {
                            Debug.WriteLine("ERR sys class {0}", tp.ClassName, "");
                        }
                    }
                }

                lst_SourceFiles.Items.Add(file_name);
            }
            foreach (TSourceFile src in prj.SourceFiles) {
                src.Parser.ResolveName(src);
            }
            Debug.WriteLine("メイン終了");
        }

        private void lst_SourceFiles_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) {
            int idx = lst_SourceFiles.SelectedIndex;

            if(0 <= idx && idx < TProject.Project.SourceFiles.Count) {
                TSourceFile src = TProject.Project.SourceFiles[idx];

                MyEditor editor = LeftEditor;

                src.Parser = TCSharpParser.CSharpParser;

                editor.SetSource(src);
                editor.Focus(FocusState.Programmatic);
                editor.InvalidateCanvas();
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e) {
        }
    }
}
