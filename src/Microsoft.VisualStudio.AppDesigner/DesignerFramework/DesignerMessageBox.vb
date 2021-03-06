' Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

Imports System.Reflection
Imports System.Windows.Forms
Imports System.Windows.Forms.Design

Imports Microsoft.VisualStudio.Shell.Interop

Namespace Microsoft.VisualStudio.Editors.AppDesDesignerFramework

    '**************************************************************************
    ';DesignerMessageBox
    '
    'Remarks:
    '   This class provides the correct way of doing message box for designer packages.
    '**************************************************************************
    ' This class is converted from <DD>\wizard\vsdesigner\designer\microsoft\vsdesigner\VSDMessageBox.cs.
    Public Class DesignerMessageBox

        Private Const MaxErrorMessageLength As Integer = 600

        '= Public =============================================================

        ''' <summary>
        ''' Displays a message box for a specified exception, caption, buttons, icons, default button and help link.
        ''' </summary>
        ''' <param name="RootDesigner">A root designer inherited from BaseRootDesigner, which has the ability to get services.</param>
        ''' <param name="Caption">The text to display in the title bar of the message box.</param>
        ''' <param name="HelpLink">Link to the help topic for this message box.</param>
#Disable Warning RS0026 ' Do not add multiple public overloads with optional parameters
        Public Shared Function Show(RootDesigner As BaseRootDesigner, Message As String,
                Caption As String, Buttons As MessageBoxButtons, Icon As MessageBoxIcon,
                Optional DefaultButton As MessageBoxDefaultButton = MessageBoxDefaultButton.Button1,
                Optional HelpLink As String = Nothing
        ) As DialogResult
#Enable Warning RS0026 ' Do not add multiple public overloads with optional parameters
            Return Show(DirectCast(RootDesigner, IServiceProvider), Message, Caption, Buttons, Icon, DefaultButton, HelpLink)
        End Function 'Show

        ''' <summary>
        ''' Displays a message box for a specified exception, caption, buttons, icons, default button and help link.
        ''' </summary>
        ''' <param name="ServiceProvider">The IServiceProvider, used to get devenv shell as the parent of the message box.</param>
        ''' <param name="ex">The exception to include in the message.</param>
        ''' <param name="Caption">The text to display in the title bar of the message box.</param>
        ''' <param name="HelpLink">Link to the help topic for this message box.</param>
#Disable Warning RS0026 ' Do not add multiple public overloads with optional parameters
        Public Shared Sub Show(ServiceProvider As IServiceProvider, ex As Exception,
                Caption As String, Optional HelpLink As String = Nothing)
#Enable Warning RS0026 ' Do not add multiple public overloads with optional parameters
            Show(ServiceProvider, Nothing, ex, Caption, HelpLink)
        End Sub

        ''' <summary>
        ''' Displays a message box for a specified exception, caption, buttons, icons, default button and help link.
        ''' </summary>
        ''' <param name="ServiceProvider">The IServiceProvider, used to get devenv shell as the parent of the message box.</param>
        ''' <param name="Message">The text to display in the message box.</param>
        ''' <param name="ex">The exception to include in the message.  The exception's message will be on a second line after errorMessage.</param>
        ''' <param name="Caption">The text to display in the title bar of the message box.</param>
        ''' <param name="HelpLink">Link to the help topic for this message box.</param>
        ''' <remarks>
        ''' The exception's message will be on a second line after errorMessage.
        ''' </remarks>
#Disable Warning RS0026 ' Do not add multiple public overloads with optional parameters
        Public Shared Sub Show(ServiceProvider As IServiceProvider, Message As String, ex As Exception,
                Caption As String, Optional HelpLink As String = Nothing)
