﻿Imports System.Diagnostics
Imports System.Data.SqlClient

Public Class F_Main
    Private szText As String
    Private PlayList As New PlayList
    Private isShort As String
    Private mySettings As New PPLInk.Settings
    Private isAutoShort As String

    Private Sub F_Main_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Dim szConn = mySettings.ProHelpConnectionUser
        Dim szDebug = mySettings.ProHelpDebug
        Dim connection As New SqlConnection(mySettings.ProHelpConnectionString)
        Dim szVersion As String

        Try
            szVersion = PPLInk.My.Application.Deployment.CurrentVersion.ToString
        Catch
            szVersion = "1.5.0.48 Proto"
        End Try
        LblVersion.Text = szVersion
        'Z.Y.X.W - Z.Y is major/minor version; X is build number, always 0; W is VS publish number
        'Z.X.Y are set in Project Properties, W is automatic (also controlled in Prroject Properties)
        'See the end of the file for history

        If szDebug = "Yes" Then MessageBox.Show("loading")
        If szConn = "" Then
            MessageBox.Show("The settings for PowerPoint Link have not been created.  Please use the Set Up button to initialise this program",
                            "PowerPoint Link: Settings Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        Else
            Try
                If szDebug = "Yes" Then MessageBox.Show("trying " & szConn)
                connection = New SqlConnection(szConn)
                T_filesTableAdapter.Connection = connection
            Catch ex As Exception
                MessageBox.Show("Connection Failure:" & vbCrLf & ex.Message & vbCrLf & ex.StackTrace,
                                "PowerPoint Link: Connecting", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End If

        ' Do necessary upgrades
        Dim priorVersion = mySettings.ProHelpVersion
        Dim bUpdated As Boolean = False
        Select Case priorVersion
            Case ""
                ' Doesn't have extended length of t_files.f_altname
                Dim cmd1 As SqlCommand = New SqlCommand("use ProHelp", connection)
                Dim cmd2 As SqlCommand = New SqlCommand("Alter table t_files Alter column f_altname varchar(250)", connection)
                connection.Open()
                cmd1.ExecuteNonQuery()
                cmd2.ExecuteNonQuery()
                mySettings.ProHelpVersion = "1.4.0.36"
                mySettings.Save()
                MessageBox.Show("Upgraded length of t_files.f_altname",
                                "PowerPointLink: Automatic Upgrader", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                bUpdated = True

            Case "1.4.36"
                mySettings.ProHelpVersion = "1.4.0.36"
                mySettings.Save()

            Case szVersion
                ' NOP

            Case >= "1.4.0.39"
                mySettings.ProHelpVersion = szVersion
                mySettings.Save()

            Case >= "1.4.0.36"
                Dim cmd1 As SqlCommand = New SqlCommand("use ProHelp", connection)
                Dim cmd2 As SqlCommand = New SqlCommand("update t_files set last_dt = create_dt where last_dt is null", connection)
                connection.Open()
                cmd1.ExecuteNonQuery()
                cmd2.ExecuteNonQuery()
                mySettings.ProHelpVersion = szVersion
                mySettings.Save()
                MessageBox.Show("Upgraded t_files.last_dt - never null, 1.4.0.36")
                bUpdated = True

        End Select
        If bUpdated Then
            MessageBox.Show("An upgrade was done.  PowerPoint Link must be restarted",
                                "PowerPointLink: Automatic Upgrader", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Close()
        End If

        If szDebug = "Yes" Then MessageBox.Show("filling")
        If mySettings.ProHelpSelected = "Y" Then
            ChkShortList.Checked = True
            isShort = "Y"
        Else
            ChkShortList.Checked = False
            isShort = "N"
        End If
        Me.T_filesTableAdapter.Fill(Me.ProHelpDataSet.t_files, isShort)
        isAutoShort = mySettings.ProHelpAutoShortList
        TxtSearch.Focus()
    End Sub

    Private Sub BtnClear_Click(sender As Object, e As EventArgs) Handles BtnClear.Click
        LBPlayList.Items.Clear()
        TxtSearch.Text = ""
    End Sub

    Private Sub BtnPlay_Click(sender As Object, e As EventArgs) Handles BtnPlay.Click
        If LBPlayList.Items.Count = 0 Then
            MessageBox.Show("Please add some items to the list",
                            "PowerPoint Link: Playing the list", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        MyBase.WindowState = FormWindowState.Minimized
        PlayList.Run(LBPlayList.Items)
        MyBase.WindowState = FormWindowState.Normal
        TxtSearch.Text = ""
        TxtSearch.Focus()
    End Sub

    Private Sub Files_DoubleClick(sender As Object, e As EventArgs) Handles T_filesDataGridView.DoubleClick
        Dim szFName As String, szFile_no As Integer

        szFName = T_filesDataGridView.SelectedRows(0).Cells(1).Value
        szFile_no = T_filesDataGridView.SelectedRows(0).Cells(0).Value
        If isAutoShort = "Y" Then AddToShortList(CInt(szFile_no))
        LBInstant.Items.Clear()
        LBInstant.Items.Add(szFile_no & "::" & szFName)
        MyBase.WindowState = FormWindowState.Minimized
        PlayList.Run(LBInstant.Items)
        MyBase.WindowState = FormWindowState.Normal
        TxtSearch.Text = ""
        TxtSearch.Focus()
    End Sub

    Private Sub BtnAdd_Click(sender As Object, e As EventArgs) Handles BtnAdd.Click
        PlayList_AddRecord()
        TxtSearch.Text = ""
        TxtSearch.Focus()
    End Sub

    Private Sub TxtSearch_TextChanged(sender As Object, e As EventArgs) Handles TxtSearch.TextChanged
        szText = TxtSearch.Text
        T_filesTableAdapter.FillByPhrase(ProHelpDataSet.t_files, szText, isShort)
    End Sub

    Private Sub TxtSearch_KeyDown(sender As Object, e As KeyEventArgs) Handles TxtSearch.KeyDown
        Select Case e.KeyCode
            Case Keys.Enter
                PlayList_AddRecord()
                TxtSearch.Text = ""
            Case Keys.Up
                T_filesChangeSelection(-1)
            Case Keys.Down
                T_filesChangeSelection(1)
            Case Else
                Exit Sub
        End Select

        e.SuppressKeyPress = True
        TxtSearch.Focus()
    End Sub

    Sub T_filesChangeSelection(ByVal iChange As Integer)
        Dim iRow As Integer = 0
        'Dim iSelected As DataGridViewRow = T_filesDataGridView.SelectedRows(0)

        While iRow < T_filesDataGridView.RowCount
            If T_filesDataGridView.SelectedRows.Contains(T_filesDataGridView.Rows(iRow)) Then Exit While
            iRow = iRow + 1
        End While

        If iRow = T_filesDataGridView.RowCount Then
            ' no selected row
            Exit Sub
        ElseIf iChange = 1 And iRow = T_filesDataGridView.RowCount - 1 Then
            ' at end of list
            Exit Sub
        ElseIf iChange = -1 And iRow = 0 Then
            ' at beginning of list
            Exit Sub
        End If

        T_filesDataGridView.Rows(iRow + iChange).Selected = True
    End Sub

    Private Sub PlayList_AddRecord()
        Dim iSongNo As Integer
        Dim szFileName As String

        '-- get file_no
        Dim bRowSel As Boolean = True

        If T_filesDataGridView.SelectedRows.Count = 0 Then
            bRowSel = False
        ElseIf T_filesDataGridView.SelectedRows(0).IsNewRow Then
            bRowSel = False
        End If

        If bRowSel = False Then
            MessageBox.Show("Please select a File from the lower table to add to this list",
                            "PowerPoint Link: Adding a file to the PlayList",
                            MessageBoxButtons.OK, MessageBoxIcon.Error)
            Exit Sub
        End If

        iSongNo = T_filesDataGridView.SelectedRows(0).Cells(0).Value
        szFileName = T_filesDataGridView.SelectedRows(0).Cells(1).Value
        If isAutoShort = "Y" Then AddToShortList(iSongNo)

        '-- Add the new row
        LBPlayList.Items.Add(iSongNo & "::" & szFileName)
    End Sub

    Private Sub AddToShortList(songNo As Integer)
        Dim iRow As Integer
        Dim filesView As DataView = ProHelpDataSet.Tables("t_files").DefaultView
        filesView.Sort = "file_no"

        iRow = filesView.Find(songNo)
        If iRow >= 0 Then
            filesView.Item(iRow).BeginEdit()
            filesView.Item(iRow)("selected") = "Y"
            filesView.Item(iRow).EndEdit()
            T_filesTableAdapter.Update(ProHelpDataSet.t_files)
            'ProHelpDataSet.t_files.AcceptChanges()
        End If
    End Sub

    Private Sub BtnSetup_Click(sender As Object, e As EventArgs) Handles BtnSetup.Click
        'Dim mySettings As New PPLInk.Settings,
        Dim newSettings As PPLInk.Settings
        Dim szConn = mySettings.ProHelpConnectionUser
        Dim Result = F_SetUp.ShowDialog()
        TxtSearch.Focus()

        newSettings = New PPLInk.Settings
        If szConn <> newSettings.ProHelpConnectionUser Then
            MessageBox.Show("The database connection has been changed.  PPLink needs to close.  Please reopen it to continue",
                            "PowerPoint Link: Setup", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Close()
        End If

        ' Has T_FILES been modified?
        If Result = DialogResult.Yes Then
            T_filesTableAdapter.Fill(Me.ProHelpDataSet.t_files, isShort)
        End If
    End Sub

    Private Sub F_Main_Shown(sender As Object, e As EventArgs) Handles MyBase.Shown
        TxtSearch.Focus()
    End Sub

    Private Sub BtnAdvanced_Click(sender As Object, e As EventArgs) Handles BtnAdvanced.Click
        F_Advanced.ShowDialog()
        T_filesTableAdapter.Fill(Me.ProHelpDataSet.t_files, isShort)
        isAutoShort = mySettings.ProHelpAutoShortList
    End Sub

    Private Sub LBPlayList_KeyPress(sender As Object, e As KeyPressEventArgs) Handles LBPlayList.KeyPress
        Dim iSelected As Integer = LBPlayList.SelectedIndex
        Dim iIndex As Integer = 0
        Dim szQueue As New Queue(Of String)

        Select Case e.KeyChar
            ' Delete key doesn't generate a KeyPress
            'Case Microsoft.VisualBasic.ChrW(Keys.Delete)

            Case "+"
                If iSelected < LBPlayList.Items.Count - 1 Then
                    szQueue.Enqueue(LBPlayList.Items(iSelected))
                    szQueue.Enqueue(LBPlayList.Items(iSelected + 1))
                    LBPlayList.Items(iSelected + 1) = szQueue.Dequeue
                    LBPlayList.Items(iSelected) = szQueue.Dequeue
                    LBPlayList.SelectedIndex += 1
                End If

            Case "-"
                If iSelected > 0 Then
                    szQueue.Enqueue(LBPlayList.Items(iSelected))
                    szQueue.Enqueue(LBPlayList.Items(iSelected - 1))
                    LBPlayList.Items(iSelected - 1) = szQueue.Dequeue
                    LBPlayList.Items(iSelected) = szQueue.Dequeue
                    LBPlayList.SelectedIndex -= 1
                End If

            Case Else
                Exit Sub
        End Select

        e.Handled = True
    End Sub

    Private Sub LBPlayList_KeyDown(sender As Object, e As KeyEventArgs) Handles LBPlayList.KeyDown
        Dim iSelected As Integer = LBPlayList.SelectedIndex
        Dim iIndex As Integer = 0
        Dim szQueue As New Queue(Of String)

        Select Case e.KeyCode
            ' Handle via KeyPress catches both num pad and normal plus key
            'Case Keys.Add

            ' Handle via KeyPress catches both num pad and normal minus key
            'Case Keys.Subtract

            Case Keys.Delete
                While iIndex < LBPlayList.Items.Count
                    If iIndex <> iSelected Then szQueue.Enqueue(LBPlayList.Items(iIndex))
                    iIndex = iIndex + 1
                End While
                LBPlayList.Items.Clear()
                iIndex = 0
                While szQueue.Count > 0
                    LBPlayList.Items.Add(szQueue.Dequeue)
                End While

            Case Else
                Exit Sub
        End Select

        e.SuppressKeyPress = True

    End Sub

    Private Sub BtnHelp_Click(sender As Object, e As EventArgs) Handles BtnHelp.Click
        F_Help.ShowDialog()
    End Sub

    Private Sub BtnLoadList_Click(sender As Object, e As EventArgs) Handles BtnLoadList.Click
        DlgLoadList.Fillqueue(LBPlayList)
        DlgLoadList.ShowDialog()
        ' returns No if a Save was done, Yes if a Load was done or Cancel
        If DlgLoadList.DialogResult = DialogResult.Yes Then
            DlgLoadList.GetList(LBPlayList)
        End If
    End Sub

    Private Sub ChkShortList_CheckedChanged(sender As Object, e As EventArgs) Handles ChkShortList.CheckedChanged
        If ChkShortList.Checked = True Then
            isShort = "Y"
        Else
            isShort = "N"
        End If
        T_filesTableAdapter.FillByPhrase(Me.ProHelpDataSet.t_files, TxtSearch.Text, isShort)
        mySettings.ProHelpSelected = isShort
        mySettings.Save()
    End Sub

    Private Sub TextBox1_KeyDown(sender As Object, e As KeyEventArgs) 'Handles TextBox1.KeyDown
        Dim messageBoxVB As New System.Text.StringBuilder()
        messageBoxVB.AppendFormat("{0} = {1}", "Alt", e.Alt)
        messageBoxVB.AppendLine()
        messageBoxVB.AppendFormat("{0} = {1}", "Control", e.Control)
        messageBoxVB.AppendLine()
        messageBoxVB.AppendFormat("{0} = {1}", "Handled", e.Handled)
        messageBoxVB.AppendLine()
        messageBoxVB.AppendFormat("{0} = {1}", "KeyCode", e.KeyCode)
        messageBoxVB.AppendLine()
        messageBoxVB.AppendFormat("{0} = {1}", "KeyValue", e.KeyValue)
        messageBoxVB.AppendLine()
        messageBoxVB.AppendFormat("{0} = {1}", "KeyData", e.KeyData)
        messageBoxVB.AppendLine()
        messageBoxVB.AppendFormat("{0} = {1}", "Modifiers", e.Modifiers)
        messageBoxVB.AppendLine()
        messageBoxVB.AppendFormat("{0} = {1}", "Shift", e.Shift)
        messageBoxVB.AppendLine()
        messageBoxVB.AppendFormat("{0} = {1}", "SuppressKeyPress", e.SuppressKeyPress)
        messageBoxVB.AppendLine()
        MessageBox.Show(messageBoxVB.ToString(), "KeyDown Event")

    End Sub

    Private Sub TextBox1_KeyPress(sender As Object, e As KeyPressEventArgs) 'Handles TextBox1.KeyPress
        Dim messageBoxVB As New System.Text.StringBuilder()
        messageBoxVB.AppendFormat("{0} = {1}", "Char", e.KeyChar)
        messageBoxVB.AppendLine()
        'messageBoxVB.AppendFormat("{0} = {1}", "Control", e.Control)
        'messageBoxVB.AppendLine()
        'messageBoxVB.AppendFormat("{0} = {1}", "Handled", e.Handled)
        'messageBoxVB.AppendLine()
        'messageBoxVB.AppendFormat("{0} = {1}", "KeyCode", e.KeyCode)
        'messageBoxVB.AppendLine()
        'messageBoxVB.AppendFormat("{0} = {1}", "KeyValue", e.KeyValue)
        'messageBoxVB.AppendLine()
        'messageBoxVB.AppendFormat("{0} = {1}", "KeyData", e.KeyData)
        'messageBoxVB.AppendLine()
        'messageBoxVB.AppendFormat("{0} = {1}", "Modifiers", e.Modifiers)
        'messageBoxVB.AppendLine()
        'messageBoxVB.AppendFormat("{0} = {1}", "Shift", e.Shift)
        'messageBoxVB.AppendLine()
        'messageBoxVB.AppendFormat("{0} = {1}", "SuppressKeyPress", e.SuppressKeyPress)
        'messageBoxVB.AppendLine()
        MessageBox.Show(messageBoxVB.ToString(), "KeyDown Event")

    End Sub

    Private Sub LBPlayList_DoubleClick(sender As Object, e As EventArgs) Handles LBPlayList.DoubleClick
        Dim szListItem As String

        If IsNothing(LBPlayList.SelectedItem) Then Exit Sub

        szListItem = LBPlayList.SelectedItems(0)
        'If isAutoShort = "Y" Then AddToShortList(CInt(szFile_no))
        LBInstant.Items.Clear()
        LBInstant.Items.Add(szListItem)
        MyBase.WindowState = FormWindowState.Minimized
        PlayList.Run(LBInstant.Items)
        MyBase.WindowState = FormWindowState.Normal
        TxtSearch.Text = ""
        TxtSearch.Focus()
    End Sub
End Class

'Version number
'Z.Y.X.W - Z.Y.X is major version.minor version.build; W is VS publish number
'1.5.0.48 Move selection after moving items in the Play List; Handle normal plus, minus keys as well as num pad; use double click in Play List
' to play that one file
'1.5.0.47 Include new User and Administrator Manuals via Help button
'1.4.0.43 Major release candidate: add feature to compare local list with a listing from the MASTER in cloud storage, implement short list
'1.4.0.42 Update File List work completed; Database modified so that no datetime fields are ever NULL
'1.4.0.37 expanded Advanced menu structure; added Edit Files and Update File List
'1.4.0.36 get version number from the deployment
'X not used below
'1.4.33 make instructions text read only; extend Import File List feature; Alternative Name field extended to 250 characters
'1.4.32 included list import and Help button to open PDF file
'1.3.28 fix bug in handling keys in search box (cleared text on arrow up or down)
'1.3.27 fix bug in table connection in DlgLoadList (tried to connect iwhen setting checkbox on load)
'1.3.26 add save and load for Play Lists
'1.2.25 experiment with PP focus
'1.2.24 add handling for up arrow, down arrow to search box and clear it after ENTER
'       add edit features to the Play List
'1.1.18 bug fix attempt in List Export when column is null - fixed in tables, not code
'1.1.17 used the actual VS publish number as the third segment 
'1.1.16 included manual setting of the version number on the main screen (used 1.1.1)
'1.1.15 included Export of file list
'1.0.14 was the last version without Advanced options 