// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNetCore.Mvc.ViewFeatures
{
    internal struct ComponentRecord
    {
        public const string ServerRecordType = "server";

        private ComponentRecord(string type, string descriptor, int? sequence, string prerenderId) : this()
        {
            Type = type;
            PrerenderId = prerenderId;
            Descriptor = descriptor;
            Sequence = sequence;
        }

        public int? Sequence { get; set; }
        public string Type { get; set; }
        public string PrerenderId { get; set; }
        public string Descriptor { get; set; }

        public static ComponentRecord Prerendered(int sequence, string descriptor) =>
            new ComponentRecord(ServerRecordType, descriptor, sequence, Guid.NewGuid().ToString("N"));

        public static ComponentRecord NonPrerendered(int sequence, string descriptor) =>
            new ComponentRecord(ServerRecordType, descriptor, sequence, null);

        public ComponentRecord GetEndRecord()
        {
            if (PrerenderId == null)
            {
                throw new InvalidOperationException("Can't get an end record for non-prerendered components.");
            }

            return new ComponentRecord(null, null, null, PrerenderId);
        }
    }
}
