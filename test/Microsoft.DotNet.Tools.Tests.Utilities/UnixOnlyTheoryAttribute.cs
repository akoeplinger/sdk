// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public class UnixOnlyTheoryAttribute : TheoryAttribute
    {
        public UnixOnlyTheoryAttribute()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                this.Skip = "This test requires Unix to run";
            }
        }
    }
}
