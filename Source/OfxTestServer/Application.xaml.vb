Class Application

    ' Application-level events, such as Startup, Exit, and DispatcherUnhandledException
    ' can be handled in this file.

    Dim args As String()

    Public ReadOnly Property CommandLineArguments As String()
        Get
            Return args
        End Get
    End Property


    Protected Overrides Sub OnStartup(e As StartupEventArgs)
        MyBase.OnStartup(e)

        Me.args = e.Args

    End Sub

End Class
