using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace MyEdit {
    public abstract partial class TTerm {
        public bool WithParenthesis;
        public bool IsType;

        public virtual void Text(StringWriter sw, TParser parser) {
            Debug.Assert(false);
        }
    }

    public abstract partial class TType {
        public static TClass IndexClass = new TClass("_index_");

        public abstract void Text(StringWriter sw, TParser parser);
    }

    public class TFunctionType : TType {
        public TType ValType;
        public TType[] ArgsType;

        public TFunctionType(TType val_type, TType[] args_type) {
            ValType = val_type;
            ArgsType = args_type;
        }

        public override void Text(StringWriter sw, TParser parser) {
            ValType.Text(sw, parser);

            for (int i = 0; i < ArgsType.Length; i++) {
                if (i == 0) {

                    sw.Write("[");
                }
                else {

                    sw.Write(",");
                }

                TType tp = ArgsType[i];
                if(tp != TType.IndexClass) {

                    sw.Write("{0}", tp);
                }
            }

            sw.Write("]");
        }
    }

    public partial class TClass : TType {
        public string ClassName;
        public List<TClass> SuperClasses = new List<TClass>();
        public List<TField> Fields = new List<TField>();
        public List<TFunction> Functions = new List<TFunction>();

        public TClass(string name) {
            ClassName = name;
        }

        public override void Text(StringWriter sw, TParser parser) {
            sw.Write(ClassName);
        }

        public void ClassLine(StringWriter sw) {
            sw.Write("class {0}", ClassName);

            for (int i = 0; i < SuperClasses.Count; i++) {
                if (i == 0) {

                    sw.Write(" : ");
                }
                else {

                    sw.Write(" , ");
                }
                sw.Write(SuperClasses[i].ClassName);
            }
        }
    }

    public class TVariable {
        public string NameVar;
        public TType TypeVar;
        public TTerm InitValue;

        public TVariable(string name) {
            NameVar = name;
        }

        public TVariable(string name, TType type, TTerm init) {
            NameVar = name;
            TypeVar = type;
            InitValue = init;
        }

        public virtual void Text(StringWriter sw, TParser parser) {
            sw.Write(NameVar);

            if (TypeVar != null) {

                sw.Write(" : ");
                TypeVar.Text(sw, parser);
            }

            if (InitValue != null) {

                sw.Write(" = ");
                InitValue.Text(sw, parser);
            }
        }
    }

    public class TMember : TVariable {
        public bool IsStatic;
        public TClass ClassMember;

        public TMember(bool is_static, string name, TType tp, TTerm init) : base(name, tp, init) {
            IsStatic = is_static;
        }
    }

    public class TField : TMember {

        public TField(bool is_static, string name, TType tp, TTerm init) : base(is_static, name, tp, init) {
        }
    }

    public partial class TFunction : TMember {
        public TVariable[] ArgsFnc;
        public TBlock BlockFnc;

        public TFunction(bool is_static, string name, TVariable[]args, TType ret_type) : base(is_static, name, ret_type, null) {
            ArgsFnc = args;

        }


        public override void Text(StringWriter sw, TParser parser) {
            sw.Write("function ");

            sw.Write(NameVar);

            sw.Write("(");
            foreach(TVariable var1 in ArgsFnc) {
                if(var1 != ArgsFnc[0]) {
                    sw.Write(", ");
                }

                var1.Text(sw, parser);
            }
            sw.Write(")");

            if (TypeVar != null) {

                sw.Write(" : ");
                TypeVar.Text(sw, parser);
            }
        }

    }

    public partial class TLiteral : TTerm {
        public EKind KindLit;
        public string TextLit;

        public TLiteral(EKind kind, string text) {
            KindLit = kind;
            TextLit = text;
        }

        public override void Text(StringWriter sw, TParser parser) {
            sw.Write(TextLit);
        }
    }

    public partial class TReference : TTerm {
        public string NameRef;
        public TVariable VarRef;

        public TReference(string name) {
            NameRef = name;
        }

        public override void Text(StringWriter sw, TParser parser) {
            sw.Write(NameRef);
        }
    }

    public partial class TFieldReference : TReference {
        public TTerm TermFldRef;

        public TFieldReference(TTerm trm, string name) : base(name) {
            TermFldRef = trm;
        }

        public override void Text(StringWriter sw, TParser parser) {
            TermFldRef.Text(sw, parser);
            sw.Write(".{0}", NameRef);
        }
    }

    public partial class TApply : TTerm {
        EKind KindApp;
        public TReference FunctionRef;
        public TTerm[] Args;

        public TApply(EKind opr_kind, TTerm t1, TTerm t2) {
            KindApp = opr_kind;
            Args = new TTerm[2];
            Args[0] = t1;
            Args[1] = t2;
        }

        public TApply(EKind opr_kind, TTerm t1) {
            KindApp = opr_kind;
            Args = new TTerm[1];
            Args[0] = t1;
        }

        public TApply(string function_name, TTerm[] args) {
            KindApp = EKind.FunctionApply;
            FunctionRef = new TReference(function_name);
            Args = args;
        }

        public override void Text(StringWriter sw, TParser parser) {
            switch (KindApp) {
            case EKind.FunctionApply:
                FunctionRef.Text(sw, parser);
                ArgsText(sw, parser);
                break;

            default:
                switch (Args.Length) {
                case 1:
                    sw.Write("{0} ", parser.KindString[KindApp]);
                    Args[0].Text(sw, parser);
                    break;

                case 2:
                    Args[0].Text(sw, parser);
                    sw.Write(" {0} ", parser.KindString[KindApp]);
                    Args[1].Text(sw, parser);
                    break;

                default:
                    Debug.Assert(false);
                    break;
                }
                break;
            }
        }

        public void ArgsText(StringWriter sw, TParser parser) {
            foreach(TTerm trm in Args) {
                if(trm == Args[0]) {

                    sw.Write("(");
                }
                else {

                    sw.Write(", ");
                }

                trm.Text(sw, parser);
            }

            sw.Write(")");
        }
    }

    public partial class TMethodApply : TApply {
        public TTerm TermApp;

        public TMethodApply(TTerm trm, string function_name, TTerm[] args) : base(function_name, args) {
            TermApp = trm;
        }

        public override void Text(StringWriter sw, TParser parser) {
            TermApp.Text(sw, parser);
            FunctionRef.Text(sw, parser);
            ArgsText(sw, parser);
        }
    }

    public class TQuery {

    }

    public class TFrom : TQuery {

    }

    public class TAggregate : TQuery {
    }

    public abstract class TStatement {
        public abstract void Text(StringWriter sw, TParser parser);
    }

    public class TVariableDeclaration : TStatement {
        public List<TVariable> Variables = new List<TVariable>();

        public override void Text(StringWriter sw, TParser parser) {
            foreach(TVariable var1 in Variables) {
                if(var1 == Variables[0]) {

                    sw.Write("var ");
                }
                else {

                    sw.Write(", ");
                }

                var1.Text(sw, parser);
            }
        }
    }

    public class TBlock : TStatement {
        public List<TVariable> VariablesBlc = new List<TVariable>();
        public List<TStatement> StatementsBlc = new List<TStatement>();

        public override void Text(StringWriter sw, TParser parser) {
        }
    }

    public class TIfBlock : TStatement {
        public TTerm ConditionIf;
        public TBlock BlockIf;

        public override void Text(StringWriter sw, TParser parser) {
        }
    }

    public class TIf : TStatement {
        public List<TIfBlock> IfBlocks = new List<TIfBlock>();

        public override void Text(StringWriter sw, TParser parser) {
        }
    }

    public class TCase : TStatement {
        public bool IsDefault;
        public List<TTerm> TermsCase = new List<TTerm>();

        public override void Text(StringWriter sw, TParser parser) {
        }
    }

    public class TSwitch : TStatement {
        public TTerm TermSwitch;
        public List<TCase> Cases = new List<TCase>();

        public override void Text(StringWriter sw, TParser parser) {
        }
    }

    public class TTry : TStatement {
        public TBlock TryBlock;
        public TBlock CatchBlock;

        public override void Text(StringWriter sw, TParser parser) {
        }
    }

    public class TCatch : TStatement {
        public TVariable CatchVariable;

        public override void Text(StringWriter sw, TParser parser) {
        }
    }

    public class TWhile : TStatement {
        public TTerm WhileCondition;
        public TBlock WhileBlock;

        public override void Text(StringWriter sw, TParser parser) {
        }
    }

    public class TFor : TStatement {
        public TVariable LoopVariable;
        public TTerm ListFor;
        public TBlock ForBlock;

        public override void Text(StringWriter sw, TParser parser) {
        }
    }

    public class TJump : TStatement {
        public EKind KindJmp;
        public TTerm RetVal;

        public TJump(EKind kind) {
            KindJmp = kind;
        }

        public override void Text(StringWriter sw, TParser parser) {
        }
    }

    public class TSourceFile {
    }
}