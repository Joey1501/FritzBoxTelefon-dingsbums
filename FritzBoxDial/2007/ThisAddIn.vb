﻿Imports Microsoft.Office.Core
Public Class ThisAddIn
#Region "Office 2003 & 2007 Eventhandler"
#If OVer < 14 Then
    Private WithEvents eBtnWaehlen As Office.CommandBarButton
    Private WithEvents eBtnDirektwahl As Office.CommandBarButton
    Private WithEvents eBtnAnrMonitor As Office.CommandBarButton
    Private WithEvents eBtnAnzeigen As Office.CommandBarButton
    Private WithEvents eBtnJournalimport As Office.CommandBarButton
    Private WithEvents eBtnLeitungsbelegung As Office.CommandBarButton
    Private WithEvents eBtnEinstellungen As Office.CommandBarButton
    Private WithEvents eBtnAnrMonNeuStart As Office.CommandBarButton

    Private WithEvents ePopWwdh As Office.CommandBarPopup
    Private WithEvents ePopWwdhDel As Office.CommandBarButton
    Private WithEvents ePopWwdh01, ePopWwdh02, ePopWwdh03, ePopWwdh04, ePopWwdh05 As Office.CommandBarButton
    Private WithEvents ePopWwdh06, ePopWwdh07, ePopWwdh08, ePopWwdh09, ePopWwdh10 As Office.CommandBarButton

    Private WithEvents ePopAnr As Office.CommandBarPopup
    Private WithEvents ePopAnrDel As Office.CommandBarButton
    Private WithEvents ePopAnr01, ePopAnr02, ePopAnr03, ePopAnr04, ePopAnr05 As Office.CommandBarButton
    Private WithEvents ePopAnr06, ePopAnr07, ePopAnr08, ePopAnr09, ePopAnr10 As Office.CommandBarButton

    Private WithEvents ePopVIP As Office.CommandBarPopup
    Private WithEvents ePopVIPDel As Office.CommandBarButton
    Private WithEvents ePopVIP01, ePopVIP02, ePopVIP03, ePopVIP04, ePopVIP05 As Office.CommandBarButton
    Private WithEvents ePopVIP06, ePopVIP07, ePopVIP08, ePopVIP09, ePopVIP10 As Office.CommandBarButton
#End If
#If OVer = 11 Then
    Private WithEvents iPopRWS As Office.CommandBarPopup
    Private WithEvents iBtnWwh As Office.CommandBarButton
    Private WithEvents iBtnRWSDasOertliche As Office.CommandBarButton
    Private WithEvents iBtnRws11880 As Office.CommandBarButton
    Private WithEvents iBtnRWSDasTelefonbuch As Office.CommandBarButton
    Private WithEvents iBtnRWStelSearch As Office.CommandBarButton
    Private WithEvents iBtnRWSAlle As Office.CommandBarButton
    Private WithEvents iBtnKontakterstellen As Office.CommandBarButton
    Private WithEvents iBtnVIP As Office.CommandBarButton
    Private WithEvents iBtnUpload As Office.CommandBarButton
#End If
#End Region

    Private WithEvents oInsps As Outlook.Inspectors
    Friend Shared ListofOpenContacts As New Generic.List(Of ContactSaved)
    Public Shared Event PowerModeChanged As Microsoft.Win32.PowerModeChangedEventHandler

#Region "Eigene Formulare"
    Private F_AnrListImport As formImportAnrList
    Private F_Cfg As formCfg
    Private F_Init As formInit
#End Region

