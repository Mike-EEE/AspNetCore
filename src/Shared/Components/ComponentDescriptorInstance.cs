// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Components
{
    internal struct ComponentDescriptorInstance
    {
        public ComponentDescriptorInstance(int sequence, string rootComponent, string invocationId) =>
            (Sequence, RootComponent, InvocationId) = (sequence, rootComponent, invocationId);

        public int Sequence { get; set; }
        public string RootComponent { get; set; }
        public string InvocationId { get; set; }
    }
}
