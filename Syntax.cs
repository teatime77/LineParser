using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Miyu {

    /*
     * プログラム言語
     */
    public enum ELanguage {
        TypeScript,
        CSharp,
        JavaScript,
        Java,
        Basic,
    }

    /*
     * 型の種類
     */
    public enum EType {
        Class,
        Enum,
        Struct,
        Interface,

        // Delegateクラスと区別するため
        Delegate_,
    }

    /*
     * クラスの種類
     */
    public enum EClass {
        SimpleClass,
        ParameterizedClass,
        ParameterClass,
        SpecializedClass,
        UnspecializedClass,
    }

    /*
     * 弱参照の属性
     */
    public class _weak : Attribute {
    }

    /*
     * 大域変数
     */
    public class TGlb {
        [ThreadStatic]
        public static TProject Project;

        [ThreadStatic]
        public static TParser Parser;

        [ThreadStatic]
        public static TFunction LambdaFunction;

        [ThreadStatic]
        public static bool InLambdaFunction;

        [ThreadStatic]
        public static TApply ApplyLambda;

        // ラムダ関数の開始行のインデント
        [ThreadStatic]
        public static int LambdaFunctionIndent;
    }

    /*
     * using文
     */
    public class TUsing {
        public List<string> Packages = new List<string>();
    }

    /*
     * namespace
     */
    public class TNamespace {
        public TToken[] CommentNS;
        public string NamespaceName;

        public TNamespace(string name) {
            NamespaceName = name;
        }
    }

    public class TSys {
        /*
         * 添え字の列挙を得る。現在は未使用。
         */
        public static IEnumerable<int> Indexes(int cnt) {
            for(int i = 0; i < cnt; i++) {
                yield return i;
            }
        }
    }

    /*
     * 修飾子
     */
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

    /*
     * 型
     */
    public partial class TType {
        public List<TField> Fields = new List<TField>();
        public List<TFunction> Functions = new List<TFunction>();

        [_weak]
        public static int CountClass;
        public int IdxClass;
        public TToken[] CommentCls;
        public TModifier ModifierCls;
        public EType KindClass = EType.Class;
        public EClass GenericType = EClass.SimpleClass;
        public string ClassName;
        public string ClassText = null;
        public string DelegateText = null;
        public TFunction DelegateFnc;
        public TypeInfo Info;
        public bool TypeInfoSearched;
        public TSourceFile SourceFileCls;

        public TType RetType;
        public TType[] ArgTypes;

        // 親クラスのリスト
        public List<TType> SuperClasses = new List<TType>();

        // 子クラスのリスト
        public List<TType> SubClasses = new List<TType>();

        void SetIdxClass() {
            IdxClass = CountClass;
            CountClass++;
        }

        public TType(string name) {
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
            KindClass = EType.Delegate_;
            ClassName = name;
            RetType = ret_type;
            ArgTypes = arg_types;
        }

        public virtual string GetClassText() {
            return ClassName;
        }

        /*
         * デリゲートの文字列を得る。
         */
        public string DelegateString() {
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

        /*
         * 要素の型を得る。
         */
        public TType ElementType() {
            TType tp;

            if (this == TGlb.Project.StringClass) {
                return TGlb.Project.CharClass;
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
                // TypeInfoがある場合

                if (Info.IsArray) {
                    // 配列の場合

                    int k = Info.Name.IndexOf('[');
                    string name = Info.Name.Substring(0, k);
                    tp = TGlb.Project.GetClassByName(name);
                    if (tp != null) {
                        return tp;
                    }
                }

                Type t = Info.GenericTypeArguments[0];
                tp = TGlb.Project.GetClassByName(t.Name);
                if (tp != null) {
                    return tp;
                }
            }

            return null;
        }

        /*
         * プリミティブ型ならtrue
         */
        public bool IsPrimitive() {
            //if(Info != null && ! Info.IsSubclassOf(typeof(object))) {

            //    return true;
            //}
            TProject p = TGlb.Project;
            if(KindClass == EType.Enum || this == p.IntClass || this == p.FloatClass || this == p.DoubleClass || this == p.CharClass || this == p.BoolClass) {
                return true;
            }

            return false;
        }

        /*
         * このクラスが引数の子孫クラスならtrue
         */
        public bool IsSubClass(TType tp) {
            if (tp == TGlb.Project.ObjectClass) {
                return !IsPrimitive();
            }
            if (SuperClasses.Contains(tp)) {
                return true;
            }
            foreach (TType super_class in SuperClasses) {
                if (super_class.IsSubClass(tp)) {
                    return true;
                }
            }

            return false;
        }

        /*
         * このクラスが引数の先祖クラスならtrue
         */
        public bool IsSuperClass(TType tp) {
            return tp.IsSubClass(this);
        }

        /*
         * t1がt2の子孫クラスならtrue
         */
        bool IsSubclassOf(TypeInfo t1, Type t2) {
            if (t1.IsSubclassOf(t2)) {
                return true;
            }

            if (t1.Name == "Int16" || t1.Name == "Int32") {
                if (t2.Name == "Single" || t2.Name == "Double") {
                    return true;
                }
            }

            return false;
        }

        /*
         * 先祖クラスのリスト
         */
        public IEnumerable<TType> AncestorSuperClasses() {
            foreach (TType t1 in SuperClasses) {
                yield return t1;

                foreach(TType t2 in t1.AncestorSuperClasses()) {
                    yield return t2;
                }
            }
        }

        /*
         * このクラスと先祖クラスのリスト
         */
        public IEnumerable<TType> ThisAncestorSuperClasses() {
            yield return this;

            foreach (TType t2 in AncestorSuperClasses()) {
                yield return t2;
            }
        }

        /*
         * 子孫クラスのリスト
         */
        public IEnumerable<TType> DescendantSubClasses() {
            foreach (TType t1 in SubClasses) {
                yield return t1;

                foreach (TType t2 in t1.DescendantSubClasses()) {
                    yield return t2;
                }
            }
        }

        /*
         * このクラスと子孫クラスのリスト
         */
        public IEnumerable<TType> ThisDescendantSubClasses() {
            yield return this;

            foreach (TType t2 in DescendantSubClasses()) {
                yield return t2;
            }
        }

        /*
         * 呼ばれうる仮想関数のリストを得る。
         */
        public IEnumerable<TFunction> GetVirtualFunctions(TFunction fnc) {
            if (fnc.InfoFnc != null) {
                // MethodInfoがある場合

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

    /*
     * 総称型
     */
    public class TGenericClass : TType {
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

                ClassText = TGlb.Project.MakeClassText(ClassName, ArgClasses, DimCnt);
            }

            return ClassText;
        }
    }

    /*
     * 変数
     */
    public partial class TVariable {
        public TModifier ModifierVar;
        public string NameVar;

        // 初期値
        public TTerm InitValue;

        // 属性のリスト
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

        /*
         * 可変長引数ならtrue
         */
        public bool isParams() {
            return ModifierVar != null && ModifierVar.isParams;
        }
    }

    /*
     * クラスのメンバー(フィールドか関数)
     */
    public class TMember : TVariable {
        public TToken[] CommentVar;

        [_weak]
        public TType DeclaringType;

        public TMember(TModifier mod1, TToken name, TType tp, TTerm init) : base(mod1, name, tp, init) {
        }

        public TMember() : base() {
        }
    }

    /*
     * フィールド
     */
    public class TField : TMember {
        public bool IsWeak;

        public TField(TType declaring_type, TModifier mod1, TToken name, TType tp, TTerm init) : base(mod1, name, tp, init) {
            DeclaringType = declaring_type;
        }

        public TField(TType declaring_type, FieldInfo fld_info) {
            DeclaringType = declaring_type;
            NameVar = fld_info.Name;
            TypeVar = TGlb.Project.RegisterTypeInfoTable(fld_info.FieldType.GetTypeInfo());
        }

        public TField(TType declaring_type, PropertyInfo fld_info) {
            DeclaringType = declaring_type;
            NameVar = fld_info.Name;
            TypeVar = TGlb.Project.RegisterTypeInfoTable(fld_info.PropertyType.GetTypeInfo());
        }

        public TField(TType declaring_type, EventInfo fld_info) {
            DeclaringType = declaring_type;
            NameVar = fld_info.Name;
            TypeVar = TGlb.Project.RegisterTypeInfoTable(fld_info.EventHandlerType.GetTypeInfo());
        }
    }

    /*
     * 関数
     */
    public partial class TFunction : TMember {
        // 関数の引数リスト
        public TVariable[] ArgsFnc;

        // 関数の本体
        public TBlock BlockFnc = new TBlock();

        public static int LambdaCnt;
        public EKind KindFnc;

        // コンストラクタのbase呼び出し
        public TApply BaseApp;

        public TTerm LambdaFnc;

        // 関数を一意に識別する文字列(クラス名は含まない)
        public string FunctionSignature = null;

        [_weak]
        public MethodInfo InfoFnc;

        // 関数内の参照のリスト
        public List<TReference> ReferencesInFnc = new List<TReference>();

        // 関数内の関数呼び出しのリスト
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
            TypeVar = TGlb.Project.RegisterTypeInfoTable(method_info.ReturnType.GetTypeInfo());
        }

        /*
         * 関数を一意に識別する文字列(クラス名は含まない)を得る。
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
         関数を一意に識別する文字列(クラス名は含む)を得る。
        */
        public string FullName() {
            if(DeclaringType == null) {
                Debug.Assert(KindFnc == EKind.Lambda);
                return GetFunctionSignature();
            }
            else {

                return DeclaringType.GetClassText() + "." + GetFunctionSignature();
            }
        }

        public string UniqueName() {
            var vfnc = from x in DeclaringType.Functions where x.NameVar == NameVar select x;
            Debug.Assert(vfnc.Any());
            if(vfnc.Count() == 1) {
                return NameVar;
            }
            int idx = vfnc.ToList().IndexOf(this);
            return string.Format("{0}.{1}", NameVar, idx);
        }

        /*
         * 戻り値の型と引数の数と型が同じならtrue
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

    /*
     * 項
     */
    public abstract partial class TTerm {
        // カッコ()で囲まれていればtrue
        public bool WithParenthesis;

        public bool IsType;

        [_weak]
        public TStatement ParentStatementTrm;
        public object ParentTrm;
        public TToken TokenTrm;
        public TType CastType;
        public TType TypeTrm;
    }

    /*
     * リテラル
     */
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

    /*
     * 文
     */
    public abstract partial class TStatement {
        public TToken[] CommentStmt;

        [_weak]
        public object ParentStmt;
        public TStatement PrevStatement;
        public TFunction ParentFunctionStmt;
    }

    /*
     * 代入文
     */
    public partial class TAssignment : TStatement {
        public TApply RelAsn;

        public TAssignment(TApply rel) {
            RelAsn = rel;
        }
    }

    /*
     * 関数呼び出し文
     */
    public partial class TCall : TStatement {
        public TApply AppCall;

        public TCall(TApply app) {
            AppCall = app;
        }
    }

    /*
     * 変数宣言文
     */
    public partial class TVariableDeclaration : TStatement {
        public bool IsVar;
        public List<TVariable> Variables = new List<TVariable>();
    }

    /*
     * 入れ子の文を持つ文の抽象型
     */
    public abstract partial class TBlockStatement : TStatement {
        public List<TStatement> StatementsBlc = new List<TStatement>();
    }

    /*
     * ブロック文
     */
    public partial class TBlock : TBlockStatement {
    }

    /*
     * if文
     */
    public partial class TIfBlock : TBlockStatement {
        public TToken CommentIf;
        public bool IsElse;
        public TTerm ConditionIf;
    }

    /*
     * case文
     */
    public partial class TCase : TBlockStatement {
        public bool IsDefault;
        public List<TTerm> TermsCase = new List<TTerm>();
    }

    /*
     * switch文
     */
    public partial class TSwitch : TStatement {
        public TTerm TermSwitch;
        public List<TCase> Cases = new List<TCase>();
    }

    /*
     * try文
     */
    public partial class TTry : TBlockStatement {
    }

    /*
     * catch文
     */
    public partial class TCatch : TBlockStatement {
        public TVariable CatchVariable;

        public TCatch(TVariable var1) {
            CatchVariable = var1;
        }
    }

    /*
     * while文
     */
    public partial class TWhile : TBlockStatement {
        public TTerm WhileCondition;
    }

    /*
     * lock文
     */
    public partial class TLock : TBlockStatement {
        public TTerm LockObj;
    }

    /*
     * for文かforeach文
     */
    public abstract class TAbsFor : TBlockStatement {
        public TVariable LoopVariable;
    }

    /*
     * foreach文
     */
    public partial class TForEach : TAbsFor {
        public TTerm ListFor;
    }

    /*
     * for文
     */
    public partial class TFor : TAbsFor {
        public TStatement InitStatement;
        public TTerm ConditionFor;
        public TStatement PostStatement;
    }

    /*
     * return, yield, throw, break, continue, goto
     */
    public partial class TJump : TStatement {
        public EKind KindJmp;
        public TTerm RetVal;
        public string LabelJmp;

        public TJump(EKind kind) {
            KindJmp = kind;
        }
    }

    /*
     * ラベル
     */
    public class TLabelStatement : TStatement {
        public string LabelToken;

        public TLabelStatement(TToken lbl) {
            LabelToken = lbl.TextTkn;
        }
    }

    /*
     * 属性
     */
    public class TAttribute : TStatement {
        public TType Attr;

        public TAttribute(TType attr) {
            Attr = attr;
        }
    }
}