#Region "Properties"
    ''' <summary>
    ''' Gibt die Versionsnummer des Addins zurück.
    ''' </summary>
    ''' <value>System.Reflection.Assembly.GetExecutingAssembly.GetName.Version</value>
    ''' <returns>.Major.Minor.Build</returns>
    Friend Shared ReadOnly Property Version() As String
        Get
            With System.Reflection.Assembly.GetExecutingAssembly.GetName.Version
                Return .Major & "." & .Minor & "." & .Build
            End With
        End Get
    End Property

    ''' <summary>
    ''' Gibt die aktuelle Outlook-Application zurück.
    ''' </summary>
    Private Shared oApp As Outlook.Application
    Friend Shared Property P_oApp() As Outlook.Application
        Get
            Return oApp
        End Get
        Set(ByVal value As Outlook.Application)
            oApp = value
        End Set
    End Property

    ''' <summary>
    ''' Rückgabewert für die Klasse DataProvider 
    ''' </summary>
    Private C_DP As DataProvider
    Friend Property P_DP() As DataProvider
        Get
            Return C_DP
        End Get
        Set(ByVal value As DataProvider)
            C_DP = value
        End Set
    End Property

    ''' <summary>
    ''' Rückgabewert für die Klasse Helfer 
    ''' </summary>
    Private C_HF As Helfer
    Friend Property P_HF() As Helfer
        Get
            Return C_HF
        End Get
        Set(ByVal value As Helfer)
            C_HF = value
        End Set
    End Property

    ''' <summary>
    ''' Rückgabewert für die Klasse KontaktFunktionen 
    ''' </summary>
    Private C_KF As KontaktFunktionen
    Friend Property P_KF() As KontaktFunktionen
        Get
            Return C_KF
        End Get
        Set(ByVal value As KontaktFunktionen)
            C_KF = value
        End Set
    End Property

    ''' <summary>
    ''' Rückgabewert für die Klasse GraphicalUserInterface 
    ''' </summary>
    Private C_GUI As GraphicalUserInterface
    Friend Property P_GUI() As GraphicalUserInterface
        Get
            Return C_GUI
        End Get
        Set(ByVal value As GraphicalUserInterface)
            C_GUI = value
        End Set
    End Property

    ''' <summary>
    ''' Rückgabewert für die Klasse AnrufMonitor 
    ''' </summary>
    Private C_AnrMon As AnrufMonitor
    Friend Property P_AnrMon() As AnrufMonitor
        Get
            Return C_AnrMon
        End Get
        Set(ByVal value As AnrufMonitor)
            C_AnrMon = value
        End Set
    End Property

    ''' <summary>
    ''' Rückgabewert für die Klasse XML 
    ''' </summary>
    Private C_XML As XML
    Friend Property P_XML() As XML
        Get
            Return C_XML
        End Get
        Set(ByVal value As XML)
            C_XML = value
        End Set
    End Property

    Private C_Fbox As FritzBox
    Friend Property P_FBox() As FritzBox
        Get
            Return C_Fbox
        End Get
        Set(ByVal value As FritzBox)
            C_Fbox = value
        End Set
    End Property
#End Region

#If Not OVer = 11 Then
    Protected Overrides Function CreateRibbonExtensibilityObject() As IRibbonExtensibility
        F_Init = New formInit(P_GUI, P_KF, P_HF, P_DP, P_AnrMon, P_XML, P_FBox)
        Return P_GUI
    End Function
