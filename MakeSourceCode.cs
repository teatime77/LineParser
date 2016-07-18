using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Miyu {
    public class TTokenWriter : TEnv {
        public List<TToken> TokenListTW = new List<TToken>();

        public TTokenWriter(TParser parser) {
            Parser = parser;
        }

        public void AddToken(EKind kind) {
            TokenListTW.Add(new TToken(kind));
        }

        public void TAB(int n) {
            var tab1 = new TToken();

            tab1.Kind = EKind.Tab;
            tab1.TabTkn = n;
            TokenListTW.Add(tab1);
        }

        public void WriteLine() {
            AddToken(EKind.NL);
        }

        public void Fmt(params object[] args) {
            foreach (object o1 in args) {
                if(o1 is char) {
                    AddToken(EKind.Space);
                }
                else if(o1 is string) {

                    TokenListTW.Add(new TToken(o1 as string, null));
                }
                else if(o1 is TToken) {
                    TokenListTW.Add(o1 as TToken);
                }
                else if (o1 is TReference || o1 is TLiteral || o1 is TType || o1 is TVariable) {
                    TokenListTW.Add(new TToken(o1));
                }
                else if (o1 is EKind) {

                    AddToken((EKind)o1);
                }
                else {
                    Debug.Assert(false);
                }
            }
        }

        public string ToPlainText() {
            StringWriter sw = new StringWriter();

            foreach(TToken tk in TokenListTW) {
                switch (tk.Kind) {
                case EKind.Space:
                    sw.Write(' ');
                    break;

                case EKind.Tab:
                    sw.Write(new string('\t', tk.TabTkn));
                    break;

                case EKind.NL:
                    sw.WriteLine();
                    break;

                case EKind.LineComment:
                case EKind.BlockComment:
                    sw.Write(tk.TextTkn);
                    sw.WriteLine();
                    break;

                case EKind.BlockCommentContinued:
                    sw.Write(tk.TextTkn);
                    break;

                case EKind.Undefined:
                    if (tk.ObjTkn != null) {
                        if(tk.ObjTkn is TReference) {
                            TReference ref1 = tk.ObjTkn as TReference;

                            sw.Write(ref1.NameRef);
                        }
                        else if (tk.ObjTkn is TLiteral) {
                            TLiteral lit = tk.ObjTkn as TLiteral;

                            sw.Write(lit.TokenTrm.TextTkn);
                        }
                        else if (tk.ObjTkn is TVariable) {
                            TVariable va = tk.ObjTkn as TVariable;

                            sw.Write(va.NameVar);
                        }
                        else if (tk.ObjTkn is TType) {
                            TType tp = tk.ObjTkn as TType;

                            sw.Write(tp.ClassName);
                        }
                        else {
                            Debug.Assert(false);
                        }
                    }
                    else {

                        sw.Write(tk.TextTkn);
                    }
                    break;

                default:
                    string s = "";
                    if(Parser.KindString.TryGetValue(tk.Kind, out s)) {

                        sw.Write(s);
                    }
                    else {

                        sw.Write(tk.TextTkn);
                    }
                    break;
                }
            }

            return sw.ToString();
        }

        public List<TToken> GetTokenList() {
            return TokenListTW;
        }
    }

    partial class TParser {
        public void TypeText(TType tp, TTokenWriter sw) {
            if (tp is TGenericClass) {
                TGenericClass gen = tp as TGenericClass;

                if (gen.DimCnt != 0) {

                    TypeText(gen.GenCla[0], sw);

                    sw.Fmt(EKind.LB);
                    for (int i = 0; i < gen.DimCnt - 1; i++) {
                        sw.Fmt(EKind.Comma);
                    }
                    sw.Fmt(EKind.RB);
                }
                else {

                    sw.Fmt(tp);
                    sw.Fmt(EKind.LT);
                    for(int i = 0; i < gen.GenCla.Count; i++) {
                        if (i != 0) {
                            sw.Fmt(EKind.Comma, ' ');
                        }
                        TypeText(gen.GenCla[i], sw);
                    }
                    sw.Fmt(EKind.GT);
                }
            }
            else {

                sw.Fmt(tp);
            }
        }

        public void VariableText(TVariable var1, TTokenWriter sw) {
            ModifierText(var1.ModifierVar, sw);

            if (var1.TypeVar != null) {

                TypeText(var1.TypeVar, sw);
                sw.Fmt(' ');
            }

            sw.Fmt(var1);

            if (var1.InitValue != null) {

                sw.Fmt(EKind.Assign);
                TermText(var1.InitValue, sw);
            }
        }

        public void ArgsText(TApply app, TTokenWriter sw) {
            foreach (TTerm trm in app.Args) {
                if (trm != app.Args[0]) {

                    sw.Fmt(EKind.Comma);
                }

                TermText(trm, sw);
            }
        }

        public void TermText(TTerm term, TTokenWriter sw) {
            if (term.CastType != null && term.CastType.IsPrimitive()) {

                sw.Fmt(EKind.LP);
                TypeText(term.CastType, sw);
                sw.Fmt(EKind.RP);
            }

            if (term.WithParenthesis) {
                sw.Fmt(EKind.LP);
            }
            if (term is TLiteral) {
                sw.Fmt(term);
            }
            else if (term is TReference) {
                TReference ref1 = term as TReference;

                if (ref1.IsOut) {
                    sw.Fmt(EKind.out_, ' ');
                }
                if (ref1.IsRef) {
                    sw.Fmt(EKind.ref_, ' ');
                }

                if (ref1 is TDotReference) {

                    TermText((ref1 as TDotReference).DotRef, sw);
                    sw.Fmt(EKind.Dot);
                }

                if(ref1.ClassRef != null) {

                    TypeText(ref1.ClassRef, sw);
                }
                else {

                    sw.Fmt(ref1);
                }
            }
            else if (term is TApply) {
                TApply app = term as TApply;

                if (app is TDotApply) {

                    TermText((app as TDotApply).DotApp, sw);
                    sw.Fmt(EKind.Dot);
                }

                switch (app.KindApp) {
                case EKind.FunctionApply:
                    TermText(app.FunctionApp, sw);
                    sw.Fmt(EKind.LP);
                    ArgsText(app, sw);
                    sw.Fmt(EKind.RP);
                    break;

                case EKind.Index:
                    TermText(app.FunctionApp, sw);
                    sw.Fmt(EKind.LB);
                    ArgsText(app, sw);
                    sw.Fmt(EKind.RB);
                    break;

                case EKind.NewInstance:
                    sw.Fmt(EKind.new_, ' ');
                    TypeText((app as TNewApply).ClassApp, sw);
                    sw.Fmt(EKind.LP);
                    ArgsText(app, sw);
                    sw.Fmt(EKind.RP);
                    break;

                case EKind.NewArray:
                    sw.Fmt(EKind.new_, ' ');
                    TypeText((app as TNewApply).ClassApp, sw);
                    sw.Fmt(EKind.LB);
                    ArgsText(app, sw);
                    sw.Fmt(EKind.RB);
                    break;

                case EKind.base_:
                    sw.Fmt(EKind.base_, EKind.Dot);
                    TermText(app.FunctionApp, sw);
                    sw.Fmt(EKind.LP);
                    ArgsText(app, sw);
                    sw.Fmt(EKind.RP);
                    break;

                case EKind.Inc:
                case EKind.Dec:
                    TermText(app.Args[0], sw);
                    sw.Fmt(app.KindApp);
                    break;

                case EKind.typeof_:
                    sw.Fmt(app.KindApp, EKind.LP);
                    TermText(app.Args[0], sw);
                    sw.Fmt(EKind.RP);
                    break;

                default:
                    switch (app.Args.Length) {
                    case 1:
                        sw.Fmt(app.KindApp, ' ');
                        TermText(app.Args[0], sw);
                        break;

                    case 2:
                        TermText(app.Args[0], sw);
                        sw.Fmt(' ', app.KindApp, ' ');
                        TermText(app.Args[1], sw);
                        break;

                    case 3:
                        TermText(app.Args[0], sw);
                        sw.Fmt(' ', EKind.Question, ' ');
                        TermText(app.Args[1], sw);
                        sw.Fmt(' ', EKind.Colon, ' ');
                        TermText(app.Args[2], sw);
                        break;

                    default:
                        Debug.Assert(false);
                        break;
                    }
                    break;
                }

            }
            else if (term is TQuery) {
                TQuery qry = term as TQuery;

                if (term is TFrom) {
                    TFrom from1 = term as TFrom;

                    sw.Fmt(EKind.from_, ' ', from1.VarQry, ' ', EKind.in_, ' ');
                    TermText(from1.SeqQry, sw);

                    if(from1.CndQry != null) {
                        sw.Fmt(' ', EKind.where_, ' ');
                        TermText(from1.CndQry, sw);
                    }

                    if (from1.SelFrom != null) {
                        sw.Fmt(' ', EKind.select_, ' ');
                        TermText(from1.SelFrom, sw);
                    }

                    if (from1.InnerFrom != null) {
                        sw.Fmt(' ');
                        TermText(from1.InnerFrom, sw);
                    }
                }
                else {
                    TAggregate aggr = term as TAggregate;
                }
            }
            else {
                Debug.Assert(false);
            }

            if(term.CastType != null && ! term.CastType.IsPrimitive()) {

                sw.Fmt(' ', EKind.as_, ' ');
                TypeText(term.CastType, sw);
            }

            if (term.WithParenthesis) {
                sw.Fmt(EKind.RP);
            }
        }

        public void StatementText(TStatement stmt, TTokenWriter sw, int nest) {
            WriteComment(stmt.CommentStmt, sw);

            if (stmt is TSwitch) {
                TSwitch swt = stmt as TSwitch;

                sw.TAB(nest);
                sw.Fmt(EKind.switch_, EKind.LP);
                TermText(swt.TermSwitch, sw);
                sw.Fmt(EKind.RP, EKind.LC, EKind.NL);

                foreach (TCase cas in swt.Cases) {
                    StatementText(cas, sw, nest);
                }

                sw.TAB(nest);
                sw.Fmt(EKind.RC, EKind.NL);
            }
            else if (stmt is TBlockStatement) {
                TBlockStatement blc_stmt = stmt as TBlockStatement;

                if(! ( stmt.ParentStmt is TFunction )) {

                    sw.TAB(nest);
                }
                if (stmt is TBlock) {
                    TBlock block = stmt as TBlock;
                    sw.Fmt(EKind.LC, EKind.NL);
                }
                else if (stmt is TIfBlock) {
                    TIfBlock if_block = stmt as TIfBlock;

                    if(if_block.ConditionIf != null) {
                        // 条件がある場合

                        if(if_block.IsElse) {
                            // 最初でない場合

                            sw.Fmt(EKind.else_, ' ');
                        }

                        sw.Fmt(EKind.if_, EKind.LP);
                        TermText(if_block.ConditionIf, sw);
                        sw.Fmt(EKind.RP, EKind.LC);
                    }
                    else {
                        // 条件がない場合

                        sw.Fmt(EKind.else_, EKind.LC);
                    }
                    sw.WriteLine();
                }
                else if (stmt is TCase) {
                    TCase cas = stmt as TCase;

                    if(cas.TermsCase.Count == 0) {

                        sw.Fmt(EKind.default_, EKind.Colon, EKind.NL);
                    }
                    else {

                        foreach (TTerm trm in cas.TermsCase) {
                            sw.Fmt(EKind.case_, ' ');
                            TermText(trm, sw);
                            sw.Fmt(EKind.Colon, EKind.NL);
                        }
                    }
                }
                else if (stmt is TForEach) {
                    TForEach for1 = stmt as TForEach;

                    sw.Fmt(EKind.foreach_,EKind.LP);
                    VariableText(for1.LoopVariable, sw);
                    sw.Fmt(' ', EKind.in_, ' ');
                    TermText(for1.ListFor, sw);
                    sw.Fmt(EKind.RP, EKind.LC, EKind.NL);
                }
                else if (stmt is TFor) {
                    TFor for1 = stmt as TFor;

                    sw.Fmt(EKind.for_, EKind.LP);
                    if (for1.InitStatement != null) {

                        StatementText(for1.InitStatement, sw, -1);
                    }
                    sw.Fmt(EKind.SemiColon);

                    if (for1.ConditionFor != null) {

                        TermText(for1.ConditionFor, sw);
                    }
                    sw.Fmt(EKind.SemiColon);

                    if (for1.PostStatement != null) {

                        StatementText(for1.PostStatement, sw, -1);
                    }
                    sw.Fmt(EKind.RP, EKind.LC);
                    sw.WriteLine();
                }
                else if (stmt is TWhile) {
                    TWhile while1 = stmt as TWhile;

                    sw.Fmt(EKind.while_,EKind.LP);
                    TermText(while1.WhileCondition, sw);
                    sw.Fmt(EKind.RP, EKind.LC, EKind.NL);
                }
                else if (stmt is TTry) {
                    TTry try1 = stmt as TTry;

                    sw.Fmt(EKind.try_, EKind.LC, EKind.NL);
                }
                else if (stmt is TCatch) {
                    TCatch catch1 = stmt as TCatch;

                    sw.Fmt(EKind.catch_, EKind.LP);
                    VariableText(catch1.CatchVariable, sw);
                    sw.Fmt(EKind.RP, EKind.LC, EKind.NL);
                }
                else {
                    Debug.Assert(false);
                }

                foreach (TStatement stmt1 in blc_stmt.StatementsBlc) {
                    StatementText(stmt1, sw, nest + 1);
                }

                if(!(stmt is TCase)) {

                    sw.TAB(nest);
                    sw.Fmt(EKind.RC, EKind.NL);
                }
            }
            else {
                if (nest != -1) {

                    sw.TAB(nest);
                }

                if (stmt is TVariableDeclaration) {
                    TVariableDeclaration var_decl = stmt as TVariableDeclaration;

                    if(var_decl.IsVar) {

                        sw.Fmt(EKind.var_);
                    }
                    else {

                        TypeText(var_decl.Variables[0].TypeVar, sw);
                    }

                    sw.Fmt(' ');
                    foreach (TVariable var1 in var_decl.Variables) {
                        if (var1 != var_decl.Variables[0]) {

                            sw.Fmt(EKind.Comma, ' ');
                        }

                        sw.Fmt(var1);

                        if (var1.InitValue != null) {

                            sw.Fmt(' ', EKind.Assign, ' ');
                            TermText(var1.InitValue, sw);
                        }
                    }
                }
                else if (stmt is TAssignment) {
                    TAssignment asn = stmt as TAssignment;

                    TermText(asn.RelAsn, sw);
                }
                else if (stmt is TCall) {
                    TCall call1 = stmt as TCall;

                    TermText(call1.AppCall, sw);
                }
                else if (stmt is TJump) {
                    TJump jmp = stmt as TJump;

                    sw.Fmt(jmp.KindJmp);

                    if(jmp.KindJmp == EKind.goto_) {

                        sw.Fmt(' ', jmp.LabelJmp);
                    }
                    else {

                        if (jmp.RetVal != null) {

                            sw.Fmt(' ');
                            TermText(jmp.RetVal, sw);
                        }
                    }
                }
                else if (stmt is TLabelStatement) {
                    TLabelStatement lbl = stmt as TLabelStatement;

                    sw.Fmt(lbl.LabelToken, EKind.Colon, EKind.NL);
                    return;
                }
                else {

                    Debug.Assert(false);
                }

                if (nest != -1) {

                    sw.Fmt(EKind.SemiColon, EKind.NL);
                }
            }
        }

        public void ModifierText(TModifier mod1, TTokenWriter sw) {
            if (mod1 != null) {

                if (mod1.isPublic) {
                    sw.Fmt(EKind.public_, ' ');
                }
                if (mod1.isPrivate) {
                    sw.Fmt(EKind.private_, ' ');
                }

                if (mod1.isPartial) {
                    sw.Fmt(EKind.partial_, ' ');
                }

                if (mod1.isStatic) {
                    sw.Fmt(EKind.static_, ' ');
                }

                if (mod1.isConst) {
                    sw.Fmt(EKind.const_, ' ');
                }

                if (mod1.isOverride) {
                    sw.Fmt(EKind.override_, ' ');
                }

                if (mod1.isAbstract) {
                    sw.Fmt(EKind.abstract_, ' ');
                }

                if (mod1.isVirtual) {
                    sw.Fmt(EKind.virtual_, ' ');
                }

                if (mod1.isSealed) {
                    sw.Fmt(EKind.sealed_, ' ');
                }

                if (mod1.isAsync) {
                    sw.Fmt(EKind.async_, ' ');
                }

                if (mod1.isRef) {
                    sw.Fmt(EKind.ref_, ' ');
                }

                if (mod1.isOut) {
                    sw.Fmt(EKind.out_, ' ');
                }

                if (mod1.isParams) {
                    sw.Fmt(EKind.params_, ' ');
                }
            }
        }

        public void WriteComment(TToken[] comments, TTokenWriter sw) {
            if(comments != null && comments.Length != 0) {
                foreach(TToken tk in comments) {
                    sw.Fmt(tk);
                }
            }
        }

        public void ClassLineText(TType cls, TTokenWriter sw) {
            ModifierText(cls.ModifierCls, sw);

            sw.Fmt(EKind.class_, ' ', cls);

            for (int i = 0; i < cls.SuperClasses.Count; i++) {
                if (i == 0) {

                    sw.Fmt(EKind.Colon);
                }
                else {

                    sw.Fmt(EKind.Comma);
                }
                TypeText(cls.SuperClasses[i], sw);
            }
            sw.Fmt(EKind.LC, EKind.NL);
        }

        public void SourceFileText(TSourceFile src, TTokenWriter sw) {
            foreach (TType cls in src.ClassesSrc) {

                WriteComment(cls.CommentCls, sw);
                sw.TAB(1);
                ClassLineText(cls, sw);

                var vfld = from x in src.FieldsSrc where x.ClassMember == cls select x;
                foreach (TField fld in vfld) {
                    WriteComment(fld.CommentVar, sw);
                    sw.TAB(2);
                    VariableText(fld, sw);
                    sw.Fmt(EKind.SemiColon, EKind.NL);
                }

                var vfnc = from x in src.FunctionsSrc where x.ClassMember == cls select x;
                foreach (TFunction fnc in vfnc) {

                    WriteComment(fnc.CommentVar, sw);
                    sw.TAB(2);

                    ModifierText(fnc.ModifierVar, sw);
                    if (fnc.TypeVar != null) {

                        TypeText(fnc.TypeVar, sw);
                        sw.Fmt(' ');
                    }

                    if (fnc.TokenVar.TokenType == ETokenType.Symbol) {

                        sw.Fmt(EKind.operator_);
                    }
                    sw.Fmt(fnc);

                    sw.Fmt(EKind.LP);
                    foreach (TVariable var1 in fnc.ArgsFnc) {
                        if (var1 != fnc.ArgsFnc[0]) {
                            sw.Fmt(EKind.Comma, ' ');
                        }

                        VariableText(var1, sw);
                    }
                    sw.Fmt(EKind.RP);

                    StatementText(fnc.BlockFnc, sw, 2);
                }

                sw.TAB(1);
                sw.Fmt(EKind.RC, EKind.NL);
            }
        }
    }
}