using System.Diagnostics;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

/*--------------------------------------------------------------------------------
        1行の構文解析
--------------------------------------------------------------------------------*/

namespace Miyu {

    public partial class TParser : TEnv {
        public const int TabSize = 4;
        public const string HTMLHead = "<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Transitional//EN\" \"http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd\">\r\n<html xmlns=\"http://www.w3.org/1999/xhtml\" >\r\n<head>\r\n<meta charset=\"utf-8\" />\r\n<title>Untitled Page</title>\r\n<style type=\"text/css\">\r\n.reserved {\r\n\tcolor: blue;\r\n}\r\n.class {\r\n\tcolor: Teal;\r\n}\r\n.string {\r\n\tcolor: red;\r\n}\r\n.comment {\r\n\tcolor: #008000;\r\n}\r\n</style>\r\n</head>\r\n<body>";
        public static TToken EOTToken = new TToken(ETokenType.White, EKind.EOT,"", 0, 0);
        public static TParser theParser;

        [ThreadStatic]
        public TType LookaheadClass;

        [ThreadStatic]
        public static TType CurrentClass;

        [ThreadStatic]
        public TProject PrjParser;

        public TToken[] TokenList;
        public int TokenPos;
        public TToken CurTkn;
        public TToken NextTkn;
        public bool Dirty;
        public bool Running;
        public ELanguage LanguageSP;

        // キーワードの文字列の辞書
        public Dictionary<string, EKind> KeywordMap;

        // 2文字の記号の表
        public EKind[,] SymbolTable2 = new EKind[256, 256];

        // 1文字の記号の表
        public EKind[] SymbolTable1 = new EKind[256];

        public Dictionary<EKind, string> KindString = new Dictionary<EKind, string>();


        public TParser(TProject prj) {
            PrjParser = prj;

            // 字句解析の初期処理をします。
            InitializeLexicalAnalysis();
        }

        public string Tab(int nest) {
            return new string(' ', nest * TabSize);
        }

        public virtual void LPopt() {
        }

        public virtual void RPopt() {
        }

        public virtual void LCopt() {
        }

        public virtual void Colonopt() {
        }

        public TType ReadEnumLine(TSourceFile src, TModifier mod1) {
            GetToken(EKind.enum_);
            TToken id = GetToken2(EKind.Identifier, EKind.ClassName);

            TType cls = PrjParser.GetClassByName(id.TextTkn);
            cls.ModifierCls = mod1;
            cls.KindClass = EClass.Enum;
            cls.SourceFileCls = src;

            LCopt();
            GetToken(EKind.EOT);

            return cls;
        }

        public TType ReadClassLine(TSourceFile src, TModifier mod1) {
            EKind kind = CurTkn.Kind;

            GetToken(EKind.Undefined);
            TToken id = GetToken2(EKind.Identifier, EKind.ClassName);

            TType cls;

            if (CurTkn.Kind == EKind.LT) {
                // 総称型の場合

                List<TType> param_classes = new List<TType>();

                GetToken(EKind.LT);
                while (true) {
                    TToken param_name = GetToken(EKind.Identifier);

                    TType param_class = new TType(param_name.TextTkn);
                    param_class.GenericType = EGeneric.ArgumentClass;

                    param_classes.Add(param_class);

                    if (CurTkn.Kind != EKind.Comma) {

                        break;
                    }

                    GetToken(EKind.Comma);
                }
                GetToken(EKind.GT);

                cls = Project.GetParameterizedClass(id.TextTkn, param_classes);
            }
            else {

                cls = PrjParser.GetClassByName(id.TextTkn);
            }

            switch (kind) {
            case EKind.class_:
                cls.KindClass = EClass.Class;
                break;

            case EKind.struct_:
                cls.KindClass = EClass.Struct;
                break;

            case EKind.interface_:
                cls.KindClass = EClass.Interface;
                break;

            default:
                Debug.Assert(false);
                break;
            }

            if (cls.ModifierCls == null || mod1.isPublic) {

                cls.ModifierCls = mod1;
                cls.SourceFileCls = src;
            }

            if (CurTkn.Kind == EKind.Colon) {

                GetToken(EKind.Colon);
                while (true) {

                    TType super_class = ReadType(cls, false);

                    cls.SuperClasses.Add(super_class);

                    if(CurTkn.Kind == EKind.Comma) {

                        GetToken(EKind.Comma);
                    }
                    else {

                        break;
                    }
                }
            }

            LCopt();
            GetToken(EKind.EOT);

            if (cls.ClassName == "int") {
                Project.IntClass = cls;
            }
            else if (cls.ClassName == "float") {
                Project.FloatClass = cls;
            }
            else if (cls.ClassName == "double") {
                Project.DoubleClass = cls;
            }
            else if (cls.ClassName == "char") {
                Project.CharClass = cls;
            }
            else if (cls.ClassName == "object") {
                Project.ObjectClass = cls;
            }
            else if (cls.ClassName == "string") {
                Project.StringClass = cls;
            }
            else if (cls.ClassName == "bool") {
                Project.BoolClass = cls;
            }
            else if (cls.ClassName == "void") {
                Project.VoidClass = cls;
            }
            else if (cls.ClassName == "Type") {
                Project.TypeClass = cls;
            }
            else if (cls.ClassName == "Action") {
                Project.ActionClass = cls;
            }
            else if (cls.ClassName == "Enumerable") {
                Project.EnumerableClass = cls as TGenericClass;
            }
            else if (cls.ClassName == "Array") {
                Project.ArrayClass = cls as TGenericClass;
            }

            return cls;
        }

        public TType ReadDelegateLine(TModifier mod1) {
            GetToken(EKind.delegate_);
            TType ret_type = ReadType(null, false);

            TToken id = GetToken2(EKind.Identifier, EKind.ClassName);

            List<TType> param_classes = new List<TType>();

            if (CurTkn.Kind == EKind.LT) {
                // 総称型の場合

                GetToken(EKind.LT);
                while (true) {
                    TToken param_name = GetToken(EKind.Identifier);

                    TType param_class = new TType(param_name.TextTkn);
                    param_class.GenericType = EGeneric.ArgumentClass;

                    param_classes.Add(param_class);

                    if (CurTkn.Kind != EKind.Comma) {

                        break;
                    }

                    GetToken(EKind.Comma);
                }
                GetToken(EKind.GT);
            }

            TType dlg;

            if (param_classes.Count == 0) {

                dlg = PrjParser.GetClassByName(id.TextTkn);
            }
            else {

                dlg = Project.GetParameterizedClass(id.TextTkn, param_classes);
            }

            List<TVariable> vars = ReadArgs(dlg);
            TType[] arg_types = (from t in vars select t.TypeVar).ToArray();

            dlg.KindClass = EClass.Delegate;
            dlg.RetType = ret_type;
            dlg.ArgTypes = arg_types;

            return dlg;
        }

