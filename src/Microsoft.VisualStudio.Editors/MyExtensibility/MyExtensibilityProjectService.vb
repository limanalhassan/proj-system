' Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

Option Strict On
Option Explicit On
Imports System.Text
Imports System.Windows.Forms

Imports EnvDTE

Imports Microsoft.VisualStudio.Editors.Common
Imports Microsoft.VisualStudio.Editors.DesignerFramework
Imports Microsoft.VisualStudio.Editors.MyExtensibility.MyExtensibilitySolutionService
Imports Microsoft.VisualStudio.Editors.MyExtensibility.MyExtensibilityUtil
Imports Microsoft.VisualStudio.Shell.Interop

Imports Res = My.Resources.MyExtensibilityRes

Namespace Microsoft.VisualStudio.Editors.MyExtensibility

    ''' ;MyExtensibilityProjectService
    ''' <summary>
    ''' Provides My Extensibility service for a VB project.
    ''' </summary>
    ''' <remarks>
    ''' My Extensibility project service is lazy-inited by MyExtensibilitySolutionService
    ''' so that performance of Create New Project is not impacted. 
    ''' [OBSOLETE] Keep for reference only.
    ''' Once created, we listen to reference events directly from there on.
    ''' That will avoid troubles with Zero-Impact-Project Save All to a DIFFERENT drive.
    ''' (the project system will call the compiler to Remove, Add, Remove, Add each new references).
    ''' [/OBSOLETE]
    ''' The solution above leads to DevDiv Bugs 51380. If multiple assemblies are being added,
    ''' the ProjectService will only know about the first assemblies.
    ''' The issue with ZIP does not exist with later Orcas build.
    ''' </remarks>
    Friend Class MyExtensibilityProjectService

        Public Event ExtensionChanged()

