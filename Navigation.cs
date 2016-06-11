﻿using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MyEdit {
    public delegate void TAction(object self, object parent, params object[] args);

    public abstract class TNavigation {
        public abstract void Action(object obj, params object[] args);

        public virtual void ClassNavi(TClass cls, params object[] args) {
            foreach(TField fld in cls.Fields) {
                FieldNavi(fld, args);
            }
            foreach(TFunction fnc in cls.Functions) {
                FunctionNavi(fnc, args);
            }

            Action(cls, args);
        }

        public virtual void FieldNavi(TField fld, params object[] args) {
            Action(fld.InitValue, args);
            Action(fld, args);
        }

        public virtual void FunctionNavi(TFunction fnc, params object[] args) {
            foreach(TVariable v in fnc.ArgsFnc) {
                VariableNavi(v, args);
            }

            Action(fnc, args);
        }

        public virtual void VariableNavi(TVariable var, params object[] args) {
            Action(var.InitValue, args);
            Action(var, args);
        }

        public virtual void TermNavi(TTerm term, params object[] args) {
        }

        public virtual void LiteralNavi(TLiteral lit, params object[] args) {
        }

        public virtual void ReferenceNavi(TReference ref1, params object[] args) {
        }

        public virtual void FieldReferenceNavi(TFieldReference fld_ref, params object[] args) {
            Action(fld_ref.TermFldRef, args);
        }

        public virtual void ApplyNavi(TApply app, params object[] args) {
        }

        public virtual void MethodApplyNavi(TMethodApply app, params object[] args) {
        }

        public virtual void StatementNavi(TStatement stmt, params object[] args) {
        }

        public virtual void AssignmentNavi(TAssignment asn, params object[] args) {
            ApplyNavi(asn.RelAsn, args);
        }

        public virtual void CallNavi(TCall call, params object[] args) {
            ApplyNavi(call.AppCall, args);
        }

        public virtual void VariableDeclarationNavi(TVariableDeclaration var_decl, params object[] args) {
            foreach(TVariable var in var_decl.Variables) {
                VariableNavi(var);
            }
        }

        public virtual void BlockStatementNavi(TBlockStatement block, params object[] args) {
        }

        public virtual void BlockNavi(TBlock block, params object[] args) {
        }

        public virtual void IfBlockNavi(TIfBlock if_block, params object[] args) {
            TermNavi(if_block.ConditionIf);
        }

        public virtual void IfNavi(TIf if_stmt, params object[] args) {
        }

        public virtual void CaseNavi(TCase cas, params object[] args) {
            foreach(TTerm t in cas.TermsCase) {
                TermNavi(t, args);
            }
        }

        public virtual void SwitchNavi(TSwitch swt, params object[] args) {
            TermNavi(swt.TermSwitch, args);
            foreach(TCase cas in swt.Cases) {
                CaseNavi(cas, args);
            }
        }

        public virtual void TryNavi(TTry try1, params object[] args) {
        }

        public virtual void CatchNavi(TCatch cat, params object[] args) {
            VariableNavi(cat.CatchVariable, args);
        }

        public virtual void WhileNavi(TWhile wh, params object[] args) {
            TermNavi(wh.WhileCondition, args);
        }

        public virtual void ForNavi(TFor for1, params object[] args) {
            TermNavi(for1.ListFor, args);
            VariableNavi(for1.LoopVariable, args);
        }

        public virtual void JumpNavi(TJump jmp, params object[] args) {
            TermNavi(jmp.RetVal, args);
        }
    }
}

