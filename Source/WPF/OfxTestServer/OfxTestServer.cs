using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using System.Xml;
using System.Xml.Linq;

namespace OfxTestServer
{
    internal class OfxServer : INotifyPropertyChanged
    {
        private bool _terminated;
        private string _prefix;
        private HttpListener _http;
        private List<SamplePayee> _payees;
        private int _delay;
        private string _userName;
        private string _password;
        private string _userCred1Label;
        private string _userCred1;
        private string _userCred2Label;
        private string _userCred2;
        private ObservableCollection<MFAChallenge> _mfaChallenges = new ObservableCollection<MFAChallenge>();
        private bool _mfaPendingResponse;
        private string _authTokenLabel;
        private string _authToken;
        private bool _changePassword;
        private Dispatcher _dispatcher;
        private string _accessKey;

        public OfxServer()
        {
            AddSamplePayees();
            _dispatcher = Application.Current.Dispatcher;
        }

        public ObservableCollection<MFAChallenge> MFAChallenges { get => _mfaChallenges; }

        public bool ChangePassword
        {
            get => _changePassword;
            set
            {
                if (_changePassword != value)
                {
                    _changePassword = value;
                    OnPropertyChanged("ChangePassword");
                }
            }
        }

        public string UserName
        {
            get => _userName;
            set
            {
                if (_userName != value)
                {
                    _userName = value;
                    OnPropertyChanged("UserName");
                }
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                if (_password != value)
                {
                    _password = value;
                    OnPropertyChanged("Password");
                }
            }
        }

        public string UserCred1Label
        {
            get => _userCred1Label;
            set
            {
                if (_userCred1Label != value)
                {
                    _userCred1Label = value;
                    OnPropertyChanged("UserCred1Label");
                }
            }
        }

        public string UserCred1
        {
            get => _userCred1;
            set
            {
                if (_userCred1 != value)
                {
                    _userCred1 = value;
                    OnPropertyChanged("UserCred1");
                }
            }
        }

        public string UserCred2Label
        {
            get => _userCred2Label;
            set
            {
                if (_userCred2Label != value)
                {
                    _userCred2Label = value;
                    OnPropertyChanged("UserCred2Label");
                }
            }
        }

        public string UserCred2
        {
            get => _userCred2;
            set
            {
                if (_userCred2 != value)
                {
                    _userCred2 = value;
                    OnPropertyChanged("UserCred2");
                }
            }
        }


        public string AuthTokenLabel
        {
            get => _authTokenLabel;
            set
            {
                if (_authTokenLabel != value)
                {
                    _authTokenLabel = value;
                    OnPropertyChanged("AuthTokenLabel");
                }
            }
        }

