﻿using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;

namespace Miyu {

    partial class TProject {
        /*
            コールグラフを作ります。
        */
        public void MakeCallGraph() {
            TSetRefFnc set_ref_fnc = new TSetRefFnc();
            set_ref_fnc.ProjectNavi(this, null);

            TSetAppFnc set_app_fnc = new TSetAppFnc();
            set_app_fnc.ProjectNavi(this, null);

            var vcls = from x in ClassTable.Values where x.Info == null && !(x is TGenericClass) && x.SourceFileCls != null select x;

            TType main_class = (from x in vcls where x.ClassName == "TProject" select x).First();

            var vfnc = from c in vcls from f in c.Functions select f;

            Dictionary<string, TFncCall> dic = new Dictionary<string, TFncCall>();

            TFunction main = (from f in vfnc where f.NameVar == "Main" select f).First();
           
            MainCall = CallGraphSub(dic, null, main.ClassMember, main);

            StringWriter sw = new StringWriter();
            Stack<TFncCall> stack = new Stack<TFncCall>();
            DmpFncCall(sw, stack, MainCall, 0);

            File.WriteAllText(OutputDir + "\\DmpFncCall.txt", sw.ToString(), Encoding.UTF8);

            TNode.NodeCnt = 0;
            var vnd = (from x in dic.Values select new TNode(x)).ToList();
            foreach(TNode nd1 in vnd) {
                TFncCall call = nd1.ObjNode as TFncCall;
                foreach(TFncCall c in call.CallTo) {
                    TNode nd2 = (from x in vnd where x.ObjNode == c select x).First();
                    nd1.OutNodes.Add(nd2);
                }
            }

            WriteDotFile("コールグラフ", vnd, OutputDir + "\\FncCall.dot");
        }

        public void DmpFncCall(StringWriter sw, Stack<TFncCall> stack, TFncCall fnc_call, int nest) {
            if (stack.Contains(fnc_call)) {
                return;
            }
            stack.Push(fnc_call);
            fnc_call.MaxNest = Math.Max(fnc_call.MaxNest, stack.Count);

            sw.WriteLine("{0}{1}", new string(' ', 4 * nest), fnc_call.Name());
            foreach(TFncCall c in fnc_call.CallTo) {
                DmpFncCall(sw, stack, c, nest + 1);
            }
            stack.Pop();
        }

        public TFncCall CallGraphSub(Dictionary<string, TFncCall> dic, TFncCall parent_call, TType tp, TFunction fnc) {
            if(fnc.InfoFnc != null || SysSourceFile.FunctionsSrc.Contains(fnc) || fnc.ClassMember is TGenericClass) {
                return null;
            }

            string name = tp.GetClassText() + "->" + fnc.GetFunctionSignature();

            TFncCall fnc_call;

            if (dic.TryGetValue(name, out fnc_call)) {
                // 処理済みの場合

                if (parent_call != null && !parent_call.CallTo.Contains(fnc_call)) {
                    parent_call.CallTo.Add(fnc_call);
                }

                return fnc_call;
            }

            fnc_call = new TFncCall(tp, fnc);
            if (parent_call != null) {
                parent_call.CallTo.Add(fnc_call);
            }
            dic.Add(name, fnc_call);

            // メソッド内のメソッド呼び出しに対し
            foreach (TApply app in fnc.AppFnc) {
                TType tp2 = tp;

                if(app is TDotApply) {
                    tp2 = (app as TDotApply).DotApp.TypeTrm;
                }

                switch (app.KindApp) {
                case EKind.FunctionApply:
                    if(app.FunctionApp is TReference) {
                        TReference ref1 = app.FunctionApp as TReference;
                        Debug.Assert(ref1.VarRef is TFunction);

/*
                        TFunction f1 = tp2.GetVirtualFunction(ref1.VarRef as TFunction);
                        Debug.Assert(f1 != null);
                        CallGraphSub(dic, fnc_call, tp2, f1);
*/
                        IEnumerable<TFunction> vf = tp2.GetVirtualFunctions(ref1.VarRef as TFunction);
                        Debug.Assert(vf.Any());
                        foreach(TFunction f in vf) {

                            CallGraphSub(dic, fnc_call, tp2, f);
                        }
                    }
                    break;

                case EKind.NewInstance:
                    break;

                case EKind.base_:
                    break;
                }
            }

            // メソッド内のラムダ関数に対し
            var vlambda = from x in fnc.ReferencesInFnc where x.VarRef is TFunction && (x.VarRef as TFunction).KindFnc == EKind.Lambda select (x.VarRef as TFunction);
            foreach(TFunction lambda in vlambda) {

                CallGraphSub(dic, fnc_call, tp, lambda);
            }

            return fnc_call;
        }

