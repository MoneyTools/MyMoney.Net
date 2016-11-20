Module Program

    Public Sub Main(ByVal args() As String)
        Dim server As OfxTestServer = New OfxTestServer()
        Dim url = "http://localhost:3000/ofx/test/"
        Dim arg As String
        Dim delay As Integer = 1000

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

        Console.WriteLine("OFX Test Server is running at " & url)
        Console.WriteLine("Press ENTER to exit...")
        Console.ReadLine()

    End Sub

End Module
