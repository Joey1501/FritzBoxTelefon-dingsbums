Imports System.Timers
Imports System.IO.Path
Public Class formAnrMon
    Private DateiPfad As String
    Private TelefonName As String
    Private aID As Integer
    Private ini As Ini
    Private HelferFunktionen As Helfer
    Private TelNr As String              ' TelNr des Anrufers
    Private KontaktID As String              ' KontaktID des Anrufers
    Private StoreID As String
    Private MSN As String
    Private AnrMon As AnrufMonitor
    Public AnrmonClosed As Boolean
    Private OlI As OutlookInterface
    Private WithEvents TimerAktualisieren As Timer


    Public Sub New(ByVal sDateiPfad As String, ByVal iAnrufID As Integer, ByVal Aktualisieren As Boolean, _
                   ByVal iniKlasse As InI, ByVal HelferKlasse As Helfer, ByVal AnrufMon As AnrufMonitor, OutlInter As OutlookInterface)

        ' Dieser Aufruf ist f�r den Windows Form-Designer erforderlich.
        InitializeComponent()
        HelferFunktionen = HelferKlasse
        ' F�gen Sie Initialisierungen nach dem InitializeComponent()-Aufruf hinzu.
        'If ThisAddIn.Debug Then ThisAddIn.Diagnose.AddLine("formAnrMon aufgerufen")
        DateiPfad = sDateiPfad
        aID = iAnrufID
        ini = iniKlasse
        OlI = OutlInter
        AnrMon = AnrufMon
        AnrMonausf�llen()
        AnrmonClosed = False


        Dim OInsp As Outlook.Inspector = Nothing
        If Aktualisieren Then
            TimerAktualisieren = HelferFunktionen.SetTimer(100)
            If TimerAktualisieren Is Nothing Then
                HelferFunktionen.LogFile("formAnrMon.New: TimerNeuStart nicht gestartet")
            End If
        End If
        OlI.InspectorVerschieben(True)

        With PopupNotifier
            .ShowDelay = CInt(ini.Read(DateiPfad, "Optionen", "TBEnblDauer", "10")) * 1000
            .AutoAusblenden = CBool(ini.Read(DateiPfad, "Optionen", "CBAutoClose", "True"))
            Dim FormVerschiebung As New Drawing.Size(CInt(ini.Read(DateiPfad, "Optionen", "TBAnrMonX", "0")), CInt(ini.Read(DateiPfad, "Optionen", "TBAnrMonY", "0")))
            .PositionsKorrektur = FormVerschiebung
            .EffektMove = CBool(ini.Read(DateiPfad, "Optionen", "CBAnrMonMove", "True"))
            .EffektTransparenz = CBool(ini.Read(DateiPfad, "Optionen", "CBAnrMonTransp", "True"))
            .EffektMoveGeschwindigkeit = CInt(ini.Read(DateiPfad, "Optionen", "TBAnrMonMoveGeschwindigkeit", "50"))
            .Popup()
        End With
        OlI.InspectorVerschieben(False)
    End Sub

    Sub AnrMonausf�llen()
        ' Diese Funktion nimmt Daten aus der Registry und �ffnet 'formAnMon'.
        Dim AnrName As String              ' Name des Anrufers
        Dim letzterAnrufer() As String = Split(ini.Read(HelferFunktionen.Dateipfade(DateiPfad, "Listen"), "letzterAnrufer", "letzterAnrufer " & aID, CStr(DateTime.Now) & ";;unbekannt;;-1;-1;"), ";", 6, CompareMethod.Text)

        AnrName = letzterAnrufer(1)
        TelNr = letzterAnrufer(2)
        MSN = letzterAnrufer(3)
        StoreID = letzterAnrufer(4)
        KontaktID = letzterAnrufer(5)
        TelefonName = AnrMon.TelefonName(MSN)
        With PopupNotifier
            If TelNr = "unbekannt" Then
                With .OptionsMenu
                    .Items("ToolStripMenuItemR�ckruf").Enabled = False ' kein R�ckruf im Fall 1
                    .Items("ToolStripMenuItemKopieren").Enabled = False ' in dem Fall sinnlos
                    .Items("ToolStripMenuItemKontakt�ffnen").Text = "Einen neuen Kontakt erstellen"
                End With
            End If
            ' Uhrzeit des Telefonates eintragen
            .Uhrzeit = letzterAnrufer(0)
            ' Telefonnamen eintragen
            .TelName = TelefonName & CStr(IIf(CBool(ini.Read(DateiPfad, "Optionen", "CBShowMSN", "False")), " (" & MSN & ")", vbNullString))

            If Not Strings.Left(KontaktID, 2) = "-1" Then
                If Not TimerAktualisieren Is Nothing Then HelferFunktionen.KillTimer(TimerAktualisieren)
                ' Kontakt einblenden wenn in Outlook gefunden
                Try
                    OlI.KontaktInformation(KontaktID, StoreID, PopupNotifier.AnrName, PopupNotifier.Firma)
                    If CBool(ini.Read(DateiPfad, "Optionen", "CBAnrMonContactImage", "True")) Then
                        Dim BildPfad = OlI.KontaktBild(KontaktID, StoreID)
                        If Not BildPfad Is vbNullString Then
                            PopupNotifier.Image = Drawing.Image.FromFile(BildPfad)
                            ' Seitenverh�ltnisse anpassen
                            Dim Bildgr��e As New Drawing.Size(PopupNotifier.ImageSize.Width, CInt((PopupNotifier.ImageSize.Width * PopupNotifier.Image.Size.Height) / PopupNotifier.Image.Size.Width))
                            PopupNotifier.ImageSize = Bildgr��e
                        End If
                    End If
                Catch ex As Exception
                    HelferFunktionen.LogFile("formAnrMon: Fehler beim �ffnen des Kontaktes " & AnrName & " (" & ex.Message & ")")
                    .Firma = ""
                    If AnrName = "" Then
                        .TelNr = ""
                        .AnrName = TelNr
                    Else
                        .TelNr = TelNr
                        .AnrName = AnrName
                    End If
                End Try

                .TelNr = TelNr
            Else
                .Firma = ""
                If AnrName = "" Then
                    .TelNr = ""
                    .AnrName = TelNr
                Else
                    .TelNr = TelNr
                    .AnrName = AnrName
                End If
            End If
        End With
    End Sub

    Private Sub PopupNotifier_Close() Handles PopupNotifier.Close
        PopupNotifier.hide()
    End Sub

    Private Sub ToolStripMenuItemR�ckruf_Click(ByVal sender As Object, ByVal e As System.EventArgs) Handles ToolStripMenuItemR�ckruf.Click
        ThisAddIn.WClient.Rueckruf(aID)
    End Sub

    Private Sub ToolStripMenuItemKopieren_Click(ByVal sender As Object, ByVal e As System.EventArgs) Handles ToolStripMenuItemKopieren.Click
        With PopupNotifier
            My.Computer.Clipboard.SetText(.AnrName & CStr(IIf(Len(.TelNr) = 0, "", " (" & .TelNr & ")")))
        End With
    End Sub

    Private Sub PopupNotifier_Closed() Handles PopupNotifier.Closed
        AnrmonClosed = True
        If Not TimerAktualisieren Is Nothing Then HelferFunktionen.KillTimer(TimerAktualisieren)
    End Sub

    Private Sub ToolStripMenuItemKontakt�ffnen_Click() Handles ToolStripMenuItemKontakt�ffnen.Click, PopupNotifier.LinkClick
        ' blendet den Kontakteintrag des Anrufers ein
        ' ist kein Kontakt vorhanden, dann wird einer angelegt und mit den vCard-Daten ausgef�llt
        Dim Kontaktdaten(2) As String
        Kontaktdaten(0) = KontaktID
        Kontaktdaten(1) = StoreID
        Kontaktdaten(2) = TelNr
        ThisAddIn.WClient.ZeigeKontakt(Kontaktdaten)
    End Sub

    Private Sub TimerAktualisieren_Elapsed(ByVal sender As Object, ByVal e As System.Timers.ElapsedEventArgs) Handles TimerAktualisieren.Elapsed
        Dim VergleichString As String = PopupNotifier.AnrName
        AnrMonausf�llen()
        If Not VergleichString = PopupNotifier.AnrName Then HelferFunktionen.KillTimer(TimerAktualisieren)
    End Sub

    Protected Overrides Sub Finalize()
        MyBase.Finalize()
    End Sub
End Class
