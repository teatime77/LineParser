using System.Diagnostics;
using System.Collections.Generic;
using System;

/*--------------------------------------------------------------------------------
        字句解析
--------------------------------------------------------------------------------*/

namespace Miyu {
    /*
        字句型
    */
    public enum ETokenType {
        // 未定義
        Undefined,

        // 空白
        EOT,

        // 文字
        Char_,

        // 文字列
        String_,

        // 逐語的文字列 ( @"文字列" )
        VerbatimString,

        // "で閉じてない逐語的文字列
        VerbatimStringContinued,

        // 識別子
        Identifier,

        // キーワード
        Keyword,        
        Int,
        Float,
        Double,

        // 記号
        Symbol,

        // 行コメント      ( // )
        LineComment,

        // ブロックコメント ( /* */ )
        BlockComment,

        // */で閉じてないブロックコメント
        BlockCommentContinued,

        // エラー
        Error,          
    }

    partial class TParser {
        /*
            16進数文字ならtrue
        */
        public bool IsHexDigit(char ch) {
            return char.IsDigit(ch) || 'a' <= ch && ch <= 'f' || 'A' <= ch && ch <= 'F';
        }

        /*
            エスケープ文字を読み込み、文字位置(pos)を進める。
        */
        public char ReadEscapeChar(string text, ref int pos) {
            if (text.Length <= pos + 1) {
                // エスケープ文字が文字列の末尾の場合

                return '\0';
            }

            // 1文字のエスケープ文字の変換リスト
            string in_str = "\'\"\\0abfnrtv";
            string out_str = "\'\"\\\0\a\b\f\n\r\t\v";

            // 変換リストにあるか調べる。
            int k = in_str.IndexOf(text[pos + 1]);

            if (k != -1) {
                // 変換リストにある場合

                pos += 2;

                // 変換した文字を返す。
                return out_str[k];
            }

            switch (text[pos + 1]) {
            case 'u':
                // \uXXXX

                pos = Math.Min(pos + 6, text.Length);

                // エスケープ文字の計算は未実装。
                return '\0';

            case 'U':
                // \UXXXXXXXX

                pos = Math.Min(pos + 10, text.Length);

                // エスケープ文字の計算は未実装。
                return '\0';

            case 'x':
                // \xX...

                // 16進数字の終わりを探す。
                for (pos++; pos < text.Length && IsHexDigit(text[pos]); pos++) ;

                // エスケープ文字の計算は未実装。
                return '\0';

            default:
                // 上記以外のエスケープ文字の場合

                Debug.WriteLine("Escape Sequence Error  [{0}]", text[pos + 1]);

                pos += 2;
                return '\0';
            }
        }

