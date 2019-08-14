// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.Components
{
    // ComponentDescriptorSerializer is an abstraction shared by both MVC and Blazor
    // This file holds the definition used by Mvc to serialize descriptors.
    internal partial class ComponentDescriptorSerializer
    {
        private static readonly object ComponentSequenceKey = new object();

        public ComponentDescriptorSerializer(IDataProtectionProvider dataProtectionProvider) =>
            DataProtector = dataProtectionProvider.CreateProtector(DataProtectionProviderPurpose);

        public ComponentMarker SerializeInvocation(HttpContext context, Type type, bool prerendered)
        {
            var (sequence, descriptor) = CreateSerializedComponentDescriptor(context, type);
            return prerendered ? ComponentMarker.Prerendered(sequence, descriptor) : ComponentMarker.NonPrerendered(sequence, descriptor);
        }

        private ComponentDescriptorInvocationSequence GetOrCreateInvocationIdentifier(HttpContext context)
        {
            if (!context.Items.TryGetValue(ComponentSequenceKey, out var sequence))
            {
                var newSequence = new ComponentDescriptorInvocationSequence();
                context.Items.Add(ComponentSequenceKey, newSequence);
                return newSequence;
            }

            return (ComponentDescriptorInvocationSequence)sequence;
        }

        private (int sequence, string payload) CreateSerializedComponentDescriptor(
            HttpContext context,
            Type rootComponent)
        {
            var invocationId = GetOrCreateInvocationIdentifier(context);
            invocationId.Next();

            var descriptor = new ComponentDescriptorInstance(
                invocationId.Sequence,
                rootComponent.Assembly.GetName().Name,
                rootComponent.FullName,
                invocationId.Value);

            return (descriptor.Sequence, DataProtector.Protect(JsonSerializer.Serialize(descriptor, _jsonSerializationOptions)));
        }

        internal IEnumerable<string> GetPreamble(ComponentMarker record)
        {
            var serializedStartRecord = JsonSerializer.Serialize(
                record,
                _jsonSerializationOptions);
            
            if (record.PrerenderId != null)
            {
                return PrerenderedStart(serializedStartRecord);
            }
            else
            {
                return NonPrerenderedSequence(serializedStartRecord);
            }

            static IEnumerable<string> PrerenderedStart(string startRecord)
            {
                yield return "<!--Blazor:";
                yield return startRecord;
                yield return "-->";
            }

            static IEnumerable<string> NonPrerenderedSequence(string record)
            {
                yield return "<!--Blazor:";
                yield return record;
                yield return "-->";
            }
        }

        internal IEnumerable<string> GetEpilogue(ComponentMarker record)
        {
            var serializedStartRecord = JsonSerializer.Serialize(
                record.GetEndRecord(),
                _jsonSerializationOptions);

            return PrerenderEnd(serializedStartRecord);

            static IEnumerable<string> PrerenderEnd(string endRecord)
            {
                yield return "<!--Blazor:";
                yield return endRecord;
                yield return "-->";
            }
        }

        private class ComponentDescriptorInvocationSequence
        {
            private static readonly RandomNumberGenerator _randomNumberGenerator = RandomNumberGenerator.Create();

            public ComponentDescriptorInvocationSequence()
            {
                Span<byte> bytes = stackalloc byte[128];
                _randomNumberGenerator.GetBytes(bytes);
                Value = Convert.ToBase64String(bytes);
                Sequence = -1;
            }

            public string Value { get; }

            public int Sequence { get; private set; }

            public void Next() => Sequence++;
        }
    }
}
