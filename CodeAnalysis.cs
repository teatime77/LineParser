using Microsoft.Graphics.Canvas.UI.Xaml;
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
    /*
     * ユーザー定義の総称型のテスト
     */
    public class TNode2<T> {
        public static int NodeCnt;
        public int IdxNode;
        public string NameNode;
        public T ObjNode;
        public int RankNode;
        public List<TNode2<T>> OutNodes;
        public Color FontColor = Colors.Black;
    }

    partial class TProject {
        /*
            コールグラフを作る。
        */
        public List<TCallNode> MakeCallGraph() {
            // 関数内の参照のリスト(ReferencesInFnc)をセットする。
            TSetReferencesInFnc set_ref_fnc = new TSetReferencesInFnc();
            set_ref_fnc.ProjectNavi(this, null);

            // 関数内の関数呼び出しのリスト(AppsInFnc)をセットする。
            TSetAppsInFnc set_app_fnc = new TSetAppsInFnc();
            set_app_fnc.ProjectNavi(this, null);

            var vcls2 = from x in ClassTable.Values where x.Info == null && x.SourceFileCls == null select x;
            foreach(TType t in vcls2) {
                Debug.WriteLine("??? {0}", t.ClassName, "");
            }

            var vcls = from x in ClassTable.Values where x.Info == null && !(x is TGenericClass) && x.SourceFileCls != null select x;

            TType main_class = (from x in vcls where x.ClassName == "TProject" select x).First();

            var vfnc = from c in vcls from f in c.Functions select f;

            Dictionary<string, TCallNode> dic = new Dictionary<string, TCallNode>();

            TFunction main = (from f in vfnc where f.NameVar == "Main" select f).First();

            // 再帰的にコールグラフのノードまたは矢印を追加する。
            MainCall = AddCallNode(dic, null, main.DeclaringType, main);

            StringWriter sw = new StringWriter();
            Stack<TCallNode> stack = new Stack<TCallNode>();

            // 再帰的にコールグラフのノードを文字列出力する。
            DumpCallNode(sw, stack, MainCall, 0);

            // コールグラフの文字列出力をファイルに書く。
            File.WriteAllText(OutputDir + "\\DumpCallNode.txt", sw.ToString(), Encoding.UTF8);

            TNode.NodeCnt = 0;

            // コールグラフのノードのリストから、表示用のノードのリストを作る。
            List<TNode> vnd = CallNodesToNodes(dic.Values);

            // ノードのリストからdotファイルを作る。
            WriteDotFile("コールグラフ", vnd, OutputDir + "\\FncCall.dot");

            // ユーザー定義の総称型のテスト
            TNode2<TCallNode> xx = new TNode2<TCallNode>();

            return dic.Values.ToList();
        }

        /*
            指定された関数を呼ぶまでの経路と、関数から呼ばれる経路のコールグラフを作る。
        */
        public void SpecificFunctionCallGraph(List<TCallNode> nodes_all, TFunction fnc, string fnc_dir) {
            for(int idx = 0; idx < 2; idx++) {

                List<TCallNode> v1 = (from x in nodes_all where x.FncCall == fnc select x).ToList();
                while (true) {
                    List<TCallNode> v2;
                    if(idx == 0) {
                        // fncを呼ぶまでの経路

                        v2 = (from x in nodes_all where x.CallTo.Intersect(v1).Any() select x).ToList();
                    }
                    else {
                        // fncから呼ばれる経路

                        v2 = (from x in nodes_all where x.CallFrom.Intersect(v1).Any() select x).ToList();
                    }

                    v2.AddRange(v1);
                    List<TCallNode> v3 = v2.Distinct().ToList();

                    if (v1.Count == v3.Count) {
                        // 変化しなくなった場合

                        break;
                    }

                    v1 = v3;
                }

                TNode.NodeCnt = 0;

                // コールグラフのノードのリストから、表示用のノードのリストを作る。
                List<TNode> vnd = CallNodesToNodes(TCallNode.CopyCallNodes(v1));

                // ノードのリストからdotファイルを作る。
                string file_name;
                if(idx == 0) {
                    // fncを呼ぶまでの経路

                    file_name = "CallTo.dot";
                }
                else {

                    // fncから呼ばれる経路
                    file_name = "CallFrom.dot";
                }
                WriteDotFile("コールグラフ", vnd, fnc_dir + "\\" + file_name);
            }
        }

        /*
         * コールグラフのノードのリストから、表示用のノードのリストを作る。
         */
        List<TNode> CallNodesToNodes(IEnumerable<TCallNode> call_nodes) {
            // コールグラフのノードのリストから、表示用のノードのリストを作る。
            var vnd = (from x in call_nodes select new TNode(x)).ToList();

            // TCallNode -> TNode の辞書を作る。
            Dictionary<TCallNode, TNode> dic = new Dictionary<TCallNode, TNode>();
            foreach (TNode nd1 in vnd) {
                dic.Add(nd1.ObjNode as TCallNode, nd1);
            }

            // TCallNodeのCallToからTNodeのOutNodesを作る。
            foreach (TCallNode cn in call_nodes) {
                TNode nd = dic[cn];
                foreach(TCallNode c in cn.CallTo) {
                    nd.OutNodes.Add(dic[c]);
                }
            }

            return vnd;
        }

        /*
         * 再帰的にコールグラフのノードまたは矢印を追加する。
         */
        public TCallNode AddCallNode(Dictionary<string, TCallNode> dic, TCallNode parent_call, TType tp, TFunction fnc) {
            if(fnc.InfoFnc != null || SystemSourceFile.FunctionsSrc.Contains(fnc) || fnc.DeclaringType is TGenericClass) {
                // リフレクションで得た関数か、システムファイルの関数か、総称型の関数の場合

                // nullを返す。
                return null;
            }

            string name = tp.GetClassText() + "->" + fnc.GetFunctionSignature();

            TCallNode fnc_call;

            if (dic.TryGetValue(name, out fnc_call)) {
                // 処理済みの場合

                if (parent_call != null && !parent_call.CallTo.Contains(fnc_call)) {
                    // 親ノードがあり、親ノードからこのノードへの矢印がない場合

                    // 親ノードからこのノードへの矢印を追加する。
                    parent_call.AddCallToFrom(fnc_call);
                }

                // 処理済みのノードを返す。
                return fnc_call;
            }

            fnc_call = new TCallNode(tp, fnc);
            if (parent_call != null) {
                // 親ノードがある場合

                // 親ノードからこのノードへの矢印を追加する。
                parent_call.AddCallToFrom(fnc_call);
            }
            dic.Add(name, fnc_call);

            // 関数内の関数呼び出しに対し
            foreach (TApply app in fnc.AppsInFnc) {
                TType tp2;

                if(app is TDotApply) {
                    // ドットつき関数呼び出しの場合

                    tp2 = (app as TDotApply).DotApp.TypeTrm;
                }
                else {
                    // ドットつき関数呼び出しでない場合

                    tp2 = tp;
                }

                switch (app.KindApp) {
                case EKind.FunctionApply:
                    if(app.FunctionApp is TReference) {
                        TReference ref1 = app.FunctionApp as TReference;
                        Debug.Assert(ref1.VarRef is TFunction);

                        // 呼ばれうる仮想関数のリストを得る。
                        IEnumerable<TFunction> virtual_functions = tp2.GetVirtualFunctions(ref1.VarRef as TFunction);
                        Debug.Assert(virtual_functions.Any());

                        // 呼ばれうる仮想関数に対し
                        foreach (TFunction f in virtual_functions) {
                            AddCallNode(dic, fnc_call, f.DeclaringType, f);
                        }
                    }
                    break;

                case EKind.NewInstance:
                    break;

                case EKind.base_:
                    break;
                }
            }

            // 関数内のラムダ関数に対し
            var vlambda = from x in fnc.ReferencesInFnc where x.VarRef is TFunction && (x.VarRef as TFunction).KindFnc == EKind.Lambda select (x.VarRef as TFunction);
            foreach(TFunction lambda in vlambda) {

                AddCallNode(dic, fnc_call, tp, lambda);
            }

            return fnc_call;
        }

        /*
         * 再帰的にコールグラフのノードを文字列出力する。
         */
        public void DumpCallNode(StringWriter sw, Stack<TCallNode> stack, TCallNode fnc_call, int nest) {
            if (stack.Contains(fnc_call)) {
                // スタックにノードがある場合

                return;
            }

            // スタックにノードをプッシュする。
            stack.Push(fnc_call);

            fnc_call.MaxNest = Math.Max(fnc_call.MaxNest, stack.Count);


            sw.WriteLine("{0}{1}", new string(' ', 4 * nest), fnc_call.Name());

            // このノードから出るすべてのノードに対し
            foreach (TCallNode c in fnc_call.CallTo) {
                DumpCallNode(sw, stack, c, nest + 1);
            }

            // スタックからノードをポップする。
            stack.Pop();
        }

        /*
         * ノードのリストからdotファイルを作る。
         */
        public void WriteDotFile(string title, IEnumerable<TNode> vnd, string path1) {
            StringWriter sw1 = new StringWriter();

            sw1.WriteLine("digraph " + title + " {");
            sw1.WriteLine("\tgraph [charset=\"UTF-8\", rankdir = LR];");

            // ノードのリストからdotファイルの内容を作る。
            Node2Dot(sw1, vnd);

            sw1.WriteLine("}");

            File.WriteAllText(path1, sw1.ToString(), new UTF8Encoding(false));
        }

        /*
         * ノードのリストからdotファイルの内容を作る。
         */
        public void Node2Dot(StringWriter sw, IEnumerable<TNode> vnd) {
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

        public void MakeUseDefineChainSub(TField fld, List<TCallNode> defined_path, Stack<TCallNode> stack, TCallNode fnc_call) {
            if (stack.Contains(fnc_call) || defined_path.Contains(fnc_call)) {
                return;
            }
            stack.Push(fnc_call);

            // 関数内のfldの参照のリスト
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

            foreach (TCallNode c in fnc_call.CallTo) {
                MakeUseDefineChainSub(fld, defined_path,stack, c);
            }

            stack.Pop();
        }

        /*
        使用・定義連鎖を作る。
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

                        List<TCallNode> defined_path = new List<TCallNode>();

                        Stack<TCallNode> stack = new Stack<TCallNode>();
                        MakeUseDefineChainSub(fld, defined_path, stack, MainCall);

                        TNode.NodeCnt = 0;
                        var vnd = (from x in defined_path select new TNode(x)).ToList();
                        foreach (TNode nd1 in vnd) {
                            TCallNode call = nd1.ObjNode as TCallNode;
                            foreach (TCallNode c in call.CallTo) {
                                var vnd2 = from x in vnd where x.ObjNode == c select x;
                                if (vnd2.Any()) {

                                    nd1.OutNodes.AddRange(vnd2);
                                }
                            }
                        }

                        if (vnd.Any()) {

                            string dot_path = string.Format("{0}\\{1}.{2}.dot", use_def_dir, cls.ClassName, fld.NameVar);

                            // ノードのリストからdotファイルを作る。
                            WriteDotFile("使用・定義連鎖", vnd, dot_path);
                        }
                    }
                }
            }
        }

        /*
        クラス図を作る。
        */
        public void MakeClassDiagram() {
            StringWriter sw = new StringWriter();

            sw.WriteLine("@startuml");
            sw.WriteLine("title <size:18>クラス図</size>");

            foreach(TType c in AppClasses) {
                if(c.KindClass != EType.Enum) {

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

        /*
        要約を作る。
        */
        public void MakeSummary() {
            foreach (TSourceFile src in SourceFiles) {
                Debug.WriteLine("{0} ------------------------------", src.PathSrc, "");

                foreach (TType cls in src.ClassesSrc) {
                    var vfnc = from x in src.FunctionsSrc
                               where x.DeclaringType == cls && x.KindFnc != EKind.Lambda && x.CommentVar != null && x.CommentVar.Length != 0 select x;

                    foreach (TFunction fnc in vfnc) {
                        var vc = from c in fnc.CommentVar where c.Kind == EKind.BlockCommentContinued && c.TextTkn.Trim() != "/*" select c;
                        if (vc.Any()) {

                            Debug.WriteLine("{0} {1}", fnc.NameVar, vc.First().TextTkn.Trim());
                        }
                    }
                }
            }
        }
    }

    /*
     * コールグラフのノード
     */
    public class TCallNode {
        public TType TypeCall;
        public TFunction FncCall;
        public List<TCallNode> CallTo = new List<TCallNode>();
        public List<TCallNode> CallFrom = new List<TCallNode>();
        public int MaxNest;
        public Color FontColor = Colors.Black;

        public TCallNode(TCallNode nd) {
            TypeCall = nd.TypeCall;
            FncCall = nd.FncCall;
            MaxNest = nd.MaxNest;
            FontColor = nd.FontColor;
        }

        public TCallNode(TType tp, TFunction fnc) {
            TypeCall = tp;
            FncCall = fnc;
        }

        public void AddCallToFrom(TCallNode call_to) {
            CallTo.Add(call_to);
            Debug.Assert(!call_to.CallFrom.Contains(this));
            call_to.CallFrom.Add(this);
        }

        public string Name() {
            if (FncCall.NameVar == null) {

                return "λ";
            }
            else {

                //return FncCall.NameVar;
                return TypeCall.GetClassText() + "->" + FncCall.FullName();
            }
        }

        /*
         * TCallNodeのリストのコピーを得る。
         */
        public static List<TCallNode> CopyCallNodes(IEnumerable<TCallNode> vsrc) {
            // コピー先のリスト
            List<TCallNode> vdst = new List<TCallNode>();

            // 変換辞書
            Dictionary<TCallNode, TCallNode> dic = new Dictionary<TCallNode, TCallNode>();

            // コピー元のノードに対し
            foreach (TCallNode src in vsrc) {
                // ノードをコピーする。
                TCallNode dst = new TCallNode(src);

                // コピー先のリストに追加する。
                vdst.Add(dst);

                // 変換辞書に登録する。
                dic.Add(src, dst);
            }

            // コピー元のノードに対し
            foreach (TCallNode src in vsrc) {
                // コピー先のノードを得る。
                TCallNode dst = dic[src];

                // 呼び出し先のリストのノードに対し
                foreach(TCallNode nd in src.CallTo) {
                    if (dic.ContainsKey(nd)) {

                        TCallNode call_to = dic[nd];

                        // 対応するノードを呼び出し先のリストに追加する。
                        dst.AddCallToFrom(call_to);
                    }
                }
            }

            return vdst;
        }
    }

    /*
     * 表示用ノード
     */
    public class TNode {
        public static int NodeCnt;
        public int IdxNode;
        public string NameNode;
        public object ObjNode;
        public int RankNode;
        public List<TNode> OutNodes = new List<TNode>();
        public Color FontColor = Colors.Black;

        public TNode(TCallNode call) {
            IdxNode = NodeCnt;
            NodeCnt++;

            NameNode = call.Name();
            ObjNode = call;
            RankNode = call.MaxNest;
            FontColor = call.FontColor;
        }
    }
}
