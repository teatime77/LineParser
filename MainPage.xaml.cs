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

namespace LineParser
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();

            TProject prj = new TProject();

            TProject.Project = prj;

            TParser.theParser = new TParser(prj);
            TCSharpParser.CSharpParser = new TCSharpParser(prj);

            prj.OpenProject();

            for(int i = 0; i < prj.SourceFiles.Count; i++) {
                TSourceFile src = prj.SourceFiles[i];

                Pivot pivot;

                if(i < prj.SourceFiles.Count / 2) {

                    pivot = LeftEditors;
                }
                else {

                    pivot = RightEditors;
                }

                PivotItem item = new PivotItem();
                item.Header = Path.GetFileName(src.PathSrc);

                MyEditor editor = new MyEditor();

                src.Parser = TCSharpParser.CSharpParser;

                item.Content = editor;

                pivot.Items.Add(item);

                editor.SetSource(src);
            }
        }
    }
}
