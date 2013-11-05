﻿Imports Microsoft.Office.Core
Imports Microsoft.Win32

Public Class ThisAddIn
#Region "Office 2003 & 2007 Eventhandler"
#If OVer < 14 Then
    Public WithEvents eBtnWaehlen As Office.CommandBarButton
    Public WithEvents eBtnDirektwahl As Office.CommandBarButton
    Public WithEvents eBtnAnrMonitor As Office.CommandBarButton
    Public WithEvents eBtnAnzeigen As Office.CommandBarButton
    Public WithEvents eBtnJournalimport As Office.CommandBarButton
    Public WithEvents eBtnLeitungsbelegung As Office.CommandBarButton
    Public WithEvents eBtnEinstellungen As Office.CommandBarButton
    Public WithEvents eBtnAnrMonNeuStart As Office.CommandBarButton
    Public WithEvents ePopWwdh As Office.CommandBarPopup
    Public WithEvents ePopWwdh1, ePopWwdh2, ePopWwdh3, ePopWwdh4, ePopWwdh5 As Office.CommandBarButton
    Public WithEvents ePopWwdh6, ePopWwdh7, ePopWwdh8, ePopWwdh9, ePopWwdh10 As Office.CommandBarButton
    Public WithEvents ePopAnr As Office.CommandBarPopup
    Public WithEvents ePopAnr1, ePopAnr2, ePopAnr3, ePopAnr4, ePopAnr5 As Office.CommandBarButton
    Public WithEvents ePopAnr6, ePopAnr7, ePopAnr8, ePopAnr9, ePopAnr10 As Office.CommandBarButton
    Public Shared WithEvents ePopVIP As Office.CommandBarPopup
    Public WithEvents ePopVIP1, ePopVIP2, ePopVIP3, ePopVIP4, ePopVIP5 As Office.CommandBarButton
    Public WithEvents ePopVIP6, ePopVIP7, ePopVIP8, ePopVIP9, ePopVIP10 As Office.CommandBarButton
#End If
#If OVer = 11 Then
    Public WithEvents iPopRWS As Office.CommandBarPopup
    Public WithEvents iBtnWwh As Office.CommandBarButton
    Public WithEvents iBtnRws11880 As Office.CommandBarButton
    Public WithEvents iBtnRWSDasTelefonbuch As Office.CommandBarButton
    Public WithEvents iBtnRWStelSearch As Office.CommandBarButton
    Public WithEvents iBtnRWSAlle As Office.CommandBarButton
    Public WithEvents iBtnKontakterstellen As Office.CommandBarButton
    Public WithEvents iBtnVIP As Office.CommandBarButton
#End If
#End Region
    Private Shared oApp As Outlook.Application

    Friend WithEvents ContactSaved As Outlook.ContactItem
    Friend WithEvents oInsps As Outlook.Inspectors
    Private Shared XML As MyXML ' Reader/Writer initialisieren
    Private Shared fBox As FritzBox  'Deklarieren der Klasse
    Private Shared AnrMon As AnrufMonitor
    Private Shared WClient As Wählclient
    Private Shared hf As Helfer
    Private Shared KontaktFunktionen As Contacts
    Private Shared GUI As GraphicalUserInterface

    Private Shared UseAnrMon As Boolean
    Private Shared Dateipfad As String
#Region "Properties"

    Friend Shared Property P_oApp() As Outlook.Application
        Get
            Return oApp
        End Get
        Set(ByVal value As Outlook.Application)
            oApp = value
        End Set
    End Property

    Friend Shared Property P_XML() As MyXML
        Get
            Return XML
        End Get
        Set(ByVal value As MyXML)
            XML = value
        End Set
    End Property

    Friend Shared Property P_hf() As Helfer
        Get
            Return hf
        End Get
        Set(ByVal value As Helfer)
            hf = value
        End Set
    End Property

    Friend Shared Property P_KontaktFunktionen() As Contacts
        Get
            Return KontaktFunktionen
        End Get
        Set(ByVal value As Contacts)
            KontaktFunktionen = value
        End Set
    End Property

    Friend Shared Property P_GUI() As GraphicalUserInterface
        Get
            Return GUI
        End Get
        Set(ByVal value As GraphicalUserInterface)
            GUI = value
        End Set
    End Property

    Friend Shared Property P_WClient() As Wählclient
        Get
            Return WClient
        End Get
        Set(ByVal value As Wählclient)
            WClient = value
        End Set
    End Property

    Friend Shared Property P_FritzBox() As FritzBox
        Get
            Return fBox
        End Get
        Set(ByVal value As FritzBox)
            fBox = value
        End Set
    End Property

    Friend Shared Property P_AnrMon() As AnrufMonitor
        Get
            Return AnrMon
        End Get
        Set(ByVal value As AnrufMonitor)
            AnrMon = value
        End Set
    End Property

    Friend Shared Property P_Dateipfad() As String
        Get
            Return Dateipfad
        End Get
        Set(ByVal value As String)
            Dateipfad = value
        End Set
    End Property

    Friend Shared Property P_UseAnrMon() As Boolean
        Get
            Return UseAnrMon
        End Get
        Set(ByVal value As Boolean)
            UseAnrMon = value
        End Set
    End Property


