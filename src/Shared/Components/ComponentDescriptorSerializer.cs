// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Microsoft.AspNetCore.Components
{
    // ComponentDescriptorSerializer is an abstraction shared by both MVC and Blazor
    // This file holds the common elements used by both.

    // **Component descriptor protocol**
    // MVC serializes one or more components as comments in HTML.
    // Each comment is in the form <!-- Blazor: <<Json>> -->
    // Where <<Json>> has the following properties:
    // 'type' indicates the marker type. For now it's limited to server.
    // 'sequence' indicates the order in which this component got rendered on the server.
    // 'descriptor' a data-protected payload that allows the server to validate the legitimacy of the rendered component.
    // 'prerenderId' a unique identifier that uniquely identifies the marker to match start and end markers.
    //
    // descriptor holds the information to validate a component render request. It prevents an infinite number of components
    // from being rendered by a given client.
    //
    // descriptor is a data protected json payload that holds the following information
    // 'sequence' indicates the order in which this component got rendered on the server.
    // 'assemblyName' the assembly name for the rendered component.
    // 'type' the full type name for the rendered component.
    // 'invocationId' a random string that matches all components rendered by as part of a single HTTP response.

    // Serialization:
    // For a given response, MVC renders one or more markers in sequence, including a descriptor for each rendered
    // component containing the information described above.

    // Deserialization:
    // To prevent a client from rendering an infinite amount of components, we require clients to send all component
    // markers in order. They can do so thanks to the sequence included in the marker.
    // When we process a marker we do the following.
    // * We unprotect the data-protected information.
    // * We validate that the sequence number for the descriptor goes after the previous descriptor.
    // * We compare the invocationId for the previous descriptor against the invocationId for the current descriptor to make sure they match.
    // By doing this we achieve three things:
    // * We ensure that the descriptor came from the server.
    // * We ensure that a client can't just send an infinite amount of components to render.
    // * We ensure that we do the minimal amount of work in the case of an invalid sequence of descriptors.
    //
    // For example:
    // A client can't just send 100 component markers and force us to process them if the server didn't generate those 100 markers.
    //  * If a marker is out of sequence we will fail early, so we process at most n-1 markers.
    //  * If a marker has the right sequence but the invocation ID is different we will fail at that point. We know for sure that the
    //    component wasn't render as part of the same response.
    //  * If a marker can't be unprotected we will fail early. We know that the marker was tampered with and can't be trusted.
    internal partial class ComponentDescriptorSerializer
    {
        private const string DataProtectionProviderPurpose = "Microsoft.AspNetCore.Components.ComponentDescriptorSerializer";

        private static readonly JsonSerializerOptions _jsonSerializationOptions =
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                IgnoreNullValues = true
            };

        public IDataProtector DataProtector { get; }
    }
}