        // dotファイルに書く
        public void WriteDotFile(string title, List<TNode> vnd, string path1) {
            StringWriter sw1 = new StringWriter();

            sw1.WriteLine("digraph " + title + " {");
            sw1.WriteLine("\tgraph [charset=\"UTF-8\", rankdir = LR];");

            // ノードの集合からdotファイルを作る
            Node2Dot(sw1, vnd);

            sw1.WriteLine("}");

            File.WriteAllText(path1, sw1.ToString(), new UTF8Encoding(false));
        }

        public void Node2Dot(StringWriter sw, List<TNode> vnd) {
            foreach(TNode nd1 in vnd) {
                //sw.WriteLine("\tn{0} [shape = ellipse, label = \"{1}\"];", nd1.IdxNode, nd1.NameNode);
                //sw.WriteLine("\tn{0} [shape = circle, label = \"{1}\"];", nd1.IdxNode, nd1.NameNode);
                if(nd1.FontColor == Colors.Blue) {

                    sw.WriteLine("\tn{0} [shape = box, label = \"{1}\", fontcolor = blue ];", nd1.IdxNode, nd1.NameNode);
                }
                else if (nd1.FontColor == Colors.Red) {

                    sw.WriteLine("\tn{0} [shape = box, label = \"{1}\", fontcolor = red ];", nd1.IdxNode, nd1.NameNode);
                }
                else {

                    sw.WriteLine("\tn{0} [shape = box, label = \"{1}\"];", nd1.IdxNode, nd1.NameNode);
                }
            }
            foreach (TNode nd1 in vnd) {
                foreach(TNode nd2 in nd1.OutNodes) {
                    sw.WriteLine("n{0} -> n{1};", nd1.IdxNode, nd2.IdxNode);
                }
            }

            int max_rank = (from x in vnd select x.RankNode).Max();
            for(int i = 0; i <= max_rank; i++) {
                var v = from x in vnd where x.RankNode == i select x;
                if (v.Any()) {

                    sw.Write("\t{rank = same");
                    foreach (TNode nd1 in v) {
                        sw.Write("; n{0}", nd1.IdxNode);
                    }
                    sw.WriteLine(" }");
                }
            }
        }

        public void MakeUseDefineChainSub(TField fld, List<TFncCall> defined_path, Stack<TFncCall> stack, TFncCall fnc_call) {
            if (stack.Contains(fnc_call) || defined_path.Contains(fnc_call)) {
                return;
            }
            stack.Push(fnc_call);

            // メソッド内のfldの参照のリスト
            var v = from r in fnc_call.FncCall.ReferencesInFnc where r.VarRef == fld select r;
            if (v.Any()) {
                // fldの参照がある場合

                if ( (from x in v where x.Defined select x).Any() ) {

                    fnc_call.FontColor = Colors.Red;
                }
                else {

                    fnc_call.FontColor = Colors.Blue;
                }

                defined_path.AddRange( (from x in stack where ! defined_path.Contains(x) select x).ToList() );
            }
            else {
                // fldの参照がない場合

                fnc_call.FontColor = Colors.Black;
            }

            foreach (TFncCall c in fnc_call.CallTo) {
                MakeUseDefineChainSub(fld, defined_path,stack, c);
            }

            stack.Pop();
        }

        /*
        使用・定義連鎖を作ります。
        */
        public void MakeUseDefineChain() {
            string use_def_dir = OutputDir + "\\UseDef";

            if (! Directory.Exists(use_def_dir)) {
                Directory.CreateDirectory(use_def_dir);
            }

            // アプリのクラスに対し
            foreach (TType cls in AppClasses) {

                // クラスのフィールドに対し
                foreach (TField fld in cls.Fields) {

                    // フィールドに値を代入する変数参照のリスト
                    List<TReference> defined_refs = (from x in fld.RefsVar where x.Defined select x).ToList();

                    if (defined_refs.Any()) {
                        // フィールドに値を代入する変数参照がある場合

                      List<TFncCall> defined_path = new List<TFncCall>();

                      Stack<TFncCall> stack = new Stack<TFncCall>();
                        MakeUseDefineChainSub(fld, defined_path, stack, MainCall);

                        TNode.NodeCnt = 0;
                        var vnd = (from x in defined_path select new TNode(x)).ToList();
                        foreach (TNode nd1 in vnd) {
                            TFncCall call = nd1.ObjNode as TFncCall;
                            foreach (TFncCall c in call.CallTo) {
                                var vnd2 = from x in vnd where x.ObjNode == c select x;
                                if (vnd2.Any()) {

                                    nd1.OutNodes.AddRange(vnd2);
                                }
                            }
                        }

                        if (vnd.Any()) {

                            string dot_path = string.Format("{0}\\{1}.{2}.dot", use_def_dir, cls.ClassName, fld.NameVar);
                            WriteDotFile("使用・定義連鎖", vnd, dot_path);
                        }
                    }
                }
            }
        }

