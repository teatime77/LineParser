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

    partial class TProject {
        public void MakeCallGraph() {
            TType tt = (from x in ClassTable.Values where x.ClassName == "TDotReference" select x).First();
            foreach(TType t in tt.ThisAncestorSuperClasses()) {
                Debug.WriteLine("super : {0}", t.ClassName, "");
            }

            TSetRefFnc set_ref_fnc = new TSetRefFnc();
            set_ref_fnc.ProjectNavi(this, null);

            TSetAppFnc set_app_fnc = new TSetAppFnc();
            set_app_fnc.ProjectNavi(this, null);

            var vcls = from x in ClassTable.Values where x.Info == null && !(x is TGenericClass) && x.SourceFileCls != null select x;
            var vfnc = from c in vcls from f in c.Functions select f;
            foreach(TFunction f in vfnc) {
                foreach(TReference ref1 in f.RefFnc) {
                    //Debug.WriteLine("{0} -> {1}", f.FullName(), ref1.NameRef);
                }
            }

            Dictionary<string, TFncCall> dic = new Dictionary<string, TFncCall>();

            TFunction main = (from f in vfnc where f.NameVar == "Main" select f).First();
           
            TFncCall main_call = CallGraphSub(dic, null, main.ClassMember, main);

            StringWriter sw = new StringWriter();
            Stack<TFncCall> stack = new Stack<TFncCall>();
            DmpFncCall(sw, stack, main_call, 0);

            string out_dir = ApplicationData.Current.LocalFolder.Path + "\\out";
            File.WriteAllText(out_dir + "\\DmpFncCall.txt", sw.ToString(), Encoding.UTF8);

            TNode.NodeCnt = 0;
            var vnd = (from x in dic.Values select new TNode(x)).ToList();
            foreach(TNode nd1 in vnd) {
                TFncCall call = nd1.ObjNode as TFncCall;
                foreach(TFncCall c in call.CallTo) {
                    TNode nd2 = (from x in vnd where x.ObjNode == c select x).First();
                    nd1.OutNodes.Add(nd2);
                }
            }

            WriteDotFile("コールグラフ", vnd, out_dir + "\\FncCall.dot");
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

                        TFunction f1 = tp2.GetVirtualFunction(ref1.VarRef as TFunction);
                        Debug.Assert(f1 != null);
                        CallGraphSub(dic, fnc_call, tp2, f1);
                    }
                    break;

                case EKind.NewInstance:
                    break;

                case EKind.base_:
                    break;
                }
            }

            var vlambda = from x in fnc.RefFnc where x.VarRef is TFunction && (x.VarRef as TFunction).KindFnc == EKind.Lambda select (x.VarRef as TFunction);
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
                sw.WriteLine("\tn{0} [shape = box, label = \"{1}\"];", nd1.IdxNode, nd1.NameNode);
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
    }

    public class TFncCall {
        public TType TypeCall;
        public TFunction FncCall;
        public List<TFncCall> CallTo = new List<TFncCall>();
        public int MaxNest;

        public TFncCall(TType tp, TFunction fnc) {
            TypeCall = tp;
            FncCall = fnc;
        }

        public string Name() {
            return TypeCall.GetClassText() + "->" + FncCall.FullName();
        }
    }

    public class TNode {
        public static int NodeCnt;
        public int IdxNode;
        public string NameNode;
        public object ObjNode;
        public int RankNode;
        public List<TNode> OutNodes = new List<TNode>();

        public TNode(TFncCall call) {
            IdxNode = NodeCnt;
            NodeCnt++;

            NameNode = call.Name();
            ObjNode = call;
            RankNode = call.MaxNest;
        }
    }
}
