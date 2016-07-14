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
using Windows.Storage;
using Windows.UI;

namespace Miyu {

    //------------------------------------------------------------ TProject

    public partial class TProject : TEnv {
        public TType IntClass;
        public TType FloatClass;
        public TType DoubleClass;
        public TType CharClass;
        public TType ObjectClass;
        public TType StringClass;
        public TType BoolClass;
        public TType VoidClass;
        public TType TypeClass;
        public TGenericClass ArrayClass;
        public TGenericClass EnumerableClass;
        public TType NullClass = new TType("null class");
        public TType HandlerClass = new TType("handler class");

        public List<TSourceFile> SourceFiles = new List<TSourceFile>();
        public Dictionary<string, TType> ClassTable = new Dictionary<string, TType>();
        public Dictionary<string, TGenericClass> ParameterizedClassTable = new Dictionary<string, TGenericClass>();
        public Dictionary<string, TGenericClass> SpecializedClassTable = new Dictionary<string, TGenericClass>();
        public List<Assembly> AssemblyList = new List<Assembly>();
        public Dictionary<string, TType> SysClassTable = new Dictionary<string, TType>();

        Dictionary<string, string> ToSysClassNameTable = new Dictionary<string, string>();
        Dictionary<string, string> FromSysClassNameTable = new Dictionary<string, string>();

        public ManualResetEvent Modified;
        public bool ParseDone;

        public TProject() {
        }

        public void Init() {
            Project = this;

            TParser.theParser = new TParser(this);
            TCSharpParser.CSharpParser = new TCSharpParser(this);
            Parser = TCSharpParser.CSharpParser;

            SetAssemblyList();

            OpenProject();

            Modified = new ManualResetEvent(true);
        }

        public void Build() {
            DateTime tick;

            Project = this;
            Parser = TCSharpParser.CSharpParser;

            RegisterClassNames();
            var v = from src in SourceFiles where Path.GetFileName(src.PathSrc) == "Sys.cs" select src;
            Debug.Assert(v.Any());
            TSourceFile sys_src = v.First();

            tick = DateTime.Now;

            sys_src.Parser.ParseFile(sys_src);

            RegisterSysClass();

            Dictionary<string, TType> class_table_save = new Dictionary<string, TType>(ClassTable);
            Dictionary<string, TGenericClass> parameterized_class_table_save = new Dictionary<string, TGenericClass>(ParameterizedClassTable);
            Dictionary<string, TGenericClass> specialized_class_table_save = new Dictionary<string, TGenericClass>(SpecializedClassTable);

            Debug.WriteLine("Sys.cs 終了 {0}", (DateTime.Now - tick).TotalMilliseconds);
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
                        if(src != sys_src) {

                            lock (src) {
                                src.Parser.ParseFile(src);
                            }
                        }
                    }
                    Debug.WriteLine("解析終了 {0}", (DateTime.Now - tick).TotalMilliseconds);
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

