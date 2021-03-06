// HgSharp
// 
// Copyright 2005-2015 Matt Mackall <mpm@selenic.com> and Mercurial contributors
// Copyright 2011-2015 Anton Gogolev <anton.gogolev@hglabhq.com>
// 
// The following code is a derivative work of the code from the Mercurial project, 
// which is licensed GPLv2. This code therefore is also licensed under the terms 
// of the GNU Public License, verison 2.
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HgSharp.Core
{
    public class HgAnnotation
    {
        public HgPath Path { get; private set; }

        public ReadOnlyCollection<HgAnnotationLine> Lines { get; private set; }

        public HgAnnotation(HgPath path, IEnumerable<HgAnnotationLine> lines)
        {
            Path = path;
            Lines = new ReadOnlyCollection<HgAnnotationLine>(new List<HgAnnotationLine>(lines));
        }
    }
}