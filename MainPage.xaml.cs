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
        TProject MainProject;

        public MainPage() {
            this.InitializeComponent();

            Debug.WriteLine("メイン開始");

            MainProject = new TProject();
            TEnv.Project = MainProject;

            TParser.theParser = new TParser(MainProject);
            TCSharpParser.CSharpParser = new TCSharpParser(MainProject);
            TEnv.Parser = TCSharpParser.CSharpParser;

            MainProject.ClearProject();

            MainProject.SetAssemblyList();

            MainProject.OpenProject();

            MainProject.RegisterClassNames();

            Debug.WriteLine("解析開始");
            foreach (TSourceFile src in MainProject.SourceFiles) {

                src.Parser.ParseFile(src);

                string file_name = Path.GetFileName(src.PathSrc);

                if(file_name == "Sys.cs") {

                    MainProject.RegisterSysClass();
                }

                lst_SourceFiles.Items.Add(file_name);
            }
            foreach (TSourceFile src in MainProject.SourceFiles) {
                src.Parser.ResolveName(src);
            }
            Debug.WriteLine("メイン終了");
        }

        private void lst_SourceFiles_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) {
            int idx = lst_SourceFiles.SelectedIndex;

            if(0 <= idx && idx < MainProject.SourceFiles.Count) {
                TSourceFile src = MainProject.SourceFiles[idx];

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
