using System.Collections.Generic;
using System.Diagnostics;

namespace Miyu {
    public enum EKind {
        Undefined,

        Identifier,
        ClassName,
        NumberLiteral,
        StringLiteral,
        CharLiteral,

        NewInstance,
        NewArray,

        Index,

        FunctionApply,

        LineComment,    // 行コメント      ( // )
        BlockComment,   // ブロックコメント ( /* */ )
        BlockCommentContinued,   // */で閉じてないブロックコメント

        NL,
        Tab,
        Space,
        EOL,

        LP,
        RP,
        LB,
        RB,
        LC,
        RC,
        Dot,
        Inc,
        Dec,
        Add,
        Sub,
        Mul,
        Div,
        Mod,

        Assign,
        AddEq,
        SubEq,
        MulEq,
        DivEq,
        ModEq,
        Mod_,
        Anp,
        Comma,
        Colon,
        SemiColon,
        Question,
        Hat,
        BitOR,
        Tilde,
        Eq,
        NE,
        LT,
        GT,
        LE,
        GE,
        Or_,
        And_,
        Not_,
        Lambda,

        abstract_,
        as_,
        base_,
        //bool_,
        break_,
        //byte_,
        case_,
        catch_,
        //char_,
        checked_,
        class_,
        const_,
        constructor_,
        continue_,
        //decimal_,
        default_,
        delegate_,
        do_,
        //double_,
        else_,
        enum_,
        event_,
        explicit_,
        extern_,
        false_,
        finally_,
        fixed_,
        //float_,
        for_,
        foreach_,
        function_,
        goto_,
        if_,
        implicit_,
        in_,
        //int_,
        interface_,
        internal_,
        is_,
        lock_,
        long_,
        namespace_,
        new_,
        null_,
        operator_,
        out_,
        override_,
        params_,
        private_,
        protected_,
        public_,
        readonly_,
        ref_,
        return_,
        //sbyte_,
        sealed_,
        //short_,
        sizeof_,
        stackalloc_,
        static_,
        //string_,
        struct_,
        switch_,
        this_,
        throw_,
        true_,
        try_,
        typeof_,
        //uint_,
        //ulong_,
        unchecked_,
        unsafe_,
        //ushort_,
        using_,
        virtual_,
        //void_,
        volatile_,
        while_,

        add_,
        alias_,
        ascending_,
        async_,
        await_,
        descending_,
        dynamic_,
        from_,
        get_,
        global_,
        group_,
        into_,
        join_,
        let_,
        orderby_,
        partial_,
        remove_,
        select_,
        set_,
        //value_,
        var_,
        where_,
        yield_,

        EOT,
    }

    partial class TParser {

