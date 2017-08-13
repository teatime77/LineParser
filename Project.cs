#if !CMD
using Microsoft.Graphics.Canvas.UI.Xaml;
using Windows.Storage;
using Windows.UI;
#endif
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Miyu {

    //------------------------------------------------------------ TProject
    public partial class TProject  {
#if CMD
        public static string HomeDir = @"C:\usr\prj\mkfn";
#else
        public static string HomeDir = ApplicationData.Current.LocalFolder.Path;
#endif
        public static string MainClassName = "TProject";
        public static string OutputDir = HomeDir + "\\out";
        public static string WebDir = OutputDir + "\\web";
        public static string ClassesDir = WebDir + "\\class";
        public static bool InBuild;
        public static TToken ChangedToken;

        public static List<TVariable> CodeCompletionVariables = new List<TVariable>();
        public static int CodeCompletionOffset;
        public static int CodeCompletionIdx;
        public const  int CodeCompletionRows = 10;
        public static string CodeCompletionString = "";

        public List<TType> AppClasses;

        [_weak]
        public TType IntClass;
        public TType FloatClass;
        public TType DoubleClass;
        public TType CharClass;
        public TType ObjectClass;
        public TType StringClass;
        public TType BoolClass;
        public TType VoidClass;
        public TType TypeClass;
        public TType ActionClass;

        public TGenericClass ArrayClass;
        public TGenericClass EnumerableClass;
        public TType NullClass = new TType("null class");
        public TType HandlerClass = new TType("handler class");

        public List<TSourceFile> SourceFiles = new List<TSourceFile>();
        public TSourceFile SystemSourceFile;

        // 単純クラスの辞書
        public Dictionary<string, TType> SimpleClassTable = new Dictionary<string, TType>();

        // パラメータ化クラスの辞書
        public Dictionary<string, TGenericClass> ParameterizedClassTable = new Dictionary<string, TGenericClass>();

        // 特定化クラスの辞書
        public Dictionary<string, TGenericClass> SpecializedClassTable = new Dictionary<string, TGenericClass>();

        // TypeInfoの辞書
        public Dictionary<string, TType> TypeInfoTable = new Dictionary<string, TType>();

        public List<Assembly> AssemblyList = new List<Assembly>();

        public Dictionary<string, string> TypeAliasTable = new Dictionary<string, string>();

        public ManualResetEvent Modified;
        public bool ParseDone;

        public TCallNode MainCall;

        public TProject() {
        }

        public void Main() {
            Init();

            Task.Run(() => {
                Build();
            });
        }

        public void Init() {
            TGlb.Project = this;

            TCSharpParser.CSharpParser = new TCSharpParser(this);
            TGlb.Parser = TCSharpParser.CSharpParser;

            SetAssemblyList();

            OpenProject();
        }

        /*
        フィールドの弱参照(IsWeak)を設定する。
        弱参照のフィールドの後ろにあるフィールドは弱参照とする。
        */
        public void SetWeakField() {
            foreach (TType c in AppClasses) {
                bool weak_field_is_found = false;

                foreach (TField fld in c.Fields) {
                    if (!weak_field_is_found) {
                        // 弱参照のフィールドが前にない場合

                        if (fld.Attributes != null && (from a in fld.Attributes where a.Attr.ClassName == "_weak" select a).Any()) {
                            // 弱参照のフィールドの場合

                            weak_field_is_found = true;
                            fld.IsWeak = true;
                        }
                    }
                    else {
                        // 弱参照のフィールドの後ろにある場合

                        fld.IsWeak = true;
                    }
                }
            }
        }

        /*
         * プロジェクトをビルドする。
         */
        public void Build() {
            DateTime tick;

            TGlb.Project = this;
            TGlb.Parser = TCSharpParser.CSharpParser;
            ParseDone = false;

            // System.csの構文解析が終わった時点での単純クラス,パラメータ化クラス,特定化クラスの辞書。
            Dictionary<string, TType> simple_class_table_save = null;
            Dictionary<string, TGenericClass> parameterized_class_table_save = null;
            Dictionary<string, TGenericClass> specialized_class_table_save = null;
            Dictionary<string, TType> type_info_table_save = null;

            tick = DateTime.Now;

            bool is_first = true;
            bool output_result = true;
            while (true) {
                do_again:

                // ソースの変更を待つ。
                Modified.WaitOne();
                InBuild = true;
                Modified.Reset();

                if (is_first) {
                    is_first = false;

                    // System.csの行のリストを得る。
                    SystemSourceFile.Lines = (from x in SystemSourceFile.EditLines select new TLine(x)).ToList();

                    // 型宣言の字句(class, struct, enum, interface, delegate)の直後の識別子は型名とする。
                    RegisterClassNames();

                    TGlb.SourceFile = SystemSourceFile;

                    // System.csの構文解析をする。
#if CMD
                    SystemSourceFile.Parser.ParseFile(SystemSourceFile);
#else
                    SystemSourceFile.Parser.LineParseFile(SystemSourceFile);
#endif

                    // 型の別名の辞書をセットする。
                    SetTypeAliasTable();

                    // System.csのクラスのTypeInfoをセットする。
                    SetSystemSourceFileTypeInfo();

                    // System.csの構文解析が終わった時点での単純クラス,パラメータ化クラス,特定化クラスの辞書を保存する。
                    simple_class_table_save         = new Dictionary<string, TType>(SimpleClassTable);
                    parameterized_class_table_save  = new Dictionary<string, TGenericClass>(ParameterizedClassTable);
                    specialized_class_table_save    = new Dictionary<string, TGenericClass>(SpecializedClassTable);
                    type_info_table_save            = new Dictionary<string, TType>(TypeInfoTable);
                }

                try {
                    TLog.WriteLine("解析開始");

                    // System.csの構文解析が終わった時点での単純クラス,パラメータ化クラス,特定化クラスの辞書を復元する。
                    SimpleClassTable        = new Dictionary<string, TType>(simple_class_table_save);
                    ParameterizedClassTable = new Dictionary<string, TGenericClass>(parameterized_class_table_save);
                    SpecializedClassTable   = new Dictionary<string, TGenericClass>(specialized_class_table_save);
                    TypeInfoTable           = new Dictionary<string, TType>(type_info_table_save);

                    ParseDone = false;

                    // すべてのソースファイルに対し
                    foreach (TSourceFile src in SourceFiles) {
                        if(src != SystemSourceFile) {
                            // System.csでない場合

                            lock (src) {
                                // ソースファイルをロックする。

                                // 行のリストを得る。
                                src.Lines = src.EditLines;
                                src.EditLines = (from x in src.Lines select new TLine(x)).ToList();
                            }

                            // 字句のエラーをクリアする。
                            foreach(TLine line in src.Lines) {
                                foreach(TToken tkn in line.Tokens) {
                                    tkn.ErrorTkn = null;
                                }
                            }
                        }
                    }

                    // コード補完の候補リストをクリアする。
                    lock (TProject.CodeCompletionVariables) {
                        TProject.CodeCompletionVariables.Clear();
                    }

                    // 型宣言の字句(class, struct, enum, interface, delegate)の直後の識別子は型名とする。
                    RegisterClassNames();

                    tick = DateTime.Now;
                    TLog.WriteLine("型名登録 終了 {0}", DateTime.Now.Subtract(tick).TotalMilliseconds);
                    tick = DateTime.Now;

                    foreach (TSourceFile src in SourceFiles) {
                        if (src != SystemSourceFile) {
                            // System.csでない場合

                            // ソースファイルの構文解析をする。
                            TGlb.SourceFile = src;

#if CMD
                            src.Parser.ParseFile(src);
#else
                            src.Parser.LineParseFile(src);
#endif
                            TGlb.SourceFile = null;
                        }

                        if (Modified.WaitOne(0)) {
                            // ソースが変更された場合

                            goto do_again;
                        }
                    }

                    TLog.WriteLine("解析終了 {0}", DateTime.Now.Subtract(tick).TotalMilliseconds);
                    tick = DateTime.Now;

                    TSetParentNavi set_parent = new TSetParentNavi();
                    List<object> args = new List<object>();
                    args.Add(null);
                    set_parent.ProjectNavi(this, args);

                    ParseDone = true;

                    // 特定化がまだの特定化クラスのリスト
                    var vc = (from c in SpecializedClassTable.Values where !c.SetMember select c).ToList();
                    if (vc.Any()) {

                        foreach (TGenericClass gen in vc) {
                            // 特定化クラスの中の型の中に仮引数クラスがあれば実引数の型を割り当てる。
                            AssignParameterInSpecializedClass(gen);
                        }
                    }

                    // 特定化がまだの特定化クラスはないはず。
                    Debug.Assert(!(from c in SpecializedClassTable.Values where !c.SetMember select c).Any());

                    // アプリのクラスのリスト
                    AppClasses = (from x in SimpleClassTable.Values where x.Info == null select x).ToList();

                    // アプリのクラスはSystem.cs以外のソースファイルで定義されているはず。
                    var apps = from x in AppClasses where x.SourceFileCls == null || x.SourceFileCls == SystemSourceFile select x;
                    if (apps.Any()) {
                        Debug.WriteLine("");
                    }
                    Debug.Assert( ! (from x in AppClasses where x.SourceFileCls == null || x.SourceFileCls == SystemSourceFile select x).Any());

                    // アプリのクラスの親クラスに対し
                    foreach (TType cls in AppClasses) {
                        foreach (TType spr in cls.SuperClasses) {

                            // 子クラスのリストをセットする。
                            if (!spr.SubClasses.Contains(cls)) {
                                spr.SubClasses.Add(cls);
                            }
                        }
                    }

                    // フィールドの弱参照(IsWeak)を設定する。
                    SetWeakField();

                    foreach (TSourceFile src in SourceFiles) {
                        src.Parser.SourceFileResolveName(src);
                    }
                    TLog.WriteLine("名前解決 終了 {0}", DateTime.Now.Subtract(tick).TotalMilliseconds);

                    // 単純クラスの辞書のチェック
                    foreach (TType c in SimpleClassTable.Values) {
                        Debug.Assert(c.GenericType == EClass.SimpleClass && !(c is TGenericClass));
                    }

                    // パラメータ化クラスの辞書のチェック
                    foreach (TType c in ParameterizedClassTable.Values) {
                        Debug.Assert(c.GenericType == EClass.ParameterizedClass);
                    }

                    // 特定化クラスの辞書のチェック
                    foreach (TType c in SpecializedClassTable.Values) {
                        Debug.Assert(c.GenericType == EClass.SpecializedClass);
                    }

//                    DeepLearning();

                    if (output_result) {
                        // 最初の場合

                        output_result = false;

                        tick = DateTime.Now;

                        // HTMLを出力するディレクトリを作る。
                        string html_dir = OutputDir + "\\html";
                        if (!Directory.Exists(OutputDir)) {
                            Directory.CreateDirectory(OutputDir);
                        }
                        if (!Directory.Exists(WebDir)) {
                            Directory.CreateDirectory(WebDir);
                        }
                        if (!Directory.Exists(html_dir)) {
                            Directory.CreateDirectory(html_dir);
                        }

                        // クラス図を作る。
                        MakeClassDiagram();

                        TLog.WriteLine("クラス図 終了 {0}", DateTime.Now.Subtract(tick).TotalMilliseconds);
                        tick = DateTime.Now;

                        // 値を代入している変数参照のDefinedをtrueにする。
                        TSetDefined set_defined = new TSetDefined();
                        set_defined.ProjectNavi(this, null);

                        // すべてのソースファイルに対し
                        foreach (TSourceFile src in SourceFiles) {
                            TTokenWriter tw = new TTokenWriter(src.Parser);
                            src.Parser.SourceFileText(src, tw);

                            string dir_path = Path.GetDirectoryName(src.PathSrc);
                            string fname;
                            if (dir_path == "") {

                                fname = Path.GetFileNameWithoutExtension(src.PathSrc);
                            }
                            else {

                                fname = dir_path + "\\" + Path.GetFileNameWithoutExtension(src.PathSrc);
                            }

                            FileWriteAllText(OutputDir + "\\" + fname + ".cs", tw.ToPlainText());
                            FileWriteAllText(html_dir + "\\" + fname + ".html", tw.ToHTMLText(fname));
                        }

                        // HTMLのソースコードを作る。
                        MakeHTMLSourceCode();

                        TLog.WriteLine("ソース生成 終了 {0}", DateTime.Now.Subtract(tick).TotalMilliseconds);
                        tick = DateTime.Now;

                        // 使用・定義連鎖を作る。
                        MakeUseDefineChain();

                        TLog.WriteLine("使用・定義連鎖 終了 {0}", DateTime.Now.Subtract(tick).TotalMilliseconds);
                        tick = DateTime.Now;

                        // 要約を作る。
                        MakeSummary();

                        TLog.WriteLine("要約 終了 {0}", DateTime.Now.Subtract(tick).TotalMilliseconds);
                    }
                }
                catch (TBuildCancel) {
                }

                InBuild = false;

#if !CMD
                // メインページを再描画する。
                MainPage.theMainPage.InvalidateMainPage();
#endif
            }
        }

        /*
         * アセンブリのリストを作る。
         */
        public void SetAssemblyList() {
#if !CMD
            AssemblyList.Add(typeof(ApplicationData).GetTypeInfo().Assembly);
#endif
            AssemblyList.Add(typeof(Assembly).GetTypeInfo().Assembly);
#if !CMD
            AssemblyList.Add(typeof(CanvasControl).GetTypeInfo().Assembly);
#endif
            AssemblyList.Add(typeof(Color).GetTypeInfo().Assembly);
            AssemblyList.Add(typeof(Debug).GetTypeInfo().Assembly);
            AssemblyList.Add(typeof(File).GetTypeInfo().Assembly);
            AssemblyList.Add(typeof(Stack<int>).GetTypeInfo().Assembly);
#if !CMD
            AssemblyList.Add(typeof(MainPage).GetTypeInfo().Assembly);
#endif
            AssemblyList.Add(typeof(WebUtility).GetTypeInfo().Assembly);
        }

        public void OpenProject() {
            SourceFiles = (from file_name in File.ReadAllLines(HomeDir + @"\ProjectFiles.txt", Encoding.UTF8)
                           select new TSourceFile(file_name, TCSharpParser.CSharpParser)).ToList();

            SystemSourceFile = (from src in SourceFiles where Path.GetFileName(src.PathSrc) == "System.cs" select src).First();

            Debug.WriteLine("ソースの変更信号");
            Modified = new ManualResetEvent(true);
        }

        /*
         * TypeInfoに対応する型を得る。無ければ新たに型を作る。
         */
        public TType RegisterTypeInfoTable(TypeInfo inf) {
            TType tp2;

            if(TypeInfoTable.TryGetValue(inf.FullName, out tp2)) {
                // 辞書にある場合

                return tp2;
            }
            else {
                // 辞書にない場合

                // 新たに型を作る。
                tp2 = new TType(inf);

                // 辞書に登録する。
                TypeInfoTable.Add(inf.FullName, tp2);

                return tp2;
            }
        }

        /*
         * 型のTypeInfoをセットする。
         */
        public void SetTypeInfo(TType tp) {
            if (tp.TypeInfoSearched) {
                // 処理済みの場合

                return;
            }
            tp.TypeInfoSearched = true;

            string sys_name;

            if (! TypeAliasTable.TryGetValue(tp.ClassName, out sys_name)) {

                sys_name = tp.ClassName;
            }

            // アセンブリのリストの中で同じ名前のTypeを探す。
            var v = from a in AssemblyList from tp2 in a.GetTypes() where tp2.Name == sys_name select tp2;
            if (v.Any()) {
                // 同じ名前のTypeがある場合

                if (v.Count() == 1) {
                    // 同じ名前のTypeが1個の場合

                    tp.Info = v.First().GetTypeInfo();
                }
                else {
                    // 同じ名前のTypeが複数ある場合

                    foreach (Type t in v) {
                        Debug.WriteLine("あいまいな型 : {0}", t.FullName, "");
                    }
                    if(sys_name == "TimeSpan" || sys_name == "Path" || sys_name== "DateTime") {

                        tp.Info = v.Last().GetTypeInfo();
                    }
                    else {

                        tp.Info = v.First().GetTypeInfo();
                    }
                }
                TypeInfoTable.Add(tp.Info.FullName, tp);
            }
        }

        /*
         * 型の別名の辞書をセットする。
         */
        public void SetTypeAliasTable() {
            TypeAliasTable.Add("bool", "Boolean");
            TypeAliasTable.Add("byte", "Byte");
            TypeAliasTable.Add("char", "Char");
            TypeAliasTable.Add("Dictionary", "Dictionary`2");
            TypeAliasTable.Add("double", "Double");
            TypeAliasTable.Add("float", "Single");
            TypeAliasTable.Add("IEnumerable", "IEnumerable`1");
            TypeAliasTable.Add("int", "Int32");
            TypeAliasTable.Add("List", "List`1");
            TypeAliasTable.Add("object", "Object");
            TypeAliasTable.Add("short", "Int16");
            TypeAliasTable.Add("string", "String");
            TypeAliasTable.Add("void", "Void");
            TypeAliasTable.Add("ThreadStatic", "ThreadStaticAttribute");
        }

        /*
         * System.csのクラスのTypeInfoをセットする。
         */
        public void SetSystemSourceFileTypeInfo() {
            // すべての単純クラスに対し
            foreach (TType tp in SimpleClassTable.Values) {
                // 型のTypeInfoをセットする。
                SetTypeInfo(tp);
                if (tp.Info == null) {
                    // TypeInfoがない場合

                    throw new TBuildException("Unknown system class : " + tp.ClassName);
                }
            }
        }

        /*
         * 型宣言の字句(class, struct, enum, interface, delegate)の直後の識別子は型名とする。
         */
        public void RegisterClassNames() {
            List<string> type_names = new List<string>();

            // すべてのソースファイルに対し
            foreach(TSourceFile src in SourceFiles) {
                // ソースファイル内のすべての行に対し
                foreach (TLine line in src.Lines) {

                    // 型宣言の字句(class, struct, enum, interface, delegate)の位置を得る。
                    int idx = new List<TToken>(line.Tokens).FindIndex(x => x.Kind == EKind.class_ || x.Kind == EKind.struct_ || x.Kind == EKind.enum_ || x.Kind == EKind.interface_ || x.Kind == EKind.delegate_);
                    if (idx != -1) {
                        // 型宣言の字句がある場合

                        if (idx + 1 < line.Tokens.Length && (line.Tokens[idx + 1].Kind == EKind.Identifier || line.Tokens[idx + 1].Kind == EKind.ClassName)) {
                            // 型宣言の字句の直後が識別子か型名の場合

                            // 型名を得る。
                            string name = line.Tokens[idx + 1].TextTkn;
                            if (!type_names.Contains(name)) {
                                // 型名のリストに含まれない場合

                                //Debug.WriteLine("typeof({0}),", name, "");
                                // 型名のリストに追加する。
                                type_names.Add(name);
                            }
                        }
                        else if(line.Tokens[idx].Kind != EKind.delegate_) {

                            Debug.WriteLine("class syntax error:{0}", line.TextLine,"");
                        }
                    }
                }
            }

            foreach (TSourceFile src in SourceFiles) {
                // 名前が型名のリストに含まれる識別子のリスト
                var vtkn = from line in src.Lines
                            from tkn in line.Tokens
                            where tkn.Kind == EKind.Identifier && type_names.Contains(tkn.TextTkn)
                            select tkn;

                foreach (TToken tkn in vtkn) {
                    // 字句の種類はClassNameにする。
                    tkn.Kind = EKind.ClassName;
                }
            }
        }

        /*
         * 名前からクラスを得る。登録済みのクラスが無ければ新たに作る。
         */
        public TType GetClassByName(string name, bool in_generic_id = false) {
            TType cls;
            TGenericClass gen_class;

            if (SimpleClassTable.TryGetValue(name, out cls)) {
                // 単純クラスの辞書にある場合

                return cls;
            }
            else if (ParameterizedClassTable.TryGetValue(name, out gen_class)) {
                // パラメータ化クラスの辞書にある場合

                return gen_class;
            }
            else {
                if (TParser.CurrentClass is TGenericClass) {
                    // 総称型のクラス定義の中の場合

                    TGenericClass gen = TParser.CurrentClass as TGenericClass;

                    // 名前が一致する仮引数クラスを得る。
                    var v = from c in gen.ArgClasses where c.ClassName == name select c;
                    if (v.Any()) {
                        // 名前が一致する仮引数クラスがある場合

                        return v.First();
                    }
                }

                var vc = from c in TParser.ParameterClasses where c.ClassName == name select c;
                if (vc.Any()) {
                    // 名前が一致する仮引数クラスがある場合

                    return vc.First();
                }

                if (in_generic_id) {

                    TType param_class = new TType(name);
                    param_class.GenericType = EClass.ParameterClass;

                    TParser.ParameterClasses.Add(param_class);

                    return param_class;
                }

                // 登録済みのクラスが無ければ単純クラスを新たに作る。
                cls = new TType(name);

                // 単純クラスの辞書に追加する。
                SimpleClassTable.Add(name, cls);

                return cls;
            }
        }

        public string MakeClassText(string class_name, List<TType> param_classes, int dim_cnt) {
            StringWriter sw = new StringWriter();

            sw.Write(class_name);

            sw.Write("<");
            foreach (TType c in param_classes) {
                if (c != param_classes[0]) {
                    sw.Write(",");
                }
                sw.Write("{0}", c.GetClassText());
            }
            sw.Write(">");

            if (dim_cnt != 0) {
                // 配列の場合

                // 配列の次元-1個のカンマを[]で囲む。
                sw.Write("[{0}]", new string(',', dim_cnt - 1));
            }

            return sw.ToString();
        }

        /*
         * パラメータ化クラスを得る。無ければ新たに作る。
         */
        public TGenericClass GetParameterizedClass(string class_name, List<TType> param_classes) {
            TGenericClass reg_class;

            if (! ParameterizedClassTable.TryGetValue(class_name, out reg_class)) {
                // パラメータ化クラスの辞書にない場合

                // パラメータ化クラスを新たに作る。
                reg_class = new TGenericClass(class_name, param_classes);
                reg_class.GenericType = EClass.ParameterizedClass;

                // パラメータ化クラスの辞書に追加する。
                ParameterizedClassTable.Add(class_name, reg_class);
            }

            return reg_class;
        }

        /*
         * 特定化クラスを得る。無ければ新たに作る。
         */
        public TGenericClass GetSpecializedClass(TGenericClass org_class, List<TType> arg_classes, int dim_cnt) {
            string class_text = MakeClassText(org_class.ClassName, arg_classes, dim_cnt);

            TGenericClass reg_class;
            if (!SpecializedClassTable.TryGetValue(class_text, out reg_class)) {
                // 特定化クラスの辞書にない場合

                // 特定化クラスを新たに作る。
                reg_class = new TGenericClass(org_class, arg_classes, dim_cnt);
                reg_class.GenericType = EClass.SpecializedClass;

                if (ParseDone) {
                    // 構文解析が終わった場合

                    // 特定化クラスの中の型の中に仮引数クラスがあれば実引数の型を割り当てる。
                    AssignParameterInSpecializedClass(reg_class);
                }
                else {
                    // 構文解析の途中の場合

                    // 特定化クラスの辞書に追加する。構文解析の後で型の中に仮引数クラスがあれば実引数の型を割り当てる。
                    SpecializedClassTable.Add(class_text, reg_class);
                }
            }

            return reg_class;
        }

        /*
         * 型の中に仮引数クラスがあれば実引数の型を割り当てる。
         */
        public TType AssignParameterClass(TType tp, Dictionary<string, TType> dic) {
            if(!(tp is TGenericClass)) {

                TType tp2;

                if(dic.TryGetValue(tp.ClassName, out tp2)) {

                    Debug.Assert(tp.GenericType == EClass.ParameterClass);
                    return tp2;
                }
                else {

                    Debug.Assert(tp.GenericType == EClass.SimpleClass);
                    return tp;
                }
            }

            TGenericClass gen = tp as TGenericClass;
            Debug.Assert(gen.GenericType == EClass.SpecializedClass || gen.GenericType == EClass.UnspecializedClass);
            bool changed = false;

            List<TType> vtp = new List<TType>();
            foreach(TType tp2 in gen.ArgClasses) {
                // 型の中に仮引数クラスがあれば実引数の型を割り当てる。
                TType tp3 = AssignParameterClass(tp2, dic);
                if(tp3 != tp2) {
                    changed = true;
                }
                vtp.Add(tp3);
            }

            if(changed) {

                Debug.Assert(gen.GenericType == EClass.UnspecializedClass);
                return GetSpecializedClass(gen.OrgCla, vtp, gen.DimCnt);
            }
            else {

                Debug.Assert(gen.GenericType == EClass.SpecializedClass);
                return tp;
            }
        }

        /*
         * 特定化クラスの変数を作る。
         */
        public TVariable MakeSpecializedClassVariable(TVariable var_src, Dictionary<string, TType> dic) {
            // 変数の型の中に仮引数クラスがあれば実引数の型を割り当てる。
            TType tp = AssignParameterClass(var_src.TypeVar, dic);
            TVariable var_dst = new TVariable(var_src.ModifierVar, var_src.TokenVar, tp, null);

            return var_dst;
        }

        /*
         * 特定化クラスのフィールドを作る。
         */
        public TField MakeSpecializedClassField(TType declaring_type, TField fld_src, Dictionary<string, TType> dic) {
            // フィールドの型の中に仮引数クラスがあれば実引数の型を割り当てる。
            TType tp = AssignParameterClass(fld_src.TypeVar, dic);
            TField fld_dst = new TField(declaring_type, fld_src.ModifierVar, fld_src.TokenVar, tp, null);

            return fld_dst;
        }

        /*
         * 特定化クラスの関数宣言を作る。
         */
        public TFunction MakeSpecializedClassFunctionDeclaration(TType declaring_type, TFunction fnc_src, Dictionary<string, TType> dic) {
            // 特定化クラスの変数を作る。
            TVariable[] args = (from x in fnc_src.ArgsFnc select MakeSpecializedClassVariable(x, dic)).ToArray();

            // 戻り値の型の中に仮引数クラスがあれば実引数の型を割り当てる。
            TType ret_type = AssignParameterClass(fnc_src.TypeVar, dic);

            TFunction fnc_dst = new TFunction(fnc_src.ModifierVar, fnc_src.TokenVar, args, ret_type, null, fnc_src.KindFnc);
            fnc_dst.DeclaringType = declaring_type;

            return fnc_dst;
        }

        /*
         * 特定化クラスの中の型の中に仮引数クラスがあれば実引数の型を割り当てる。
         */
        public void AssignParameterInSpecializedClass(TGenericClass cls) {
            string class_text = cls.GetClassText();

            if (cls.SetMember) {
                // 処理済みの場合

                return;
            }
            cls.SetMember = true;

            if (! SpecializedClassTable.ContainsKey(class_text)) {
                // 特定化クラスの辞書にない場合

                // 特定化クラスの辞書に追加する
                SpecializedClassTable.Add(class_text, cls);
            }

            if(cls.ArgClasses == null || cls.ArgClasses.Count != cls.OrgCla.ArgClasses.Count) {
                throw new TBuildException("Assign Parameter In Specialized Class Error: " + cls.ClassName);
            }

            // 仮引数クラスから実引数の型への変換辞書を作る。
            Dictionary<string, TType> dic = new Dictionary<string, TType>();
            for (int i = 0; i < cls.OrgCla.ArgClasses.Count; i++) {
                dic.Add(cls.OrgCla.ArgClasses[i].ClassName, cls.ArgClasses[i]);
            }

            for (int i = 0; i < cls.ArgClasses.Count; i++) {
                // 型の中に仮引数クラスがあれば実引数の型を割り当てる。
                cls.ArgClasses[i] = AssignParameterClass(cls.ArgClasses[i], dic);
            }

            cls.KindClass = cls.OrgCla.KindClass;

            // 親クラスのリストの中に仮引数クラスがあれば実引数の型を割り当てる。
            cls.SuperClasses = (from c in cls.OrgCla.SuperClasses select AssignParameterClass(c, dic)).ToList();

            Debug.Assert((cls.OrgCla.RetType != null) == (cls.OrgCla.KindClass == EType.Delegate_));
            if (cls.OrgCla.RetType != null) {
                // デリゲートの場合

                // デリゲートの戻り値の型の中に仮引数クラスがあれば実引数の型を割り当てる。
                cls.RetType = AssignParameterClass(cls.OrgCla.RetType, dic);

                // デリゲートの引数の型のリストの中に仮引数クラスがあれば実引数の型を割り当てる。
                cls.ArgTypes = (from c in cls.OrgCla.ArgTypes select AssignParameterClass(c, dic)).ToArray();
            }

            // 特定化クラスのフィールドを作る。
            cls.Fields = (from x in cls.OrgCla.Fields select MakeSpecializedClassField(cls, x, dic)).ToList();

            // 特定化クラスの関数宣言を作る。
            cls.Functions = (from x in cls.OrgCla.Functions select MakeSpecializedClassFunctionDeclaration(cls, x, dic)).ToList();
        }
    }
}