        /*
            字句解析をして各文字の字句型の配列を得る。
        */
        public TToken[] LexicalAnalysis(string text, ETokenType prev_token_type) {
            List<TToken> token_list = new List<TToken>();

            // 文字列の長さ
            int text_len = text.Length;

            // 現在の文字位置
            int pos = 0;

            // 各文字の字句型の配列
            //ETokenType[] token_type_list = new ETokenType[text_len];

            // 文字列の最後までループする。
            while (pos < text_len) {
                EKind token_kind = EKind.Undefined;
                ETokenType token_type = ETokenType.Error;

                // 字句の開始位置
                int start_pos = pos;

                // 現在位置の文字
                char ch1 = text[pos];

                // 次の文字の位置。行末の場合は'\0'
                char ch2;

                if (pos + 1 < text.Length) {
                    // 行末でない場合

                    ch2 = text[pos + 1];
                }
                else {
                    // 行末の場合

                    ch2 = '\0';
                }

                bool is_white = false;
                if (pos == 0 && (prev_token_type == ETokenType.BlockCommentContinued || prev_token_type == ETokenType.VerbatimStringContinued)) {
                    // 文字列の最初で直前が閉じてないブロックコメントか逐語的文字列の場合

                    if (prev_token_type == ETokenType.BlockCommentContinued) {
                        // 閉じてないブロックコメントの場合

                        // ブロックコメントの終わりを探す。
                        int k = text.IndexOf("*/");

                        if (k != -1) {
                            // ブロックコメントの終わりがある場合

                            token_kind = EKind.BlockComment;
                            token_type = ETokenType.BlockComment;
                            pos = k + 2;
                        }
                        else {
                            // ブロックコメントの終わりがない場合

                            token_kind = EKind.BlockCommentContinued;
                            token_type = ETokenType.BlockCommentContinued;
                            pos = text_len;
                        }
                    }
                    else {
                        // 逐語的文字列の場合

                        token_kind = EKind.StringLiteral;

                        // 逐語的文字列の終わりを探す。
                        int k = text.IndexOf('\"');

                        if (k != -1) {
                            // 逐語的文字列の終わりがある場合

                            token_type = ETokenType.VerbatimString;
                            pos = k + 1;
                        }
                        else {
                            // 逐語的文字列の終わりがない場合

                            token_type = ETokenType.VerbatimStringContinued;
                            pos = text_len;
                        }
                    }
                }
                else if (char.IsWhiteSpace(ch1)) {
                    // 空白の場合

                    is_white = true;

                    // 空白の終わりを探す。
                    for (pos++; pos < text_len && char.IsWhiteSpace(text[pos]); pos++) ;
                }
                else if (ch1 == '@' && ch2 == '\"') {
                    // 逐語的文字列の場合

                    token_kind = EKind.StringLiteral;

                    // 逐語的文字列の終わりの位置
                    int k = text.IndexOf('\"', pos + 2);

                    if (k != -1) {
                        // 逐語的文字列の終わりがある場合

                        token_type = ETokenType.VerbatimString;
                        pos = k + 1;
                    }
                    else {
                        // 逐語的文字列の終わりがない場合

                        token_type = ETokenType.VerbatimStringContinued;
                        pos = text_len;
                    }
                }
                else if (char.IsLetter(ch1) || ch1 == '_') {
                    // 識別子の最初の文字の場合

                    // 識別子の文字の最後を探す。識別子の文字はユニコードカテゴリーの文字か数字か'_'。
                    for (pos++; pos < text_len && (char.IsLetterOrDigit(text[pos]) || text[pos] == '_'); pos++) ;

                    // 識別子の文字列
                    string name = text.Substring(start_pos, pos - start_pos);

                    if (KeywordMap.TryGetValue(name, out token_kind)) {
                        // 名前がキーワード辞書にある場合

                        token_type = ETokenType.Keyword;
                    }
                    else {
                        // 名前がキーワード辞書にない場合

                        token_kind = EKind.Identifier;
                        token_type = ETokenType.Identifier;
                    }
                }
                else if (char.IsDigit(ch1)) {
                    // 数字の場合

                    token_kind = EKind.NumberLiteral;

                    if (ch1 == '0' && ch2 == 'x') {
                        // 16進数の場合

                        pos += 2;

                        // 16進数字の終わりを探す。
                        for (; pos < text_len && IsHexDigit(text[pos]); pos++) ;

                        token_type = ETokenType.Int;
                    }
                    else {
                        // 10進数の場合

                        // 10進数の終わりを探す。
                        for (; pos < text_len && char.IsDigit(text[pos]); pos++) ;

                        if (pos < text_len && text[pos] == '.') {
                            // 小数点の場合

                            pos++;

                            // 10進数の終わりを探す。
                            for (; pos < text_len && char.IsDigit(text[pos]); pos++) ;

                            if (pos < text_len && text[pos] == 'f') {
                                // floatの場合

                                pos++;
                                token_type = ETokenType.Float;
                            }
                            else {
                                // doubleの場合

                                token_type = ETokenType.Double;
                            }
                        }
                        else {

                            token_type = ETokenType.Int;
                        }
                    }
                }
                else if (ch1 == '\'') {
                    // 文字の場合

                    pos++;
                    if (ch2 == '\\') {
                        // エスケープ文字の場合

                        // エスケープ文字を読み込み、文字位置(pos)を進める。
                        ReadEscapeChar(text, ref pos);
                    }
                    else {
                        // エスケープ文字でない場合

                        pos++;
                    }

                    if (pos < text_len && text[pos] == '\'') {
                        // 文字の終わりがある場合

                        pos++;
                        token_kind = EKind.CharLiteral;
                        token_type = ETokenType.Char_;
                    }
                    else {
                        // 文字の終わりがない場合

                        token_type = ETokenType.Error;
                    }
                }
                else if (ch1 == '\"') {
                    // 文字列の場合

                    token_kind = EKind.StringLiteral;
                    token_type = ETokenType.Error;

                    // 文字列の終わりを探す。
                    for (pos++; pos < text_len;) {
                        char ch3 = text[pos];

                        if (ch3 == '\"') {
                            // 文字列の終わりの場合

                            // ループを抜ける。
                            pos++;
                            token_type = ETokenType.String_;
                            break;
                        }
                        else if (ch3 == '\\') {
                            // エスケープ文字の場合

                            // エスケープ文字を読み込み、文字位置(pos)を進める。
                            ReadEscapeChar(text, ref pos);
                        }
                        else {
                            // エスケープ文字でない場合

                            pos++;
                        }
                    }
                }
                else if (ch1 == '/' && ch2 == '/') {
                    // 行コメントの場合

                    // 空白か行の先頭までstart_posを戻す。
                    while (0 < start_pos && char.IsWhiteSpace(text[start_pos - 1])) {
                        start_pos--;
                    }

                    token_kind = EKind.LineComment;
                    token_type = ETokenType.LineComment;

                    // 改行を探す。
                    int k = text.IndexOf('\n', pos);

                    if (k != -1) {
                        // 改行がある場合

                        pos = k;
                    }
                    else {
                        // 改行がない場合

                        pos = text_len;
                    }
                }
                else if (ch1 == '/' && ch2 == '*') {
                    // ブロックコメントの場合

                    // 空白か行の先頭までstart_posを戻す。
                    while (0 < start_pos && char.IsWhiteSpace(text[start_pos - 1])) {
                        start_pos--;
                    }

                    // ブロックコメントの終わりを探す。
                    int idx = text.IndexOf("*/", pos + 2);

                    if (idx != -1) {
                        // ブロックコメントの終わりがある場合

                        token_kind = EKind.BlockComment;
                        token_type = ETokenType.BlockComment;
                        pos = idx + 2;
                    }
                    else {
                        // ブロックコメントの終わりがない場合

                        token_kind = EKind.BlockCommentContinued;
                        token_type = ETokenType.BlockCommentContinued;
                        pos = text_len;
                    }
                }
                else if (ch1 < 256 && ch2 < 256 && SymbolTable2[ch1, ch2] != EKind.Undefined) {
                    // 2文字の記号の表にある場合

                    token_type = ETokenType.Symbol;
                    token_kind = SymbolTable2[ch1, ch2];
                    pos += 2;
                }
                else if (ch1 < 256 && SymbolTable1[ch1] != EKind.Undefined) {
                    // 1文字の記号の表にある場合

                    token_type = ETokenType.Symbol;
                    token_kind = SymbolTable1[ch1];
                    pos++;
                }
                else {
                    // 不明の文字の場合

                    token_type = ETokenType.Error;
                    pos++;
                }

                if (!is_white) {
                    // 空白でない場合

                    // 字句の文字列を得る。
                    string s = text.Substring(start_pos, pos - start_pos);

                    // トークンを作り、トークンのリストに追加する。
                    token_list.Add(new TToken(token_type, token_kind, s, start_pos, pos));
                }
            }

            // 各文字の字句型の配列を返す。
            return token_list.ToArray();
        }
    }
}
