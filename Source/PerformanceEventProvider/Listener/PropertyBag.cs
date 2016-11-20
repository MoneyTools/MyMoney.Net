//-----------------------------------------------------------------------
// <copyright file="PropertyBag.cs" company="Microsoft">
//   (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------
// Author: Daniel Vasquez Lopez
//------------------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Diagnostics.PerformanceProvider.Listener
{
    [Serializable]
    public sealed class PropertyBag : Dictionary<string, object>
    {
        public PropertyBag()
            : base(StringComparer.OrdinalIgnoreCase)
        {
        }
    }
}
