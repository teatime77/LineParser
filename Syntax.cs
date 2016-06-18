using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

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

    //------------------------------------------------------------ TClass

    public partial class TClass {
        public EClass KindClass = EClass.Class;
        public EGeneric GenericType = EGeneric.SimpleClass;
        public string ClassName;
        public string ClassText = null;

        public List<TClass> SuperClasses = new List<TClass>();
        public List<TField> Fields = new List<TField>();
        public List<TFunction> Functions = new List<TFunction>();

        public TClass(string name) {
            ClassName = name;
        }

        public virtual string GetClassText() {
            return ClassName;
        }
    }

    public class TGenericClass : TClass {
        public TGenericClass OrgCla;
        public bool ContainsArgumentClass;
        public int DimCnt;

        public List<TClass> GenCla;

        public TGenericClass(string name, List<TClass> arg_classes) : base(name) {
            GenCla = arg_classes;
        }

        public TGenericClass(TGenericClass org_class, List<TClass> arg_classes) : base(org_class.ClassName) {
            OrgCla = org_class;
            GenCla = arg_classes;
        }

        public TGenericClass(TClass element_class, int dim_cnt) : base(element_class.ClassName) {
            GenCla = new List<TClass>();
            GenCla.Add(element_class);
        }

        public override string GetClassText() {
            if(ClassText == null) {

                StringWriter sw = new StringWriter();

                sw.Write(ClassName);

                if(GenCla != null) {

                    sw.Write("<");
                    foreach (TClass c in GenCla) {
                        if (c != GenCla[0]) {
                            sw.Write(",");
                        }
                        sw.Write("{0}", c.GetClassText());
                    }
                    sw.Write(">");
                }

                if (DimCnt != 0) {

                    sw.Write("[{0}]", new string(',', DimCnt - 1));
                }

                ClassText = sw.ToString();
            }

            return ClassText;
        }
    }

    //------------------------------------------------------------ TVariable

    public partial class TVariable {
        public TToken TokenVar;
        public string NameVar;
        public TClass TypeVar;
        public TTerm InitValue;

        public TVariable(TToken name) {
            TokenVar = name;
            NameVar = name.TextTkn;
        }

        public TVariable(TToken name, TClass type, TTerm init) {
            TokenVar = name;
            NameVar = name.TextTkn;
            TypeVar = type;
            InitValue = init;
        }
    }

    public class TMember : TVariable {
        public bool IsStatic;
        public TClass ClassMember;

        public TMember(bool is_static, TToken name, TClass tp, TTerm init) : base(name, tp, init) {
            IsStatic = is_static;
        }
    }

    public class TField : TMember {

        public TField(bool is_static, TToken name, TClass tp, TTerm init) : base(is_static, name, tp, init) {
        }
    }

    public partial class TFunction : TMember {
        public TVariable[] ArgsFnc;
        public TBlock BlockFnc = new TBlock();

        public TFunction(bool is_static, TToken name, TVariable[]args, TClass ret_type) : base(is_static, name, ret_type, null) {
            ArgsFnc = args;
        }
    }

    //------------------------------------------------------------ TTerm

    public abstract partial class TTerm {
        public TToken TokenTrm;
        public TClass CastType;
        public TClass TypeTrm;
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

        public TReference(TToken name) {
            TokenTrm = name;
            NameRef = name.TextTkn;
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

        public TDotApply(TTerm trm, TToken fnc, TTerm[] args) : base(fnc, args) {
            DotApp = trm;
        }
    }

    public class TNewApply : TApply {
        public TClass ClassApp;

        public TNewApply(EKind kind, TToken class_token, TClass cls, TTerm[] args) {
            TokenTrm = class_token;
            Debug.Assert(kind == EKind.NewInstance || kind == EKind.NewArray);
            KindApp = kind;
            Args = args;

            ClassApp = cls;
        }
    }

    public partial class TQuery : TTerm {
    }

    public class TFrom : TQuery {
    }

    public class TAggregate : TQuery {
    }

    //------------------------------------------------------------ TStatement

    public abstract partial class TStatement {
        public void sub(TStatement stmt) {
            if (stmt is TVariableDeclaration) {
                TVariableDeclaration var_decl = stmt as TVariableDeclaration;
            }
            else if (stmt is TAssignment) {
                TAssignment asn = stmt as TAssignment;
            }
            else if (stmt is TCall) {
                TCall call1 = stmt as TCall;
            }
            else if (stmt is TJump) {
                TJump jmp = stmt as TJump;
            }
            else if (stmt is TBlockStatement) {
                TBlockStatement blc_stmt = stmt as TBlockStatement;

                if (stmt is TBlock) {
                    TBlock block = stmt as TBlock;
                }
                else if (stmt is TIfBlock) {
                    TIfBlock if_block = stmt as TIfBlock;
                }
                else if (stmt is TCase) {
                    TCase cas = stmt as TCase;
                }
                else if (stmt is TSwitch) {
                    TSwitch swt = stmt as TSwitch;
                }
                else if (stmt is TFor) {
                    TFor for1 = stmt as TFor;
                }
                else if (stmt is TWhile) {
                    TWhile while1 = stmt as TWhile;
                }
                else if (stmt is TTry) {
                    TTry try1 = stmt as TTry;
                }
                else if (stmt is TCatch) {
                    TCatch catch1 = stmt as TCatch;
                }
            }
        }
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
    }

    public partial class TWhile : TBlockStatement {
        public TTerm WhileCondition;
    }

    public partial class TFor : TBlockStatement {
        public TVariable LoopVariable;
        public TTerm ListFor;
    }

    public partial class TJump : TStatement {
        public EKind KindJmp;
        public TTerm RetVal;

        public TJump(EKind kind) {
            KindJmp = kind;
        }
    }

    public class TSourceFile {
        public List<TClass> ClassesSrc = new List<TClass>();
    }
}