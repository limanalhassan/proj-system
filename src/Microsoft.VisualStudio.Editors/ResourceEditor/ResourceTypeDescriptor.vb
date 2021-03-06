' Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

Option Explicit On
Option Strict On
Option Compare Binary
Imports System.ComponentModel

Namespace Microsoft.VisualStudio.Editors.ResourceEditor

    ''' <summary>
    ''' Provides type description for Resource class.
    ''' </summary>
    ''' <remarks>
    ''' This class inherits from CustomTypeDescriptor, which saves us from implementing everything on ICustomTypeDescriptor.
    ''' We only override GetProperties() methods to return the properties of the Resource class we want the Property 
    '''   Window to display.
    ''' </remarks>
    Friend NotInheritable Class ResourceTypeDescriptor
        Inherits CustomTypeDescriptor

        ' The instance of the Resource that we're providing type description information for.
        Private ReadOnly _instance As Resource

        '======================================================================
        '= Constructors =                                                     =
        '======================================================================

        ''' <summary>
        '''  Constructs a new instance of ResourceTypeDescriptor with the specified Resource.
        ''' </summary>
        ''' <param name="Instance">An instance of Resource class.</param>
        Public Sub New(Instance As Resource)
            MyBase.New()

            Debug.Assert(Instance IsNot Nothing, "Instance is Nothing!!!")
            _instance = Instance
        End Sub

        '======================================================================
        '= Properties =                                                       =
        '======================================================================

        ''' <summary>
        '''  Returns the properties of this Resource instance.
        ''' </summary>
        ''' <returns>A PropertyDescriptorCollection that represents the properties for this Resource instance.</returns>
        ''' <remarks>We call the Resource instance to describe its properties itself.</remarks>
        Public Overrides Function GetProperties() As PropertyDescriptorCollection
            Return _instance.GetProperties
        End Function

        ''' <summary>
        '''  Returns the properties for this Resource instance using the attribute array as a filter.
        ''' </summary>
        ''' <param name="attributes">An array of type Attribute that is used as a filter.</param>
        ''' <returns>A PropertyDescriptorCollection that represents the filtered properties for this Resource instance.</returns>
        ''' <remarks>We don't have any attributes on the properties except possibly the Category attribute.</remarks>
        Public Overrides Function GetProperties(attributes() As Attribute) As PropertyDescriptorCollection
            Return _instance.GetProperties
        End Function

        ''' <summary>
        ''' The GetComponentName method returns the name of the component instance 
        '''     this type descriptor is describing.
        ''' </summary>
        Public Overrides Function GetComponentName() As String
            Debug.Assert(_instance.Name <> "")
            Return _instance.Name
        End Function

        ''' <summary>
        ''' The GetClassName method returns the name of the resource type
        '''     this type descriptor is describing.
        ''' </summary>
        Public Overrides Function GetClassName() As String
            ' CONSIDER: We should return Category here...
            If _instance IsNot Nothing Then
                Dim typeName As String = _instance.FriendlyValueTypeName
                Dim idx As Integer = typeName.LastIndexOf("."c)
                If idx > 0 Then
                    typeName = typeName.Substring(idx + 1)
                End If
                Return typeName
            Else
                Return String.Empty
            End If
        End Function

    End Class
End Namespace

