﻿// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using Microsoft.VisualStudio.LanguageServices.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.ProjectSystem.VS;

namespace Microsoft.VisualStudio.ProjectSystem.LanguageServices.Handlers
{
    /// <summary>
    ///     Responsible for coordinating changes and conflicts between evaluation and design-time builds, and pushing those changes
    ///     onto Roslyn via a <see cref="IWorkspaceProjectContext"/>.
    /// </summary>
    internal abstract partial class AbstractEvaluationCommandLineHandler : AbstractWorkspaceContextHandler
    {
        // This class is not thread-safe, and the assumption is that the caller will make sure that project evaluations and builds (design-time) 
        // do not overlap inside the class at the same time.
        //
        // In the ideal world, we would simply wait for a design-time build to get the command-line arguments that would have been passed
        // to Csc/Vbc and push these onto Roslyn. This is exactly what the legacy project system did; when a user added or removed a file
        // or changed the project, it performed a blocking wait on the design-time build before returning control to the user. In CPS,
        // however, design-time builds are not UI blocking, so control can be returned to the user before Roslyn has been told about the 
        // file. This leads to the user observable behavior where the source file for a period of time lives in the "Misc" project and is 
        // without "project" IntelliSense. To counteract that, we push changes both in design-time builds *and* during evaluations, which 
        // gives the user results a lot faster than if we just pushed during design-time builds only. Evaluations are guaranteed to have 
        // occurred before a file is seen by components outside of the project system.
        //
        // Typically, adds and removes of files found at evaluation time are also found during a design-time build, with the later also 
        // including generated files. This forces us to remember what files we've already sent to Roslyn to avoid sending duplicate adds
        // or removes of the same file. Due to design-time builds being significantly slower than evaluations, there are also times where 
        // many evaluations have occured by the time a design-time build based on a past version of the ConfiguredProject has completed.
        // This can lead to conflicts.
        //
        // A conflict occurs when evaluation or design-time build adds a item that the other removed, or vice versa. 
        // 
        //  Examples of conflicts include:
        //
        //   - A user removes a item before a design-time build that contains the addition of that item has finished
        //   - A user adds a item before a design-time build that contains the removal of that item has finished
        //   - A user adds a item that was previously generated by a target (but stopped generating it)
        //   - A user removes a item and in the same version it starts getting generated via a target during design-time build
        //
        //  Examples of changes that are not conflicts include:
        // 
        //   - A user adds a item and it appears as an addition in both evaluation and design-time build (the item is always added)
        //   - A user removes a item and it appears as a removal in both evaluation and design-time build  (the item is always removed)
        //   - A target during design-time build generates an item that did not appear during evaluation (the item is always added)
        //   - A target, new since the last design-time build, removes a item that appeared during evaluation (the item is always removed)
        //
        // TODO: These are also not conflicts, but we're currently handling differently to a normal build, which we should fix:
        //
        //    - A target from the very first design-time build, removed an item that appeared during evaluation. Currently, the item is "added"
        //      but command-line builds do not see the source file. This is because a design-time build IProjectChangeDescription is only a 
        //      diff between itself and the previous build, not between itself and evaluation, which means that design-time build diff never 
        //      knows that the item was removed, it was just never present.
        //
        // Algorithm for resolving conflicts is as follows:
        //
        // 1. Walk every evaluation since the last design-time build, discarding those from conflict resolution that have a version less 
        //    than or equal to the current design-time build. 
        // 2. Walk every design-time build addition, if there's an associated removal in a later evaluation - we throw away the addition
        // 3. Walk every design-time build removal, if there's an associated addition in a later evaluation - we throw away the removal
        //
        // We don't resolve conflicts between changes items, because the design-time build doesn't produce them due to the way we represent
        // command-line arguments as individual item includes, such as <CscCommandLineArguments Include="/reference:Foo.dll"/>, without any 
        // metadata.
        //
        private readonly HashSet<string> _paths = new(StringComparers.Paths);
        private readonly Queue<VersionedProjectChangeDiff> _projectEvaluations = new();
        private readonly UnconfiguredProject _project;

        /// <summary>
        ///     Initializes a new instance of the <see cref="AbstractEvaluationCommandLineHandler"/> class with the specified project.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="project"/> is <see langword="null"/>.
        /// </exception>
        protected AbstractEvaluationCommandLineHandler(UnconfiguredProject project)
        {
            Requires.NotNull(project, nameof(project));

            _project = project;
        }

