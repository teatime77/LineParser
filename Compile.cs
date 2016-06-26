using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MyEdit {

    //------------------------------------------------------------ TVariable
    partial class TVariable {
        public virtual void ResolveName(TClass cls, List<TVariable> vars) {
            if(InitValue != null) {
                InitValue.ResolveName(cls, vars);
            }
        }
    }

    partial class TFunction {
        /*
            関数名と引数の数と型が一致したらtrueを返します。
        */
        public bool Match(string name, List<TClass> arg_types, bool exact) {
            if (NameVar != name || arg_types.Count != ArgsFnc.Length) {
                // 関数名か引数の数が違う場合

                return false;
            }

            for (int i = 0; i < ArgsFnc.Length; i++) {
                TVariable var1 = ArgsFnc[i];
                if ( ! ( var1.TypeVar == arg_types[i] || !exact && arg_types[i].IsSubClass(var1.TypeVar) ) ) {
                    return false;
                }
            }

            return true;
        }

        public override void ResolveName(TClass cls, List<TVariable> vars) {
            if(BlockFnc != null) {

                int vars_count = vars.Count;

                vars.AddRange(ArgsFnc);
                BlockFnc.ResolveName(cls, vars);
                vars.RemoveRange(vars_count, vars.Count - vars_count);
            }
        }
    }

    //------------------------------------------------------------ TTerm

    partial class TTerm {
        public virtual void ResolveName(TClass cls, List<TVariable> vars) {
        }
    }

    partial class TLiteral {
        public override void ResolveName(TClass cls, List<TVariable> vars) {
            switch (TokenTrm.TokenType) {
            case ETokenType.Int:
                TypeTrm = TProject.IntClass;
                break;

            case ETokenType.Float:
                TypeTrm = TProject.FloatClass;
                break;

            case ETokenType.Double:
                TypeTrm = TProject.DoubleClass;
                break;

            case ETokenType.Char_:
                TypeTrm = TProject.CharClass;
                break;

            case ETokenType.String_:
                TypeTrm = TProject.StringClass;
                break;

            default:
                throw new TResolveNameException(TokenTrm);
            }
        }
    }

    partial class TReference {
        public override void ResolveName(TClass cls, List<TVariable> vars) {
            if(this is TDotReference) {
                TDotReference dot_ref = this as TDotReference;

                dot_ref.DotRef.ResolveName(cls, vars);

                VarRef = dot_ref.DotRef.TypeTrm.FindField(NameRef);
                if(VarRef == null) {

                    throw new TResolveNameException(this);
                }
            }
            else {

                var v = from x in vars where x.NameVar == NameRef select x;
                if (v.Any()) {

                    VarRef = v.First();
                }
                else {

                    throw new TResolveNameException(this);
                }
            }

            if(CastType != null) {

                TypeTrm = CastType;
            }
            else {

                TypeTrm = VarRef.TypeVar;
            }

            if (TypeTrm == null) {
                throw new TResolveNameException(this);
            }
        }
    }

    partial class TApply {
        public override void ResolveName(TClass cls, List<TVariable> vars) {
            if(this is TDotApply) {

                (this as TDotApply).DotApp.ResolveName(cls, vars);
            }

            List<TClass> arg_types = new List<TClass>();

            foreach (TTerm t in Args) {
                t.ResolveName(cls, vars);
                arg_types.Add(t.TypeTrm);
            }

            if(FunctionApp is TReference) {
                TReference fnc_ref = FunctionApp as TReference;

                if (this is TDotApply) {

                    fnc_ref.VarRef = (this as TDotApply).DotApp.TypeTrm.MatchFunction(fnc_ref.NameRef, arg_types);
                }
                else {

                    fnc_ref.VarRef = cls.MatchFunction(fnc_ref.NameRef, arg_types);
                }
                if(fnc_ref.VarRef == null) {
                    throw new TResolveNameException(fnc_ref);
                }

                TypeTrm = fnc_ref.VarRef.TypeVar;
            }
            else {

                switch (KindApp) {
                case EKind.NewInstance:
                    TypeTrm = (this as TNewApply).ClassApp;
                    break;

                case EKind.NewArray:
                    break;

                case EKind.base_:
                    break;

                case EKind.Inc:
                case EKind.Dec:
                    TypeTrm = Args[0].TypeTrm;
                    break;

                case EKind.Add:
                case EKind.Sub:
                case EKind.Mul:
                case EKind.Div:
                case EKind.Mod:

                    TypeTrm = Args[0].TypeTrm;
                    break;

                case EKind.Eq:
                case EKind.NE:
                case EKind.LT:
                case EKind.LE:
                case EKind.GT:
                case EKind.GE:
                    TypeTrm = TProject.BoolClass;
                    break;

                case EKind.Not_:
                    TypeTrm = TProject.BoolClass;
                    break;

                case EKind.And_:
                case EKind.Or_:
                    TypeTrm = TProject.BoolClass;
                    break;

                case EKind.Assign:
                case EKind.AddEq:
                case EKind.SubEq:
                case EKind.DivEq:
                case EKind.ModEq:
                    TypeTrm = TProject.VoidClass;
                    break;
                }
            }

            if(TypeTrm == null) {
                throw new TResolveNameException(TokenTrm);
            }
        }
    }

    partial class TQuery {
        public override void ResolveName(TClass cls, List<TVariable> vars) {
        }
    }

    //------------------------------------------------------------ TStatement

    partial class TStatement {
        public virtual void ResolveName(TClass cls, List<TVariable> vars) {
        }
    }

    partial class TAssignment {
        public override void ResolveName(TClass cls, List<TVariable> vars) {
            RelAsn.ResolveName(cls, vars);
        }
    }

    partial class TCall {
        public override void ResolveName(TClass cls, List<TVariable> vars) {
            AppCall.ResolveName(cls, vars);
        }
    }


    partial class TVariableDeclaration {
        public override void ResolveName(TClass cls, List<TVariable> vars) {
            foreach(TVariable var1 in Variables) {
                var1.ResolveName(cls, vars);
            }
        }
    }

    partial class TBlockStatement {
        public void ResolveNameBlock(TClass cls, List<TVariable> vars) {
/*
            int vars_count = vars.Count;

            foreach (TStatement stmt in StatementsBlc) {
                try {
                    stmt.ResolveName(cls, vars);
                }
                catch (TResolveNameException) {
                }
            }

            vars.RemoveRange(vars_count, vars.Count - vars_count);
*/
        }
    }

    partial class TBlock {
        public override void ResolveName(TClass cls, List<TVariable> vars) {
            ResolveNameBlock(cls, vars);
        }
    }

    partial class TIfBlock {
        public override void ResolveName(TClass cls, List<TVariable> vars) {
            if(ConditionIf != null) {
                ConditionIf.ResolveName(cls, vars);
            }
            ResolveNameBlock(cls, vars);
        }
    }

    partial class TIf {
        public override void ResolveName(TClass cls, List<TVariable> vars) {
            foreach(TIfBlock if_block in IfBlocks) {
                if_block.ResolveName(cls, vars);
            }
        }
    }

    partial class TCase {
        public override void ResolveName(TClass cls, List<TVariable> vars) {
            foreach(TTerm t in TermsCase) {
                t.ResolveName(cls, vars);
            }
            ResolveNameBlock(cls, vars);
        }
    }

    partial class TSwitch {
        public override void ResolveName(TClass cls, List<TVariable> vars) {
            TermSwitch.ResolveName(cls, vars);
/*
            foreach(TCase cas in Cases) {
                cas.ResolveName(cls, vars);
            }
*/
        }
    }

    partial class TTry {
        public override void ResolveName(TClass cls, List<TVariable> vars) {
            ResolveNameBlock(cls, vars);
        }
    }

    partial class TCatch {
        public override void ResolveName(TClass cls, List<TVariable> vars) {
            CatchVariable.ResolveName(cls, vars);

            vars.Add(CatchVariable);
            ResolveNameBlock(cls, vars);
            vars.RemoveAt(vars.Count - 1);
        }
    }

    partial class TWhile {
        public override void ResolveName(TClass cls, List<TVariable> vars) {
            WhileCondition.ResolveName(cls, vars);
            ResolveNameBlock(cls, vars);
        }
    }

    partial class TForEach {
        public override void ResolveName(TClass cls, List<TVariable> vars) {
            ListFor.ResolveName(cls, vars);
            LoopVariable.ResolveName(cls, vars);

            vars.Add(LoopVariable);
            ResolveNameBlock(cls, vars);
            vars.RemoveAt(vars.Count - 1);
        }
    }

    partial class TFor {
        public override void ResolveName(TClass cls, List<TVariable> vars) {
            if(InitStatement != null) {

                InitStatement.ResolveName(cls, vars);
            }

            if(LoopVariable != null) {

                vars.Add(LoopVariable);
            }

            if (ConditionFor != null) {

                ConditionFor.ResolveName(cls, vars);
            }

            if(PostStatement != null) {
                PostStatement.ResolveName(cls, vars);
            }

            if (LoopVariable != null) {

                vars.RemoveAt(vars.Count - 1);
            }
        }
    }

    partial class TJump {
        public override void ResolveName(TClass cls, List<TVariable> vars) {
            if(RetVal != null) {
                RetVal.ResolveName(cls, vars);
            }
        }
    }
    
    partial class TClass {
        public bool IsSubClass(TClass tp) {
            if (SuperClasses.Contains(tp)) {
                return true;
            }
            foreach(TClass super_class in SuperClasses) {
                if (super_class.IsSubClass(tp)) {
                    return true;
                }
            }

            return false;
        }

        public bool IsSuperClass(TClass tp) {
            return tp.IsSubClass(this);
        }
    }

    partial class TClass {
        public TField FindField(string name) {
            var v = from f in Fields where f.NameVar == name select f;
            if (v.Any()) {
                return v.First();
            }

            foreach (TClass super_class in SuperClasses) {
                TField fld = super_class.FindField(name);
                if (fld != null) {
                    return fld;
                }
            }

            return null;
        }
        
        public TFunction MatchFunction(string name, List<TClass> arg_types) {
            // 関数名と引数の数と型が一致する関数を探します。
            var v1 = from f in Functions where f.Match(name, arg_types, true) select f;
            if (v1.Any()) {
                return v1.First();
            }

            // 関数名と引数の数と型が一致する関数を探します。
            var v2 = from f in Functions where f.Match(name, arg_types, false) select f;
            if (v2.Any()) {
                return v2.First();
            }

            foreach (TClass super_class in SuperClasses) {
                TFunction fnc = super_class.MatchFunction(name, arg_types);
                if(fnc != null) {
                    return fnc;
                }
            }

            return null;
        }

        public void ResolveName() {
            List<TVariable> vars = new List<TVariable>();

            foreach (TField fld in Fields) {
                fld.ResolveName(this, vars);
            }

            foreach (TFunction fnc in Functions) {
                fnc.ResolveName(this, vars);
            }
        }
    }

    partial class TProject {
        public void ResolveName() {
            var classes = (from src in SourceFiles from c in src.ClassesSrc select c).Distinct();
            foreach(TClass cls in classes) {
                cls.ResolveName();
            }
        }
    }
}