        public TField ReadFieldLine(TType parent_class, TModifier mod1, TType type_prepend) {
            TToken id = GetToken(EKind.Identifier);

            TType tp;
            
            if(type_prepend != null) {

                tp = type_prepend;
            }
            else {

                GetToken(EKind.Colon);
                tp = ReadType(parent_class, false);
            }

            TTerm init = null;
            if (CurTkn.Kind == EKind.Assign) {

                GetToken(EKind.Assign);

                init = Expression();
            }

            LineEnd();

            return new TField(parent_class, mod1, id, tp, init);
        }

        public TField ReadEnumFieldLine(TType parent_class) {
            TToken id = GetToken(EKind.Identifier);

            OptGetToken(EKind.Comma);
            GetToken(EKind.EOT);

            return new TField(parent_class, null, id, parent_class, null);
        }

        public TType ReadType(TType parent_class, bool new_class) {
            TToken id = GetToken2(EKind.Identifier, EKind.ClassName);
            TType cls1 = PrjParser.GetParamClassByName(parent_class, id.TextTkn);

            List<TType> param_classes = null;
            bool contains_argument_class = false;

            if (CurTkn.Kind == EKind.LT) {
                // 総称型の場合

                if (! (cls1 is TGenericClass)) {

                    throw new TParseException(cls1.ClassName + ":総称型以外に引数型があります。");
                }
                TGenericClass org_cla = cls1 as TGenericClass;

                param_classes = new List<TType>();

                GetToken(EKind.LT);
                while (true) {
                    TType param_class = ReadType(parent_class, false);

                    if (param_class.GenericType == EGeneric.ArgumentClass || param_class is TGenericClass && (parent_class as TGenericClass).ContainsArgumentClass) {

                        contains_argument_class = true;
                    }

                    param_classes.Add(param_class);

                    if(CurTkn.Kind != EKind.Comma) {

                        break;
                    }

                    GetToken(EKind.Comma);
                }
                GetToken(EKind.GT);

                if(org_cla.GenCla.Count != param_classes.Count) {

                    throw new TParseException("総称型の引数の数が一致しません。");
                }
            }

            int dim_cnt = 0;
            if (CurTkn.Kind == EKind.LB && ! new_class) {
                GetToken(EKind.LB);

                dim_cnt = 1;
                while (CurTkn.Kind == EKind.Comma) {
                    GetToken(EKind.Comma);
                    dim_cnt++;
                }

                GetToken(EKind.RB);
            }

            if (contains_argument_class) {
                // 引数にArgumentClassを含む場合

                TGenericClass tmp_class = new TGenericClass(cls1 as TGenericClass, param_classes, dim_cnt);
                tmp_class.ContainsArgumentClass = true;

                return tmp_class;
            }

            TType cls2 = null;
            if (param_classes == null) {
                // 引数がない場合

                cls2 = cls1;
            }
            else {
                // 引数がある場合

                cls2 = Project.GetSpecializedClass(cls1 as TGenericClass, param_classes, 0);
            }

            if (dim_cnt == 0) {
                // 配列でない場合

                return cls2;
            }

            List<TType> array_element_class_list = new List<TType> { cls2 };

            if (cls2.GenericType == EGeneric.ArgumentClass) {

                TGenericClass tmp_class = new TGenericClass(Project.ArrayClass, array_element_class_list, dim_cnt);
                tmp_class.ContainsArgumentClass = true;

                return tmp_class;
            }

            return Project.GetSpecializedClass(Project.ArrayClass, array_element_class_list, dim_cnt);
        }

        public virtual void LineEnd() {
            GetToken(EKind.EOT);
        }

        public TUsing ReadUsing() {
            TUsing using1 = new TUsing();

            GetToken(EKind.using_);

            while (true) {
                TToken id = GetToken2(EKind.Identifier, EKind.ClassName);

                using1.Packages.Add(id.TextTkn);
                if(CurTkn.Kind != EKind.Dot) {
                    break;
                }
                GetToken(EKind.Dot);
            }

            LineEnd();

            return using1;
        }

        public TNamespace ReadNamespace() {
            GetToken(EKind.namespace_);

            TToken id = GetToken(EKind.Identifier);
            GetToken(EKind.LC);
            GetToken(EKind.EOT);

            return new TNamespace(id.TextTkn);
        }

        public List<TVariable> ReadArgs(TType parent_class) {
            List<TVariable> vars = new List<TVariable>();

            GetToken(EKind.LP);

            while (CurTkn.Kind != EKind.RP) {
                TVariable var1 = ReadArgVariable(parent_class);
                vars.Add(var1);

                if (CurTkn.Kind != EKind.Comma) {

                    break;
                }

                GetToken(EKind.Comma);
            }

            GetToken(EKind.RP);

            return vars;
        }

        public TFunction ReadFunctionLine(TType parent_class, TToken constructor_token, TType constructor_class, TModifier mod1, TType ret_type_prepend) {
            TToken fnc_name;
            EKind kind_fnc = EKind.function_;
            
            if(constructor_class != null) {
                fnc_name = constructor_token;
                kind_fnc = EKind.constructor_;
            }
            else {

                if (CurTkn.Kind == EKind.operator_) {

                    GetToken(EKind.operator_);
                    fnc_name = GetToken(EKind.Undefined);
                    if (fnc_name.TokenType != ETokenType.Symbol) {
                        throw new TParseException();
                    }
                }
                else {

                    fnc_name = GetToken(EKind.Identifier);
                }
            }

            List<TVariable> vars = ReadArgs(parent_class);

            TType ret_type = ret_type_prepend;
            TApply base_app = null;

            if(CurTkn.Kind == EKind.Colon) {

                GetToken(EKind.Colon);

                if(ret_type_prepend == null) {

                    ret_type = ReadType(parent_class, false);
                }
                else if(constructor_class != null) {

                    if(CurTkn.Kind == EKind.base_) {

                        base_app = PrimaryExpression() as TApply;
                    }
                    else {
                        throw new TParseException();
                    }
                }
            }

            if(CurTkn.Kind == EKind.SemiColon) {

                GetToken(EKind.SemiColon);
            }
            else {

                LCopt();
            }

            GetToken(EKind.EOT);

            return new TFunction(mod1, fnc_name, vars.ToArray(), ret_type, base_app, kind_fnc);
        }

