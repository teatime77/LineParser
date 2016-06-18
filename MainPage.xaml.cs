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

            Debug.Assert(Editor1 != null && Editor1.SourceFile != null);

            TProject.Project = new TProject();

            Editor1.SourceFile.Parser = new TParser(TProject.Project);
            Editor2.SourceFile.Parser = Editor1.SourceFile.Parser;
            Editor3.SourceFile.Parser = Editor1.SourceFile.Parser;
            Editor4.SourceFile.Parser = Editor1.SourceFile.Parser;

            TProject.Project.SourceFiles.Add(Editor1.SourceFile);
            TProject.Project.SourceFiles.Add(Editor2.SourceFile);
            TProject.Project.SourceFiles.Add(Editor3.SourceFile);
            TProject.Project.SourceFiles.Add(Editor4.SourceFile);
        }
    }
}