        public string AuthToken
        {
            get => _authToken;
            set
            {
                if (_authToken != value)
                {
                    _authToken = value;
                    _accessKey = null;
                    OnPropertyChanged("AuthToken");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        internal void AddStandardChallenges()
        {
            if (_mfaChallenges.Count == 0)
            {
                _mfaChallenges.Add(new MFAChallenge("MFA13", "Please enter the last four digits of your social security number", "1234"));
                _mfaChallenges.Add(new MFAChallenge("MFA107", null, "QWIN 1700")); // Built in question for "App id"
                _mfaChallenges.Add(new MFAChallenge("123", "With which branch is your account associated?", "Newcastle"));
                _mfaChallenges.Add(new MFAChallenge("MFA16", null, "HigginBothum")); // Built in label for "Mother’s maiden name"
            }
        }

        internal void RemoveChallenges()
        {
            _mfaChallenges.Clear();
        }

        internal void Start(string prefix, int delayInMilliseconds)
        {
            _delay = delayInMilliseconds;
            _prefix = prefix;
            ThreadPool.QueueUserWorkItem(new WaitCallback(RunServer));
        }

        internal void Terminate()
        {
            _terminated = true;
            using (_http)
            {
                if (_http != null)
                {
                    _http.Stop();
                    _http = null;
                }
            }
        }

        //========================================================================================
        private void RunServer(object state)
        {
            _http = new HttpListener();
            _http.Prefixes.Add(_prefix);
            _http.Start();

            try
            {
                while (!_terminated)
                {
                    // Note: The GetContext method blocks while waiting for a request. 
                    HttpListenerContext context = _http.GetContext();
                    HttpListenerRequest request = context.Request;
                    HttpListenerResponse response = context.Response;

                    try
                    {
                        XDocument input = null;
                        using (Stream s = request.InputStream)
                        {
                            input = XDocument.Load(s);
                        }


                        Thread.Sleep(_delay);  // slow it down to make it more realistic otherwise it is too fast to see...

                        XDocument output = ProcessRequest(input);

                        using (Stream s = response.OutputStream)
                        {

                            XmlWriterSettings settings = new XmlWriterSettings();
                            settings.Indent = true;
                            using (XmlWriter w = XmlWriter.Create(s, settings))
                            {
                                output.WriteTo(w);
                            }
                            s.Close();
                        }

                    }
                    catch (Exception ex)
                    {
                        response.StatusCode = (int)HttpStatusCode.BadRequest;
                        using (Stream s = response.OutputStream)
                        {
                            using (StreamWriter writer = new StreamWriter(s, Encoding.UTF8))
                            {
                                writer.WriteLine(ex.ToString());
                            }
                            s.Close();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception " + ex.GetType().FullName + ": " + ex.Message);
            }
        }

        private XDocument ProcessRequest(XDocument input)
        {
            XElement root = input.Root; // should be "OFX"
            XDocument result = new XDocument(new XElement("OFX"));

            bool challenge = true;

            foreach (var e in root.Elements())
            {
                string name = e.Name.LocalName;
                if (name == "PROFMSGSRQV1") {
                    challenge = false;
                }
            }

            foreach (var e in root.Elements()) {
                ProcessElement(e, challenge, result);
            }
            return result;
        }

        private void ProcessElement(XElement e, bool challenge, XDocument result)
        {
            switch (e.Name.LocalName)
            {
                case "SIGNONMSGSRQV1":
                    ProcessSignonRequest(e, challenge, result);
                    break;

                case "SIGNUPMSGSRQV1":
                    ProcessSignupRequest(e, result);
                    break;

                case "PROFMSGSRQV1":
                    ProcessProfileRequest(e, result);
                    break;

                case "BANKMSGSRQV1":
                    ProcessBankRequest(e, result);
                    break;
            }
        }

        /*
        <SIGNONMSGSRSV1>
            <SONRS>
                <STATUS>
                    <CODE>0</CODE>
                    <SEVERITY>INFO</SEVERITY>
                </STATUS>
                <DTSERVER>20120207014332.760[0:GMT]</DTSERVER>
                <LANGUAGE>ENG</LANGUAGE>
                <FI>
                    <ORG>becu</ORG>
                    <FID>1001</FID>
                </FI>
            </SONRS>
        </SIGNONMSGSRSV1>
         */
        private void ProcessSignonRequest(XElement e, bool challenge, XDocument result)
        {
            XElement response;
            var sonrq = e.Element("SONRQ");
            int code = 0;
            string message = string.Empty;
            string severity = "INFO";

            if (sonrq == null) {
                throw new Exception("Missing SONRQ");
            }

            if (challenge) {

                string userId = GetElementValue(sonrq, "USERID");
                string pswd = GetElementValue(sonrq, "USERPASS");

                if (_userName != userId) {
                    message = "Invalid user id";
                    code = 15500;
                } else if (_password != pswd) {
                    message = "Invalid password";
                    code = (int)OfxErrors.SignonInvalid;
                }

                if (code == 0 && !String.IsNullOrEmpty(_userCred1Label)) {
                    string cred1 = GetElementValue(sonrq, "USERCRED1");
                    if (_userCred1 != cred1)
                    {
                        message = "Invalid USERCRED1";
                        code = (int)OfxErrors.SignonInvalid;
                    }
                }

                if (code == 0 && !String.IsNullOrEmpty(_userCred2Label)) {

                    string cred2 = GetElementValue(sonrq, "USERCRED2");
                    if (_userCred2 != cred2)
                    {
                        message = "Invalid USERCRED2";
                        code = (int)OfxErrors.SignonInvalid;
                    }
                }

                if (_mfaChallenges.Count == 0)
                {
                    challenge = false;
                }
            }

            XElement pinchrs = null;

            if (code == 0 && _changePassword) {

                // PINCHTRNRQ
                XElement pinchrq = e.Element("PINCHTRNRQ");

                if (pinchrq != null)
                {
                    pinchrs = ProcessChangePassword(pinchrq);
                    challenge = false;
                }
                else
                {
                    message = "Please change your password";
                    code = (int)OfxErrors.MustChangeUSERPASS;
                }
            }

            XElement accesskey = null;
            bool hasAccessKey = false;

            if (code == 0 && _accessKey != null) {
                string value = GetElementValue(sonrq, "ACCESSKEY");
                if (_accessKey == value) {
                    hasAccessKey = true;
                }
            }


            if (challenge && code == 0) {

                XElement challengeTran = e.Element("MFACHALLENGETRNRQ");

                if (challengeTran != null) {

                    // we are now expecting next request from user to contain the MFA challenge answers.
                    _mfaPendingResponse = true;

                    response = new XElement("SIGNONMSGSRSV1",
                                   new XElement("SONRS",
                                       new XElement("STATUS",
                                           new XElement("CODE", 0),
                                           new XElement("SEVERITY", "INFO")
                                       ),
                                       new XElement("DTSERVER", GetIsoDateTime(DateTime.Now)),
                                       new XElement("LANGUAGE", "ENG"),
                                       new XElement("FI",
                                            new XElement("ORG", "bankofhope"),
                                            new XElement("FID", 7777)
                                       )
                                    ),
                                    new XElement("MFACHALLENGETRNRS",
                                        // MFA Challenge Transaction aggregate
                                        new XElement("TRNUID", "66D3749F-5B3B-4DC3-87A3-8F795EA59EDB"),
                                        new XElement("STATUS",
                                            new XElement("CODE", 0),
                                            new XElement("SEVERITY", "INFO"),
                                            new XElement("MESSAGE", "SUCCESS")
                                        ),
                                        GetMFAChallenges()
                                    )
                                );

                    result.Root.Add(response);
                    return;
                }

                if (_mfaPendingResponse) {
                    _mfaPendingResponse = false;
                    if (!VerifyMFAAnswers(sonrq))
                    {
                        accesskey = null;
                        code = 3001;
                    }
                    else
                    {
                        _accessKey = Guid.NewGuid().ToString();
                        accesskey = new XElement("ACCESSKEY", _accessKey);
                    }
                } else if (code == 0 && _mfaChallenges.Count > 0) {
                    // Initiate MFA Challenge
                    code = 3000;
                }
            }

            if (code == 0 && !String.IsNullOrEmpty(_authTokenLabel) && !hasAccessKey) {

                string token = GetElementValue(sonrq, "AUTHTOKEN");
                if (token == null)
                {
                    message = "AUTHTOKEN Required";
                    code = (int)OfxErrors.AUTHTOKENRequired;
                }
                else if (_authToken != token)
                {
                    message = "Invalid AUTHTOKEN";
                    code = (int)OfxErrors.AUTHTOKENInvalid;
                }
                else
                {
                    _accessKey = Guid.NewGuid().ToString();
                    accesskey = new XElement("ACCESSKEY", _accessKey);
                }
            }
            response = new XElement("SIGNONMSGSRSV1",
               new XElement("SONRS",
                   new XElement("STATUS",
                       new XElement("CODE", code),
                       new XElement("SEVERITY", severity),
                       new XElement("MESSAGE", message)
                   ),
                   new XElement("DTSERVER", GetIsoDateTime(DateTime.Now)),
                   new XElement("LANGUAGE", "ENG"),
                   new XElement("FI",
                        new XElement("ORG", "bankofhope"),
                        new XElement("FID", 7777)
                   )
                )
            );


            if (pinchrs != null) {
                response.Add(pinchrs);
            } else if (accesskey != null) {
                response.Element("SONRS").Add(accesskey);
            }

            result.Root.Add(response);
        }

        private bool VerifyMFAAnswers(XElement sonrq)
        {
            var notVerified = new HashSet<MFAChallenge>(_mfaChallenges);

            foreach (var challengeResponse in sonrq.Elements("MFACHALLENGEANSWER"))
            {
                string id = challengeResponse.Element("MFAPRHASEID").Value;
                string answer = challengeResponse.Element("MFAPHRASEA").Value;

                foreach (var item in _mfaChallenges) {
                    if (item.PhraseId == id && item.PhraseAnswer == answer) {
                        notVerified.Remove(item);
                    }
                }
            }

            return notVerified.Count == 0;
        }

        /*
         * for example:
            <MFACHALLENGERS>
                <!--MFA Challenge aggregate-->
                <MFACHALLENGE>
                    <MFAPHRASEID>MFA13</MFAPHRASEID>
                    <MFAPHRASELABEL>Please enter the last four digits of your social security number.</MFAPHRASELABEL>
                </MFACHALLENGE>
                <MFACHALLENGE>
                    <!--built in question w/o label and no user prompt -->
                    <MFAPHRASEID>MFA107</MFAPHRASEID>
                </MFACHALLENGE>
                <MFACHALLENGE>
                    <!--MFA Challenge aggregate-->
                    <MFAPHRASEID>123</MFAPHRASEID>
                    <MFAPHRASELABEL>With which branch is your account associated?</MFAPHRASELABEL>
                </MFACHALLENGE>
                <MFACHALLENGE>
                    <!--should have built in label-->
                    <MFAPHRASEID>MFA16</MFAPHRASEID>
                </MFACHALLENGE>
            </MFACHALLENGERS>
         */
        private XElement GetMFAChallenges()
        {
            XElement wrapper = new XElement("MFACHALLENGERS");

            foreach (var item in _mfaChallenges) {

                XElement x = new XElement("MFACHALLENGE", new XElement("MFAPHRASEID", item.PhraseId));

                if (!String.IsNullOrWhiteSpace(item.PhraseLabel)) {
                    x.Add(new XElement("MFAPHRASELABEL", item.PhraseLabel));
                }

                wrapper.Add(x);
            }

            return wrapper;
        }

        private void ProcessSignupRequest(XElement e, XDocument result)
        {
            XElement r = new XElement("SIGNUPMSGSRSV1",
                            new XElement("ACCTINFOTRNRS",
                                new XElement("TRNUID", GetTransactionId(e)),
                                new XElement("STATUS",
                                    new XElement("CODE", "0"),
                                    new XElement("SEVERITY", "INFO")
                                ),
                                new XElement("CLTCOOKIE", "1"),
                                new XElement("ACCTINFORS",
                                    new XElement("DTACCTUP", GetIsoDateTime(DateTime.Now)),
                                    new XElement("ACCTINFO",
                                        new XElement("DESC", "Checking"),
                                        new XElement("BANKACCTINFO",
                                            new XElement("BANKACCTFROM",
                                                new XElement("BANKID", "123456"),
                                                new XElement("ACCTID", "456789"),
                                                new XElement("ACCTTYPE", "CHECKING")
                                            ),
                                            new XElement("SUPTXDL", "Y"),
                                            new XElement("XFERSRC", "Y"),
                                            new XElement("XFERDEST", "Y"),
                                            new XElement("SVCSTATUS", "ACTIVE")
                                        )
                                    )
                                )
                            )
                        );
            result.Root.Add(r);
        }

        private XElement GetMsgSetCore()
        {
            return new XElement("MSGSETCORE",
                        new XElement("VER", 1),
                        new XElement("URL", _prefix),
                        new XElement("OFXSEC", "NONE"),
                        new XElement("TRANSPSEC", "Y"),
                        new XElement("SIGNONREALM", "DefaultRealm"),
                        new XElement("LANGUAGE", "ENG"),
                        new XElement("SYNCMODE", "FULL"),
                        new XElement("RESPFILEER", "Y"),
                        new XElement("SPNAME", "Corillian Corp"));
        }

        /*
        <PROFMSGSRQV1>
          <PROFTRNRQ>
            <TRNUID>6ab04f71-6bcb-44d2-a021-16931e1f251d</TRNUID>
            <PROFRQ>
              <CLIENTROUTING>MSGSET</CLIENTROUTING>
              <DTPROFUP>20120207015824</DTPROFUP>
            </PROFRQ>
          </PROFTRNRQ>
        </PROFMSGSRQV1>
        */
        private void ProcessProfileRequest(XElement e, XDocument result)
        {
            var r = new XElement("PROFMSGSRSV1",
                        new XElement("PROFTRNRS",
                            new XElement("TRNUID", GetTransactionId(e)),
                            new XElement("STATUS",
                                new XElement("CODE", 0),
                                new XElement("SEVERITY", "INFO")
                            ),
                            new XElement("PROFRS",
                                new XElement("MSGSETLIST",
                                    new XElement("SIGNONMSGSET",
                                        new XElement("SIGNONMSGSETV1", GetMsgSetCore())
                                    ),
                                    new XElement("SIGNUPMSGSET",
                                        new XElement("SIGNUPMSGSETV1", GetMsgSetCore(),
                                            new XElement("WEBENROLL",
                                                new XElement("URL", "http://localhost")
                                            ),
                                            new XElement("CHGUSERINFO", "N"),
                                            new XElement("AVAILACCTS", "Y"),
                                            new XElement("CLIENTACTREQ", "N")
                                        )
                                    ),
                                    new XElement("BANKMSGSET",
                                        new XElement("BANKMSGSETV1", GetMsgSetCore(),
                                            new XElement("CLOSINGAVAIL", "N"),
                                            new XElement("XFERPROF",
                                                new XElement("PROCENDTM", "235959[0:GMT]"),
                                                new XElement("CANSCHED", "Y"),
                                                new XElement("CANRECUR", "N"),
                                                new XElement("CANMODXFERS", "N"),
                                                new XElement("CANMODMDLS", "N"),
                                                new XElement("MODELWND", "0"),
                                                new XElement("DAYSWITH", "0"),
                                                new XElement("DFLTDAYSTOPAY", "0")
                                            ),
                                            new XElement("EMAILPROF",
                                                new XElement("CANEMAIL", "N"),
                                                new XElement("CANNOTIFY", "Y")
                                            )
                                        )
                                    ),
                                    new XElement("PROFMSGSET",
                                        new XElement("PROFMSGSETV1", GetMsgSetCore())
                                    )
                                ),
                                new XElement("SIGNONINFOLIST", GetSignOnInfo()),
                                new XElement("DTPROFUP", "20111031070000.000[0:GMT]"),
                                new XElement("FINAME", "Last Chance Bank of Hope"),
                                new XElement("ADDR1", "123 Walkabout Drive"),
                                new XElement("CITY", "Wooloomaloo"),
                                new XElement("STATE", "WA"),
                                new XElement("POSTALCODE", "12345"),
                                new XElement("COUNTRY", "USA"),
                                new XElement("CSPHONE", "123-456-7890"),
                                new XElement("URL", "http://localhost"),
                                new XElement("EMAIL", "feedback@localhost.org")
                            )
                        )
                    );

            result.Root.Add(r);
        }

        private XElement GetSignOnInfo()
        {
            return new XElement("SIGNONINFO",
                         new XElement("SIGNONREALM", "DefaultRealm"),
                         new XElement("MIN", "6"),
                         new XElement("MAX", "32"),
                         new XElement("CHARTYPE", "ALPHAORNUMERIC"),
                         new XElement("CASESEN", "N"),
                         new XElement("SPECIAL", "N"),
                         new XElement("SPACES", "N"),
                         new XElement("PINCH", "Y"),
                         new XElement("CHGPINFIRST", "N"),
                         new XElement("USERCRED1LABEL", _userCred1Label),
                         new XElement("USERCRED2LABEL", _userCred2Label),
                         new XElement("CLIENTUIDREQ", "Y"),
                         new XElement("AUTHTOKENFIRST", "N"),
                         new XElement("AUTHTOKENLABEL", "Authentication Token"),
                         new XElement("AUTHTOKENINFOURL", "http://www.bing.com"),
                         new XElement("MFACHALLENGESUPT", "Y"),
                         new XElement("MFACHALLENGEFIRST", "N")
                     );
        }

        private void ProcessBankRequest(XElement e, XDocument result)
        {
            var r = new XElement("BANKMSGSRSV1");

            foreach (var statementRequest in e.Elements("STMTTRNRQ")) {
                ProcessBankStatementRequest(statementRequest, r);
            }
            result.Root.Add(r);
        }

        private string GetTransactionId(XElement e)
        {
            var t = e.Descendants("TRNUID").FirstOrDefault();
            if (t != null)
            {
                return t.Value;
            }
            return Guid.NewGuid().ToString();
        }

        private XElement GetBankAccountFrom(XElement e)
        {
            XElement t = e.Descendants("BANKACCTFROM").FirstOrDefault();
            if (t == null)
            {
                throw new Exception("Missing BANKACCTFROM");
            }
            return t;
        }

        /*
        <STMTTRNRQ>
          <TRNUID>042919d9-ccb5-4915-bc10-0f248a60fd2f</TRNUID>
          <CLTCOOKIE>1</CLTCOOKIE>
          <STMTRQ>
            <BANKACCTFROM>
              <BANKID>123456</BANKID>
              <ACCTID>456789</ACCTID>
              <ACCTTYPE>CHECKING</ACCTTYPE>
            </BANKACCTFROM>
            <INCTRAN>
              <DTSTART>20120310</DTSTART>
              <INCLUDE>Y</INCLUDE>
            </INCTRAN>
          </STMTRQ>
        </STMTTRNRQ>
         */
        private void ProcessBankStatementRequest(XElement e, XElement result)
        {
            var acct = GetBankAccountFrom(e);
            var endDate = DateTime.Now;
            var startDate = endDate.AddMonths(-1);
            XElement stmt = null;
            try
            {
                stmt = new XElement("STMTTRNRS",
                       new XElement("TRNUID", GetTransactionId(e)),
                       new XElement("STATUS",
                           new XElement("CODE", "0"),
                           new XElement("SEVERITY", "INFO")
                       ),
                       new XElement("CLTCOOKIE", "1"),
                       new XElement("STMTRS",
                           new XElement("CURDEF", "USD"),
                           acct,
                           new XElement("BANKTRANLIST",
                               new XElement("DTSTART", GetIsoDateTime(startDate)),
                               new XElement("DTEND", GetIsoDateTime(endDate)),
                               GetRandomTransactions(startDate, endDate)
                           ),
                           new XElement("LEDGERBAL",
                               new XElement("BALAMT", "8722.69"),
                               new XElement("DTASOF", GetIsoDateTime(DateTime.Now))
                           ),
                           new XElement("AVAILBAL",
                               new XElement("BALAMT", "8717.69"),
                               new XElement("DTASOF", GetIsoDateTime(DateTime.Now))
                           )
                       )
                   );
            } catch (Exception ex)
            {
                stmt = new XElement("STMTTRNRS",
                        new XElement("TRNUID", GetTransactionId(e)),
                        new XElement("STATUS",
                            new XElement("CODE", "2000"),
                            new XElement("SEVERITY", "ERROR"),
                            new XElement("MESSAGE", ex.Message)
                        )
                    );
            }
            result.Add(stmt);
        }

        private XElement ProcessChangePassword(XElement e)
        {
            var user = "";
            var code = 0;
            var severity = "INFO";
            var message = "";

            var req = e.Element("PINCHRQ");
            if (req != null) {
                var ue = req.Element("USERID");
                if (ue != null) {
                    user = ue.Value;
                    if (user != _userName) {
                        message = "User id unknown";
                    }
                } else {
                    message = "Missing USERID";
                }

                var newpass = req.Element("NEWUSERPASS");
                if (newpass != null) {
                    Password = newpass.Value;
                    ChangePassword = false;
                } else {
                    message = "Cannot have empty password";
                }
            } else {
                message = "Missing PINCHRQ";
            }

            if (!string.IsNullOrEmpty(message)) {
                severity = "ERROR";
                code = (int)OfxErrors.CouldNotChangeUSERPASS;
            }

            XElement r = new XElement("PINCHTRNRS",
                                new XElement("TRNUID", GetTransactionId(e)),
                                new XElement("STATUS",
                                    new XElement("CODE", code),
                                    new XElement("SEVERITY", severity),
                                    new XElement("MESSAGE", message)
                                ),
                                new XElement("PINCHRS",
                                    new XElement("USERID", user),
                                    new XElement("DTCHANGED", GetIsoDateTime(DateTime.Now))
                                )
                            );

            return r;
        }

        private object GetRandomTransactions(DateTime startDate, DateTime endDate)
        {
            var span = endDate - startDate;
            var incr = new TimeSpan(span.Ticks / 10);
            var rand = new Random(Environment.TickCount);
            var result = new List<XElement>();

            for (int index = 0; index < 10; index++) {

                var payee = GetRandomPayee(rand);
                var range = payee.Max - payee.Min;
                var amount = rand.NextDouble() * range;
                amount = amount + payee.Min;
                amount = Math.Round(amount, 2);

                var type = "DEBIT";
                if (amount > 0) {
                    type = "CREDIT";
                }

                var e = new XElement("STMTTRN",
                            new XElement("TRNTYPE", type),
                            new XElement("DTPOSTED", GetIsoDateTime(startDate)),
                            new XElement("TRNAMT", amount),
                            new XElement("FITID", index),
                            new XElement("NAME", payee.Name)
                        );

                result.Add(e);

                startDate = startDate + incr;
            }

            return result;
        }

        private SamplePayee GetRandomPayee(Random rand)
        {
            var index = rand.Next(0, _payees.Count);
            return _payees[index];
        }

        private object GetIsoDateTime(DateTime dt)
        {
            var gmt = dt.ToUniversalTime();
            var isodate = GetIsoDate(gmt) + gmt.Hour.ToString("D2") + gmt.Minute.ToString("D2") + gmt.Second.ToString("D2");
            return isodate;
        }

        private string GetIsoDate(DateTime dt)
        {
            return dt.Year.ToString() + dt.Month.ToString("D2") + dt.Day.ToString("D2");
        }

        private string GetElementValue(XElement e, string name)
        {
            XElement child = e.Element(name);
            if (child == null) {
                return null;
            }
            return child.Value;
        }

        private void AddSamplePayees()
        {
            _payees = new List<SamplePayee>();
            _payees.Add(new SamplePayee("Sprint PCS", -275, -27));
            _payees.Add(new SamplePayee("Costco Gas", -76, -12));
            _payees.Add(new SamplePayee("Garlic Jims Famous Gourmet Pizza", -56, -11));
            _payees.Add(new SamplePayee("Veterinary Hostpital", -222, -11));
            _payees.Add(new SamplePayee("Safeway", -199, 50));
            _payees.Add(new SamplePayee("World Market", -100, -9));
            _payees.Add(new SamplePayee("AAA", -242, -3));
            _payees.Add(new SamplePayee("Radio Shack", -199, 54));
            _payees.Add(new SamplePayee("Costco", -4621, 870));
            _payees.Add(new SamplePayee("Rite Aid", -50, -3));
            _payees.Add(new SamplePayee("Starbucks", -108, -1));
            _payees.Add(new SamplePayee("GTC Telecom", -98, -2));
            _payees.Add(new SamplePayee("ARCO", -73, -3));
            _payees.Add(new SamplePayee("Home Depot", -912, 224));
            _payees.Add(new SamplePayee("McLendon Hardware", -202, 25));
            _payees.Add(new SamplePayee("World Vision", -147, -5));
            _payees.Add(new SamplePayee("State Farm Insurance", -3100, 1191));
            _payees.Add(new SamplePayee("Bank of America", -2063, -2063));
            _payees.Add(new SamplePayee("Comcast", -74, -49));
            _payees.Add(new SamplePayee("Puget Sound Energy", -428, 50));
            _payees.Add(new SamplePayee("Albertsons", -62, -1));
            _payees.Add(new SamplePayee("Top Foods", -203, 0));
            _payees.Add(new SamplePayee("Target", -218, 76));
            _payees.Add(new SamplePayee("Ruby's Diner", -99, -3));
            _payees.Add(new SamplePayee("DeYoung's Farm & Garden", -181, 84));
            _payees.Add(new SamplePayee("Water District", -510, -42));
            _payees.Add(new SamplePayee("Whole Foods", -167, -9));
            _payees.Add(new SamplePayee("Applebee's", -123, -14));
            _payees.Add(new SamplePayee("PCC", -119, -3));
            _payees.Add(new SamplePayee("Dairy Queen", -20, -1));
            _payees.Add(new SamplePayee("In Harmony", -438, -9));
            _payees.Add(new SamplePayee("Jerusalem Post", -49, -3));
            _payees.Add(new SamplePayee("Volvo Dealer", -2000, -23));
            _payees.Add(new SamplePayee("Big Foot Bagels", -30, -5));
            _payees.Add(new SamplePayee("Amazon.com", -293, 71));
            _payees.Add(new SamplePayee("REI", -855, 290));
            _payees.Add(new SamplePayee("PetSmart", -144, 10));
            _payees.Add(new SamplePayee("Jamba Juice", -39, -4));
            _payees.Add(new SamplePayee("Waste Management", -150, -92));
            _payees.Add(new SamplePayee("Fred Meyer", -121, 42));
            _payees.Add(new SamplePayee("Molbaks Inc.", -146, -7));
            _payees.Add(new SamplePayee("Walmart", -183, 41));
            _payees.Add(new SamplePayee("Verizon", -83, -33));
            _payees.Add(new SamplePayee("Barnes & Noble", -113, -2));
            _payees.Add(new SamplePayee("McDonalds", -57, -1));
            _payees.Add(new SamplePayee("Quizno's", -51, 0));
            _payees.Add(new SamplePayee("Pony Mailbox", -78, -4));
            _payees.Add(new SamplePayee("Blockbuster Video", -115, 36));
            _payees.Add(new SamplePayee("Office Max", -128, -2));
            _payees.Add(new SamplePayee("Teddy's Bigger Burgers", -48, -4));
            _payees.Add(new SamplePayee("Pallino Pastaria", -57, -15));
            _payees.Add(new SamplePayee("Famous Footwear", -322, 142));
            _payees.Add(new SamplePayee("Department of Licensing", -61, -20));
            _payees.Add(new SamplePayee("Play It Again Sports", -76, 81));
            _payees.Add(new SamplePayee("NewEgg.com", -1745, 161));
            _payees.Add(new SamplePayee("Marriot Atlanta m:Store", -19, -3));
            _payees.Add(new SamplePayee("Marta Atlanta", -18, -4));
            _payees.Add(new SamplePayee("Applebees", -73, -15));
            _payees.Add(new SamplePayee("Seattle Times", -98, -27));
            _payees.Add(new SamplePayee("Foot Zone", -255, 109));
            _payees.Add(new SamplePayee("Staples", -161, 326));
            _payees.Add(new SamplePayee("The Whole Pet Shop", -78, -8));
            _payees.Add(new SamplePayee("Chinese Restaurant", -99, -46));
            _payees.Add(new SamplePayee("Animal Healing Center", -328, 140));
            _payees.Add(new SamplePayee("Travelsmith Catalogue", -419, 190));
            _payees.Add(new SamplePayee("Baskin Robbins", -48, -5));
            _payees.Add(new SamplePayee("Subway", -46, -5));
            _payees.Add(new SamplePayee("The Lego Store", -405, -13));
            _payees.Add(new SamplePayee("Red Robin", -85, -10));
            _payees.Add(new SamplePayee("Dentist", -200, -12));
            _payees.Add(new SamplePayee("Black Angus", -227, -28));
            _payees.Add(new SamplePayee("Lands End", -437, 218));
            _payees.Add(new SamplePayee("Gymnastics", -533, -30));
            _payees.Add(new SamplePayee("KFC", -45, -5));
            _payees.Add(new SamplePayee("Fry's Electronics", -4025, 163));
            _payees.Add(new SamplePayee("Milk Delivery", -57, -8));
            _payees.Add(new SamplePayee("Soccer West", -140, -13));
            _payees.Add(new SamplePayee("Borders Books", -114, 15));
            _payees.Add(new SamplePayee("Stevens Pass Ski Resort", -289, -18));
            _payees.Add(new SamplePayee("Ben Franklin", -165, -5));
            _payees.Add(new SamplePayee("Audible Books", -41, 68));
            _payees.Add(new SamplePayee("Great Harvest Bakery", -29, 0));
            _payees.Add(new SamplePayee("Osh Kosh B'gosh", -212, -17));
            _payees.Add(new SamplePayee("QFC", -36, -1));
            _payees.Add(new SamplePayee("Trader Joe's", -93, 0));
            _payees.Add(new SamplePayee("Round Table Pizza", -84, -11));
            _payees.Add(new SamplePayee("Southwest Airlines", -394, 243));
            _payees.Add(new SamplePayee("Consumer Reports", -26, 24));
            _payees.Add(new SamplePayee("Cost Plus World Market", -162, -1));
            _payees.Add(new SamplePayee("IKEA", -585, 79));
            _payees.Add(new SamplePayee("Lego Shop At Home", -391, -3));
            _payees.Add(new SamplePayee("Pacific Science Center", -131, -10));
            _payees.Add(new SamplePayee("Countrywide", -2063, -2063));
            _payees.Add(new SamplePayee("Alaska Airlines", -299, -5));
            _payees.Add(new SamplePayee("Chinese Restaurant", -129, -25));
            _payees.Add(new SamplePayee("Stevens Pass Cascadian", -27, -4));
            _payees.Add(new SamplePayee("Toys R Us", -97, 4));
            _payees.Add(new SamplePayee("Linens N Things", -457, -5));
            _payees.Add(new SamplePayee("Ski Rentals", -609, 32));
            _payees.Add(new SamplePayee("Tapatio Mexican Grill", -83, -7));
            _payees.Add(new SamplePayee("Sir Plus", -110, -3));
            _payees.Add(new SamplePayee("Bartell Drugs", -76, -1));
            _payees.Add(new SamplePayee("Stevens Pass", -540, 34));
            _payees.Add(new SamplePayee("SeaTac Airport", -8, 0));
            _payees.Add(new SamplePayee("Lego Store", -199, -10));
            _payees.Add(new SamplePayee("Intuit", -76, 76));
            _payees.Add(new SamplePayee("Loews", -49, -10));
            _payees.Add(new SamplePayee("Residential House Cleaning", -135, -125));
            _payees.Add(new SamplePayee("Tina Eddy", -50, -25));
            _payees.Add(new SamplePayee("A Canine Experience", -755, -25));
            _payees.Add(new SamplePayee("Veterinary Hospital", -563, -21));
            _payees.Add(new SamplePayee("Denny's", -20, -1));
            _payees.Add(new SamplePayee("Museum Of Flight", -48, -3));
            _payees.Add(new SamplePayee("USPS", -36, -1));
            _payees.Add(new SamplePayee("Legoland", -290, -3));
            _payees.Add(new SamplePayee("Sears", -1810, 63));
            _payees.Add(new SamplePayee("Hobby Town USA", -80, -5));
        }

        private void OnPropertyChanged(string name)
        {
            _dispatcher.BeginInvoke(new Action<string>(RaisePropertyChanged), name);
        }

        private void RaisePropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}