        public virtual TVariable ReadArgVariable(TType parent_class) {
            EKind kind = EKind.Undefined;

            switch (CurTkn.Kind) {
            case EKind.ref_:
                GetToken(EKind.ref_);
                kind = EKind.ref_;
                break;

            case EKind.out_:
                GetToken(EKind.out_);
                kind = EKind.out_;
                break;

            case EKind.params_:
                GetToken(EKind.params_);
                kind = EKind.params_;
                break;
            }

            TToken id = GetToken(EKind.Identifier);

            TType type = null;
            if (CurTkn.Kind == EKind.Colon) {

                GetToken(EKind.Colon);

                type = ReadType(parent_class, false);
            }

            return new TVariable(id, type, kind);
        }

        public TVariable ReadVariable(TType type_prepend) {
            TToken id = GetToken(EKind.Identifier);

            TType type = null;
            if (type_prepend != null) {

                type = type_prepend;
            }
            else { 

                if (CurTkn.Kind == EKind.Colon) {

                    GetToken(EKind.Colon);

                    type = ReadType(null, false);
                }
            }

            TTerm init = null;
            if (CurTkn.Kind == EKind.Assign) {

                GetToken(EKind.Assign);

                init = Expression();
            }

            return new TVariable(null, id, type, init);
        }

        public virtual TVariableDeclaration ReadVariableDeclarationLine(TType type_prepend, bool in_for) {
            TVariableDeclaration var_decl = new TVariableDeclaration();

            if(type_prepend == null) {

                GetToken(EKind.var_);
                var_decl.IsVar = true;
            }

            while (true) {
                TVariable var1 = ReadVariable(type_prepend);

                var_decl.Variables.Add(var1);

                if(CurTkn.Kind != EKind.Comma) {

                    break;
                }

                GetToken(EKind.Comma);
            }

            if(!in_for && TokenList[TokenList.Length - 1].Kind != EKind.LC) {

                LineEnd();
            }

            return var_decl;
        }

        public TIfBlock ReadIfLine() {
            TIfBlock if_block = new TIfBlock();

            GetToken(EKind.if_);
            LPopt();

            if_block.ConditionIf = Expression();
            RPopt();
            LCopt();

            GetToken(EKind.EOT);
            return if_block;
        }

        public TIfBlock ReadElseLine() {
            TIfBlock if_block = new TIfBlock();
            if_block.IsElse = true;

            GetToken(EKind.else_);

            if(CurTkn.Kind == EKind.if_) {

                GetToken(EKind.if_);
                LPopt();
                if_block.ConditionIf = Expression();
                RPopt();
            }
            LCopt();
            GetToken(EKind.EOT);

            return if_block;
        }

        public TSwitch ReadSwitchLine() {
            TSwitch switch1 = new TSwitch();

            GetToken(EKind.switch_);

            LPopt();
            switch1.TermSwitch = Expression();
            RPopt();
            LCopt();
            GetToken(EKind.EOT);

            return switch1;
        }

        public TCase ReadCaseLine() {
            TCase case1 = new TCase();

            TToken tkn = GetToken(EKind.case_);

            List<TTerm> expr_list = ExpressionList();
            case1.TermsCase.AddRange(expr_list);

            Colonopt();
            if(CurTkn.Kind == EKind.LC) {
                GetToken(EKind.LC);
            }
            GetToken(EKind.EOT);
            return case1;
        }

        public TWhile ReadWhileLine() {
            TWhile while1 = new TWhile();

            GetToken(EKind.while_);

            LPopt();
            while1.WhileCondition = Expression();
            RPopt();

            LCopt();
            GetToken(EKind.EOT);
            return while1;
        }

        public TLock ReadLockLine() {
            TLock lock1 = new TLock();

            GetToken(EKind.lock_);

            LPopt();
            lock1.LockObj = Expression();
            RPopt();

            LCopt();
            GetToken(EKind.EOT);
            return lock1;
        }

        public TForEach ReadForEachLine() {
            TForEach for1 = new TForEach();

            GetToken(EKind.foreach_);
            LPopt();

            if(CurTkn.Kind == EKind.ClassName) {

                for1.LoopVariable = ReadArgVariable(null);
            }
            else {

                TToken id = GetToken(EKind.Identifier);
                for1.LoopVariable = new TVariable(id);
            }

            GetToken(EKind.in_);

            for1.ListFor = Expression();

            RPopt();
            LCopt();
            GetToken(EKind.EOT);

            return for1;
        }

        public TFor ReadForLine() {
            TFor for1 = new TFor();

            GetToken(EKind.for_);
            GetToken(EKind.LP);

            if (CurTkn.Kind != EKind.SemiColon) {

                if (CurTkn.Kind == EKind.ClassName) {
                    TType tp = ReadType(null, false);

                    for1.InitStatement = ReadVariableDeclarationLine(tp, true);
                    for1.LoopVariable = (for1.InitStatement as TVariableDeclaration).Variables[0];
                }
                else {

                    for1.InitStatement = ReadAssignmentCallLine(true) as TStatement;
                }
            }

            GetToken(EKind.SemiColon);

            if (CurTkn.Kind != EKind.SemiColon) {

                for1.ConditionFor = Expression();
            }

            GetToken(EKind.SemiColon);

            if(CurTkn.Kind != EKind.RP) {

                for1.PostStatement = ReadAssignmentCallLine(true) as TStatement;
            }

            GetToken(EKind.RP);
            GetToken2(EKind.LC, EKind.SemiColon);
            GetToken(EKind.EOT);

            return for1;
        }

