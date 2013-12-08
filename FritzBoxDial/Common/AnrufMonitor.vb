﻿Imports System.Threading
Imports System.Net
Imports System.IO
Imports System.ComponentModel

Friend Class AnrufMonitor
    Private WithEvents BWAnrMonEinblenden As BackgroundWorker
    Private WithEvents BWStoppuhrEinblenden As BackgroundWorker
    Private WithEvents BWStartTCPReader As BackgroundWorker
    Private WithEvents TimerReStartStandBy As System.Timers.Timer
    Private WithEvents TimerCheckAnrMon As System.Timers.Timer

    Private ReceiveThread As Thread
    Private AnrMonList As New Collections.ArrayList
    Private Shared AnrMonStream As Sockets.NetworkStream 'Shared, da ansonsten AnrMonAktiv Fehler liefert
    Private AnrMonTCPClient As Sockets.TcpClient
    Private STUhrDaten(5) As StructStoppUhr

    Private C_GUI As GraphicalUserInterface
    Private C_OlI As OutlookInterface
    Private C_Kontakt As Contacts
    Private C_XML As DataProvider
    Private C_hf As Helfer
    Private F_Config As formCfg
    Private F_RWS As formRWSuche
    Private F_StoppUhr As formStoppUhr

    Private StandbyCounter As Integer
    Friend AnrMonAktiv As Boolean                    ' damit 'AnrMonAktion' nur einmal aktiv ist
    Friend AnrMonError As Boolean
    Private TelAnzahl As Integer
    Private Eingeblendet As Integer = 0

    Private sIPAddresse As String = "fritz.box"
    Private FBAnrMonPort As Integer = P_DefaultFBAnrMonPort

#Region "Properties"
    Friend Property P_FBAddr() As String
        Get
            Return sIPAddresse
        End Get
        Set(ByVal Value As String)
            sIPAddresse = Value
        End Set
    End Property

    Private ReadOnly Property P_DefaultFBAnrMonPort() As Integer
        Get
            Return 1012
        End Get
    End Property
#End Region
#Region "Phoner"
    Private AnrMonPhoner As Boolean = False
#End Region

    Public Sub New(ByVal RWS As formRWSuche, _
                   ByVal iniKlasse As DataProvider, _
                   ByVal HelferKlasse As Helfer, _
                   ByVal KontaktKlasse As Contacts, _
                   ByVal InterfacesKlasse As GraphicalUserInterface, _
                   ByVal OutlInter As OutlookInterface, _
                   ByVal FBAdr As String)

        C_hf = HelferKlasse
        C_Kontakt = KontaktKlasse
        C_GUI = InterfacesKlasse
        C_XML = iniKlasse
        F_RWS = RWS
        C_OlI = OutlInter
        P_FBAddr = FBAdr
        AnrMonStart(False)

    End Sub

    Protected Overrides Sub Finalize()
        MyBase.Finalize()
    End Sub
#Region "Strukturen"
    Structure StructStoppUhr
        Dim Anruf As String
        Dim Abbruch As Boolean
        Dim StartZeit As String
        Dim Richtung As String
        Dim MSN As String
    End Structure
#End Region

