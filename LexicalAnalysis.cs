using System.Diagnostics;
using System.Collections.Generic;
using System;
using Windows.UI;
using System.IO;
using System.Net;

/*--------------------------------------------------------------------------------
        字句解析
--------------------------------------------------------------------------------*/

namespace MyEdit {
    /*
        字句型
    */
    public enum ETokenType {
        Undefined,      // 未定義
        White,          // 空白
        Char_,          // 文字
        String_,        // 文字列
        VerbatimString, // 逐語的文字列 ( @"文字列" )
        Identifier,     // 識別子
        Keyword,        // キーワード
        Number,         // 数値
        Symbol,         // 記号
        LineComment,    // 行コメント      ( // )
        BlockComment,   // ブロックコメント ( /* */ )
        Error,          // エラー
    }

    partial class MyEditor {

        // キーワードの文字列の辞書
        public Dictionary<string, EKind> KeywordMap;

        // 2文字の記号の表
        public EKind[,] SymbolTable2 = new EKind[256, 256];

        // 1文字の記号の表
        public EKind[] SymbolTable1 = new EKind[256];

        /*
            16進数文字ならtrueを返します。
        */
        public bool IsHexDigit(char ch) {
            return char.IsDigit(ch) || 'a' <= ch && ch <= 'f' || 'A' <= ch && ch <= 'F';
        }

        /*
            エスケープ文字を読み込み、文字位置(pos)を進めます。
        */
        public char ReadEscapeChar(string text, ref int pos) {
            if(text.Length <= pos + 1) {
                return '\0';
            }

            // 1文字のエスケープ文字の変換リスト
            string in_str = "\'\"\\0abfnrtv";
            string out_str = "\'\"\\\0\a\b\f\n\r\t\v";

            // 変換リストにあるか調べます。
            int k = in_str.IndexOf(text[pos + 1]);

            if (k != -1) {
                // 変換リストにある場合

                pos += 2;

                // 変換した文字を返します。
                return out_str[k];
            }

            switch(text[pos + 1]) {
            case 'u':
                // \uXXXX

                pos = Math.Min(pos + 6, text.Length);

                // エスケープ文字の計算は未実装です。
                return '\0';

            case 'U':
                // \UXXXXXXXX

                pos = Math.Min(pos + 10, text.Length);

                // エスケープ文字の計算は未実装です。
                return '\0';

            case 'x':
                // \xX...

                // 16進数字の終わりを探します。
                for (pos++; pos < text.Length && IsHexDigit(text[pos]); pos++);

                // エスケープ文字の計算は未実装です。
                return '\0';

            default:
                // 上記以外のエスケープ文字の場合

                Debug.WriteLine("Escape Sequence Error  [{0}]", text[pos + 1]);

                pos += 2;
                return '\0';
            }

        }