        public TTry ReadTryLine() {
            GetToken(EKind.try_);
            LCopt();
            GetToken(EKind.EOT);
            return new TTry();
        }

        public TCatch ReadCatchLine() {
            GetToken(EKind.catch_);
            LPopt();

            TType tp = ReadType(null, false);

            string name = "";
            if(CurTkn.Kind == EKind.Identifier) {
                name = GetToken(EKind.Identifier).TextTkn;
            }

            RPopt();
            LCopt();
            GetToken(EKind.EOT);

            return new TCatch(new TVariable(name, tp));
        }

        public TJump ReadJumpLine() {
            TToken tkn = GetToken(EKind.Undefined);

            TJump jump = new TJump(tkn.Kind);

            switch (tkn.Kind) {
            case EKind.yield_:
                if(CurTkn.Kind == EKind.return_) {

                    GetToken(EKind.return_);
                    jump.RetVal = Expression();
                }
                else if (CurTkn.Kind == EKind.break_) {

                    GetToken(EKind.break_);
                }
                else {
                    throw new TParseException();
                }
                break;

            case EKind.return_:
            case EKind.throw_:
                if(CurTkn.Kind != EKind.SemiColon && CurTkn.Kind != EKind.EOT) {

                    jump.RetVal = Expression();
                }
                break;

            case EKind.break_:                
                break;

            case EKind.goto_:
                TToken lbl = GetToken(EKind.Identifier);
                jump.LabelJmp = lbl.TextTkn;
                break;
            }

            LineEnd();

            return jump;
        }

        public object ReadAssignmentCallLine(bool in_for) {
            TAssignment asn = null;

            TTerm t1 = Expression();

            switch (CurTkn.Kind) {
            case EKind.Assign:
            case EKind.AddEq:
            case EKind.SubEq:
            case EKind.DivEq:
            case EKind.ModEq:

                TToken opr = GetToken(EKind.Undefined);

                TTerm t2 = Expression();

                TApply app1 = new TApply(opr, t1, t2);

                asn = new TAssignment(app1);
                break;
            }

            if(!in_for) {

                if(CurTkn.Kind == EKind.Comma) {
                    GetToken(EKind.Comma);
                    GetToken(EKind.EOT);
                    return t1;
                }

                if(LambdaFunction != null) {

                    GetToken(EKind.EOT);
                }
                else {

                    LineEnd();
                }
            }

            if(asn != null) {
                return asn;
            }

            if (!(t1 is TApply)) {
                throw new TParseException();
            }

            return new TCall(t1 as TApply);
        }

        public int LineTopTokenIndex(TToken[] token_list) {
            for(int i = 0; i < token_list.Length; i++) {
                TToken tkn = token_list[i];
                switch (tkn.TokenType) {
                case ETokenType.BlockComment:
                case ETokenType.BlockCommentContinued:
                case ETokenType.LineComment:
                case ETokenType.White:
                    break;

                default:
                    return i;
                }
            }

            return -1;
        }

        public object ParseLine(TSourceFile src, TType cls, TFunction parent_fnc, TStatement parent_stmt, int line_top_idx, TToken[] token_list) {
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

            try {
                if(parent_stmt is TVariableDeclaration) {
                    TVariable var1 = (parent_stmt as TVariableDeclaration).Variables[0];

                    if(! (var1.InitValue is TNewApply)) {
                        throw new TParseException();
                    }

                    TTerm ele = Expression();

                    OptGetToken(EKind.Comma);

                    (var1.InitValue as TNewApply).InitList.Add(ele);

                    return ele;
                }

                TModifier mod1 = new TModifier();
                while (true) {
                    switch (CurTkn.Kind) {
                    case EKind.public_:
                        GetToken(EKind.public_);
                        mod1.isPublic = true;
                        break;

                    case EKind.private_:
                        GetToken(EKind.private_);
                        mod1.isPrivate = true;
                        break;

                    case EKind.abstract_:
                        GetToken(EKind.abstract_);
                        mod1.isAbstract = true;
                        break;

                    case EKind.sealed_:
                        GetToken(EKind.sealed_);
                        mod1.isSealed = true;
                        break;

                    case EKind.partial_:
                        GetToken(EKind.partial_);
                        mod1.isPartial = true;
                        break;

                    case EKind.const_:
                        GetToken(EKind.const_);
                        mod1.isConst = true;
                        break;

                    case EKind.static_:
                        GetToken(EKind.static_);
                        mod1.isStatic = true;
                        break;

                    case EKind.virtual_:
                        GetToken(EKind.virtual_);
                        mod1.isVirtual = true;
                        break;

                    case EKind.override_:
                        GetToken(EKind.override_);
                        mod1.isOverride = true;
                        break;

                    case EKind.async_:
                        GetToken(EKind.async_);
                        mod1.isAsync = true;
                        break;

                    case EKind.RC:
                        GetToken(EKind.RC);
                        if (CurTkn.Kind == EKind.RP && InLambdaFunction) {
                            InLambdaFunction = false;
                            GetToken(EKind.RP);
                        }

                        if(CurTkn.Kind == EKind.SemiColon) {

                            GetToken(EKind.SemiColon);
                        }
                        GetToken(EKind.EOT);
                        return null;

                    case EKind.get_:
                        GetToken(EKind.get_);
                        GetToken(EKind.LC);
                        GetToken(EKind.EOT);
                        return null;

                    default:
                        goto start_l;
                    }
                }
                start_l:

                switch (CurTkn.Kind) {
                case EKind.using_:
                    return ReadUsing();

                case EKind.namespace_:
                    return ReadNamespace();

                case EKind.enum_:
                    return ReadEnumLine(src, mod1);

                case EKind.class_:
                case EKind.struct_:
                case EKind.interface_:
                    return ReadClassLine(src, mod1);

                case EKind.delegate_:
                    return ReadDelegateLine(mod1);

                case EKind.var_:
                    return ReadVariableDeclarationLine(null, false);

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

                case EKind.lock_:
                    return ReadLockLine();

                case EKind.while_:
                    return ReadWhileLine();

                case EKind.for_:
                    return ReadForLine();

                case EKind.foreach_:
                    return ReadForEachLine();

                case EKind.try_:
                    return ReadTryLine();

                case EKind.catch_:
                    return ReadCatchLine();

                case EKind.return_:
                case EKind.yield_:
                case EKind.throw_:
                case EKind.break_:
                case EKind.goto_:
                    return ReadJumpLine();

                case EKind.ClassName:
                    TToken class_token = CurTkn;
                    TType tp = ReadType(null, false);

                    if (CurTkn.Kind == EKind.LP) {

                        return ReadFunctionLine(cls, class_token, tp, mod1, tp);
                    }

                    if (CurTkn.Kind != EKind.Identifier) {

                        LookaheadClass = tp;
                        return ReadAssignmentCallLine(false);
                    }

                    if (NextTkn.Kind == EKind.LP) {

                        return ReadFunctionLine(cls, null, null, mod1, tp);
                    }
                    else {

                        if (parent_fnc == null) {

                            return ReadFieldLine(cls, mod1, tp);
                        }
                        else {

                            return ReadVariableDeclarationLine(tp, false);
                        }
                    }

                case EKind.Identifier:
                case EKind.base_:
                    if(cls != null && cls.KindClass == EClass.Enum) {

                        return ReadEnumFieldLine(cls);
                    }

                    if (NextTkn.Kind == EKind.Colon) {
                        if(TokenPos + 2 < TokenList.Length) {

                            return ReadFieldLine(cls, mod1, null);
                        }
                        else {

                            TToken id = GetToken(EKind.Identifier);
                            GetToken(EKind.Colon);
                            return new TLabelStatement(id);
                        }
                    }
                    else if (parent_fnc == null) {

                        return ReadFunctionLine(cls, null, null, mod1, null);
                    }
                    else {

                        return ReadAssignmentCallLine(false);
                    }

                case EKind.this_:
                case EKind.await_:
                case EKind.LP:
                case EKind.StringLiteral:
                case EKind.typeof_:
                    return ReadAssignmentCallLine(false);

                case EKind.operator_:
                    return ReadFunctionLine(cls, null, null, mod1, null);

                case EKind.LB:
                    GetToken(EKind.LB);
                    TType attr = ReadType(null, false);
                    GetToken(EKind.RB);
                    return new TAttribute(attr);

                default:
                    Debug.WriteLine("行頭 {0}", CurTkn.Kind);
                    throw new TParseException();
                }
            }
            catch (TParseException) {
            }