#End Region

#If OVer < 14 Then
    Private FritzCmdBar As Office.CommandBar
#End If

    Private Initialisierung As formInit
    Public Const Version As String = "3.6.1"
    Public Shared Event PowerModeChanged As PowerModeChangedEventHandler

#If Not OVer = 11 Then
    Protected Overrides Function CreateRibbonExtensibilityObject() As IRibbonExtensibility
        Initialisierung = New formInit
        Return GUI
    End Function
#End If

    Sub AnrMonRestartNachStandBy(ByVal sender As Object, ByVal e As PowerModeChangedEventArgs)
        Select Case e.Mode
            Case PowerModes.Resume
                hf.LogFile("Aufwachen aus StandBy: " & e.Mode)
                AnrMon.AnrMonStartNachStandby()
            Case PowerModes.Suspend
                AnrMon.AnrMonQuit()
                hf.LogFile("Anrufmonitor für StandBy beendet")
            Case Else
                hf.LogFile("Empfangener Powermode: " & e.Mode)
        End Select
    End Sub

    Private Sub ThisAddIn_Startup(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Startup
        AddHandler SystemEvents.PowerModeChanged, AddressOf AnrMonRestartNachStandBy

        Dim i As Integer = 2

        oApp = CType(Application, Outlook.Application)

        If Not oApp.ActiveExplorer Is Nothing Then
#If OVer = 11 Then
            Initialisierung = New formInit
#End If

#If OVer < 14 Then
            GUI.SymbolleisteErzeugen(ePopWwdh, ePopAnr, ePopVIP, eBtnWaehlen, eBtnDirektwahl, eBtnAnrMonitor, eBtnAnzeigen, eBtnAnrMonNeuStart, eBtnJournalimport, eBtnEinstellungen, _
                                     ePopWwdh1, ePopWwdh2, ePopWwdh3, ePopWwdh4, ePopWwdh5, ePopWwdh6, ePopWwdh7, ePopWwdh8, ePopWwdh9, ePopWwdh10, _
                                     ePopAnr1, ePopAnr2, ePopAnr3, ePopAnr4, ePopAnr5, ePopAnr6, ePopAnr7, ePopAnr8, ePopAnr9, ePopAnr10, _
                                     ePopVIP1, ePopVIP2, ePopVIP3, ePopVIP4, ePopVIP5, ePopVIP6, ePopVIP7, ePopVIP8, ePopVIP9, ePopVIP10)
#End If
            If Not CBool(XML.Read("Optionen", "CBIndexAus", "False")) Then oInsps = Application.Inspectors
        Else
            hf.LogFile("Addin nicht gestartet, da kein Explorer vorhanden war")
        End If
    End Sub

    Private Sub ContactSaved_Write(ByRef Cancel As Boolean) Handles ContactSaved.Write
        If Not CBool(XML.Read("Optionen", "CBIndexAus", "False")) Then
            KontaktFunktionen.IndiziereKontakt(ContactSaved, True)
        End If
    End Sub

    Private Sub ThisAddIn_Shutdown(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Shutdown
        AnrMon.AnrMonQuit()
        XML.SpeichereXMLDatei()
        With hf
            .NAR(oApp)
#If OVer < 14 Then
            .NAR(FritzCmdBar)
#End If
        End With
    End Sub

    Protected Overrides Sub Finalize()
        MyBase.Finalize()
    End Sub

    Private Sub myOlInspectors(ByVal Inspector As Outlook.Inspector) Handles oInsps.NewInspector
#If OVer = 11 Then
        GUI.InspectorSybolleisteErzeugen(Inspector, iPopRWS, iBtnWwh, iBtnRws11880, iBtnRWSDasTelefonbuch, iBtnRWStelSearch, iBtnRWSAlle, iBtnKontakterstellen, iBtnVIP)
#End If
        If TypeOf Inspector.CurrentItem Is Outlook.ContactItem Then
            If XML.Read("Optionen", "CBKHO", "True") = "True" Then
                Dim Ordner As Outlook.MAPIFolder
                Dim StandardOrdner As Outlook.MAPIFolder
                Dim olNamespace As Outlook.NameSpace
                Ordner = CType(CType(Inspector.CurrentItem, Outlook.ContactItem).Parent, Outlook.MAPIFolder)
                olNamespace = oApp.GetNamespace("MAPI")
                StandardOrdner = olNamespace.GetDefaultFolder(Outlook.OlDefaultFolders.olFolderContacts)
                If Not StandardOrdner.StoreID = Ordner.StoreID Then Exit Sub
            End If
            ContactSaved = CType(Inspector.CurrentItem, Outlook.ContactItem)
        End If
    End Sub

#Region " Office 2003 & 2007"
#If OVer < 14 Then
#Region " Button"
    Private Sub eBtnDirektwahl_click(ByVal control As Office.CommandBarButton, ByRef cancel As Boolean) Handles eBtnDirektwahl.Click
        GUI.ÖffneDirektwahl()
    End Sub

    Private Sub ebtnWaehlen_click(ByVal control As Office.CommandBarButton, ByRef cancel As Boolean) Handles eBtnWaehlen.Click
        GUI.WählenExplorer()
    End Sub

    Private Sub ePopAnr1_click(ByVal control As Office.CommandBarButton, ByRef cancel As Boolean) Handles _
    ePopAnr1.Click, ePopAnr2.Click, ePopAnr3.Click, ePopAnr4.Click, ePopAnr5.Click, ePopAnr6.Click, ePopAnr7.Click, ePopAnr8.Click, ePopAnr9.Click, ePopAnr10.Click, _
    ePopWwdh1.Click, ePopWwdh2.Click, ePopWwdh3.Click, ePopWwdh4.Click, ePopWwdh5.Click, ePopWwdh6.Click, ePopWwdh7.Click, ePopWwdh8.Click, ePopWwdh9.Click, ePopWwdh10.Click, _
    ePopVIP1.Click, ePopVIP2.Click, ePopVIP3.Click, ePopVIP4.Click, ePopVIP5.Click, ePopVIP6.Click, ePopVIP7.Click, ePopVIP8.Click, ePopVIP9.Click, ePopVIP10.Click
        GUI.KlickListen(control.Tag)
    End Sub

    Private Sub eBtnEinstellungen_click(ByVal control As Office.CommandBarButton, ByRef cancel As Boolean) Handles eBtnEinstellungen.Click
        GUI.ÖffneEinstellungen()
    End Sub

    Private Sub eBtnAnrMonitor_Click(ByVal Ctrl As Office.CommandBarButton, ByRef CancelDefault As Boolean) Handles eBtnAnrMonitor.Click
        AnrMon.AnrMonAnAus()
    End Sub

    Private Sub eBtnAnzeigen_Click(ByVal Ctrl As Office.CommandBarButton, ByRef CancelDefault As Boolean) Handles eBtnAnzeigen.Click
        GUI.ÖffneAnrMonAnzeigen()
    End Sub

    Private Sub eBtnJournalimport_Click(ByVal Ctrl As Microsoft.Office.Core.CommandBarButton, ByRef CancelDefault As Boolean) Handles eBtnJournalimport.Click
        GUI.ÖffneJournalImport()
    End Sub

    Private Sub eBtnAnrMonNeuStart_Click(ByVal Ctrl As Microsoft.Office.Core.CommandBarButton, ByRef CancelDefault As Boolean) Handles eBtnAnrMonNeuStart.Click
        GUI.AnrMonNeustarten()
    End Sub
#End Region

#End If
#End Region

#Region " Office 2003 Inspectorfenster"
#If OVer = 11 Then
    Private Sub iBtn_Click(ByVal Ctrl As Microsoft.Office.Core.CommandBarButton, ByRef CancelDefault As Boolean) Handles iBtnKontakterstellen.Click, _
                                                                                                                         iBtnRws11880.Click, _
                                                                                                                         iBtnRWSDasTelefonbuch.Click, _
                                                                                                                         iBtnRWStelSearch.Click, _
                                                                                                                         iBtnRWSAlle.Click, _
                                                                                                                         iBtnWwh.Click, _
                                                                                                                         iBtnVIP.Click

        With (GUI)
            Select Case CType(Ctrl, CommandBarButton).Caption
                Case "Kontakt erstellen"
                    .KontaktErstellen()
                Case "11880"
                    .RWS11880(oApp.ActiveInspector)
                Case "DasTelefonbuch"
                    .RWSDasTelefonbuch(oApp.ActiveInspector)
                Case "tel.search.ch"
                    .RWSTelSearch(oApp.ActiveInspector)
                Case "Alle"
                    .RWSAlle(oApp.ActiveInspector)
                Case "Wählen"
                    WClient.WählenAusInspector()
                Case "VIP"
                    Dim aktKontakt As Outlook.ContactItem = CType(oApp.ActiveInspector.CurrentItem, Outlook.ContactItem)
                    If .IsVIP(aktKontakt) Then
                        .RemoveVIP(aktKontakt.EntryID, CType(aktKontakt.Parent, Outlook.MAPIFolder).StoreID)
                        Ctrl.State = MsoButtonState.msoButtonUp
                    Else
                        .AddVIP(aktKontakt)
                        Ctrl.State = MsoButtonState.msoButtonDown
                    End If
            End Select
        End With
    End Sub
#End If
#End Region

End Class
