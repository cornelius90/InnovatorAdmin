// HgSharp
// 
// Copyright 2005-2015 Matt Mackall <mpm@selenic.com> and Mercurial contributors
// Copyright 2011-2015 Anton Gogolev <anton.gogolev@hglabhq.com>
// 
// The following code is a derivative work of the code from the Mercurial project, 
// which is licensed GPLv2. This code therefore is also licensed under the terms 
// of the GNU Public License, verison 2.
using System.Diagnostics;

namespace HgSharp.Core
{
    [DebuggerDisplay("{Changeset.Metadata.Revision}@{Changeset.Metadata.NodeID.Short,nq} {Line,nq}")]
    public class HgAnnotationLine
    {
        public HgChangeset Changeset { get; private set; }

        public string Line { get; private set; }

        public HgAnnotationLine(HgChangeset changeset, string line)
        {
            Changeset = changeset;
            Line = line;
        }
    }
}