            return null;
        }

        public void GetVariableClass(TSourceFile src, int current_line_idx, out List<TVariable> vars) {
            vars = new List<TVariable>();

            int min_indent = src.Lines[current_line_idx].Indent;

            for (int line_idx = current_line_idx; 0 <= line_idx; line_idx--) {
                TLine line = src.Lines[line_idx];

                if (line.ObjLine != null && line.Indent <= min_indent) {

                    if (line.Indent < min_indent) {

                        min_indent = line.Indent;
                    }

                    if (line.ObjLine is TVariableDeclaration) {

                        TVariableDeclaration var_decl = line.ObjLine as TVariableDeclaration;
                        vars.AddRange(var_decl.Variables);
                    }
                    else if (line.ObjLine is TFunction) {
                        TFunction fnc = line.ObjLine as TFunction;
                        vars.AddRange(fnc.ArgsFnc);
                    }
                    else if (line.ObjLine is TAbsFor) {
                        TAbsFor for1 = line.ObjLine as TAbsFor;
                        if (for1.LoopVariable != null) {

                            vars.Add(for1.LoopVariable);
                        }
                    }
                    else if (line.ObjLine is TCatch) {
                        TCatch catch1 = line.ObjLine as TCatch;
                        vars.Add(catch1.CatchVariable);
                    }
                    else if (line.ObjLine is TType) {

                        return;
                    }
                    foreach(TVariable x in vars) {
                        Debug.Assert(x != null);
                    }
                }
            }
        }

