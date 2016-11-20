Imports System.Windows
Imports System.Windows.Controls

Class MainWindow

    Private Sub OnShowAdditionalCredentials(sender As Object, e As RoutedEventArgs)
        AdditionalCredentials.Visibility = Visibility.Visible
    End Sub

    Private Sub OnHideAdditionalCredentials(sender As Object, e As RoutedEventArgs)
        AdditionalCredentials.Visibility = Visibility.Collapsed
    End Sub

    Private Sub OnShowAuthTokenQuestions(sender As Object, e As RoutedEventArgs)
        AuthTokenQuestions.Visibility = Visibility.Visible
    End Sub
    Private Sub OnHideAuthTokenQuestions(sender As Object, e As RoutedEventArgs)
        AuthTokenQuestions.Visibility = Visibility.Collapsed
    End Sub

    Private Sub OnHideMFAChallengeQuestions(sender As Object, e As RoutedEventArgs)
        server.RemoveChallenges()
        MFAChallengeQuestions.Visibility = Visibility.Collapsed
    End Sub

    Private Sub OnShowMFAChallengeQuestions(sender As Object, e As RoutedEventArgs)
        server.AddStandardChallenges()
        MFAChallengeQuestions.Visibility = Visibility.Visible
    End Sub

    Private Sub OnShowChangePasswordQuestions(sender As Object, e As RoutedEventArgs)
        NewPasswordQuestions.Visibility = Visibility.Visible
        server.ChangePassword = True
    End Sub

    Private Sub OnHideChangePasswordQuestions(sender As Object, e As RoutedEventArgs)
        NewPasswordQuestions.Visibility = Visibility.Collapsed
    End Sub

    Dim server As OfxTestServer

    Private Sub OnFocusTextBox(box As TextBox)
        box.Focus()
        box.SelectAll()
    End Sub

    Private Sub OnLoaded(sender As Object, e As RoutedEventArgs)

        Dim url = "http://localhost:3000/ofx/test/"
        Dim arg As String
        Dim delay As Integer = 1000
        server = New OfxTestServer()

        server.UserName = "test"
        server.Password = "1234"

        Dispatcher.BeginInvoke(New Action(Of TextBox)(AddressOf OnFocusTextBox), UserName)

        Me.DataContext = server

        Me.MFAChallengeGrid.ItemsSource = server.MFAChallenges

        Dim args As String() = My.Application.CommandLineArguments

        For index = 0 To args.Length - 1

            arg = args(index)

            If arg.StartsWith("/") Or arg.StartsWith("-") Then

                Dim name As String = arg.Substring(1)
                If (name = "delay" And index < args.Length - 1) Then

                    Dim s = args(index + 1)
                    Integer.TryParse(s, delay)
                    index = index + 1

                End If
            Else
                url = arg
            End If

        Next

        server.Start(url, delay)

    End Sub

    Protected Overrides Sub OnClosing(e As ComponentModel.CancelEventArgs)
        MyBase.OnClosing(e)

        server.Terminate()
    End Sub

    Private Sub OnTextBoxGotFocus(sender As Object, e As RoutedEventArgs)
        Dim box As TextBox = sender
        OnFocusTextBox(box)
    End Sub
End Class