#Region "Public methods"

        Public Shared Function CreateNew(
                vbPackage As VBPackage, project As Project,
                projectHierarchy As IVsHierarchy, extensibilitySettings As MyExtensibilitySettings) _
                As MyExtensibilityProjectService
            If vbPackage Is Nothing Then
                Return Nothing
            End If
            If project Is Nothing Then
                Return Nothing
            End If
            If projectHierarchy Is Nothing Then
                Return Nothing
            End If
            If extensibilitySettings Is Nothing Then
                Return Nothing
            End If

            Dim projectService As New MyExtensibilityProjectService(
                vbPackage, project, projectHierarchy, extensibilitySettings)

            Dim projectSettings As MyExtensibilityProjectSettings = MyExtensibilityProjectSettings.CreateNew(
                projectService, vbPackage, project, projectHierarchy)
            If projectSettings Is Nothing Then
                Return Nothing
            End If
            projectService._projectSettings = projectSettings

            Return projectService
        End Function

        ''' ;AddExtensionsFromPropPage
        ''' <summary>
        ''' Launch Add Extensions dialog and add the specified extensions to the project
        ''' (from "My Extensions" property page).
        ''' </summary>
        Public Sub AddExtensionsFromPropPage()
            Dim addExtensionsDialog As New AddMyExtensionsDialog(
                _vbPackage, _extensibilitySettings.GetExtensionTemplates(ProjectTypeID, _project))
            If addExtensionsDialog.ShowDialog() = DialogResult.OK Then
                _excludedTemplates = addExtensionsDialog.ExtensionTemplatesToAdd
                Try
                    Dim extensionsAddedSB As New StringBuilder()
                    AddTemplates(addExtensionsDialog.ExtensionTemplatesToAdd, extensionsAddedSB)
                    SetExtensionsStatus(extensionsAddedSB, Nothing)
                Catch ex As Exception When ReportWithoutCrash(ex, NameOf(AddExtensionsFromPropPage), NameOf(MyExtensibilityProjectService))
                Finally
                    _excludedTemplates = Nothing
                End Try
            End If
        End Sub

        ''' ;GetExtensionProjectItemGroups
        ''' <summary>
        ''' Return a list of all extension project item groups in the current project to display in "My Extensions" property page.
        ''' </summary>
        Public Function GetExtensionProjectItemGroups() As List(Of MyExtensionProjectItemGroup)
            Return _projectSettings.GetExtensionProjectItemGroups()
        End Function

        ''' ;ReferenceAdded
        ''' <summary>
        ''' VB Compiler calls this method (through VBReferenceChangedService) when a reference is added into the current project.
        ''' _dispReferencesEvents.ReferenceAdded also calls this method when a reference is added into the current project.
        ''' </summary>
        Public Sub ReferenceAdded(assemblyFullName As String)
            HandleReferenceChanged(AddRemoveAction.Add, NormalizeAssemblyFullName(assemblyFullName))
        End Sub

        ''' ;ReferenceRemoved
        ''' <summary>
        ''' VB Compiler calls this method (through VBReferenceChangedService) when a reference is removed from the current project.
        ''' </summary>
        Public Sub ReferenceRemoved(assemblyFullName As String)
            HandleReferenceChanged(AddRemoveAction.Remove, NormalizeAssemblyFullName(assemblyFullName))
        End Sub

        ''' ;RemoveExtensionsFromPropPage
        ''' <summary>
        ''' Remove the selected extensions from "My Extensions" property page.
        ''' </summary>
        Public Sub RemoveExtensionsFromPropPage(extensionProjectItemGroups As List(Of MyExtensionProjectItemGroup))
            Dim projectFilesRemovedSB As New StringBuilder()
            RemoveExtensionProjectItemGroups(extensionProjectItemGroups, projectFilesRemovedSB)
            SetExtensionsStatus(Nothing, projectFilesRemovedSB)
        End Sub

        ''' ;GetExtensionTemplateNameAndDescription
        ''' <summary>
        ''' Get the extension template name and description from the machine settings.
        ''' This is called by MyExtensiblityProjectSettings.
        ''' </summary>
        Public Sub GetExtensionTemplateNameAndDescription(
                id As String, version As Version, assemblyName As String,
                ByRef name As String, ByRef description As String)
            _extensibilitySettings.GetExtensionTemplateNameAndDescription(ProjectTypeID, _project,
                id, version, assemblyName,
                name, description)
        End Sub

        ''' ;Dispose
        ''' <summary>
        ''' Not a member of IDisposable method. This method is used to clean up
        ''' the project settings associated with this project service.
        ''' </summary>
        Public Sub Dispose()
            If _projectSettings IsNot Nothing Then
                _projectSettings.UnadviseTrackProjectDocumentsEvents()
                _projectSettings = Nothing
            End If
        End Sub

#End Region

