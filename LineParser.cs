using System.Diagnostics;
using System.Collections.Generic;
using System;
using Windows.UI;
using System.IO;
using System.Net;

/*--------------------------------------------------------------------------------
        1行の構文解析
--------------------------------------------------------------------------------*/

namespace MyEdit {
    partial class TParser {
        static TToken EOTToken = new TToken(ETokenType.White, EKind.EOT,"", 0, 0);
        TToken[] TokenList;
        int TokenPos;
        TToken CurTkn;
        TToken NextTkn;
        Dictionary<string, TClass> ClassTable = new Dictionary<string, TClass>();
        Dictionary<string, TType> TypeTable = new Dictionary<string, TType>();

        // キーワードの文字列の辞書
        public Dictionary<string, EKind> KeywordMap;

        // 2文字の記号の表
        public EKind[,] SymbolTable2 = new EKind[256, 256];

        // 1文字の記号の表
        public EKind[] SymbolTable1 = new EKind[256];

        public Dictionary<EKind, string> KindString = new Dictionary<EKind, string>();


        public TParser() {
            // 字句解析の初期処理をします。
            InitializeLexicalAnalysis();
        }

        public TClass GetClassByName(string name) {
            TClass cls;

            if(ClassTable.TryGetValue(name, out cls)) {
                return cls;
            }
            else {
                cls = new TClass(name);

                ClassTable.Add(name, cls);

                return cls;
            }
        }

        public TClass ReadClassLine() {
            GetToken(EKind.class_);
            TToken id = GetToken(EKind.Identifier);

            TClass cls = GetClassByName(id.TextTkn);

            if (CurTkn.Kind == EKind.Colon) {

                GetToken(EKind.Colon);
                while (true) {

                    TToken super_class_name = GetToken(EKind.Identifier);

                    TClass super_class = GetClassByName(super_class_name.TextTkn);
                    cls.SuperClasses.Add(super_class);

                    if(CurTkn.Kind == EKind.Comma) {

                        GetToken(EKind.Comma);
                    }
                    else {

                        break;
                    }
                }
            }
            GetToken(EKind.EOT);

            return cls;
        }

        public TField ReadFieldLine(bool is_static) {
            TToken id = GetToken(EKind.Identifier);

            GetToken(EKind.Colon);

            TType tp = ReadType();

            TTerm init = null;
            if (CurTkn.Kind == EKind.Assign) {

                GetToken(EKind.Assign);

                init = Expression();
            }

            GetToken(EKind.EOT);

            return new TField(is_static, id.TextTkn, tp, init);
        }

        public TClass ReadEnumLine() {
            return null;
        }

        public TType ReadType() {
            TToken id = GetToken(EKind.Identifier);
            TClass cls1 = GetClassByName(id.TextTkn);

            if (CurTkn.Kind != EKind.LB) {

                return cls1;
            }
            else { 
                GetToken(EKind.LB);

                List<TType> types = new List<TType>();
                while (true) {
                    TType tp1;
                    
                    if(CurTkn.Kind == EKind.Identifier) {

                        tp1 = ReadType();
                    }
                    else {

                        tp1 = TType.IndexClass;
                    }
                    types.Add(tp1);

                    if(CurTkn.Kind == EKind.Comma) {

                        GetToken(EKind.Comma);
                    }
                    else {
                        break;
                    }
                }

                GetToken(EKind.RB);

                TFunctionType fnc_type = new TFunctionType(cls1, types.ToArray());

                string type_text = fnc_type.ToString();

                TType reg_type;
                if(TypeTable.TryGetValue(type_text, out reg_type)) {
                    return reg_type;
                }

                TypeTable.Add(type_text, fnc_type);

                return fnc_type;
            }
        }

        public TFunction ReadFunctionLine(bool is_static) {
            TToken tkn = GetToken(EKind.Undefined);

            TToken fnc_name = GetToken(EKind.Identifier);

            GetToken(EKind.LP);

            List<TVariable> vars = new List<TVariable>();
            while (CurTkn.Kind != EKind.RP) {
                TVariable var1 = ReadVariable();
                vars.Add(var1);

                if (CurTkn.Kind != EKind.Comma) {

                    break;
                }

                GetToken(EKind.Comma);
            }

            GetToken(EKind.RP);

            TType ret_type = null;
            if(CurTkn.Kind == EKind.Colon) {

                GetToken(EKind.Colon);

                ret_type = ReadType();
            }

            GetToken(EKind.EOT);

            return new TFunction(is_static, fnc_name.TextTkn, vars.ToArray(), ret_type);
        }

