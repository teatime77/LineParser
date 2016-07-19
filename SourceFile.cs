using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Windows.UI;

namespace Miyu {

    public class TSourceFile : TEnv {
        // 改行文字
        public const char LF = '\n';

        // 文書内のテキスト
        public List<TChar> Chars = new List<TChar>();

        public string PathSrc;
        public TNamespace Namespace;
        public List<TUsing> Usings = new List<TUsing>();
        public List<TType> ClassesSrc = new List<TType>();
        public List<TLine> Lines = new List<TLine>();
        public List<MyEditor> Editors = new List<MyEditor>();

        public TParser Parser;

        public List<TField> FieldsSrc = new List<TField>();
        public List<TFunction> FunctionsSrc = new List<TFunction>();

        public TSourceFile(string path, TParser parser) {
            PathSrc = path;
            Parser = parser;

            string text = File.ReadAllText(path, Encoding.UTF8).Replace("\r\n", "\n");
            Chars.AddRange(from c in text select new TChar(c));

            Lines.Clear();
            UpdateTokenType(0, 0, Chars.Count);
        }

        /*
            行の先頭位置を返します。
        */
        public int GetLineTop(int current_pos) {
            int i;

            for (i = current_pos - 1; 0 <= i && Chars[i].Chr != LF; i--) ;
            return i + 1;
        }

        /*
            次の行の先頭位置を返します。
        */
        public int GetNextLineTop(int current_pos) {
            for (int i = current_pos; i < Chars.Count; i++) {
                if (Chars[i].Chr == LF) {
                    return i + 1;
                }
            }

            return -1;
        }

        /*
            次の行の先頭位置または文書の終わりを返します。
        */
        public int GetNextLineTopOrEOT(int current_pos) {
            int i = GetNextLineTop(current_pos);

            return (i != -1 ? i : Chars.Count);
        }

        /*
            行のインデックスを返します。
        */
        public int GetLineIndex(int pos) {
            return (from x in Chars.GetRange(0, pos) where x.Chr == LF select x).Count();
        }

        /*
            行の最終位置を返します。
            行の最終位置は改行文字の位置または文書の最後の位置です。
        */
        public int GetLineEnd(int current_pos) {
            int i = GetNextLineTop(current_pos);

            return (i != -1 ? i - 1 : Chars.Count);
        }

        /*
            指定した範囲にある改行文字の個数を返します。
        */
        public int GetLFCount(int start_pos, int end_pos) {
            return (from x in Chars.GetRange(start_pos, Math.Min(Chars.Count, end_pos) - start_pos) where x.Chr == LF select x).Count();
        }

        /*
            指定した範囲のテキストを返します。
        */
        public string StringFromRange(int start_pos, int end_pos) {
            return new string((from c in Chars.GetRange(start_pos, Math.Min(Chars.Count, end_pos) - start_pos) select c.Chr).ToArray());
        }

        /*
            字句型を更新します。
        */
        public void UpdateTokenType(int start_line_idx, int sel_start, int sel_end) {
            // 行の先頭位置
            int line_top = GetLineTop(sel_start);

            // 直前の字句型
            ETokenType last_token_type = (line_top == 0 ? ETokenType.Undefined : Chars[line_top - 1].CharType);

            for (int line_idx = start_line_idx; ; line_idx++) {
                TLine line;

                if (line_idx < Lines.Count) {

                    line = Lines[line_idx];
                }
                else {

                    line = new TLine();
                    Lines.Add(line);
                }

                // 次の行の先頭位置または文書の終わり
                int next_line_top = GetNextLineTopOrEOT(line_top);

                // 行の先頭位置から次の行の先頭位置または文書の終わりまでの文字列
                line.TextLine = StringFromRange(line_top, next_line_top);

                // 変更前の最後の字句型
                ETokenType last_token_type_before = (next_line_top == 0 ? ETokenType.Undefined : Chars[next_line_top - 1].CharType);

                // 現在行の字句解析をして字句タイプのリストを得ます。
                TToken[] tokens = Parser.LexicalAnalysis(line.TextLine, last_token_type);
                lock (this) {
                    line.Tokens = tokens;
                }
                foreach (TToken tkn in line.Tokens) {

                    // 字句型をテキストにセットします。
                    for (int i = tkn.StartPos; i < tkn.EndPos; i++) {
                        TChar ch = Chars[line_top + i];
                        ch.CharType = tkn.TokenType;
                        Chars[line_top + i] = ch;

                        last_token_type = tkn.TokenType;
                    }
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

                            if (last_token_type_before != ETokenType.BlockCommentContinued && last_token_type_before != ETokenType.VerbatimStringContinued) {
                                // 変更前も最後の字句が複数行にまたがらない場合

                                if (last_token_type != ETokenType.BlockCommentContinued && last_token_type != ETokenType.VerbatimStringContinued) {
                                    // 変更後も最後の字句が複数行にまたがらない場合

                                    break;
                                }
                            }
                        }
                    }
                }

                // 次の行の字句解析をします。
                line_top = next_line_top;
            }
        }

        /*
            字句型から色を得ます。
        */
        public static Color ColorFromTokenType(ETokenType token_type) {
            switch (token_type) {
            case ETokenType.Char_:
            case ETokenType.String_:
            case ETokenType.VerbatimString:
            case ETokenType.VerbatimStringContinued:
                return Colors.Red;

            case ETokenType.Keyword:
                return Colors.Blue;

            case ETokenType.LineComment:
            case ETokenType.BlockComment:
            case ETokenType.BlockCommentContinued:
                return Colors.Green;

            default:
                return Colors.Black;
            }
        }

        /*
            CSSの色指定の文字列を得ます。
        */
        public static string ColorStyleString(Color c) {
            return string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
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
    }
}
