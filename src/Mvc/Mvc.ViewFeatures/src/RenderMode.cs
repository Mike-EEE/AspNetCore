// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNetCore.Mvc.ViewFeatures
{
    /// <summary>
    /// Describes the render mode of the component.
    /// </summary>
    /// <remarks>
    /// The rendering mode determines how the component gets rendered on the page. This enum configures whether the component
    /// is prerendered into the page or not and whether it simply renders static HTML on the page or if it includes the necessary
    /// information for to bootstrap a Blazor application from the user agent.
    /// </remarks>
    [Flags]
    public enum RenderMode
    {
        /// <summary>
        /// Indicates the component will not be prerendered. This option is not compatible with <see cref="Static"/>.
        /// </summary>
        NonPrerendered = 0b0000000_0,

        /// <summary>
        /// Indicates the component will be prerendered.
        /// </summary>
        Prerendered = 0b0000000_1,

        /// <summary>
        /// Indicates that the component will be prerendered into static HTML on to the page. Needs to be used alongside <see cref="Prerendered"/>.
        /// </summary>
        Static = 0b00000_00_0,

        /// <summary>
        /// Indicates that we are rendering a server-side component (Blazor) and that we need
        /// to emit the appropriate markers for client-side blazor to start a circuit and render the
        /// component from JavaScript.
        /// </summary>
        Server = 0b00000_01_0,

        /// <summary>
        /// An alias for RenderMode.Server | RenderMode.Prerendered
        /// </summary>
        ServerPrerendered = 0b00000_01_1,

        /// <summary>
        /// An alias for RenderMode.Static | RenderMode.Prerendered
        /// </summary>
        Html = 0b00000_00_1
    }
}
