﻿using System.Diagnostics;
using System.Collections.Generic;
using System;
using Windows.UI;
using System.IO;
using System.Net;

/*--------------------------------------------------------------------------------
        1行の構文解析
--------------------------------------------------------------------------------*/

namespace MyEdit {
    public class TParser {
        static TToken EOTToken = new TToken(ETokenType.White, EKind.EOT,"", 0, 0);
        TToken[] TokenList;
        int TokenPos;
        TToken CurTkn;
        TToken NextTkn;
        Dictionary<string, TClass> ClassTable = new Dictionary<string, TClass>();
        Dictionary<string, TType> TypeTable = new Dictionary<string, TType>();

        public TParser() {
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

            Debug.WriteLine("read class : {0}{1}", "", cls.ClassLine());

            return cls;
        }

        public TField ReadFieldLine() {
            TToken id = GetToken(EKind.Identifier);

            TField fld = new TField(id.TextTkn);

            GetToken(EKind.Colon);

            fld.TypeVar = ReadType();

            GetToken(EKind.EOT);

            Debug.WriteLine("read field : {0}", fld);

            return fld;
        }

        public TClass ReadEnumLine() {
            return null;
        }

        //public TClass ReadClassType() {
        //    switch (CurTkn.Kind) {
        //    case EKind.Identifier:
        //        break;

        //    case EKind.char_:
        //    case EKind.bool_:
        //    case EKind.int_:
        //    case EKind.short_:
        //    case EKind.float_:
        //    case EKind.double_:
        //    case EKind.string_:
        //        break;

        //    default:
        //        throw new TParseException();
        //    }

        //    TToken id = GetToken(EKind.Undefined);
        //    TClass cls1 = GetClassByName(id.TextTkn);

        //    return cls1;
        //}

        public TType ReadType() {
            //TClass cls1 = ReadClassType();
            TToken id = GetToken(EKind.Undefined);
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

        public TFunction ReadFunctionLine() {
            TToken tkn = GetToken(EKind.Undefined);

            TToken fnc_name = GetToken(EKind.Identifier);

            TFunction fnc1 = new TFunction(fnc_name.TextTkn);

            GetToken(EKind.LP);

            while (CurTkn.Kind != EKind.LP) {
                TToken arg_name = GetToken(EKind.Identifier);

                TVariable var1 = new TVariable(arg_name.TextTkn);

                if (CurTkn.Kind == EKind.Colon) {

                    GetToken(EKind.Colon);

                    var1.TypeVar = ReadType();
                }
            }

            GetToken(EKind.RP);

            if(CurTkn.Kind == EKind.Colon) {

                GetToken(EKind.Colon);

                fnc1.ReturnType = ReadType();
            }

            return fnc1;
        }

        public TVariable ReadVariableDeclarationLine() {
            return null;
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

            object obj = ExpressionList();

            if(obj is TTerm) {

                case1.TermsCase.Add((TTerm)obj);
            }
            else {

                case1.TermsCase.AddRange((List<TTerm>)obj);
            }

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

        public TStatement ReadAssignmentCallLine() {
            return null;
        }

        public object ParseLine(TToken[] token_list) {
            TokenList = token_list;
            TokenPos = 0;

            while (true) {
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

                TokenPos++;
            }
            if (TokenPos + 1 < TokenList.Length) {

                NextTkn = TokenList[TokenPos + 1];
            }
            else {

                NextTkn = EOTToken;
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
                    return ReadFunctionLine();

                case EKind.static_:
                case EKind.var_:
                    break;

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

                        return ReadFieldLine();
                    }
                    else {

                    }
                    break;

                case EKind.Type_:
                    break;

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

                return new TReference(id.TextTkn);

            case EKind.LP:
                GetToken(EKind.LP);
                TTerm t1 = Expression();
                t1.WithParenthesis = true;
                GetToken(EKind.RP);

                return t1;

            case EKind.LC:
                break;

            case EKind.LB:
                break;

            }

            return null;
        }

        TTerm DotExpression() {
            TTerm t1 = PrimaryExpression();

            if(CurTkn.Kind == EKind.Dot) {
                GetToken(EKind.Dot);

                TToken id = GetToken(EKind.Identifier);

                if (CurTkn.Kind == EKind.LP) {
                    GetToken(EKind.LP);

                    TTerm exp = Expression();

                    GetToken(EKind.RP);
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

        Object ExpressionList() {
            List<TTerm> expr_list = null;
            TTerm t1 = Expression();

            while (true) {
                switch (CurTkn.Kind) {
                case EKind.Comma:

                    if(expr_list == null) {
                        // 最初の場合

                        expr_list = new List<TTerm>();

                        expr_list.Add(t1);
                    }

                    TToken opr = GetToken(EKind.Undefined);
                    TTerm t2 = Expression();

                    expr_list.Add(t2);
                    break;

                default:
                    if (expr_list == null) {
                        // リストでない場合

                        return t1;
                    }
                    else {
                        // リストの場合

                        return expr_list;
                    }
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
        public TToken[] Tokens;
        public object ObjLine;
    }

    public interface IIndent {
        int Indent();
    }

    public class TParseException : Exception {

    }
}