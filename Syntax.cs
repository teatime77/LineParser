﻿using System.Collections.Generic;
using System.IO;

namespace MyEdit {
    public class TTerm {
        public bool WithParenthesis;
        public bool IsType;
    }

    public class TVariable {
        public string NameVar;
        public TType TypeVar;
        public TTerm InitValue;
        
        public TVariable(string name) {
            NameVar = name;
        }
    }

    public class TType {
        public static TClass IndexClass = new TClass("_index_");
    }

    public class TFunctionType : TType {
        public TType ValType;
        public TType[] ArgsType;

        public TFunctionType(TType val_type, TType[] args_type) {
            ValType = val_type;
            ArgsType = args_type;
        }

        public override string ToString() {
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

        public override string ToString() {
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
        public TClass ClassMember;

        public TMember(string name) : base(name) {
        }
    }

    public class TField : TMember {

        public TField(string name) : base(name) {
        }

        public override string ToString() {
            return string.Format("{0} : {1}", NameVar, TypeVar);
        }
    }

    public class TFunction : TMember {
        public List<TVariable> ArgsFnc;
        public TType ReturnType;
        public TBlock BlockFnc;

        public TFunction(string name) : base(name) {
        }
    }

    public class TLiteral : TTerm {
        public EKind KindLit;
        public string Text;
    }

    public class TReference : TTerm {
        public string Name;

        public TReference(string name) {
            Name = name;
        }
    }

    public class TApply : TTerm {
        EKind KindApp;
        TTerm[] Args;

        public TApply(EKind opr_kind, TTerm t1, TTerm t2) {

        }

        public TApply(EKind opr_kind, TTerm t1) {

        }
    }

    public class TQuery {

    }

    public class TFrom : TQuery {

    }

    public class TAggregate : TQuery {
    }

    public class TStatement {
    }

    public class TVariableDeclaration : TStatement {
        public List<TVariable> VariablesDcl = new List<TVariable>();
    }

    public class TBlock : TStatement {
        public List<TVariable> VariablesBlc = new List<TVariable>();
        public List<TStatement> StatementsBlc = new List<TStatement>();
    }

    public class TIfBlock : TStatement {
        public TTerm ConditionIf;
        public TBlock BlockIf;
    }

    public class TIf : TStatement {
        public List<TIfBlock> IfBlocks = new List<TIfBlock>();
    }

    public class TCase : TStatement {
        public bool IsDefault;
        public List<TTerm> TermsCase = new List<TTerm>();
    }

    public class TSwitch : TStatement {
        public TTerm TermSwitch;
        public List<TCase> Cases = new List<TCase>();
    }

    public class TTry : TStatement {
        public TBlock TryBlock;
        public TVariable CatchVariable;
        public TBlock CatchBlock;
    }

    public class TWhile : TStatement {
        public TTerm WhileCondition;
        public TBlock WhileBlock;
    }

    public class TFor : TStatement {
        public TVariable LoopVariable;
        public TTerm ListFor;
        public TBlock ForBlock;
    }

    public class TJump : TStatement {
        public EKind KindJmp;
        public TTerm RetVal;

        public TJump(EKind kind) {
            KindJmp = kind;
        }
    }

    public class TSourceFile {
    }
}