using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MyEdit {
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
        SpecializedClass
    }


    public enum EAggregateFunction {
        Sum,
        Max,
        Min,
        Average
    }

    public class TEnv {
        [ThreadStatic]
        public static TProject Project;

        [ThreadStatic]
        public static TParser Parser;
    }

    public class TUsing {
        public List<string> Packages = new List<string>();
    }

    public class TNamespace {
        public string NamespaceName;

        public TNamespace(string name) {
            NamespaceName = name;
        }
    }

    //------------------------------------------------------------ TType

    public partial class TType : TEnv {
        public EClass KindClass = EClass.Class;
        public EGeneric GenericType = EGeneric.SimpleClass;
        public string ClassName;
        public string ClassText = null;
        public string DelegateText = null;
        public TFunction DelegateFnc;
        public TypeInfo Info;
        public bool TypeInfoSearched;

        public TType RetType;
        public TType[] ArgTypes;

        public List<TType> SuperClasses = new List<TType>();
        public List<TField> Fields = new List<TField>();
        public List<TFunction> Functions = new List<TFunction>();

        public TType() {
        }

        public TType(string name) {
            ClassName = name;
        }

        public TType(TypeInfo info) {
            Info = info;
        }

        public TType(string name, TType ret_type, TType[] arg_types) {
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

            if (this == Project.StringClass) {
                return Project.CharClass;
            }
            if(this is TGenericClass) {
                TGenericClass gen = this as TGenericClass;

                if (gen.GenCla[0].ClassName == "T") {
                    return null;
                }
                if (ClassName == "List" || ClassName == "Array" || ClassName == "IEnumerable") {

                    return gen.GenCla[0];
                }
                else if (ClassName == "Dictionary") {

                    return gen.GenCla[1];
                }
            }
            if(Info != null) {
                if (Info.IsArray) {

                    int k = Info.Name.IndexOf('[');
                    string name = Info.Name.Substring(0, k);
                    tp = Project.GetClassByName(name);
                    if (tp != null) {
                        return tp;
                    }
                }

                Type t = Info.GenericTypeArguments[0];
                tp = Project.GetClassByName(t.Name);
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
            TProject p = Project;
            if(this == p.IntClass || this == p.FloatClass || this == p.DoubleClass || this == p.CharClass || this == p.BoolClass) {
                return true;
            }

            return false;
        }
    }

    public class TGenericClass : TType {
        public TGenericClass OrgCla;
        public bool ContainsArgumentClass;
        public int DimCnt;
        public bool SetMember;

        public List<TType> GenCla;

        public TGenericClass(string name, List<TType> arg_classes) : base(name) {
            GenCla = arg_classes;
        }

        public TGenericClass(TGenericClass org_class, List<TType> arg_classes, int dim_cnt) : base(org_class.ClassName) {
            OrgCla = org_class;
            GenCla = arg_classes;
            DimCnt = dim_cnt;
        }

        public override string GetClassText() {
            if(ClassText == null) {

                ClassText = Project.MakeClassText(ClassName, GenCla, DimCnt);
            }

            return ClassText;
        }
    }

    //------------------------------------------------------------ TVariable

    public partial class TVariable : TEnv {
        public TToken TokenVar;
        public string NameVar;
        public TType TypeVar;
        public TTerm InitValue;
        public EKind KindVar;

        public TVariable() {
        }

        public TVariable(TToken name) {
            TokenVar = name;
            NameVar = name.TextTkn;
        }

        public TVariable(TToken name, TType type, TTerm init) {
            TokenVar = name;
            NameVar = name.TextTkn;
            TypeVar = type;
            InitValue = init;
        }

        public TVariable(TToken name, TType type, EKind kind) {
            TokenVar = name;
            NameVar = name.TextTkn;
            TypeVar = type;
            KindVar = kind;
        }

        public TVariable(string name, TType type) {
            NameVar = name;
            TypeVar = type;
        }
    }

    public class TMember : TVariable {
        public bool IsStatic;
        public TType ClassMember;

        public TMember(bool is_static, TToken name, TType tp, TTerm init) : base(name, tp, init) {
            IsStatic = is_static;
        }

        public TMember() : base() {
        }
    }

    public class TField : TMember {
        public TField(TType parent_class, bool is_static, TToken name, TType tp, TTerm init) : base(is_static, name, tp, init) {
        }

        public TField(TType parent_class, FieldInfo fld_info) {
            NameVar = fld_info.Name;
            TypeVar = Project.GetSysClass(fld_info.FieldType.GetTypeInfo());
        }

        public TField(TType parent_class, PropertyInfo fld_info) {
            NameVar = fld_info.Name;
            TypeVar = Project.GetSysClass(fld_info.PropertyType.GetTypeInfo());
        }

        public TField(TType parent_class, EventInfo fld_info) {
            NameVar = fld_info.Name;
            TypeVar = Project.GetSysClass(fld_info.EventHandlerType.GetTypeInfo());
        }
    }

    public partial class TFunction : TMember {
        public TVariable[] ArgsFnc;
        public TApply BaseApp;
        public TBlock BlockFnc = new TBlock();
        public TTerm LambdaFnc;
        public MethodInfo InfoFnc;

        public TFunction(bool is_static, TToken name, TVariable[]args, TType ret_type, TApply base_app) : base(is_static, name, ret_type, null) {
            ArgsFnc = args;
            TypeVar = ret_type;
            BaseApp = base_app;
        }

        public TFunction(TToken name, TTerm trm) : base() {
            ArgsFnc = new TVariable[1];
            ArgsFnc[0] = new TVariable(name);
            LambdaFnc = trm;
        }

        public TFunction(TType parent_class, MethodInfo method_info) : base() {
            InfoFnc = method_info;
            TypeVar = Project.GetSysClass(method_info.ReturnType.GetTypeInfo());
        }
    }

    //------------------------------------------------------------ TTerm

    public abstract partial class TTerm : TEnv {
        public object ParentTrm;
        public TToken TokenTrm;
        public TType CastType;
        public TType TypeTrm;
        public bool WithParenthesis;
        public bool IsType;
    }

    public partial class TLiteral : TTerm {
        public TLiteral(TToken tkn) {
            TokenTrm = tkn;
        }
    }

    public partial class TReference : TTerm {
        public string NameRef;
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

    public class TDotReference : TReference {
        public TTerm DotRef;

        public TDotReference(TTerm trm, TToken name) : base(name) {
            DotRef = trm;
        }
    }

    public partial class TApply : TTerm {
        public EKind KindApp;
        public TTerm FunctionApp;
        public TTerm[] Args;

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

    public class TNewApply : TApply {
        public TType ClassApp;
        public List<TTerm> InitList;

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

    public partial class TQuery : TTerm {
        public TVariable VarQry;
        public TTerm SeqQry;
        public TTerm CndQry;
    }

    public partial class TFrom : TQuery {
        public TTerm SelFrom;
        public TTerm TakeFrom;
        public TTerm InnerFrom;
    }

    public partial class TAggregate : TQuery {
        public TTerm IntoAggr;
    }

    //------------------------------------------------------------ TStatement

    public abstract partial class TStatement : TEnv {
        public object ParentStmt;
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
        public List<TVariable> Variables = new List<TVariable>();
    }

    public abstract partial class TBlockStatement : TStatement {
        public List<TStatement> StatementsBlc = new List<TStatement>();
    }

    public partial class TBlock : TBlockStatement {
        public List<TVariable> VariablesBlc = new List<TVariable>();
    }

    public partial class TIfBlock : TBlockStatement {
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

        public TJump(EKind kind) {
            KindJmp = kind;
        }
    }

    public class TLabelStatement : TStatement {
        public TToken LabelToken;

        public TLabelStatement(TToken lbl) {
            LabelToken = lbl;
        }
    }

    public class TAttribute : TStatement {
        public TType Attr;
        public TAttribute(TType attr) {
            Attr = attr;
        }
    }
}