#End If

    ''' <summary>
    ''' Startet den Anrufmonitor nach dem Aufwachen nach dem Standby neu, bzw. Beendet ihn, falls ein Standyby erkannt wird.
    ''' </summary>
    Sub AnrMonRestartNachStandBy(ByVal sender As Object, ByVal e As Microsoft.Win32.PowerModeChangedEventArgs)
        P_HF.LogFile("PowerMode: " & e.Mode.ToString & " (" & e.Mode & ")")
        Select Case e.Mode
            Case Microsoft.Win32.PowerModes.Resume
                ThisAddIn_Startup(True)
            Case Microsoft.Win32.PowerModes.Suspend
                P_AnrMon.AnrMonStartStopp()
                P_DP.SpeichereXMLDatei()
        End Select
    End Sub

    ''' <summary>
    ''' Startet das Fritz!Box Telefon-dingsbums
    ''' </summary>
    Private Overloads Sub ThisAddIn_Startup(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Startup
        'StandBy Handler
        AddHandler Microsoft.Win32.SystemEvents.PowerModeChanged, AddressOf AnrMonRestartNachStandBy

        ' Starte das Addin normal
        ThisAddIn_Startup(False)
    End Sub

    ''' <summary>
    ''' Startet das Fritz!Box Telefon-dingsbums manuell
    ''' </summary>
    ''' <param name="Standby">Angabe, obb das Addin aus dem Standby automatisch gestartet wird.</param>
    Private Overloads Sub ThisAddin_Startup(ByVal Standby As Boolean)

        If P_oApp Is Nothing Then
            P_oApp = CType(Application, Outlook.Application)
        End If

        If Standby Then
#If OVer < 14 Then
            C_GUI.SetAnrMonButton()
#Else
            C_GUI.RefreshRibbon()
#End If
            F_Init.StandByReStart()
        Else
            If P_oApp.ActiveExplorer IsNot Nothing Then
#If OVer = 11 Then
                F_Init = New formInit(P_GUI, P_KF, P_HF, P_DP, P_AnrMon, P_XML, P_FBox)
#End If
                ' Letzten Anrufer laden. Dazu wird P_oApp benötigt (Kontaktbild)
                P_AnrMon.LetzterAnrufer = P_AnrMon.LadeLetzterAnrufer()
#If OVer < 14 Then
                C_GUI.SymbolleisteErzeugen(eBtnWaehlen:=eBtnWaehlen, _
                                           eBtnDirektwahl:=eBtnDirektwahl, _
                                           eBtnAnrMon:=eBtnAnrMonitor, _
                                           eBtnAnzeigen:=eBtnAnzeigen, _
                                           eBtnAnrMonNeuStart:=eBtnAnrMonNeuStart, _
                                           eBtnJournalimport:=eBtnJournalimport, _
                                           eBtnEinstellungen:=eBtnEinstellungen, _
                                           ePopWwdh:=ePopWwdh, _
                                           ePopWwdh01:=ePopWwdh01, _
                                           ePopWwdh02:=ePopWwdh02, _
                                           ePopWwdh03:=ePopWwdh03, _
                                           ePopWwdh04:=ePopWwdh04, _
                                           ePopWwdh05:=ePopWwdh05, _
                                           ePopWwdh06:=ePopWwdh06, _
                                           ePopWwdh07:=ePopWwdh07, _
                                           ePopWwdh08:=ePopWwdh08, _
                                           ePopWwdh09:=ePopWwdh09, _
                                           ePopWwdh10:=ePopWwdh10, _
                                           ePopWwdhDel:=ePopWwdhDel, _
                                           ePopAnr:=ePopAnr, _
                                           ePopAnr01:=ePopAnr01, _
                                           ePopAnr02:=ePopAnr02, _
                                           ePopAnr03:=ePopAnr03, _
                                           ePopAnr04:=ePopAnr04, _
                                           ePopAnr05:=ePopAnr05, _
                                           ePopAnr06:=ePopAnr06, _
                                           ePopAnr07:=ePopAnr07, _
                                           ePopAnr08:=ePopAnr08, _
                                           ePopAnr09:=ePopAnr09, _
                                           ePopAnr10:=ePopAnr10, _
                                           ePopAnrDel:=ePopAnrDel, _
                                           ePopVIP:=ePopVIP, _
                                           ePopVIP01:=ePopVIP01, _
                                           ePopVIP02:=ePopVIP02, _
                                           ePopVIP03:=ePopVIP03, _
                                           ePopVIP04:=ePopVIP04, _
                                           ePopVIP05:=ePopVIP05, _
                                           ePopVIP06:=ePopVIP06, _
                                           ePopVIP07:=ePopVIP07, _
                                           ePopVIP08:=ePopVIP08, _
                                           ePopVIP09:=ePopVIP09, _
                                           ePopVIP10:=ePopVIP10, _
                                           ePopVIPDel:=ePopVIPDel)
#End If
                If Not C_DP.P_CBIndexAus Then oInsps = Application.Inspectors
            Else
                P_HF.LogFile("Addin nicht gestartet, da kein Explorer vorhanden")
            End If
        End If

    End Sub

    Private Sub Application_Quit() Handles Application.Quit, Me.Shutdown
        P_AnrMon.AnrMonStartStopp()
        P_HF.LogFile(DataProvider.P_Def_Addin_LangName & " V" & Version & " beendet.")
        P_DP.SpeichereXMLDatei()
        P_HF.NAR(P_oApp)
    End Sub

    Private Sub myOlInspectors(ByVal Inspector As Outlook.Inspector) Handles oInsps.NewInspector
#If OVer = 11 Then
        C_GUI.InspectorSybolleisteErzeugen(Inspector, iPopRWS, iBtnWwh, iBtnRWSDasOertliche, iBtnRws11880, iBtnRWSDasTelefonbuch, iBtnRWStelSearch, iBtnRWSAlle, iBtnKontakterstellen, iBtnVIP, iBtnUpload)
#End If
        If TypeOf Inspector.CurrentItem Is Outlook.ContactItem Then
            If Not (P_DP.P_CBKHO AndAlso Not _
                    CType(CType(Inspector.CurrentItem, Outlook.ContactItem).Parent, Outlook.MAPIFolder).StoreID = _
                    P_KF.P_DefContactFolder.StoreID) Then

                Dim KS As New ContactSaved(P_KF)
                KS.ContactSaved = CType(Inspector.CurrentItem, Outlook.ContactItem)
                ListofOpenContacts.Add(KS)
            End If
        End If
    End Sub

#Region " Office 2003 & 2007"
#If OVer < 14 Then
#Region " Button"
    Private Sub eBtn_Click(ByVal Ctrl As Microsoft.Office.Core.CommandBarButton, ByRef CancelDefault As Boolean) Handles eBtnDirektwahl.Click, _
                                                                                                                         eBtnWaehlen.Click, _
                                                                                                                         eBtnEinstellungen.Click, _
                                                                                                                         eBtnAnrMonitor.Click, _
                                                                                                                         eBtnAnzeigen.Click, _
                                                                                                                         eBtnJournalimport.Click, _
                                                                                                                         eBtnAnrMonNeuStart.Click

        With (C_GUI)
            Select Case Ctrl.Tag
                Case DataProvider.P_CMB_eBtnDirektwahl_Tag
                    .OnAction(GraphicalUserInterface.TaskToDo.DialDirect)
                Case DataProvider.P_CMB_eBtnWaehlen_Tag
                    .OnAction(GraphicalUserInterface.TaskToDo.DialExplorer)
                Case DataProvider.P_CMB_eBtnEinstellungen_Tag
                    .OnAction(GraphicalUserInterface.TaskToDo.OpenConfig)
                Case DataProvider.P_CMB_eBtnAnrMon_Tag
                    C_AnrMon.AnrMonStartStopp()
                Case DataProvider.P_CMB_eBtnAnzeigen_Tag
                    .OnAction(GraphicalUserInterface.TaskToDo.ShowAnrMon)
                Case DataProvider.P_CMB_eBtnJournalimport_Tag
                    .OnAction(GraphicalUserInterface.TaskToDo.OpenJournalimport)
                Case DataProvider.P_CMB_eBtnAnrMonNeuStart_Tag
                    .OnAction(GraphicalUserInterface.TaskToDo.RestartAnrMon)
            End Select
        End With
    End Sub

    Private Sub ePopUp_Click(ByVal control As Office.CommandBarButton, ByRef cancel As Boolean) Handles ePopAnr01.Click, _
                                                                                                        ePopAnr02.Click, _
                                                                                                        ePopAnr03.Click, _
                                                                                                        ePopAnr04.Click, _
                                                                                                        ePopAnr05.Click, _
                                                                                                        ePopAnr06.Click, _
                                                                                                        ePopAnr07.Click, _
                                                                                                        ePopAnr08.Click, _
                                                                                                        ePopAnr09.Click, _
                                                                                                        ePopAnrDel.Click, _
                                                                                                        ePopWwdh01.Click, _
                                                                                                        ePopWwdh02.Click, _
                                                                                                        ePopWwdh03.Click, _
                                                                                                        ePopWwdh04.Click, _
                                                                                                        ePopWwdh05.Click, _
                                                                                                        ePopWwdh06.Click, _
                                                                                                        ePopWwdh07.Click, _
                                                                                                        ePopWwdh08.Click, _
                                                                                                        ePopWwdh09.Click, _
                                                                                                        ePopWwdh10.Click, _
                                                                                                        ePopWwdhDel.Click, _
                                                                                                        ePopVIP01.Click, _
                                                                                                        ePopVIP02.Click, _
                                                                                                        ePopVIP03.Click, _
                                                                                                        ePopVIP04.Click, _
                                                                                                        ePopVIP05.Click, _
                                                                                                        ePopVIP06.Click, _
                                                                                                        ePopVIP07.Click, _
                                                                                                        ePopVIP08.Click, _
                                                                                                        ePopVIP09.Click, _
                                                                                                        ePopVIP10.Click, _
                                                                                                        ePopVIPDel.Click
        Select Case control.Tag
            Case ePopAnrDel.Tag, ePopWwdhDel.Tag, ePopVIPDel.Tag
                C_GUI.ClearInListe(control.Tag)
            Case Else
                C_GUI.OnActionListen(control.Tag)
        End Select

    End Sub
#End Region
#End If
#End Region

#Region " Office 2003 Inspectorfenster"
#If OVer = 11 Then
    Private Sub iBtn_Click(ByVal Ctrl As Microsoft.Office.Core.CommandBarButton, ByRef CancelDefault As Boolean) Handles iBtnKontakterstellen.Click, _
                                                                                                                         iBtnRWSDasOertliche.Click, _
                                                                                                                         iBtnRws11880.Click, _
                                                                                                                         iBtnRWSDasTelefonbuch.Click, _
                                                                                                                         iBtnRWStelSearch.Click, _
                                                                                                                         iBtnRWSAlle.Click, _
                                                                                                                         iBtnWwh.Click, _
                                                                                                                         iBtnVIP.Click, _
                                                                                                                         iBtnUpload.click

        With (C_GUI)
            Select Case CType(Ctrl, CommandBarButton).Tag
                Case DataProvider.P_Tag_Insp_Kontakt
                    .OnAction(GraphicalUserInterface.TaskToDo.CreateContact)
                Case DataProvider.P_RWSDasOertliche_Name
                    .OnActionRWS(oApp.ActiveInspector, RückwärtsSuchmaschine.RWSDasOertliche)
                Case DataProvider.P_RWS11880_Name
                    .OnActionRWS(oApp.ActiveInspector, RückwärtsSuchmaschine.RWS11880)
                Case DataProvider.P_RWSDasTelefonbuch_Name
                    .OnActionRWS(oApp.ActiveInspector, RückwärtsSuchmaschine.RWSDasTelefonbuch)
                Case DataProvider.P_RWSTelSearch_Name
                    .OnActionRWS(oApp.ActiveInspector, RückwärtsSuchmaschine.RWStelSearch)
                Case DataProvider.P_RWSAlle_Name
                    .OnActionRWS(oApp.ActiveInspector, RückwärtsSuchmaschine.RWSAlle)
                Case DataProvider.P_Tag_Insp_Dial
                    .OnAction(GraphicalUserInterface.TaskToDo.DialInspector)
                Case DataProvider.P_CMB_Insp_VIP
                    Dim aktKontakt As Outlook.ContactItem = CType(oApp.ActiveInspector.CurrentItem, Outlook.ContactItem)
                    If .IsVIP(aktKontakt) Then
                        .RemoveVIP(aktKontakt.EntryID, CType(aktKontakt.Parent, Outlook.MAPIFolder).StoreID)
                        Ctrl.State = MsoButtonState.msoButtonUp
                    Else
                        .AddVIP(aktKontakt)
                        Ctrl.State = MsoButtonState.msoButtonDown
                    End If
                Case DataProvider.P_CMB_Insp_Upload
                    Dim aktKontakt As Outlook.ContactItem = CType(oApp.ActiveInspector.CurrentItem, Outlook.ContactItem)
                    C_Fbox.UploadKontaktToFritzBox(aktKontakt, .IsVIP(aktKontakt))
            End Select
        End With
    End Sub
#End If
#End Region
End Class