#Region "Private support methods"

        ''' ;New
        ''' <summary>
        ''' Create a new project service.
        ''' </summary>
        Private Sub New(vbPackage As VBPackage, project As Project,
                projectHierarchy As IVsHierarchy, extensibilitySettings As MyExtensibilitySettings)
            Debug.Assert(vbPackage IsNot Nothing, "vbPackage Is Nothing")
            Debug.Assert(project IsNot Nothing, "project Is Nothing")
            Debug.Assert(projectHierarchy IsNot Nothing, "projectHierarchy Is Nothing")
            Debug.Assert(extensibilitySettings IsNot Nothing, "extensibilitySettings Is Nothing")

            _vbPackage = vbPackage
            _project = project
            _projectHierarchy = projectHierarchy
            _extensibilitySettings = extensibilitySettings
        End Sub

        ''' ;ProjectTypeID
        ''' <summary>
        ''' The GUID string used to query for project item templates, 
        ''' this will be __VSHPROPID2.VSHPROPID_AddItemTemplatesGuid, or __VSHPROPID.VSHPROPID_TypeGuid,
        ''' or EnvDTE.Project.Kind.
        ''' </summary>
        Private ReadOnly Property ProjectTypeID As String
            Get
                If _projectTypeID Is Nothing Then
                    Dim projGuid As Guid = Guid.Empty
                    Try
                        If _projectHierarchy IsNot Nothing Then
                            Dim hr As Integer
                            Try
                                hr = _projectHierarchy.GetGuidProperty(
                                    VSITEMID.ROOT, __VSHPROPID2.VSHPROPID_AddItemTemplatesGuid, projGuid)
                            Catch ex As Exception
                                hr = System.Runtime.InteropServices.Marshal.GetHRForException(ex)
                            End Try
                            If VSErrorHandler.Failed(hr) Then
                                hr = _projectHierarchy.GetGuidProperty(
                                    VSITEMID.ROOT, __VSHPROPID.VSHPROPID_TypeGuid, projGuid)
                                If VSErrorHandler.Failed(hr) Then
                                    projGuid = Guid.Empty
                                End If
                            End If
                        End If
                    Catch ex As Exception
                        ' This is a non-vital function - ignore if we fail to get the GUID...
                    End Try
                    If Guid.Empty.Equals(projGuid) Then
                        If _project IsNot Nothing Then
                            _projectTypeID = _project.Kind
                        End If
                    Else
                        _projectTypeID = projGuid.ToString("B").ToUpperInvariant()
                    End If
                End If

                Debug.Assert(Not StringIsNullEmptyOrBlank(_projectTypeID), "Could not get project type ID!")
                Return _projectTypeID
            End Get
        End Property

        ''' ;HandleReferenceChanged
        ''' <summary>
        ''' Use this method to handle reference add / remove events.
        ''' This method will verify that the change does not come from My Extensions page,
        ''' and will hook up to the Application's Idle event if necessary.
        ''' Adding / removing extensions at the same time of the events will lead to 
        ''' compiler and MSBuild errors.
        ''' </summary>
        Private Sub HandleReferenceChanged(
                changeType As AddRemoveAction, assemblyFullName As String)
            Debug.Assert(Not StringIsNullEmptyOrBlank(assemblyFullName), "assemblyFullName is NULL!")
            Debug.Assert(String.Equals(NormalizeAssemblyFullName(assemblyFullName), assemblyFullName,
                StringComparison.OrdinalIgnoreCase), "assemblyFullName not normalized!")
            Debug.Assert(_pendingAssemblyChangesList Is Nothing OrElse _pendingAssemblyChangesList.Count > 0,
                "m_AssemblyActionList in in valid state!")
            Debug.Assert(changeType = AddRemoveAction.Add OrElse changeType = AddRemoveAction.Remove,
                "Invalid changeType!")

            Dim pendingChangesExist As Boolean = _pendingAssemblyChangesList IsNot Nothing
            If changeType = AddRemoveAction.Add Then
                HandleReferenceAdded(assemblyFullName)
            Else
                HandleReferenceRemoved(assemblyFullName)
            End If

            If Not pendingChangesExist AndAlso _pendingAssemblyChangesList IsNot Nothing Then
                AddHandler Application.Idle, AddressOf HandleReferenceChangedOnIdle
            End If
        End Sub

        ''' ;HandleReferenceAdded
        ''' <summary>
        ''' Do not call this method directly, use HandleReferenceChanged.
        ''' </summary>
        Private Sub HandleReferenceAdded(assemblyFullName As String)
            Debug.Assert(Not StringIsNullEmptyOrBlank(assemblyFullName), "assemblyFullName is NULL!")
            Debug.Assert(String.Equals(NormalizeAssemblyFullName(assemblyFullName), assemblyFullName,
                StringComparison.OrdinalIgnoreCase), "assemblyFullName not normalized!")

            Dim addActivity As New AssemblyChange(assemblyFullName, AddRemoveAction.Add)
            ' Check the pending assembly changes list, if this assembly is in the list with Added status, 
            ' ignore this reference added.
            If _pendingAssemblyChangesList IsNot Nothing AndAlso _pendingAssemblyChangesList.Contains(addActivity) Then
                Exit Sub
            End If

            ' Check if the assembly has any extension templates associated with it.
            Dim extensionTemplates As List(Of MyExtensionTemplate) =
                _extensibilitySettings.GetExtensionTemplates(ProjectTypeID, _project, assemblyFullName)
            ' Check the list of templates being added directly from My Extension property page.
            ' These should be excluded from adding again due to references being added.
            If _excludedTemplates IsNot Nothing AndAlso extensionTemplates IsNot Nothing AndAlso _excludedTemplates.Count > 0 Then
                For Each excludedTemplate As MyExtensionTemplate In _excludedTemplates
                    If extensionTemplates.Contains(excludedTemplate) Then
                        extensionTemplates.Remove(excludedTemplate)
                    End If
                Next
            End If
            ' If there are no extension templates, no-op.
            If extensionTemplates Is Nothing OrElse extensionTemplates.Count <= 0 Then
                Exit Sub
            End If

            ' Prompt the user if necessary.
            Dim assemblyOption As AssemblyOption = _extensibilitySettings.GetAssemblyAutoAdd(assemblyFullName)

            Dim addExtensions As Boolean
            If assemblyOption = AssemblyOption.Prompt Then
                Dim addExtensionDialog As AssemblyOptionDialog =
                    AssemblyOptionDialog.GetAssemblyOptionDialog(
                    assemblyFullName, _vbPackage, extensionTemplates, AddRemoveAction.Add)
                addExtensions = addExtensionDialog.ShowDialog() = DialogResult.Yes
                If addExtensionDialog.OptionChecked Then
                    _extensibilitySettings.SetAssemblyAutoAdd(assemblyFullName, addExtensions)
                End If
            Else
                addExtensions = assemblyOption = MyExtensibility.AssemblyOption.Yes
            End If

            If addExtensions Then
                ' Queue the activity to the pending changes list.
                addActivity.ExtensionTemplates = extensionTemplates
                If _pendingAssemblyChangesList Is Nothing Then
                    _pendingAssemblyChangesList = New List(Of AssemblyChange)
                End If
                _pendingAssemblyChangesList.Add(addActivity)
            End If
        End Sub

        ''' ;HandleReferenceRemoved
        ''' <summary>
        ''' Do not call this method directly, use HandleReferenceChanged.
        ''' </summary>
        Private Sub HandleReferenceRemoved(assemblyFullName As String)
            Debug.Assert(Not StringIsNullEmptyOrBlank(assemblyFullName), "assemblyFullName is NULL!")
            Debug.Assert(String.Equals(NormalizeAssemblyFullName(assemblyFullName), assemblyFullName,
                StringComparison.OrdinalIgnoreCase), "assemblyFullName not normalized!")

            Dim removeActivity As New AssemblyChange(assemblyFullName, AddRemoveAction.Remove)
            Dim addActivity As New AssemblyChange(assemblyFullName, AddRemoveAction.Add)

            Dim previousRemoveActivityIndex As Integer = -1
            Dim previousAddActivityIndex As Integer = -1
            Dim projectItemGroupsToRemove As List(Of MyExtensionProjectItemGroup) = Nothing

            ' Check the pending assembly changes list, find the index of existing remove activity (if any) 
            ' and existing add activity (if any)
            If _pendingAssemblyChangesList IsNot Nothing Then
                Debug.Assert(_pendingAssemblyChangesList.IndexOf(removeActivity) =
                    _pendingAssemblyChangesList.LastIndexOf(removeActivity),
                    "m_PendingAssemblyChangesList should contain 1 instance of remove activity!")
                Debug.Assert(_pendingAssemblyChangesList.IndexOf(addActivity) =
                    _pendingAssemblyChangesList.LastIndexOf(addActivity),
                    "m_PendingAssemblyChangesList should contain 1 instance of add activity!")

                previousRemoveActivityIndex = _pendingAssemblyChangesList.IndexOf(removeActivity)
                previousAddActivityIndex = _pendingAssemblyChangesList.IndexOf(addActivity)
            End If

            If previousAddActivityIndex > 0 Then
                ' If assembly action list contains "Add Foo", continue to ask for remove of templates.
                Debug.Assert(previousRemoveActivityIndex < previousAddActivityIndex,
                    "m_PendingAssemblyChangesList should not have Add Foo continue by Remove Foo!")
            ElseIf previousRemoveActivityIndex > 0 Then
                ' If assembly action list does not contain "Add Foo" but contains "Remove Foo", no op.
                Exit Sub
            Else
                ' If assembly action list does not contain either, check for existing extension project items.
                projectItemGroupsToRemove = _projectSettings.GetExtensionProjectItemGroups(assemblyFullName)
                ' If there are no extension project items, no op.
                If projectItemGroupsToRemove Is Nothing OrElse projectItemGroupsToRemove.Count = 0 Then
                    Exit Sub
                End If
            End If

            ' Either there's a pending "Add Foo" activity or some project items to remove. Prompt if necessary.
            Dim assemblyOption As AssemblyOption = _extensibilitySettings.GetAssemblyAutoRemove(assemblyFullName)

            Dim removeExtensions As Boolean
            If assemblyOption = AssemblyOption.Prompt Then

                Dim itemList As IList

                If previousAddActivityIndex > 0 Then
                    itemList = _pendingAssemblyChangesList(previousAddActivityIndex).ExtensionTemplates
                Else
                    itemList = projectItemGroupsToRemove
                End If

                Dim removeExtensionDialog As AssemblyOptionDialog =
                    AssemblyOptionDialog.GetAssemblyOptionDialog(
                    assemblyFullName, _vbPackage, itemList, AddRemoveAction.Remove)
                removeExtensions = removeExtensionDialog.ShowDialog() = DialogResult.Yes
                If removeExtensionDialog.OptionChecked Then
                    _extensibilitySettings.SetAssemblyAutoRemove(assemblyFullName, removeExtensions)
                End If
            Else
                removeExtensions = assemblyOption = MyExtensibility.AssemblyOption.Yes
            End If

            If removeExtensions Then
                ' Queue the activity to the pending changes list.
                If previousAddActivityIndex > 0 Then
                    _pendingAssemblyChangesList.RemoveAt(previousAddActivityIndex)
                Else
                    If _pendingAssemblyChangesList Is Nothing Then
                        _pendingAssemblyChangesList = New List(Of AssemblyChange)
                    End If
                    removeActivity.ExtensionProjectItemGroups = projectItemGroupsToRemove
                    _pendingAssemblyChangesList.Add(removeActivity)
                End If
            End If
        End Sub

        ''' ;HandleReferenceChangedOnIdle
        ''' <summary>
        ''' When VS is idle, loop through the pending activities list and add / remove extensions.
        ''' </summary>
        Private Sub HandleReferenceChangedOnIdle(sender As Object, e As EventArgs)
            RemoveHandler Application.Idle, AddressOf HandleReferenceChangedOnIdle

            Debug.Assert(_pendingAssemblyChangesList IsNot Nothing, "Invalid pending assembly changes list!")

            Dim extensionAddedSB As New StringBuilder
            Dim extensionRemovedSB As New StringBuilder

            While _pendingAssemblyChangesList.Count > 0
                Dim asmActivity As AssemblyChange = _pendingAssemblyChangesList(0)
                If asmActivity.ChangeType = AddRemoveAction.Add Then
                    AddTemplates(asmActivity.ExtensionTemplates, extensionAddedSB)
                Else
                    RemoveExtensionProjectItemGroups(asmActivity.ExtensionProjectItemGroups, extensionRemovedSB)
                End If
                _pendingAssemblyChangesList.RemoveAt(0)
            End While

            _pendingAssemblyChangesList = Nothing
            SetExtensionsStatus(extensionAddedSB, extensionRemovedSB)
        End Sub

        ''' ;AddTemplates
        ''' <summary>
        ''' Add the given extension project item templates to the current project.
        ''' </summary>
        Private Sub AddTemplates(extensionTemplates As List(Of MyExtensionTemplate),
                extensionsAddedSB As StringBuilder)
            Debug.Assert(extensionTemplates IsNot Nothing AndAlso extensionTemplates.Count > 0, "Invalid extensionTemplates!")
            Debug.Assert(extensionsAddedSB IsNot Nothing, "Invalid extensionsAddedSB!")

            Using New WaitCursor()
                IdeStatusBar.StartProgress(Res.StatusBar_Add_Start, extensionTemplates.Count)

                For Each extensionTemplate As MyExtensionTemplate In extensionTemplates

                    ' If extension already exists in project, prompt user for replacing.
                    Dim existingExtProjItemGroup As MyExtensionProjectItemGroup =
                        _projectSettings.GetExtensionProjectItemGroup(extensionTemplate.ID)
                    If existingExtProjItemGroup IsNot Nothing Then
                        Dim replaceExtension As DialogResult = DesignerMessageBox.Show(_vbPackage,
                            String.Format(Res.ExtensionExists_Message,
                                existingExtProjItemGroup.ExtensionVersion.ToString(),
                                extensionTemplate.DisplayName,
                                extensionTemplate.Version.ToString()),
                            Res.ExtensionExists_Title,
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1)
                        If replaceExtension = DialogResult.No Then
                            Continue For
                        End If
                        _projectSettings.RemoveExtensionProjectItemGroup(existingExtProjItemGroup)
                    End If

                    ' Add extension project item template and set project item attributes.
                    Dim projectItemsAdded As List(Of ProjectItem) = _projectSettings.AddExtensionTemplate(extensionTemplate)
                    '' If succeeded, add the extension to our collection, otherwise, show warning message.
                    If projectItemsAdded IsNot Nothing AndAlso projectItemsAdded.Count > 0 Then
                        IdeStatusBar.UpdateProgress(String.Format(Res.StatusBar_Add_Progress, extensionTemplate.DisplayName))
                        If extensionsAddedSB.Length = 0 Then
                            extensionsAddedSB.Append(extensionTemplate.DisplayName)
                        Else
                            extensionsAddedSB.Append(Globalization.CultureInfo.CurrentUICulture.TextInfo.ListSeparator & extensionTemplate.DisplayName)
                        End If
                    End If
                Next

                RaiseEvent ExtensionChanged()
                IdeStatusBar.StopProgress(String.Empty)
            End Using
        End Sub

        ''' ;RemoveExtensionProjectItemGroups
        ''' <summary>
        ''' Remove the given extension code files from the project.
        ''' </summary>
        Private Sub RemoveExtensionProjectItemGroups(extensionProjectItemGroups As List(Of MyExtensionProjectItemGroup), projectFilesRemovedSB As StringBuilder)
            Debug.Assert(extensionProjectItemGroups IsNot Nothing AndAlso extensionProjectItemGroups.Count > 0, "Invalid extensionProjectFiles!")
            Debug.Assert(projectFilesRemovedSB IsNot Nothing, "Invalid projectFilesRemovedSB!")

            Using New WaitCursor()
                IdeStatusBar.StartProgress(Res.StatusBar_Remove_Start, extensionProjectItemGroups.Count)

                ' Using For Each here may lead to exception since garbage collector may decide to 
                ' clean up the removed extensionProjectFile object and enumeration will fail. 
                For i As Integer = 0 To extensionProjectItemGroups.Count - 1
                    Dim extensionProjectItemGroup As MyExtensionProjectItemGroup = extensionProjectItemGroups(i)
                    Try
                        _projectSettings.RemoveExtensionProjectItemGroup(extensionProjectItemGroup)
                        IdeStatusBar.UpdateProgress(String.Format(Res.StatusBar_Remove_Progress, extensionProjectItemGroup.DisplayName))
                        If projectFilesRemovedSB.Length = 0 Then
                            projectFilesRemovedSB.Append(extensionProjectItemGroup.DisplayName)
                        Else
                            projectFilesRemovedSB.Append(Globalization.CultureInfo.CurrentUICulture.TextInfo.ListSeparator & extensionProjectItemGroup.DisplayName)
                        End If
                    Catch ex As Exception When ReportWithoutCrash(ex, NameOf(RemoveExtensionProjectItemGroups), NameOf(MyExtensibilityProjectService))
                        ' Ignore exceptions.
                    End Try
                Next

                RaiseEvent ExtensionChanged()
                IdeStatusBar.StopProgress(String.Empty)
            End Using
        End Sub

        ''' ;SetExtensionsStatus
        ''' <summary>
        ''' Set the final status of VS status bar to the names of the items added / removed.
        ''' </summary>
        Private Shared Sub SetExtensionsStatus(addSB As StringBuilder, removeSB As StringBuilder)
            If addSB IsNot Nothing AndAlso addSB.Length > 0 Then
                If removeSB IsNot Nothing AndAlso removeSB.Length > 0 Then
                    IdeStatusBar.SetText(String.Format(Res.StatusBar_Add_Remove_Finish, addSB.ToString(), removeSB.ToString()))
                Else
                    IdeStatusBar.SetText(String.Format(Res.StatusBar_Add_Finish, addSB.ToString()))
                End If
            ElseIf removeSB IsNot Nothing AndAlso removeSB.Length > 0 Then
                IdeStatusBar.SetText(String.Format(Res.StatusBar_Remove_Finish, removeSB.ToString()))
            End If
        End Sub

        ''' ;m_ProjectSettings_ExtensionChanged
        ''' <summary>
        ''' Forward the ExtensionChanged event.
        ''' </summary>
        Private Sub OnProjectSettingsExtensionChanged() Handles _projectSettings.ExtensionChanged
            RaiseEvent ExtensionChanged()
        End Sub