                    foreach (TSourceFile src in SourceFiles) {
                        src.Parser.ResolveName(src);
                    }
                    Debug.WriteLine("名前解決 終了 {0}", (DateTime.Now - tick).TotalMilliseconds);

                }
                catch (TBuildCancel) {

                }
            }

        }

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
            string[] project_files = File.ReadAllLines(localFolder.Path + @"\ProjectFiles.txt", Encoding.UTF8);
            foreach (string file_name in project_files) {

                string path = localFolder.Path + @"\" + file_name;

                TSourceFile src = new TSourceFile(path, TCSharpParser.CSharpParser);

                SourceFiles.Add(src);
            }
        }

        public TType GetSysClass(TypeInfo inf) {
            TType tp2;

            if(SysClassTable.TryGetValue(inf.FullName, out tp2)) {

                return tp2;
            }

            tp2 = new TType(inf);
            SysClassTable.Add(inf.FullName, tp2);

            return tp2;
        }

        public string FromSysClassName(string name) {
            string name2;

            if(FromSysClassNameTable.TryGetValue(name, out name2)) {
                return name2;
            }
            return name;
        }

        public string ToSysClassName(string name) {
            string name2;

            if (ToSysClassNameTable.TryGetValue(name, out name2)) {
                return name2;
            }
            return name;
        }

        public void SetTypeInfo(TType tp) {
            if (tp.TypeInfoSearched) {
                return;
            }
            tp.TypeInfoSearched = true;

            string sys_name = ToSysClassName(tp.ClassName);
            var v = from a in AssemblyList from tp2 in a.GetTypes() where tp2.Name == sys_name select tp2;
            if (v.Any()) {


                if (v.Count() == 1) {
                    tp.Info = v.First().GetTypeInfo();
                }
                else { 
                    foreach(Type t in v) {
                        Debug.WriteLine("あいまいな型 : {0}", t.FullName, "");
                    }
                    if(sys_name == "TimeSpan" || sys_name == "Path") {

                        tp.Info = v.Last().GetTypeInfo();
                    }
                    else {

                        tp.Info = v.First().GetTypeInfo();
                    }

                }
                SysClassTable.Add(tp.Info.FullName, tp);
            }
        }

        public void RegisterSysClass() {
            ToSysClassNameTable.Add("bool", "Boolean");
            ToSysClassNameTable.Add("byte", "Byte");
            ToSysClassNameTable.Add("char", "Char");
            ToSysClassNameTable.Add("Dictionary", "Dictionary`2");
            ToSysClassNameTable.Add("double", "Double");
            ToSysClassNameTable.Add("float", "Single");
            ToSysClassNameTable.Add("IEnumerable", "IEnumerable`1");
            ToSysClassNameTable.Add("int", "Int32");
            ToSysClassNameTable.Add("List", "List`1");
            ToSysClassNameTable.Add("object", "Object");
            ToSysClassNameTable.Add("short", "Int16");
            ToSysClassNameTable.Add("string", "String");
            ToSysClassNameTable.Add("void", "Void");

            foreach(string s in ToSysClassNameTable.Keys) {
                FromSysClassNameTable.Add(ToSysClassNameTable[s], s);
            }
            
            foreach (TType tp in ClassTable.Values) {
                if (tp is TGenericClass) {
                    Debug.WriteLine("sys 総称型 {0}", tp.ClassName, "");
                }
                else {
                    SetTypeInfo(tp);
                    if (tp.Info == null) {
                        Debug.WriteLine("ERR sys class {0}", tp.ClassName, "");
                    }
                }
            }
        }

        public void RegisterClassNames() {
            List<string> class_names = new List<string>();

            foreach(TSourceFile src in SourceFiles) {
                foreach(TLine line in src.Lines) {
                    if(line.Tokens != null) {
                        int idx = new List<TToken>(line.Tokens).FindIndex(x => x.Kind == EKind.class_ || x.Kind == EKind.struct_ || x.Kind == EKind.enum_ || x.Kind == EKind.interface_ || x.Kind == EKind.delegate_);
                        if(idx != -1) {
                            if(idx + 1 < line.Tokens.Length && (line.Tokens[idx + 1].Kind == EKind.Identifier || line.Tokens[idx + 1].Kind == EKind.ClassName)) {

                                string name = line.Tokens[idx + 1].TextTkn;
                                if (!class_names.Contains(name)) {

                                    class_names.Add(name);
                                    //Debug.WriteLine("typeof({0}),", name, "");
                                }
                            }
                            else {

                                Debug.WriteLine("class syntax error");
                            }
                        }
                    }
                }
            }

            foreach (TSourceFile src in SourceFiles) {
                foreach (TLine line in src.Lines) {
                    if (line.Tokens != null) {
                        var v = from x in line.Tokens where x.Kind == EKind.Identifier && class_names.Contains(x.TextTkn) select x;
                        foreach(TToken tkn in v) {
                            tkn.Kind = EKind.ClassName;
                        }
                    }
                }
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

                    var v = from t in gen.GenCla where t.ClassName == name select t;
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

                var v = from c in gen.GenCla where c.ClassName == name select c;
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
                reg_class.GenericType = EGeneric.ParameterizedClass;

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
                reg_class.GenericType = EGeneric.SpecializedClass;

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
            foreach(TType tp2 in gen.GenCla) {
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
            TVariable var1 = new TVariable(var_src.TokenVar, tp, null);

            return var1;
        }

        public TField CopyField(TType cla1, TField fld_src, Dictionary<string, TType> dic) {
            TType tp = SubstituteArgumentClass(fld_src.TypeVar, dic);
            TField fld1 = new TField(cla1, fld_src.IsStatic, fld_src.TokenVar, tp, null);

            return fld1;
        }

        public TFunction CopyFunctionDeclaration(TType cla1, TFunction fnc_src, Dictionary<string, TType> dic) {
            TVariable[] args = (from x in fnc_src.ArgsFnc select CopyVariable(x, dic)).ToArray();
            TType ret_type = SubstituteArgumentClass(fnc_src.TypeVar, dic);

            TFunction fnc = new TFunction(fnc_src.IsStatic, fnc_src.TokenVar, args, ret_type, null);

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

            if(cls.GenCla == null || cls.GenCla.Count != cls.OrgCla.GenCla.Count) {
                throw new TParseException();
            }

            for(int i = 0; i < cls.OrgCla.GenCla.Count; i++) {
                dic.Add(cls.OrgCla.GenCla[i].ClassName, cls.GenCla[i]);
            }

            for (int i = 0; i < cls.GenCla.Count; i++) {
                cls.GenCla[i] = SubstituteArgumentClass(cls.GenCla[i], dic);
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