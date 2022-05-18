﻿// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics;

namespace Microsoft.VisualStudio.ProjectSystem.Build
{
    /// <summary>
    /// A set of outputs generated by a project that fit under some common category.
    /// </summary>
    [DebuggerDisplay("Output Group {Name} ({TargetName})")]
    internal class OutputGroup : IOutputGroup
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OutputGroup"/> class.
        /// </summary>
        internal OutputGroup(string name, string targetName, string displayName, string? description, IImmutableList<KeyValuePair<string, IImmutableDictionary<string, string>>> items, bool successful)
        {
            Requires.NotNullOrEmpty(name, nameof(name));
            Requires.NotNullOrEmpty(targetName, nameof(targetName));
            Requires.NotNullOrEmpty(displayName, nameof(displayName));
            Requires.NotNull(items, nameof(items));

            Name = name;
            TargetName = targetName;
            DisplayName = displayName;
            Description = description;
            Outputs = items;
            IsSuccessful = successful;
        }

        public string TargetName { get; }

        public string Name { get; }

        public string DisplayName { get; }

        public string? Description { get; }

        public bool IsSuccessful { get; }

        public IImmutableList<KeyValuePair<string, IImmutableDictionary<string, string>>> Outputs { get; }
    }
}
