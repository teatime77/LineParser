using System.Diagnostics;
using System.Collections.Generic;
using System;
using Windows.UI;
using System.IO;
using System.Net;
using System.Linq;
using System.Threading.Tasks;

/*--------------------------------------------------------------------------------
        1行の構文解析
--------------------------------------------------------------------------------*/

namespace MyEdit {
    public class TCSharpParser : TParser {
        public static TCSharpParser CSharpParser;

        public TCSharpParser(TProject prj) : base(prj) {
        }

        public override void LPopt() {
            GetToken(EKind.LP);
        }

        public override void RPopt() {
            GetToken(EKind.RP);
        }

        public override void LCopt() {
            GetToken(EKind.LC);
        }

        public override void Colonopt() {
            GetToken(EKind.Colon);
        }

        public override void LineEnd() {
            GetToken(EKind.SemiColon);
            GetToken(EKind.EOT);
        }

        public override TVariable ReadArgVariable(TType parent_class) {
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

            TType type = ReadType(parent_class, false);
            TToken id = GetToken(EKind.Identifier);

            return new TVariable(id, type, kind);
        }
    }
}