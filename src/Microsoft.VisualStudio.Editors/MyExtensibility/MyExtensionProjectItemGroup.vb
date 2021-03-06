' Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

Option Strict On
Option Explicit On
Imports EnvDTE

Imports Microsoft.VisualStudio.Editors.MyExtensibility.MyExtensibilityUtil

Namespace Microsoft.VisualStudio.Editors.MyExtensibility

    ''' ;MyExtensionProjectItemGroup
    ''' <summary>
    ''' Contains information about a My Extension in a project,
    ''' including its extension ID, version, name, description; 
    ''' and the list of physical files in the project.
    ''' </summary>
    Friend Class MyExtensionProjectItemGroup
        Implements INamedDescribedObject

        ''' ;New
        ''' <summary>
        ''' fileName, extensionID and extensionVersion are required.
        ''' </summary>
        Public Sub New(
                extensionID As String, extensionVersion As Version,
                extensionName As String, extensionDescription As String)
            Debug.Assert(Not StringIsNullEmptyOrBlank(extensionID), "Invalid extensionID!")
            Debug.Assert(extensionVersion IsNot Nothing, "Invalid extensionVersion!")

            _extensionID = extensionID
            _extensionVersion = extensionVersion
            _extensionName = extensionName
            _extensionDescription = extensionDescription
        End Sub

        Public ReadOnly Property ExtensionProjectItems As List(Of ProjectItem)
            Get
                Return _projectItems
            End Get
        End Property

        Public ReadOnly Property ExtensionID As String
            Get
                Return _extensionID
            End Get
        End Property

        Public ReadOnly Property ExtensionVersion As Version
            Get
                Return _extensionVersion
            End Get
        End Property

        Public ReadOnly Property ExtensionDescription As String Implements INamedDescribedObject.Description
            Get
                Return _extensionDescription
            End Get
        End Property

        Public ReadOnly Property DisplayName As String Implements INamedDescribedObject.DisplayName
            Get
                If StringIsNullEmptyOrBlank(_extensionName) Then
                    Return _extensionID
                Else
                    Return _extensionName
                End If
            End Get
        End Property

        Public Sub AddProjectItem(projectItem As ProjectItem)
            If projectItem IsNot Nothing Then
                If _projectItems Is Nothing Then
                    _projectItems = New List(Of ProjectItem)
                End If
                _projectItems.Add(projectItem)
            End If
        End Sub

        Public Function IDEquals(id As String) As Boolean
            Return String.Equals(_extensionID, id, StringComparison.Ordinal)
        End Function

        Private Sub New()
        End Sub

        Private ReadOnly _extensionID As String
        Private ReadOnly _extensionVersion As Version
        Private ReadOnly _extensionName As String
        Private ReadOnly _extensionDescription As String
        Private _projectItems As List(Of ProjectItem)
    End Class

End Namespace

