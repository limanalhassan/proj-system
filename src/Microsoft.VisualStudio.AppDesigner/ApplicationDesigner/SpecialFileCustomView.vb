' Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

Imports Microsoft.VisualStudio.Shell.Interop

Namespace Microsoft.VisualStudio.Editors.ApplicationDesigner

    ''' <summary>
    ''' This class is a custom view for the project designer that displays
    '''   a message asking the user to click it in order to add a special file
    '''   to the project (using IVsProjectSpecialFiles).  It is currently used
    '''   to allow users to view the Resources and Settings tabs without actually
    '''   having a default resx or settings file in the project.  When they want to
    '''   create one, they just click on the message and the new file is created
    '''   using IVsProjectSpecialFiles, then the view is changed to display the editor
    '''   for the new file.
    ''' </summary>
    Public Class SpecialFileCustomView
        Inherits System.Windows.Forms.UserControl

        'The SpecialFileCustomViewProvider which created this class instance
        Private _viewProvider As SpecialFileCustomViewProvider

        ''' <summary>
        ''' Communicates to this class the SpecialFileCustomViewProvider which created it.
        '''   Used to access the special file ID, etc.
        ''' </summary>
        ''' <param name="ViewProvider"></param>
        Public Sub SetSite(ViewProvider As SpecialFileCustomViewProvider)
            _viewProvider = ViewProvider
            If ViewProvider IsNot Nothing Then
                LinkLabel.Text = _viewProvider.LinkText
            End If
        End Sub

        ''' <summary>
        ''' The link message has been clicked.  This means the user has requested that the
        '''   file be created.
        ''' </summary>
        ''' <param name="sender"></param>
        ''' <param name="e"></param>
        Private Sub LinkLabel_LinkClicked(sender As Object, e As System.Windows.Forms.LinkLabelLinkClickedEventArgs) Handles LinkLabel.LinkClicked
            CreateNewSpecialFile()
        End Sub

        ''' <summary>
        ''' Create the special file associated with this view.
        ''' </summary>
        Private Sub CreateNewSpecialFile()
            If _viewProvider IsNot Nothing Then
                Debug.Assert(_viewProvider.DesignerView IsNot Nothing)
                If _viewProvider.DesignerView IsNot Nothing _
                AndAlso _viewProvider.DesignerPanel IsNot Nothing _
                AndAlso _viewProvider.DesignerView.SpecialFiles IsNot Nothing Then
                    Dim ItemId As UInteger
                    Dim FileName As String = Nothing

                    Try
                        'Create the file
                        VSErrorHandler.ThrowOnFailure(
                            _viewProvider.DesignerView.SpecialFiles.GetFile(_viewProvider.SpecialFileId,
                                CUInt(__PSFFLAGS.PSFF_FullPath + __PSFFLAGS.PSFF_CreateIfNotExist),
                                ItemId, FileName)
                            )

                        'Set the filename
                        _viewProvider.DesignerPanel.MkDocument = FileName

                        'Remove the custom view
                        _viewProvider.DesignerPanel.CloseFrame() 'Note: this call may Dispose 'Me'
                        _viewProvider.DesignerPanel.CustomViewProvider = Nothing

                        'Now show without the custom view, which will cause the
                        '  real editor to appear on the file
                        _viewProvider.DesignerPanel.ShowDesigner(True)
                    Catch ex As Exception When AppDesCommon.ReportWithoutCrash(ex, NameOf(CreateNewSpecialFile), NameOf(SpecialFileCustomView))
                        _viewProvider.DesignerView.DsMsgBox(ex)
                    End Try
                End If
            End If
        End Sub
    End Class

End Namespace
