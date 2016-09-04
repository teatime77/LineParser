using Microsoft.Graphics.Canvas.UI.Xaml;
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
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;

namespace Miyu {

    //------------------------------------------------------------ TProject
    public partial class TProject  {
        public static string OutputDir = ApplicationData.Current.LocalFolder.Path + "\\out";
        public static string WebDir = OutputDir + "\\web";
        public static string ClassesDir = WebDir + "\\class";

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
        public Dictionary<string, TType> ClassTable = new Dictionary<string, TType>();
        public Dictionary<string, TGenericClass> ParameterizedClassTable = new Dictionary<string, TGenericClass>();
        public Dictionary<string, TGenericClass> SpecializedClassTable = new Dictionary<string, TGenericClass>();
        public List<Assembly> AssemblyList = new List<Assembly>();
        public Dictionary<string, TType> TypeInfoTable = new Dictionary<string, TType>();

        Dictionary<string, string> ReflectionNameTable = new Dictionary<string, string>();
        public TSourceFile SystemSourceFile;

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

            Modified = new ManualResetEvent(true);
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

            RegisterClassNames();

            tick = DateTime.Now;

            SystemSourceFile.Parser.ParseFile(SystemSourceFile);

            // 型の別名の辞書をセットする。
            SetTypeAliasTable();

            // System.csのクラスのTypeInfoをセットする。
            SetSystemSourceFileTypeInfo();

            Dictionary<string, TType> class_table_save = new Dictionary<string, TType>(ClassTable);
            Dictionary<string, TGenericClass> parameterized_class_table_save = new Dictionary<string, TGenericClass>(ParameterizedClassTable);
            Dictionary<string, TGenericClass> specialized_class_table_save = new Dictionary<string, TGenericClass>(SpecializedClassTable);

            Debug.WriteLine("System.cs 終了 {0}", DateTime.Now.Subtract(tick).TotalMilliseconds);
            tick = DateTime.Now;