#Region "Anrufmonitor Grundlagen"
    Private Sub AnrMonAktion()
        ' schaut in der FritzBox im Port 1012 nach und startet entsprechende Unterprogramme
        Dim r As New StreamReader(AnrMonStream)
        Dim FBStatus As String  ' Status-String der FritzBox
        Dim aktZeile() As String  ' aktuelle Zeile im Status-String
        Do
            If AnrMonStream.DataAvailable And AnrMonAktiv Then
                FBStatus = r.ReadLine
                Select Case FBStatus
                    Case "Welcome to Phoner"
                        AnrMonPhoner = True
                    Case "Sorry, too many clients"
                        C_hf.LogFile("AnrMonAktion, Phoner: ""Sorry, too many clients""")
                    Case Else
                        C_hf.LogFile("AnrMonAktion: " & FBStatus)
                        aktZeile = Split(FBStatus, ";", , CompareMethod.Text)
                        If Not aktZeile.Length = 1 Then
                            'Schauen ob "RING", "CALL", "CONNECT" oder "DISCONNECT" übermittelt wurde
                            Select Case CStr(aktZeile.GetValue(1))
                                Case "RING"
                                    AnrMonRING(aktZeile, True, True)
                                Case "CALL"
                                    AnrMonCALL(aktZeile, True)
                                Case "CONNECT"
                                    AnrMonCONNECT(aktZeile, True)
                                Case "DISCONNECT"
                                    AnrMonDISCONNECT(aktZeile, True)
                            End Select
                        End If
                End Select
            End If
            Thread.Sleep(50)
            Windows.Forms.Application.DoEvents()
        Loop Until Not AnrMonAktiv
        r.Close()
        r = Nothing
    End Sub '(AnrMonAktion)

    ''' <summary>
    ''' Wird durch das Symbol 'Anrufmonitor' in der 'FritzBox'-Symbolleiste ausgeführt
    ''' </summary>
    ''' <returns>Boolean: Ob Anrufmonitor eingeschaltet ist.</returns>
    ''' <remarks></remarks>
    Friend Function AnrMonAnAus() As Boolean
        If AnrMonAktiv Then
            ' Timer stoppen, TCP/IP-Verbindung(schließen)
            AnrMonQuit()
#If OVer < 14 Then
                C_GUI.SetAnrMonButton(False)
#End If
            AnrMonAnAus = False
        Else
            ' Timer starten, TCP/IP-Verbindung öffnen
            If AnrMonStart(True) Then
#If OVer < 14 Then
                C_GUI.SetAnrMonButton(True)
#End If
            End If
            AnrMonAnAus = True
        End If
    End Function '(AnrMonAnAus)

    Function AnrMonStart(ByVal Manuell As Boolean) As Boolean
        If (C_XML.P_CBAnrMonAuto Or Manuell) And C_XML.P_CBUseAnrMon Then

            If C_XML.P_CBPhonerAnrMon Then
                FBAnrMonPort = 2012
                P_FBAddr = "127.0.0.1"
            End If

            If C_hf.Ping(P_FBAddr) Or CBool(C_XML.P_CBForceFBAddr) Then
                BWStartTCPReader = New BackgroundWorker
                With BWStartTCPReader
                    .WorkerReportsProgress = True
                    .RunWorkerAsync()
                End With
            Else
                AnrMonAktiv = False
                AnrMonError = True
            End If
        End If
        Return True
    End Function '(AnrMonStart)

    Function AnrMonStartNachStandby() As Boolean
        Dim formjournalimort As formJournalimport
        Dim FritzBoxErreichbar As Boolean

        AnrMonStartNachStandby = False
        If C_XML.P_CBAnrMonAuto And C_XML.P_CBUseAnrMon Then
            If C_XML.P_CBForceFBAddr Then
                ' Erfahrung: Fritz!Box ist noch nicht erreichbar wenn Outlook aus Standby erwacht ist.
                FritzBoxErreichbar = False
            Else
                FritzBoxErreichbar = C_hf.Ping(sIPAddresse)
            End If


            If Not FritzBoxErreichbar Then
                TimerReStartStandBy = C_hf.SetTimer(3000)
                StandbyCounter = 2

                C_hf.LogFile("Standby Timer 1. Ping nicht erfolgreich")
            Else
                C_hf.LogFile("Standby 1. Ping erfolgreich")
                AnrMonStart(False)

                If C_XML.P_CBJournal Then formjournalimort = New formJournalimport(Me, C_hf, C_XML, False)
            End If
            Return True
        End If


    End Function

    Private Sub TimerReStartStandBy_Elapsed(ByVal sender As Object, ByVal e As System.Timers.ElapsedEventArgs) Handles TimerReStartStandBy.Elapsed
        If C_hf.Ping(C_XML.P_TBFBAdr) Then
            C_hf.LogFile("Standby Timer " & StandbyCounter & ". Ping erfolgreich")
            StandbyCounter = 15
            AnrMonStart(False)
            If C_XML.P_CBJournal Then
                Dim formjournalimort As New formJournalimport(Me, C_hf, C_XML, False)
            End If
            C_hf.LogFile("Anrufmonitor nach StandBy neugestartet")
        End If

        If StandbyCounter >= 14 Then
            If StandbyCounter = 14 Then C_hf.LogFile("TimerReStartStandBy: Reaktivierung des Anrufmonitors nicht erfolgreich.")
            C_hf.KillTimer(TimerReStartStandBy)
        Else
            C_hf.LogFile("Standby Timer " & StandbyCounter & ". nicht Ping erfolgreich")
            StandbyCounter += 1
        End If

    End Sub

    Private Sub TimerCheckAnrMon_Elapsed(sender As Object, e As Timers.ElapsedEventArgs) Handles TimerCheckAnrMon.Elapsed
        ' Es kann sein, dass die Verbindung zur FB abreißt. Z. B. wenn die VPN unterbrochen ist. 
        Dim IPAddress As IPAddress
        Dim tmpTCPclient As Sockets.TcpClient
        Dim RemoteEP As IPEndPoint
        Dim IPHostInfo As IPHostEntry

        If LCase(P_FBAddr) = ThisAddIn.P_FritzBox.P_DefaultFBAddr Then
            IPHostInfo = Dns.GetHostEntry(P_FBAddr)
            IPAddress = IPAddress.Parse(IPHostInfo.AddressList(0).ToString)
        Else
            IPAddress = IPAddress.Parse(P_FBAddr)
        End If
        RemoteEP = New IPEndPoint(IPAddress, FBAnrMonPort)

        tmpTCPclient = New Sockets.TcpClient()
        Try
            tmpTCPclient.Connect(RemoteEP)
        Catch
            C_hf.LogFile("Die TCP-Verbindung zum Fritz!Box Anrufmonitor wurde verloren.")
            AnrMonQuit()
            AnrMonError = True
        End Try
        tmpTCPclient.Close()
        RemoteEP = Nothing
        IPHostInfo = Nothing

        tmpTCPclient = Nothing
#If OVer < 14 Then
        C_GUI.SetAnrMonButton(True)
#Else
        C_GUI.RefreshRibbon()
#End If
    End Sub

    Friend Sub AnrMonQuit()
        ' wird beim Beenden von Outlook ausgeführt und beendet den Anrufmonitor
        AnrMonAktiv = False

        With TimerCheckAnrMon
            .Stop()
            .Dispose()
        End With
        With AnrMonStream
            .Close()
            .Dispose()
        End With
        With AnrMonTCPClient
            .Close()
        End With
        TimerCheckAnrMon = Nothing
        AnrMonStream = Nothing
        AnrMonTCPClient = Nothing
        C_hf.LogFile("AnrMonQuit: Anrufmonitor beendet")
    End Sub '(AnrMonQuit)

    Friend Sub AnrMonReStart()
        AnrMonQuit()
        AnrMonStart(True)
    End Sub

    Friend Function TelefonName(ByVal MSN As String) As String
        TelefonName = vbNullString
        If Not MSN = vbNullString Then
            If Not AnrMonPhoner Then
                Dim xPathTeile As New ArrayList
                With xPathTeile
                    .Add("Telefone")
                    .Add("Telefone")
                    .Add("*")
                    .Add("Telefon")
                    .Add("[TelNr = """ & MSN & """ and not(@Dialport > 599)]") ' Keine Anrufbeantworter
                    .Add("TelName")
                End With
                TelefonName = Replace(C_XML.Read(xPathTeile, ""), ";", ", ")
                xPathTeile = Nothing
            Else
                TelefonName = "Phoner" ' ,  werden danach entfernt.
            End If
        End If
    End Function

    Private Sub BWAnrMonEinblenden_DoWork(ByVal sender As Object, ByVal e As System.ComponentModel.DoWorkEventArgs) Handles BWAnrMonEinblenden.DoWork
        Dim ID As Integer = CInt(e.Argument)
        AnrMonList.Add(New formAnrMon(CInt(ID), True, C_XML, C_hf, Me, C_OlI))
        Dim a As Integer
        Do
            a = AnrMonList.Count - 1
            For i = 0 To a
                If i < AnrMonList.Count Then
                    If CType(AnrMonList(i), formAnrMon).AnrmonClosed Then
                        AnrMonList.RemoveAt(i)
                        i = 0
                        a = AnrMonList.Count - 1
                    Else
                        Thread.Sleep(2)
                        Windows.Forms.Application.DoEvents()
                    End If
                End If
            Next
            Windows.Forms.Application.DoEvents()
        Loop Until (AnrMonList.Count = 0)
    End Sub

    Private Sub BWStoppuhrEinblenden_DoWork(ByVal sender As Object, ByVal e As System.ComponentModel.DoWorkEventArgs) Handles BWStoppuhrEinblenden.DoWork
        Dim ID As Integer = CInt(e.Argument)
        Dim WarteZeit As Integer
        Dim Beendet As Boolean = False
        Dim StartPosition As System.Drawing.Point
        Dim x As Integer = 0
        Dim y As Integer = 0

        If C_XML.P_CBStoppUhrAusblenden Then
            WarteZeit = C_XML.P_TBStoppUhr
        Else
            WarteZeit = -1
        End If

        StartPosition = New System.Drawing.Point(C_XML.P_CBStoppUhrX, C_XML.P_CBStoppUhrY)
        For Each Bildschirm In Windows.Forms.Screen.AllScreens
            x += Bildschirm.Bounds.Size.Width
            y += Bildschirm.Bounds.Size.Height
        Next
        With StartPosition
            If .X > x Or .Y > y Then
                .X = CInt((Windows.Forms.Screen.PrimaryScreen.Bounds.Width - 100) / 2)
                .Y = CInt((Windows.Forms.Screen.PrimaryScreen.Bounds.Height - 50) / 2)
            End If
        End With

        With STUhrDaten(ID)
            Dim frmStUhr As New formStoppUhr(.Anruf, .StartZeit, .Richtung, WarteZeit, StartPosition, .MSN)
            C_hf.LogFile("Stoppuhr gestartet - ID: " & ID & ", Anruf: " & .Anruf)
            BWStoppuhrEinblenden.WorkerSupportsCancellation = True
            Do Until frmStUhr.StUhrClosed
                If Not Beendet And .Abbruch Then
                    frmStUhr.Stopp()
                    Beendet = True
                End If
                Thread.Sleep(20)
                Windows.Forms.Application.DoEvents()
            Loop
            C_XML.P_CBStoppUhrX = frmStUhr.Position.X
            C_XML.P_CBStoppUhrY = frmStUhr.Position.Y
            frmStUhr = Nothing
        End With
    End Sub

    Private Sub BWStartTCPReader_DoWork(sender As Object, e As DoWorkEventArgs) Handles BWStartTCPReader.DoWork
        System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(500))
        Dim IPAddress As IPAddress
        Dim ReceiveThread As Thread
        Dim RemoteEP As IPEndPoint
        Dim IPHostInfo As IPHostEntry

        If LCase(P_FBAddr) = ThisAddIn.P_FritzBox.P_DefaultFBAddr Then
            IPHostInfo = Dns.GetHostEntry(P_FBAddr)
            IPAddress = IPAddress.Parse(IPHostInfo.AddressList(0).ToString)
        Else
            IPAddress = IPAddress.Parse(P_FBAddr)
        End If
        RemoteEP = New IPEndPoint(IPAddress, FBAnrMonPort)

        AnrMonTCPClient = New Sockets.TcpClient()
        Try
            AnrMonTCPClient.Connect(RemoteEP)
        Catch Err As Exception
            C_hf.LogFile("TCP Verbindung nicht aufgebaut: " & Err.Message)
            AnrMonError = True
            e.Result = False
        End Try

        If AnrMonTCPClient.Connected Then
            AnrMonStream = AnrMonTCPClient.GetStream()

            If AnrMonStream.CanRead Then
                ReceiveThread = New Thread(AddressOf AnrMonAktion)
                With ReceiveThread
                    .IsBackground = True
                    .Start()
                    AnrMonAktiv = .IsAlive
                End With
                ' Timer AnrufmonitorCheck starten
                TimerCheckAnrMon = New System.Timers.Timer
                With TimerCheckAnrMon
                    .Interval = TimeSpan.FromMinutes(1).TotalMilliseconds
                    .Start()
                End With

                e.Result = AnrMonAktiv
            Else
                AnrMonError = True
                e.Result = False
            End If
        Else
            AnrMonError = True
            e.Result = False
        End If
    End Sub

    Private Sub BWStartTCPReader_RunWorkerCompleted(sender As Object, e As RunWorkerCompletedEventArgs) Handles BWStartTCPReader.RunWorkerCompleted

        If CBool(e.Result) Then
#If OVer < 14 Then
            C_GUI.SetAnrMonButton(True)
#Else
            C_GUI.RefreshRibbon()
#End If
            AnrMonAktiv = CBool(e.Result)
            AnrMonError = False
        Else
            C_hf.LogFile("BWStartTCPReader_RunWorkerCompleted: Es ist ein TCP/IP Fehler aufgetreten.")
            AnrMonAktiv = False
            AnrMonError = True
        End If
        BWStartTCPReader.Dispose()
    End Sub
#End Region

#Region "Anrufmonitor Ereignisse"
    ''' <summary>
    ''' Behandelt den vom Anrufmonitor der Fritz!Box erhaltener String für RING
    ''' </summary>
    ''' <param name="FBStatus">String: Vom Anrufmonitor der Fritz!Box erhaltener String für RING</param>
    ''' <param name="AnrMonAnzeigen">Boolean: Soll Anrufmonitor angezeigt werden. Bei Journalimport nicht, ansonsten ja (unabhängig von der Einstellung des Users)</param>
    ''' <param name="StoppUhrAnzeigen">Boolean: Soll StoppUhr angezeigt werden. Bei Journalimport nicht, ansonsten ja (unabhängig von der Einstellung des Users)</param>
    ''' <remarks></remarks>
    Friend Sub AnrMonRING(ByVal FBStatus As String(), ByVal AnrMonAnzeigen As Boolean, ByVal StoppUhrAnzeigen As Boolean)
        ' wertet einen eingehenden Anruf aus
        ' Parameter: FBStatus (String ()):   Status-String der FritzBox
        '            anzeigen (Boolean):  nur bei 'true' wird 'AnrMonEinblenden' ausgeführt
        ' FBStatus(0): Uhrzeit
        ' FBStatus(1): RING, wird nicht verwendet
        ' FBStatus(2): Die Nummer der aktuell aufgebauten Verbindungen (0 ... n), dient zur Zuordnung der Telefonate, ID
        ' FBStatus(3): Eingehende Telefonnummer, TelNr
        ' FBStatus(4): Angerufene eigene Telefonnummer, MSN
        ' FBStatus(5): ???


        Dim MSN As String = C_hf.OrtsVorwahlEntfernen(CStr(FBStatus.GetValue(4)), C_XML.P_TBVorwahl)

        ' Anruf nur anzeigen, wenn die MSN stimmt
        Dim xPathTeile As New ArrayList
        With xPathTeile
            .Add("Telefone")
            .Add("Nummern")
            .Add("*")
            .Add("[. = """ & C_hf.OrtsVorwahlEntfernen(MSN, C_XML.P_TBVorwahl) & """]")
            .Add("@Checked")
        End With

        If C_hf.IsOneOf("1", Split(C_XML.Read(xPathTeile, "0;") & ";", ";", , CompareMethod.Text)) Or AnrMonPhoner Then
            'If C_hf.IsOneOf(C_hf.OrtsVorwahlEntfernen(MSN, Vorwahl), Split(checkstring, ";", , CompareMethod.Text)) Or AnrMonPhoner Then

            Dim TelNr As String            ' ermittelte TelNr
            Dim Anrufer As String = vbNullString           ' ermittelter Anrufer
            Dim vCard As String = vbNullString           ' vCard des Anrufers
            Dim KontaktID As String = vbNullString             ' ID der Kontaktdaten des Anrufers
            Dim StoreID As String = vbNullString           ' ID des Ordners, in dem sich der Kontakt befindet
            Dim ID As Integer            ' ID des Telefonats
            Dim rws As Boolean = False    ' 'true' wenn die Rückwärtssuche erfolgreich war
            Dim LetzterAnrufer(5) As String
            Dim RWSIndex As Boolean

            ID = CInt(FBStatus.GetValue(2))
            TelNr = CStr(FBStatus.GetValue(3))
            'MSN = CStr(FBStatus.GetValue(4))  'Ist doch schon belegt
            ' Phoner
            If AnrMonPhoner Then
                Dim PhonerTelNr() As String
                Dim pos As Integer = InStr(TelNr, "@", CompareMethod.Text)
                If Not pos = 0 Then
                    TelNr = Left(TelNr, pos - 1)
                Else
                    PhonerTelNr = C_hf.TelNrTeile(TelNr)
                    If Not PhonerTelNr(1) = "" Then TelNr = PhonerTelNr(1) & Mid(TelNr, InStr(TelNr, ")", CompareMethod.Text) + 2)
                    If Not PhonerTelNr(0) = "" Then TelNr = PhonerTelNr(0) & Mid(TelNr, 2)
                End If
                TelNr = C_hf.nurZiffern(TelNr, C_XML.P_TBLandesVW)
            End If
            ' Ende Phoner

            If Len(TelNr) = 0 Then TelNr = "unbekannt"
            LetzterAnrufer(0) = CStr(FBStatus.GetValue(0)) 'Zeit
            LetzterAnrufer(1) = Anrufer
            LetzterAnrufer(2) = TelNr
            LetzterAnrufer(3) = MSN
            SpeichereLetzerAnrufer(CStr(ID), LetzterAnrufer)
            ' Daten für Anzeige im Anrurfmonitor speichern
            If AnrMonAnzeigen Then
                If Not C_OlI.VollBildAnwendungAktiv Then
                    BWAnrMonEinblenden = New BackgroundWorker
                    BWAnrMonEinblenden.RunWorkerAsync(ID)
                End If
            End If

            ' Daten in den Kontakten suchen und per Rückwärtssuche ermitteln
            If Not TelNr = "unbekannt" Then
                Dim FullName As String = vbNullString
                Dim CompanyName As String = vbNullString
                ' Anrufer in den Outlook-Kontakten suchen
                If C_OlI.StarteKontaktSuche(KontaktID, StoreID, C_XML.P_CBKHO, TelNr, "", C_XML.P_TBLandesVW) Then
                    C_OlI.KontaktInformation(KontaktID, StoreID, FullName:=FullName, CompanyName:=CompanyName)
                    Anrufer = Replace(FullName & " (" & CompanyName & ")", " ()", "")
                    If C_XML.P_CBIgnoTelNrFormat Then TelNr = C_hf.formatTelNr(TelNr)
                Else
                    ' Anrufer per Rückwärtssuche ermitteln
                    If C_XML.P_CBRueckwaertssuche Then
                        If RWSIndex Then
                            With xPathTeile
                                .Clear()
                                .Add("CBRWSIndex")
                                .Add("Eintrag[@ID=""" & TelNr & """]")
                            End With
                            vCard = C_XML.Read(xPathTeile, Nothing)
                        End If
                        If vCard = Nothing Then
                            Select Case C_XML.P_CBoxRWSuche
                                Case 0
                                    rws = F_RWS.RWS11880(TelNr, vCard)
                                Case 1
                                    rws = F_RWS.RWSDasTelefonbuch(TelNr, vCard)
                                Case 2
                                    rws = F_RWS.RWStelsearch(TelNr, vCard)
                                Case 3
                                    rws = F_RWS.RWSAlle(TelNr, vCard)
                            End Select
                            'Im folgenden wird automatisch ein Kontakt erstellt, der durch die Rückwärtssuche ermittlt wurde. Dies geschieht nur, wenn es gewünscht ist.
                            If rws And C_XML.P_CBKErstellen Then
                                C_Kontakt.ErstelleKontakt(KontaktID, StoreID, vCard, TelNr)
                                C_OlI.KontaktInformation(KontaktID, StoreID, FullName:=FullName, CompanyName:=CompanyName)
                                Anrufer = Replace(FullName & " (" & CompanyName & ")", " ()", "")
                            End If
                        Else
                            rws = True
                        End If

                        If rws And KontaktID = "-1;" Then
                            Anrufer = ReadFNfromVCard(vCard)
                            Anrufer = Replace(Anrufer, Chr(13), "", , , CompareMethod.Text)
                            If InStr(1, Anrufer, "Firma", CompareMethod.Text) = 1 Then Anrufer = Right(Anrufer, Len(Anrufer) - 5)
                            Anrufer = Trim(Anrufer)
                            If RWSIndex Then
                                xPathTeile.Item(xPathTeile.Count - 1) = "Eintrag"
                                C_XML.Write(xPathTeile, vCard, "ID", TelNr)
                            End If
                            KontaktID = "-1" & Anrufer & ";" & vCard
                        End If
                    End If
                    TelNr = C_hf.formatTelNr(TelNr)
                End If

                LetzterAnrufer(1) = Anrufer
                LetzterAnrufer(2) = TelNr
                LetzterAnrufer(4) = StoreID
                LetzterAnrufer(5) = KontaktID
                SpeichereLetzerAnrufer(CStr(ID), LetzterAnrufer)
                UpdateList("RingList", Anrufer, TelNr, FBStatus(0), StoreID, KontaktID)
#If OVer < 14 Then
                If C_XML.P_CBSymbAnrListe Then C_GUI.FillPopupItems("AnrListe")
#End If
            End If
            'StoppUhr
            If C_XML.P_CBStoppUhrEinblenden And StoppUhrAnzeigen Then
                With STUhrDaten(ID)
                    .Richtung = "Anruf von:"
                    If Anrufer = "" Then
                        .Anruf = TelNr
                    Else
                        .Anruf = Anrufer
                    End If
                End With
            End If
            ' Daten für den Journaleintrag sichern
            If C_XML.P_CBJournal Or C_XML.P_CBStoppUhrEinblenden Then
                NeuerJournalEintrag(ID, "Eingehender Anruf von", CStr(FBStatus.GetValue(0)), MSN, TelNr, KontaktID, StoreID)
            End If
        End If

    End Sub '(AnrMonRING)

    ''' <summary>
    ''' Behandelt den vom Anrufmonitor der Fritz!Box erhaltener String für CALL
    ''' </summary>
    ''' <param name="FBStatus">String: Vom Anrufmonitor der Fritz!Box erhaltener String für CALL</param>
    ''' <param name="StoppUhrAnzeigen">Boolean: Soll StoppUhr angezeigt werden. Bei Journalimport nicht, ansonsten ja (unabhängig von der Einstellung des Users)</param>
    ''' <remarks></remarks>
    Friend Sub AnrMonCALL(ByVal FBStatus As String(), ByVal StoppUhrAnzeigen As Boolean)
        ' wertet einen ausgehenden Anruf aus
        ' Parameter: FBStatus (String()):  Status-String der FritzBox

        ' FBStatus(0): Uhrzeit
        ' FBStatus(1): CALL, wird nicht verwendet
        ' FBStatus(2): Die Nummer der aktuell aufgebauten Verbindungen (0 ... n), dient zur Zuordnung der Telefonate, ID
        ' FBStatus(3): Nebenstellennummer, eindeutige Zuordnung des Telefons
        ' FBStatus(4): Ausgehende eigene Telefonnummer, MSN
        ' FBStatus(5): die gewählte Rufnummer

        Dim ID As Integer = CInt(FBStatus.GetValue(2))  ' ID des Telefonats
        Dim NSN As Integer = CInt(FBStatus.GetValue(3)) ' Nebenstellennummer des Telefonates
        Dim MSN As String = C_hf.OrtsVorwahlEntfernen(CStr(FBStatus.GetValue(4)), C_XML.P_TBVorwahl)  ' Ausgehende eigene Telefonnummer, MSN
        Dim LandesVW As String = C_XML.P_TBLandesVW     ' eigene Landesvorwahl
        Dim TelNr As String                             ' ermittelte TelNr
        Dim Anrufer As String = "-1"                    ' ermittelter Anrufer
        Dim vCard As String = ""                        ' vCard des Anrufers
        Dim KontaktID As String = "-1;"                 ' ID der Kontaktdaten des Anrufers
        Dim StoreID As String = "-1"                    ' ID des Ordners, in dem sich der Kontakt befindet

        Dim rws As Boolean                              ' 'true' wenn die Rückwärtssuche erfolgreich war
        Dim RWSIndex As Boolean
        Dim xPathTeile As New ArrayList

        ' Problem DECT/IP-Telefone: keine MSN  über Anrufmonitor eingegangen. Aus Datei ermitteln.
        If MSN = vbNullString Then
            Select Case NSN
                Case 0 To 2 ' FON1-3
                    NSN += 1
                Case 10 To 19 ' DECT
                    NSN += 50
            End Select
            Select Case NSN
                Case 3, 4, 5, 36, 37
                    ' Diese komischen Dinger werden ignoriert:
                    ' 3=Durchwahl
                    ' 4=ISDN Gerät
                    ' 5=Fax (intern/PC)
                    ' 36=Data S0
                    ' 37=Data PC
                    MSN = "-1"
                Case Else
                    With xPathTeile
                        .Add("Telefone")
                        .Add("Telefone")
                        .Add("*")
                        .Add("Telefon[@Dialport = """ & NSN & """]")
                        .Add("TelNr")
                    End With
                    MSN = C_XML.Read(xPathTeile, "")
            End Select
        End If

        With xPathTeile
            .Clear()
            .Add("Telefone")
            .Add("Nummern")
            .Add("*")
            .Add("[. = """ & Replace(MSN, ";", """ or . = """, , , CompareMethod.Text) & """]")
            .Add("@Checked")
        End With

        If C_hf.IsOneOf("1", Split(C_XML.Read(xPathTeile, "0;") & ";", ";", , CompareMethod.Text)) Or AnrMonPhoner Then

            TelNr = C_hf.nurZiffern(CStr(FBStatus.GetValue(5)), LandesVW)
            If TelNr = "" Then TelNr = "unbekannt"
            ' CbC-Vorwahl entfernen
            If Left(TelNr, 4) = "0100" Then TelNr = Right(TelNr, Len(TelNr) - 6)
            If Left(TelNr, 3) = "010" Then TelNr = Right(TelNr, Len(TelNr) - 5)
            If Not Left(TelNr, 1) = "0" And Not Left(TelNr, 2) = "11" And Not Left(TelNr, 1) = "+" Then TelNr = C_XML.P_TBVorwahl & TelNr
            ' Raute entfernen
            If Right(TelNr, 1) = "#" Then TelNr = Left(TelNr, Len(TelNr) - 1)
            ' Daten zurücksetzen
            'Anrufer = TelNr
            If Not TelNr = "unbekannt" Then
                Dim FullName As String = vbNullString
                Dim CompanyName As String = vbNullString
                ' Anrufer in den Outlook-Kontakten suchen
                If C_OlI.StarteKontaktSuche(KontaktID, StoreID, C_XML.P_CBKHO, TelNr, "", LandesVW) Then
                    C_OlI.KontaktInformation(KontaktID, StoreID, FullName:=FullName, CompanyName:=CompanyName)
                    Anrufer = Replace(FullName & " (" & CompanyName & ")", " ()", "")
                    If C_XML.P_CBIgnoTelNrFormat Then TelNr = C_hf.formatTelNr(TelNr)
                Else
                    ' Anrufer per Rückwärtssuche ermitteln
                    If C_XML.P_CBRueckwaertssuche Then
                        RWSIndex = C_XML.P_CBRWSIndex
                        If RWSIndex Then
                            With xPathTeile
                                .Clear()
                                .Add("CBRWSIndex")
                                .Add("Eintrag[@ID=""" & TelNr & """]")
                            End With
                            vCard = C_XML.Read(xPathTeile, Nothing)
                        End If
                        If vCard = Nothing Then
                            Select Case C_XML.P_CBoxRWSuche
                                Case 0
                                    rws = F_RWS.RWS11880(TelNr, vCard)
                                Case 1
                                    rws = F_RWS.RWSDasTelefonbuch(TelNr, vCard)
                                Case 2
                                    rws = F_RWS.RWStelsearch(TelNr, vCard)
                                Case 3
                                    rws = F_RWS.RWSAlle(TelNr, vCard)
                            End Select
                            'Im folgenden wird automatisch ein Kontakt erstellt, der durch die Rückwärtssuche ermittlt wurde. Dies geschieht nur, wenn es gewünscht ist.
                            If rws And C_XML.P_CBKErstellen Then
                                C_Kontakt.ErstelleKontakt(KontaktID, StoreID, vCard, TelNr)
                                C_OlI.KontaktInformation(KontaktID, StoreID, FullName:=FullName, CompanyName:=CompanyName)
                                Anrufer = Replace(FullName & " (" & CompanyName & ")", " ()", "")
                            End If
                        Else
                            rws = True
                        End If
                        If rws And KontaktID = "-1;" Then
                            Anrufer = ReadFNfromVCard(vCard)
                            Anrufer = Replace(Anrufer, Chr(13), "", , , CompareMethod.Text)
                            If InStr(1, Anrufer, "Firma", CompareMethod.Text) = 1 Then
                                Anrufer = Right(Anrufer, Len(Anrufer) - 5)
                            End If
                            Anrufer = Trim(Anrufer)
                            If RWSIndex Then
                                xPathTeile.Item(xPathTeile.Count - 1) = "Eintrag"
                                C_XML.Write(xPathTeile, vCard, "ID", TelNr)
                            End If
                            KontaktID = "-1" & Anrufer & ";" & vCard
                        End If
                    End If
                    TelNr = C_hf.formatTelNr(TelNr)
                End If
            End If
            ' Daten im Menü für Wahlwiederholung speichern
            UpdateList("CallList", Anrufer, TelNr, FBStatus(0), StoreID, KontaktID)
#If OVer < 14 Then
            If C_XML.P_CBSymbWwdh Then C_GUI.FillPopupItems("Wwdh")
#End If
            'StoppUhr
            If C_XML.P_CBStoppUhrEinblenden And StoppUhrAnzeigen Then
                With STUhrDaten(ID)
                    .Richtung = "Anruf zu:"
                    If Anrufer = "" Then
                        .Anruf = TelNr
                    Else
                        .Anruf = Anrufer
                    End If
                End With
            End If
            ' Daten für den Journaleintrag sichern
            If C_XML.P_CBJournal Or C_XML.P_CBStoppUhrEinblenden Then
                NeuerJournalEintrag(ID, "Ausgehender Anruf zu", CStr(FBStatus.GetValue(0)), MSN, TelNr, KontaktID, StoreID)
                JEReadorWrite(False, ID, "NSN", CStr(FBStatus.GetValue(3)))
            End If
        End If
    End Sub '(AnrMonCALL)

    ''' <summary>
    ''' Behandelt den vom Anrufmonitor der Fritz!Box erhaltener String für CONNECT
    ''' </summary>
    ''' <param name="FBStatus">String: Vom Anrufmonitor der Fritz!Box erhaltener String für CONNECT</param>
    ''' <param name="StoppUhrAnzeigen">Boolean: Soll StoppUhr angezeigt werden. Bei Journalimport nicht, ansonsten ja (unabhängig von der Einstellung des Users)</param>
    ''' <remarks></remarks>
    Friend Sub AnrMonCONNECT(ByVal FBStatus As String(), ByVal StoppUhrAnzeigen As Boolean)
        ' wertet eine Zustande gekommene Verbindung aus
        ' Parameter: FBStatus (String()):  Status-String der FritzBox

        ' FBStatus(0): Uhrzeit
        ' FBStatus(1): CONNECT, wird nicht verwendet
        ' FBStatus(2): Die Nummer der aktuell aufgebauten Verbindungen (0 ... n), dient zur Zuordnung der Telefonate, ID
        ' FBStatus(3): Nebenstellennummer, eindeutige Zuordnung des Telefons


        Dim xPathTeile As New ArrayList
        Dim MSN As String = "-1"
        Dim NSN As Integer
        Dim ID As Integer
        Dim Zeit As String

        If (C_XML.P_CBJournal) Or (C_XML.P_CBStoppUhrEinblenden And StoppUhrAnzeigen) Then
            ID = CInt(FBStatus.GetValue(2))
            NSN = CInt(FBStatus.GetValue(3))

            MSN = JEReadorWrite(True, ID, "MSN", "")
            If MSN = "-1" Then
                ' Wenn Journal nicht erstellt wird, muss MSN anderweitig ermittelt werden.
                Select Case NSN
                    Case 0 To 2 ' FON1-3
                        NSN += 1
                    Case 10 To 19 ' DECT
                        NSN += 50
                End Select
                Select Case NSN
                    Case 3, 4, 5, 36, 37
                        ' Diese komischen Dinger werden ignoriert:
                        ' 3=Durchwahl
                        ' 4=ISDN Gerät
                        ' 5=Fax (intern/PC)
                        ' 36=Data S0
                        ' 37=Data PC
                        MSN = "-1"
                    Case Else
                        With xPathTeile
                            .Add("Telefone")
                            .Add("Telefone")
                            .Add("*")
                            .Add("Telefon[@Dialport = """ & NSN & """]")
                            .Add("TelNr")
                        End With
                        MSN = C_XML.Read(xPathTeile, "")
                End Select
            End If

            If MSN = "-1" Then
                C_hf.LogFile("Ein unvollständiges Telefonat wurde registriert.")
            Else
                With xPathTeile
                    .Clear()
                    .Add("Telefone")
                    .Add("Nummern")
                    .Add("*")
                    .Add("[. = """ & Replace(MSN, ";", """ or . = """, , , CompareMethod.Text) & """]")
                    .Add("@Checked")
                End With

                If C_hf.IsOneOf("1", Split(C_XML.Read(xPathTeile, "0;") & ";", ";", , CompareMethod.Text)) Or AnrMonPhoner Then
                    If C_XML.P_CBJournal Then
                        JEReadorWrite(False, ID, "NSN", CStr(FBStatus.GetValue(3)))
                        JEReadorWrite(False, ID, "Zeit", CStr(FBStatus.GetValue(0)))
                    End If
                    ' StoppUhr einblenden
                    If C_XML.P_CBStoppUhrEinblenden And StoppUhrAnzeigen Then
                        C_hf.LogFile("StoppUhr wird eingeblendet.")
                        With System.DateTime.Now
                            Zeit = String.Format("{0:00}:{1:00}:{2:00}", .Hour, .Minute, .Second)
                        End With
                        With STUhrDaten(ID)
                            .MSN = MSN
                            .StartZeit = Zeit
                            .Abbruch = False
                        End With
                        BWStoppuhrEinblenden = New BackgroundWorker
                        With BWStoppuhrEinblenden
                            .WorkerSupportsCancellation = True
                            .RunWorkerAsync(ID)
                        End With
                    End If
                End If
            End If
        End If
    End Sub '(AnrMonCONNECT)

    ''' <summary>
    ''' Behandelt den vom Anrufmonitor der Fritz!Box erhaltener String für DISCONNECT
    ''' </summary>
    ''' <param name="FBStatus">String: Vom Anrufmonitor der Fritz!Box erhaltener String für DISCONNECT</param>
    ''' <param name="StoppUhrAnzeigen">Boolean: Soll StoppUhr angezeigt werden. Bei Journalimport nicht, ansonsten ja (unabhängig von der Einstellung des Users)</param>
    ''' <remarks></remarks>
    Friend Sub AnrMonDISCONNECT(ByVal FBStatus As String(), ByVal StoppUhrAnzeigen As Boolean)
        ' legt den Journaleintrag (und/oder Kontakt) an
        ' Parameter: FBStatus (String):     Status-String der FritzBox

        ' FBStatus(0): Uhrzeit
        ' FBStatus(1): DISCONNECT, wird nicht verwendet
        ' FBStatus(2): Die Nummer der aktuell aufgebauten Verbindungen (0 ... n), dient zur Zuordnung der Telefonate, ID
        ' FBStatus(3): Dauer des Telefonates

        Dim ID As Integer = CInt(FBStatus.GetValue(2))          ' ID des Telefonats
        Dim Dauer As Integer = CInt(FBStatus.GetValue(3))          ' Dauer des Telefonats in s
        Dim AnrName As String              ' Name des Telefonpartners
        Dim Firma As String              ' Firma des Telefonpartners
        Dim Body As String              ' Text des Journaleintrags
        Dim vCard As String              ' vCard des Telefonpartners
        ' die zum Anruf gehörende MSN oder VoIP-Nr
        Dim TelName As String
        Dim tmpTelName As String = vbNullString

        Dim NSN As Double = -1
        Dim Zeit As String = vbNullString
        Dim Typ As String = vbNullString
        Dim MSN As String = vbNullString
        Dim TelNr As String = vbNullString
        Dim StoreID As String = vbNullString
        Dim KontaktID As String = vbNullString

        Dim FritzFolderExists As Boolean = False
        Dim SchließZeit As Date = C_XML.P_StatOLClosedZeit

        Dim xPathTeile As New ArrayList

        If C_XML.P_CBJournal Then
            JIauslesen(ID, NSN, Zeit, Typ, MSN, TelNr, StoreID, KontaktID)
            Dim JMSN As String = C_hf.OrtsVorwahlEntfernen(MSN, C_XML.P_TBVorwahl)
            If Not MSN = Nothing Then
                With xPathTeile
                    .Add("Telefone")
                    .Add("Nummern")
                    .Add("*")
                    .Add("[. = """ & C_hf.OrtsVorwahlEntfernen(MSN, C_XML.P_TBVorwahl) & """]")
                    .Add("@Checked")
                End With

                If C_hf.IsOneOf("1", Split(C_XML.Read(xPathTeile, "0;") & ";", ";", , CompareMethod.Text)) Or AnrMonPhoner Then

                    Body = "Tel.-Nr.: " & TelNr & vbCrLf & "Status: " & CStr(IIf(Dauer = 0, "nicht ", vbNullString)) & "angenommen" & vbCrLf & vbCrLf

                    If Left(KontaktID, 2) = "-1" Then
                        ' kein Kontakt vorhanden
                        AnrName = Mid(KontaktID, 3, InStr(KontaktID, ";") - 3)
                        If AnrName = "" Then AnrName = TelNr
                        If InStr(1, AnrName, "Firma", CompareMethod.Text) = 1 Then
                            AnrName = Right(AnrName, Len(AnrName) - 5)
                        End If
                        AnrName = Trim(AnrName)
                        vCard = Mid(KontaktID, InStr(KontaktID, ";") + 1)
                        Firma = ReadFromVCard(vCard, "ORG", "")
                        If Not vCard = "" Then Body = Body & "Kontaktdaten (vCard):" & vbCrLf & vCard & vbCrLf
                    Else
                        ' Kontakt in den 'Links' eintragen
                        Dim FullName As String = vbNullString
                        Dim CompanyName As String = vbNullString
                        Dim HomeAddress As String = vbNullString
                        Dim BusinessAddress As String = vbNullString

                        C_OlI.KontaktInformation(KontaktID, StoreID, FullName:=FullName, CompanyName:=CompanyName, BusinessAddress:=BusinessAddress, HomeAddress:=HomeAddress)

                        If FullName = "" Then
                            If CompanyName = "" Then
                                AnrName = TelNr
                            Else
                                AnrName = CompanyName
                            End If
                        Else
                            AnrName = FullName
                        End If
                        Firma = CompanyName
                        If Firma = "" Then
                            If Not HomeAddress = "" Then
                                Body = Body & "Kontaktdaten:" & vbCrLf & AnrName _
                                    & vbCrLf & Firma & vbCrLf & HomeAddress & vbCrLf
                            End If
                        Else
                            If Not BusinessAddress = "" Then
                                Body = Body & "Kontaktdaten:" & vbCrLf & AnrName _
                                    & vbCrLf & Firma & vbCrLf & BusinessAddress & vbCrLf
                            End If
                        End If
                    End If

                    Select Case NSN
                        Case 0 To 2 ' FON1-3
                            NSN += 1
                        Case 10 To 19 ' DECT
                            NSN += 50
                    End Select
                    Select Case NSN
                        Case 3
                            TelName = "Durchwahl"
                        Case 4
                            TelName = "ISDN Gerät"
                        Case 5
                            TelName = "Fax (intern/PC)"
                        Case 36
                            TelName = "Data S0"
                        Case 37
                            TelName = "Data PC"
                        Case Else
                            With xPathTeile
                                .Clear()
                                .Add("Telefone")
                                .Add("Telefone")
                                .Add("*")
                                .Add("Telefon[@Dialport = """ & NSN & """]")
                                .Add("TelName")
                            End With
                            TelName = C_XML.Read(xPathTeile, "")
                    End Select
                    'With xPathTeile
                    '    .Clear()
                    '    .Add("Telefone")
                    '    .Add("Telefone")
                    '    .Add("*")
                    '    .Add("Telefon")
                    '    .Add("[@Dialport = """ & NSN & """]")
                    '    .Add("TelName")
                    'End With
                    'TelName = C_XML.Read(xPathTeile, "")
                    ' Journaleintrag schreiben
                    C_OlI.ErstelleJournalItem(Subject:=Typ & " " & AnrName & CStr(IIf(AnrName = TelNr, vbNullString, " (" & TelNr & ")")) & CStr(IIf(Split(TelName, ";", , CompareMethod.Text).Length = 1, vbNullString, " (" & TelName & ")")), _
                                              Duration:=CInt(IIf(Dauer > 0 And Dauer <= 30, 31, Dauer)) / 60, _
                                              Body:=Body, _
                                              Start:=CDate(Zeit), _
                                              Companies:=Firma, _
                                              Categories:=TelName & "; FritzBox Anrufmonitor; Telefonanrufe", _
                                              KontaktID:=KontaktID, _
                                              StoreID:=StoreID)
                    If Dauer = 0 Then
                        If Left(Typ, 3) = "Ein" Then
                            Typ = "Verpasster Anruf von"
                            C_XML.P_StatVerpasst += 1
                        Else
                            Typ = "Nicht erfolgreicher Anruf zu"
                            C_XML.P_StatNichtErfolgreich += 1
                        End If
                    End If
                    If Dauer > 0 Then
                        With xPathTeile
                            .Item(.Count - 1) = IIf(Mid(Typ, 1, 3) = "Ein", "Eingehend", "Ausgehend")
                        End With
                        With C_XML
                            .Write(xPathTeile, CStr(CInt(.Read(xPathTeile, CStr(0))) + Dauer))
                        End With
                    End If
                    C_XML.P_StatJournal += 1

                    If CDate(Zeit) > SchließZeit Or SchließZeit = System.DateTime.Now Then C_XML.P_StatOLClosedZeit = System.DateTime.Now.AddMinutes(1)
                    JEentfernen(ID)
                End If
            Else
                C_hf.LogFile("AnrMonDISCONNECT: Ein unvollständiges Telefonat wurde registriert.")
                With xPathTeile
                    .Add("Telefone")
                    .Add("Nummern")
                    .Add("*")
                    .Add("[. = """ & C_hf.OrtsVorwahlEntfernen(JMSN, C_XML.P_TBVorwahl) & """]")
                    .Add("@Checked")
                End With
                If C_XML.P_CBJournal And C_hf.IsOneOf("1", Split(C_XML.Read(xPathTeile, "0;") & ";", ";", , CompareMethod.Text)) Then
                    ' Wenn Anruf vor dem Outlookstart begonnen wurde, wurde er nicht nachträglich importiert.
                    Dim ZeitAnruf As Date = CDate(FBStatus(0))
                    ZeitAnruf = ZeitAnruf.AddSeconds(-1 * (ZeitAnruf.Second + Dauer + 70))
                    If ZeitAnruf < SchließZeit Then C_XML.P_StatOLClosedZeit = ZeitAnruf
                    C_hf.LogFile("AnrMonDISCONNECT: Journalimport wird gestartet")
                    Dim formjournalimort As New formJournalimport(Me, C_hf, C_XML, False)
                End If
            End If
        End If

        If C_XML.P_CBStoppUhrEinblenden And StoppUhrAnzeigen Then
            STUhrDaten(ID).Abbruch = True
        End If
    End Sub '(AnrMonDISCONNECT)
#End Region

#Region "Journaleinträge"
    Sub NeuerJournalEintrag(ByVal ID As Integer, _
                            ByVal Typ As String, _
                            ByVal Zeit As String, _
                            ByVal MSN As String, _
                            ByVal TelNr As String, _
                            ByVal KontaktID As String, _
                            ByVal StoreID As String)

        Dim LANodeNames As New ArrayList
        Dim LANodeValues As New ArrayList
        Dim LAAttributeNames As New ArrayList
        Dim LAAttributeValues As New ArrayList
        Dim xPathTeile As New ArrayList

        If Not Typ Is vbNullString Then
            LANodeNames.Add("Typ")
            LANodeValues.Add(Typ)
        End If

        If Not Typ Is vbNullString Then
            LANodeNames.Add("Zeit")
            LANodeValues.Add(Zeit)
        End If

        If Not MSN Is vbNullString Then
            LANodeNames.Add("MSN")
            LANodeValues.Add(MSN)
        End If

        If Not TelNr Is vbNullString Then
            LANodeNames.Add("TelNr")
            LANodeValues.Add(TelNr)
        End If

        If Not KontaktID Is vbNullString Then
            LANodeNames.Add("KontaktID")
            LANodeValues.Add(KontaktID)
        End If

        If Not StoreID Is vbNullString Then
            LANodeNames.Add("StoreID")
            LANodeValues.Add(StoreID)
        End If


        LAAttributeNames.Add("ID")
        LAAttributeValues.Add(CStr(ID))
        xPathTeile.Add("Journal")

        With C_XML
            .AppendNode(xPathTeile, .CreateXMLNode("Eintrag", LANodeNames, LANodeValues, LAAttributeNames, LAAttributeValues))
        End With
        xPathTeile = Nothing
    End Sub

    Sub JIauslesen(ByVal ID As Integer, _
               ByRef NSN As Double, _
               ByRef Zeit As String, _
               ByRef Typ As String, _
               ByRef MSN As String, _
               ByRef TelNr As String, _
               ByRef StoreID As String, _
               ByRef KontaktID As String)

        Dim LANodeNames As New ArrayList
        Dim LANodeValues As New ArrayList
        Dim xPathTeile As New ArrayList

        ' Uhrzeit
        LANodeNames.Add("Zeit")
        LANodeValues.Add("-1")

        ' Typ
        LANodeNames.Add("Typ")
        LANodeValues.Add("-1")

        ' TelNr
        LANodeNames.Add("TelNr")
        LANodeValues.Add("-1")

        ' MSN
        LANodeNames.Add("MSN")
        LANodeValues.Add("-1")

        ' NSN
        LANodeNames.Add("NSN")
        LANodeValues.Add(-1)

        ' StoreID
        LANodeNames.Add("StoreID")
        LANodeValues.Add("-1")

        ' KontaktID
        LANodeNames.Add("KontaktID")
        LANodeValues.Add("-1;")

        With xPathTeile
            .Add("Journal")
            .Add("Eintrag")
        End With
        C_XML.ReadXMLNode(xPathTeile, LANodeNames, LANodeValues, CStr(ID))

        Zeit = CStr(LANodeValues.Item(LANodeNames.IndexOf("Zeit")))
        Typ = CStr(LANodeValues.Item(LANodeNames.IndexOf("Typ")))
        TelNr = CStr(LANodeValues.Item(LANodeNames.IndexOf("TelNr")))
        MSN = CStr(LANodeValues.Item(LANodeNames.IndexOf("MSN")))
        NSN = CDbl(LANodeValues.Item(LANodeNames.IndexOf("NSN")))
        StoreID = CStr(LANodeValues.Item(LANodeNames.IndexOf("StoreID")))
        KontaktID = CStr(LANodeValues.Item(LANodeNames.IndexOf("KontaktID")))

        xPathTeile = Nothing
        LANodeNames = Nothing
        LANodeValues = Nothing
    End Sub

    Function JEReadorWrite(ByVal JERead As Boolean, ByVal ID As Integer, ByVal Name As String, ByVal Value As String) As String

        Dim xPathTeile As New ArrayList
        With xPathTeile
            .Add("Journal")
            .Add("Eintrag[@ID=""" & ID & """]")
            .Add(Name)
            If JERead Then
                JEReadorWrite = C_XML.Read(xPathTeile, "-1")
            Else
                JEReadorWrite = CStr(C_XML.Write(xPathTeile, Value))
            End If
        End With
        xPathTeile = Nothing
    End Function

    Sub JEentfernen(ID As Integer)
        Dim xPathTeile As New ArrayList
        With xPathTeile
            .Add("Journal")
            .Add("Eintrag[@ID=""" & ID & """]")
            C_XML.Delete(xPathTeile)
        End With
    End Sub
#End Region

#Region "LetzterAnrufer"
    Sub SpeichereLetzerAnrufer(ByVal ID As String, ByVal LA As String())
        'LA(0) = Zeit
        'LA(1) = Anrufer
        'LA(2) = TelNr
        'LA(3) = MSN
        'LA(4) = StoreID
        'LA(5) = KontaktID

        Dim LANodeNames As New ArrayList
        Dim LANodeValues As New ArrayList
        Dim xPathTeile As New ArrayList
        Dim AttributeNames As New ArrayList
        Dim AttributeValues As New ArrayList

        ' Uhrzeit
        LANodeNames.Add("Zeit")
        LANodeValues.Add(LA(0))

        ' Anrufername
        If Not LA(1) Is vbNullString Then
            LANodeNames.Add("Anrufer")
            LANodeValues.Add(LA(1))
        End If

        ' TelNr
        LANodeNames.Add("TelNr")
        LANodeValues.Add(LA(2))

        ' MSN
        LANodeNames.Add("MSN")
        LANodeValues.Add(LA(3))

        ' StoreID
        If Not LA(4) Is vbNullString Then
            LANodeNames.Add("StoreID")
            LANodeValues.Add(LA(4))
        End If

        ' KontaktID
        If Not LA(4) Is vbNullString Then
            LANodeNames.Add("KontaktID")
            LANodeValues.Add(LA(5))
        End If

        AttributeNames.Add("ID")
        AttributeValues.Add(ID)

        xPathTeile.Add("LetzterAnrufer")
        xPathTeile.Add("Letzter")

        With C_XML
            .Write(xPathTeile, ID)
            xPathTeile.Remove("Letzter")
            .AppendNode(xPathTeile, .CreateXMLNode("Eintrag", LANodeNames, LANodeValues, AttributeNames, AttributeValues))
        End With
        xPathTeile = Nothing
        LANodeNames = Nothing
        LANodeValues = Nothing
        AttributeNames = Nothing
        AttributeValues = Nothing
    End Sub
#End Region

#Region "RingCallList"
    Sub UpdateList(ByVal ListName As String, _
                   ByVal Anrufer As String, _
                   ByVal TelNr As String, _
                   ByVal Zeit As String, _
                   ByVal StoreID As String, _
                   ByVal KontaktID As String)

        Dim NodeNames As New ArrayList
        Dim NodeValues As New ArrayList
        Dim AttributeNames As New ArrayList
        Dim AttributeValues As New ArrayList
        Dim xPathTeile As New ArrayList
        Dim index As Integer              ' Zählvariable

        index = CInt(C_XML.Read(ListName, "Index", "0"))

        xPathTeile.Add(ListName)
        xPathTeile.Add("Eintrag[@ID=""" & index & """]")
        xPathTeile.Add("TelNr")
        If Not C_XML.Read(xPathTeile, "0") = TelNr Then

            If Not Anrufer Is vbNullString Then
                NodeNames.Add("Anrufer")
                NodeValues.Add(Anrufer)
            End If

            If Not TelNr Is vbNullString Then
                NodeNames.Add("TelNr")
                NodeValues.Add(TelNr)
            End If

            If Not Zeit Is vbNullString Then
                NodeNames.Add("Zeit")
                NodeValues.Add(Zeit)
            End If

            NodeNames.Add("Index")
            NodeValues.Add(CStr((index + 1) Mod 10))

            If Not StoreID Is vbNullString Then
                NodeNames.Add("StoreID")
                NodeValues.Add(StoreID)
            End If

            If Not KontaktID Is vbNullString Then
                NodeNames.Add("KontaktID")
                NodeValues.Add(KontaktID)
            End If

            AttributeNames.Add("ID")
            AttributeValues.Add(CStr(index))

            With C_XML
                xPathTeile.RemoveRange(0, xPathTeile.Count)
                xPathTeile.Add(ListName)
                xPathTeile.Add("Index")
                .Write(xPathTeile, CStr((index + 1) Mod 10))
                xPathTeile.Remove("Index")
                .AppendNode(xPathTeile, .CreateXMLNode("Eintrag", NodeNames, NodeValues, AttributeNames, AttributeValues))
            End With
        End If
        xPathTeile = Nothing
        NodeNames = Nothing
        NodeValues = Nothing
        AttributeNames = Nothing
        AttributeValues = Nothing
#If OVer > 12 Then
        C_GUI.RefreshRibbon()
#End If

    End Sub
#End Region

End Class