        public TVariable ReadVariable() {
            TToken id = GetToken(EKind.Identifier);

            TType type = null;
            if(CurTkn.Kind == EKind.Colon) {

                GetToken(EKind.Colon);

                type = ReadType();
            }

            TTerm init = null;
            if (CurTkn.Kind == EKind.Assign) {

                GetToken(EKind.Assign);

                init = Expression();
            }

            return new TVariable(id.TextTkn, type, init);
        }

        public TVariableDeclaration ReadVariableDeclarationLine() {
            TVariableDeclaration var_decl = new TVariableDeclaration();

            GetToken(EKind.var_);

            while (true) {
                TVariable var1 = ReadVariable();

                var_decl.Variables.Add(var1);

                if(CurTkn.Kind != EKind.Comma) {

                    GetToken(EKind.EOT);
                    return var_decl;
                }

                GetToken(EKind.Comma);
            }
        }

        public TIfBlock ReadIfLine() {
            TIfBlock if_block = new TIfBlock();

            GetToken(EKind.if_);

            if_block.ConditionIf = Expression();

            return if_block;
        }

        public TIfBlock ReadElseLine() {
            TIfBlock if_block = new TIfBlock();

            GetToken(EKind.else_);

            if(CurTkn.Kind == EKind.if_) {

                if_block.ConditionIf = Expression();
            }

            return if_block;
        }

        public TSwitch ReadSwitchLine() {
            TSwitch switch1 = new TSwitch();

            GetToken(EKind.switch_);

            switch1.TermSwitch = Expression();

            return switch1;
        }

        public TCase ReadCaseLine() {
            TCase case1 = new TCase();

            TToken tkn = GetToken(EKind.case_);

            List<TTerm> expr_list = ExpressionList();
            case1.TermsCase.AddRange(expr_list);

            return case1;
        }

        public TWhile ReadWhileLine() {
            TWhile while1 = new TWhile();

            GetToken(EKind.for_);

            while1.WhileCondition = Expression();

            return while1;
        }

        public TFor ReadForLine() {
            TFor for1 = new TFor();

            GetToken(EKind.for_);

            TToken id = GetToken(EKind.Identifier);
            for1.LoopVariable = new TVariable(id.TextTkn);

            GetToken(EKind.in_);

            for1.ListFor = Expression();

            return for1;
        }

        public TTry ReadTryLine() {
            GetToken(EKind.try_);
            return new TTry();
        }

        public TJump ReadJumpLine() {
            TToken tkn = GetToken(EKind.Undefined);

            TJump jump = new TJump(tkn.Kind);

            switch (tkn.Kind) {
            case EKind.return_:
            case EKind.yield_:
            case EKind.throw_:
                jump.RetVal = Expression();
                break;

            case EKind.break_:
                break;
            }

            return jump;
        }

        public TTerm ReadAssignmentCallLine() {
            TTerm t1 = Expression();

            switch (CurTkn.Kind) {
            case EKind.Assign:
            case EKind.AddEq:
            case EKind.SubEq:
            case EKind.DivEq:
            case EKind.ModEq:

                TToken opr = GetToken(EKind.Undefined);

                TTerm t2 = Expression();

                TApply app1 = new TApply(opr.Kind, t1, t2);
                return app1;
            }

            GetToken(EKind.EOT);

            return t1;
        }

        public int LineTopTokenIndex(TToken[] token_list) {
            for(int i = 0; i < token_list.Length; i++) {
                TToken tkn = token_list[i];
                switch (tkn.TokenType) {
                case ETokenType.BlockComment:
                case ETokenType.LineComment:
                case ETokenType.White:
                    break;

                default:
                    return i;
                }
            }

            return -1;
        }

        public object ParseLine(int line_top_idx, TToken[] token_list) {
            TokenList = token_list;
            TokenPos = line_top_idx;

