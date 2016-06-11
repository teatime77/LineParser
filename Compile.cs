using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MyEdit {
    partial class TTerm {
        public virtual void ResolveName(TClass cls, List<TVariable> vars) {
        }

        public virtual TClass CalcType() {
            return null;
        }
    }

    partial class TReference {
        public override void ResolveName(TClass cls, List<TVariable> vars) {
            var v = from var1 in vars where var1.NameVar == NameRef select var1;
            if (v.Any()) {

                VarRef = v.First();
            }
            else {
                throw new TParseException();
            }
        }

        public override TClass CalcType() {
            if(VarRef != null) {
                return VarRef.TypeVar;
            }
            return null;
        }
    }

    partial class TFieldReference {
        public override void ResolveName(TClass cls, List<TVariable> vars) {
            TermFldRef.ResolveName(cls, vars);
        }
    }

    partial class TApply {
        public override void ResolveName(TClass cls, List<TVariable> vars) {
            List<TClass> arg_types = new List<TClass>();

            foreach(TTerm trm in Args) {
                trm.ResolveName(cls, vars);
                arg_types.Add(trm.CalcType());
            }

            FunctionRef.VarRef = cls.MatchFunction(FunctionRef.NameRef, arg_types);
        }
    }

    partial class TMethodApply {
        public override void ResolveName(TClass cls, List<TVariable> vars) {
            TermApp.ResolveName(cls, vars);
            TClass tp = TermApp.CalcType();

            List<TClass> arg_types = new List<TClass>();

            foreach (TTerm trm in Args) {
                trm.ResolveName(cls, vars);
                arg_types.Add(trm.CalcType());
            }

            FunctionRef.VarRef = ((TClass)tp).MatchFunction(FunctionRef.NameRef, arg_types);
        }
    }

    partial class TClass {
        public bool IsSuper(TClass tp) {
            return true;
        }
    }

    partial class TFunction {
        /*
            関数名と引数の数と型が一致したらtrueを返します。
        */
        public bool Match(string name, List<TClass> arg_types) {
            if(NameVar != name || arg_types.Count != ArgsFnc.Length) {
                // 関数名か引数の数が違う場合

                return false;
            }

            for(int i = 0; i < ArgsFnc.Length; i++) {
                TVariable var1 = ArgsFnc[i];
                if (! var1.TypeVar.IsSuper(arg_types[i])) {
                    return false;
                }
            }

            return true;
        }
    }

    partial class TClass {
        public TFunction MatchFunction(string name, List<TClass> arg_types) {
            var v = from f in Functions where f.Match(name, arg_types) select f;
            if (v.Any()) {
                return v.First();
            }

            foreach(TClass super_class in SuperClasses) {
                TFunction fnc = super_class.MatchFunction(name, arg_types);
                if(fnc != null) {
                    return fnc;
                }
            }

            return null;
        }
    }


}