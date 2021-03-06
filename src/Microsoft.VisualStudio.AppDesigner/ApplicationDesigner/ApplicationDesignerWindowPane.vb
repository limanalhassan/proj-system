' Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

Imports System.ComponentModel.Design
Imports System.Drawing
Imports System.Reflection
Imports System.Runtime.InteropServices
Imports System.Windows.Forms

Imports Microsoft.Internal.Performance
Imports Microsoft.VisualStudio.Editors.AppDesDesignerFramework
Imports Microsoft.VisualStudio.Editors.AppDesInterop
Imports Microsoft.VisualStudio.Shell.Design
Imports Microsoft.VisualStudio.Shell.Interop

Imports Common = Microsoft.VisualStudio.Editors.AppDesCommon
Imports IOleDataObject = Microsoft.VisualStudio.OLE.Interop.IDataObject

Namespace Microsoft.VisualStudio.Editors.ApplicationDesigner

    Public NotInheritable Class ApplicationDesignerWindowPane
        Inherits DesignerWindowPane
        Implements IVsMultiViewDocumentView ' CONSIDER: Do we really need to implement this? GetActiveLogicalView always returns Guid.Empty
        Implements IVsWindowPaneCommit

        'The main view (the ApplicationDesignerView will be a child of this control)
        Private _view As ApplicationDesignerWindowPaneControl
        Private _viewHelper As CmdTargetHelper
        Private _uiShellService As IVsUIShell
        Private _uiShell2Service As IVsUIShell2
        Private _uiShell5Service As IVsUIShell5

        Private WithEvents _broadcastMessageEventsHelper As Common.ShellUtil.BroadcastMessageEventsHelper

        ''' <summary>
        ''' Creates a new WinformsWindowPane.
        ''' </summary>
        ''' <param name="surface"></param>
        Public Sub New(surface As DesignSurface)
            MyBase.New(surface)
            'Create our view control and hook its focus event.
            'Do not be tempted to create a container control here
            '   and use it for focus management!  It will steal key
            '   events from the shell and break things.
            '
            _view = New ApplicationDesignerWindowPaneControl()
            AddHandler _view.GotFocus, AddressOf OnViewFocus
            OnThemeChanged()

            _broadcastMessageEventsHelper = New Common.ShellUtil.BroadcastMessageEventsHelper(Me)

            AddHandler surface.Unloaded, AddressOf OnSurfaceUnloaded

        End Sub

        Public Overrides ReadOnly Property EditorView As Object
            Get
                Return Me
            End Get
        End Property

        Protected Overrides Sub Initialize()
            MyBase.Initialize()
            Common.Switches.TracePDFocus(TraceLevel.Warning, "ApplicationDesignerWindowPane.Initialize")

            Dim WindowFrame As IVsWindowFrame
            WindowFrame = TryCast(GetService(GetType(IVsWindowFrame)), IVsWindowFrame)
            If WindowFrame IsNot Nothing Then
                _viewHelper = New CmdTargetHelper(Me)
                'Must marshal ourselves as IUnknown wrapper
                VSErrorHandler.ThrowOnFailure(WindowFrame.SetProperty(__VSFPROPID.VSFPROPID_ViewHelper, New UnknownWrapper(_viewHelper)))

                ' make sure scrollbars in the view are not themed
                VSErrorHandler.ThrowOnFailure(WindowFrame.SetProperty(__VSFPROPID5.VSFPROPID_NativeScrollbarThemeMode, __VSNativeScrollbarThemeMode.NSTM_None))
            End If

        End Sub

        ''' <summary>
        '''     This method is called when Visual Studio needs to
        '''     evaluate which toolbox items should be enabled.  The
        '''     default implementation searches the service provider
        '''     for IVsToolboxUser and delegates.  If IVsToolboxUser
        '''     cannot be found this will search the service provider for
        '''     IToolboxService and call IsSupported.
        ''' </summary>
        ''' <remarks>
        ''' We override this so we can disable toolbox support for the project designer.
        ''' </remarks>
        Protected Overrides Function GetToolboxItemSupported(toolboxItem As IOleDataObject) As Boolean
            'PERF: NOTE: We don't need toolbox support for the project designer itself, and this takes up 
            '  performance, so we simply return False here for all toolbox items
            Return False
        End Function

        ''' <summary>
        ''' Our view always hands focus to its child.  
        ''' </summary>
        ''' <param name="sender"></param>
        ''' <param name="e"></param>
        Private Sub OnViewFocus(sender As Object, e As EventArgs)
            'Note: this sub never seems to get hit when controls count > 0
            Common.Switches.TracePDFocus(TraceLevel.Warning, "ApplicationDesignerWindowPane.OnViewFocus (Project Designer's window pane)")
            If _view IsNot Nothing AndAlso _view.Controls.Count > 0 Then
                Common.Switches.TracePDFocus(TraceLevel.Warning, "  ...setting focus to view's first child: """ & _view.Controls(0).Name & """" & " (view is type """ & _view.GetType.Name & """)")
                _view.Controls(0).Focus()
            Else
                Common.Switches.TracePDFocus(TraceLevel.Warning, "  ... ignoring - m_View currently has no children")
            End If
        End Sub

        Private Sub OnThemeChanged()
            _view.BackColor = Common.ShellUtil.GetProjectDesignerThemeColor(VsUIShell5Service, "Background", __THEMEDCOLORTYPE.TCT_Background, SystemColors.Window)
        End Sub

        Private Sub OnBroadcastMessageEventsHelperBroadcastMessage(msg As UInteger, wParam As IntPtr, lParam As IntPtr) Handles _broadcastMessageEventsHelper.BroadcastMessage
            Select Case msg
                Case Win32Constant.WM_PALETTECHANGED, Win32Constant.WM_SYSCOLORCHANGE, Win32Constant.WM_THEMECHANGED
                    OnThemeChanged()
            End Select
        End Sub

        Public Overrides ReadOnly Property Window As IWin32Window
            Get
                Return _view
            End Get
        End Property

        ''' <summary>
        '''  This takes our control UI and populates it with the
        '''  design surface.  If there was an error encountered
        '''  it will display the error control.
        ''' </summary>
        Private Sub PopulateView()

            Common.Switches.TracePDFocus(TraceLevel.Warning, "ApplicationDesignerWindowPane.PopulateView")
            Using New Common.WaitCursor()
                _view.Controls.Clear()
                Dim childView As Control = Nothing

                Try
                    'Note: this call will cause the ApplicationDesignerView to be created that
                    '  acts as the view for the window pane.
                    childView = TryCast(Surface.View, Control)
                    If TypeOf childView Is ApplicationDesignerView Then
                        Dim childAppDesignerView As ApplicationDesignerView = DirectCast(childView, ApplicationDesignerView)

                        Common.Switches.TracePDFocus(TraceLevel.Warning, "  ... ApplicationDesignerWindowPane.PopulateView: Adding viewChild to view")
                        childAppDesignerView.SuspendLayout()
                        childAppDesignerView.Dock = DockStyle.Fill
                        childAppDesignerView.ResumeLayout(False)
                        _view.Controls.Add(childAppDesignerView)

                        'Okay, go ahead and set up the designer view.  This adds all the tabs but does not
                        '  activate the project designer or add or activate any of the property page panels.
                        childAppDesignerView.InitView()
                    Else
                        Throw New InvalidOperationException("Only ApplicationDesignerView should be created by this window pane")
                    End If
                Catch loadError As Exception When Common.ReportWithoutCrash(loadError, "Got an exception trying to populate the project designer's view", NameOf(ApplicationDesignerWindowPane))
                    Do While TypeOf loadError Is TargetInvocationException AndAlso loadError.InnerException IsNot Nothing
                        loadError = loadError.InnerException
                    Loop

                    Dim message As String = loadError.Message
                    If message Is Nothing OrElse message.Length = 0 Then
                        message = loadError.ToString()
                    End If

                    If childView IsNot Nothing Then
                        childView.Dispose()
                    End If

                    childView = New ErrorControl With {
                        .Text = My.Resources.Designer.GetString(My.Resources.Designer.APPDES_ErrorLoading_Msg, message)
                    }
                End Try

                If childView Is Nothing Then
                    childView = New ErrorControl With {
                        .Text = My.Resources.Designer.GetString(My.Resources.Designer.APPDES_ErrorLoading_Msg, "")
                    }
                End If

                'If we haven't added the viewChild to m_View yet, do so now.
                If childView.Parent Is Nothing Then
                    Common.Switches.TracePDFocus(TraceLevel.Warning, "  ... ApplicationDesignerWindowPane.PopulateView: Adding viewChild to view")
                    childView.Dock = DockStyle.Fill
                    _view.Controls.Add(childView)
                End If

                'Don't set the active view here - OnInitializationComplete() will handle that (using
                '  the last-viewed property page).
                'SetActiveView(guidLogicalView)

                If AppDesignerView IsNot Nothing Then
                    'This activates the project designer and the initial page.
                    AppDesignerView.OnInitializationComplete()
                End If
            End Using
        End Sub

        ' <devdoc>
        '     Called when the surface has completed its unload.  If our view
        '     was populated with controls from the designer then the view
        '     should be empty now.  But, if it was populated with error
        '     information then it could still have the error control on it,
        '     in which case we should dispose it.
        ' </devdoc>
        Private Sub OnSurfaceUnloaded(sender As Object, e As EventArgs)
            If _view IsNot Nothing AndAlso _view.Controls.Count > 0 Then
                Dim controls As Control() = New Control(_view.Controls.Count - 1) {}
                _view.Controls.CopyTo(controls, 0)
                For Each c As Control In controls
                    c.Dispose()
                Next
            End If
        End Sub

        Private Function GetActiveView() As Guid
            If AppDesignerView IsNot Nothing Then
                Return AppDesignerView.ActiveView
            End If
            Return Guid.Empty
        End Function

        ''' <summary>
        ''' Sets the active view to the one that matches the given GUID.  If guid is empty or unrecognized, keeps the current tab.
        ''' </summary>
        ''' <param name="LogicalView"></param>
        Private Sub SetActiveView(LogicalView As Guid)
            If AppDesignerView IsNot Nothing Then
                AppDesignerView.ActiveView = LogicalView
            End If
        End Sub

        Public Function ActivateLogicalView(ByRef rguidLogicalView As Guid) As Integer Implements IVsMultiViewDocumentView.ActivateLogicalView
            Common.Switches.TracePDFocus(TraceLevel.Warning, "CodeMarker: perfMSVSEditorsActivateLogicalViewStart")
            Common.Switches.TracePDPerf("CodeMarker: perfMSVSEditorsActivateLogicalViewStart")
            CodeMarkers.Instance.CodeMarker(RoslynCodeMarkerEvent.PerfMSVSEditorsActivateLogicalViewStart)

            If AppDesignerView Is Nothing Then
                PopulateView()
                If Not rguidLogicalView.Equals(Guid.Empty) And Not rguidLogicalView.Equals(GetActiveView()) Then
                    SetActiveView(rguidLogicalView)
                End If
            Else
                'App designer already loaded - just navigate to the correct view
                AppDesignerView.AppDesignerAlreadyLoaded()
                If Not rguidLogicalView.Equals(Guid.Empty) And Not rguidLogicalView.Equals(GetActiveView()) Then
                    SetActiveView(rguidLogicalView)
                End If
            End If

            CodeMarkers.Instance.CodeMarker(RoslynCodeMarkerEvent.PerfMSVSEditorsActivateLogicalViewEnd)
            Common.Switches.TracePDFocus(TraceLevel.Warning, "CodeMarker: perfMSVSEditorsActivateLogicalViewEnd")
            Common.Switches.TracePDPerf("CodeMarker: perfMSVSEditorsActivateLogicalViewEnd")
        End Function

        Public Function GetActiveLogicalView(ByRef pguidLogicalView As Guid) As Integer Implements IVsMultiViewDocumentView.GetActiveLogicalView
            ' We are only supposed to return logical views that are registered in the registry - we can't register our 
            ' logical views, because we don't know the GUID of the property page beforehand...
            pguidLogicalView = Guid.Empty
        End Function

        Public Function IsLogicalViewActive(ByRef rguidLogicalView As Guid, ByRef pIsActive As Integer) As Integer Implements IVsMultiViewDocumentView.IsLogicalViewActive
            If rguidLogicalView.Equals(GetActiveView()) Then
                pIsActive = 1
            Else
                pIsActive = 0
            End If
        End Function

        ''' <summary>
        ''' The OnClose method is called by the base class in response to the ClosePane method on
        '''    IVsWindowPane.  The default implementation calls Dispose()
        ''' </summary>
        Protected Overrides Sub OnClose()
            MyBase.OnClose() 'Calls Dispose()
        End Sub

        Public ReadOnly Property AppDesignerView As ApplicationDesignerView
            Get
                If _view IsNot Nothing AndAlso _view.Controls.Count > 0 Then
                    Return TryCast(_view.Controls(0), ApplicationDesignerView)
                Else
                    Return Nothing
                End If
            End Get
        End Property

        ''' <summary>
        ''' Moves to the next tab in the project designer
        ''' </summary>
        Public Sub NextTab()
            If AppDesignerView IsNot Nothing Then
                AppDesignerView.SwitchTab(True)
            End If
        End Sub

        ''' <summary>
        ''' Moves to the previous tab in the project designer
        ''' </summary>
        Public Sub PrevTab()
            If AppDesignerView IsNot Nothing Then
                AppDesignerView.SwitchTab(False)
            End If
        End Sub

        ''' <summary>
        ''' Closes the application designer, but first prompts the user which of the open children
        '''   documents they want to save, and saves the ones selected.
        ''' </summary>
        Public Function ClosePromptSave() As Integer
            Dim hr As Integer = SaveChildren(__VSRDTSAVEOPTIONS.RDTSAVEOPT_DocClose Or __VSRDTSAVEOPTIONS.RDTSAVEOPT_PromptSave)
            If Not VSErrorHandler.Succeeded(hr) Then
                Return hr
            End If

            Return CloseFrameNoSave()
        End Function

        ''' <summary>
        ''' Closes the window frame for the project designer.  Any children with unsaved DocData will be discarded
        '''   without saving.
        ''' </summary>
        ''' <remarks>
        ''' This will cause the IVsWindowFrameNotify3.OnClose notification on CmdTargetHelper to fire.
        ''' </remarks>
        Public Function CloseFrameNoSave() As Integer
            If AppDesignerView IsNot Nothing Then
                Dim Frame As IVsWindowFrame = AppDesignerView.WindowFrame
                If Frame IsNot Nothing Then
                    AppDesignerView.NotifyShuttingDown()
                    'By the time we get here, we will have already saved all child documents if the user chose to save them
                    '  (via SaveChildren()), and if the user chose no, we must not save them now.  Therefore we pass
                    '  in NoSave.
                    'Note that this will cause IVsWindowFrameNotify3.OnClose on CmdTargetHelper to fire, but since we're passing
                    '  in NoSave, it will simply clean up and exit immediately.
                    Return CloseFrameInternal(Frame, __FRAMECLOSE.FRAMECLOSE_NoSave)
                End If
            End If

            Return NativeMethods.S_OK
        End Function

        ''' <summary>
        ''' Closes the window frame for the project designer
        ''' </summary>
        ''' <param name="WindowFrame"></param>
        ''' <param name="flags"></param>
        Private Shared Function CloseFrameInternal(WindowFrame As IVsWindowFrame, flags As __FRAMECLOSE) As Integer
            If WindowFrame IsNot Nothing Then
                Dim hr As Integer = WindowFrame.CloseFrame(Common.NoOverflowCUInt(flags))
                Return hr
            End If
        End Function

        ''' <summary>
        ''' Saves the DocDatas for all child DocViews of the project designer (i.e., the resource
        '''   editor and settings designer, if they're dirty, plus the doc datas of any property
        '''   page which proffers them up via IVsProjDesDocDataContainer).  It does *not* save
        '''   the project file.
        ''' </summary>
        ''' <param name="flags">Controls whether the user is prompted to save the items (which the user can
        '''   cancel), or whether they're saved without prompting, etc.
        ''' </param>
        ''' <returns>HRESULT failure code</returns>
        ''' <remarks>
        ''' We do saving of children specifically rather than letting devenv handle it, because otherwise we would
        '''   end up with multiple save dialogs instead of a single one.
        ''' </remarks>
        Public Function SaveChildren(flags As __VSRDTSAVEOPTIONS) As Integer
            If AppDesignerView IsNot Nothing Then
                ' we should commit pending changes before saving
                If Not AppDesignerView.CommitAnyPendingChanges() Then
                    Return NativeMethods.E_ABORT
                End If

                Dim items As VSSAVETREEITEM() = AppDesignerView.GetSaveTreeItems(flags)
                If items.Length = 0 Then
                    Return NativeMethods.S_OK
                End If
                Return VsUIShell2Service.SaveItemsViaDlg(CUInt(items.Length), items)
            End If

            Return NativeMethods.S_OK
        End Function

        ''' <summary>
        ''' Saves the project file associated with the project being displayed in the application designer, without
        '''   prompting the user.
        ''' </summary>
        ''' <returns>HRESULT error code</returns>
        ''' <remarks>Throws an exception on failure</remarks>
        Public Function SaveProjectFile() As Integer
            Dim hr As Integer = NativeMethods.E_FAIL

            If AppDesignerView IsNot Nothing Then
                Dim Project As EnvDTE.Project = AppDesignerView.DTEProject
                Debug.Assert(Project IsNot Nothing)
                If Project IsNot Nothing Then
                    'Can't just do Project.Save - that saves all files in the project
                    Dim ProjectFullName As String = Project.FullName
                    Dim rdt As IVsRunningDocumentTable = TryCast(GetService(GetType(IVsRunningDocumentTable)), IVsRunningDocumentTable)
                    If rdt IsNot Nothing Then
                        Dim punkDocData As New IntPtr(0)
                        Try
                            Dim Hierarchy As IVsHierarchy = Nothing
                            Dim ItemId As UInteger
                            Dim dwCookie As UInteger
                            hr = rdt.FindAndLockDocument(CUInt(_VSRDTFLAGS.RDT_NoLock), ProjectFullName, Hierarchy, ItemId, punkDocData, dwCookie)
                            If VSErrorHandler.Succeeded(hr) Then
                                'We only want to save the project file itself, not any other its children (any of the other files in the project)
                                'but we do want to force it so that the MyApplication file will be saved is necessary
                                hr = rdt.SaveDocuments(CUInt(__VSRDTSAVEOPTIONS.RDTSAVEOPT_ForceSave Or __VSRDTSAVEOPTIONS.RDTSAVEOPT_SaveNoChildren),
                                    Hierarchy, ItemId, dwCookie)

                                'Now that the project file has been saved, we need to tell the
                                '  pages that they're in a clean state.  We can't rely on
                                '  listening to the project file getting dirtied because some
                                '  pages don't save changes to the project file but to somewhere
                                '  else.
                                If AppDesignerView IsNot Nothing Then
                                    AppDesignerView.SetUndoRedoCleanStateOnAllPropertyPages()
                                End If
                            End If
                        Finally
                            If punkDocData <> IntPtr.Zero Then
                                Marshal.Release(punkDocData)
                            End If
                        End Try
                    End If
                End If
            End If

            Return hr
        End Function

        ''' <summary>
        ''' Retrieves the IVsUIShell service
        ''' </summary>
        Public ReadOnly Property VsUIShellService As IVsUIShell
            Get
                If _uiShellService Is Nothing Then
                    If Common.VBPackageInstance IsNot Nothing Then
                        _uiShellService = TryCast(Common.VBPackageInstance.GetService(GetType(IVsUIShell)), IVsUIShell)
                    Else
                        _uiShellService = TryCast(GetService(GetType(IVsUIShell)), IVsUIShell)
                    End If
                End If

                Return _uiShellService
            End Get
        End Property

        ''' <summary>
        ''' Retrieves the IVsUIShell2 service
        ''' </summary>
        Public ReadOnly Property VsUIShell2Service As IVsUIShell2
            Get
                If _uiShell2Service Is Nothing Then
                    Dim VsUiShell As IVsUIShell = VsUIShellService
                    If VsUiShell IsNot Nothing Then
                        _uiShell2Service = TryCast(VsUiShell, IVsUIShell2)
                    End If
                End If

                Return _uiShell2Service
            End Get
        End Property

        ''' <summary>
        ''' Retrieves the IVsUIShell5 service
        ''' </summary>
        Public ReadOnly Property VsUIShell5Service As IVsUIShell5
            Get
                If _uiShell5Service Is Nothing Then
                    Dim VsUiShell As IVsUIShell = VsUIShellService
                    If VsUiShell IsNot Nothing Then
                        _uiShell5Service = TryCast(VsUiShell, IVsUIShell5)
                    End If
                End If

                Return _uiShell5Service
            End Get
        End Property

#Region "IVsBackForwardNavigation"
#If 0 Then 'CONSIDER implementing
        Public Sub IsEqual(pFrame As Shell.Interop.IVsWindowFrame, bstrData As String, punk As Object, ByRef fReplaceSelf As Integer) Implements Shell.Interop.IVsBackForwardNavigation.IsEqual
            fReplaceSelf = 0
        End Sub

        Public Sub NavigateTo(pFrame As Shell.Interop.IVsWindowFrame, bstrData As String, punk As Object) Implements Shell.Interop.IVsBackForwardNavigation.NavigateTo

        End Sub
#End If
#End Region

#Region "IVsWindowPaneCommit"
        Public Function IVsWindowPaneCommit_CommitPendingEdit(ByRef pfCommitFailed As Integer) As Integer Implements IVsWindowPaneCommit.CommitPendingEdit
            pfCommitFailed = 0
            If AppDesignerView IsNot Nothing Then
                If Not AppDesignerView.CommitAnyPendingChanges() Then
                    pfCommitFailed = 1
                End If
            End If
        End Function
#End Region

        ''' <summary>
        ''' Clears the viewhelper on the frame (our view helper is a CmdTargetHelper class instance)
        ''' </summary>
        Private Sub ClearViewHelper()
            Dim WindowFrame As IVsWindowFrame
            WindowFrame = TryCast(GetService(GetType(IVsWindowFrame)), IVsWindowFrame)
            If WindowFrame IsNot Nothing Then
                _viewHelper = Nothing
                'Must marshal ourselves as IUnknown wrapper
                VSErrorHandler.ThrowOnFailure(WindowFrame.SetProperty(__VSFPROPID.VSFPROPID_ViewHelper, New UnknownWrapper(Nothing)))
            End If
        End Sub

        Protected Overrides Function PreProcessMessage(ByRef m As Message) As Boolean
            Common.Switches.TracePDMessageRouting(TraceLevel.Warning, "ApplicationDesignerWindowPane.PreProcessMessage", m)
            Return MyBase.PreProcessMessage(m)
        End Function

#Region "Dispose/IDisposable"
        ''' <summary>
        ''' Unhook events and prepare for takeoff
        ''' </summary>
        ''' <param name="disposing"></param>
        Private Overloads Sub Dispose(disposing As Boolean)
            Dim disposedView As Control = _view

            Try
                If disposing Then
                    ClearViewHelper()

                    ' The base class will try to dispose our view if
                    ' it exists.  We want to take care of that here after the
                    ' surface is disposed so the design surface can have a
                    ' chance to systematically tear down controls and components.
                    ' So set the view to null here, but remember it in
                    ' disposedView.  After we're done calling base.Dispose()
                    ' will take care of our own stuff.
                    '
                    _uiShellService = Nothing
                    _uiShell2Service = Nothing
                    _uiShell5Service = Nothing
                    _view = Nothing
                    Dim DesSurface As DesignSurface = Surface
                    If DesSurface IsNot Nothing Then
                        RemoveHandler DesSurface.Unloaded, AddressOf OnSurfaceUnloaded
                    End If
                    If _broadcastMessageEventsHelper IsNot Nothing Then
                        _broadcastMessageEventsHelper.Dispose()
                        _broadcastMessageEventsHelper = Nothing
                    End If
                End If

            Finally
                If disposing AndAlso disposedView IsNot Nothing Then
                    RemoveHandler disposedView.GotFocus, AddressOf OnViewFocus
                    disposedView.Dispose()
                End If
            End Try

        End Sub
#End Region

    End Class

End Namespace
