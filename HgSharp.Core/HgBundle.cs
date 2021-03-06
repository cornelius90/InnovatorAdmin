﻿// HgSharp
// 
// Copyright 2005-2015 Matt Mackall <mpm@selenic.com> and Mercurial contributors
// Copyright 2011-2015 Anton Gogolev <anton.gogolev@hglabhq.com>
// 
// The following code is a derivative work of the code from the Mercurial project, 
// which is licensed GPLv2. This code therefore is also licensed under the terms 
// of the GNU Public License, verison 2.
using System.Collections.Generic;

namespace HgSharp.Core
{
    public class HgBundle
    {
        public IEnumerable<HgChunk> Changelog { get; private set; }

        public IEnumerable<HgChunk> Manifest { get; private set; }

        public IEnumerable<HgBundleFile> Files { get; private set; }

        public HgBundle(IEnumerable<HgChunk> changelog, IEnumerable<HgChunk> manifest, IEnumerable<HgBundleFile> files)
        {
            Changelog = changelog;
            Manifest = manifest;
            Files = files;
        }
    }
}
