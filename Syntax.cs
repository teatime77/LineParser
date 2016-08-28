using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Miyu {

    public enum ELanguage {
        TypeScript,
        CSharp,
        JavaScript,
        Java,
        Basic,
    }

    public enum EClass {
        Class,
        Enum,
        Struct,
        Interface,
        Delegate,
    }

    public enum EGeneric {
        UnknownClass,
        SimpleClass,
        ParameterizedClass,
        ArgumentClass,
        SpecializedClass,
    }

    public enum EAggregateFunction {
        Sum,
        Max,
        Min,
        Average,
    }

    public class _weak : Attribute {
    }

    public class TEnv {
        [ThreadStatic]
        public static TProject Project;

        [ThreadStatic]
        public static TParser Parser;

        [ThreadStatic]
        public static TFunction LambdaFunction;

        [ThreadStatic]
        public static bool InLambdaFunction;
    }

    public class TUsing {
        public List<string> Packages = new List<string>();
    }

    public class TNamespace {
        public TToken[] CommentNS;
        public string NamespaceName;

        public TNamespace(string name) {
            NamespaceName = name;
        }
    }

    public class TSys {
        public static IEnumerable<int> Indexes(int cnt) {
            for(int i = 0; i < cnt; i++) {
                yield return i;
            }
        }
    }

    //-------------------------------------------------------------------------------- TModifier
    public class TModifier {
        public bool ValidMod;
        public bool isPublic;
        public bool isPrivate;
        public bool isPartial;
        public bool isStatic;
        public bool isConst;
        public bool isOverride;
        public bool isAbstract;
        public bool isVirtual;
        public bool isSealed;
        public bool isAsync;

        public bool isRef;
        public bool isOut;
        public bool isParams;

        public bool isIterator;
        public bool isWeak;
        public bool isParent;
        public bool isPrev;
        public bool isNext;
        public bool isInvariant;

        public bool isXmlIgnore;

        public List<TToken> TokenListMod;

        public bool isStrong() {
            return ! isWeak && ! isParent && ! isPrev && !  isNext;
        }
    }

    //------------------------------------------------------------ TType

    public partial class TType {
        public List<TField> Fields = new List<TField>();
        public List<TFunction> Functions = new List<TFunction>();

        [_weak]
        public static int CountClass;
        public int IdxClass;
        public TToken[] CommentCls;
        public TModifier ModifierCls;
        public EClass KindClass = EClass.Class;
        public EGeneric GenericType = EGeneric.SimpleClass;
        public string ClassName;
        public string ClassText = null;
        public string DelegateText = null;
        public TFunction DelegateFnc;
        public TypeInfo Info;
        public bool TypeInfoSearched;
        public TSourceFile SourceFileCls;

        public TType RetType;
        public TType[] ArgTypes;

        public List<TType> SuperClasses = new List<TType>();

        public List<TType> SubClasses = new List<TType>();

        void SetIdxClass() {
            IdxClass = CountClass;
            CountClass++;
        }

        public TType(string name) {
            if(name == "Predicate" || name == "ThreadStatic" || name == "DoubleTappedRoutedEventArgs") {
                Debug.WriteLine("");
            }
            SetIdxClass();
            ClassName = name;
        }

        public TType(TypeInfo info) {
            Debug.Assert(info != null);
            SetIdxClass();
            Info = info;
        }

        public TType(string name, TType ret_type, TType[] arg_types) {
            SetIdxClass();
            KindClass = EClass.Delegate;
            ClassName = name;
            RetType = ret_type;
            ArgTypes = arg_types;
        }

        public virtual string GetClassText() {
            return ClassName;
        }

        public string GetDelegateText() {
            if(DelegateText == null) {

                StringWriter sw = new StringWriter();

                sw.Write(RetType.GetClassText());
                sw.Write(" (");

                foreach (TType t in ArgTypes) {
                    if (t != ArgTypes.First()) {
                        sw.Write(",");
                    }
                    sw.Write(t.GetClassText());
                }
                sw.Write(")");

                DelegateText = sw.ToString();
            }

            return DelegateText;
        }

        public TType ElementType() {
            TType tp;

            if (this == TEnv.Project.StringClass) {
                return TEnv.Project.CharClass;
            }
            if(this is TGenericClass) {
                TGenericClass gen = this as TGenericClass;

                if (gen.ArgClasses[0].ClassName == "T") {
                    return null;
                }
                if (ClassName == "List" || ClassName == "Stack" || ClassName == "Array" || ClassName == "IEnumerable" || ClassName == "Enumerable") {

                    return gen.ArgClasses[0];
                }
                else if (ClassName == "Dictionary") {

                    return gen.ArgClasses[1];
                }
            }
            if(Info != null) {
                if (Info.IsArray) {

                    int k = Info.Name.IndexOf('[');
                    string name = Info.Name.Substring(0, k);
                    tp = TEnv.Project.GetClassByName(name);
                    if (tp != null) {
                        return tp;
                    }
                }

                Type t = Info.GenericTypeArguments[0];
                tp = TEnv.Project.GetClassByName(t.Name);
                if (tp != null) {
                    return tp;
                }
            }

            return null;
        }

        public bool IsPrimitive() {
            //if(Info != null && ! Info.IsSubclassOf(typeof(object))) {

            //    return true;
            //}
            TProject p = TEnv.Project;
            if(KindClass == EClass.Enum || this == p.IntClass || this == p.FloatClass || this == p.DoubleClass || this == p.CharClass || this == p.BoolClass) {
                return true;
            }

            return false;
        }

        public IEnumerable<TType> AncestorSuperClasses() {
            foreach (TType t1 in SuperClasses) {
                yield return t1;

                foreach(TType t2 in t1.AncestorSuperClasses()) {
                    yield return t2;
                }
            }
        }

        public IEnumerable<TType> ThisAncestorSuperClasses() {
            yield return this;

            foreach (TType t2 in AncestorSuperClasses()) {
                yield return t2;
            }
        }


        public IEnumerable<TType> DescendantSubClasses() {
            foreach (TType t1 in SubClasses) {
                yield return t1;

                foreach (TType t2 in t1.DescendantSubClasses()) {
                    yield return t2;
                }
            }
        }

        public IEnumerable<TType> ThisDescendantSubClasses() {
            yield return this;

            foreach (TType t2 in DescendantSubClasses()) {
                yield return t2;
            }
        }

        public IEnumerable<TFunction> GetVirtualFunctions(TFunction fnc) {
            if (fnc.InfoFnc != null) {
                yield return fnc;
                yield break;
            }

            var v = from c in ThisAncestorSuperClasses() from f in c.Functions where f == fnc || f.NameVar == fnc.NameVar && f.IsEqualType(fnc) select f;
            if (v.Any()) {
                yield return v.First();
            }

            var v2 = from c in DescendantSubClasses() from f in c.Functions where f == fnc || f.NameVar == fnc.NameVar && f.IsEqualType(fnc) select f;
            foreach(TFunction f in v2) {
                yield return f;
            }
        }
    }

    public class TGenericClass : TType {
        public bool ContainsArgumentClass;
        public int DimCnt;
        public bool SetMember;

        [_weak]
        public TGenericClass OrgCla;
        public List<TType> ArgClasses;

        public TGenericClass(string name, List<TType> arg_classes) : base(name) {
            ArgClasses = arg_classes;
        }

        public TGenericClass(TGenericClass org_class, List<TType> arg_classes, int dim_cnt) : base(org_class.ClassName) {
            OrgCla = org_class;
            ArgClasses = arg_classes;
            DimCnt = dim_cnt;
        }

        public override string GetClassText() {
            if(ClassText == null) {

                ClassText = TEnv.Project.MakeClassText(ClassName, ArgClasses, DimCnt);
            }

            return ClassText;
        }
    }

    //------------------------------------------------------------ TVariable

    public partial class TVariable {
        public TModifier ModifierVar;
        public string NameVar;
        public TTerm InitValue;
        public List<TAttribute> Attributes;

        [_weak]
        public List<TReference> RefsVar = new List<TReference>();
        public TToken TokenVar;
        public TType TypeVar;

        public TVariable() {
        }

        public TVariable(TToken name) {
            TokenVar = name;
            NameVar = name.TextTkn;
        }

        public TVariable(TModifier mod1, TToken name, TType type, TTerm init) {
            ModifierVar = mod1;
            TokenVar = name;
            NameVar = name.TextTkn;
            TypeVar = type;
            InitValue = init;
        }

        public TVariable(TToken name, TType type, EKind kind) {
            TokenVar = name;
            NameVar = name.TextTkn;
            TypeVar = type;

            if(kind != EKind.Undefined) {
                ModifierVar = new TModifier();
                switch (kind) {
                case EKind.ref_:
                    ModifierVar.isRef = true;
                    break;

                case EKind.out_:
                    ModifierVar.isOut = true;
                    break;

                case EKind.params_:
                    ModifierVar.isParams = true;
                    break;

                default:
                    Debug.Assert(false);
                    break;
                }
            }
        }

        public TVariable(string name, TType type) {
            NameVar = name;
            TypeVar = type;
        }

        public bool isParams() {
            return ModifierVar != null && ModifierVar.isParams;
        }
    }

    public class TMember : TVariable {
        public TToken[] CommentVar;

        [_weak]
        public TType ClassMember;

        public TMember(TModifier mod1, TToken name, TType tp, TTerm init) : base(mod1, name, tp, init) {
        }

        public TMember() : base() {
        }
    }

    public class TField : TMember {
        public bool IsWeak;

        public TField(TType parent_class, TModifier mod1, TToken name, TType tp, TTerm init) : base(mod1, name, tp, init) {
            ClassMember = parent_class;
        }

        public TField(TType parent_class, FieldInfo fld_info) {
            ClassMember = parent_class;
            NameVar = fld_info.Name;
            TypeVar = TEnv.Project.GetSysClass(fld_info.FieldType.GetTypeInfo());
        }

        public TField(TType parent_class, PropertyInfo fld_info) {
            ClassMember = parent_class;
            NameVar = fld_info.Name;
            TypeVar = TEnv.Project.GetSysClass(fld_info.PropertyType.GetTypeInfo());
        }

        public TField(TType parent_class, EventInfo fld_info) {
            ClassMember = parent_class;
            NameVar = fld_info.Name;
            TypeVar = TEnv.Project.GetSysClass(fld_info.EventHandlerType.GetTypeInfo());
        }
    }

    public partial class TFunction : TMember {
        // メソッドの引数リスト
        public TVariable[] ArgsFnc;

        // メソッドの本体
        public TBlock BlockFnc = new TBlock();

        public static int LambdaCnt;
        public EKind KindFnc;

        // コンストラクタのbase呼び出し
        public TApply BaseApp;

        public TTerm LambdaFnc;

        // メソッドを一意に識別する文字列(クラス名は含まない)
        public string FunctionSignature = null;

        [_weak]
        public MethodInfo InfoFnc;

        // メソッド内の参照のリスト
        public List<TReference> ReferencesInFnc = new List<TReference>();

        // メソッド内のメソッド呼び出しのリスト
        public List<TApply> AppsInFnc = new List<TApply>();

        public TFunction(TModifier mod1, TToken name, TVariable[]args, TType ret_type, TApply base_app, EKind kind) : base(mod1, name, ret_type, null) {
            KindFnc = kind;
            ArgsFnc = args;
            TypeVar = ret_type;
            BaseApp = base_app;
        }

        public TFunction(TToken lambda) : base() {
            KindFnc = EKind.Lambda;
            TokenVar = lambda;
            ArgsFnc = new TVariable[0];
        }

        public TFunction(TToken name, TTerm trm) : base() {
            KindFnc = EKind.Lambda;
            ArgsFnc = new TVariable[1];
            ArgsFnc[0] = new TVariable(name);
            LambdaFnc = trm;
        }

        public TFunction(TType parent_class, MethodInfo method_info) : base() {
            KindFnc = EKind.Undefined;
            InfoFnc = method_info;
            TypeVar = TEnv.Project.GetSysClass(method_info.ReturnType.GetTypeInfo());
        }

        /*
         * メソッドを一意に識別する文字列(クラス名は含まない)を返す。
        */
        public string GetFunctionSignature() {
            if(FunctionSignature == null) {
                StringWriter sw = new StringWriter();

                if(KindFnc == EKind.Lambda) {

                    sw.Write("Lambda-{0}", LambdaCnt);
                    LambdaCnt++;
                }
                else {

                    sw.Write(NameVar);
                }
                sw.Write('(');
                foreach (TVariable va in ArgsFnc) {
                    if(va != ArgsFnc[0]) {
                        sw.Write(',');
                    }

                    if(va.TypeVar != null) {
                        sw.Write(va.TypeVar.GetClassText());
                    }
                    else {
                        sw.Write("any");
                    }
                }
                sw.Write(')');

                FunctionSignature = sw.ToString();
            }

            return FunctionSignature;
        }

        /*
         メソッドを一意に識別する文字列(クラス名は含む)を返す。
        */
        public string FullName() {
            if(ClassMember == null) {
                Debug.Assert(KindFnc == EKind.Lambda);
                return GetFunctionSignature();
            }
            else {

                return ClassMember.GetClassText() + "." + GetFunctionSignature();
            }
        }

        public string UniqueName() {
            var vfnc = from x in ClassMember.Functions where x.NameVar == NameVar select x;
            Debug.Assert(vfnc.Any());
            if(vfnc.Count() == 1) {
                return NameVar;
            }
            int idx = vfnc.ToList().IndexOf(this);
            return string.Format("{0}.{1}", NameVar, idx);
        }

        /*
         * 戻り値の型と引数の数と型が同じならtrueを返す。
        */
        public bool IsEqualType(TFunction fnc) {
            if(TypeVar != fnc.TypeVar) {
                // 戻り値の型が違う場合

                return false;
            }

            if(ArgsFnc.Length != fnc.ArgsFnc.Length) {
                // 引数の数が違う場合

                return false;
            }

            // すべての引数に対し
            for(int i = 0; i < ArgsFnc.Length; i++) {
                if(ArgsFnc[i].TypeVar != fnc.ArgsFnc[i].TypeVar) {
                    // 引数の型が違う場合

                    return false;
                }
            }

            return true;
        }
    }

    //------------------------------------------------------------ TTerm

    public abstract partial class TTerm {
        public bool WithParenthesis;
        public bool IsType;

        [_weak]
        public TStatement ParentStatementTrm;
        public object ParentTrm;
        public TToken TokenTrm;
        public TType CastType;
        public TType TypeTrm;
    }

    public partial class TLiteral : TTerm {
        public TLiteral(TToken tkn) {
            TokenTrm = tkn;
        }
    }

    /*
     * 変数参照
     */
    public partial class TReference : TTerm {
        public string NameRef;
        public bool IsOut;
        public bool IsRef;
        public bool Defined;

        [_weak]
        public TVariable VarRef;
        public TType ClassRef;

        public TReference(TToken name) {
            TokenTrm = name;
            NameRef = name.TextTkn;
        }

        public TReference(TType cls) {
            NameRef = cls.ClassName;
            ClassRef = cls;
        }

        public TReference(TVariable var1) {
            VarRef = var1;
        }
    }

    /*
     * ドットつき変数参照
     */
    public class TDotReference : TReference {
        public TTerm DotRef;

        public TDotReference(TTerm trm, TToken name) : base(name) {
            DotRef = trm;
        }
    }

    /*
     * 関数呼び出し
     */
    public partial class TApply : TTerm {
        public EKind KindApp;
        public TTerm[] Args;

        [_weak]
        public TTerm FunctionApp;

        public TApply() {
        }

        public TApply(TToken fnc, params TTerm[] args) {
            TokenTrm = fnc;
            if(fnc.Kind == EKind.Identifier) {

                KindApp = EKind.FunctionApply;
                FunctionApp = new TReference(fnc);
            }
            else {

                KindApp = fnc.Kind;
            }
            Args = args;
        }

        public TApply(EKind kind, TToken function_name, TTerm[] args) {
            TokenTrm = function_name;
            KindApp = kind;
            Debug.Assert(kind == EKind.base_);
            FunctionApp = new TReference(function_name);
            Args = args;
        }

        public TApply(TToken lb, TTerm function_app, TTerm[] args) {
            Debug.Assert(lb.Kind == EKind.LB);
            TokenTrm = lb;
            KindApp = EKind.Index;
            FunctionApp = function_app;
            Args = args;
        }
    }

    /*
     * ドットつき関数呼び出し
     */
    public partial class TDotApply : TApply {
        public TTerm DotApp;
        public TType DotClass;

        public TDotApply(TTerm trm, TToken fnc, TTerm[] args) : base(fnc, args) {
            DotApp = trm;
        }

        public TDotApply(TType cls, TToken fnc, TTerm[] args) : base(fnc, args) {
            DotClass = cls;
        }
    }

    /*
     * new呼び出し
     */
    public class TNewApply : TApply {
        public List<TTerm> InitList;

        [_weak]
        public TType ClassApp;

        public TNewApply(EKind kind, TToken class_token, TType cls, TTerm[] args, List<TTerm> init) {
            TokenTrm = class_token;
            Debug.Assert(kind == EKind.NewInstance || kind == EKind.NewArray);
            KindApp = kind;
            Args = args;

            if(init != null) {
                InitList = init;
            }
            else {
                InitList = new List<TTerm>();
            }

            ClassApp = cls;
        }
    }

    /*
     * from句
     */
    public partial class TFrom : TTerm {
        public TVariable VarQry;
        public TTerm SeqQry;
        public TTerm CndQry;
        public TTerm SelFrom;
        public TFrom InnerFrom;
    }

    //------------------------------------------------------------ TStatement

    public abstract partial class TStatement {
        public TToken[] CommentStmt;

        [_weak]
        public object ParentStmt;
        public TStatement PrevStatement;
        public TFunction ParentFunctionStmt;
    }

    public partial class TAssignment : TStatement {
        public TApply RelAsn;

        public TAssignment(TApply rel) {
            RelAsn = rel;
        }
    }

    public partial class TCall : TStatement {
        public TApply AppCall;

        public TCall(TApply app) {
            AppCall = app;
        }
    }

    public partial class TVariableDeclaration : TStatement {
        public bool IsVar;
        public List<TVariable> Variables = new List<TVariable>();
    }

    public abstract partial class TBlockStatement : TStatement {
        public List<TStatement> StatementsBlc = new List<TStatement>();
    }

    public partial class TBlock : TBlockStatement {
    }

    public partial class TIfBlock : TBlockStatement {
        public TToken CommentIf;
        public bool IsElse;
        public TTerm ConditionIf;
    }

    public partial class TIf : TStatement {
        public List<TIfBlock> IfBlocks = new List<TIfBlock>();
    }

    public partial class TCase : TBlockStatement {
        public bool IsDefault;
        public List<TTerm> TermsCase = new List<TTerm>();
    }

    public partial class TSwitch : TStatement {
        public TTerm TermSwitch;
        public List<TCase> Cases = new List<TCase>();
    }

    public partial class TTry : TBlockStatement {
    }

    public partial class TCatch : TBlockStatement {
        public TVariable CatchVariable;

        public TCatch(TVariable var1) {
            CatchVariable = var1;
        }
    }

    public partial class TWhile : TBlockStatement {
        public TTerm WhileCondition;
    }

    public partial class TLock : TBlockStatement {
        public TTerm LockObj;
    }

    public abstract class TAbsFor : TBlockStatement {
        public TVariable LoopVariable;
    }

    public partial class TForEach : TAbsFor {
        public TTerm ListFor;
    }

    public partial class TFor : TAbsFor {
        public TStatement InitStatement;
        public TTerm ConditionFor;
        public TStatement PostStatement;
    }

    public partial class TJump : TStatement {
        public EKind KindJmp;
        public TTerm RetVal;
        public string LabelJmp;

        public TJump(EKind kind) {
            KindJmp = kind;
        }
    }

    public class TLabelStatement : TStatement {
        public string LabelToken;

        public TLabelStatement(TToken lbl) {
            LabelToken = lbl.TextTkn;
        }
    }

    public class TAttribute : TStatement {
        public TType Attr;

        public TAttribute(TType attr) {
            Attr = attr;
        }
    }
}