        /// <summary>
        ///     Applies the specified version of the project evaluation <see cref="IProjectChangeDiff"/> and metadata to the underlying
        ///     <see cref="IWorkspaceProjectContext"/>, indicating if the context is the currently active one.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="version"/> is <see langword="null"/>.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="difference" /> is <see langword="null"/>.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="previousMetadata" /> is <see langword="null"/>.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="currentMetadata" /> is <see langword="null"/>.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="logger" /> is <see langword="null"/>.
        /// </exception>
        public void ApplyProjectEvaluation(IComparable version, IProjectChangeDiff difference, IImmutableDictionary<string, IImmutableDictionary<string, string>> previousMetadata, IImmutableDictionary<string, IImmutableDictionary<string, string>> currentMetadata, bool isActiveContext, IProjectDiagnosticOutputService logger)
        {
            Requires.NotNull(version, nameof(version));
            Requires.NotNull(difference, nameof(difference));
            Requires.NotNull(previousMetadata, nameof(previousMetadata));
            Requires.NotNull(currentMetadata, nameof(currentMetadata));
            Requires.NotNull(logger, nameof(logger));

            if (!difference.AnyChanges)
                return;

            difference = HandlerServices.NormalizeRenames(difference);
            EnqueueProjectEvaluation(version, difference);

            ApplyChangesToContext(difference, previousMetadata, currentMetadata, isActiveContext, logger, evaluation: true);
        }

        /// <summary>
        ///     Applies the specified version of the project build <see cref="IProjectChangeDiff"/> to the underlying
        ///     <see cref="IWorkspaceProjectContext"/>, indicating if the context is the currently active one.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="version"/> is <see langword="null"/>.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="difference" /> is <see langword="null"/>.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="logger" /> is <see langword="null"/>.
        /// </exception>
        public void ApplyProjectBuild(IComparable version, IProjectChangeDiff difference, bool isActiveContext, IProjectDiagnosticOutputService logger)
        {
            Requires.NotNull(version, nameof(version));
            Requires.NotNull(difference, nameof(difference));
            Requires.NotNull(logger, nameof(logger));

            if (!difference.AnyChanges)
                return;

            difference = HandlerServices.NormalizeRenames(difference);
            difference = ResolveProjectBuildConflicts(version, difference);

            ApplyChangesToContext(difference, ImmutableStringDictionary<IImmutableDictionary<string, string>>.EmptyOrdinal, ImmutableStringDictionary<IImmutableDictionary<string, string>>.EmptyOrdinal, isActiveContext, logger, evaluation: false);
        }

        protected abstract void AddToContext(string fullPath, IImmutableDictionary<string, string> metadata, bool isActiveContext, IProjectDiagnosticOutputService logger);

        protected abstract void RemoveFromContext(string fullPath, IProjectDiagnosticOutputService logger);

        protected abstract void UpdateInContext(string fullPath, IImmutableDictionary<string, string> previousMetadata, IImmutableDictionary<string, string> currentMetadata, bool isActiveContext, IProjectDiagnosticOutputService logger);

        private bool IsItemInCurrentConfiguration(string includePath, IImmutableDictionary<string, IImmutableDictionary<string, string>> metadata)
        {
            if (metadata.TryGetValue(includePath, out IImmutableDictionary<string, string> itemMetadata)
                && itemMetadata.GetBoolProperty(Compile.ExcludeFromCurrentConfigurationProperty) is true)
            {
                return false;
            }

            return true;
        }

        private void ApplyChangesToContext(IProjectChangeDiff difference, IImmutableDictionary<string, IImmutableDictionary<string, string>> previousMetadata, IImmutableDictionary<string, IImmutableDictionary<string, string>> currentMetadata, bool isActiveContext, IProjectDiagnosticOutputService logger, bool evaluation)
        {
            foreach (string includePath in difference.RemovedItems)
            {
                RemoveFromContextIfPresent(includePath, logger);
            }

            foreach (string includePath in difference.AddedItems)
            {
                if (evaluation && !IsItemInCurrentConfiguration(includePath, currentMetadata))
                {
                    // The item is present in evaluation but contains metadata indicating it should be
                    // ignored.
                    continue;
                }

                AddToContextIfNotPresent(includePath, currentMetadata, isActiveContext, logger);
            }

            if (evaluation)
            {   // No need to look at metadata for design-time builds, the items that come from
                // that aren't traditional items, but are just command-line args we took from
                // the compiler and converted them to look like items.

                foreach (string includePath in difference.ChangedItems)
                {
                    UpdateInContextIfPresent(includePath, previousMetadata, currentMetadata, isActiveContext, logger);

                    // TODO: Check for changes in the metadata indicating if we should ignore the file
                    // in the current configuration.
                }
            }

            Assumes.True(difference.RenamedItems.Count == 0, "We should have normalized renames.");
        }

