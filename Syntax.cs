using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace MyEdit {
    public class TTerm {
        public bool WithParenthesis;
        public bool IsType;

        public virtual string Text(TParser parser) {
            Debug.Assert(false);
            return null;
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

        public virtual string Text(TParser parser) {
            StringWriter sw = new StringWriter();

            sw.Write(NameVar);

            if (TypeVar != null) {

                sw.Write(" : {0}", TypeVar.Text(parser));
            }

            if (InitValue != null) {

                sw.Write(" = {0}", InitValue.Text(parser));
            }

            return sw.ToString();
        }
    }

    public abstract class TType {
        public static TClass IndexClass = new TClass("_index_");

        public abstract string Text(TParser parser);
    }

    public class TFunctionType : TType {
        public TType ValType;
        public TType[] ArgsType;

        public TFunctionType(TType val_type, TType[] args_type) {
            ValType = val_type;
            ArgsType = args_type;
        }

        public override string Text(TParser parser) {
            StringWriter sw = new StringWriter();

            sw.Write("{0}", ValType);

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

            return sw.ToString();
        }
    }

    public class TClass : TType {
        public string ClassName;
        public List<TClass> SuperClasses = new List<TClass>();
        public List<TField> Fields = new List<TField>();
        public List<TFunction> Functions = new List<TFunction>();

        public TClass(string name) {
            ClassName = name;
        }

        public override string Text(TParser parser) {
            return ClassName;
        }

        public string ClassLine() {
            StringWriter sw = new StringWriter();

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

            return sw.ToString();
        }
    }

    public class TMember : TVariable {
        public bool IsStatic;
        public TClass ClassMember;

        public TMember(bool is_static, string name) : base(name) {
            IsStatic = is_static;
        }
    }

    public class TField : TMember {

        public TField(bool is_static, string name) : base(is_static, name) {
        }

        public override string Text(TParser parser) {
            return string.Format("{0} : {1}", NameVar, TypeVar);
        }
    }

    public class TFunction : TMember {
        public List<TVariable> ArgsFnc;
        public TType ReturnType;
        public TBlock BlockFnc;

        public TFunction(bool is_static, string name) : base(is_static, name) {
        }
    }

    public class TLiteral : TTerm {
        public EKind KindLit;
        public string TextLit;

        public TLiteral(EKind kind, string text) {
            KindLit = kind;
            TextLit = text;
        }

        public override string Text(TParser parser) {
            return TextLit;
        }
    }

    public class TReference : TTerm {
        public string NameRef;

        public TReference(string name) {
            NameRef = name;
        }

        public override string Text(TParser parser) {
            return NameRef;
        }
    }

    public class TFieldReference : TReference {
        public TTerm TermFldRef;

        public TFieldReference(TTerm trm, string name) : base(name) {
            TermFldRef = trm;
        }

        public override string Text(TParser parser) {
            return string.Format("{0}.{1}", TermFldRef.Text(parser), NameRef);
        }
    }

    public class TApply : TTerm {
        EKind KindApp;
        public string FunctionName;
        TTerm[] Args;

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
            FunctionName = function_name;
            Args = args;
        }

        public override string Text(TParser parser) {
            switch (KindApp) {
            case EKind.FunctionApply:
                return string.Format("{0}{1}", FunctionName, ArgsText(parser));

            default:
                switch (Args.Length) {
                case 1:
                    return string.Format("{0} {1}", parser.KindString[KindApp], Args[0].Text(parser));

                case 2:
                    return string.Format("{0} {1} {2}", Args[0].Text(parser), parser.KindString[KindApp], Args[1].Text(parser));

                default:
                    Debug.Assert(false);
                    return null;
                }
            }
        }

        public string ArgsText(TParser parser) {
            StringWriter sw = new StringWriter();

            foreach(TTerm trm in Args) {
                if(trm == Args[0]) {

                    sw.Write("(");
                }
                else {

                    sw.Write(", ");
                }

                sw.Write(trm.Text(parser));
            }

            sw.Write(")");

            return sw.ToString();
        }
    }

    public class TMethodApply : TApply {
        public TTerm TermApp;

        public TMethodApply(TTerm trm, string function_name, TTerm[] args) : base(function_name, args) {
            TermApp = trm;
        }

        public override string Text(TParser parser) {
            return string.Format("{0}.{1}{2}", TermApp.Text(parser), FunctionName, ArgsText(parser));
        }
    }

    public class TQuery {

    }

    public class TFrom : TQuery {

    }

    public class TAggregate : TQuery {
    }

    public abstract class TStatement {
        public abstract string Text(TParser parser);
    }

    public class TVariableDeclaration : TStatement {
        public List<TVariable> Variables = new List<TVariable>();

        public override string Text(TParser parser) {
            StringWriter sw = new StringWriter();

            foreach(TVariable var1 in Variables) {
                if(var1 == Variables[0]) {

                    sw.Write("var ");
                }
                else {

                    sw.Write(", ");
                }

                sw.Write(var1.Text(parser));
            }

            return sw.ToString();
        }
    }

    public class TBlock : TStatement {
        public List<TVariable> VariablesBlc = new List<TVariable>();
        public List<TStatement> StatementsBlc = new List<TStatement>();

        public override string Text(TParser parser) {
            return null;
        }
    }

    public class TIfBlock : TStatement {
        public TTerm ConditionIf;
        public TBlock BlockIf;

        public override string Text(TParser parser) {
            return null;
        }
    }

    public class TIf : TStatement {
        public List<TIfBlock> IfBlocks = new List<TIfBlock>();

        public override string Text(TParser parser) {
            return null;
        }
    }

    public class TCase : TStatement {
        public bool IsDefault;
        public List<TTerm> TermsCase = new List<TTerm>();

        public override string Text(TParser parser) {
            return null;
        }
    }

    public class TSwitch : TStatement {
        public TTerm TermSwitch;
        public List<TCase> Cases = new List<TCase>();

        public override string Text(TParser parser) {
            return null;
        }
    }

    public class TTry : TStatement {
        public TBlock TryBlock;
        public TVariable CatchVariable;
        public TBlock CatchBlock;

        public override string Text(TParser parser) {
            return null;
        }
    }

    public class TWhile : TStatement {
        public TTerm WhileCondition;
        public TBlock WhileBlock;

        public override string Text(TParser parser) {
            return null;
        }
    }

    public class TFor : TStatement {
        public TVariable LoopVariable;
        public TTerm ListFor;
        public TBlock ForBlock;

        public override string Text(TParser parser) {
            return null;
        }
    }

    public class TJump : TStatement {
        public EKind KindJmp;
        public TTerm RetVal;

        public TJump(EKind kind) {
            KindJmp = kind;
        }

        public override string Text(TParser parser) {
            return null;
        }
    }

    public class TSourceFile {
    }
}