using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Windows.Storage;

namespace MyEdit {

    //------------------------------------------------------------ TProject

    public partial class TProject {
        public static TProject Project;

        public static TClass IntClass;
        public static TClass FloatClass;
        public static TClass DoubleClass;
        public static TClass CharClass;
        public static TClass StringClass;
        public static TClass BoolClass;
        public static TClass VoidClass;

        public List<TSourceFile> SourceFiles = new List<TSourceFile>();
        public Dictionary<string, TClass> ClassTable = new Dictionary<string, TClass>();
        public Dictionary<string, TGenericClass> ParameterizedClassTable = new Dictionary<string, TGenericClass>();
        public Dictionary<string, TGenericClass> SpecializedClassTable = new Dictionary<string, TGenericClass>();
        public Dictionary<string, TGenericClass> ArrayClassTable = new Dictionary<string, TGenericClass>();

        public TProject() {
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
                                    //Debug.WriteLine("class {0}", name, "");
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

        public TClass GetClassByName(string name) {
            TClass cls;

            if (ClassTable.TryGetValue(name, out cls)) {
                return cls;
            }
            else {
                cls = new TClass(name);

                //Debug.WriteLine("class : {0}", cls.GetClassText(), "");
                ClassTable.Add(name, cls);

                return cls;
            }
        }

        public TClass GetParamClassByName(TClass cls, string name) {
            if (cls is TGenericClass) {
                TGenericClass gen = cls as TGenericClass;

                var v = from c in gen.GenCla where c.ClassName == name select c;
                if (v.Any()) {
                    return v.First();
                }
            }

            return GetClassByName(name);
        }

        public void RegClass(Dictionary<string, TClass> dic, TClass cls) {
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

        public TGenericClass GetSpecializedClass(TGenericClass cls, List<TClass> vtp) {
            string class_text = cls.GetClassText();

            TGenericClass gen1 = null;
            if(! SpecializedClassTable.TryGetValue(class_text, out gen1)) {

                gen1 = new TGenericClass(cls, vtp);
                SpecializedClassTable.Add(class_text, gen1);
            }

            return gen1;
        }

        public TClass SubstituteArgumentClass(TClass tp, Dictionary<string, TClass> dic) {
            if(!(tp is TGenericClass)) {

                return tp;
            }

            TGenericClass gen = tp as TGenericClass;
            bool changed = false;

            List<TClass> vtp = new List<TClass>();
            foreach(TClass tp2 in gen.GenCla) {
                TClass tp3 = SubstituteArgumentClass(tp2, dic);
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

        public TVariable CopyVariable(TVariable var_src, Dictionary<string, TClass> dic) {
            TClass tp = SubstituteArgumentClass(var_src.TypeVar, dic);
            TVariable var1 = new TVariable(var_src.TokenVar, tp, null);

            return var1;
        }

        public TField CopyField(TClass cla1, TField fld_src, Dictionary<string, TClass> dic) {
            TClass tp = SubstituteArgumentClass(fld_src.TypeVar, dic);
            TField fld1 = new TField(cla1, fld_src.IsStatic, fld_src.TokenVar, tp, null);

            return fld1;
        }

        public TFunction CopyFunctionDeclaration(TClass cla1, TFunction fnc_src, Dictionary<string, TClass> dic) {
            TVariable[] args = (from x in fnc_src.ArgsFnc select CopyVariable(x, dic)).ToArray();
            TClass ret_type = SubstituteArgumentClass(fnc_src.TypeVar, dic);

            TFunction fnc = new TFunction(fnc_src.IsStatic, fnc_src.TokenVar, args, ret_type, null);

            return fnc;
        }

        public void SetMemberOfSpecializedClass(TGenericClass cls) {
            Dictionary<string, TClass> dic = new Dictionary<string, TClass>();

            for(int i = 0; i < cls.OrgCla.GenCla.Count; i++) {
                dic.Add(cls.OrgCla.GenCla[i].ClassName, cls.GenCla[i]);
            }

            cls.Fields = (from x in cls.Fields select CopyField(cls, x, dic)).ToList();
            cls.Functions = (from x in cls.Functions select CopyFunctionDeclaration(cls, x, dic)).ToList();
        }
    }

}