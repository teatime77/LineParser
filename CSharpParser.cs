
/*--------------------------------------------------------------------------------
        1行の構文解析
--------------------------------------------------------------------------------*/

namespace Miyu {
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

        public override TVariable ReadArgVariable() {
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

            TType type = ReadType(false);
            TToken id = GetToken(EKind.Identifier);

            TTerm init = null;
            if (CurrentToken.Kind == EKind.Assign) {
                // 初期値がある場合

                GetToken(EKind.Assign);

                // 初期値の式
                init = Expression();
            }

            return new TVariable(id, type, kind, init);
        }
    }
}