        private void RemoveFromContextIfPresent(string includePath, IProjectDiagnosticOutputService logger)
        {
            string fullPath = _project.MakeRooted(includePath);

            if (_paths.Contains(fullPath))
            {
                // Remove from the context first so if Roslyn throws due to a bug 
                // or other reason, that our state of the world remains consistent
                RemoveFromContext(fullPath, logger);
                bool removed = _paths.Remove(fullPath);
                Assumes.True(removed);
            }
        }

        private void AddToContextIfNotPresent(string includePath, IImmutableDictionary<string, IImmutableDictionary<string, string>> metadata, bool isActiveContext, IProjectDiagnosticOutputService logger)
        {
            string fullPath = _project.MakeRooted(includePath);

            if (!_paths.Contains(fullPath))
            {
                IImmutableDictionary<string, string> itemMetadata = metadata.GetValueOrDefault(includePath, ImmutableStringDictionary<string>.EmptyOrdinal);

                // Add to the context first so if Roslyn throws due to a bug or
                // other reason, that our state of the world remains consistent
                AddToContext(fullPath, itemMetadata, isActiveContext, logger);
                bool added = _paths.Add(fullPath);
                Assumes.True(added);
            }
        }

        private void UpdateInContextIfPresent(string includePath, IImmutableDictionary<string, IImmutableDictionary<string, string>> previousMetadata, IImmutableDictionary<string, IImmutableDictionary<string, string>> currentMetadata, bool isActiveContext, IProjectDiagnosticOutputService logger)
        {
            string fullPath = _project.MakeRooted(includePath);

            if (_paths.Contains(fullPath))
            {
                IImmutableDictionary<string, string> previousItemMetadata = previousMetadata.GetValueOrDefault(includePath, ImmutableStringDictionary<string>.EmptyOrdinal);
                IImmutableDictionary<string, string> currentItemMetadata = currentMetadata.GetValueOrDefault(includePath, ImmutableStringDictionary<string>.EmptyOrdinal);

                UpdateInContext(fullPath, previousItemMetadata, currentItemMetadata, isActiveContext, logger);
            }
        }

        private IProjectChangeDiff ResolveProjectBuildConflicts(IComparable projectBuildVersion, IProjectChangeDiff projectBuildDifference)
        {
            DiscardOutOfDateProjectEvaluations(projectBuildVersion);

            // Walk all evaluations (if any) that occurred since we launched and resolve the conflicts
            foreach (VersionedProjectChangeDiff evaluation in _projectEvaluations)
            {
                Assumes.True(evaluation.Version.IsLaterThan(projectBuildVersion), "Attempted to resolve a conflict between a project build and an earlier project evaluation.");

                projectBuildDifference = ResolveConflicts(evaluation.Difference, projectBuildDifference);
            }

            return projectBuildDifference;
        }

        private static IProjectChangeDiff ResolveConflicts(IProjectChangeDiff evaluationDifferences, IProjectChangeDiff projectBuildDifferences)
        {
            // Remove added items that were removed by later evaluations, and vice versa
            IImmutableSet<string> added = projectBuildDifferences.AddedItems.Except(evaluationDifferences.RemovedItems);
            IImmutableSet<string> removed = projectBuildDifferences.RemovedItems.Except(evaluationDifferences.AddedItems);

            Assumes.True(projectBuildDifferences.ChangedItems.Count == 0, "We should never see ChangedItems during project builds.");

            return new ProjectChangeDiff(added, removed, projectBuildDifferences.ChangedItems);
        }

        private void DiscardOutOfDateProjectEvaluations(IComparable version)
        {
            // Throw away evaluations that are the same version or earlier than the design-time build
            // version as it has more up-to-date information on the the current state of the project

            // Note, evaluations could be empty if previous evaluations resulted in no new changes
            while (_projectEvaluations.Count > 0)
            {
                VersionedProjectChangeDiff projectEvaluation = _projectEvaluations.Peek();
                if (!projectEvaluation.Version.IsEarlierThanOrEqualTo(version))
                    break;

                _projectEvaluations.Dequeue();
            }
        }

        private void EnqueueProjectEvaluation(IComparable version, IProjectChangeDiff evaluationDifference)
        {
            Assumes.False(_projectEvaluations.Count > 0 && version.IsEarlierThan(_projectEvaluations.Peek().Version), "Attempted to push a project evaluation that regressed in version.");

            _projectEvaluations.Enqueue(new VersionedProjectChangeDiff(version, evaluationDifference));
        }
    }
}