            if (TokenPos < TokenList.Length) {

                CurTkn = TokenList[TokenPos];
            }
            else {

                CurTkn = EOTToken;
            }

            if (TokenPos + 1 < TokenList.Length) {

                NextTkn = TokenList[TokenPos + 1];
            }
            else {

                NextTkn = EOTToken;
            }

            if(CurTkn.Kind == EKind.static_) {

                GetToken(EKind.static_);

                switch (CurTkn.Kind) {
                case EKind.function_:
                    return ReadFunctionLine(true);

                case EKind.Identifier:
                    return ReadFieldLine(true);

                default:
                    throw new TParseException();
                }
            }

            TTerm t1;
            object line_obj = null;
            try {
                switch (CurTkn.Kind) {
                case EKind.class_:
                case EKind.enum_:
                    return ReadClassLine();

                case EKind.function_:
                case EKind.constructor_:
                    return ReadFunctionLine(false);

                case EKind.var_:
                    return ReadVariableDeclarationLine();

                case EKind.if_:
                    return ReadIfLine();

                case EKind.else_:
                    return ReadElseLine();

                case EKind.switch_:
                    return ReadSwitchLine();

                case EKind.case_:
                    return ReadCaseLine();

                case EKind.default_:
                    return new TCase();

                case EKind.while_:
                    return ReadWhileLine();

                case EKind.for_:
                    return ReadForLine();

                case EKind.try_:
                    return ReadTryLine();

                case EKind.catch_:
                    break;

                case EKind.return_:
                case EKind.yield_:
                case EKind.throw_:
                case EKind.break_:
                    return ReadJumpLine();

                case EKind.Identifier:
                    if(NextTkn.Kind == EKind.Colon) {

                        return ReadFieldLine(false);
                    }
                    else {

                        return ReadAssignmentCallLine();
                    }

                default:
                    break;
                }

                t1 = Expression();
            }
            catch (TParseException) {

            }

            return line_obj;
        }

        TToken GetToken(EKind type) {

            if(type != EKind.Undefined && type != CurTkn.Kind) {

                throw new TParseException();
            }

            TToken tkn = CurTkn;

            while (true) {

                TokenPos++;
                if (TokenPos < TokenList.Length) {

                    CurTkn = TokenList[TokenPos];

                    if (CurTkn.TokenType != ETokenType.BlockComment && CurTkn.TokenType != ETokenType.LineComment && CurTkn.TokenType != ETokenType.White) {

                        break;
                    }
                }
                else {

                    CurTkn = EOTToken;
                    break;
                }
            }

            if (TokenPos + 1 < TokenList.Length) {

                NextTkn = TokenList[TokenPos + 1];
            }
            else {

                NextTkn = EOTToken;
            }

            return tkn;
        }

        TTerm PrimaryExpression() {
            switch (CurTkn.Kind) {
            case EKind.Identifier:
                TToken id = GetToken(EKind.Identifier);

                if (CurTkn.Kind == EKind.LP) {
                    GetToken(EKind.LP);

                    TTerm[] expr_list = ExpressionList().ToArray();

                    GetToken(EKind.RP);

                    return new TApply(id.TextTkn, expr_list);
                }
                else {

                    return new TReference(id.TextTkn);
                }

            case EKind.NumberLiteral:
            case EKind.StringLiteral:
            case EKind.CharLiteral:
                TToken tkn = GetToken(EKind.Undefined);

                return new TLiteral(tkn.Kind, tkn.TextTkn);
            }

            throw new TParseException();
        }

        TTerm DotExpression() {
            TTerm t1 = PrimaryExpression();

            if(CurTkn.Kind == EKind.Dot) {
                GetToken(EKind.Dot);

                TToken id = GetToken(EKind.Identifier);

                if (CurTkn.Kind == EKind.LP) {
                    GetToken(EKind.LP);

                    TTerm[] expr_list = ExpressionList().ToArray();

                    GetToken(EKind.RP);

                    return new TMethodApply(t1, id.TextTkn, expr_list);
                }
                else {

                    return new TFieldReference(t1, id.TextTkn);
                }
            }

            return t1;
        }

        TTerm IndexExpression() {
            TTerm t1 = DotExpression();

            if (CurTkn.Kind == EKind.LB) {
                GetToken(EKind.LB);

                TTerm exp = Expression();

                GetToken(EKind.RB);
            }

            return t1;
        }

