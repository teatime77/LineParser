using MyEdit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Reflection;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Text.Core;
using Windows.Storage;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.Graphics.Canvas.Text;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.ApplicationModel;
using System.Collections;
using Windows.Globalization;
using Windows.Foundation;
using Windows.UI.Input;
using Windows.UI.Text;
using Windows.System;

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

        private void Page_Loaded(object sender, RoutedEventArgs e) {
            Type[] type_list = new Type[] {
                typeof(ApplicationData),
                typeof(bool),
                typeof(byte),
                typeof(CanvasControl),
                typeof(CanvasDrawEventArgs),
                typeof(CanvasTextFormat),
                typeof(CanvasTextLayout),
                typeof(char),
                typeof(Clipboard),
                typeof(Colors),
                typeof(Color),
                typeof(CoreApplication),
                typeof(CoreCursor),
                typeof(CoreCursorType),
                typeof(CoreTextCompositionCompletedEventArgs),
                typeof(CoreTextCompositionSegment),
                typeof(CoreTextCompositionStartedEventArgs),
                typeof(CoreTextEditContext),
                typeof(CoreTextFormatUpdatingEventArgs),
                typeof(CoreTextLayoutRequestedEventArgs),
                typeof(CoreTextRange),
                typeof(CoreTextSelectionRequestedEventArgs),
                typeof(CoreTextSelectionUpdatingEventArgs),
                typeof(CoreTextServicesManager),
                typeof(CoreTextTextRequestedEventArgs),
                typeof(CoreTextTextUpdatingEventArgs),
                typeof(CoreVirtualKeyStates),
                typeof(CoreWindow),
                typeof(DataPackage),
                typeof(DataPackageOperation),
                typeof(DataPackageView),
                typeof(Debug),
                typeof(DesignMode),
                typeof(Dictionary<,>),
                typeof(DispatcherTimer),
                typeof(double),
                typeof(Exception),
                typeof(File),
                typeof(float),
                typeof(Flyout),
                typeof(FocusState),
                typeof(FrameworkElement),
                typeof(IEnumerator),
                typeof(int),
                typeof(Language),
                typeof(List<>),
                typeof(Math),
                typeof(object),
                typeof(Point),
                typeof(PointerEventArgs),
                typeof(PointerPoint),
                typeof(PointerRoutedEventArgs),
                typeof(Rect),
                typeof(RoutedEventArgs),
                typeof(ScrollViewerViewChangedEventArgs),
                typeof(short),
                typeof(Size),
                typeof(Stack),
                typeof(StandardDataFormats),
                typeof(StorageFolder),
                typeof(string),
                typeof(StringWriter),
                typeof(TimeSpan),
                typeof(UnderlineType),
                typeof(UserControl),
                typeof(VirtualKey),
                typeof(void),
                typeof(Window),
                typeof(MyEditor),
                typeof(EEvent),
                typeof(TChar),
                typeof(TDiff),
                typeof(TShape),
                typeof(TParser),
                typeof(TToken),
                typeof(TLine),
                typeof(TParseException),
                typeof(TResolveNameException),
                typeof(ETokenType),
                typeof(TVariable),
                typeof(TFunction),
                typeof(TTerm),
                typeof(TLiteral),
                typeof(TReference),
                typeof(TApply),
                typeof(TQuery),
                typeof(TStatement),
                typeof(TAssignment),
                typeof(TCall),
                typeof(TVariableDeclaration),
                typeof(TBlockStatement),
                typeof(TBlock),
                typeof(TIfBlock),
                typeof(TIf),
                typeof(TCase),
                typeof(TSwitch),
                typeof(TTry),
                typeof(TCatch),
                typeof(TWhile),
                typeof(TForEach),
                typeof(TFor),
                typeof(TJump),
                typeof(TClass),
                typeof(TProject),
                typeof(TCSharpParser),
                typeof(EKind),
                typeof(MainPage),
                typeof(TNavigation),
                typeof(TResolveNameNavi),
                typeof(EClass),
                typeof(EGeneric),
                typeof(EAggregateFunction),
                typeof(TUsing),
                typeof(TNamespace),
                typeof(TGenericClass),
                typeof(TMember),
                typeof(TField),
                typeof(TDotReference),
                typeof(TDotApply),
                typeof(TNewApply),
                typeof(TFrom),
                typeof(TAggregate),
                typeof(TAbsFor),
                typeof(TLabelStatement),
                typeof(TAttribute),
                typeof(TSourceFile),
            };

            string[] ass_name_list = new string[] {
                "Windows, Version=255.255.255.255, Culture=neutral, PublicKeyToken=null, ContentType=WindowsRuntime",
//                "mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e",
                "Microsoft.Graphics.Canvas, Version=255.255.255.255, Culture=neutral, PublicKeyToken=null, ContentType=WindowsRuntime",
                "System.Runtime.WindowsRuntime, Version=4.0.10.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
                "System.Diagnostics.Debug, Version=4.0.10.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
                "System.IO.FileSystem, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
                "System.Collections.NonGeneric, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
                "LineParser, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
            };

            Debug.WriteLine("------------------------------------------------------------------------------------");
            List<Assembly> va = new List<Assembly>();
            foreach(Type t in type_list) {
                Assembly a = t.GetTypeInfo().Assembly;
//                Debug.WriteLine("{0} {1}", t.Name, a.FullName);
                if(!va.Contains(a)) {
                    va.Add(a);
                    Debug.WriteLine("\"{0}\",", a.FullName,"");
                }
            }
            Debug.WriteLine("------------------------------------------------------------------------------------");

            List<Type> vt = new List<Type>(type_list);
            foreach(Assembly ass0 in va) {//string ass_name in ass_name_list
                //Assembly ass0 = Assembly.Load(new AssemblyName(ass_name));

                foreach (Type t in ass0.GetTypes()) {
                    if (vt.Contains(t)) {
                        TypeInfo ti = t.GetTypeInfo();
                        Debug.WriteLine("type info {0}", ti.Name, "");
                    }
                }
            }

            Assembly ass1 = typeof(File).GetTypeInfo().Assembly;// CoreTextServicesManager
            ass1 = Assembly.Load(new AssemblyName("System.IO.FileSystem, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"));
            foreach (Type t in ass1.GetTypes()) {
                Debug.WriteLine("get type {0}", t.Name, "");
                if(t.Name == "File") {
                    TypeInfo ti = t.GetTypeInfo();
                }
            }

/*
            AssemblyName an = new AssemblyName("Windows.UI.Text.Core");
            Assembly ass = Assembly.Load(an);
            foreach (TypeInfo ti in ass.DefinedTypes) {
                Debug.WriteLine("type {0}", ti.Name, "");
            }
            foreach (Type t in ass.ExportedTypes) {
                Debug.WriteLine("exported type {0}", t.Name, "");
            }
            foreach (Type t in ass.GetTypes()) {
                Debug.WriteLine("get type {0}", t.Name, "");
            }
*/

            //string @namespace = "System.IO";

            //var q = from t in Assembly.GetExecutingAssembly().GetTypes()
            //        where t.IsClass && t.Namespace == @namespace
            //        select t;
            //q.ToList().ForEach(t => Debug.WriteLine("", t.Name, ""));

        }
    }
}
