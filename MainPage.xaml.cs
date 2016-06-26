using MyEdit;
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

            prj.OpenProject();

            prj.RegisterClassNames();

            Debug.WriteLine("解析開始");
            foreach (TSourceFile src in prj.SourceFiles) {

                src.Parser.ParseFile(src);

                string file_name = Path.GetFileName(src.PathSrc);

                lst_SourceFiles.Items.Add(file_name);
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
    }
}