        public async void ParseFile(TSourceFile src) {
            while (Running) {
                await Task.Delay(1);
            }
            Running = true;
            Dirty = false;
            //Debug.WriteLine("parse file : 開始 {0}", Path.GetFileName(src.PathSrc), "");

            src.ClassesSrc.Clear();

            object prev_obj = null;
            List<TToken> comments = new List<TToken>();
            List<object> obj_stack = new List<object>();

            for(int line_idx = 0; line_idx < src.Lines.Count; line_idx++) {
                //await Task.Delay(1);

                //Debug.WriteLine("parse file : {0} {1}", line_idx, Dirty);
                if (Dirty) {
                    Debug.WriteLine("parse file : 中断");
                    Running = false;
                    return;
                }

                TLine line = src.Lines[line_idx];

                line.Indent = -1;
                line.ObjLine = null;
                if (line.Tokens == null || line.Tokens.Length == 0) {
                    comments.Add(new TToken(EKind.NL));
                }
                else {
                    int line_top_idx = LineTopTokenIndex(line.Tokens);

                    if (line_top_idx == -1) {
                        comments.Add(line.Tokens[0]);
                    }
                    else { 

                        TToken line_top_token = line.Tokens[line_top_idx];
                        line.Indent = line_top_token.StartPos;

                        switch (line_top_token.TokenType) {
                        case ETokenType.VerbatimString:
                        case ETokenType.VerbatimStringContinued:
                        case ETokenType.Error:
                            break;

                        default:

                            if (line.Indent < obj_stack.Count) {

                                obj_stack.RemoveRange(line.Indent, obj_stack.Count - line.Indent);
                            }

                            TType cls = null;
                            TFunction parent_fnc = null;
                            TBlockStatement parent_stmt = null;
                            List<object> obj_stack_rev = new List<object>(obj_stack);
                            obj_stack_rev.Reverse();

                            // スタックの中からクラスを探します。
                            var vcls = from x in obj_stack_rev where x is TType select x as TType;
                            if (vcls.Any()) {
                                cls = vcls.First();

                                if (cls is TGenericClass) {
                                    TGenericClass gen = cls as TGenericClass;

                                    var vtkn = from t in gen.GenCla from tk in line.Tokens where tk.TextTkn == t.ClassName select tk;
                                    foreach (TToken tk in vtkn) {
                                        tk.Kind = EKind.ClassName;
                                    }
                                }
                            }
                            line.ClassLine = cls;
                            CurrentClass = cls;

                            // スタックの中から関数を探します。
                            var vfnc = from x in obj_stack_rev where x is TFunction select x as TFunction;
                            if (vfnc.Any()) {
                                parent_fnc = vfnc.First();
                            }

                            // スタックの中から最も内側の文を探します。
                            var vstmt = from x in obj_stack_rev where x is TBlockStatement select x as TBlockStatement;
                            if (vstmt.Any()) {
                                parent_stmt = vstmt.First();
                            }

                            if (parent_stmt == null && parent_fnc != null) {
                                parent_stmt = parent_fnc.BlockFnc;
                            }

                            //Debug.Write(string.Format("行解析 {0}", line.TextLine));
                            LambdaFunction = null;
                            object obj = ParseLine(src, cls, parent_fnc, parent_stmt, line_top_idx, line.Tokens);
                            if (obj != null) {

                                while (obj_stack.Count < line.Indent) {
                                    obj_stack.Add(null);
                                }

                                if (LambdaFunction != null) {
                                    obj_stack.Add(LambdaFunction);

                                    LambdaFunction.ClassMember = cls;
                                    cls.Functions.Add(LambdaFunction);
                                    src.FunctionsSrc.Add(LambdaFunction);

                                    LambdaFunction = null;
                                }
                                else {

                                    obj_stack.Add(obj);
                                    Debug.Assert(obj_stack.IndexOf(obj) == line.Indent);
                                }


                                if (obj is TType) {
                                    TType class_def = obj as TType;

                                    if(class_def.SourceFileCls == src) {

                                        class_def.CommentCls = comments.ToArray();
                                    }
                                    //ClassLineText(class_def, sw);
                                    src.ClassesSrc.Add(class_def);
                                }
                                else if (obj is TVariable) {
                                    TVariable var1 = obj as TVariable;

                                    if (prev_obj is TAttribute) {
                                        var1.AttributeVar = prev_obj as TAttribute;
                                    }

                                    if (cls != null) {

                                        if (obj is TField) {
                                            TField fld = obj as TField;

                                            fld.CommentVar = comments.ToArray();
                                            fld.ClassMember = cls;
                                            cls.Fields.Add(fld);
                                            src.FieldsSrc.Add(fld);
                                        }
                                        else if (obj is TFunction) {
                                            TFunction fnc = obj as TFunction;

                                            fnc.CommentVar = comments.ToArray();
                                            fnc.ClassMember = cls;
                                            cls.Functions.Add(fnc);
                                            src.FunctionsSrc.Add(fnc);
                                        }
                                    }
                                }
                                else if (obj is TUsing) {

                                    src.Usings.Add(obj as TUsing);
                                }
                                else if(obj is TNamespace) {
                                    src.Namespace = obj as TNamespace;
                                    src.Namespace.CommentNS = comments.ToArray();
                                }
                                else if (obj is TStatement) {
                                    TStatement stmt = obj as TStatement;

                                    stmt.CommentStmt = comments.ToArray();
                                    if (parent_stmt != null || parent_fnc != null) {
                                        if (stmt is TCase) {
                                            TCase case1 = stmt as TCase;

                                            if (parent_stmt.StatementsBlc.Count == 0) {

                                                Debug.WriteLine("caseがブロックの先頭にある。");
                                            }
                                            else {
                                                TStatement last_stmt = parent_stmt.StatementsBlc[parent_stmt.StatementsBlc.Count - 1];
                                                if (last_stmt is TSwitch) {

                                                    (last_stmt as TSwitch).Cases.Add(case1);
                                                }
                                                else {

                                                    Debug.WriteLine("caseの前にswitchがない。");
                                                }
                                            }
                                        }
                                        else {

                                            parent_stmt.StatementsBlc.Add(stmt);
                                        }
                                    }
                                }
                            }

                            line.ObjLine = obj;
                            prev_obj = obj;

                            break;
                        }
                        if(! (prev_obj is TAttribute)) {

                            comments.Clear();
                        }
                    }
                }
            }
            CurrentClass = null;

            //foreach (MyEditor editor in src.Editors) {
            //    editor.InvalidateCanvas();
            //}

            Running = false;
        }

        public void ResolveName(TSourceFile src) {
            //Debug.WriteLine("名前解決 : {0} -------------------------------------------------------", Path.GetFileName(src.PathSrc), "");
            for (int line_idx = 0; line_idx < src.Lines.Count; line_idx++) {
                //await Task.Delay(1);
                if (Dirty) {
                    Debug.WriteLine("名前解決 : 中断");
                    Running = false;
                    return;
                }

                TLine line = src.Lines[line_idx];
                //Debug.WriteLine("名前解決 : {0}", line.TextLine.TrimEnd(), "");

                if (line.ObjLine is TStatement) {
                    TStatement stmt = line.ObjLine as TStatement;

                    List<TVariable> vars;
                    GetVariableClass(src, line_idx, out vars);

                    // 名前解決のエラーをクリアします。
                    lock (src) {
                        var name_err_tkns = from x in line.Tokens where x.ErrorTkn is TResolveNameException select x;
                        foreach (TToken name_err_tkn in name_err_tkns) {
                            name_err_tkn.ErrorTkn = null;
                        }
                    }

                    try {
                        stmt.ResolveName(line.ClassLine, vars);
                    }
                    catch (TResolveNameException) {
                    }
                }
            }
        }

        public TToken GetToken(EKind type) {

            if(type != EKind.Undefined && type != CurTkn.Kind) {

                throw new TParseException();
            }

            TToken tkn = CurTkn;

            while (true) {

                TokenPos++;
                if (TokenPos < TokenList.Length) {

                    CurTkn = TokenList[TokenPos];

                    switch (CurTkn.TokenType) {
                    case ETokenType.White:
                    case ETokenType.BlockComment:
                    case ETokenType.BlockCommentContinued:
                    case ETokenType.LineComment:
                        break;

                    default:
                        goto while_end;
                    }
                }
                else {

                    CurTkn = EOTToken;
                    break;
                }
            }
            while_end:

            if (TokenPos + 1 < TokenList.Length) {

                NextTkn = TokenList[TokenPos + 1];
            }
            else {

                NextTkn = EOTToken;
            }

            return tkn;
        }

        public TToken GetToken2(EKind kind1, EKind kind2) {
            if (CurTkn.Kind != kind1 && CurTkn.Kind != kind2) {

                throw new TParseException();
            }
            return GetToken(EKind.Undefined);
        }