#Enable Warning RS0026 ' Do not add multiple public overloads with optional parameters

            If ex Is Nothing Then
                Debug.Fail("ex should not be Nothing")
                Return
            End If

            'Pull out the original exception from target invocation exceptions (happen during serialization, etc.)
            If TypeOf ex Is TargetInvocationException Then
                ex = ex.InnerException
            End If

            If AppDesCommon.IsCheckoutCanceledException(ex) Then
                'The user knows they just canceled the checkout.  We don't have to tell them.  (Yes, other editors and the
                '  Fx framework itself does it this way, too.)
                Return
            End If

            If HelpLink = "" AndAlso ex IsNot Nothing Then
                HelpLink = ex.HelpLink
            End If

            'Add the exception text to the message
            If ex IsNot Nothing Then
                If Message = "" Then
                    Message = ex.Message
                Else
                    Message = Message & vbCrLf & ex.Message
                End If

                ' limit the length of message to prevent a bad layout.
                If Message.Length > MaxErrorMessageLength Then
                    Message = Message.Substring(0, MaxErrorMessageLength)
                End If
            Else
                Debug.Assert(Message <> "")
            End If

            Show(ServiceProvider, Message, Caption, MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1, HelpLink)
        End Sub

        ''' <summary>
        ''' Displays a message box for a specified exception, caption, buttons, icons, default button and help link.
        ''' </summary>
        ''' <param name="ServiceProvider">The IServiceProvider, used to get devenv shell as the parent of the message box.</param>
        ''' <param name="Message">The text to display in the message box.</param>
        ''' <param name="Caption">The text to display in the title bar of the message box.</param>
        ''' <param name="Buttons">One of the MessageBoxButtons values that specifies which buttons to display in the message box.</param>
        ''' <param name="Icon">One of the MessageBoxIcon values that specifies which icon to display in the message box.</param>
        ''' <param name="HelpLink">Link to the help topic for this message box.</param>
        ''' <param name="DefaultButton">One of the MessageBoxDefaultButton values that specifies the default button of the message box.</param>
#Disable Warning RS0026 ' Do not add multiple public overloads with optional parameters
        Public Shared Function Show(ServiceProvider As IServiceProvider, Message As String,
                Caption As String, Buttons As MessageBoxButtons, Icon As MessageBoxIcon,
                Optional DefaultButton As MessageBoxDefaultButton = MessageBoxDefaultButton.Button1,
                Optional HelpLink As String = Nothing
        ) As DialogResult
#Enable Warning RS0026 ' Do not add multiple public overloads with optional parameters
            Return ShowHelper(ServiceProvider, Message, Caption, Buttons, Icon, DefaultButton, HelpLink)
        End Function 'Show

        ''' <summary>
        ''' Displays a message box for a specified exception, caption, buttons, icons, default button and help link.
        ''' </summary>
        ''' <param name="ServiceProvider">The IServiceProvider, used to get devenv shell as the parent of the message box.</param>
        ''' <param name="Message">The text to display in the message box.</param>
        ''' <param name="Caption">The text to display in the title bar of the message box.</param>
        ''' <param name="Buttons">One of the MessageBoxButtons values that specifies which buttons to display in the message box.</param>
        ''' <param name="Icon">One of the MessageBoxIcon values that specifies which icon to display in the message box.</param>
        ''' <param name="HelpLink">Link to the help topic for this message box.</param>
        ''' <param name="DefaultButton">One of the MessageBoxDefaultButton values that specifies the default button of the message box.</param>
        Private Shared Function ShowHelper(ServiceProvider As IServiceProvider, Message As String,
                Caption As String, Buttons As MessageBoxButtons, Icon As MessageBoxIcon,
                Optional DefaultButton As MessageBoxDefaultButton = MessageBoxDefaultButton.Button1,
                Optional HelpLink As String = Nothing
        ) As DialogResult

            If HelpLink = "" Then
                'Giving an empty string will show the Help button, we don't want it. Null won't.
                HelpLink = Nothing
            End If

            If Caption = "" Then
                Caption = Nothing 'Causes "Error" to be the caption...
            End If

            If ServiceProvider IsNot Nothing Then
                Try
                    Return ShowInternal(CType(ServiceProvider.GetService(GetType(IUIService)), IUIService),
                        CType(ServiceProvider.GetService(GetType(IVsUIShell)), IVsUIShell),
                        Message, Caption, Buttons, Icon, DefaultButton, HelpLink)
                Catch ex As Exception When AppDesCommon.ReportWithoutCrash(ex, NameOf(ShowHelper), NameOf(DesignerMessageBox))
                End Try
            Else
                Debug.Fail("ServiceProvider is Nothing! Message box won't have parent!")
            End If

            ' If there is no IServiceProvider, message box has no parent.
            Return MessageBox.Show(Nothing, Message, Caption, Buttons, Icon, DefaultButton)
        End Function 'Show

        '= PROTECTED ==========================================================

        '**************************************************************************
        ';ShowInternal
        '
        'Summary:
        '   Our implementation to display a message box with the specified message, caption, buttons, icons, default button,
        '   and help link. Also correctly set the parent of the message box.
        'Params:
        '   UIService: The IUIService class used to show message in case there is no help link.
        '   VsUIShell: The VsUIShell class used to show message in case there is a help link.
        '   Other params: see above.
        'Returns:
        '   One of the DialogResult values.
        'Remarks: (from VSDMessageBox)
        '   The current implementation prevents us from specifying a caption when a helpLink is provided. This is because 
        '   IVsUIShell.ShowMessageBox will display the caption as part of the message itself, not in the title bar.
        '   So instead of this we cut this feature. When no help is needed, a standard MessageBox will be shown 
        '   but parented using the service provider if available, the caption will also be shown normally.
        '**************************************************************************
        Protected Shared Function ShowInternal(UIService As IUIService, VsUIShell As IVsUIShell,
                Message As String, Caption As String, Buttons As MessageBoxButtons,
                Icon As MessageBoxIcon, DefaultButton As MessageBoxDefaultButton, HelpLink As String) _
        As DialogResult
            If VsUIShell IsNot Nothing Then
                Dim Guid As Guid = Guid.Empty

                Dim OLEButtons As OLEMSGBUTTON = CType(Buttons, OLEMSGBUTTON)
                Dim OLEDefaultButton As OLEMSGDEFBUTTON = OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST
                Select Case DefaultButton
                    Case MessageBoxDefaultButton.Button1
                        OLEDefaultButton = OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST
                    Case MessageBoxDefaultButton.Button2
                        OLEDefaultButton = OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_SECOND
                    Case MessageBoxDefaultButton.Button2
                        OLEDefaultButton = OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_THIRD
                End Select

                'We pass in Nothing for the caption because if we pass in an actual caption,
                '  IVsUIShell doesn't show it as the actual caption, but just as an extra line
                '  at the front of the text.  The caption is always chosen by IVsUIShell, which
                '  is the best thing anyway, we shouldn't have to provide a caption (it changes
                '  by installed SKU/product, for instance).
                Dim Result As Integer
                VSErrorHandler.ThrowOnFailure(VsUIShell.ShowMessageBox(0, Guid, Nothing, Message, HelpLink, 0,
                        OLEButtons, OLEDefaultButton, MessageBoxIconToOleIcon(Icon), CInt(False), Result))
                Return CType(Result, DialogResult)
            Else
                Debug.Fail("Could not retreive IVsUIShell, message box will not be parented")
            End If

            ' Either UIService or VsUIShell does not exist, show message box without parent.
            Return MessageBox.Show(Nothing, Message, Caption, Buttons, Icon, DefaultButton)
        End Function 'ShowInternal

        '= PRIVATE ============================================================

        '**************************************************************************
        ';MessageBoxIconToOleIcon
        '
        'Summary:
        '   Convert the values from Framework's MessageBoxIcon enum to OLEMSGICON.
        '   The reason is IVsUIShell.ShowMessageBox does not accept values from MessageBoxIcon or WinUser.h,
        '       but values from OLEMSGICON in oleipc.h
        'Params:
        '   Icon: One of the MessageBoxIcon values.
        'Returns:
        '   The appropriate OLEMSGICON value.
        '**************************************************************************
        Private Shared Function MessageBoxIconToOleIcon(icon As MessageBoxIcon) As OLEMSGICON
            Select Case icon
                Case MessageBoxIcon.Error
                    'case MessageBoxIcon.Hand:
                    'case MessageBoxIcon.Stop:
                    Return OLEMSGICON.OLEMSGICON_CRITICAL
                Case MessageBoxIcon.Exclamation
                    'case MessageBoxIcon.Warning:
                    Return OLEMSGICON.OLEMSGICON_WARNING
                'case MessageBoxIcon.Asterisk:
                Case MessageBoxIcon.Information
                    Return OLEMSGICON.OLEMSGICON_INFO
                Case MessageBoxIcon.Question
                    Return OLEMSGICON.OLEMSGICON_QUERY
                Case Else
                    Return OLEMSGICON.OLEMSGICON_NOICON
            End Select
        End Function 'MessageBoxIconToOleIcon 

    End Class 'DesignerMessageBox 
End Namespace
