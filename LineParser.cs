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

    public partial class TParser {
        public const int TabSize = 4;
        public const string HTMLHead1 = "<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Transitional//EN\" \"http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd\">\r\n<html xmlns=\"http://www.w3.org/1999/xhtml\" >\r\n<head>\r\n<meta charset=\"utf-8\" />\r\n<title>";
        public const string HTMLHead2 = "</title>\r\n<style type=\"text/css\">\r\n.reserved {\r\n\tcolor: blue;\r\n}\r\n.class {\r\n\tcolor: Teal;\r\n}\r\n.string {\r\n\tcolor: red;\r\n}\r\n.comment {\r\n\tcolor: #008000;\r\n}\r\n</style>\r\n</head>\r\n<body>";
        public static TToken EOTToken = new TToken(ETokenType.EOT, EKind.EOT,"", 0, 0);

        [ThreadStatic]
        public static TType LookaheadClass;

        [ThreadStatic]
        public static TType CurrentClass;

        [ThreadStatic]
        public static TLine CurrentLine;

        public TToken[] TokenList;
        public int TokenPos;
        public TToken CurrentToken;
        public TToken NextToken;
        public bool Running;
        public ELanguage LanguageSP;

        // キーワードの文字列の辞書
        public Dictionary<string, EKind> KeywordMap;

        // 2文字の記号の表
        public EKind[,] SymbolTable2 = new EKind[256, 256];

        // 1文字の記号の表
        public EKind[] SymbolTable1 = new EKind[256];

        // トークンの種類の文字列の辞書
        public Dictionary<EKind, string> KindString = new Dictionary<EKind, string>();


        public TParser(TProject prj) {
            // 字句解析の初期処理をする。
            InitializeLexicalAnalysis();
        }

        /*
         * タブ数から空白の文字列を得る。
         */
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

        /*
         * enumの開始行を読む。
         */
        public TType ReadEnumLine(TSourceFile src, TModifier mod1) {
            GetToken(EKind.enum_);
            TToken id = GetToken2(EKind.Identifier, EKind.ClassName);

            TType cls = TGlb.Project.GetClassByName(id.TextTkn);
            cls.ModifierCls = mod1;
            cls.KindClass = EType.Enum;
            cls.SourceFileCls = src;

            LCopt();
            GetToken(EKind.EOT);

            return cls;
        }

        /*
         * classの開始行を読む。
         */
        public TType ReadClassLine(TSourceFile src, TModifier mod1) {
            EKind kind = CurrentToken.Kind;

            GetToken(EKind.Undefined);
            TToken id = GetToken2(EKind.Identifier, EKind.ClassName);

            TType cls;

            if (CurrentToken.Kind == EKind.LT) {
                // 総称型の場合

                List<TType> param_classes = new List<TType>();

                GetToken(EKind.LT);
                while (true) {
                    TToken param_name = GetToken(EKind.Identifier);

                    TType param_class = new TType(param_name.TextTkn);
                    param_class.GenericType = EClass.ParameterClass;

                    param_classes.Add(param_class);

                    if (CurrentToken.Kind != EKind.Comma) {

                        break;
                    }

                    GetToken(EKind.Comma);
                }
                GetToken(EKind.GT);

                // パラメータ化クラスを得る。無ければ新たに作る。
                cls = TGlb.Project.GetParameterizedClass(id.TextTkn, param_classes);
            }
            else {
                // 総称型でない場合

                cls = TGlb.Project.GetClassByName(id.TextTkn);
            }

            switch (kind) {
            case EKind.class_:
                cls.KindClass = EType.Class;
                break;

            case EKind.struct_:
                cls.KindClass = EType.Struct;
                break;

            case EKind.interface_:
                cls.KindClass = EType.Interface;
                break;

            default:
                Debug.Assert(false);
                break;
            }

            if (cls.ModifierCls == null || mod1.isPublic) {

                cls.ModifierCls = mod1;
                cls.SourceFileCls = src;
            }

            Debug.Assert(CurrentClass == null);
            CurrentClass = cls;
            if (CurrentToken.Kind == EKind.Colon) {
                // 親クラスがある場合

                GetToken(EKind.Colon);
                while (true) {

                    TType super_class = ReadType(false);

                    cls.SuperClasses.Add(super_class);

                    if(CurrentToken.Kind == EKind.Comma) {

                        GetToken(EKind.Comma);
                    }
                    else {

                        break;
                    }
                }
            }

            LCopt();
            GetToken(EKind.EOT);
            CurrentClass = null;

            if (cls.ClassName == "int") {
                TGlb.Project.IntClass = cls;
            }
            else if (cls.ClassName == "float") {
                TGlb.Project.FloatClass = cls;
            }
            else if (cls.ClassName == "double") {
                TGlb.Project.DoubleClass = cls;
            }
            else if (cls.ClassName == "char") {
                TGlb.Project.CharClass = cls;
            }
            else if (cls.ClassName == "object") {
                TGlb.Project.ObjectClass = cls;
            }
            else if (cls.ClassName == "string") {
                TGlb.Project.StringClass = cls;
            }
            else if (cls.ClassName == "bool") {
                TGlb.Project.BoolClass = cls;
            }
            else if (cls.ClassName == "void") {
                TGlb.Project.VoidClass = cls;
            }
            else if (cls.ClassName == "Type") {
                TGlb.Project.TypeClass = cls;
            }
            else if (cls.ClassName == "Action") {
                TGlb.Project.ActionClass = cls;
            }
            else if (cls.ClassName == "Enumerable") {
                TGlb.Project.EnumerableClass = cls as TGenericClass;
            }
            else if (cls.ClassName == "Array") {
                TGlb.Project.ArrayClass = cls as TGenericClass;
            }

            return cls;
        }

        /*
         * デリゲートの行を読む。
         */
        public TType ReadDelegateLine(TModifier mod1) {
            Debug.Assert(CurrentClass == null);

            GetToken(EKind.delegate_);

            // 戻り値の型を読む。
            TType ret_type = ReadType(false);

            // デリケートの名前を読む。
            TToken id = GetToken2(EKind.Identifier, EKind.ClassName);

            List<TType> param_classes = new List<TType>();

            if (CurrentToken.Kind == EKind.LT) {
                // 総称型の場合

                GetToken(EKind.LT);
                while (true) {
                    TToken param_name = GetToken(EKind.Identifier);

                    TType param_class = new TType(param_name.TextTkn);
                    param_class.GenericType = EClass.ParameterClass;

                    param_classes.Add(param_class);

                    if (CurrentToken.Kind != EKind.Comma) {

                        break;
                    }

                    GetToken(EKind.Comma);
                }
                GetToken(EKind.GT);
            }

            TType dlg;

            if (param_classes.Count == 0) {

                dlg = TGlb.Project.GetClassByName(id.TextTkn);
            }
            else {

                // パラメータ化クラスを得る。無ければ新たに作る。
                dlg = TGlb.Project.GetParameterizedClass(id.TextTkn, param_classes);
            }
            dlg.KindClass = EType.Delegate_;

            // 引数のリストを読む。
            CurrentClass = dlg;
            List<TVariable> vars = ReadArgs();
            CurrentClass = null;

            // 戻り値の型
            dlg.RetType = ret_type;

            // 引数の型
            dlg.ArgTypes = (from t in vars select t.TypeVar).ToArray();

            return dlg;
        }

        /*
         * フィールドの行を読む。
         */
        public TField ReadFieldLine(TModifier mod1, TType type_prepend) {
            TToken id = GetToken(EKind.Identifier);

            TType tp;
            
            if(type_prepend != null) {

                tp = type_prepend;
            }
            else {

                GetToken(EKind.Colon);
                tp = ReadType(false);
            }

            TTerm init = null;
            if (CurrentToken.Kind == EKind.Assign) {
                // 初期値がある場合

                GetToken(EKind.Assign);

                init = Expression();
            }

            LineEnd();

            return new TField(CurrentClass, mod1, id, tp, init);
        }

        /*
         * enumのフィールドの行を読む。
         */
        public TField ReadEnumFieldLine() {
            TToken id = GetToken(EKind.Identifier);

            OptGetToken(EKind.Comma);
            GetToken(EKind.EOT);

            return new TField(CurrentClass, null, id, CurrentClass, null);
        }

        /*
         * 型を読む。
         */
        public TType ReadType(bool new_class) {
            TToken id = GetToken2(EKind.Identifier, EKind.ClassName);
            TType cls1 = TGlb.Project.GetClassByName(id.TextTkn);

            List<TType> param_classes = null;
            bool contains_argument_class = false;

            if (CurrentToken.Kind == EKind.LT) {
                // 総称型の場合

                if (! (cls1 is TGenericClass)) {

                    throw new TParseException(CurrentToken, cls1.ClassName + ":総称型以外に引数型があります。");
                }
                TGenericClass org_cla = cls1 as TGenericClass;

                param_classes = new List<TType>();

                GetToken(EKind.LT);
                while (true) {
                    TType param_class = ReadType(false);

                    if (param_class.GenericType == EClass.ParameterClass || param_class.GenericType == EClass.UnspecializedClass) {
                        // 仮引数クラスか非特定化クラスを含むクラスの場合

                        contains_argument_class = true;
                    }

                    param_classes.Add(param_class);

                    if(CurrentToken.Kind != EKind.Comma) {

                        break;
                    }

                    GetToken(EKind.Comma);
                }
                TToken gt = GetToken(EKind.GT);

                if(org_cla.ArgClasses.Count != param_classes.Count) {
                    // 仮引数クラスの数が一致しない場合

                    throw new TParseException(gt, "総称型の引数の数が一致しません。");
                }
            }

            int dim_cnt = 0;
            if (CurrentToken.Kind == EKind.LB && ! new_class) {
                // 配列の場合

                GetToken(EKind.LB);

                // 配列の次元を得る。1次元配列のdim_cntは1。
                dim_cnt = 1;
                while (CurrentToken.Kind == EKind.Comma) {
                    GetToken(EKind.Comma);
                    dim_cnt++;
                }

                GetToken(EKind.RB);
            }

            if (contains_argument_class) {
                // 仮引数クラスか非特定化クラスを含む場合

                TGenericClass tmp_class = new TGenericClass(cls1 as TGenericClass, param_classes, dim_cnt);
                tmp_class.GenericType = EClass.UnspecializedClass;

                return tmp_class;
            }

            TType cls2 = null;
            if (param_classes == null) {
                // 引数がない場合

                cls2 = cls1;
            }
            else {
                // 引数がある場合

                cls2 = TGlb.Project.GetSpecializedClass(cls1 as TGenericClass, param_classes, 0);
            }

            if (dim_cnt == 0) {
                // 配列でない場合

                return cls2;
            }
            else {
                // 配列の場合

                List<TType> array_element_class_list = new List<TType> { cls2 };

                if (cls2.GenericType == EClass.ParameterClass || cls2.GenericType == EClass.UnspecializedClass) {
                    // 仮引数クラスか非特定化クラスの場合

                    TGenericClass tmp_class = new TGenericClass(TGlb.Project.ArrayClass, array_element_class_list, dim_cnt);
                    tmp_class.GenericType = EClass.UnspecializedClass;

                    return tmp_class;
                }

                return TGlb.Project.GetSpecializedClass(TGlb.Project.ArrayClass, array_element_class_list, dim_cnt);
            }
        }

        public virtual void LineEnd() {
            GetToken(EKind.EOT);
        }

        /*
         * usingの行を読む。
         */
        public TUsing ReadUsing() {
            TUsing using1 = new TUsing();

            GetToken(EKind.using_);

            while (true) {
                TToken id = GetToken2(EKind.Identifier, EKind.ClassName);

                using1.Packages.Add(id.TextTkn);
                if(CurrentToken.Kind != EKind.Dot) {
                    break;
                }
                GetToken(EKind.Dot);
            }

            LineEnd();

            return using1;
        }

        /*
         * namespaceの開始行を読む。
         */
        public TNamespace ReadNamespace() {
            GetToken(EKind.namespace_);

            TToken id = GetToken(EKind.Identifier);
            GetToken(EKind.LC);
            GetToken(EKind.EOT);

            return new TNamespace(id.TextTkn);
        }

        /*
         * 引数のリストを読む。
         */
        public List<TVariable> ReadArgs() {
            List<TVariable> vars = new List<TVariable>();

            GetToken(EKind.LP);

            while (CurrentToken.Kind != EKind.RP) {

                // 引数の変数を読む。
                TVariable var1 = ReadArgVariable();
                vars.Add(var1);

                if (CurrentToken.Kind != EKind.Comma) {

                    break;
                }

                GetToken(EKind.Comma);
            }

            GetToken(EKind.RP);

            return vars;
        }

        /*
         * 関数定義の開始行を読む。
         */
        public TFunction ReadFunctionLine(TToken constructor_token, TType constructor_class, TModifier mod1, TType ret_type_prepend) {
            TToken fnc_name;
            EKind kind_fnc = EKind.function_;
            
            if(constructor_class != null) {
                fnc_name = constructor_token;
                kind_fnc = EKind.constructor_;
            }
            else {

                if (CurrentToken.Kind == EKind.operator_) {

                    GetToken(EKind.operator_);
                    fnc_name = GetToken(EKind.Undefined);
                    if (fnc_name.TokenType != ETokenType.Symbol) {
                        throw new TParseException(fnc_name);
                    }
                }
                else {

                    fnc_name = GetToken(EKind.Identifier);
                }
            }

            // 引数のリストを読む。
            List<TVariable> vars = ReadArgs();

            TType ret_type = ret_type_prepend;
            TApply base_app = null;

            if(CurrentToken.Kind == EKind.Colon) {

                GetToken(EKind.Colon);

                if(ret_type_prepend == null) {

                    ret_type = ReadType(false);
                }
                else if(constructor_class != null) {

                    if(CurrentToken.Kind == EKind.base_) {

                        base_app = PrimaryExpression() as TApply;
                    }
                    else {
                        throw new TParseException(CurrentToken);
                    }
                }
            }

            if(CurrentToken.Kind == EKind.SemiColon) {

                GetToken(EKind.SemiColon);
            }
            else {

                LCopt();
            }

            GetToken(EKind.EOT);

            return new TFunction(mod1, fnc_name, vars.ToArray(), ret_type, base_app, kind_fnc);
        }

        /*
         * 引数の変数を読む。
         */
        public virtual TVariable ReadArgVariable() {
            EKind kind = EKind.Undefined;

            switch (CurrentToken.Kind) {
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
            if (CurrentToken.Kind == EKind.Colon) {

                GetToken(EKind.Colon);

                type = ReadType(false);
            }

            return new TVariable(id, type, kind);
        }

        /*
         * 変数を読む。
         */
        public TVariable ReadVariable(TType type_prepend) {
            TToken id = GetToken(EKind.Identifier);

            TType type = null;
            if (type_prepend != null) {

                type = type_prepend;
            }
            else { 

                if (CurrentToken.Kind == EKind.Colon) {

                    GetToken(EKind.Colon);

                    type = ReadType(false);
                }
            }

            TTerm init = null;
            if (CurrentToken.Kind == EKind.Assign) {
                // 初期値がある場合

                GetToken(EKind.Assign);

                // 初期値の式
                init = Expression();
            }

            return new TVariable(null, id, type, init);
        }

        /*
         * 変数宣言の行を読む。
         */
        public virtual TVariableDeclaration ReadVariableDeclarationLine(TType type_prepend, bool in_for) {
            TVariableDeclaration var_decl = new TVariableDeclaration();

            if(type_prepend == null) {

                GetToken(EKind.var_);
                var_decl.IsVar = true;
            }

            while (true) {
                // 変数を読む。
                TVariable var1 = ReadVariable(type_prepend);

                var_decl.Variables.Add(var1);

                if(CurrentToken.Kind != EKind.Comma) {

                    break;
                }

                GetToken(EKind.Comma);
            }

            if(!in_for && TokenList[TokenList.Length - 1].Kind != EKind.LC) {

                LineEnd();
            }

            return var_decl;
        }

        /*
         * ifの開始行を読む。
         */
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

        /*
         * else/else ifの開始行を読む。
         */
        public TIfBlock ReadElseLine() {
            TIfBlock if_block = new TIfBlock();
            if_block.IsElse = true;

            GetToken(EKind.else_);

            if(CurrentToken.Kind == EKind.if_) {

                GetToken(EKind.if_);
                LPopt();
                if_block.ConditionIf = Expression();
                RPopt();
            }
            LCopt();
            GetToken(EKind.EOT);

            return if_block;
        }

        /*
         * switchの開始行を読む。
         */
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

        /*
         * caseの行を読む。
         */
        public TCase ReadCaseLine() {
            TCase case1 = new TCase();

            TToken tkn = GetToken(EKind.case_);

            List<TTerm> expr_list = ExpressionList();
            case1.TermsCase.AddRange(expr_list);

            Colonopt();
            if(CurrentToken.Kind == EKind.LC) {
                GetToken(EKind.LC);
            }
            GetToken(EKind.EOT);
            return case1;
        }

        /*
         * whileの開始行を読む。
         */
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

        /*
         * lockの開始行を読む。
         */
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

        /*
         * usingの開始行を読む。
         */
        public TUsingBlock ReadUsingLine() {
            TUsingBlock using1 = new TUsingBlock();

            GetToken(EKind.using_);

            LPopt();

            if(CurrentToken.Kind == EKind.ClassName) {

                TType type = ReadType(false);
                TToken id = GetToken(EKind.Identifier);

                using1.UsingVar = new TVariable(null, id, type, null);

                GetToken(EKind.Assign);
            }
            using1.UsingObj = Expression();
            RPopt();

            LCopt();
            GetToken(EKind.EOT);

            return using1;
        }

        /*
         * foreachの開始行を読む。
         */
        public TForEach ReadForEachLine() {
            TForEach for1 = new TForEach();

            GetToken(EKind.foreach_);
            LPopt();

            if(CurrentToken.Kind == EKind.ClassName) {

                for1.LoopVariable = ReadArgVariable();
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

        /*
         * forの開始行を読む。
         */
        public TFor ReadForLine() {
            TFor for1 = new TFor();

            GetToken(EKind.for_);
            GetToken(EKind.LP);

            if (CurrentToken.Kind != EKind.SemiColon) {

                if (CurrentToken.Kind == EKind.ClassName) {
                    TType tp = ReadType(false);

                    for1.InitStatement = ReadVariableDeclarationLine(tp, true);
                    for1.LoopVariable = (for1.InitStatement as TVariableDeclaration).Variables[0];
                }
                else {

                    // 代入文か関数呼び出し文を読む。
                    for1.InitStatement = ReadAssignmentCallLine(true) as TStatement;
                }
            }

            GetToken(EKind.SemiColon);

            if (CurrentToken.Kind != EKind.SemiColon) {

                for1.ConditionFor = Expression();
            }

            GetToken(EKind.SemiColon);

            if(CurrentToken.Kind != EKind.RP) {

                // 代入文か関数呼び出し文を読む。
                for1.PostStatement = ReadAssignmentCallLine(true) as TStatement;
            }

            GetToken(EKind.RP);
            GetToken2(EKind.LC, EKind.SemiColon);
            GetToken(EKind.EOT);

            return for1;
        }

        /*
         * tryの開始行を読む。
         */
        public TTry ReadTryLine() {
            GetToken(EKind.try_);
            LCopt();
            GetToken(EKind.EOT);
            return new TTry();
        }

        /*
         * catchの開始行を読む。
         */
        public TCatch ReadCatchLine() {
            GetToken(EKind.catch_);
            LPopt();

            TType tp = ReadType(false);

            string name = "";
            if(CurrentToken.Kind == EKind.Identifier) {
                name = GetToken(EKind.Identifier).TextTkn;
            }

            RPopt();
            LCopt();
            GetToken(EKind.EOT);

            return new TCatch(new TVariable(name, tp));
        }

        /*
         * goto,returnなどの制御の移動の行を読む。
         */
        public TJump ReadJumpLine() {
            TToken tkn = GetToken(EKind.Undefined);

            TJump jump = new TJump(tkn.Kind);

            switch (tkn.Kind) {
            case EKind.yield_:
                if(CurrentToken.Kind == EKind.return_) {

                    GetToken(EKind.return_);
                    jump.RetVal = Expression();
                }
                else if (CurrentToken.Kind == EKind.break_) {

                    GetToken(EKind.break_);
                }
                else {
                    throw new TParseException(CurrentToken);
                }
                break;

            case EKind.return_:
            case EKind.throw_:
                if(CurrentToken.Kind != EKind.SemiColon && CurrentToken.Kind != EKind.EOT) {

                    jump.RetVal = Expression();
                }
                break;

            case EKind.break_:
            case EKind.continue_:
                break;

            case EKind.goto_:
                TToken lbl = GetToken(EKind.Identifier);
                jump.LabelJmp = lbl.TextTkn;
                break;
            }

            LineEnd();

            return jump;
        }

        /*
         * 代入文か関数呼び出し文を読む。
         */
        public object ReadAssignmentCallLine(bool in_for) {
            TAssignment asn = null;

            TTerm t1 = Expression();

            switch (CurrentToken.Kind) {
            case EKind.Assign:
            case EKind.AddEq:
            case EKind.SubEq:
            case EKind.DivEq:
            case EKind.ModEq:
                // 代入文の場合

                TToken opr = GetToken(EKind.Undefined);

                // 右辺を読む。
                TTerm t2 = Expression();

                TApply app1 = new TApply(opr, t1, t2);

                asn = new TAssignment(app1);
                break;
            }

            if(!in_for) {

                if(CurrentToken.Kind == EKind.Comma) {
                    GetToken(EKind.Comma);
                    GetToken(EKind.EOT);
                    return t1;
                }

                if(TGlb.LambdaFunction != null) {

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
                throw new TParseException(CurrentToken);
            }

            // 関数呼び出し文を返す。
            return new TCall(t1 as TApply);
        }

        public int LineTopTokenIndex(TToken[] token_list) {
            for(int i = 0; i < token_list.Length; i++) {
                TToken tkn = token_list[i];
                switch (tkn.TokenType) {
                case ETokenType.BlockComment:
                case ETokenType.BlockCommentContinued:
                case ETokenType.LineComment:
                    break;

                default:
                    return i;
                }
            }

            return -1;
        }

        /*
         * 1行の構文解析をする。
         */
        public object ParseLine(TSourceFile src, TFunction parent_fnc, TStatement parent_stmt, int line_top_idx) {
            TokenPos = line_top_idx;

            if (TokenPos < TokenList.Length) {

                CurrentToken = TokenList[TokenPos];
            }
            else {

                CurrentToken = EOTToken;
            }

            if (TokenPos + 1 < TokenList.Length) {

                NextToken = TokenList[TokenPos + 1];
            }
            else {

                NextToken = EOTToken;
            }

            try {
                if(parent_stmt is TVariableDeclaration) {
                    TVariable var1 = (parent_stmt as TVariableDeclaration).Variables[0];

                    if(! (var1.InitValue is TNewApply)) {
                        throw new TParseException(CurrentToken);
                    }

                    TTerm ele = Expression();

                    OptGetToken(EKind.Comma);

                    (var1.InitValue as TNewApply).InitList.Add(ele);

                    return ele;
                }

                TModifier mod1 = new TModifier();
                while (true) {
                    switch (CurrentToken.Kind) {
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
                        if (TGlb.InLambdaFunction) {
                            // ラムダ関数の中の場合

                            if (CurrentLine.Tokens[0].StartPos == TGlb.LambdaFunctionIndent) {
                                // 現在行のインデントがラムダ関数の開始行のインデントと同じ場合

                                Debug.Assert(TGlb.ApplyLambda != null);

                                // ラムダ関数の呼び出しの引数を追加する。
                                List<TTerm> args = new List<TTerm>(TGlb.ApplyLambda.Args);
                                while (CurrentToken.Kind == EKind.Comma) {
                                    // 追加の引数がある場合

                                    GetToken(EKind.Comma);
                                    args.Add(Expression());
                                }
                                TGlb.ApplyLambda.Args = args.ToArray();

                                TGlb.InLambdaFunction = false;
                                TGlb.ApplyLambda = null;

                                GetToken(EKind.RP);
                            }
                        }

                        if(CurrentToken.Kind == EKind.SemiColon) {

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

                switch (CurrentToken.Kind) {
                case EKind.using_:
                    if(NextToken.Kind == EKind.LP) {

                        return ReadUsingLine();
                    }
                    else {

                        return ReadUsing();
                    }

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
                case EKind.continue_:
                case EKind.goto_:
                    return ReadJumpLine();

                case EKind.ClassName:
                    TToken class_token = CurrentToken;
                    TType tp = ReadType(false);

                    if (CurrentToken.Kind == EKind.LP) {

                        return ReadFunctionLine(class_token, tp, mod1, tp);
                    }

                    if (CurrentToken.Kind != EKind.Identifier) {

                        LookaheadClass = tp;

                        // 代入文か関数呼び出し文を読む。
                        return ReadAssignmentCallLine(false);
                    }

                    if (NextToken.Kind == EKind.LP) {

                        return ReadFunctionLine(null, null, mod1, tp);
                    }
                    else {

                        if (parent_fnc == null) {

                            return ReadFieldLine(mod1, tp);
                        }
                        else {

                            return ReadVariableDeclarationLine(tp, false);
                        }
                    }

                case EKind.Identifier:
                case EKind.base_:
                    if(CurrentClass != null && CurrentClass.KindClass == EType.Enum) {

                        return ReadEnumFieldLine();
                    }

                    if (NextToken.Kind == EKind.Colon) {
                        if(TokenPos + 2 < TokenList.Length) {

                            return ReadFieldLine(mod1, null);
                        }
                        else {

                            TToken id = GetToken(EKind.Identifier);
                            GetToken(EKind.Colon);
                            return new TLabelStatement(id);
                        }
                    }
                    else if (parent_fnc == null) {

                        return ReadFunctionLine(null, null, mod1, null);
                    }
                    else {

                        // 代入文か関数呼び出し文を読む。
                        return ReadAssignmentCallLine(false);
                    }

                case EKind.this_:
                case EKind.await_:
                case EKind.LP:
                case EKind.StringLiteral:
                case EKind.typeof_:
                    // 代入文か関数呼び出し文を読む。
                    return ReadAssignmentCallLine(false);

                case EKind.operator_:
                    return ReadFunctionLine(null, null, mod1, null);

                case EKind.LB:
                    GetToken(EKind.LB);
                    TType attr = ReadType(false);
                    GetToken(EKind.RB);
                    return new TAttribute(attr);

                default:
                    Debug.WriteLine("行頭 {0}", CurrentToken.Kind);
                    throw new TParseException(CurrentToken);
                }
            }
            catch (TCodeCompletionException code_completion_exception) {
                return code_completion_exception.CodeCompletion;
            }
            catch (TParseException) {
            }

            return null;
        }

        public void GetVariablesClass(TSourceFile src, int current_line_idx, out List<TVariable> vars) {
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

        /*
         * ソースファイル内の継続行をセットする。
         */
        public void SetLineContinued(TSourceFile src) {
            TLine prev_line = null;
            foreach(TLine line in src.Lines) {
                line.Indent = -1;
                if(line.Tokens.Length != 0) {
                    // 空行でない場合

                    // 行頭の字句
                    TToken line_top_token = line.Tokens[0];

                    switch (line_top_token.TokenType) {
                    case ETokenType.BlockComment:
                    case ETokenType.BlockCommentContinued:
                    case ETokenType.LineComment:
                        break;

                    default:
                        // コメントでない場合

                        line.Indent = line_top_token.StartPos;

                        if(prev_line != null) {
                            if (prev_line.Continued) {

                                if (prev_line.Indent == line.Indent) {

                                    //Debug.WriteLine("継続行 {0}", line.TextLine, "");
                                    line.Continued = true;
                                }
                            }
                            else {

                                TToken prev_line_end_token = prev_line.Tokens.Last();
                                if (prev_line_end_token.Kind != EKind.LC && prev_line_end_token.Kind != EKind.Colon && prev_line.Indent < line.Indent) {

                                    //Debug.WriteLine("継続行 {0}", line.TextLine, "");
                                    line.Continued = true;
                                }
                            }
                        }
                        break;
                    }
                }

                if(line.Indent == -1) {
                    prev_line = null;
                }
                else {
                    prev_line = line;
                }
            }
        }

        /*
         * ソースファイルの構文解析をする。
         */
        public async void ParseFile(TSourceFile src) {
            while (Running) {
                await Task.Delay(1);
            }
            Running = true;
            //Debug.WriteLine("parse file : 開始 {0}", Path.GetFileName(src.PathSrc), "");

            src.ClassesSrc.Clear();

            object prev_obj = null;
            List<TToken> comments = new List<TToken>();
            List<object> obj_stack = new List<object>();

            SetLineContinued(src);

            for(int line_idx = 0; line_idx < src.Lines.Count; line_idx++) {
                //await Task.Delay(1);

                if (TGlb.Project.Modified.WaitOne(0)) {
                    // ソースが変更された場合

                    Debug.WriteLine("parse file : 中断");
                    Running = false;
                    return;
                }

                TLine line = src.Lines[line_idx];
                CurrentLine = line;
                if (line.Continued) {
                    // 継続行の場合

                    continue;
                }

                line.ObjLine = null;
                if (line.Tokens.Length == 0) {
                    comments.Add(new TToken(EKind.NL));
                }
                else {
                    int line_top_idx = LineTopTokenIndex(line.Tokens);

                    if (line_top_idx == -1) {
                        comments.Add(line.Tokens[0]);
                    }
                    else { 

                        TToken line_top_token = line.Tokens[line_top_idx];

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

                            // スタックの中からクラスを探す。
                            var vcls = from x in obj_stack_rev where x is TType select x as TType;
                            if (vcls.Any()) {
                                cls = vcls.First();

                                if (cls is TGenericClass) {
                                    TGenericClass gen = cls as TGenericClass;

                                    var vtkn = from t in gen.ArgClasses from tk in line.Tokens where tk.TextTkn == t.ClassName select tk;
                                    foreach (TToken tk in vtkn) {
                                        tk.Kind = EKind.ClassName;
                                    }
                                }
                            }
                            line.ClassLine = cls;
                            CurrentClass = cls;

                            // スタックの中から関数を探す。
                            var vfnc = from x in obj_stack_rev where x is TFunction select x as TFunction;
                            if (vfnc.Any()) {
                                parent_fnc = vfnc.First();
                            }

                            // スタックの中から最も内側の文を探す。
                            var vstmt = from x in obj_stack_rev where x is TBlockStatement select x as TBlockStatement;
                            if (vstmt.Any()) {
                                parent_stmt = vstmt.First();
                            }

                            if (parent_stmt == null && parent_fnc != null) {
                                parent_stmt = parent_fnc.BlockFnc;
                            }

                            //Debug.Write(string.Format("行解析 {0}", line.TextLine));
                            TGlb.LambdaFunction = null;

                            List<TToken> token_list = new List<TToken>(line.Tokens);
                            for (int cont_line_idx = line_idx + 1; cont_line_idx < src.Lines.Count && src.Lines[cont_line_idx].Continued ; cont_line_idx++) {
                                token_list.AddRange(src.Lines[cont_line_idx].Tokens);
                            }

                            TokenList = token_list.ToArray();
                            object obj = ParseLine(src, parent_fnc, parent_stmt, line_top_idx);
                            if (obj != null) {

                                while (obj_stack.Count < line.Indent) {
                                    obj_stack.Add(null);
                                }

                                if (TGlb.LambdaFunction != null) {
                                    // ラムダ関数の始まりの場合

                                    if(cls == null) {
                                        // 構文エラーがある場合

                                        TGlb.InLambdaFunction = false;
                                    }
                                    else {
                                        // 構文エラーがない場合

                                        obj_stack.Add(TGlb.LambdaFunction);

                                        TGlb.LambdaFunction.DeclaringType = cls;
                                        cls.Functions.Add(TGlb.LambdaFunction);
                                        src.FunctionsSrc.Add(TGlb.LambdaFunction);
                                    }

                                    TGlb.LambdaFunction = null;
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
                                        if(var1.Attributes == null) {
                                            var1.Attributes = new List<TAttribute>();
                                        }
                                        var1.Attributes.Add(prev_obj as TAttribute);
                                    }

                                    if (cls != null) {

                                        if (obj is TField) {
                                            TField fld = obj as TField;

                                            fld.CommentVar = comments.ToArray();
                                            fld.DeclaringType = cls;
                                            cls.Fields.Add(fld);
                                            src.FieldsSrc.Add(fld);
                                        }
                                        else if (obj is TFunction) {
                                            TFunction fnc = obj as TFunction;

                                            fnc.CommentVar = comments.ToArray();
                                            fnc.DeclaringType = cls;
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

        /*
         * ソースファイルの名前解決をする。
         */
        public void SourceFileResolveName(TSourceFile src) {
            //Debug.WriteLine("名前解決 : {0} -------------------------------------------------------", Path.GetFileName(src.PathSrc), "");
            for (int line_idx = 0; line_idx < src.Lines.Count; line_idx++) {
                //await Task.Delay(1);
                if (TGlb.Project.Modified.WaitOne(0)) {
                    // ソースが変更された場合

                    Debug.WriteLine("名前解決 : 中断");
                    Running = false;
                    return;
                }

                TLine line = src.Lines[line_idx];

                if (line.ObjLine is TStatement) {
                    TStatement stmt = line.ObjLine as TStatement;

                    List<TVariable> vars;
                    GetVariablesClass(src, line_idx, out vars);

                    // 名前解決のエラーをクリアする。
                    var name_err_tkns = from x in line.Tokens where x.ErrorTkn is TResolveNameException select x;
                    foreach (TToken name_err_tkn in name_err_tkns) {
                        name_err_tkn.ErrorTkn = null;
                    }

                    try {
                        stmt.ResolveName(line.ClassLine, vars);
                    }
                    catch (TResolveNameException ex) {
                        Debug.WriteLine("名前解決 {0} : {1}", ex.ErrRef.NameRef, line.TextLine.Trim());
                    }
                }
            }
        }

        /*
         * 指定された種類の字句を読む。
         */
        public TToken GetToken(EKind type) {

            if(type != EKind.Undefined && type != CurrentToken.Kind) {

                throw new TParseException(CurrentToken);
            }

            TToken tkn = CurrentToken;

            while (true) {

                TokenPos++;
                if (TokenPos < TokenList.Length) {

                    CurrentToken = TokenList[TokenPos];

                    switch (CurrentToken.TokenType) {
                    case ETokenType.BlockComment:
                    case ETokenType.BlockCommentContinued:
                    case ETokenType.LineComment:
                        break;

                    default:
                        goto while_end;
                    }
                }
                else {

                    CurrentToken = EOTToken;
                    break;
                }
            }
            while_end:

            if (TokenPos + 1 < TokenList.Length) {

                NextToken = TokenList[TokenPos + 1];
            }
            else {

                NextToken = EOTToken;
            }

            return tkn;
        }

        /*
         * いずれかの2種類の字句を読む。
         */
        public TToken GetToken2(EKind kind1, EKind kind2) {
            if (CurrentToken.Kind != kind1 && CurrentToken.Kind != kind2) {

                throw new TParseException(CurrentToken);
            }
            return GetToken(EKind.Undefined);
        }

        /*
         * 指定された種類なら字句を読む。
         */
        public void OptGetToken(EKind kind) {
            if (CurrentToken.Kind == kind) {

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

            if (CurrentToken.Kind == EKind.where_) {
                GetToken(EKind.where_);
                from1.CndQry = Expression();
            }

            if (CurrentToken.Kind == EKind.select_) {
                GetToken(EKind.select_);
                from1.SelFrom = Expression();
            }

            if (CurrentToken.Kind == EKind.from_) {
                from1.InnerFrom = FromExpression();
            }

            if(from1.SelFrom == null && from1.InnerFrom == null) {
                throw new TParseException(CurrentToken);
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

            switch (CurrentToken.Kind) {
            case EKind.Identifier:
            case EKind.this_:
            case EKind.true_:
            case EKind.false_:
            case EKind.null_:
            case EKind.base_:
                id = GetToken(EKind.Undefined);

                if (CurrentToken.Kind == EKind.LP) {
                    GetToken(EKind.LP);

                    TTerm[] expr_list = ExpressionList().ToArray();

                    GetToken(EKind.RP);

                    return new TApply(id, expr_list);
                }
                else if(CurrentToken.Kind == EKind.Lambda) {
                    // ラムダ関数の場合

                    TToken lambda = GetToken(EKind.Lambda);

                    if (CurrentToken.Kind == EKind.LC) {
                        // 「 x => { 」 の形の場合

                        GetToken(EKind.LC);

                        TGlb.LambdaFunction = new TFunction(id, null);
                        TGlb.InLambdaFunction = true;

                        // ラムダ関数の開始行のインデント
                        TGlb.LambdaFunctionIndent = CurrentLine.Tokens[0].StartPos;

                        return new TReference(TGlb.LambdaFunction);
                    }
                    else {
                        // 「 x => x * x 」 の形の場合

                        TTerm ret = Expression();
                        TFunction fnc = new TFunction(id, ret);

                        return new TReference(fnc);
                    }
                }
                else {

                    return new TReference(id);
                }

            case EKind.LP:
                GetToken(EKind.LP);
                if(CurrentToken.Kind == EKind.ClassName) {

                    cls = ReadType(false);
                    if(CurrentToken.Kind == EKind.RP) {

                        GetToken(EKind.RP);
                        term = Expression();
                        term.CastType = cls;

                        return term;
                    }
                    else {

                        LookaheadClass = cls;
                    }
                }
                else if(CurrentToken.Kind == EKind.RP) {
                    GetToken(EKind.RP);
                    TToken lambda = GetToken(EKind.Lambda);
                    GetToken(EKind.LC);

                    TGlb.LambdaFunction = new TFunction(lambda);
                    TGlb.InLambdaFunction = true;

                    return new TReference(TGlb.LambdaFunction);
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

                cls = ReadType(true);
                if(CurrentToken.Kind == EKind.LP) {

                    GetToken(EKind.LP);

                    args = ExpressionList().ToArray();

                    GetToken(EKind.RP);

                    return new TNewApply(EKind.NewInstance, new_tkn, cls, args, null);
                }
                else if (CurrentToken.Kind == EKind.LB) {

                    GetToken(EKind.LB);

                    args = ExpressionList().ToArray();

                    GetToken(EKind.RB);

                    init = null;
                    if (CurrentToken.Kind == EKind.LC) {

                        GetToken(EKind.LC);

                        if(CurrentToken.Kind != EKind.EOT) {

                            init = ExpressionList();
                            GetToken(EKind.RC);
                        }
                    }

                    return new TNewApply(EKind.NewArray, new_tkn, cls, args, init);
                }
                else if(CurrentToken.Kind == EKind.LC) {

                    GetToken(EKind.LC);

                    init = ExpressionList();

                    GetToken(EKind.RC);

                    return new TNewApply(EKind.NewInstance, new_tkn, cls, new TTerm[0], init);
                }
                else {
                    throw new TParseException(CurrentToken);
                }

            case EKind.ClassName:
                id = CurrentToken;
                cls = ReadType(false);

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
                cls = ReadType(false);
                GetToken(EKind.RP);
                return new TApply(opr, new TReference(cls));
            }

            throw new TParseException(CurrentToken);
        }

        public TTerm DotIndexExpression() {
            TTerm t1 = PrimaryExpression();

            while(CurrentToken.Kind == EKind.Dot || CurrentToken.Kind == EKind.LB) {
                if(CurrentToken.Kind == EKind.Dot) {

                    TToken dot = GetToken(EKind.Dot);

                    if(dot == TProject.ChangedToken && CurrentToken.Kind != EKind.Identifier && CurrentToken.Kind != EKind.ClassName) {
                        // 変更した字句がこのドットで現在の字句が識別子やクラス名でない場合

                        Debug.WriteLine("コード補完開始 !!!!!!");
                        throw new TCodeCompletionException(t1, null);
                    }

                    TToken id = GetToken2(EKind.Identifier, EKind.ClassName);

                    if (id == TProject.ChangedToken) {
                        // 変更した字句が現在の字句(識別子/クラス名)の場合

                        Debug.WriteLine("コード補完 絞り込み !!!!!!");
                        throw new TCodeCompletionException(t1, id);
                    }

                    if (CurrentToken.Kind == EKind.LP) {
                        GetToken(EKind.LP);

                        TTerm[] args = ExpressionList().ToArray();

                        if(TGlb.LambdaFunction == null) {

                            GetToken(EKind.RP);
                        }

                        TDotApply dot_app = new TDotApply(t1, id, args);
                        if(args.Length == 1 && args[0] is TReference) {
                            // 引数が1個の変数参照の場合

                            TReference arg0 = args[0] as TReference;
                            if(arg0.VarRef is TFunction && (arg0.VarRef as TFunction).KindFnc == EKind.Lambda) {
                                // ラムダ関数の変数参照の場合

                                TGlb.ApplyLambda = dot_app;
                            }
                        }

                        t1 = dot_app;
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

            if (CurrentToken.Kind == EKind.Inc || CurrentToken.Kind == EKind.Dec) {
                TToken opr = GetToken(EKind.Undefined);

                return new TApply(opr, t1);
            }
            else {

                return t1;
            }
        }

        public TTerm UnaryExpression() {
            if (CurrentToken.Kind == EKind.Add || CurrentToken.Kind == EKind.Sub) {
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
                switch (CurrentToken.Kind) {
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
                switch (CurrentToken.Kind) {
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
                switch (CurrentToken.Kind) {
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
                    t1.CastType = ReadType(false);
                    break;

                default:
                    return t1;
                }
            }
        }

        public TTerm NotExpression() {
            if(CurrentToken.Kind == EKind.Not_) {

                TToken not_tkn = GetToken(EKind.Not_);
                TTerm t1 = RelationalExpression();

                return new TApply(not_tkn, t1);
            }

            return RelationalExpression();
        }

        public TTerm BitExpression() {
            TTerm t1 = NotExpression();

            while (true) {
                switch (CurrentToken.Kind) {
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
                switch (CurrentToken.Kind) {
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
                switch (CurrentToken.Kind) {
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
                switch (CurrentToken.Kind) {
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

            if (CurrentToken.Kind == EKind.RP || CurrentToken.Kind == EKind.RB || CurrentToken.Kind == EKind.RC) {
                return expr_list;
            }

            while (true) {
                bool is_out = false;
                bool is_ref = false;

                switch (CurrentToken.Kind) {
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

                if(CurrentToken.Kind == EKind.Comma) {

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

        public TToken(TToken token) {
            TokenType   = token.TokenType;
            Kind        = token.Kind;
            TextTkn     = token.TextTkn;
            StartPos    = token.StartPos;
            EndPos      = token.EndPos;
            ErrorTkn    = token.ErrorTkn;
            TabTkn      = token.TabTkn;
            ObjTkn      = token.ObjTkn;
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
        public bool Continued;
        public string TextLine;
        public TToken[] Tokens;
        public object ObjLine;
        public TType ClassLine;

        public TLine() {
        }

        public TLine(TLine line) {
            Indent = line.Indent;
            Continued = line.Continued;
            TextLine = line.TextLine;
            Tokens = (from x in line.Tokens select new TToken(x)).ToArray();
            ObjLine = line.ObjLine;
            ClassLine = line.ClassLine;
        }

        public void Invariant() {
            // newの後でUpdateTokenTypeが呼ばれるから。
            Debug.Assert(Tokens != null);
        }
    }

    public class TBuildException : Exception {
        public TBuildException(string msg) {
            Debug.WriteLine("Build Exception : {0}", msg);
        }
    }

    public class TParseException : Exception {
        void SetErrorTkn(TToken token) {
            if (token == null) {

                TParser.CurrentLine.Tokens[0].ErrorTkn = this;
            }
            else if (token == TParser.EOTToken) {

                TParser.CurrentLine.Tokens[TParser.CurrentLine.Tokens.Length - 1].ErrorTkn = this;
            }
            else {

                token.ErrorTkn = this;
            }
        }

        public TParseException(TToken token) {
            SetErrorTkn(token);

            Debug.WriteLine("Parse Exception : {0}", TParser.CurrentLine.TextLine, "");
        }

        public TParseException(TToken token, string msg) {
            SetErrorTkn(token);

            Debug.WriteLine("Parse Exception : {0}\r\n\t{1}", TParser.CurrentLine.TextLine, msg);
        }
    }

    public class TResolveNameException : Exception {
        public TReference ErrRef;

        public TResolveNameException() {
        }

        public TResolveNameException(TToken tkn) {
            tkn.ErrorTkn = this;
        }

        public TResolveNameException(TReference ref1) {
            ErrRef = ref1;
            if(ref1.TokenTrm != null) {
                ref1.TokenTrm.ErrorTkn = this;
            }
        }
    }

    public class TBuildCancel : Exception {
    }

    /*
     * コード補完の例外
     */
    public class TCodeCompletionException : Exception {
        // コード補完の文
        public TCodeCompletion CodeCompletion;

        public TCodeCompletionException(TTerm dot_left, TToken id) {
            CodeCompletion = new TCodeCompletion(dot_left, id);
        }
    }
}