        TTerm PostIncDecExpression() {
            TTerm t1 = IndexExpression();

            if (CurTkn.Kind == EKind.Inc || CurTkn.Kind == EKind.Dec) {
                TToken opr = GetToken(EKind.Undefined);

                return new TApply(opr.Kind, t1);
            }
            else {

                return t1;
            }
        }

        TTerm UnaryExpression() {
            if (CurTkn.Kind == EKind.Add || CurTkn.Kind == EKind.Sub) {
                TToken opr = GetToken(EKind.Undefined);

                TTerm t1 = PostIncDecExpression();

                return new TApply(opr.Kind, t1);
            }
            else {

                return PostIncDecExpression();
            }
        }

        TTerm MultiplicativeExpression() {
            TTerm t1 = UnaryExpression();

            while (true) {
                switch (CurTkn.Kind) {
                case EKind.Mul:
                case EKind.Div:
                case EKind.Mod:

                    TToken opr = GetToken(EKind.Undefined);
                    TTerm t2 = UnaryExpression();

                    t1 = new TApply(opr.Kind, t1, t2);
                    break;

                default:
                    return t1;
                }
            }
        }

        TTerm AdditiveExpression() {
            TTerm t1 = MultiplicativeExpression();

            while (true) {
                switch (CurTkn.Kind) {
                case EKind.Add:
                case EKind.Sub:

                    TToken opr = GetToken(EKind.Undefined);
                    TTerm t2 = MultiplicativeExpression();

                    t1 = new TApply(opr.Kind, t1, t2);
                    break;

                default:
                    return t1;
                }
            }
        }

        TTerm RelationalExpression() {
            TTerm t1 = AdditiveExpression();

            while (true) {
                switch (CurTkn.Kind) {
                case EKind.Eq:
                case EKind.NE:
                case EKind.LT:
                case EKind.LE:
                case EKind.GT:
                case EKind.GE:

                    TToken opr = GetToken(EKind.Undefined);
                    TTerm t2 = AdditiveExpression();

                    t1 = new TApply(opr.Kind, t1, t2);
                    break;

                default:
                    return t1;
                }
            }
        }

        TTerm NotExpression() {
            if(CurTkn.Kind == EKind.Not_) {

                GetToken(EKind.Not_);
                TTerm t1 = RelationalExpression();

                return new TApply(EKind.Not_, t1);
            }

            return RelationalExpression();
        }

        TTerm AndExpression() {
            TTerm t1 = NotExpression();

            while (true) {
                switch (CurTkn.Kind) {
                case EKind.And_:

                    TToken opr = GetToken(EKind.Undefined);
                    TTerm t2 = NotExpression();

                    t1 = new TApply(opr.Kind, t1, t2);
                    break;

                default:
                    return t1;
                }
            }
        }

        TTerm OrExpression() {
            TTerm t1 = AndExpression();

            while (true) {
                switch (CurTkn.Kind) {
                case EKind.Or_:

                    TToken opr = GetToken(EKind.Undefined);
                    TTerm t2 = AndExpression();

                    t1 = new TApply(opr.Kind, t1, t2);
                    break;

                default:
                    return t1;
                }
            }
        }

        List<TTerm> ExpressionList() {
            List<TTerm> expr_list = new List<TTerm>();

            while (true) {
                TTerm t1 = Expression();
                expr_list.Add(t1);

                if(CurTkn.Kind == EKind.Comma) {

                    GetToken(EKind.Comma);
                }
                else {
                    
                    return expr_list;
                }
            }
        }

        TTerm Expression() {
            return OrExpression();
        }
    }

    public class TToken {
        public ETokenType TokenType;
        public EKind Kind;
        public string TextTkn;
        public int StartPos;
        public int EndPos;

        public TToken(ETokenType token_type, EKind kind, string txt, int start_pos, int end_pos) {
            TokenType = token_type;
            Kind = kind;
            TextTkn = txt;
            StartPos = start_pos;
            EndPos = end_pos;
        }
    }

    public class TLine {
        public int Indent;
        public TToken[] Tokens;
        public object ObjLine;
    }

    public class TParseException : Exception {
        public TParseException() {
        }
    }
}