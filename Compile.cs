using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace MyEdit {

    //------------------------------------------------------------ TVariable
    partial class TVariable {
        public virtual void ResolveName(TType cls, List<TVariable> vars) {
            if(InitValue != null) {
                InitValue.ResolveName(cls, vars);
            }
        }
    }

    partial class TFunction {
        /*
            関数名と引数の数と型が一致したらtrueを返します。
        */
        public bool Match(string name, List<TType> arg_types, bool exact) {
            if (NameVar != name || arg_types.Count < ArgsFnc.Length) {
                // 関数名か引数の数が違う場合

                return false;
            }

            if (ArgsFnc.Length < arg_types.Count) {

                if(ArgsFnc.Length == 0 || ArgsFnc[ArgsFnc.Length - 1].KindVar != EKind.params_) {
                    // 最後の仮引数が可変個の引数でない場合

                    return false;
                }
            }

            for (int i = 0; i < ArgsFnc.Length; i++) {
                TVariable var1 = ArgsFnc[i];

                if (var1.KindVar == EKind.params_) {
                    // 仮引数が可変個の引数の場合

                    if (i != ArgsFnc.Length - 1) {
                        // 最後でない場合

                        throw new TResolveNameException();
                    }

                    return true;
                }

                if ( ! ( var1.TypeVar == arg_types[i] || !exact && arg_types[i].IsSubClass(var1.TypeVar) ) ) {
                    return false;
                }
            }

            return true;
        }

        public override void ResolveName(TType cls, List<TVariable> vars) {
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
        public virtual void ResolveName(TType cls, List<TVariable> vars) {
        }
    }

    partial class TLiteral {
        public override void ResolveName(TType cls, List<TVariable> vars) {
            switch (TokenTrm.TokenType) {
            case ETokenType.Int:
                TypeTrm = Project.IntClass;
                break;

            case ETokenType.Float:
                TypeTrm = Project.FloatClass;
                break;

            case ETokenType.Double:
                TypeTrm = Project.DoubleClass;
                break;

            case ETokenType.Char_:
                TypeTrm = Project.CharClass;
                break;

            case ETokenType.String_:
                TypeTrm = Project.StringClass;
                break;

            default:
                throw new TResolveNameException(TokenTrm);
            }
        }
    }

    partial class TReference {
        public override void ResolveName(TType cls, List<TVariable> vars) {
            if(this is TDotReference) {
                TDotReference dot_ref = this as TDotReference;

                dot_ref.DotRef.ResolveName(cls, vars);

                VarRef = dot_ref.DotRef.TypeTrm.FindField(NameRef);
                if(VarRef == null) {

                    throw new TResolveNameException(this);
                }
            }
            else {
                if(ClassRef != null) {

                    TypeTrm = ClassRef;
                    return;
                }

                Debug.Assert(TokenTrm != null);
                switch (TokenTrm.Kind) {
                case EKind.this_:
                    TypeTrm = cls;
                    return;

                case EKind.true_:
                case EKind.false_:
                    TypeTrm = Project.BoolClass;
                    return;

                case EKind.null_:
                    TypeTrm = Project.NullClass;
                    return;

                case EKind.base_:
                    if(cls.SuperClasses.Count == 0) {

                        throw new TResolveNameException(this);
                    }
                    TypeTrm = cls.SuperClasses[0];
                    return;
                }

                var v = from x in vars where x.NameVar == NameRef select x;
                if (v.Any()) {

                    VarRef = v.First();
                }
                else {

                    if(cls != null) {

                        VarRef = cls.FindField(NameRef);
                        if (VarRef == null) {

                            throw new TResolveNameException(this);
                        }
                    }
                    else {

                        throw new TResolveNameException(this);
                    }
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
        public override void ResolveName(TType cls, List<TVariable> vars) {
            if(this is TDotApply) {

                (this as TDotApply).DotApp.ResolveName(cls, vars);
            }

            List<TType> arg_types = new List<TType>();

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

                    if(fnc_ref.NameRef == "InitializeComponent") {
                        TypeTrm = Project.VoidClass;
                        return;
                    }
                    if (this is TDotApply) {

                        fnc_ref.VarRef = (this as TDotApply).DotApp.TypeTrm.MatchFunction(fnc_ref.NameRef, arg_types);
                    }
                    else {

                        fnc_ref.VarRef = cls.MatchFunction(fnc_ref.NameRef, arg_types);
                    }

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
                    TypeTrm = Project.BoolClass;
                    break;

                case EKind.Not_:
                    TypeTrm = Project.BoolClass;
                    break;

                case EKind.And_:
                case EKind.Or_:
                    TypeTrm = Project.BoolClass;
                    break;

                case EKind.Assign:
                case EKind.AddEq:
                case EKind.SubEq:
                case EKind.DivEq:
                case EKind.ModEq:
                    TypeTrm = Project.VoidClass;
                    break;
                }
            }

            if(TypeTrm == null) {
                throw new TResolveNameException(TokenTrm);
            }
        }
    }

    partial class TQuery {
        public override void ResolveName(TType cls, List<TVariable> vars) {
        }
    }

    //------------------------------------------------------------ TStatement

    partial class TStatement {
        public virtual void ResolveName(TType cls, List<TVariable> vars) {
        }
    }

    partial class TAssignment {
        public override void ResolveName(TType cls, List<TVariable> vars) {
            RelAsn.ResolveName(cls, vars);
        }
    }

    partial class TCall {
        public override void ResolveName(TType cls, List<TVariable> vars) {
            AppCall.ResolveName(cls, vars);
        }
    }


    partial class TVariableDeclaration {
        public override void ResolveName(TType cls, List<TVariable> vars) {
            foreach(TVariable var1 in Variables) {
                var1.ResolveName(cls, vars);
            }
        }
    }

    partial class TBlockStatement {
        public void ResolveNameBlock(TType cls, List<TVariable> vars) {
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
        public override void ResolveName(TType cls, List<TVariable> vars) {
            ResolveNameBlock(cls, vars);
        }
    }

    partial class TIfBlock {
        public override void ResolveName(TType cls, List<TVariable> vars) {
            if(ConditionIf != null) {
                ConditionIf.ResolveName(cls, vars);
            }
            ResolveNameBlock(cls, vars);
        }
    }

    partial class TIf {
        public override void ResolveName(TType cls, List<TVariable> vars) {
            foreach(TIfBlock if_block in IfBlocks) {
                if_block.ResolveName(cls, vars);
            }
        }
    }

    partial class TCase {
        public override void ResolveName(TType cls, List<TVariable> vars) {
            foreach(TTerm t in TermsCase) {
                t.ResolveName(cls, vars);
            }
            ResolveNameBlock(cls, vars);
        }
    }

    partial class TSwitch {
        public override void ResolveName(TType cls, List<TVariable> vars) {
            TermSwitch.ResolveName(cls, vars);
/*
            foreach(TCase cas in Cases) {
                cas.ResolveName(cls, vars);
            }
*/
        }
    }

    partial class TTry {
        public override void ResolveName(TType cls, List<TVariable> vars) {
            ResolveNameBlock(cls, vars);
        }
    }

    partial class TCatch {
        public override void ResolveName(TType cls, List<TVariable> vars) {
            CatchVariable.ResolveName(cls, vars);

            vars.Add(CatchVariable);
            ResolveNameBlock(cls, vars);
            vars.RemoveAt(vars.Count - 1);
        }
    }

    partial class TWhile {
        public override void ResolveName(TType cls, List<TVariable> vars) {
            WhileCondition.ResolveName(cls, vars);
            ResolveNameBlock(cls, vars);
        }
    }

    partial class TForEach {
        public override void ResolveName(TType cls, List<TVariable> vars) {
            ListFor.ResolveName(cls, vars);
            LoopVariable.ResolveName(cls, vars);

            vars.Add(LoopVariable);
            ResolveNameBlock(cls, vars);
            vars.RemoveAt(vars.Count - 1);
        }
    }

    partial class TFor {
        public override void ResolveName(TType cls, List<TVariable> vars) {
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
        public override void ResolveName(TType cls, List<TVariable> vars) {
            if(RetVal != null) {
                RetVal.ResolveName(cls, vars);
            }
        }
    }
    
    partial class TType {
        public bool IsSubClass(TType tp) {
            if (SuperClasses.Contains(tp)) {
                return true;
            }
            foreach(TType super_class in SuperClasses) {
                if (super_class.IsSubClass(tp)) {
                    return true;
                }
            }

            return false;
        }

        public bool IsSuperClass(TType tp) {
            return tp.IsSubClass(this);
        }
    }

    partial class TType {
        public TField FindField(string name) {
            var v = from f in Fields where f.NameVar == name select f;
            if (v.Any()) {
                return v.First();
            }

            foreach (TType super_class in SuperClasses) {
                TField fld = super_class.FindField(name);
                if (fld != null) {
                    return fld;
                }
            }

            return null;
        }

        /*
            関数名と引数の数と型が一致したらtrueを返します。
        */
        public bool MatchMethod(MethodInfo method, List<TType> arg_types, bool exact) {
            ParameterInfo[] param_list = method.GetParameters();
            if (arg_types.Count !=  param_list.Length) {
                // 引数の数が違う場合

                return false;
            }

            for (int i = 0; i < param_list.Length; i++) {

                if(arg_types[i].Info == null) {
                    return false;
                }

                ParameterInfo param = param_list[i];
                TypeInfo param_type_info = param.ParameterType.GetTypeInfo();

                if (!(param_type_info == arg_types[i].Info || !exact && arg_types[i].Info.IsSubclassOf(param.ParameterType))) {
                    return false;
                }
            }

            return true;
        }

        public TFunction FindMethod(TypeInfo tp, string name, List<TType> arg_types) {
            var v3 = from f in tp.DeclaredMethods where f.Name == name && MatchMethod(f, arg_types, true) select f;
            if (v3.Any()) {
                return new TFunction(this, v3.First());
            }

            var v4 = from f in tp.DeclaredMethods where f.Name == name && MatchMethod(f, arg_types, false) select f;
            if (v4.Any()) {
                return new TFunction(this, v4.First());
            }

            if(tp.BaseType != null) {
                TFunction fnc = FindMethod(tp.BaseType.GetTypeInfo(), name, arg_types);
                if(fnc != null) {
                    return fnc;
                }
            }

            if(tp.ImplementedInterfaces != null) {
                foreach(Type tp2 in tp.ImplementedInterfaces) {

                    TFunction fnc = FindMethod(tp2.GetTypeInfo(), name, arg_types);
                    if (fnc != null) {
                        return fnc;
                    }
                }
            }

            return null;
        }

        public TFunction MatchFunction(string name, List<TType> arg_types) {
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

            foreach (TType super_class in SuperClasses) {
                TFunction fnc = super_class.MatchFunction(name, arg_types);
                if(fnc != null) {
                    return fnc;
                }
            }

            if(Info != null) {

                return FindMethod(Info, name, arg_types);
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
            foreach(TType cls in classes) {
                cls.ResolveName();
            }
        }
    }
}