        public void OptGetToken(EKind kind) {
            if (CurTkn.Kind == kind) {

                GetToken(kind);
            }
        }

        public TFrom FromExpression() {
            TFrom from1 = new TFrom();

            GetToken(EKind.from_);

            TToken id1 = GetToken(EKind.Identifier);
            from1.VarQry = new TVariable(id1);

            GetToken(EKind.in_);
            from1.SeqQry = Expression();

            if (CurTkn.Kind == EKind.where_) {
                GetToken(EKind.where_);
                from1.CndQry = Expression();
            }

            if (CurTkn.Kind == EKind.select_) {
                GetToken(EKind.select_);
                from1.SelFrom = Expression();
            }

            if (CurTkn.Kind == EKind.from_) {
                from1.InnerFrom = FromExpression();
            }

            if(from1.SelFrom == null && from1.InnerFrom == null) {
                throw new TParseException();
            }

            return from1;
        }

        public TTerm PrimaryExpression() {
            TToken id;
            TTerm[] args;
            TType cls;
            TTerm term;
            TToken opr;
            List<TTerm> init;

            if (LookaheadClass != null) {
                TReference ref_class = new TReference(LookaheadClass);
                LookaheadClass = null;
                return ref_class;
            }

            switch (CurTkn.Kind) {
            case EKind.Identifier:
            case EKind.this_:
            case EKind.true_:
            case EKind.false_:
            case EKind.null_:
            case EKind.base_:
                id = GetToken(EKind.Undefined);

                if (CurTkn.Kind == EKind.LP) {
                    GetToken(EKind.LP);

                    TTerm[] expr_list = ExpressionList().ToArray();

                    GetToken(EKind.RP);

                    return new TApply(id, expr_list);
                }
                else if(CurTkn.Kind == EKind.Lambda) {
                    GetToken(EKind.Lambda);

                    TTerm ret = Expression();
                    TFunction fnc = new TFunction(id, ret);

                    return new TReference(fnc);
                }
                else {

                    return new TReference(id);
                }

            case EKind.LP:
                GetToken(EKind.LP);
                if(CurTkn.Kind == EKind.ClassName) {

                    cls = ReadType(null, false);
                    if(CurTkn.Kind == EKind.RP) {

                        GetToken(EKind.RP);
                        term = Expression();
                        term.CastType = cls;

                        return term;
                    }
                    else {

                        LookaheadClass = cls;
                    }
                }
                else if(CurTkn.Kind == EKind.RP) {
                    GetToken(EKind.RP);
                    TToken lambda = GetToken(EKind.Lambda);
                    GetToken(EKind.LC);

                    LambdaFunction = new TFunction(lambda);
                    InLambdaFunction = true;

                    return new TReference(LambdaFunction);
                }

                term = Expression();

                GetToken(EKind.RP);

                term.WithParenthesis = true;
                return term;


            case EKind.NumberLiteral:
            case EKind.StringLiteral:
            case EKind.CharLiteral:
                TToken tkn = GetToken(EKind.Undefined);

                return new TLiteral(tkn);

            case EKind.new_:
                TToken new_tkn = GetToken(EKind.new_);

                cls = ReadType(null, true);
                if(CurTkn.Kind == EKind.LP) {

                    GetToken(EKind.LP);

                    args = ExpressionList().ToArray();

                    GetToken(EKind.RP);

                    return new TNewApply(EKind.NewInstance, new_tkn, cls, args, null);
                }
                else if (CurTkn.Kind == EKind.LB) {

                    GetToken(EKind.LB);

                    args = ExpressionList().ToArray();

                    GetToken(EKind.RB);

                    init = null;
                    if (CurTkn.Kind == EKind.LC) {

                        GetToken(EKind.LC);

                        if(CurTkn.Kind != EKind.EOT) {

                            init = ExpressionList();
                            GetToken(EKind.RC);
                        }
                    }

                    return new TNewApply(EKind.NewArray, new_tkn, cls, args, init);
                }
                else if(CurTkn.Kind == EKind.LC) {

                    GetToken(EKind.LC);

                    init = ExpressionList();

                    GetToken(EKind.RC);

                    return new TNewApply(EKind.NewInstance, new_tkn, cls, new TTerm[0], init);
                }
                else {
                    throw new TParseException();
                }

            case EKind.ClassName:
                id = CurTkn;
                cls = ReadType(null, false);

                //!!!!!!!!!! idとclsは違う !!!!!!!!!!
                return new TReference(cls);

            case EKind.from_:
                return FromExpression();

            case EKind.await_:
                opr = GetToken(EKind.await_);
                term = Expression();
                return new TApply(opr, term);

            case EKind.typeof_:
                opr = GetToken(EKind.typeof_);
                GetToken(EKind.LP);
                cls = ReadType(null, false);
                GetToken(EKind.RP);
                return new TApply(opr, new TReference(cls));
            }

            throw new TParseException();
        }

        public TTerm DotIndexExpression() {
            TTerm t1 = PrimaryExpression();

            while(CurTkn.Kind == EKind.Dot || CurTkn.Kind == EKind.LB) {
                if(CurTkn.Kind == EKind.Dot) {

                    GetToken(EKind.Dot);

                    TToken id = GetToken2(EKind.Identifier, EKind.ClassName);

                    if (CurTkn.Kind == EKind.LP) {
                        GetToken(EKind.LP);

                        TTerm[] args = ExpressionList().ToArray();

                        if(LambdaFunction == null) {

                            GetToken(EKind.RP);
                        }

                        t1 = new TDotApply(t1, id, args);
                    }
                    else {

                        t1 = new TDotReference(t1, id);
                    }
                }
                else {

                    TToken lb = GetToken(EKind.LB);

                    TTerm[] args = ExpressionList().ToArray();

                    GetToken(EKind.RB);

                    t1 = new TApply(lb, t1, args);
                }
            }

            return t1;
        }

        public TTerm PostIncDecExpression() {
            TTerm t1 = DotIndexExpression();

            if (CurTkn.Kind == EKind.Inc || CurTkn.Kind == EKind.Dec) {
                TToken opr = GetToken(EKind.Undefined);

                return new TApply(opr, t1);
            }
            else {

                return t1;
            }
        }

