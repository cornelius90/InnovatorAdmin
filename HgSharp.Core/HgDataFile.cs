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
    [DebuggerDisplay("{StoreRelativePath,nq}")]
    public class HgDataFile
    {
        public HgPath Path { get; private set; }

        public string StoreRelativePath { get; private set; }
        
        public long Length { get; private set; }

        public HgDataFile(HgPath path, string storeRelativePath, long length)
        {
            Path = path;
            StoreRelativePath = storeRelativePath;
            Length = length;
        }
    }
}