// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.Components
{
    internal class ComponentDescriptorSerializer
    {
        private const string DataProtectionProviderPurpose = "Microsoft.AspNetCore.Components.ComponentDescriptorSerializer";

        private static readonly object ComponentSequenceKey = new object();
        private static readonly JsonSerializerOptions _jsonSerializationOptions =
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                IgnoreNullValues = true
            };

        public ComponentDescriptorSerializer(
            IDataProtectionProvider dataProtectionProvider,
            RootComponentTypeCache rootComponentTypeCache)
        {
            DataProtector = dataProtectionProvider.CreateProtector(DataProtectionProviderPurpose);
            RootComponentTypeCache = rootComponentTypeCache;
        }

        public RootComponentTypeCache RootComponentTypeCache { get; }

        public IDataProtector DataProtector { get; }


        public bool TryDeserializeComponentDescriptorCollection(string serializedComponentRecords, out List<ComponentDescriptor> descriptors)
        {
            var records = JsonSerializer.Deserialize<IEnumerable<ComponentRecord>>(serializedComponentRecords, _jsonSerializationOptions);
            descriptors = new List<ComponentDescriptor>();

            var previousInstance = new ComponentDescriptorInstance();
            foreach (var record in records)
            {
                if (record.Descriptor == null)
                {
                    descriptors.Clear();
                    return false;
                }

                if (record.Descriptor != null)
                {
                    var (descriptor, instance) = DeserializeComponentDescriptor(record);
                    if (descriptor != null)
                    {
                        // We force our client to send the descriptors in order so that we do minimal work.
                        if (instance.Sequence != 0)
                        {
                            if (instance.Sequence - 1 != previousInstance.Sequence)
                            {
                                descriptors.Clear();
                                return false;
                            }

                            // As described below, we build a chain of descriptors to prevent being flooded by
                            // descriptors from a client not behaving properly.
                            if (!CryptographicOperations.FixedTimeEquals(
                                MemoryMarshal.AsBytes(previousInstance.InvocationId.AsSpan()),
                                MemoryMarshal.AsBytes(instance.InvocationId.AsSpan())))
                            {
                                return false;
                            }
                        }

                        previousInstance = instance;
                        descriptors.Add(descriptor);
                    }
                    else
                    {
                        descriptors.Clear();
                        return false;
                    }
                }
            }

            return true;
        }

        private (ComponentDescriptor, ComponentDescriptorInstance) DeserializeComponentDescriptor(ComponentRecord record)
        {
            if (record.Descriptor == null || record.Sequence == null || record.Type != ComponentRecord.ServerRecordType)
            {
                return default;
            }

            try
            {
                var unprotected = DataProtector.Unprotect(record.Descriptor);

                var descriptorInstance = JsonSerializer.Deserialize<ComponentDescriptorInstance>(
                    unprotected,
                    _jsonSerializationOptions);

                var componentDescriptor = new ComponentDescriptor
                {
                    ComponentType = RootComponentTypeCache.GetRootComponent(descriptorInstance.RootComponent),
                    Sequence = descriptorInstance.Sequence
                };
                return (componentDescriptor, descriptorInstance);
            }
            catch
            {
                return default;
            }
        }

        public ComponentRecord SerializeInvocation(HttpContext context, Type type, ParameterView parametersCollection, bool prerendered)
        {
            var (sequence, descriptor) = CreateSerializedComponentDescriptor(context, type);
            return prerendered ? ComponentRecord.Prerendered(sequence, descriptor) : ComponentRecord.NonPrerendered(sequence, descriptor);
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

            var rootComponentKey = RootComponentTypeCache.RegisterRootComponent(rootComponent);
            var descriptor = new ComponentDescriptorInstance(invocationId.Sequence, rootComponentKey, invocationId.Value);

            return (descriptor.Sequence, DataProtector.Protect(JsonSerializer.Serialize(descriptor, _jsonSerializationOptions)));
        }

        internal IEnumerable<string> GetPreamble(ComponentRecord record)
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

        internal IEnumerable<string> GetEpilogue(ComponentRecord record)
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