            while (true) {
                Modified.WaitOne();
                Modified.Reset();

                try {
                    Debug.WriteLine("解析開始");

                    tick = DateTime.Now;

                    ClassTable = new Dictionary<string, TType>(class_table_save);
                    ParameterizedClassTable = new Dictionary<string, TGenericClass>(parameterized_class_table_save);
                    SpecializedClassTable = new Dictionary<string, TGenericClass>(specialized_class_table_save);
                    ParseDone = false;

                    foreach (TSourceFile src in SourceFiles) {
                        if(src != SystemSourceFile) {

                            lock (src) {
                                src.Parser.ParseFile(src);
                            }
                        }
                    }
                    Debug.WriteLine("解析終了 {0}", DateTime.Now.Subtract(tick).TotalMilliseconds);
                    tick = DateTime.Now;

                    TSetParentNavi set_parent = new TSetParentNavi();
                    List<object> args = new List<object>();
                    args.Add(null);
                    set_parent.ProjectNavi(this, args);

                    while (true) {

                        var vc = (from c in SpecializedClassTable.Values where !c.SetMember select c).ToList();
                        if (vc.Any()) {

                            foreach (TGenericClass gen in vc) {
                                SetMemberOfSpecializedClass(gen);
                            }
                        }
                        else {
                            break;
                        }
                    }

                    ParseDone = true;

                    foreach(TType c in ClassTable.Values) {
                        if (c is TGenericClass) {

                            if (ParameterizedClassTable.ContainsValue(c as TGenericClass)) {

                                Debug.WriteLine("Parameterized Class:{0} {1}", c.ClassName, c.GenericType);
                            }
                            else {

                                Debug.WriteLine("Generic Class:{0}", c.ClassName, "");
                            }
                        }
                        else {
                            
                            //Debug.WriteLine("class:{0} {1}", c.ClassName, c.GenericType);
                            Debug.Assert(c.GenericType == EClass.SimpleClass);
                        }
                    }

                    // アプリのクラスのリスト
                    AppClasses = (from x in ClassTable.Values where x.Info == null && !(x is TGenericClass) && x.SourceFileCls != null select x).ToList();

                    foreach(TType cls in AppClasses) {
                        foreach(TType spr in cls.SuperClasses) {
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
                    Debug.WriteLine("名前解決 終了 {0}", DateTime.Now.Subtract(tick).TotalMilliseconds);

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

                    // 値を代入している変数参照のDefinedをtrueにする。
                    TSetDefined set_defined = new TSetDefined();
                    set_defined.ProjectNavi(this, null);

                    // すべてのソースファイルに対し
                    foreach (TSourceFile src in SourceFiles) {
                        TTokenWriter tw = new TTokenWriter(src.Parser);
                        src.Parser.SourceFileText(src, tw);

                        string fname = Path.GetFileNameWithoutExtension(src.PathSrc);

                        File.WriteAllText(OutputDir + "\\" + fname + ".cs", tw.ToPlainText(), Encoding.UTF8);
                        File.WriteAllText(html_dir + "\\" + fname + ".html", tw.ToHTMLText(fname), Encoding.UTF8);
                    }

                    // HTMLのソースコードを作る。
                    MakeHTMLSourceCode();

                    Debug.WriteLine("ソース生成 終了 {0}", DateTime.Now.Subtract(tick).TotalMilliseconds);
                    tick = DateTime.Now;

                    // 使用・定義連鎖を作る。
                    MakeUseDefineChain();

                    // クラス図を作る。
                    MakeClassDiagram();

                    // 要約を作る。
                    MakeSummary();

                    Debug.WriteLine("静的解析 終了 {0}", DateTime.Now.Subtract(tick).TotalMilliseconds);
                }
                catch (TBuildCancel) {
                }
            }
        }

        /*
         * アセンブリのリストを作る。
         */
        public void SetAssemblyList() {
            AssemblyList.Add(typeof(ApplicationData).GetTypeInfo().Assembly);
            AssemblyList.Add(typeof(Assembly).GetTypeInfo().Assembly);
            AssemblyList.Add(typeof(CanvasControl).GetTypeInfo().Assembly);
            AssemblyList.Add(typeof(Color).GetTypeInfo().Assembly);
            AssemblyList.Add(typeof(Debug).GetTypeInfo().Assembly);
            AssemblyList.Add(typeof(File).GetTypeInfo().Assembly);
            AssemblyList.Add(typeof(Stack<int>).GetTypeInfo().Assembly);
            AssemblyList.Add(typeof(MainPage).GetTypeInfo().Assembly);
            AssemblyList.Add(typeof(WebUtility).GetTypeInfo().Assembly);
        }

        public void OpenProject() {
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            SourceFiles = (from file_name in File.ReadAllLines(localFolder.Path + @"\ProjectFiles.txt", Encoding.UTF8)
                           select new TSourceFile(localFolder.Path + @"\" + file_name, TCSharpParser.CSharpParser)).ToList();

            SystemSourceFile = (from src in SourceFiles where Path.GetFileName(src.PathSrc) == "System.cs" select src).First();
        }

        public TType RegisterTypeInfoTable(TypeInfo inf) {
            TType tp2;

            if(TypeInfoTable.TryGetValue(inf.FullName, out tp2)) {

                return tp2;
            }

            tp2 = new TType(inf);
            TypeInfoTable.Add(inf.FullName, tp2);

            return tp2;
        }

        public void SetTypeInfo(TType tp) {
            if (tp.TypeInfoSearched) {
                return;
            }
            tp.TypeInfoSearched = true;

            string sys_name;

            if (! ReflectionNameTable.TryGetValue(tp.ClassName, out sys_name)) {

                sys_name = tp.ClassName;
            }

            var v = from a in AssemblyList from tp2 in a.GetTypes() where tp2.Name == sys_name select tp2;
            if (v.Any()) {

                if (v.Count() == 1) {
                    tp.Info = v.First().GetTypeInfo();
                }
                else { 
                    foreach(Type t in v) {
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
            ReflectionNameTable.Add("bool", "Boolean");
            ReflectionNameTable.Add("byte", "Byte");
            ReflectionNameTable.Add("char", "Char");
            ReflectionNameTable.Add("Dictionary", "Dictionary`2");
            ReflectionNameTable.Add("double", "Double");
            ReflectionNameTable.Add("float", "Single");
            ReflectionNameTable.Add("IEnumerable", "IEnumerable`1");
            ReflectionNameTable.Add("int", "Int32");
            ReflectionNameTable.Add("List", "List`1");
            ReflectionNameTable.Add("object", "Object");
            ReflectionNameTable.Add("short", "Int16");
            ReflectionNameTable.Add("string", "String");
            ReflectionNameTable.Add("void", "Void");
        }

        /*
         * System.csのクラスのTypeInfoをセットする。
         */
        public void SetSystemSourceFileTypeInfo() {
            
            foreach (TType tp in ClassTable.Values) {
                if (tp is TGenericClass) {
                    Debug.WriteLine("system 総称型 {0}", tp.ClassName, "");
                }
                else {
                    SetTypeInfo(tp);
                    if (tp.Info == null) {
                        Debug.WriteLine("ERR system class {0}", tp.ClassName, "");
                    }
                }
            }
        }

        public void RegisterClassNames() {
            //var v = from src in SourceFiles
            //        from line in src.Lines where line.Tokens != null
            //        from idx in TSys.Indexes(line.Tokens.Length) where line.Tokens[idx].Kind


            List<string> class_names = new List<string>();

            foreach(TSourceFile src in SourceFiles) {
                foreach(TLine line in src.Lines) {
                    int idx = new List<TToken>(line.Tokens).FindIndex(x => x.Kind == EKind.class_ || x.Kind == EKind.struct_ || x.Kind == EKind.enum_ || x.Kind == EKind.interface_ || x.Kind == EKind.delegate_);
                    if (idx != -1) {
                        if (idx + 1 < line.Tokens.Length && (line.Tokens[idx + 1].Kind == EKind.Identifier || line.Tokens[idx + 1].Kind == EKind.ClassName)) {

                            string name = line.Tokens[idx + 1].TextTkn;
                            if (!class_names.Contains(name)) {

                                //Debug.WriteLine("typeof({0}),", name, "");
                                class_names.Add(name);
                            }
                        }
                        else {

                            Debug.WriteLine("class syntax error");
                        }
                    }
                }
            }

            // クラス名のトークンのリスト
            var vtkn = from src in SourceFiles
                    from line in src.Lines
                    from tkn in line.Tokens
                    where tkn.Kind == EKind.Identifier && class_names.Contains(tkn.TextTkn) select tkn;

            foreach (TToken tkn in vtkn) {
                // 種類はClassNameにする。
                tkn.Kind = EKind.ClassName;
            }
        }

        public TType GetClassByName(string name) {
            TType cls;

            if (ClassTable.TryGetValue(name, out cls)) {
                return cls;
            }
            else {
                if(TParser.CurrentClass is TGenericClass) {
                    TGenericClass gen = TParser.CurrentClass as TGenericClass;

                    var v = from t in gen.ArgClasses where t.ClassName == name select t;
                    if (v.Any()) {

                        return v.First();
                    }
                }
                cls = new TType(name);

                //Debug.WriteLine("class : {0}", cls.GetClassText(), "");
                ClassTable.Add(name, cls);

                return cls;
            }
        }

        public TType GetParamClassByName(TType cls, string name) {
            if (cls is TGenericClass) {
                TGenericClass gen = cls as TGenericClass;

                var v = from c in gen.ArgClasses where c.ClassName == name select c;
                if (v.Any()) {
                    return v.First();
                }
            }

            return GetClassByName(name);
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

                sw.Write("[{0}]", new string(',', dim_cnt - 1));
            }

            return sw.ToString();
        }

        public TGenericClass GetParameterizedClass(string class_name, List<TType> param_classes) {
            string class_text = MakeClassText(class_name, param_classes, 0);

            TGenericClass reg_class;
            if (! ParameterizedClassTable.TryGetValue(class_text, out reg_class)) {

                reg_class = new TGenericClass(class_name, param_classes);
                reg_class.GenericType = EClass.ParameterizedClass;

                ParameterizedClassTable.Add(class_text, reg_class);

                ClassTable.Add(reg_class.ClassName, reg_class);
            }

            return reg_class;
        }

        public TGenericClass GetSpecializedClass(TGenericClass org_class, List<TType> param_classes, int dim_cnt) {
            string class_text = MakeClassText(org_class.ClassName, param_classes, dim_cnt);

            TGenericClass reg_class;
            if (!SpecializedClassTable.TryGetValue(class_text, out reg_class)) {

                reg_class = new TGenericClass(org_class, param_classes, dim_cnt);
                reg_class.GenericType = EClass.SpecializedClass;

                if (ParseDone) {
                    SetMemberOfSpecializedClass(reg_class);
                }
                else {

                    SpecializedClassTable.Add(class_text, reg_class);
                }
            }

            return reg_class;
        }

        public TType SubstituteArgumentClass(TType tp, Dictionary<string, TType> dic) {
            if(!(tp is TGenericClass)) {

                TType tp2;

                if(! dic.TryGetValue(tp.ClassName, out tp2)) {

                    tp2 = tp;
                }

                return tp2;
            }

            TGenericClass gen = tp as TGenericClass;
            bool changed = false;

            List<TType> vtp = new List<TType>();
            foreach(TType tp2 in gen.ArgClasses) {
                TType tp3 = SubstituteArgumentClass(tp2, dic);
                if(tp3 != tp2) {
                    changed = true;
                }
                vtp.Add(tp3);
            }

            if(!changed) {

                return tp;
            }

            return GetSpecializedClass(gen.OrgCla, vtp, gen.DimCnt);
        }

        public TVariable CopyVariable(TVariable var_src, Dictionary<string, TType> dic) {
            TType tp = SubstituteArgumentClass(var_src.TypeVar, dic);
            TVariable var1 = new TVariable(var_src.ModifierVar, var_src.TokenVar, tp, null);

            return var1;
        }

        public TField CopyField(TType cla1, TField fld_src, Dictionary<string, TType> dic) {
            TType tp = SubstituteArgumentClass(fld_src.TypeVar, dic);
            TField fld1 = new TField(cla1, fld_src.ModifierVar, fld_src.TokenVar, tp, null);

            return fld1;
        }

        public TFunction CopyFunctionDeclaration(TType cla1, TFunction fnc_src, Dictionary<string, TType> dic) {
            TVariable[] args = (from x in fnc_src.ArgsFnc select CopyVariable(x, dic)).ToArray();
            TType ret_type = SubstituteArgumentClass(fnc_src.TypeVar, dic);

            TFunction fnc = new TFunction(fnc_src.ModifierVar, fnc_src.TokenVar, args, ret_type, null, fnc_src.KindFnc);
            fnc.DeclaringType = cla1;

            return fnc;
        }

        public void SetMemberOfSpecializedClass(TGenericClass cls) {
            string class_text = cls.GetClassText();

            if (cls.SetMember) {
                return;
            }

            if (! SpecializedClassTable.ContainsKey(class_text)) {
                SpecializedClassTable.Add(class_text, cls);
            }

            cls.SetMember = true;

            Dictionary<string, TType> dic = new Dictionary<string, TType>();

            if(cls.ArgClasses == null || cls.ArgClasses.Count != cls.OrgCla.ArgClasses.Count) {
                throw new TParseException();
            }

            for(int i = 0; i < cls.OrgCla.ArgClasses.Count; i++) {
                dic.Add(cls.OrgCla.ArgClasses[i].ClassName, cls.ArgClasses[i]);
            }

            for (int i = 0; i < cls.ArgClasses.Count; i++) {
                cls.ArgClasses[i] = SubstituteArgumentClass(cls.ArgClasses[i], dic);
            }

            cls.KindClass = cls.OrgCla.KindClass;

            cls.SuperClasses = (from c in cls.OrgCla.SuperClasses select SubstituteArgumentClass(c, dic)).ToList();

            if(cls.OrgCla.RetType != null) {
                cls.RetType = SubstituteArgumentClass(cls.OrgCla.RetType, dic);
                cls.ArgTypes = (from c in cls.OrgCla.ArgTypes select SubstituteArgumentClass(c, dic)).ToArray();
            }

            cls.Fields = (from x in cls.OrgCla.Fields select CopyField(cls, x, dic)).ToList();
            cls.Functions = (from x in cls.OrgCla.Functions select CopyFunctionDeclaration(cls, x, dic)).ToList();
        }
    }
}
