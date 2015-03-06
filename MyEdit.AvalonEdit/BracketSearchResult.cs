using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MyEdit.AvalonEdit
{
    public class BracketSearchResult
    {
        public int OpeningBracketOffset { get; set; }

        public int OpeningBracketLength { get; set; }

        public int ClosingBracketOffset { get; set; }

        public int ClosingBracketLength { get; set; }
    }
}