#End Region

        ' Service provider, current project, project hierarchy and solution.
        Private ReadOnly _vbPackage As VBPackage
        Private ReadOnly _project As Project
        Private ReadOnly _projectHierarchy As IVsHierarchy
        Private _projectTypeID As String

        ' Extension templates information.
        Private ReadOnly _extensibilitySettings As MyExtensibilitySettings
        ' Managing extension code files in current project.
        Private WithEvents _projectSettings As MyExtensibilityProjectSettings

        ' Add / remove extension templates through compiler notification.
        '' List of pending assembly changes that will be handled on Idle loop.
        Private _pendingAssemblyChangesList As List(Of AssemblyChange)
        '' List of templates being added through My Extension property pages. 
        '' These should be excluded from any reference added events resulting from the templates being added.
        '' Scenario: Template T triggered by A, also contains A. Add template T explicitly should not 
        '' trigger template T again. 
        Private _excludedTemplates As List(Of MyExtensionTemplate)

#Region "Private Class AssemblyChange"
        ''' ;AssemblyChange
        ''' <summary>
        ''' Class to hold the information about an assembly change, including the normalized name,
        ''' the activity (add or remove) and the extensions associated with this change.
        ''' </summary>
        Private Class AssemblyChange
            Public Sub New(assemblyName As String, actionType As AddRemoveAction)
                Debug.Assert(Not StringIsNullEmptyOrBlank(assemblyName), "NULL assemblyName!")
                Debug.Assert(String.Equals(NormalizeAssemblyFullName(assemblyName), assemblyName,
                    StringComparison.OrdinalIgnoreCase), "assemblyName not normalized!")
                Debug.Assert(actionType = AddRemoveAction.Add Or actionType = AddRemoveAction.Remove,
                    "Invalid actionType!")

                _assemblyName = assemblyName
                _changeType = actionType
            End Sub

            Public ReadOnly Property AssemblyName As String
                Get
                    Return _assemblyName
                End Get
            End Property

            Public ReadOnly Property ChangeType As AddRemoveAction
                Get
                    Return _changeType
                End Get
            End Property

            Public Property ExtensionTemplates As List(Of MyExtensionTemplate)
                Get
                    Return _extensionTemplates
                End Get
                Set
                    _extensionTemplates = value
                End Set
            End Property

            Public Property ExtensionProjectItemGroups As List(Of MyExtensionProjectItemGroup)
                Get
                    Return _extensionProjectFiles
                End Get
                Set
                    _extensionProjectFiles = value
                End Set
            End Property

            Public Overrides Function Equals(obj As Object) As Boolean
                Dim asmActivity As AssemblyChange = TryCast(obj, AssemblyChange)
                If asmActivity IsNot Nothing Then
                    Return String.Equals(_assemblyName, asmActivity._assemblyName, StringComparison.OrdinalIgnoreCase) _
                        AndAlso _changeType = asmActivity._changeType
                End If
                Return MyBase.Equals(obj)
            End Function

            Private ReadOnly _assemblyName As String
            Private ReadOnly _changeType As AddRemoveAction
            Private _extensionTemplates As List(Of MyExtensionTemplate)
            Private _extensionProjectFiles As List(Of MyExtensionProjectItemGroup)
        End Class ' Private Class AssemblyChange
#End Region ' "Private Class AssemblyChange"

    End Class ' Friend Class MyExtensibilityProjectService

End Namespace
