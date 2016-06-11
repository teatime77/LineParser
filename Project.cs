using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MyEdit {

    //------------------------------------------------------------ TProject

    public class TProject {
        public List<TClass> Classes = new List<TClass>();

        public Dictionary<string, TClass> ClassTable = new Dictionary<string, TClass>();
        public Dictionary<string, TGenericClass> ParameterizedClassTable = new Dictionary<string, TGenericClass>();
        public Dictionary<string, TGenericClass> SpecializedClassTable = new Dictionary<string, TGenericClass>();
        public Dictionary<string, TGenericClass> ArrayClassTable = new Dictionary<string, TGenericClass>();

        public void ClearProject() {
            Classes.Clear();
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

                Debug.WriteLine("class : {0}", cls.GetClassText(), "");
                ClassTable.Add(name, cls);

                Classes.Add(cls);

                return cls;
            }
        }

        public TClass GetParamClassByName(TClass cls, string name) {
            if (cls is TGenericClass) {
                TGenericClass gen = (TGenericClass)cls;

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
            TVariable var1 = new TVariable(var_src.NameVar, tp, null);

            return var1;
        }

        public TField CopyField(TClass cla1, TField fld_src, Dictionary<string, TClass> dic) {
            TClass tp = SubstituteArgumentClass(fld_src.TypeVar, dic);
            TField fld1 = new TField(fld_src.IsStatic, fld_src.NameVar, tp, null);

            return fld1;
        }

        public TFunction CopyFunctionDeclaration(TClass cla1, TFunction fnc_src, Dictionary<string, TClass> dic) {
            TVariable[] args = (from x in fnc_src.ArgsFnc select CopyVariable(x, dic)).ToArray();
            TClass ret_type = SubstituteArgumentClass(fnc_src.TypeVar, dic);

            TFunction fnc = new TFunction(fnc_src.IsStatic, fnc_src.NameVar, args, ret_type);

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