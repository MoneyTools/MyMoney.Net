Imports System
Imports System.Linq
Imports System.Collections.Generic
Imports System.Text
Imports System.Threading
Imports System.Net
Imports System.Net.Sockets
Imports System.IO
Imports System.Xml.Linq
Imports System.Xml
Imports System.ComponentModel
Imports System.Collections.ObjectModel
Imports System.Windows.Threading

Public Class OfxTestServer
    Implements INotifyPropertyChanged

    Dim _terminated As Boolean
    Dim _prefix As String
    Dim _pending As IAsyncResult
    Dim _http As HttpListener
    Dim _payees As List(Of SamplePayee)
    Dim _delay As Integer
    Dim _userName As String
    Dim _password As String
    Dim _userCred1Label As String
    Dim _userCred1 As String
    Dim _userCred2Label As String
    Dim _userCred2 As String
    Dim _mfaChallenges As ObservableCollection(Of MFAChallenge) = New ObservableCollection(Of MFAChallenge)
    Dim _mfaPendingResponse As Boolean
    Dim _authTokenLabel As String
    Dim _authToken As String
    Dim _changePassword As Boolean
    Dim _dispatcher As Dispatcher
    Dim _accessKey As String


    Public Sub New()
        AddSamplePayees()
        _dispatcher = Application.Current.Dispatcher
    End Sub

    Public Sub AddStandardChallenges()

        If (_mfaChallenges.Count = 0) Then

            _mfaChallenges.Add(New MFAChallenge("MFA13", "Please enter the last four digits of your social security number", "1234"))
            _mfaChallenges.Add(New MFAChallenge("MFA107", Nothing, "QWIN 1700")) ' Built in question for "App id"
            _mfaChallenges.Add(New MFAChallenge("123", "With which branch is your account associated?", "Newcastle"))
            _mfaChallenges.Add(New MFAChallenge("MFA16", Nothing, "Aston")) ' Built in label for "Mother’s maiden name"

        End If
    End Sub

    Public Sub RemoveChallenges()
        _mfaChallenges.Clear()
    End Sub


    '// INotifyPropertyChanged event
    'public event PropertyChangedEventHandler PropertyChanged;

    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Private Sub OnPropertyChanged(name As String)
        _dispatcher.BeginInvoke(New Action(Of String)(AddressOf RaisePropertyChanged), name)
    End Sub

    Private Sub RaisePropertyChanged(name As String)
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(name))
    End Sub

    Public Property UserName As String
        Get
            Return _userName
        End Get

        Set(value As String)
            _userName = value
            OnPropertyChanged("UserName")
        End Set
    End Property

    Public Property Password As String
        Get
            Return _password
        End Get

        Set(value As String)
            _password = value
            OnPropertyChanged("Password")
        End Set
    End Property

    Public Property UserCred1Label As String
        Get
            Return _userCred1Label
        End Get

        Set(value As String)
            _userCred1Label = value
            OnPropertyChanged("UserCred1Label")
        End Set
    End Property

    Public Property UserCred1 As String
        Get
            Return _userCred1
        End Get

        Set(value As String)
            _userCred1 = value
            OnPropertyChanged("UserCred1")
        End Set
    End Property

    Public Property UserCred2Label As String
        Get
            Return _userCred2Label
        End Get

        Set(value As String)
            _userCred2Label = value
            OnPropertyChanged("UserCred2Label")
        End Set
    End Property

    Public Property UserCred2 As String
        Get
            Return _userCred2
        End Get

        Set(value As String)
            _userCred2 = value
            OnPropertyChanged("UserCred2")
        End Set
    End Property

    Public Property AuthTokenLabel As String
        Get
            Return _authTokenLabel
        End Get

        Set(value As String)
            _authTokenLabel = value
            OnPropertyChanged("AuthTokenLabel")
        End Set
    End Property

    Public Property AuthToken As String
        Get
            Return _authToken
        End Get

        Set(value As String)
            _authToken = value
            _accessKey = Nothing
            OnPropertyChanged("AuthToken")
        End Set
    End Property

    Public Property ChangePassword As String
        Get
            Return _changePassword
        End Get

        Set(value As String)
            _changePassword = value
            OnPropertyChanged("ChangePassword")
        End Set
    End Property


    Public ReadOnly Property MFAChallenges As ObservableCollection(Of MFAChallenge)
        Get
            Return _mfaChallenges
        End Get

    End Property


    Public Sub Start(prefix As String, delayInMilliseconds As Integer)
        _delay = delayInMilliseconds
        Me._prefix = prefix
        ThreadPool.QueueUserWorkItem(New WaitCallback(AddressOf RunServer))
    End Sub


    Public Sub Terminate()
        Me._terminated = True
        If (Not (_http Is Nothing)) Then
            _http.Stop()
            _http = Nothing
        End If
    End Sub

    Private Sub RunServer(state As Object)

        _http = New HttpListener()
        _http.Prefixes.Add(_prefix)
        _http.Start()

        Try

            While (Not Me._terminated)

                ' Note: The GetContext method blocks while waiting for a request. 
                Dim context As HttpListenerContext = _http.GetContext()
                Dim request As HttpListenerRequest = context.Request
                Dim response As HttpListenerResponse = context.Response

                Try

                    Dim input As XDocument = Nothing

                    Using s As Stream = request.InputStream
                        input = XDocument.Load(s)
                    End Using

                    Thread.Sleep(_delay)  'slow it down to make it more realistic otherwise it is too fast to see...

                    Dim output As XDocument = ProcessRequest(input)

                    Using s As Stream = response.OutputStream

                        Dim settings As XmlWriterSettings = New XmlWriterSettings()
                        settings.Indent = True
                        Using w As XmlWriter = XmlWriter.Create(s, settings)
                            output.WriteTo(w)
                        End Using
                        s.Close()
                    End Using

                Catch ex As Exception
                    response.StatusCode = HttpStatusCode.BadRequest
                    Using s As Stream = response.OutputStream
                        Using writer As StreamWriter = New StreamWriter(s, Encoding.UTF8)
                            writer.WriteLine(ex.ToString())
                        End Using
                        s.Close()
                    End Using
                End Try
            End While

        Catch ex As Exception
            Console.WriteLine("Exception " + ex.GetType().FullName + ": " + ex.Message)
        End Try

    End Sub

    Private Function ProcessRequest(input As XDocument) As XDocument
        Dim root As XElement = input.Root ' should be "OFX"
        Dim result As XDocument = New XDocument(New XElement("OFX"))

        Dim challenge As Boolean = True

        For Each e As XElement In root.Elements()
            Dim name As String = e.Name.LocalName
            If (name = "PROFMSGSRQV1") Then
                challenge = False
            End If
        Next

        For Each e As XElement In root.Elements()
            ProcessElement(e, challenge, result)
        Next
        Return result
    End Function


    Private Sub ProcessElement(e As XElement, challenge As Boolean, result As XDocument)

        Select Case (e.Name.LocalName)

            Case "SIGNONMSGSRQV1"
                ProcessSignonRequest(e, challenge, result)

            Case "SIGNUPMSGSRQV1"
                ProcessSignupRequest(e, result)

            Case "PROFMSGSRQV1"
                ProcessProfileRequest(e, result)

            Case "BANKMSGSRQV1"
                ProcessBankRequest(e, result)

        End Select

    End Sub

    Private Function GetElementValue(e As XElement, name As String) As String

        Dim child As XElement = e.Element(name)
        If (child Is Nothing) Then
            Return Nothing
        End If

        Return child.Value

    End Function



    '<SIGNONMSGSRSV1>
    '    <SONRS>
    '        <STATUS>
    '            <CODE>0</CODE>
    '            <SEVERITY>INFO</SEVERITY>
    '        </STATUS>
    '        <DTSERVER>20120207014332.760[0:GMT]</DTSERVER>
    '        <LANGUAGE>ENG</LANGUAGE>
    '        <FI>
    '            <ORG>becu</ORG>
    '            <FID>1001</FID>
    '        </FI>
    '    </SONRS>
    '</SIGNONMSGSRSV1>
    Private Sub ProcessSignonRequest(e As XElement, challenge As Boolean, result As XDocument)

        Dim response As XElement
        Dim sonrq = e.Element("SONRQ")
        Dim code As Integer = 0
        Dim message As String = Nothing
        Dim severity As String = "INFO"

        If (sonrq Is Nothing) Then
            Throw New Exception("Missing SONRQ")
        End If

        If (challenge) Then

            Dim userId As String = GetElementValue(sonrq, "USERID")
            Dim pswd As String = GetElementValue(sonrq, "USERPASS")

            If (_userName <> userId) Then
                message = "Invalid user id"
                code = 15500
            ElseIf (_password <> pswd) Then
                message = "Invalid password"
                code = OfxErrors.SignonInvalid
            End If

            If (code = 0 And Not String.IsNullOrEmpty(_userCred1Label)) Then

                Dim cred1 As String = GetElementValue(sonrq, "USERCRED1")
                If (_userCred1 <> cred1) Then
                    message = "Invalid USERCRED1"
                    code = OfxErrors.SignonInvalid
                End If

            End If

            If (code = 0 And Not String.IsNullOrEmpty(_userCred2Label)) Then

                Dim cred2 As String = GetElementValue(sonrq, "USERCRED2")
                If (_userCred2 <> cred2) Then
                    message = "Invalid USERCRED2"
                    code = OfxErrors.SignonInvalid
                End If

            End If

            If (_mfaChallenges.Count = 0) Then
                challenge = False
            End If
        End If

        Dim pinchrs As XElement = Nothing

        If (code = 0 And _changePassword) Then

            'PINCHTRNRQ
            Dim pinchrq As XElement = e.Element("PINCHTRNRQ")

            If (Not pinchrq Is Nothing) Then
                pinchrs = ProcessChangePassword(pinchrq)
                challenge = False
            Else
                message = "Please change your password"
                code = OfxErrors.MustChangeUSERPASS
            End If

        End If

        Dim accesskey As XElement = Nothing
        Dim hasAccessKey As Boolean = False

        If (code = 0 And Not _accessKey Is Nothing) Then
            Dim value As String = GetElementValue(sonrq, "ACCESSKEY")            

            If (_accessKey = value) Then
                hasAccessKey = True
            End If
        End If


        If (challenge And code = 0) Then

            Dim challengeTran As XElement = e.Element("MFACHALLENGETRNRQ")

            If (Not challengeTran Is Nothing) Then

                ' we are now expecting next request from user to contain the MFA challenge answers.
                _mfaPendingResponse = True

                response = <SIGNONMSGSRSV1>
                               <SONRS>
                                   <STATUS>
                                       <CODE>0</CODE>
                                       <SEVERITY>INFO</SEVERITY>
                                   </STATUS>
                                   <DTSERVER><%= GetIsoDateTime(DateTime.Now) %></DTSERVER>
                                   <LANGUAGE>ENG</LANGUAGE>
                                   <FI>
                                       <ORG>bankofhope</ORG>
                                       <FID>7777</FID>
                                   </FI>
                               </SONRS>
                               <MFACHALLENGETRNRS>
                                   <!--MFA Challenge Transaction aggregate-->
                                   <TRNUID>66D3749F-5B3B-4DC3-87A3-8F795EA59EDB</TRNUID>
                                   <STATUS>
                                       <CODE>0</CODE>
                                       <SEVERITY>INFO</SEVERITY>
                                       <MESSAGE>SUCCESS</MESSAGE>
                                   </STATUS>
                                   <%= GetMFAChallenges() %>
                               </MFACHALLENGETRNRS>
                           </SIGNONMSGSRSV1>

                result.Root.Add(response)
                Return
            End If

            If (_mfaPendingResponse) Then
                _mfaPendingResponse = False
                If (Not VerifyMFAAnswers(sonrq)) Then
                    accesskey = Nothing
                    code = 3001
                Else
                    _accessKey = Guid.NewGuid().ToString()
                    accesskey = New XElement("ACCESSKEY", _accessKey)
                End If
            ElseIf (code = 0 And _mfaChallenges.Count > 0) Then
                ' Initiate MFA Challenge
                code = 3000
            End If

        End If

        If (code = 0 And Not String.IsNullOrEmpty(_authTokenLabel) And Not hasAccessKey) Then

            Dim token As String = GetElementValue(sonrq, "AUTHTOKEN")
            If (token Is Nothing) Then
                message = "AUTHTOKEN Required"
                code = OfxErrors.AUTHTOKENRequired
            ElseIf (_authToken <> token) Then
                message = "Invalid AUTHTOKEN"
                code = OfxErrors.AUTHTOKENInvalid
            Else
                _accessKey = Guid.NewGuid().ToString()
                accesskey = New XElement("ACCESSKEY", _accessKey)
            End If

        End If


        response = <SIGNONMSGSRSV1>
                       <SONRS>
                           <STATUS>
                               <CODE><%= code %></CODE>
                               <SEVERITY><%= severity %></SEVERITY>
                               <MESSAGE><%= message %></MESSAGE>
                           </STATUS>
                           <DTSERVER><%= GetIsoDateTime(DateTime.Now) %></DTSERVER>
                           <LANGUAGE>ENG</LANGUAGE>
                           <FI>
                               <ORG>bankofhope</ORG>
                               <FID>7777</FID>
                           </FI>
                       </SONRS>
                   </SIGNONMSGSRSV1>


        If (Not pinchrs Is Nothing) Then
            response.Add(pinchrs)
        ElseIf (Not accesskey Is Nothing) Then
            response.Element("SONRS").Add(accesskey)
        End If

        result.Root.Add(response)
    End Sub

    Private Function VerifyMFAAnswers(sonrq As XElement) As Boolean

        Dim notVerified As HashSet(Of MFAChallenge) = New HashSet(Of MFAChallenge)(_mfaChallenges)

        For Each challengeResponse As XElement In sonrq.Elements("MFACHALLENGEANSWER")

            Dim id As XElement = challengeResponse.Element("MFAPRHASEID")
            Dim answer As String = challengeResponse.Element("MFAPHRASEA").Value


            For Each item In _mfaChallenges
                If (item.PhraseId = id And item.PhraseAnswer = answer) Then
                    notVerified.Remove(item)
                End If
            Next
        Next

        Return notVerified.Count = 0

    End Function

    Private Function GetMFAChallenges() As XElement

        Dim wrapper As XElement = New XElement("MFACHALLENGERS")

        For Each item In _mfaChallenges

            Dim x As XElement = New XElement("MFACHALLENGE", New XElement("MFAPHRASEID", item.PhraseId))

            If (Not String.IsNullOrWhiteSpace(item.PhraseLabel)) Then
                x.Add(New XElement("MFAPHRASELABEL", item.PhraseLabel))
            End If

            wrapper.Add(x)
        Next

        Return wrapper

        ' For example:
        '<MFACHALLENGERS>
        '    <!--MFA Challenge aggregate-->
        '    <MFACHALLENGE>
        '        <MFAPHRASEID>MFA13</MFAPHRASEID>
        '        <MFAPHRASELABEL>Please enter the last four digits of your social security number.</MFAPHRASELABEL>
        '    </MFACHALLENGE>
        '    <MFACHALLENGE>
        '        <!--built in question w/o label and no user prompt -->
        '        <MFAPHRASEID>MFA107</MFAPHRASEID>
        '    </MFACHALLENGE>
        '    <MFACHALLENGE>
        '        <!--MFA Challenge aggregate-->
        '        <MFAPHRASEID>123</MFAPHRASEID>
        '        <MFAPHRASELABEL>With which branch is your account associated?</MFAPHRASELABEL>
        '    </MFACHALLENGE>
        '    <MFACHALLENGE>
        '        <!--should have built in label-->
        '        <MFAPHRASEID>MFA16</MFAPHRASEID>
        '    </MFACHALLENGE>
        '</MFACHALLENGERS>

    End Function

    '<SIGNUPMSGSRQV1>
    '   <ACCTINFOTRNRQ>
    '     <TRNUID>3f84d542-a754-4e2f-844b-fc8cc73c7060</TRNUID>
    '     <CLTCOOKIE>1</CLTCOOKIE>
    '     <ACCTINFORQ>
    '       <DTACCTUP>19700101000000</DTACCTUP>
    '     </ACCTINFORQ>
    '   </ACCTINFOTRNRQ>
    ' </SIGNUPMSGSRQV1>
    Private Sub ProcessSignupRequest(e As XElement, result As XDocument)

        Dim r As XElement = <SIGNUPMSGSRSV1>
                                <ACCTINFOTRNRS>
                                    <TRNUID><%= GetTransactionId(e) %></TRNUID>
                                    <STATUS>
                                        <CODE>0</CODE>
                                        <SEVERITY>INFO</SEVERITY>
                                    </STATUS>
                                    <CLTCOOKIE>1</CLTCOOKIE>
                                    <ACCTINFORS>
                                        <DTACCTUP><%= GetIsoDateTime(DateTime.Now) %></DTACCTUP>
                                        <ACCTINFO>
                                            <DESC>Checking</DESC>
                                            <BANKACCTINFO>
                                                <BANKACCTFROM>
                                                    <BANKID>123456</BANKID>
                                                    <ACCTID>456789</ACCTID>
                                                    <ACCTTYPE>CHECKING</ACCTTYPE>
                                                </BANKACCTFROM>
                                                <SUPTXDL>Y</SUPTXDL>
                                                <XFERSRC>Y</XFERSRC>
                                                <XFERDEST>Y</XFERDEST>
                                                <SVCSTATUS>ACTIVE</SVCSTATUS>
                                            </BANKACCTINFO>
                                        </ACCTINFO>
                                    </ACCTINFORS>
                                </ACCTINFOTRNRS>
                            </SIGNUPMSGSRSV1>

        result.Root.Add(r)
    End Sub

    Private Function GetMsgSetCore() As XElement
        Dim r As XElement = <MSGSETCORE>
                                <VER>1</VER>
                                <URL><%= _prefix %></URL>
                                <OFXSEC>NONE</OFXSEC>
                                <TRANSPSEC>Y</TRANSPSEC>
                                <SIGNONREALM>DefaultRealm</SIGNONREALM>
                                <LANGUAGE>ENG</LANGUAGE>
                                <SYNCMODE>FULL</SYNCMODE>
                                <RESPFILEER>Y</RESPFILEER>
                                <SPNAME>Corillian Corp</SPNAME>
                            </MSGSETCORE>
        Return r
    End Function

    '<PROFMSGSRQV1>
    '  <PROFTRNRQ>
    '    <TRNUID>6ab04f71-6bcb-44d2-a021-16931e1f251d</TRNUID>
    '    <PROFRQ>
    '      <CLIENTROUTING>MSGSET</CLIENTROUTING>
    '      <DTPROFUP>20120207015824</DTPROFUP>
    '    </PROFRQ>
    '  </PROFTRNRQ>
    '</PROFMSGSRQV1>

    Private Sub ProcessProfileRequest(e As XElement, result As XDocument)

        Dim r As XElement = <PROFMSGSRSV1>
                                <PROFTRNRS>
                                    <TRNUID><%= GetTransactionId(e) %></TRNUID>
                                    <STATUS>
                                        <CODE>0</CODE>
                                        <SEVERITY>INFO</SEVERITY>
                                    </STATUS>
                                    <PROFRS>
                                        <MSGSETLIST>
                                            <SIGNONMSGSET>
                                                <SIGNONMSGSETV1>
                                                    <%= GetMsgSetCore() %>
                                                </SIGNONMSGSETV1>
                                            </SIGNONMSGSET>
                                            <SIGNUPMSGSET>
                                                <SIGNUPMSGSETV1>
                                                    <%= GetMsgSetCore() %>
                                                    <WEBENROLL>
                                                        <URL>http://localhost</URL>
                                                    </WEBENROLL>
                                                    <CHGUSERINFO>N</CHGUSERINFO>
                                                    <AVAILACCTS>Y</AVAILACCTS>
                                                    <CLIENTACTREQ>N</CLIENTACTREQ>
                                                </SIGNUPMSGSETV1>
                                            </SIGNUPMSGSET>
                                            <BANKMSGSET>
                                                <BANKMSGSETV1>
                                                    <%= GetMsgSetCore() %>
                                                    <CLOSINGAVAIL>N</CLOSINGAVAIL>
                                                    <XFERPROF>
                                                        <PROCENDTM>235959[0:GMT]</PROCENDTM>
                                                        <CANSCHED>Y</CANSCHED>
                                                        <CANRECUR>N</CANRECUR>
                                                        <CANMODXFERS>N</CANMODXFERS>
                                                        <CANMODMDLS>N</CANMODMDLS>
                                                        <MODELWND>0</MODELWND>
                                                        <DAYSWITH>0</DAYSWITH>
                                                        <DFLTDAYSTOPAY>0</DFLTDAYSTOPAY>
                                                    </XFERPROF>
                                                    <EMAILPROF>
                                                        <CANEMAIL>N</CANEMAIL>
                                                        <CANNOTIFY>Y</CANNOTIFY>
                                                    </EMAILPROF>
                                                </BANKMSGSETV1>
                                            </BANKMSGSET>
                                            <PROFMSGSET>
                                                <PROFMSGSETV1>
                                                    <%= GetMsgSetCore() %>
                                                </PROFMSGSETV1>
                                            </PROFMSGSET>
                                        </MSGSETLIST>
                                        <SIGNONINFOLIST>
                                            <%= GetSignOnInfo() %>
                                        </SIGNONINFOLIST>
                                        <DTPROFUP>20111031070000.000[0:GMT]</DTPROFUP>
                                        <FINAME>Last Chance Bank of Hope</FINAME>
                                        <ADDR1>123 Walkabout Drive</ADDR1>
                                        <CITY>Wooloomaloo</CITY>
                                        <STATE>WA</STATE>
                                        <POSTALCODE>12345</POSTALCODE>
                                        <COUNTRY>USA</COUNTRY>
                                        <CSPHONE>123-456-7890</CSPHONE>
                                        <URL>http://localhost</URL>
                                        <EMAIL>feedback@localhost.org</EMAIL>
                                    </PROFRS>
                                </PROFTRNRS>
                            </PROFMSGSRSV1>

        result.Root.Add(r)
    End Sub

    Private Function GetSignOnInfo() As XElement
        Return <SIGNONINFO>
                   <SIGNONREALM>DefaultRealm</SIGNONREALM>
                   <MIN>6</MIN>
                   <MAX>32</MAX>
                   <CHARTYPE>ALPHAORNUMERIC</CHARTYPE>
                   <CASESEN>N</CASESEN>
                   <SPECIAL>N</SPECIAL>
                   <SPACES>N</SPACES>
                   <PINCH>Y</PINCH>
                   <CHGPINFIRST>N</CHGPINFIRST>
                   <USERCRED1LABEL><%= _userCred1Label %></USERCRED1LABEL>
                   <USERCRED2LABEL><%= _userCred2Label %></USERCRED2LABEL>
                   <CLIENTUIDREQ>Y</CLIENTUIDREQ>
                   <AUTHTOKENFIRST>N</AUTHTOKENFIRST>
                   <AUTHTOKENLABEL>Authentication Token</AUTHTOKENLABEL>
                   <AUTHTOKENINFOURL>http://www.bing.com</AUTHTOKENINFOURL>
                   <MFACHALLENGESUPT>Y</MFACHALLENGESUPT>
                   <MFACHALLENGEFIRST>N</MFACHALLENGEFIRST>
               </SIGNONINFO>
    End Function

    Private Sub ProcessBankRequest(e As XElement, result As XDocument)

        Dim r As XElement = New XElement("BANKMSGSRSV1")

        For Each statementRequest As XElement In e.Elements("STMTTRNRQ")
            ProcessBankStatementRequest(statementRequest, r)
        Next

        result.Root.Add(r)
    End Sub


    Private Function GetTransactionId(e As XElement) As String

        Dim t As XElement = e.Descendants("TRNUID").FirstOrDefault()
        If (Not (t Is Nothing)) Then
            Return t.Value
        End If
        Return Guid.NewGuid().ToString()
    End Function

    Private Function GetBankAccountFrom(e As XElement) As XElement
        Dim r As XElement = e.Descendants("BANKACCTFROM").FirstOrDefault()
        If (r Is Nothing) Then
            Throw New Exception("Missing BANKACCTFROM")
        End If
        Return r
    End Function


    '<STMTTRNRQ>
    '  <TRNUID>042919d9-ccb5-4915-bc10-0f248a60fd2f</TRNUID>
    '  <CLTCOOKIE>1</CLTCOOKIE>
    '  <STMTRQ>
    '    <BANKACCTFROM>
    '      <BANKID>123456</BANKID>
    '      <ACCTID>456789</ACCTID>
    '      <ACCTTYPE>CHECKING</ACCTTYPE>
    '    </BANKACCTFROM>
    '    <INCTRAN>
    '      <DTSTART>20120310</DTSTART>
    '      <INCLUDE>Y</INCLUDE>
    '    </INCTRAN>
    '  </STMTRQ>
    '</STMTTRNRQ>
    Private Sub ProcessBankStatementRequest(e As XElement, result As XElement)

        Try
            Dim acct As XElement = GetBankAccountFrom(e)

            Dim endDate = DateTime.Now
            Dim startDate = endDate.AddMonths(-1)

            Dim stmt As XElement = <STMTTRNRS>
                                       <TRNUID><%= GetTransactionId(e) %></TRNUID>
                                       <STATUS>
                                           <CODE>0</CODE>
                                           <SEVERITY>INFO</SEVERITY>
                                       </STATUS>
                                       <CLTCOOKIE>1</CLTCOOKIE>
                                       <STMTRS>
                                           <CURDEF>USD</CURDEF>
                                           <%= acct %>
                                           <BANKTRANLIST>
                                               <DTSTART><%= GetIsoDateTime(startDate) %></DTSTART>
                                               <DTEND><%= GetIsoDateTime(endDate) %></DTEND>
                                               <%= GetRandomTransactions(startDate, endDate) %>
                                           </BANKTRANLIST>
                                           <LEDGERBAL>
                                               <BALAMT>8722.69</BALAMT>
                                               <DTASOF><%= GetIsoDateTime(DateTime.Now) %></DTASOF>
                                           </LEDGERBAL>
                                           <AVAILBAL>
                                               <BALAMT>8717.69</BALAMT>
                                               <DTASOF><%= GetIsoDateTime(DateTime.Now) %></DTASOF>
                                           </AVAILBAL>
                                       </STMTRS>
                                   </STMTTRNRS>
            result.Add(stmt)

        Catch ex As Exception
            Dim stmt As XElement = <STMTTRNRS>
                                       <TRNUID><%= GetTransactionId(e) %></TRNUID>
                                       <STATUS>
                                           <CODE>2000</CODE>
                                           <SEVERITY>ERROR</SEVERITY>
                                           <MESSAGE><%= ex.Message %></MESSAGE>
                                       </STATUS>
                                   </STMTTRNRS>
            result.Add(stmt)
        End Try


    End Sub

    Private Function ProcessChangePassword(e As XElement) As XElement

        Dim user As String = ""
        Dim code As Integer = 0
        Dim severity As String = "INFO"
        Dim message As String = Nothing

        Dim req As XElement = e.Element("PINCHRQ")
        If (Not (req Is Nothing)) Then
            Dim ue As XElement = req.Element("USERID")
            If (Not (ue Is Nothing)) Then
                user = ue.Value
                If (user <> _userName) Then
                    message = "User id unknown"
                End If
            Else
                message = "Missing USERID"
            End If

            Dim newpass As XElement = req.Element("NEWUSERPASS")
            If (Not (newpass Is Nothing)) Then
                Password = newpass.Value
                ChangePassword = False
            Else
                message = "Cannot have empty password"
            End If
        Else
            message = "Missing PINCHRQ"
        End If

        If (Not message Is Nothing) Then
            severity = "ERROR"
            code = OfxErrors.CouldNotChangeUSERPASS
        End If

        Dim r As XElement = <PINCHTRNRS>
                                <TRNUID><%= GetTransactionId(e) %></TRNUID>
                                <STATUS>
                                    <CODE><%= code %></CODE>
                                    <SEVERITY><%= severity %></SEVERITY>
                                    <MESSAGE><%= message %></MESSAGE>
                                </STATUS>
                                <PINCHRS>
                                    <USERID><%= user %></USERID>
                                    <DTCHANGED><%= GetIsoDateTime(DateTime.Now) %></DTCHANGED>
                                </PINCHRS>
                            </PINCHTRNRS>

        ProcessChangePassword = r

    End Function

    Private Function GetRandomTransactions(startDate As DateTime, endDate As DateTime) As IEnumerable

        Dim span = endDate - startDate
        Dim incr As TimeSpan = New TimeSpan(span.Ticks / 10)
        Dim rand As Random = New Random()
        Dim result As List(Of XElement) = New List(Of XElement)

        For index = 1 To 10

            Dim payee As SamplePayee = GetRandomPayee(rand)
            Dim range As Double = payee.Max - payee.Min
            Dim amount As Double = rand.NextDouble() * range
            amount = amount + payee.Min
            amount = Math.Round(amount, 2)

            Dim type = "DEBIT"
            If (amount > 0) Then
                type = "CREDIT"
            End If

            Dim e As XElement = <STMTTRN>
                                    <TRNTYPE><%= type %></TRNTYPE>
                                    <DTPOSTED><%= GetIsoDateTime(startDate) %></DTPOSTED>
                                    <TRNAMT><%= amount %></TRNAMT>
                                    <FITID><%= index %></FITID>
                                    <NAME><%= payee.Name %></NAME>
                                </STMTTRN>
            result.Add(e)

            startDate = startDate + incr
        Next

        Return result
    End Function

    Private Function GetRandomPayee(rand As Random) As SamplePayee

        Dim index = rand.Next(0, _payees.Count)
        Return _payees.Item(index)

    End Function


    Private Function GetIsoDateTime(dt As DateTime) As String
        Dim gmt As DateTime = dt.ToUniversalTime()
        Dim isodate As String = GetIsoDate(gmt) + gmt.Hour.ToString("D2") + gmt.Minute.ToString("D2") + gmt.Second.ToString("D2")
        Return isodate
    End Function


    Private Function GetIsoDate(dt As DateTime) As String
        Return dt.Year.ToString() + dt.Month.ToString("D2") + dt.Day.ToString("D2")
    End Function

    Private Sub AddSamplePayees()
        _payees = New List(Of SamplePayee)()
        _payees.Add(New SamplePayee("Sprint PCS", -275, -27))
        _payees.Add(New SamplePayee("Costco Gas", -76, -12))
        _payees.Add(New SamplePayee("Garlic Jims Famous Gourmet Pizza", -56, -11))
        _payees.Add(New SamplePayee("Veterinary Hostpital", -222, -11))
        _payees.Add(New SamplePayee("Safeway", -199, 50))
        _payees.Add(New SamplePayee("World Market", -100, -9))
        _payees.Add(New SamplePayee("AAA", -242, -3))
        _payees.Add(New SamplePayee("Radio Shack", -199, 54))
        _payees.Add(New SamplePayee("Costco", -4621, 870))
        _payees.Add(New SamplePayee("Rite Aid", -50, -3))
        _payees.Add(New SamplePayee("Starbucks", -108, -1))
        _payees.Add(New SamplePayee("GTC Telecom", -98, -2))
        _payees.Add(New SamplePayee("ARCO", -73, -3))
        _payees.Add(New SamplePayee("Home Depot", -912, 224))
        _payees.Add(New SamplePayee("McLendon Hardware", -202, 25))
        _payees.Add(New SamplePayee("World Vision", -147, -5))
        _payees.Add(New SamplePayee("State Farm Insurance", -3100, 1191))
        _payees.Add(New SamplePayee("Bank of America", -2063, -2063))
        _payees.Add(New SamplePayee("Comcast", -74, -49))
        _payees.Add(New SamplePayee("Puget Sound Energy", -428, 50))
        _payees.Add(New SamplePayee("Albertsons", -62, -1))
        _payees.Add(New SamplePayee("Top Foods", -203, 0))
        _payees.Add(New SamplePayee("Target", -218, 76))
        _payees.Add(New SamplePayee("Ruby's Diner", -99, -3))
        _payees.Add(New SamplePayee("DeYoung's Farm & Garden", -181, 84))
        _payees.Add(New SamplePayee("Water District", -510, -42))
        _payees.Add(New SamplePayee("Whole Foods", -167, -9))
        _payees.Add(New SamplePayee("Applebee's", -123, -14))
        _payees.Add(New SamplePayee("PCC", -119, -3))
        _payees.Add(New SamplePayee("Dairy Queen", -20, -1))
        _payees.Add(New SamplePayee("In Harmony", -438, -9))
        _payees.Add(New SamplePayee("Jerusalem Post", -49, -3))
        _payees.Add(New SamplePayee("Volvo Dealer", -2000, -23))
        _payees.Add(New SamplePayee("Big Foot Bagels", -30, -5))
        _payees.Add(New SamplePayee("Amazon.com", -293, 71))
        _payees.Add(New SamplePayee("REI", -855, 290))
        _payees.Add(New SamplePayee("PetSmart", -144, 10))
        _payees.Add(New SamplePayee("Jamba Juice", -39, -4))
        _payees.Add(New SamplePayee("Waste Management", -150, -92))
        _payees.Add(New SamplePayee("Fred Meyer", -121, 42))
        _payees.Add(New SamplePayee("Molbaks Inc.", -146, -7))
        _payees.Add(New SamplePayee("Walmart", -183, 41))
        _payees.Add(New SamplePayee("Verizon", -83, -33))
        _payees.Add(New SamplePayee("Barnes & Noble", -113, -2))
        _payees.Add(New SamplePayee("McDonalds", -57, -1))
        _payees.Add(New SamplePayee("Quizno's", -51, 0))
        _payees.Add(New SamplePayee("Pony Mailbox", -78, -4))
        _payees.Add(New SamplePayee("Blockbuster Video", -115, 36))
        _payees.Add(New SamplePayee("Office Max", -128, -2))
        _payees.Add(New SamplePayee("Teddy's Bigger Burgers", -48, -4))
        _payees.Add(New SamplePayee("Pallino Pastaria", -57, -15))
        _payees.Add(New SamplePayee("Famous Footwear", -322, 142))
        _payees.Add(New SamplePayee("Department of Licensing", -61, -20))
        _payees.Add(New SamplePayee("Play It Again Sports", -76, 81))
        _payees.Add(New SamplePayee("NewEgg.com", -1745, 161))
        _payees.Add(New SamplePayee("Marriot Atlanta m:Store", -19, -3))
        _payees.Add(New SamplePayee("Marta Atlanta", -18, -4))
        _payees.Add(New SamplePayee("Applebees", -73, -15))
        _payees.Add(New SamplePayee("Seattle Times", -98, -27))
        _payees.Add(New SamplePayee("Foot Zone", -255, 109))
        _payees.Add(New SamplePayee("Staples", -161, 326))
        _payees.Add(New SamplePayee("The Whole Pet Shop", -78, -8))
        _payees.Add(New SamplePayee("Chinese Restaurant", -99, -46))
        _payees.Add(New SamplePayee("Animal Healing Center", -328, 140))
        _payees.Add(New SamplePayee("Travelsmith Catalogue", -419, 190))
        _payees.Add(New SamplePayee("Baskin Robbins", -48, -5))
        _payees.Add(New SamplePayee("Subway", -46, -5))
        _payees.Add(New SamplePayee("The Lego Store", -405, -13))
        _payees.Add(New SamplePayee("Red Robin", -85, -10))
        _payees.Add(New SamplePayee("Dentist", -200, -12))
        _payees.Add(New SamplePayee("Black Angus", -227, -28))
        _payees.Add(New SamplePayee("Lands End", -437, 218))
        _payees.Add(New SamplePayee("Gymnastics", -533, -30))
        _payees.Add(New SamplePayee("KFC", -45, -5))
        _payees.Add(New SamplePayee("Fry's Electronics", -4025, 163))
        _payees.Add(New SamplePayee("Milk Delivery", -57, -8))
        _payees.Add(New SamplePayee("Soccer West", -140, -13))
        _payees.Add(New SamplePayee("Borders Books", -114, 15))
        _payees.Add(New SamplePayee("Stevens Pass Ski Resort", -289, -18))
        _payees.Add(New SamplePayee("Ben Franklin", -165, -5))
        _payees.Add(New SamplePayee("Audible Books", -41, 68))
        _payees.Add(New SamplePayee("Great Harvest Bakery", -29, 0))
        _payees.Add(New SamplePayee("Osh Kosh B'gosh", -212, -17))
        _payees.Add(New SamplePayee("QFC", -36, -1))
        _payees.Add(New SamplePayee("Trader Joe's", -93, 0))
        _payees.Add(New SamplePayee("Round Table Pizza", -84, -11))
        _payees.Add(New SamplePayee("Southwest Airlines", -394, 243))
        _payees.Add(New SamplePayee("Consumer Reports", -26, 24))
        _payees.Add(New SamplePayee("Cost Plus World Market", -162, -1))
        _payees.Add(New SamplePayee("IKEA", -585, 79))
        _payees.Add(New SamplePayee("Lego Shop At Home", -391, -3))
        _payees.Add(New SamplePayee("Pacific Science Center", -131, -10))
        _payees.Add(New SamplePayee("Countrywide", -2063, -2063))
        _payees.Add(New SamplePayee("Alaska Airlines", -299, -5))
        _payees.Add(New SamplePayee("Chinese Restaurant", -129, -25))
        _payees.Add(New SamplePayee("Stevens Pass Cascadian", -27, -4))
        _payees.Add(New SamplePayee("Toys R Us", -97, 4))
        _payees.Add(New SamplePayee("Linens N Things", -457, -5))
        _payees.Add(New SamplePayee("Ski Rentals", -609, 32))
        _payees.Add(New SamplePayee("Tapatio Mexican Grill", -83, -7))
        _payees.Add(New SamplePayee("Sir Plus", -110, -3))
        _payees.Add(New SamplePayee("Bartell Drugs", -76, -1))
        _payees.Add(New SamplePayee("Stevens Pass", -540, 34))
        _payees.Add(New SamplePayee("SeaTac Airport", -8, 0))
        _payees.Add(New SamplePayee("Lego Store", -199, -10))
        _payees.Add(New SamplePayee("Intuit", -76, 76))
        _payees.Add(New SamplePayee("Loews", -49, -10))
        _payees.Add(New SamplePayee("Residential House Cleaning", -135, -125))
        _payees.Add(New SamplePayee("Tina Eddy", -50, -25))
        _payees.Add(New SamplePayee("A Canine Experience", -755, -25))
        _payees.Add(New SamplePayee("Veterinary Hospital", -563, -21))
        _payees.Add(New SamplePayee("Denny's", -20, -1))
        _payees.Add(New SamplePayee("Museum Of Flight", -48, -3))
        _payees.Add(New SamplePayee("USPS", -36, -1))
        _payees.Add(New SamplePayee("Legoland", -290, -3))
        _payees.Add(New SamplePayee("Sears", -1810, 63))
        _payees.Add(New SamplePayee("Hobby Town USA", -80, -5))
    End Sub


    Public Class MFAChallenge
        Implements INotifyPropertyChanged

        Dim _mfaPhraseId As String
        Dim _mfaPhraseLabel As String
        Dim _mfaPhraseAnswer As String

        Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

        Private Sub OnPropertyChanged(name As String)
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(name))
        End Sub

        Public Sub New(id As String, label As String, answer As String)
            _mfaPhraseId = id
            _mfaPhraseLabel = label
            _mfaPhraseAnswer = answer
        End Sub

        Public Property PhraseId As String
            Get
                Return _mfaPhraseId
            End Get

            Set(value As String)
                _mfaPhraseId = value
                OnPropertyChanged("PhraseId")
            End Set
        End Property

        Public Property PhraseLabel As String
            Get
                Return _mfaPhraseLabel
            End Get

            Set(value As String)
                _mfaPhraseLabel = value
                OnPropertyChanged("PhraseLabel")
            End Set
        End Property

        Public Property PhraseAnswer As String
            Get
                Return _mfaPhraseAnswer
            End Get

            Set(value As String)
                _mfaPhraseAnswer = value
                OnPropertyChanged("PhraseAnswer")
            End Set
        End Property
    End Class
End Class