        /*
            字句解析の初期処理をします。
        */
        public void InitializeLexicalAnalysis() {
            // C#のキーワードのリスト
            // https://msdn.microsoft.com/en-us/library/x53a06bb.aspx
            string[] keyword_list = new string[] {
                "abstract",
                "as",
                "base",
                //"bool",
                "break",
                //"byte",
                "case",
                "catch",
                //"char",
                "checked",
                "class",
                "const",
                "constructor",
                "continue",
                //"decimal",
                "default",
                "delegate",
                "do",
                //"double",
                "else",
                "enum",
                "event",
                "explicit",
                "extern",
                "false",
                "finally",
                "fixed",
                //"float",
                "for",
                "foreach",
                "function",
                "goto",
                "if",
                "implicit",
                "in",
                //"int",
                "interface",
                "internal",
                "is",
                "lock",
                "long",
                "namespace",
                "new",
                "null",
                "operator",
                "out",
                "override",
                "params",
                "private",
                "protected",
                "public",
                "readonly",
                "ref",
                "return",
                //"sbyte",
                "sealed",
                //"short",
                "sizeof",
                "stackalloc",
                "static",
                //"string",
                "struct",
                "switch",
                "this",
                "throw",
                "true",
                "try",
                "typeof",
                //"uint",
                //"ulong",
                "unchecked",
                "unsafe",
                //"ushort",
                "using",
                "virtual",
                //"void",
                "volatile",
                "while",

                "add",
                "alias",
                "ascending",
                "async",
                "await",
                "descending",
                "dynamic",
                "from",
                "get",
                "global",
                "group",
                "into",
                "join",
                "let",
                "orderby",
                "partial",
                "remove",
                "select",
                "set",
                //"value",
                "var",
                "where",
                "yield",
            };

            EKind[] token_list = new EKind[] {
                EKind.abstract_,
                EKind.as_,
                EKind.base_,
                //EKind.bool_,
                EKind.break_,
                //EKind.byte_,
                EKind.case_,
                EKind.catch_,
                //EKind.char_,
                EKind.checked_,
                EKind.class_,
                EKind.const_,
                EKind.constructor_,
                EKind.continue_,
                //EKind.decimal_,
                EKind.default_,
                EKind.delegate_,
                EKind.do_,
                //EKind.double_,
                EKind.else_,
                EKind.enum_,
                EKind.event_,
                EKind.explicit_,
                EKind.extern_,
                EKind.false_,
                EKind.finally_,
                EKind.fixed_,
                //EKind.float_,
                EKind.for_,
                EKind.foreach_,
                EKind.function_,
                EKind.goto_,
                EKind.if_,
                EKind.implicit_,
                EKind.in_,
                //EKind.int_,
                EKind.interface_,
                EKind.internal_,
                EKind.is_,
                EKind.lock_,
                EKind.long_,
                EKind.namespace_,
                EKind.new_,
                EKind.null_,
                EKind.operator_,
                EKind.out_,
                EKind.override_,
                EKind.params_,
                EKind.private_,
                EKind.protected_,
                EKind.public_,
                EKind.readonly_,
                EKind.ref_,
                EKind.return_,
                //EKind.sbyte_,
                EKind.sealed_,
                //EKind.short_,
                EKind.sizeof_,
                EKind.stackalloc_,
                EKind.static_,
                //EKind.string_,
                EKind.struct_,
                EKind.switch_,
                EKind.this_,
                EKind.throw_,
                EKind.true_,
                EKind.try_,
                EKind.typeof_,
                //EKind.uint_,
                //EKind.ulong_,
                EKind.unchecked_,
                EKind.unsafe_,
                //EKind.ushort_,
                EKind.using_,
                EKind.virtual_,
                //EKind.void_,
                EKind.volatile_,
                EKind.while_,

                EKind.add_,
                EKind.alias_,
                EKind.ascending_,
                EKind.async_,
                EKind.await_,
                EKind.descending_,
                EKind.dynamic_,
                EKind.from_,
                EKind.get_,
                EKind.global_,
                EKind.group_,
                EKind.into_,
                EKind.join_,
                EKind.let_,
                EKind.orderby_,
                EKind.partial_,
                EKind.remove_,
                EKind.select_,
                EKind.set_,
                //EKind.value_,
                EKind.var_,
                EKind.where_,
                EKind.yield_,
            };

            Debug.Assert(keyword_list.Length == token_list.Length);

            KeywordMap = new Dictionary<string, EKind>();

            // キーワードの文字列を辞書に登録します。
            for(int i = 0; i < keyword_list.Length; i++) {
                KeywordMap.Add(keyword_list[i], token_list[i]);

                KindString.Add(token_list[i], keyword_list[i]);
            }

            AddSymbol("=", EKind.Assign);
            AddSymbol("+=", EKind.AddEq);
            AddSymbol("-=", EKind.SubEq);
            AddSymbol("*=", EKind.MulEq);
            AddSymbol("/=", EKind.DivEq);
            AddSymbol("%=", EKind.ModEq);

            AddSymbol("+", EKind.Add);
            AddSymbol("-", EKind.Sub);
            AddSymbol("%", EKind.Mod_);
            AddSymbol("&", EKind.Anp);
            AddSymbol("(", EKind.LP);
            AddSymbol(")", EKind.RP);
            AddSymbol("*", EKind.Mul);
            AddSymbol(",", EKind.Comma);
            AddSymbol(".", EKind.Dot);
            AddSymbol("/", EKind.Div);
            AddSymbol(":", EKind.Colon);
            AddSymbol(";", EKind.SemiColon);
            AddSymbol("?", EKind.Question);
            AddSymbol("[", EKind.LB);
            AddSymbol("]", EKind.RB);
            AddSymbol("^", EKind.Hat);
            AddSymbol("{", EKind.LC);
            AddSymbol("|", EKind.BitOR);
            AddSymbol("}", EKind.RC);
            AddSymbol("~", EKind.Tilde);

            AddSymbol("++", EKind.Inc);
            AddSymbol("--", EKind.Dec);

            AddSymbol("==", EKind.Eq);
            AddSymbol("!=", EKind.NE);
            AddSymbol("<", EKind.LT);
            AddSymbol(">", EKind.GT);
            AddSymbol("<=", EKind.LE);
            AddSymbol(">=", EKind.GE);

            AddSymbol("||", EKind.Or_);
            AddSymbol("&&", EKind.And_);
            AddSymbol("!", EKind.Not_);

            AddSymbol("=>", EKind.Lambda);
        }

        void AddSymbol(string s, EKind kind) {
            switch (s.Length) {
            case 1:
                SymbolTable1[s[0]] = kind;
                break;

            case 2:
                SymbolTable2[s[0], s[1]] = kind;
                break;

            default:
                Debug.Assert(false);
                break;
            }

            KindString.Add(kind, s);
        }
    }
}