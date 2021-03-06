﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Miyu {
    public delegate void NaviAction(object self);
    public delegate bool NaviFnc(object self, out object ret);

    //------------------------------------------------------------ TProject
    public partial class TProject {
        Number Zero = new Number(0);
        Number One = new Number(1);
        Variable AddFnc = new Variable("+", null, null);
        Variable SubFnc = new Variable("-", null, null);
        Variable MulFnc = new Variable("*", null, null);
        Variable DivFnc = new Variable("/", null, null);
        Variable σ_prime;
        Variable tanh_prime;

        public void DeepLearning() {

            var intArray = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            Debug.WriteLine( string.Join(", ", (from x in intArray select (x*10).ToString()) ));

            Debug.WriteLine("深層学習");

            TType layer = (from cls in AppClasses where cls.ClassName == "Layer" select cls).First();

            TType[] layers = (from cls in AppClasses where cls.IsSubClass(layer) select cls).ToArray();

            StringWriter sw = new StringWriter();

            σ_prime = ToVariable((from fnc in layer.Functions where fnc.NameVar == "σ_prime" select fnc).First(), null);
            tanh_prime = ToVariable((from fnc in layer.Functions where fnc.NameVar == "tanh_prime" select fnc).First(), null);

            // アプリのクラスの親クラスに対し
            foreach (TType cls in layers) {
                Debug.WriteLine("layer : {0}", cls.ClassName, "");

                TFunction fnc = (from f in cls.Functions where f.NameVar == "Forward" select f).First();

                Variable x_var = ToVariable((from f in cls.Fields where f.NameVar == "x" select f).First() , null);
                Variable y_var = ToVariable((from f in cls.Fields where f.NameVar == "y" select f).First(), null);
                Debug.Assert(x_var.TypeVar is ArrayType && y_var.TypeVar is ArrayType);

                Debug.Assert(fnc.BlockFnc.StatementsBlc.Count == 1 && fnc.BlockFnc.StatementsBlc[0] is TForEach);

                ForEach top_for = (ForEach)ToStatement(fnc.BlockFnc.StatementsBlc[0]);

                Variable t_var = null;
                if(top_for.LoopVariable.Name == "t") {
                    t_var = top_for.LoopVariable;
                }

                List<Term> all_terms = new List<Term>();
                List<Assignment> all_asns = new List<Assignment>();
                Navi(top_for, 
                    delegate (object obj) {
                        if(obj is Term) {
                            all_terms.Add(obj as Term);
                        }
                        else if (obj is Assignment) {
                            all_asns.Add(obj as Assignment);
                        }
                    });

                Reference[] all_refs = (from t in all_terms where t is Reference select t as Reference).ToArray();

                //------------------------------------------------------------ 順伝播
                sw.WriteLine("<hr/>");
                sw.WriteLine("<h4>{0}順伝播</h4>", cls.ClassName, "");
                sw.WriteLine("$$");
                sw.WriteLine(string.Join("\r\n \\\\ \r\n", from asn in all_asns select MathJax(asn.Left) + " = " + MathJax(asn.Right)));
                sw.WriteLine("$$");


                List<Reference> lefts = new List<Reference>();
//???????????                lefts.Add(x_var);

                // すべての代入文に対し
                foreach (Assignment asn in all_asns) {
                    Debug.WriteLine(asn.ToString());
                    Debug.Assert(asn.Left is Reference);

                    // 代入文の左辺の変数参照
                    Reference left = asn.Left as Reference;
                    Debug.Assert(left.Indexes != null);

                    // 左辺の変数参照の次元
                    int dim_cnt = left.Indexes.Length;

                    // 代入文の祖先のForEachのリスト
                    ForEach[] vfor = AncestorForEach(asn);
                    Debug.Assert(vfor.Length == dim_cnt);

                    // 左辺の変数参照の各添え字に対し
                    for (int dim = 0; dim < dim_cnt; dim++) {
                        if (left.Name == "a" && dim == 1) {

                            Debug.WriteLine("a[t, φ[t, n]] = (1 - u[t, φ[t, n]]) * Prod(from i in Range(n) select u[t, φ[t, i]]);");
                        }
                        else {

                            // 左辺の変数参照の添え字 = 代入文の祖先のForEachの変数
                            Debug.Assert(left.Indexes[dim] is Reference && (left.Indexes[dim] as Reference).VarRef == vfor[dim].LoopVariable);
                        }
                    }
                    lefts.Add(left);
                }

                // すべての代入文の左辺の変数参照に対し
                foreach (Reference left in lefts) {
                    List<Reference> prop = new List<Reference>();
                    List<Reference> propt1 = new List<Reference>();

                    // 変数を使用している変数参照に対し
                    foreach (Reference ref_use in (from r in all_refs where r.VarRef == left.VarRef && r != left && r.Indexes != null select r)) {

                        // 変数を使用している変数参照の親の文
                        Statement stmt = ParentStatement(ref_use);
                        if(stmt is Assignment) {
                            // 変数を使用している変数参照の親の文が代入文の場合

                            // 変数を使用している代入文
                            Assignment asn_use = stmt as Assignment;
                            Debug.Assert(asn_use.Left is Reference);

                            // 変数を使用している代入文の左辺の変数参照
                            Reference left_use = asn_use.Left as Reference;

                            // 変数を使用している代入文の祖先のForEachのリスト
                            ForEach[] vfor = AncestorForEach(asn_use);

                            // 変数参照の次元
                            int dim_cnt = ref_use.Indexes.Length;
                            int ref_linq = 0;
                            int t_1 = 0;
                            int plus_linq = 0;

                            // 変数参照の各添え字に対し
                            for (int dim = 0; dim < dim_cnt; dim++) {
                                if (ref_use.Indexes[dim] is Reference) {
                                    // 添え字が変数参照の場合

                                    // 変数参照の添え字の変数参照
                                    Reference idx_ref = ref_use.Indexes[dim] as Reference;
                                    if (idx_ref.Indexes == null) {
                                        // 変数参照の添え字が添え字を持たない場合

                                        if (idx_ref.VarRef.ParentVar is LINQ) {
                                            // 変数参照の添え字がLINQのループ変数の場合

                                            // 変数参照の添え字がループ変数のLINQ
                                            LINQ lnq = idx_ref.VarRef.ParentVar as LINQ;
                                            Debug.Assert(lnq.Aggregate != null);
                                            ref_linq++;

                                            if (lnq.Aggregate.Name == "Sum") {
                                                Debug.Write("");
                                            }
                                            else if (lnq.Aggregate.Name == "Prod") {
                                                Debug.Write("");
                                            }
                                            else if (lnq.Aggregate.Name == "Max") {
                                                Debug.Assert(false, "未実装");
                                            }
                                            else {
                                                Debug.Assert(false);
                                            }
                                        }
                                        else if(idx_ref.VarRef.ParentVar is ForEach){
                                            // 変数参照の添え字がForEachのループ変数の場合
                                        }
                                        else {
                                            // 変数参照の添え字がLINQやForEachのループ変数でない場合

                                            Debug.Assert(false);
                                        }
                                    }
                                    else {
                                    }
                                }
                                else {
                                    // 添え字が変数参照でない場合
                                    Debug.Assert(ref_use.Indexes[dim] is Apply);

                                    // 添え字の関数適用
                                    Apply app = ref_use.Indexes[dim] as Apply;
                                    Debug.Assert(app.Args.Length == 2 && app.Args[0] is Reference);

                                    // 添え字の関数適用の最初の引数
                                    Reference arg_ref1 = app.Args[0] as Reference;
                                    Debug.Assert(arg_ref1.VarRef == vfor[dim].LoopVariable);

                                    if (app.Function.Name == "-") {
                                        // 添え字が"-"式の場合

                                        Debug.Assert(app.Args[1] is Number);

                                        // 添え字の関数適用の2番目の引数
                                        Number n = app.Args[1] as Number;
                                        Debug.Assert(arg_ref1.VarRef == t_var && n.Value == 1);
                                        // t - 1
                                        t_1++;
                                    }
                                    else if (app.Function.Name == "+") {
                                        // 添え字が"+"式の場合

                                        Debug.Assert(app.Args[1] is Reference);

                                        // 添え字の関数適用の2番目の引数
                                        Reference arg_ref2 = app.Args[1] as Reference;
                                        Debug.Assert(dim < vfor.Length && arg_ref1.VarRef == vfor[dim].LoopVariable);
                                        Debug.Assert(arg_ref2.VarRef.ParentVar is LINQ);

                                        // 添え字の関数適用の2番目の引数はLINQのループ変数
                                        LINQ lnq = arg_ref2.VarRef.ParentVar as LINQ;

                                        if (lnq.Aggregate.Name == "Sum") {
                                            Debug.Write("");
                                        }
                                        else if (lnq.Aggregate.Name == "Max") {
                                            Debug.Write("");
                                        }
                                        else {
                                            Debug.Assert(false);
                                        }

                                        // i + p, j + q
                                        plus_linq++;
                                    }
                                    else {
                                        Debug.Assert(false);
                                    }
                                }
                            }
                            if (t_1 != 0 && ref_linq != 0) {

                            }
                            else if (ref_linq != 0 && plus_linq != 0 || plus_linq != 0 && t_1 != 0) {
                                Debug.Assert(false);
                            }

                            if (t_1 == 0) {
                                if (!(from r in prop where r.Eq(left_use) select r).Any()) {
                                    // 伝播先リストにない場合

                                    // 伝播先リストに追加する。
                                    prop.Add(left_use);
                                }
                            }
                            else {
                                if (!(from r in propt1 where r.Eq(left_use) select r).Any()) {
                                    // t-1の伝播先リストにない場合

                                    // t-1の伝播先リストに追加する。
                                    propt1.Add(left_use);
                                }
                            }
                        }
                    }

                    if(prop.Any() || propt1.Any()) {

                        // 右辺
                        Dictionary<Reference, Term> use_right = prop.ToDictionary(r => r, r => (r.Parent as Assignment).Right);

                        // 右辺を微分する。
                        //Dictionary<Reference, Term> use_right_diff = prop.ToDictionary(r => r, r => Differential(use_right[r], left, null));


                        // 左辺を+1する。
                        Dictionary<Reference, Reference> use_left_inc = propt1.ToDictionary(r => r, r => Tplus1(r, t_var, null) as Reference);

                        // 右辺を+1する。
                        Dictionary<Reference, Term> use_right_inc = propt1.ToDictionary(r => r, r => Tplus1((r.Parent as Assignment).Right, t_var, null));

                        // 右辺を+1して簡約化する。
                        Dictionary<Reference, Term> use_right_inc_simple = propt1.ToDictionary(r => r, r => SimplifyExpression(use_right_inc[r].Clone(null)));

                        // tとt+1の合併
                        Debug.Assert(! prop.Intersect(propt1).Any());
                        List<Reference> prop_union = prop.Union(propt1).ToList();

                        // tとt+1の左辺の合併
                        Dictionary<Reference, Reference> use_left_union = prop_union.ToDictionary(r => r, r => (prop.Contains(r) ? r : use_left_inc[r]));

                        // tとt+1の右辺の合併
                        Dictionary<Reference, Term> use_right_union = prop_union.ToDictionary(r => r, r => (prop.Contains(r) ? use_right[r] : use_right_inc_simple[r]));

                        // 右辺を微分する。
                        Dictionary<Reference, Term> use_right_diff = prop_union.ToDictionary(r => r, r => SetParent( Differential(use_right_union[r], left, null)));

                        // 右辺を微分して簡約化する。
                        Dictionary<Reference, Term> use_right_diff_simple = prop_union.ToDictionary(r => r, r => SimplifyExpression(use_right_diff[r].Clone(null)));


                        //------------------------------------------------------------ δE/δu = δE/δv1 * δv1/δu + ... δE/δvn * δvn/δu
                        sw.WriteLine("<hr/>");
                        sw.WriteLine("<h5>δE/δu = δE/δv1 * δv1/δu + ... δE/δvn * δvn/δu</h5>");
                        sw.WriteLine("$$");

                        sw.Write(@"\frac{{ \partial E }}{{ \partial {0} }} = ", MathJax(left), "");

                        sw.WriteLine(string.Join(" + ", from r in prop.Union(use_left_inc.Values)
                                                        select string.Format(@"\frac{{ \partial E }}{{ \partial {0} }} \cdot \frac{{ \partial {0} }}{{ \partial {1} }}",
                                                        MathJax(r), MathJax(left))));

                        sw.WriteLine("$$");

                        //------------------------------------------------------------  = δE/δv1 * δ式1/δu + ... δE/δvn * δ式n/δu
                        sw.WriteLine("<h5>= δE/δv1 * δ式1/δu + ... δE/δvn * δ式n/δu</h5>");
                        sw.WriteLine("$$");
                        sw.Write("= ");

                        sw.WriteLine(string.Join(@" \\ + ", from r in prop select string.Format(@"\delta^{{ {0} }} \cdot \frac{{ \partial ({1}) }}{{ \partial {2} }}",
                            MathJax(r), MathJax(use_right[r]), MathJax(left))));

                        if (prop.Any() && propt1.Any()) {
                            sw.WriteLine(" + ");
                        }

                        sw.WriteLine(string.Join(@" \\ + ", from r in propt1
                                                            select string.Format(@"\delta^{{ {0} }} \cdot \frac{{ \partial ({1}) }}{{ \partial {2} }}",
                                                            MathJax(use_left_inc[r]), MathJax(use_right_inc[r]), MathJax(left))));
                        sw.WriteLine("$$");

                        //------------------------------------------------------------  = δE/δv1 * δ式1/δu + ... δE/δvn * δ簡約式n/δu
                        sw.WriteLine("<h5>δE/δv1 * δ式1/δu + ... δE/δvn * δ簡約式n/δu</h5>");
                        sw.WriteLine("$$");
                        sw.Write("= ");

                        sw.WriteLine(string.Join(@" \\ + ", from r in prop
                                                            select string.Format(@"\delta^{{ {0} }} \cdot \frac{{ \partial ({1}) }}{{ \partial {2} }}",
                                                            MathJax(r), MathJax(use_right[r]), MathJax(left))));

                        if (prop.Any() && propt1.Any()) {
                            sw.WriteLine(" + ");
                        }

                        sw.WriteLine(string.Join(@" \\ + ", from r in propt1
                                                            select string.Format(@"\delta^{{ {0} }} \cdot \frac{{ \partial ({1}) }}{{ \partial {2} }}",
                                                            MathJax(use_left_inc[r]), MathJax(use_right_inc_simple[r]), MathJax(left))));
                        sw.WriteLine("$$");

                        //------------------------------------------------------------  = δE/δv1 * 微分1 + ... δE/δvn * 微分n
                        sw.WriteLine("<h5>= δE/δv1 * 微分1 + ... δE/δvn * 微分n</h5>");
                        sw.WriteLine("$$");
                        sw.Write("= ");

                        sw.WriteLine(string.Join(" \\\\ \r\n + ", from r in prop_union
                                                            select string.Format(@"\delta^{{ {0} }} \cdot ( {1} )",
                                                            MathJax(use_left_union[r]), MathJax(use_right_diff[r]))));

                        sw.WriteLine("$$");

                        //------------------------------------------------------------  = 簡約化(δE/δv1 * 微分1 + ... δE/δvn * 微分n)
                        sw.WriteLine("<h5>= 簡約化(δE/δv1 * 微分1 + ... δE/δvn * 微分n)</h5>");
                        sw.WriteLine("<div style='color:blue'>");
                        sw.WriteLine("$$");
                        sw.Write("= ");

                        sw.WriteLine(string.Join(" + ", from r in prop_union
                                                            select string.Format(@"\delta^{{ {0} }} \cdot {1}",
                                                            MathJax(use_left_union[r]), MathJax(use_right_diff_simple[r]))));

                        sw.WriteLine("$$");

                        Dictionary<Reference, Variable> delta_fnc = prop_union.ToDictionary(r => r, r => new Variable("δ_" + r.Name, null, null));

                        Term result = SimplifyExpression(Add((from r in prop_union
                            select Mul(new Apply(new Reference(delta_fnc[r]), (from i in r.Indexes select i.Clone(null)).ToArray()), use_right_diff_simple[r])).ToArray()).Clone(null));
                        
                        sw.WriteLine("<pre><b>");
                        sw.WriteLine("double δ_" + left.Name + "(" + string.Join(", ", from i in left.Indexes select "int " + (i as Reference).Name) + "){");
                        sw.WriteLine("\treturn " + result.ToString() + ";");
                        sw.WriteLine("}");
                        sw.WriteLine("</b></pre>");

                        sw.WriteLine("</div>");
                    }

                    Debug.WriteLine(left.ToString() + " : " + string.Join(" ", from v in prop select v.Name) + " t-1:" + string.Join(" ", from v in propt1 select v.Name));
                }

                foreach (Assignment asn in all_asns) {
                    sw.WriteLine("$$");
                    sw.WriteLine("{0} = {1}", MathJax(asn.Left), MathJax(asn.Right));
                    sw.WriteLine("$$");

                    if(t_var != null && (from r in Refs(asn) where r.VarRef == t_var select r).Any()) {

                        Dictionary<Variable, Variable> var_tbl = new Dictionary<Variable, Variable>();

                        sw.WriteLine("<div style='color:red'>");
                        sw.WriteLine("$$");
                        sw.WriteLine("{0} = {1}", MathJax(Tplus1(asn.Left, t_var, var_tbl)), MathJax(Tplus1(asn.Right, t_var, var_tbl)));
                        sw.WriteLine("$$");
                        sw.WriteLine("</div>");
                    }
                }
            }

            WriteMathJax(sw);
        }

        void WriteMathJax(StringWriter sw) {
/*

*/

            string head = @"<!DOCTYPE html>

<html lang=""en"" xmlns=""http://www.w3.org/1999/xhtml"">
<head>
    <meta charset=""utf-8"" />
    <title>MyML</title>
    <script type=""text/x-mathjax-config"">
      MathJax.Hub.Config({
        extensions: [""tex2jax.js""],
        jax: [""input/TeX"",""output/HTML-CSS""],
        tex2jax: {inlineMath: [[""$"",""$""],[""\\("",""\\)""]]}
      });
    </script>

    <script type=""text/javascript"" src=""MathJax/MathJax.js"" ></script>
</head>
<body>
";
            File.WriteAllText(HomeDir + "\\html\\MathJax.html", head + sw.ToString() + "\r\n</body></html>", Encoding.UTF8);
        }

        /*
            t - 1 => t
            t => t + 1
        */
        Term Tplus1(Term t1, Variable t_var, Dictionary<Variable, Variable> var_tbl) {
            if (var_tbl == null) {
                var_tbl = new Dictionary<Variable, Variable>();
            }

            if (t1 is Reference) {
                Reference r1 = t1 as Reference;
                if(r1.VarRef == t_var) {

                    Debug.Assert(r1.Indexes == null);

                    return new Apply(new Reference(AddFnc), new Term[] { r1.Clone(var_tbl), new Number(1) });
                }
                if (r1.Indexes == null) {
                    return r1.Clone(var_tbl);
                }
                Term[] idx = (from t in r1.Indexes select Tplus1(t, t_var, var_tbl)).ToArray();

                Variable v1;
                if (!var_tbl.TryGetValue(r1.VarRef, out v1)) {
                    v1 = r1.VarRef;
                }

                return new Reference(r1.Name, v1, idx);
            }
            else if (t1 is Number) {
                return t1.Clone(var_tbl);
            }
            else if (t1 is Apply) {
                Apply app = t1 as Apply;
                Term[] args = (from t in app.Args select Tplus1(t, t_var, var_tbl)).ToArray();
                return new Apply(Tplus1(app.Function, t_var, var_tbl) as Reference, args);
            }
            else if (t1 is LINQ) {
                LINQ lnq = t1 as LINQ;
                Variable[] vars = (from v in lnq.Variables select Tplus1Var(v, t_var, var_tbl)).ToArray();
                return new LINQ(vars, Tplus1(lnq.Select, t_var, var_tbl), (lnq.Aggregate == null ? null : Tplus1(lnq.Aggregate, t_var, var_tbl) as Reference));
            }
            else {
                Debug.Assert(false);
            }

            return null;
        }

        Variable Tplus1Var(Variable v, Variable t_var, Dictionary<Variable, Variable> var_tbl) {
            Term domain = (v.Domain == null ? null : Tplus1(v.Domain, t_var, var_tbl));
            Variable v1 = new Variable(v.Name, v.TypeVar, domain);
            var_tbl.Add(v, v1);

            return v1;
        }

        Apply Mul(params Term[] args) {
            return new Apply(new Reference(MulFnc), args);
        }

        Apply Add(params Term[] args) {
            return new Apply(new Reference(AddFnc), args);
        }

        Apply Sub(params Term[] args) {
            return new Apply(new Reference(SubFnc), args);
        }

        public int[] Range(int n) {
            int[] v = new int[n];

            for (int i = 0; i < n; i++) {
                v[i] = i;
            }

            return v;
        }

        /*
            微分
        */
        Term DifferentialLINQ(LINQ lnq, Reference r1, Dictionary<Variable, Variable> var_tbl) {
            Debug.Assert(lnq.Aggregate != null);

            Dictionary<Reference, Dictionary<Variable, Term>> rs = new Dictionary<Reference, Dictionary<Variable, Term>>();
            bool exact = false;
            Navi(lnq.Select,
                delegate (object obj) {
                    if (obj is Reference) {
                        Reference r2 = obj as Reference;

                        if (!(from r in rs.Keys where r.Eq(r2) select r).Any()) {
                                // 処理済みでない場合

                                if (r1.VarRef == r2.VarRef) {
                                    // 同じ変数を参照する場合

                                    if (r1.Eq(obj)) {
                                        // 一致する場合

                                        exact = true;
                                }
                                else {
                                        // 一致しない添え字がある場合

                                        Dictionary<Variable, Term> pairs = new Dictionary<Variable, Term>();
                                    bool ok = true;
                                    for (int i = 0; i < r1.Indexes.Length; i++) {
                                        if (!r1.Indexes[i].Eq(r2.Indexes[i])) {
                                                // 添え字が一致しない場合

                                                if (!(r2.Indexes[i] is Reference)) {
                                                    // 代入候補の変数参照の添え字が変数参照でない場合

                                                    ok = false;
                                                break;
                                            }
                                            else {
                                                    // 両方の添え字が変数参照の場合

                                                    Reference r3 = r2.Indexes[i] as Reference;
                                                IEnumerable<Variable> linq_eq_vars = from v in lnq.Variables where v == r3.VarRef select v;
                                                if (linq_eq_vars.Any()) {
                                                        // LINQの変数の場合

                                                        Variable v = linq_eq_vars.First();
                                                    Debug.Assert(!pairs.ContainsKey(v));
                                                    pairs.Add(v, r1.Indexes[i]);
                                                }
                                                else {
                                                        // LINQの変数でない場合

                                                        ok = false;
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                    if (ok) {
                                        rs.Add(r2, pairs);
                                    }
                                }
                            }
                        }
                    }
                });

            Debug.Assert(!(exact && rs.Any()), "完全一致の変数参照と代入で一致の変数参照の両方がある場合は未対応");
            if (!(exact || rs.Any())) {
                // 完全一致や代入で一致の変数参照がない場合

                return Zero;
            }

            // LINQをコピーする。
            LINQ lnq1 = lnq.Clone(var_tbl);

            if (exact) {
                // 完全一致の変数参照がある場合

                // select句を微分する。
                Term dif1 = Differential(lnq1.Select, r1, var_tbl);

                if (lnq.Aggregate.Name == "Sum") {
                    return lnq1;
                }
                else if (lnq.Aggregate.Name == "Prod") {
                    Debug.Write("の微分は未実装");
                    return lnq1;
                }
                if (lnq.Aggregate.Name == "Max") {
                    Debug.Write("の微分は未実装");
                    return lnq1;
                }
                else {
                    Debug.Assert(false);
                }
            }
            else {
                // 代入で一致の変数参照がある場合

                Debug.Assert(rs.Keys.Count == 1, "代入で一致の変数参照は1種類のみ実装");
                Dictionary<Variable, Term> subst_tbl = rs.First().Value;
                Debug.Assert(subst_tbl.Count == lnq.Variables.Length, "LINQの全変数に代入する。");

                // LINQのselect句の変数参照に代入する。
                Term subst_sel = Subst(lnq1.Select, subst_tbl, var_tbl);

                // LINQの変数に代入をしたselect句を微分する。
                Term dif1 = Differential(subst_sel, r1, var_tbl);

                if (lnq.Aggregate.Name == "Sum") {
                    return dif1;
                }
                else if (lnq.Aggregate.Name == "Prod") {
                    Debug.Write("の微分は未実装");
                    return dif1;
                }
                if (lnq.Aggregate.Name == "Max") {
                    Debug.Write("の微分は未実装");
                    return lnq1;
                }
                else {
                    Debug.Assert(false);
                }
            }

            return null;
        }

        /*
            微分
        */
        Term Differential(Term t1, Reference r1, Dictionary<Variable, Variable> var_tbl_up) {
            if (t1 is Reference) {
                // 変数参照の場合

                if (t1.Eq(r1)) {
                    return One;
                }
                else {
                    return Zero;
                }
            }
            else if (t1 is Number) {
                // 数値の場合

                return Zero;
            }

            Dictionary<Variable, Variable> var_tbl = (var_tbl_up == null ? new Dictionary<Variable, Variable>() : new Dictionary<Variable, Variable>(var_tbl_up));
            if (t1 is Apply) {
                // 関数適用の場合

                Apply app = t1 as Apply;

                Term[] diffs = (from t in app.Args select Differential(t, r1, var_tbl)).ToArray();

                if(app.Function.VarRef == AddFnc) {
                    // 加算の場合

                    return Add(diffs);
                }
                else if (app.Function.VarRef == SubFnc) {
                    // 減算の場合

                    return Sub(diffs);
                }
                else if (app.Function.VarRef == MulFnc) {
                    // 乗算の場合

                    Term[] args = new Term[app.Args.Length];
                    foreach(int i in Range(app.Args.Length)) {
                        args[i] = Mul((from j in Range(app.Args.Length) select (i == j ? diffs[i] : app.Args[j].Clone(var_tbl))).ToArray());
                    }

                    return Add(args);
                }
                else if (app.Function.Name == "σ") {

                    Term[] args = (from t in app.Args select t.Clone(var_tbl)).ToArray();
                    return Mul(new Apply(new Reference(σ_prime), args), diffs[0]);
                }
                else if (app.Function.Name == "tanh") {

                    Term[] args = (from t in app.Args select t.Clone(var_tbl)).ToArray();
                    return Mul(new Apply(new Reference(tanh_prime), args), diffs[0]);
                }
                else {
                    Debug.Write("");
                }
            }
            else if (t1 is LINQ) {
                return DifferentialLINQ(t1 as LINQ, r1, var_tbl);
            }
            else {
                Debug.Assert(false);
            }
            
            return null;
        }

        Term Subst(Term t1, Dictionary<Variable, Term> subst_tbl, Dictionary<Variable, Variable> var_tbl) {
            return NaviRep(t1,
                delegate (object obj, out object ret) {
                    ret = obj;
                    if (obj is Reference) {
                        Reference r2 = obj as Reference;

                        Term t2;
                        if(subst_tbl.TryGetValue(r2.VarRef, out t2)) {
                            Term t3 = t2.Clone(var_tbl);
                            t3.Parent = r2.Parent;
                            ret = t3;
                            return true;
                        }
                    }

                    return false;
                }) as Term;
        }

        Term SetParent(Term t1) {
            return NaviRep(t1,
                delegate (object obj, out object ret) {
                    ret = obj;

                    return false;
                }) as Term;

        }

        Term SimplifyExpression(Term t1) {
            return NaviRep(t1,
                delegate (object obj, out object ret) {
                    ret = obj;

                    if (obj is Apply) {
                        Apply app = obj as Apply;

                        Term[] args1 = (from t in app.Args select SimplifyExpression(t)).ToArray();

                        List<Term> args2 = new List<Term>();

                        if (app.Function.VarRef == SubFnc && args1[0] is Apply && (args1[0] as Apply).Function.VarRef == AddFnc && args1[1] is Number) {
                            // (t + 1) - 1

                            Debug.Assert(args1.Length == 2);

                            Apply app3 = args1[0] as Apply;
                            Number n = args1[1] as Number;

                            app.Function = app3.Function;
                            List<Term> v = new List<Term>(app3.Args);
                            v.Add(new Number(- n.Value));
                            args1 = v.ToArray();
                        }
                        /*
                        */

                        if (app.Function.VarRef == AddFnc || app.Function.VarRef == MulFnc) {
                            foreach(Term t in args1) {
                                if(t is Apply && (t as Apply).Function.VarRef == app.Function.VarRef) {
                                    args2.AddRange((t as Apply).Args);
                                }
                                else if(! (t is Number)) {
                                    args2.Add(t);
                                }
                            }

                            Number[] ns = (from t in args1 where t is Number select t as Number).ToArray();
                            if (ns.Any()) {
                                if (app.Function.VarRef == AddFnc) {

                                    double d = (from x in ns select x.Value).Sum();
                                    if (d != 0) {

                                        args2.Add(new Number(d));
                                    }
                                }
                                else {

                                    double d = (from x in ns select x.Value).Aggregate((x,y) => x * y);
                                    if(d == 0) {

                                        ret = Zero;

                                        return true;
                                    }
                                    else if (d != 1) {

                                        args2.Insert(0, new Number(d));
                                    }
                                }
                            }

                            switch (args2.Count) {
                            case 0:
                                if (app.Function.VarRef == AddFnc) {

                                    ret = Zero;
                                }
                                else if (app.Function.VarRef == MulFnc) {

                                    ret = One;
                                }
                                else {

                                    Debug.Assert(false);
                                }
                                return true;

                            case 1:
                                args2[0].Parent = app.Parent;
                                ret = args2[0];

                                return true;
                            }
                        }
                        else {
                            args2 = new List<Term>(args1);
                        }

                        Dictionary<Variable, Variable> var_tbl = new Dictionary<Variable, Variable>();
                        Apply app2 = new Apply(app.Function.Clone(var_tbl), args2.ToArray());
                        app2.Parent = app.Parent;
                        ret = app2;

                        return true;
                    }

                    return false;
                }) as Term;
        }

        /*
            誤差逆伝播法
        */
        Term Backpropagation(Term t1, Variable v) {
            return null;
        }

        string MathJax(Term t1) {
            if (t1 is Reference) {
                Reference r1 = t1 as Reference;
                if(r1.Indexes == null) {
                    return r1.Name;
                }
                else {

                    switch (r1.Indexes.Length) {
                    case 1:
                        return r1.Name + "_{" + MathJax(r1.Indexes[0]) + "}";
                    case 2:
                        return r1.Name + "_{" + MathJax(r1.Indexes[0]) + "}^{" + MathJax(r1.Indexes[1]) + "}";
                    case 3:
                        return r1.Name + "_{" + MathJax(r1.Indexes[0]) + "}^{" + MathJax(r1.Indexes[1]) + ", " + MathJax(r1.Indexes[2]) + "}";
                    }
                    return r1.Name + "[" + string.Join(", ", from x in r1.Indexes select MathJax(x)) + "]";
                }
            }
            else if (t1 is Number) {
                Number n = t1 as Number;

                return n.Value.ToString();
            }
            else if (t1 is Apply) {
                Apply app = t1 as Apply;

                if ("+-*/%".Contains(app.Function.Name[0])) {
                    string s;

                    if (app.Args.Length == 1) {

                        s = app.Function.Name + " " + MathJax(app.Args[0]);
                    }
                    else {

                        if(app.Function.VarRef == MulFnc) {

                            s = string.Join(@" \cdot ", from x in app.Args select MathJax(x));
                        }
                        else {

                            s = string.Join(" " + app.Function.Name + " ", from x in app.Args select MathJax(x));
                        }
                    }
                    if(s.IndexOf("0 + 0 + [0 \\cdot s_{t}^{j} + wI_{j} \\cdot 1] + 0") != -1) {
                        //MathJax(t1);
                    }

                    if (app.Parent is Apply && (app.Parent as Apply).Precedence() <= app.Precedence()) {
                        return "(" + s + ")";
                    }
                    else {
                        return s;
                    }
                }
                else {
                    string name = app.Function.Name;
                    if(app.Function.VarRef == σ_prime) {
                        name = "σ'";
                    }
                    if (app.Function.VarRef == tanh_prime) {
                        name = "tanh'";
                    }
                    return name + "(" + string.Join(", ", from x in app.Args select MathJax(x)) + ")";
                }
            }
            else if (t1 is LINQ) {
                
                LINQ lnq = t1 as LINQ;

                string s = "";

                var vv = from v in lnq.Variables where v.Domain is Apply && (v.Domain as Apply).Function.Name == "Range" select (v.Domain as Apply).Args[0];
                Debug.Assert(vv.Count() == lnq.Variables.Count());
                Dictionary<Variable, Term> doms = lnq.Variables.ToDictionary(v => v, v => (v.Domain as Apply).Args[0]);

                if (lnq.Aggregate.Name == "Sum") {

                    foreach (Variable v in lnq.Variables) {
                        s += @"\displaystyle \sum_{" + v.Name + " }^{ " + MathJax(doms[v]) + " } ";
                    }
                }
                else if (lnq.Aggregate.Name == "Prod") {

                    foreach (Variable v in lnq.Variables) {
                        s += "\\prod_{" + v.Name + " }^{ " + MathJax(doms[v]) + " } ";
                    }
                }
                else if (lnq.Aggregate.Name == "Max") {

                    foreach (Variable v in lnq.Variables) {
                        s += "Max_{" + v.Name + " }^{ " + MathJax(doms[v]) + " } ";
                    }
                }
                else {
                    Debug.Assert(false);
                }
                s += MathJax(lnq.Select);

                return s;              
            }
            else {
                Debug.Assert(false);
            }

            return null;
        }

        Term[] AllTerms(object root) {
            List<Term> v = new List<Term>();
            Navi(root,
                delegate (object obj) {
                    if (obj is Term) {
                        v.Add(obj as Term);
                    }
                });
            return v.ToArray();
        }

        Reference[] Refs(object root) {
            List<Reference> v = new List<Reference>();
            Navi(root,
                delegate (object obj) {
                    if (obj is Reference) {
                        v.Add(obj as Reference);
                    }
                });
            return v.ToArray();
        }

        Statement ParentStatement(Term t1) {
            for (Object obj = t1.Parent; ; ) {
                Debug.Assert(obj != null);

                if (obj is Statement) {
                    return obj as Statement;
                }
                else if (obj is Term) {
                    obj = (obj as Term).Parent;
                }
                else if (obj is Variable) {
                    obj = (obj as Variable).ParentVar;
                }
                else {
                    Debug.Assert(false);
                }
            }
        }

        ForEach[] AncestorForEach(Object o) {
            Statement stmt;

            if(o is Statement) {
                stmt = o as Statement;
            }
            else {
                Debug.Assert(o is Term);
                stmt = ParentStatement(o as Term);
            }

            List<ForEach> vfor = new List<ForEach>();
            Debug.Assert(stmt.ParentStmt is ForEach);

            ForEach for1 = stmt.ParentStmt as ForEach;
            vfor.Add(for1);
            while (for1.ParentStmt is ForEach) {
                for1 = for1.ParentStmt as ForEach;
                vfor.Add(for1);
            }
            Debug.Assert(for1.ParentStmt == null);

            vfor.Reverse();
            return vfor.ToArray();
        }

        List<object> Ancestors(List<Term> terms) {
            HashSet<Term> pending = new HashSet<Term>(terms);
            HashSet<object> processed = new HashSet<object>();

            while (pending.Any()) {
                HashSet<object> tmp = new HashSet<object>(from x in pending where x.Parent != null select x.Parent);

                foreach(object t in pending) {
                    processed.Add(t);
                }

                pending = new HashSet<Term>(from t in tmp where t is Term && ! processed.Contains(t) select (Term)t);
            }

            return new List<object>(processed);
        }

        Dictionary<string, Class> ClassTbl = new Dictionary<string, Class>();

        Class ToType(TType type) {
            Class cls;

            switch (type.GenericType) {
            case EClass.SimpleClass:

                if(ClassTbl.TryGetValue(type.ClassName, out cls)) {
                    return cls;
                }
                cls = new SimpleType(type.ClassName);
                ClassTbl.Add(cls.Name, cls);
                return cls;


            case EClass.SpecializedClass:
                Debug.Assert(type is TGenericClass && type.ClassName == "Array");
                TGenericClass gc = type as TGenericClass;
                Class element_type = ToType(gc.ArgClasses[0]);

                string key = string.Format("{0}[{1}]", element_type.Name, new string(',', gc.DimCnt - 1));

                if (ClassTbl.TryGetValue(key, out cls)) {
                    return cls;
                }
                cls = new ArrayType(element_type, gc.DimCnt);
                ClassTbl.Add(key, cls);
                return cls;

            default:
                Debug.Assert(false);
                return null;
            }
        }

        Dictionary<TVariable, Variable> VarTbl = new Dictionary<TVariable, Variable>();

        Variable ToVariable(TVariable v1, Term domain) {
            Variable v2;

            if(VarTbl.TryGetValue(v1, out v2)) {

                if(domain != null) {
                    v2.Domain = domain;
                    v2.Domain.Parent = v2;
                }
                return v2;
            }

            v2 = new Variable(v1.NameVar, ToType(v1.TypeVar), domain);
            VarTbl.Add(v1, v2);
            return v2;
        }

        Statement ToStatement(TStatement stmt1) {
            if(stmt1 is TAssignment) {
                TAssignment asn = stmt1 as TAssignment;

                return new Assignment(ToTerm(asn.RelAsn.Args[0]), ToTerm(asn.RelAsn.Args[1]));
            }
            else if(stmt1 is TForEach) {
                TForEach for1 = stmt1 as TForEach;
                List<Statement> stmts = new List<Statement>();

                foreach(TStatement s in for1.StatementsBlc) {
                    stmts.Add(ToStatement(s));
                }

                return new ForEach(ToVariable(for1.LoopVariable, ToTerm(for1.ListFor)), stmts.ToArray());
            }
            Debug.Assert(false);
            return null;
        }

        Term ToTerm(TTerm t1) {
            if(t1 is TReference) {
                TReference r = t1 as TReference;
                return new Reference(r.NameRef, ToVariable(r.VarRef, null), null);
            }
            else if(t1 is TLiteral) {
                TLiteral l1 = t1 as TLiteral;
                double d;
                if(double.TryParse(l1.TokenTrm.TextTkn, out d)) {
                    return new Number(d);
                }
                else {
                    Debug.Assert(false);
                }
            }
            else if (t1 is TDotApply) {
                TDotApply dotapp = t1 as TDotApply;
                Debug.Assert(dotapp.DotApp is TFrom && dotapp.FunctionApp is TReference);
                TFrom frm = dotapp.DotApp as TFrom;
                TReference fnc = dotapp.FunctionApp as TReference;
                Debug.Assert(fnc.NameRef == "Sum" || fnc.NameRef == "Max");

                List<Variable> vars = new List<Variable>();
                Reference aggregate = new Reference(fnc.NameRef, ToVariable(fnc.VarRef, null), null);
                while (true) {
                    vars.Add(ToVariable(frm.VarQry, ToTerm(frm.SeqQry)));
                    if(frm.InnerFrom != null) {

                        Debug.Assert(frm.SelFrom == null);
                        frm = frm.InnerFrom;
                    }
                    else {
                        Debug.Assert(frm.SelFrom != null);

                        return new LINQ(vars.ToArray(), ToTerm(frm.SelFrom), aggregate);
                    }
                }
            }
            else if (t1 is TApply) {
                TApply app = t1 as TApply;
                Term[] args = new Term[app.Args.Length];
                for(int i = 0; i < app.Args.Length; i++) {
                    args[i] = ToTerm(app.Args[i]);
                }

                switch (app.KindApp) {
                case EKind.Index:
                    TReference r = app.FunctionApp as TReference;

                    return new Reference(r.NameRef, ToVariable(r.VarRef, null), args);

                case EKind.Add:
                    return new Apply(new Reference(AddFnc), args);
                case EKind.Sub:
                    return new Apply(new Reference(SubFnc), args);
                case EKind.Mul:
                    return new Apply(new Reference(MulFnc), args);
                case EKind.Div:
                    return new Apply(new Reference(DivFnc), args);

                case EKind.FunctionApply:
                    Debug.Assert(app.FunctionApp is TReference);
                    TReference fnc_ref = app.FunctionApp as TReference;
                    if(fnc_ref.NameRef == "Prod") {
                        Debug.Assert(args.Length == 1 && args[0] is LINQ);
                        LINQ lnq = args[0] as LINQ;
                        Debug.Assert(lnq.Aggregate == null);
                        lnq.Aggregate = new Reference(fnc_ref.NameRef, ToVariable(fnc_ref.VarRef, null), null);
                        lnq.Aggregate.Parent = lnq;
                        return lnq;
                    }
                    return new Apply((Reference)ToTerm(fnc_ref), args);

                default:
                    return null;
                }
            }
            else if(t1 is TFrom) {
                TFrom frm = t1 as TFrom;
                Debug.Assert(frm.CndQry == null);

                return new LINQ(new Variable[] { ToVariable(frm.VarQry, ToTerm(frm.SeqQry)) }, ToTerm(frm.SelFrom), null);
            }

            return null;
        }

        void Navi(object obj, NaviAction before, NaviAction after = null) {
            if (obj == null) {
                return;
            }

            before(obj);

            if (obj is Term) {

                if (obj is Reference) {
                    Reference r1 = obj as Reference;

                    if(r1.Indexes != null) {
                        foreach(Term t in r1.Indexes) {
                            Navi(t, before, after);
                        }
                    }
                }
                else if (obj is Number) {
                }
                else if (obj is Apply) {
                    Apply app = obj as Apply;
                    Navi(app.Function, before, after);
                    foreach (Term t in app.Args) {
                        Navi(t, before, after);
                    }
                }
                else if (obj is LINQ) {
                    LINQ lnq = obj as LINQ;
                    foreach (Variable v in lnq.Variables) {
                        Navi(v, before, after);
                    }
                    Navi(lnq.Select, before, after);
                    Navi(lnq.Aggregate, before, after);
                }
                else {
                    Debug.Assert(false);
                }
            }
            else if (obj is Variable) {
                Variable v = obj as Variable;

                Navi(v.Domain, before, after);
            }
            else if (obj is Statement) {
                if (obj is Assignment) {
                    Assignment asn = obj as Assignment;
                    Navi(asn.Left, before, after);
                    Navi(asn.Right, before, after);
                }
                else if (obj is ForEach) {
                    ForEach for1 = obj as ForEach;
                    Navi(for1.LoopVariable, before, after);
                    foreach (Statement s in for1.Statements) {
                        Navi(s, before, after);
                    }
                }
                else {
                    Debug.Assert(false);
                }
            }
            else {
                Debug.Assert(false);
            }

            if(after != null) {

                after(obj);
            }
        }

        object NaviRep(object obj, NaviFnc before, NaviFnc after = null) {
            if (obj == null) {
                return null;
            }

            object ret;
            bool done = before(obj, out ret);
            if (done) {

                return ret;
            }

            if (obj is Term) {

                if (obj is Reference) {
                    Reference r1 = obj as Reference;

                    if (r1.Indexes != null) {
                        r1.Indexes = (from t in r1.Indexes select NaviRep(t, before, after) as Term).ToArray();
                        
                        foreach(Term t in r1.Indexes) {
                            t.Parent = obj;
                        }
                    }
                }
                else if (obj is Number) {
                }
                else if (obj is Apply) {
                    Apply app = obj as Apply;
                    app.Function = NaviRep(app.Function, before, after) as Reference;
                    app.Args = (from t in app.Args select NaviRep(t, before, after) as Term).ToArray();

                    app.Function.Parent = app;
                    foreach (Term t in app.Args) {
                        t.Parent = obj;
                    }
                }
                else if (obj is LINQ) {
                    LINQ lnq = obj as LINQ;
                    lnq.Variables = (from v in lnq.Variables select NaviRep(v, before, after) as Variable).ToArray();
                    lnq.Select = NaviRep(lnq.Select, before, after) as Term;
                    lnq.Aggregate = NaviRep(lnq.Aggregate, before, after) as Reference;

                    foreach(Variable v in lnq.Variables) {
                        v.ParentVar = obj;
                    }
                    lnq.Select.Parent = obj;
                    if (lnq.Aggregate != null) {
                        lnq.Aggregate.Parent = obj;
                    }
                }
                else {
                    Debug.Assert(false);
                }
            }
            else if (obj is Variable) {
                Variable v = obj as Variable;

                v.Domain = NaviRep(v.Domain, before, after) as Term;
                if(v.Domain != null) {
                    v.Domain.Parent = obj;
                }
            }
            else if (obj is Statement) {
                if (obj is Assignment) {
                    Assignment asn = obj as Assignment;
                    asn.Left = NaviRep(asn.Left, before, after) as Term;
                    asn.Right = NaviRep(asn.Right, before, after) as Term;

                    asn.Left.Parent = obj;
                    asn.Right.Parent = obj;
                }
                else if (obj is ForEach) {
                    ForEach for1 = obj as ForEach;
                    for1.LoopVariable = NaviRep(for1.LoopVariable, before, after) as Variable;
                    for1.Statements = (from s in for1.Statements select NaviRep(s, before, after) as Statement).ToArray();

                    for1.LoopVariable.ParentVar = obj;
                    foreach(Statement stmt in for1.Statements) {
                        stmt.ParentStmt = obj;
                    }
                }
                else {
                    Debug.Assert(false);
                }
            }
            else {
                Debug.Assert(false);
            }

            if (after != null) {

                after(obj, out ret);
            }

            return ret;
        }      
    }

    public class SubstPair {
        public Variable VarPair;
        public Term TermPair;

        public SubstPair(Variable var, Term term) {
            VarPair = var;
            TermPair = term;
        }
    }


    public class Class {
        public string Name;
    }

    public class SimpleType : Class {
        public SimpleType(string name) {
            Name = name;
        }
    }

    public class ArrayType : Class {
        public Class ElementType;
        public int DimCnt;

        public ArrayType(Class element_type, int dim_cnt) {
            ElementType = element_type;
            DimCnt = dim_cnt;
        }
    }

    public class Variable {
        public object ParentVar;
        public string Name;
        public Class TypeVar;
        public Term Domain;

        public Variable(string name, Class type, Term domain) {
            Name = name;
            TypeVar = type;
            Domain = domain;

            if(Domain != null) {
                Domain.Parent = this;
            }
        }

        public Variable Clone(Dictionary<Variable, Variable> var_tbl) {
            Term domain = (Domain == null ? null : Domain.Clone(var_tbl));
            Variable v1 = new Variable(Name, TypeVar, domain);
            var_tbl.Add(this, v1);

            return v1;
        }
    }

    public class Statement {
        public object ParentStmt;
    }

    public class Assignment : Statement {
        public Term Left;
        public Term Right;

        public Assignment(Term left, Term right) {
            Left = left;
            Right = right;

            Left.Parent = this;
            Right.Parent = this;
        }

        public override string ToString() {
            return Left.ToString() + " = " + Right.ToString();
        }
    }

    public class ForEach : Statement {
        public Variable LoopVariable;
        public Statement[] Statements;

        public ForEach(Variable variable, Statement[] statements) {
            LoopVariable = variable;
            Statements = statements;

            LoopVariable.ParentVar = this;
            foreach(Statement s in Statements) {
                s.ParentStmt = this;
            }
        }
    }

    public abstract class Term {
        public object   Parent;

        public virtual bool Eq(Object obj) {
            return false;
        }

        public Term Clone(Dictionary<Variable, Variable> var_tbl) {
            if(var_tbl == null) {
                var_tbl = new Dictionary<Variable, Variable>();
            }

            if (this is Reference) {
                return (this as Reference).Clone(var_tbl);
            }
            else if (this is Number) {
                return (this as Number).Clone(var_tbl);
            }
            else if (this is Apply) {
                return (this as Apply).Clone(var_tbl);
            }
            else if (this is LINQ) {
                return (this as LINQ).Clone(var_tbl);
            }
            else {
                Debug.Assert(false);
                return null;
            }
        }
    }

    public class Number : Term {
        public double Value;

        public Number(double d) {
            Value = d;
        }

        public new Number Clone(Dictionary<Variable, Variable> var_tbl) {
            return new Number(Value);
        }

        public override bool Eq(Object obj) {
            if (!(obj is Number)) {
                return false;
            }
            return Value == (obj as Number).Value;
        }

        public override string ToString() {
            return Value.ToString();
        }
    }

    public class Reference : Term {
        public string Name;
        public Variable VarRef;
        public Term[] Indexes;

        public Reference(string name, Variable ref_var, Term[] idx) {
            Name = name;
            VarRef = ref_var;
            Indexes = idx;

            if (Indexes != null) {
                foreach(Term t in Indexes) {
                    t.Parent = this;
                }
            }
        }

        public Reference(Variable v) {
            Name = v.Name;
            VarRef = v;
            Indexes = null;
        }

        public new Reference Clone(Dictionary<Variable, Variable> var_tbl) {
            if(var_tbl == null) {
                var_tbl = new Dictionary<Variable, Variable>();
            }
            Variable v1;
            if(! var_tbl.TryGetValue(VarRef, out v1)) {
                v1 = VarRef;
            }

            if(Indexes == null) {
                return new Reference(Name, v1, null);
            }

            Term[] idx = (from t in Indexes select t.Clone(var_tbl)).ToArray();

            return new Reference(Name, v1, idx);
        }

        public override bool Eq(Object obj) {
            if(!(obj is Reference)) {
                return false;
            }
            Reference r = obj as Reference;
            if(r.VarRef != VarRef) {
                return false;
            }
            if ((Indexes == null) != (r.Indexes == null)) {
                return false;
            }
            if (Indexes == null) {
                return true;
            }
            else {
                Debug.Assert(Indexes.Length == r.Indexes.Length);
                for(int i = 0; i < Indexes.Length; i++) {
                    if(!Indexes[i].Eq(r.Indexes[i])) {
                        return false;
                    }
                }
                return true;
            }
        }

        public override string ToString() {
            if(Indexes == null) {
                return Name;
            }
            else {
                return Name + "[" + string.Join(", ", from x in Indexes select x.ToString())  + "]";
            }
        }
    }

    public class Apply : Term {
        public Reference Function;
        public Term[] Args;

        public Apply(Reference function, Term[] args) {
            Function = function;
            Args = args;

            Function.Parent = this;
            foreach(Term t in Args) {
                t.Parent = this;
            }
        }

        public new Apply Clone(Dictionary<Variable, Variable> var_tbl) {
            Term[] args = (from t in Args select t.Clone(var_tbl)).ToArray();
            return new Apply(Function.Clone(var_tbl), args);
        }

        public override string ToString() {
            if ("+-*/%".Contains(Function.Name[0])) {
                string s;

                if(Args.Length == 1) {

                    s = Function.Name + " " + Args[0].ToString();
                }
                else {

                    s = string.Join(" " + Function.Name + " ", from x in Args select x.ToString());
                }

                if(Parent is Apply && (Parent as Apply).Precedence() <= Precedence()) {
                    return "(" + s + ")";
                }
                else {
                    return s;
                }
            }
            else {

                return Function.Name + "(" + string.Join(", ", from x in Args select x.ToString()) + ")";
            }
        }

        public int Precedence() {
            if (Char.IsLetter( Function.Name[0])) {
                return 20;
            }

            if ("*/%".Contains(Function.Name[0])){
                return 1;
            }          

            if(Args.Length == 1 && "+-!".Contains(Function.Name[0])){
                return 2;
            }

            return 10;
        }
    }

    public class LINQ : Term {
        public Variable[] Variables;
        public Term Select;
        public Reference Aggregate;

        public LINQ(Variable[] variables, Term select, Reference aggregate) {
            Variables = variables;
            Select = select;
            Aggregate = aggregate;

            foreach(Variable v in Variables) {
                v.ParentVar = this;
            }
            Select.Parent = this;
            if(Aggregate != null) {

                Aggregate.Parent = this;
            }
        }

        public new LINQ Clone(Dictionary<Variable, Variable> var_tbl) {
            Variable[] vars = (from v in Variables select v.Clone(var_tbl)).ToArray();
            return new LINQ(vars, Select.Clone(var_tbl), (Aggregate == null ? null : Aggregate.Clone(var_tbl)));
        }

        public override string ToString() {
            string list = string.Join(" ", from x in Variables select "from " + x.Name + " in " + x.Domain.ToString() ) + " select " + Select.ToString();
            if (Aggregate == null) {

                return list;
            }
            return "(" + list + ")." + Aggregate.Name + "()";
        }
    }
}