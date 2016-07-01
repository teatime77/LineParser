using Microsoft.Graphics.Canvas.UI.Xaml;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Windows.Storage;
using Windows.UI;

namespace MyEdit {

    //------------------------------------------------------------ TProject

    public partial class TProject : TEnv {
        public TType IntClass;
        public TType FloatClass;
        public TType DoubleClass;
        public TType CharClass;
        public TType StringClass;
        public TType BoolClass;
        public TType VoidClass;
        public TType NullClass = new TType("null class");

        public List<TSourceFile> SourceFiles = new List<TSourceFile>();
        public Dictionary<string, TType> ClassTable = new Dictionary<string, TType>();
        public Dictionary<string, TGenericClass> ParameterizedClassTable = new Dictionary<string, TGenericClass>();
        public Dictionary<string, TGenericClass> SpecializedClassTable = new Dictionary<string, TGenericClass>();
        public Dictionary<string, TGenericClass> ArrayClassTable = new Dictionary<string, TGenericClass>();
        public List<Assembly> AssemblyList = new List<Assembly>();
        public Dictionary<TypeInfo, TType> SysClassTable = new Dictionary<TypeInfo, TType>();

        public TProject() {
        }

        public void SetAssemblyList() {
            AssemblyList.Add(typeof(ApplicationData).GetTypeInfo().Assembly);
            AssemblyList.Add(typeof(Assembly).GetTypeInfo().Assembly);
            AssemblyList.Add(typeof(CanvasControl).GetTypeInfo().Assembly);
            AssemblyList.Add(typeof(Color).GetTypeInfo().Assembly);
            AssemblyList.Add(typeof(Debug).GetTypeInfo().Assembly);
            AssemblyList.Add(typeof(File).GetTypeInfo().Assembly);
            AssemblyList.Add(typeof(Stack<int>).GetTypeInfo().Assembly);
        }

        public void OpenProject() {
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            string[] project_files = File.ReadAllLines(localFolder.Path + @"\ProjectFiles.txt", System.Text.Encoding.UTF8);
            foreach (string file_name in project_files) {

                string path = localFolder.Path + @"\" + file_name;

                TSourceFile src = new TSourceFile(path, TCSharpParser.CSharpParser);

                SourceFiles.Add(src);
            }
        }

        public TType GetSysClass(TypeInfo tp) {
            TType tp2;

            if(SysClassTable.TryGetValue(tp, out tp2)) {

                return tp2;
            }

            tp2 = new TType(tp);
            SysClassTable.Add(tp, tp2);

            return tp2;
        }
        public void RegisterSysClass() {
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

            foreach (TType tp in ClassTable.Values) {
                string name;

                if (!dic.TryGetValue(tp.ClassName, out name)) {
                    name = tp.ClassName;
                }
                var v = from a in AssemblyList from tp2 in a.GetTypes() where tp2.Name == name select tp2;
                if (v.Any()) {
                    tp.Info = v.First().GetTypeInfo();
                    SysClassTable.Add(tp.Info, tp);
                }
                else {
                    Debug.WriteLine("ERR sys class {0}", tp.ClassName, "");
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

        public void ClearProject() {
            ClassTable.Clear();
            ParameterizedClassTable.Clear();
            SpecializedClassTable.Clear();
            ArrayClassTable.Clear();
        }

        public TType GetClassByName(string name) {
            TType cls;

            if (ClassTable.TryGetValue(name, out cls)) {
                return cls;
            }
            else {
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

        public void RegClass(Dictionary<string, TType> dic, TType cls) {
            if (dic.ContainsKey(cls.ClassName)) {

                dic[cls.ClassName] = cls;
            }
            else {

                dic.Add(cls.ClassName, cls);
            }
        }

        public void RegGenericClass(Dictionary<string, TGenericClass> dic, TGenericClass cls) {
            if (dic.ContainsKey(cls.ClassName)) {

                dic[cls.ClassName] = cls;
            }
            else {

                dic.Add(cls.ClassName, cls);
            }
        }

        public TGenericClass GetSpecializedClass(TGenericClass cls, List<TType> vtp) {
            string class_text = cls.GetClassText();

            TGenericClass gen1 = null;
            if(! SpecializedClassTable.TryGetValue(class_text, out gen1)) {

                gen1 = new TGenericClass(cls, vtp);
                SpecializedClassTable.Add(class_text, gen1);
            }

            return gen1;
        }

        public TType SubstituteArgumentClass(TType tp, Dictionary<string, TType> dic) {
            if(!(tp is TGenericClass)) {

                return tp;
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

            return GetSpecializedClass(gen.OrgCla, vtp);
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
            Dictionary<string, TType> dic = new Dictionary<string, TType>();

            for(int i = 0; i < cls.OrgCla.GenCla.Count; i++) {
                dic.Add(cls.OrgCla.GenCla[i].ClassName, cls.GenCla[i]);
            }

            cls.Fields = (from x in cls.Fields select CopyField(cls, x, dic)).ToList();
            cls.Functions = (from x in cls.Functions select CopyFunctionDeclaration(cls, x, dic)).ToList();
        }
    }

}