        /*
            字句解析をして各文字の字句型の配列を返します。
        */
        public TToken[] LexicalAnalysis(string text, ETokenType prev_token_type) {
            List<TToken> token_list = new List<TToken>();

            // 文字列の長さ
            int text_len = text.Length;

            // 現在の文字位置
            int pos = 0;

            // 各文字の字句型の配列
            //ETokenType[] token_type_list = new ETokenType[text_len];

            // 文字列の最後までループします。
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

                if(pos == 0 && (prev_token_type == ETokenType.BlockComment || prev_token_type == ETokenType.VerbatimString)) {
                    // 文字列の最初で直前がブロックコメントか逐語的文字列の場合

                    if (prev_token_type == ETokenType.BlockComment) {
                        // ブロックコメントの場合

                        token_type = ETokenType.BlockComment;

                        // ブロックコメントの終わりを探します。
                        int k = text.IndexOf("*/");
                
                        if (k != -1) {
                            // ブロックコメントの終わりがある場合

                            pos = k + 2;
                        }
                        else {
                            // ブロックコメントの終わりがない場合

                            pos = text_len;
                        }
                    }
                    else {
                        // 逐語的文字列の場合

                        token_type = ETokenType.VerbatimString;

                        // 逐語的文字列の終わりを探します。
                        int k = text.IndexOf('\"');

                        if (k != -1) {
                            // 逐語的文字列の終わりがある場合

                            pos = k + 1;
                        }
                        else {
                            // 逐語的文字列の終わりがない場合

                            pos = text_len;
                        }
                    }
                }
                else if (char.IsWhiteSpace(ch1)) {
                    // 空白の場合

                    token_type = ETokenType.White;

                    // 空白の終わりを探します。
                    for (pos++; pos < text_len && char.IsWhiteSpace(text[pos]); pos++) ;
                }
                else if (ch1 == '@' && ch2 == '\"') {
                    // 逐語的文字列の場合

                    token_type = ETokenType.VerbatimString;

                    // 逐語的文字列の終わりの位置
                    int k = text.IndexOf('\"', pos + 2);

                    if (k != -1) {
                        // 逐語的文字列の終わりがある場合

                        pos = k + 1;
                    }
                    else {
                        // 逐語的文字列の終わりがない場合

                        pos = text_len;
                    }
                }
                else if (char.IsLetter(ch1) || ch1 == '_') {
                    // 識別子の最初の文字の場合

                    // 識別子の文字の最後を探します。識別子の文字はユニコードカテゴリーの文字か数字か'_'です。
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

                    token_type = ETokenType.Number;

                    if (ch1 == '0' && ch2 == 'x') {
                        // 16進数の場合

                        pos += 2;

                        // 16進数字の終わりを探します。
                        for (; pos < text_len && IsHexDigit(text[pos]); pos++) ;
                    }
                    else {
                        // 10進数の場合

                        // 10進数の終わりを探します。
                        for (; pos < text_len && char.IsDigit(text[pos]); pos++) ;

                        if (pos < text_len && text[pos] == '.') {
                            // 小数点の場合

                            pos++;

                            // 10進数の終わりを探します。
                            for (; pos < text_len && char.IsDigit(text[pos]); pos++) ;
                        }
                    }
                }
                else if (ch1 == '\'') {
                    // 文字の場合

                    pos++;
                    if (ch2 == '\\') {
                        // エスケープ文字の場合

                        // エスケープ文字を読み込み、文字位置(pos)を進めます。
                        ReadEscapeChar(text, ref pos);
                    }
                    else {
                        // エスケープ文字でない場合

                        pos++;
                    }

                    if (pos < text_len && text[pos] == '\'') {
                        // 文字の終わりがある場合

                        pos++;
                        token_type = ETokenType.Char_;
                    }
                    else {
                        // 文字の終わりがない場合

                        token_type = ETokenType.Error;
                    }
                }
                else if (ch1 == '\"') {
                    // 文字列の場合

                    token_type = ETokenType.Error;

                    // 文字列の終わりを探します。
                    for (pos++; pos < text_len;) {
                        char ch3 = text[pos];

                        if (ch3 == '\"') {
                            // 文字列の終わりの場合

                            // ループを抜けます。
                            pos++;
                            token_type = ETokenType.String_;
                            break;
                        }
                        else if (ch3 == '\\') {
                            // エスケープ文字の場合

                            // エスケープ文字を読み込み、文字位置(pos)を進めます。
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

                    token_type = ETokenType.LineComment;

                    // 改行を探します。
                    int k = text.IndexOf('\n', pos);

                    if(k != -1) {
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

                    token_type = ETokenType.BlockComment;

                    // ブロックコメントの終わりを探します。
                    int idx = text.IndexOf("*/", pos + 2);

                    if (idx != -1) {
                        // ブロックコメントの終わりがある場合

                        pos = idx + 2;
                    }
                    else {
                        // ブロックコメントの終わりがない場合

                        pos = text_len;
                    }
                }
                else {
                    // 上記以外は1文字の記号とします。

                    if(ch1 < 256 && ch2 < 256 && SymbolTable2[ch1, ch2] != EKind.Undefined) {

                        token_type = ETokenType.Symbol;
                        token_kind = SymbolTable2[ch1, ch2];
                        pos += 2;
                    }
                    else if (ch1 < 256 && SymbolTable1[ch1] != EKind.Undefined) {

                        token_type = ETokenType.Symbol;
                        token_kind = SymbolTable1[ch1];
                        pos++;
                    }
                    else {

                        token_type = ETokenType.Error;
                        pos++;
                    }
                }

                token_list.Add(new TToken(token_type, token_kind, start_pos, pos));

                // 各文字の字句型の配列に字句型をセットします。
                //for (int k = start_pos; k < pos; k++) {
                //    token_type_list[k] = token_type;
                //}
            }

            // 各文字の字句型の配列を返します。
            return token_list.ToArray();
        }

        /*
            字句型を更新します。
        */
        void UpdateTokenType(int start_line_idx, int sel_start, int sel_end) {
            // 行の先頭位置
            int line_top = GetLineTop(sel_start);

            // 直前の字句型
            ETokenType last_token_type = (line_top == 0 ? ETokenType.Undefined : Chars[line_top - 1].CharType);

            TParser parser = new TParser();

            for (int line_idx = start_line_idx; ; line_idx++) {
                TLine line = Lines[line_idx];

                // 次の行の先頭位置または文書の終わり
                int next_line_top = GetNextLineTopOrEOT(line_top);

                // 行の先頭位置から次の行の先頭位置または文書の終わりまでの文字列
                string lex_string = StringFromRange(line_top, next_line_top);

                // 変更前の最後の字句型
                ETokenType last_token_type_before = (next_line_top == 0 ? ETokenType.Undefined : Chars[next_line_top - 1].CharType);

                // 現在行の字句解析をして字句タイプのリストを得ます。
                line.Tokens = LexicalAnalysis(lex_string, last_token_type);
                foreach(TToken tkn in line.Tokens) {

                    // 字句型をテキストにセットします。
                    for (int i = tkn.StartPos; i < tkn.EndPos; i++) {
                        TChar ch = Chars[line_top + i];
                        ch.CharType = tkn.TokenType;
                        Chars[line_top + i] = ch;

                        last_token_type = tkn.TokenType;
                    }
                }

                if(line.Tokens.Length != 0) {

                    parser.ParseLine(line.Tokens);
                }

                if (sel_end <= next_line_top) {
                    // 変更した文字列の字句解析が終わった場合

                    if (Chars.Count <= next_line_top) {
                        // 文書の終わりの場合

                        break;
                    }
                    else {
                        // 文書の終わりでない場合

                        if (last_token_type == last_token_type_before) {
                            // 最後の字句型が同じ場合

                            break;
                        }
                        else {
                            // 最後の字句型が違う場合

                            if (last_token_type_before != ETokenType.BlockComment && last_token_type_before != ETokenType.VerbatimString &&
                               last_token_type != ETokenType.BlockComment && last_token_type != ETokenType.VerbatimString) {
                                // 変更前も変更後の最後の字句が複数行にまたがらない場合

                                break;
                            }
                        }
                    }
                }

                // 次の行の字句解析をします。
                line_top = next_line_top;
            }
        }

        /*
            指定した範囲をHTML文字列に変換して返します。
        */
        public string HTMLStringFromRange(int start_pos, int end_pos) {
            StringWriter sw = new StringWriter();

            sw.WriteLine("<pre><code>");

            for (int pos = start_pos; pos < end_pos;) {
                // 字句の開始位置
                int token_start_pos = pos;

                // 字句型から文字色を得ます。
                Color text_color = ColorFromTokenType(Chars[pos].CharType);

                // 文字色が同じで改行でない文字を１つの字句とします。
                for (; pos < end_pos && Chars[pos].Chr != '\n' && ColorFromTokenType(Chars[pos].CharType) == text_color; pos++) ;

                // HTMLエンコードしてから、空白は&nbsp;に変換します。
                string html_str = WebUtility.HtmlEncode(StringFromRange(token_start_pos, pos));

                if (text_color == Colors.Black) {
                    // 文字色が黒の場合

                    // HTMLにそのまま出力します。
                    sw.Write("{0}", html_str);
                }
                else {
                    // 文字色が黒以外の場合

                    // SPANで文字色を指定して出力します。
                    sw.Write("<span style=\"color:{0}\">{1}</span>", ColorStyleString(text_color), html_str);
                }

                for (; pos < end_pos && Chars[pos].Chr == '\n'; pos++) {
                    // 改行の場合

                    sw.Write("\r\n");
                }
            }
            sw.WriteLine("</code></pre>");

            return sw.ToString();
        }


        /*
            字句型から色を得ます。
        */
        Color ColorFromTokenType(ETokenType token_type) {
            switch (token_type) {
            case ETokenType.Char_:
            case ETokenType.String_:
            case ETokenType.VerbatimString:
                return Colors.Red;

            case ETokenType.Keyword:
                return Colors.Blue;

            case ETokenType.LineComment:
            case ETokenType.BlockComment:
                return Colors.Green;

            default:
                return Colors.Black;
            }
        }

        /*
            CSSの色指定の文字列を得ます。
        */
        string ColorStyleString(Color c) {
            return string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
        }
    }
}