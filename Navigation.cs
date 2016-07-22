using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Miyu {
    public abstract class TNavigation {
        public abstract void BeforeAction(object self, List<object> args);
        public abstract void AfterAction(object self, List<object> args);

        public virtual void ProjectNavi(TProject prj, List<object> args) {
            BeforeAction(prj, args);

            foreach (TType tp in prj.ClassTable.Values) {
                ClassNavi(tp, args);
            }

            AfterAction(prj, args);
        }

        public virtual void ClassNavi(TType cls, List<object> args) {
            BeforeAction(cls, args);

            foreach (TField fld in cls.Fields) {
                FieldNavi(fld, args);
            }
            foreach(TFunction fnc in cls.Functions) {
                FunctionNavi(fnc, args);
            }

            AfterAction(cls, args);
        }

        public virtual void FieldNavi(TField fld, List<object> args) {
            BeforeAction(fld, args);

            TermNavi(fld.InitValue, args);

            AfterAction(fld, args);
        }

        public virtual void FunctionNavi(TFunction fnc, List<object> args) {
            BeforeAction(fnc, args);

            foreach (TVariable v in fnc.ArgsFnc) {
                VariableNavi(v, args);
            }

            BlockNavi(fnc.BlockFnc, args);

            AfterAction(fnc, args);
        }

        public virtual void VariableNavi(TVariable var1, List<object> args) {
            BeforeAction(var1, args);

            TermNavi(var1.InitValue, args);

            AfterAction(var1, args);
        }

        public virtual void TermNavi(TTerm trm, List<object> args) {
            if(trm == null) {
                return;
            }

            if (trm is TLiteral) {
                LiteralNavi(trm as TLiteral, args);
            }
            else if (trm is TReference) {
                ReferenceNavi(trm as TReference, args);
            }
            else if (trm is TApply) {
                ApplyNavi(trm as TApply, args);
            }
            else if (trm is TQuery) {
                if (trm is TFrom) {
                    FromNavi(trm as TFrom, args);
                }
                else {
                    AggregateNavi(trm as TAggregate, args);
                }
            }
            else {
                Debug.Assert(false);
            }
        }

        public virtual void LiteralNavi(TLiteral lit, List<object> args) {
            BeforeAction(lit, args);
            AfterAction(lit, args);
        }

        public virtual void ReferenceNavi(TReference ref1, List<object> args) {
            BeforeAction(ref1, args);

            if (ref1 is TDotReference) {
                TermNavi((ref1 as TDotReference).DotRef, args);
            }

            AfterAction(ref1, args);
        }

        public virtual void ApplyNavi(TApply app, List<object> args) {
            BeforeAction(app, args);

            if(app is TDotApply) {

                TermNavi((app as TDotApply).DotApp, args);
            }

            foreach(TTerm t in app.Args) {
                TermNavi(t, args);
            }

            TermNavi(app.FunctionApp, args);

            if(app is TNewApply) {
                TNewApply new_app = app as TNewApply;
                foreach(TTerm trm in new_app.InitList) {
                    TermNavi(trm, args);
                }
            }

            AfterAction(app, args);
        }

        public virtual void FromNavi(TFrom frm, List<object> args) {
            BeforeAction(frm, args);

            VariableNavi(frm.VarQry, args);
            TermNavi(frm.SeqQry, args);
            TermNavi(frm.CndQry, args);

            TermNavi(frm.SelFrom, args);
            TermNavi(frm.TakeFrom, args);
            TermNavi(frm.InnerFrom, args);

            AfterAction(frm, args);
        }

        public virtual void AggregateNavi(TAggregate aggr, List<object> args) {
            BeforeAction(aggr, args);

            VariableNavi(aggr.VarQry, args);
            TermNavi(aggr.SeqQry, args);
            TermNavi(aggr.CndQry, args);

            TermNavi(aggr.IntoAggr, args);

            AfterAction(aggr, args);
        }

        public virtual void StatementNavi(TStatement stmt, List<object> args) {
            if(stmt == null) {
                return;
            }

            if (stmt is TVariableDeclaration) {
                VariableDeclarationNavi(stmt as TVariableDeclaration, args);
            }
            else if (stmt is TAssignment) {
                AssignmentNavi(stmt as TAssignment, args);
            }
            else if (stmt is TCall) {
                CallNavi(stmt as TCall, args);
            }
            else if (stmt is TJump) {
                JumpNavi(stmt as TJump, args);
            }
            else if(stmt is TIf) {
                IfNavi(stmt as TIf, args);
            }
            else if (stmt is TSwitch) {
                SwitchNavi(stmt as TSwitch, args);
            }
            else if (stmt is TLabelStatement) {
                LabelNavi(stmt as TLabelStatement, args);
            }
            else if (stmt is TBlockStatement) {
                if (stmt is TBlock) {
                    BlockNavi(stmt as TBlock, args);
                }
                else if (stmt is TIfBlock) {
                    IfBlockNavi(stmt as TIfBlock, args);
                }
                else if (stmt is TCase) {
                    CaseNavi(stmt as TCase, args);
                }
                else if (stmt is TForEach) {
                    ForEachNavi(stmt as TForEach, args);
                }
                else if (stmt is TFor) {
                    ForNavi(stmt as TFor, args);
                }
                else if (stmt is TLock) {
                    LockNavi(stmt as TLock, args);
                }
                else if (stmt is TWhile) {
                    WhileNavi(stmt as TWhile, args);
                }
                else if (stmt is TTry) {
                    TryNavi(stmt as TTry, args);
                }
                else if (stmt is TCatch) {
                    CatchNavi(stmt as TCatch, args);
                }
                else {
                    Debug.Assert(false);
                }
            }
            else {
                Debug.Assert(false);
            }
        }

        public virtual void AssignmentNavi(TAssignment asn, List<object> args) {
            BeforeAction(asn, args);

            ApplyNavi(asn.RelAsn, args);

            AfterAction(asn, args);
        }

        public virtual void CallNavi(TCall call, List<object> args) {
            BeforeAction(call, args);

            ApplyNavi(call.AppCall, args);

            AfterAction(call, args);
        }

        public virtual void VariableDeclarationNavi(TVariableDeclaration var_decl, List<object> args) {
            BeforeAction(var_decl, args);

            foreach (TVariable var1 in var_decl.Variables) {
                VariableNavi(var1, args);
            }

            AfterAction(var_decl, args);
        }

        public virtual void BlockNavi(TBlock block, List<object> args) {
            BeforeAction(block, args);

            foreach (TStatement stmt in block.StatementsBlc) {
                StatementNavi(stmt, args);
            }

            AfterAction(block, args);
        }

        public virtual void IfBlockNavi(TIfBlock if_block, List<object> args) {
            BeforeAction(if_block, args);

            TermNavi(if_block.ConditionIf, args);

            foreach(TStatement stmt in if_block.StatementsBlc) {
                StatementNavi(stmt, args);
            }

            AfterAction(if_block, args);
        }

        public virtual void IfNavi(TIf if_stmt, List<object> args) {
            BeforeAction(if_stmt, args);

            foreach (TIfBlock if_block in if_stmt.IfBlocks) {
                IfBlockNavi(if_block, args);
            }

            AfterAction(if_stmt, args);
        }

        public virtual void CaseNavi(TCase cas, List<object> args) {
            BeforeAction(cas, args);

            foreach (TTerm t in cas.TermsCase) {
                TermNavi(t, args);
            }

            foreach (TStatement stmt in cas.StatementsBlc) {
                StatementNavi(stmt, args);
            }

            AfterAction(cas, args);
        }

        public virtual void SwitchNavi(TSwitch swt, List<object> args) {
            BeforeAction(swt, args);

            TermNavi(swt.TermSwitch, args);
            foreach(TCase cas in swt.Cases) {
                CaseNavi(cas, args);
            }

            AfterAction(swt, args);
        }

        public virtual void TryNavi(TTry try1, List<object> args) {
            BeforeAction(try1, args);

            foreach (TStatement stmt in try1.StatementsBlc) {
                StatementNavi(stmt, args);
            }

            AfterAction(try1, args);
        }

        public virtual void CatchNavi(TCatch cat, List<object> args) {
            BeforeAction(cat, args);

            VariableNavi(cat.CatchVariable, args);

            foreach (TStatement stmt in cat.StatementsBlc) {
                StatementNavi(stmt, args);
            }

            AfterAction(cat, args);
        }

        public virtual void LockNavi(TLock lck, List<object> args) {
            BeforeAction(lck, args);

            TermNavi(lck.LockObj, args);

            foreach (TStatement stmt in lck.StatementsBlc) {
                StatementNavi(stmt, args);
            }

            AfterAction(lck, args);
        }

        public virtual void WhileNavi(TWhile wh, List<object> args) {
            BeforeAction(wh, args);

            TermNavi(wh.WhileCondition, args);

            foreach (TStatement stmt in wh.StatementsBlc) {
                StatementNavi(stmt, args);
            }

            AfterAction(wh, args);
        }

        public virtual void ForEachNavi(TForEach for1, List<object> args) {
            BeforeAction(for1, args);

            TermNavi(for1.ListFor, args);
            VariableNavi(for1.LoopVariable, args);

            foreach (TStatement stmt in for1.StatementsBlc) {
                StatementNavi(stmt, args);
            }

            AfterAction(for1, args);
        }

        public virtual void ForNavi(TFor for1, List<object> args) {
            BeforeAction(for1, args);

            StatementNavi(for1.InitStatement, args);
            TermNavi(for1.ConditionFor, args);

            foreach (TStatement stmt in for1.StatementsBlc) {
                StatementNavi(stmt, args);
            }

            StatementNavi(for1.PostStatement, args);

            AfterAction(for1, args);
        }

        public virtual void JumpNavi(TJump jmp, List<object> args) {
            BeforeAction(jmp, args);

            TermNavi(jmp.RetVal, args);

            AfterAction(jmp, args);
        }

        public virtual void LabelNavi(TLabelStatement lbl, List<object> args) {
            BeforeAction(lbl, args);

            AfterAction(lbl, args);
        }

        void SubTerm(TTerm trm) {
            if (trm is TLiteral) {
                TLiteral lit = trm as TLiteral;
            }
            else if (trm is TReference) {
                if(trm is TDotReference) {

                    TReference dref = trm as TDotReference;
                }
                else {
                    TReference ref1 = trm as TReference;
                }
            }
            else if (trm is TApply) {
                if(trm is TDotApply) {
                    TDotApply dapp = trm as TDotApply;
                }
                else if(trm is TNewApply) {
                    TNewApply napp = trm as TNewApply;
                }
                else {
                    TApply app = trm as TApply;
                }
            }
            else if(trm is TQuery) {
                if(trm is TFrom) {
                    TFrom frm = trm as TFrom;
                }
                else {
                   TAggregate agg = trm as TAggregate;
                }
            }
            else {
                Debug.Assert(false);
            }
        }

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
                else if (stmt is TLock) {
                    TLock lck = stmt as TLock;
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

    public class TSetParentNavi : TNavigation {
        public override void BeforeAction(object self, List<object> args) {
            if(self == null) {
                return;
            }

            object last = args[args.Count - 1];

            if(self is TTerm) {
                (self as TTerm).ParentTrm = last;
            }
            else if (self is TStatement) {
                TStatement self_stmt = self as TStatement;
                self_stmt.ParentStmt = last;

                if(last is TBlockStatement) {
                    TBlockStatement blc_stmt = last as TBlockStatement;
                    int i = blc_stmt.StatementsBlc.IndexOf(self_stmt);
                    switch (i) {
                    case -1:
                        Debug.Assert(blc_stmt is TFor);
                        TFor for1 = blc_stmt as TFor;
                        Debug.Assert(self_stmt == for1.InitStatement || self_stmt == for1.PostStatement);
                        self_stmt.PrevStatement = null;
                        break;

                    case 0:
                        self_stmt.PrevStatement = null;
                        break;

                    default:
                        self_stmt.PrevStatement = blc_stmt.StatementsBlc[i - 1];
                        break;
                    }
                }
            }

            args.Add(self);
        }

        public override void AfterAction(object self, List<object> args) {
            if (self == null) {
                return;
            }

            args.RemoveAt(args.Count - 1);
        }
    }

    public class TSetRefFnc : TNavigation {
        public Stack<TFunction> Funcs = new Stack<TFunction>();

        public override void BeforeAction(object self, List<object> args) {
            if (self == null) {
                return;
            }

            if (self is TReference) {
                if(Funcs.Count != 0) {
                    Funcs.Peek().RefFnc.Add(self as TReference);
                }
            }
            else if (self is TFunction) {
                Funcs.Push(self as TFunction);
            }
        }

        public override void AfterAction(object self, List<object> args) {
            if (self == null) {
                return;
            }

            if (self is TFunction) {
                Funcs.Pop();
            }
        }
    }

    public class TSetAppFnc : TNavigation {
        public Stack<TFunction> Funcs = new Stack<TFunction>();

        public override void BeforeAction(object self, List<object> args) {
            if (self == null) {
                return;
            }

            if (self is TApply) {
                if (Funcs.Count != 0) {
                    Funcs.Peek().AppFnc.Add(self as TApply);
                }
            }
            else if (self is TFunction) {
                TFunction fnc = self as TFunction;
                Funcs.Push(fnc);

                if(fnc.BaseApp != null) {
                    fnc.AppFnc.Add(fnc.BaseApp);
                }
            }
        }

        public override void AfterAction(object self, List<object> args) {
            if (self == null) {
                return;
            }

            if (self is TFunction) {
                Funcs.Pop();
            }
        }
    }
}
