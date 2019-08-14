// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Components
{
    // ComponentDescriptorSerializer is an abstraction shared by both MVC and Blazor
    // This file holds the definition used by Blazor to deserialize descriptors.
    internal partial class ComponentDescriptorSerializer
    {
        public ComponentDescriptorSerializer(
            IDataProtectionProvider dataProtectionProvider,
            ILogger<ComponentDescriptorSerializer> logger,
            RootComponentTypeCache rootComponentTypeCache)
        {
            DataProtector = dataProtectionProvider.CreateProtector(DataProtectionProviderPurpose);
            Logger = logger;
            RootComponentTypeCache = rootComponentTypeCache;
        }

        public ILogger<ComponentDescriptorSerializer> Logger { get; }

        public RootComponentTypeCache RootComponentTypeCache { get; }

        public bool TryDeserializeComponentDescriptorCollection(string serializedComponentRecords, out List<ComponentDescriptor> descriptors)
        {
            var markers = JsonSerializer.Deserialize<IEnumerable<ComponentMarker>>(serializedComponentRecords, _jsonSerializationOptions);
            descriptors = new List<ComponentDescriptor>();
            int lastSequence = -1;

            var previousInstance = new ComponentDescriptorInstance();
            foreach (var marker in markers)
            {
                if (marker.Type != ComponentMarker.ServerMarkerType)
                {
                    Log.InvalidMarkerType(Logger, marker.Type);
                    descriptors.Clear();
                    return false;
                }

                if (marker.Sequence == null)
                {
                    Log.MissingMarkerSequence(Logger);
                    descriptors.Clear();
                    return false;
                }

                if (marker.Descriptor == null)
                {
                    Log.MissingMarkerDescriptor(Logger);
                    descriptors.Clear();
                    return false;
                }

                var (descriptor, instance) = DeserializeComponentDescriptor(marker);
                if (descriptor == null)
                {
                    // We failed to deserialize the component descriptor for some reason.
                    descriptors.Clear();
                    return false;
                }

                // We force our client to send the descriptors in order so that we do minimal work.
                if (lastSequence != -1 && lastSequence != instance.Sequence - 1)
                {
                    Log.OutOfSequenceDescriptor(Logger, lastSequence, instance.Sequence);
                    descriptors.Clear();
                    return false;
                }

                if (lastSequence != -1 &&
                    !CryptographicOperations.FixedTimeEquals(
                        MemoryMarshal.AsBytes(previousInstance.InvocationId.AsSpan()),
                        MemoryMarshal.AsBytes(instance.InvocationId.AsSpan())))
                {
                    Log.MismatchedInvocationId(Logger, previousInstance.InvocationId, instance.InvocationId);
                    return false;
                }

                // As described below, we build a chain of descriptors to prevent being flooded by
                // descriptors from a client not behaving properly.
                lastSequence = instance.Sequence;
                previousInstance = instance;
                descriptors.Add(descriptor);
            }

            return true;
        }

        private (ComponentDescriptor, ComponentDescriptorInstance) DeserializeComponentDescriptor(ComponentMarker record)
        {
            string unprotected;
            try
            {
                unprotected = DataProtector.Unprotect(record.Descriptor);
            }
            catch (Exception e)
            {
                Log.FailedToUnprotectDescriptor(Logger, e);
                return default;
            }

            ComponentDescriptorInstance descriptorInstance;
            try
            {
                descriptorInstance = JsonSerializer.Deserialize<ComponentDescriptorInstance>(
                    unprotected,
                    _jsonSerializationOptions);
            }
            catch (Exception e)
            {
                Log.FailedToDeserializeDescriptor(Logger, e);
                return default;
            }

            var componentType = RootComponentTypeCache
                .GetRootComponent(descriptorInstance.AssemblyName, descriptorInstance.TypeName);

            if (componentType == null)
            {
                Log.FailedToFindComponent(Logger, descriptorInstance.TypeName, descriptorInstance.AssemblyName);
                return default;
            }

            var componentDescriptor = new ComponentDescriptor
            {
                ComponentType = componentType,
                Sequence = descriptorInstance.Sequence
            };

            return (componentDescriptor, descriptorInstance);
        }

        private static class Log
        {
            private static readonly Action<ILogger, Exception> _failedToDeserializeDescriptor =
                LoggerMessage.Define(
                    LogLevel.Debug,
                    new EventId(1, "FailedToDeserializeDescriptor"),
                    "Failed to deserialize the component descriptor.");

            private static readonly Action<ILogger, string, string, Exception> _failedToFindComponent =
                LoggerMessage.Define<string, string>(
                LogLevel.Debug,
                new EventId(2, "FailedToFindComponent"),
                "Failed to find component '{ComponentName}' in assembly '{Assembly}'.");

            private static readonly Action<ILogger, Exception> _failedToUnprotectDescriptor =
                LoggerMessage.Define(
                    LogLevel.Debug,
                    new EventId(3, "FailedToUnprotectDescriptor"),
                    "Failed to unprotect the component descriptor.");

            private static readonly Action<ILogger, string, Exception> _invalidMarkerType =
                LoggerMessage.Define<string>(
                LogLevel.Debug,
                new EventId(4, "InvalidMarkerType"),
                "Invalid component marker type '{}'.");

            private static readonly Action<ILogger, Exception> _missingMarkerDescriptor =
                LoggerMessage.Define(
                LogLevel.Debug,
                new EventId(5, "MissingMarkerDescriptor"),
                "The component marker is missing the descriptor.");

            private static readonly Action<ILogger, Exception> _missingMarkerSequence =
                LoggerMessage.Define(
                LogLevel.Debug,
                new EventId(6, "MissingMarkerSequence"),
                "The component marker is missing the descriptor.");

            private static readonly Action<ILogger, string, string, Exception> _mismatchedInvocationId =
                LoggerMessage.Define<string, string>(
                LogLevel.Debug,
                new EventId(7, "MismatchedInvocationId"),
                "The descriptor invocationId is '{invocationId}' and got a descriptor with invocationId '{currentInvocationId}'.");

            private static readonly Action<ILogger, int, int, Exception> _outOfSequenceDescriptor =
                LoggerMessage.Define<int, int>(
                LogLevel.Debug,
                new EventId(8, "OutOfSequenceDescriptor"),
                "The last descriptor sequence was '{lastSequence}' and got a descriptor with sequence '{receivedSequence}'.");

            internal static void FailedToDeserializeDescriptor(ILogger<ComponentDescriptorSerializer> logger, Exception e) =>
                _failedToDeserializeDescriptor(logger, e);

            internal static void FailedToFindComponent(ILogger<ComponentDescriptorSerializer> logger, string assemblyName, string typeName) =>
                _failedToFindComponent(logger, assemblyName, typeName, null);

            internal static void FailedToUnprotectDescriptor(ILogger<ComponentDescriptorSerializer> logger, Exception e) =>
                _failedToUnprotectDescriptor(logger, e);

            internal static void InvalidMarkerType(ILogger<ComponentDescriptorSerializer> logger, string markerType) =>
                _invalidMarkerType(logger, markerType, null);

            internal static void MismatchedInvocationId(ILogger<ComponentDescriptorSerializer> logger, string invocationId, string currentInvocationId) =>
                _mismatchedInvocationId(logger, invocationId, currentInvocationId, null);

            internal static void MissingMarkerDescriptor(ILogger<ComponentDescriptorSerializer> logger) => _missingMarkerDescriptor(logger, null);

            internal static void MissingMarkerSequence(ILogger<ComponentDescriptorSerializer> logger) => _failedToDeserializeDescriptor(logger, null);

            internal static void OutOfSequenceDescriptor(ILogger<ComponentDescriptorSerializer> logger, int lastSequence, int sequence) =>
                _outOfSequenceDescriptor(logger, lastSequence, sequence, null);
        }
    }
}