        /*
        クラス図を作ります。
        */
        public void MakeClassDiagram() {
            StringWriter sw = new StringWriter();

            sw.WriteLine("@startuml");
            sw.WriteLine("title <size:18>クラス図</size>");

            foreach(TType c in AppClasses) {
                if(c.KindClass != EClass.Enum) {

                    sw.WriteLine("class {0} {{", c.ClassName);
                    sw.WriteLine("\t[[http://www.google.com]]");

                    foreach (TField fld in c.Fields) {
                        sw.WriteLine("\t\t{0} {1}", fld.TypeVar.GetClassText(), fld.NameVar);
                    }

                    sw.WriteLine("}");

                    foreach(TType c2 in c.SuperClasses) {
                        sw.WriteLine("{0} <|-- {1}", c2.ClassName, c.ClassName);
                    }
                }
            }

            sw.WriteLine("@enduml");

            File.WriteAllText(OutputDir + "\\ClassDiagram.txt", sw.ToString(), new UTF8Encoding(false));
        }

        public void WriteComment(TToken[] comments, TTokenWriter sw) {
            if (comments != null && comments.Length != 0) {
                foreach (TToken tk in comments) {
                    sw.Fmt(tk);
                }
            }
        }

        /*
        要約を作ります。
        */
        public void MakeSummary() {

            foreach (TSourceFile src in SourceFiles) {
                TTokenWriter sw = new TTokenWriter(TEnv.Parser);

                Debug.WriteLine("{0} ------------------------------", src.PathSrc, "");
                foreach (TType cls in src.ClassesSrc) {

                    //sw.Fmt(cls.ClassName, " --------------------", EKind.NL);
                    //if (cls.SourceFileCls == src) {

                    //    WriteComment(cls.CommentCls, sw);
                    //}
                    //else {
                    //    sw.WriteLine();
                    //}

                    //var vfld = from x in src.FieldsSrc where x.ClassMember == cls select x;
                    //foreach (TField fld in vfld) {
                    //    sw.Fmt(fld.NameVar, " ----------", EKind.NL);
                    //    WriteComment(fld.CommentVar, sw);
                    //}

                    var vfnc = from x in src.FunctionsSrc where x.ClassMember == cls && x.KindFnc != EKind.Lambda && x.CommentVar != null && x.CommentVar.Length != 0 select x;
                    foreach (TFunction fnc in vfnc) {
                        var vc = from c in fnc.CommentVar where c.Kind == EKind.BlockCommentContinued && c.TextTkn.Trim() != "/*" select c;
                        if (vc.Any()) {

                            //sw.Fmt(fnc.NameVar, " ----------", EKind.NL);
                            Debug.WriteLine("{0} {1}", fnc.NameVar, vc.First().TextTkn.Trim());
                            //foreach(TToken c in vc) {
                            //    string s = c.TextTkn.Trim();
                            //    if(s != "/*" && s != "*/") {

                            //        switch (c.Kind) {
                            //        case EKind.LineComment:
                            //            Debug.WriteLine("行 {0}", c.TextTkn, "");
                            //            break;
                            //        case EKind.BlockComment:
                            //            Debug.WriteLine("ブロック {0}", c.TextTkn, "");
                            //            break;
                            //        case EKind.BlockCommentContinued:
                            //            Debug.WriteLine("継続 {0}", c.TextTkn, "");
                            //            break;
                            //        }

                            //        break;
                            //    }
                            //}


                            //WriteComment(fnc.CommentVar, sw);
                        }
                    }
                }

                //Debug.WriteLine(sw.ToPlainText());
            }
        }
    }

    public class TFncCall {
        public TType TypeCall;
        public TFunction FncCall;
        public List<TFncCall> CallTo = new List<TFncCall>();
        public int MaxNest;
        public Color FontColor = Colors.Black;

        public TFncCall(TType tp, TFunction fnc) {
            TypeCall = tp;
            FncCall = fnc;
        }

        public string Name() {
            //return TypeCall.GetClassText() + "->" + FncCall.FullName();
            if (FncCall.NameVar == null) {

                return "λ";
            }
            else {

                return FncCall.NameVar;
            }
        }
    }

    public class TNode {
        public static int NodeCnt;
        public int IdxNode;
        public string NameNode;
        public object ObjNode;
        public int RankNode;
        public List<TNode> OutNodes = new List<TNode>();
        public Color FontColor = Colors.Black;

        public TNode(TFncCall call) {
            IdxNode = NodeCnt;
            NodeCnt++;

            NameNode = call.Name();
            ObjNode = call;
            RankNode = call.MaxNest;
            FontColor = call.FontColor;
        }
    }
}