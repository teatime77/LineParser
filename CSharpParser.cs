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

        public override void LCopt() {
            GetToken(EKind.LC);
        }

        public override void LineEnd() {
            GetToken(EKind.SemiColon);
            GetToken(EKind.EOT);
        }

        public override TVariable ReadArgVariable() {
            TClass type = ReadType(null, false);
            TToken id = GetToken(EKind.Identifier);

            return new TVariable(id, type, null);
        }
    }
}