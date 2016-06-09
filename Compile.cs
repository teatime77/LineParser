using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MyEdit {
    
    partial class MyEditor {
        void GetVariableClass(int current_line_idx, out TClass cls, out List<TVariable> vars) {
            vars = new List<TVariable>();
            cls = null;

            int min_indent = Lines[current_line_idx].Indent;

            for (int line_idx = current_line_idx; 0 <= line_idx; line_idx--) {
                TLine line = Lines[line_idx];

                if(line.ObjLine != null && line.Indent <= min_indent) {

                    if (line.Indent < min_indent) {

                        min_indent = line.Indent;
                    }

                    if (line.ObjLine is TVariableDeclaration) {

                        TVariableDeclaration var_decl = (TVariableDeclaration)line.ObjLine;
                        vars.AddRange(var_decl.Variables);
                    }
                    else if(line.ObjLine is TFunction) {
                        TFunction fnc = (TFunction)line.ObjLine;
                        vars.AddRange(fnc.ArgsFnc);
                    }
                    else if (line.ObjLine is TFor) {
                        TFor for1 = (TFor)line.ObjLine;
                        vars.Add(for1.LoopVariable);
                    }
                    else if (line.ObjLine is TCatch) {
                        TCatch catch1 = (TCatch)line.ObjLine;
                        vars.Add(catch1.CatchVariable);
                    }
                    else if (line.ObjLine is TClass) {

                        cls = (TClass)line.ObjLine;
                        return;
                    }
                }
            }
        }
    }

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
        public bool Match(string name, List<TClass> arg_types) {
            if(NameVar != name || arg_types.Count != ArgsFnc.Length) {
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