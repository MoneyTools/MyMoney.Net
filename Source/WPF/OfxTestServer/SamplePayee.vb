
Public Class SamplePayee

    Private m_name As String
    Private m_min As Double
    Private m_max As Double

    Public Sub New(name As String, min As Double, max As Double)
        Me.m_name = name
        Me.m_min = min
        Me.m_max = max
    End Sub

    Property Name() As String
        Get
            Return m_name
        End Get

        Set(ByVal Value As String)
            m_name = Value
        End Set
    End Property

    Property Min() As Double
        Get
            Return m_min
        End Get

        Set(ByVal Value As Double)
            m_min = Value
        End Set
    End Property


    Property Max() As Double
        Get
            Return m_max
        End Get

        Set(ByVal Value As Double)
            m_max = Value
        End Set
    End Property



End Class