        public TTerm UnaryExpression() {
            if (CurTkn.Kind == EKind.Add || CurTkn.Kind == EKind.Sub) {
                TToken opr = GetToken(EKind.Undefined);

                TTerm t1 = PostIncDecExpression();

                return new TApply(opr, t1);
            }
            else {

                return PostIncDecExpression();
            }
        }

        public TTerm MultiplicativeExpression() {
            TTerm t1 = UnaryExpression();

            while (true) {
                switch (CurTkn.Kind) {
                case EKind.Mul:
                case EKind.Div:
                case EKind.Mod:

                    TToken opr = GetToken(EKind.Undefined);
                    TTerm t2 = UnaryExpression();

                    t1 = new TApply(opr, t1, t2);
                    break;

                default:
                    return t1;
                }
            }
        }

        public TTerm AdditiveExpression() {
            TTerm t1 = MultiplicativeExpression();

            while (true) {
                switch (CurTkn.Kind) {
                case EKind.Add:
                case EKind.Sub:

                    TToken opr = GetToken(EKind.Undefined);
                    TTerm t2 = MultiplicativeExpression();

                    t1 = new TApply(opr, t1, t2);
                    break;

                default:
                    return t1;
                }
            }
        }

        public TTerm RelationalExpression() {
            TTerm t1 = AdditiveExpression();

            while (true) {
                switch (CurTkn.Kind) {
                case EKind.Eq:
                case EKind.NE:
                case EKind.LT:
                case EKind.LE:
                case EKind.GT:
                case EKind.GE:
                case EKind.is_:

                    TToken opr = GetToken(EKind.Undefined);
                    TTerm t2 = AdditiveExpression();

                    t1 = new TApply(opr, t1, t2);
                    break;

                case EKind.as_:
                    GetToken(EKind.as_);
                    t1.CastType = ReadType(null, false);
                    break;

                default:
                    return t1;
                }
            }
        }

        public TTerm NotExpression() {
            if(CurTkn.Kind == EKind.Not_) {

                TToken not_tkn = GetToken(EKind.Not_);
                TTerm t1 = RelationalExpression();

                return new TApply(not_tkn, t1);
            }

            return RelationalExpression();
        }

        public TTerm BitExpression() {
            TTerm t1 = NotExpression();

            while (true) {
                switch (CurTkn.Kind) {
                case EKind.Hat:
                case EKind.Anp:
                case EKind.BitOR:

                    TToken opr = GetToken(EKind.Undefined);
                    TTerm t2 = NotExpression();

                    t1 = new TApply(opr, t1, t2);
                    break;

                default:
                    return t1;
                }
            }
        }

        public TTerm AndExpression() {
            TTerm t1 = BitExpression();

            while (true) {
                switch (CurTkn.Kind) {
                case EKind.And_:

                    TToken opr = GetToken(EKind.Undefined);
                    TTerm t2 = BitExpression();

                    t1 = new TApply(opr, t1, t2);
                    break;

                default:
                    return t1;
                }
            }
        }

        public TTerm OrExpression() {
            TTerm t1 = AndExpression();

            while (true) {
                switch (CurTkn.Kind) {
                case EKind.Or_:

                    TToken opr = GetToken(EKind.Undefined);
                    TTerm t2 = AndExpression();

                    t1 = new TApply(opr, t1, t2);
                    break;

                default:
                    return t1;
                }
            }
        }

        public TTerm ConditionalExpression() {
            TTerm t1 = OrExpression();

            while (true) {
                switch (CurTkn.Kind) {
                case EKind.Question:
                    TToken opr = GetToken(EKind.Question);
                    TTerm t2 = OrExpression();

                    GetToken(EKind.Colon);

                    TTerm t3 = OrExpression();

                    t1 = new TApply(opr, t1, t2, t3);
                    break;

                default:
                    return t1;
                }
            }
        }

        public List<TTerm> ExpressionList() {
            List<TTerm> expr_list = new List<TTerm>();

            if (CurTkn.Kind == EKind.RP || CurTkn.Kind == EKind.RB || CurTkn.Kind == EKind.RC) {
                return expr_list;
            }

            while (true) {
                bool is_out = false;
                bool is_ref = false;

                switch (CurTkn.Kind) {
                case EKind.out_:
                    GetToken(EKind.out_);
                    is_out = true;
                    break;

                case EKind.ref_:
                    GetToken(EKind.ref_);
                    is_ref = true;
                    break;
                }
                TTerm t1 = Expression();

                if(is_out || is_ref) {
                    TReference ref1 = t1 as TReference;

                    ref1.IsOut = is_out;
                    ref1.IsRef = is_ref;
                }

                expr_list.Add(t1);

                if(CurTkn.Kind == EKind.Comma) {

                    GetToken(EKind.Comma);
                }
                else {
                    
                    return expr_list;
                }
            }
        }

        public TTerm Expression() {
            return ConditionalExpression();
        }

        public void ClassText(TType cls, StringWriter sw) {
            sw.Write(cls.GetClassText());
        }
    }

    public class TToken {
        public ETokenType TokenType;
        public EKind Kind;
        public string TextTkn;
        public int StartPos;
        public int EndPos;
        public Exception ErrorTkn;

        public int TabTkn;
        public object ObjTkn;

        public TToken() {
        }

        public TToken(object obj) {
            ObjTkn = obj;
        }

        public TToken(EKind kind) {
            Kind = kind;
        }

        public TToken(EKind kind, object obj) {
            Kind = kind;
            ObjTkn = obj;
        }

        public TToken(string txt, object obj) {
            TextTkn = txt;
            ObjTkn = obj;
        }

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
        public string TextLine;
        public TToken[] Tokens;
        public object ObjLine;
        public TType ClassLine;
    }

    public class TParseException : Exception {
        public TParseException() {
        }

        public TParseException(string msg) {
            Debug.WriteLine(msg);
        }
    }

    public class TResolveNameException : Exception {

        public TResolveNameException() {
        }

        public TResolveNameException(TToken tkn) {
            tkn.ErrorTkn = this;
        }

        public TResolveNameException(TReference ref1) {
            if(ref1.TokenTrm != null) {
                ref1.TokenTrm.ErrorTkn = this;
            }
        }
    }

    public class TBuildCancel : Exception {
    }
}
