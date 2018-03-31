using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

// This file contains the OFX object model that implements the Open Financial Exchange spec version 2.03.
namespace Walkabout.Ofx
{
    public enum OfxErrorCode
    {
        None = 0, // 
        ClientUptoDate = 1, // Based on the client timestamp, the client has the latest information. The response does not supply any additional information.
        GeneralError = 2000, // Error other than those specified by the remaining error codes.
        InvalidAccount = 2001,
        GeneralAccountError = 2002, // Account error not specified by the remaining error codes
        AccountNotFound = 2003, // The specified account number does not correspond to one of the user’s accounts.
        AccountClosed = 2004, // The specified account number corresponds to an account that has been closed.
        AccountNotAuthorized = 2005, // The user is not authorized to perform this action on the account, or the server does not allow this type of action to be performed on the account
        SourceAccountNotFound = 2006, // The specified account number does not correspond to one of the user’s accounts.
        SourceAccountClosed = 2007, // The specified account number corresponds to an account that has been closed
        SourceAccountNnotAuthorized = 2008, // The user is not authorized to perform this action on the account, or the server does not allow this type of action to be performed on the account
        DestinationAccountNotFound = 2009, // The specified account number does not correspond to one of the user’s accounts
        DestinationAccountClosed = 2010, // The specified account number corresponds to an account that has been closed
        DestinationAccountNotAuthorized = 2011, // The user is not authorized to perform this action on the account, or the server does not allow this type of action to be performed on the account
        InvalidAmount = 2012, // The specified amount is not valid for this action; for example, the user specified a negative payment amount
        DateTooSoon = 2014, // The server cannot process the requested action by the date specified by the user
        DateTooFarInFuture = 2015, // The server cannot accept requests for an action that far in the future
        TransactionAlreadyCommitted = 2016, // Transaction has entered the processing loop and cannot be modified/cancelled using OFX. The transaction may still be cancelled or modified using other means (for example, a phone call to Customer Service).
        AlreadyCanceled = 2017, // The transaction cannot be canceled or modified because it has already been canceled.
        UnknownServerId = 2018, // The specified server ID does not exist or no longer exists
        DuplicateRequest = 2019, // A request with this <TRNUID> has already been received and processed
        InvalidDate = 2020, // The specified datetime stamp cannot be parsed; for instance, the datetime stamp specifies 25:00 hours
        UnsupportedVersion = 2021, // The server does not support the requested version. The version of the message set specified by the client is not supported by this server.
        InvalidTan = 2022, // The server was unable to validate the TAN sent in the request
        UnknownFITID = 2023, // The specified FITID/BILLID does not exist or no longer exists
        BranchIdMissing = 2025, // A <BRANCHID> value must be provided in the <BANKACCTFROM> aggregate for this country system, but this field is missing
        BankNameDoesntMatchBank = 2026, // The value of <BANKNAME> in the <EXTBANKACCTTO> aggregate is inconsistent with the value of <BANKID> in the <BANKACCTTO> aggregate.
        InvalidateDateRange = 2027, // Response for non-overlapping dates, date ranges in the future, et cetera
        RequestedElementUnknown = 2028, // One or more elements of the request were not recognized by the server or the server (as noted in the FI Profile) does not support the elements. The server executed the element transactions it understood and supported. For example, the request file included private tags in a <PMTRQ> but the server was able to execute the rest of the request.
        MFAChallengeAuthenticationRequired = 3000, // User credentials are correct, but further authentication required. Client should send <MFACHALLENGERQ> in next request.
        MFAChallengeInformationIsInvalid = 3001, // User or client information sent in MFACHALLENGEA contains invalid information
        RejectIfMissingInvalidWithoutToken = 6500, // This error code may appear in the <SYNCERROR> element of an <xxxSYNCRS> wrapper (in <PRESDLVMSGSRSV1> and V2 message set responses) or the <CODE> contained in any embedded transaction wrappers within a sync response. The corresponding sync request wrapper included <REJECTIFMISSING>Y with <REFRESH>Y or <TOKENONLY>Y, which is illegal
        EmbeddedTransactionsInRequestFailed = 6501, // <REJECTIFMISSING>Y and embedded transactions appeared in the request sync wrapper and the provided <TOKEN> was out of date. This code should be used in the <SYNCERROR> of the response sync wrapper.
        UnableToProcessEmbeddedTransactionDueToOutOfDate = 6502, // Used in response transaction wrapper for embedded transactions when <SYNCERROR>6501 appears in the surrounding sync wrapper.
        StopCheckInProcess = 10000, // Stop check is already in process
        TooManyChecksToProcess = 10500, // The stop-payment request <STPCHKRQ> specifies too many checks
        InvalidPayee = 10501, // Payee error not specified by the remaining error codes
        InvalidPayeeAddress = 10502, // Some portion of the payee’s address is incorrect or unknown
        InvalidPayeeAccountNumber = 10503, // The account number <PAYACCT> of the requested payee is invalid        
        InsufficientFunds = 10504, // The server cannot process the request because the specified account does not have enough funds.
        CannotModifyElement = 10505, // The server does not allow modifications to one or more values in a modification request.
        CannotModifySourceAccount = 10506, // Reserved for future use.
        CannotModifyDestinationAccount = 10507, // Reserved for future use.
        InvalidFrequency = 10508, // The specified frequency <FREQ> does not match one of the accepted frequencies for recurring transactions.
        ModelAlreadyCanceled = 10509, // The server has already canceled the specified recurring model.
        InvalidPayeeID = 10510, // The specified payee ID does not exist or no longer exists.
        InvalidPayeeCity = 10511, // The specified city is incorrect or unknown.
        InvalidPayeeState = 10512,  // The specified state is incorrect or unknown.
        InvalidPayeePostalCode = 10513, // The specified postal code is incorrect or unknown.
        TransactionAlreadyProcessed = 10514, // Transaction has already been sent or date due is past
        PayeeNotModifiableByClient = 10515, // The server does not allow clients to change payee information.
        WireBeneficiaryInvalid = 10516, // The specified wire beneficiary does not exist or no longer exists.
        InvalidPayeeName = 10517, // The server does not recognize the specified payee name.
        UnknownModelID = 10518, // The specified model ID does not exist or no longer exists.
        InvalidPayeeListID = 10519, // The specified payee list ID does not exist or no longer exists.
        TableTypeNotFound = 10600, // The specified table type is not recognized or does not exist.
        InvestmentTransactionDownloadNotSupported = 12250, // The server does not support investment transaction download.
        InvestmentPositionDownloadNotSupported = 12251, // The server does not support investment position download.
        InvestmentPositionsForSpecifiedDateNotAvailable = 12252, // The server does not support investment positions for the specified date.
        InvestmentOpenOrderDownloadNotSupported = 12253, // The server does not support open order download.
        InvestmentBalancesDownloadNotSupported = 12254, // The server does not support investment balances download.
        Error401kNotAvailableForThisAccount = 12255, // (ERROR) 401(k) information requested from a non-401(k) account.
        OneOrMoreSecuritiesNotFound = 12500, // (ERROR) The server could not find the requested securities.
        UserIDAndPasswordWillBeSentOutOfBand = 13000, // (INFO) The server will send the user ID and password via postal mail, e-mail, or another means. The accompanying message will provide details.
        UnableToEnrollUser = 13500, // (ERROR) The server could not enroll the user.
        UserAlreadyEnrolled = 13501, // (ERROR) The server has already enrolled the user.
        InvalidService = 13502, // (ERROR) The server does not support the service <SVC> specified in the service-activation request.
        CannotChangeUserInformation = 13503, // (ERROR) The server does not support the <CHGUSERINFORQ> request.
        FIMissingOrInvalidInSONRQ = 13504, // (ERROR) The FI requires the client to provide the <FI> aggregate in the <SONRQ> request, but either none was provided, or the one provided was invalid.
        Form1099NotAvailable = 14500, // (ERROR) 1099 forms are not yet available for the tax year requested.
        Form1099NotAvailableForUserID = 14501, // (ERROR) This user does not have any 1099 forms available.
        W2formsNotAvailable = 14600, // (ERROR) W2 forms are not yet available for the tax year requested.
        W2FormsNotAvailableForUserID = 14601, // (ERROR) The user does not have any W2 forms available.
        Form1098NotAvailable = 14700, // (ERROR) 1098 forms are not yet available for the tax year requested.
        Form1098NotAvailableForUserID = 14701, // (ERROR) The user does not have any 1098 forms available.
        MustChangeUSERPASS = 15000, // (INFO) The user must change his or her <USERPASS> number as part of the next OFX request.
        SignonInvalid = 15500, // (ERROR) The user cannot signon because he or she entered an invalid user ID or password.
        CustomerAccountAlreadyInUse = 15501, // (ERROR) The server allows only one connection at a time, and another user is already signed on. Please try again later.
        UserPasslockout = 15502, // (ERROR) The server has received too many failed signon attempts for this user. Please call the FI’s technical support number.
        CouldNotChangeUSERPASS = 15503, // (ERROR) The server does not support the <PINCHRQ> request.
        CouldNotProvideRandomData = 15504, // (ERROR) The server could not generate random data as requested by the <CHALLENGERQ>.
        CountrySystemNotSupported = 15505, // (ERROR) The server does not support the country specified in the <COUNTRY> field of the <SONRQ> aggregate.
        EmptySignonNotSupported = 15506, // (ERROR) The server does not support signons not accompanied by some other transaction.
        SignonInvalidWithoutSupportingPinChangeRequest = 15507, // (ERROR) The OFX block associated with the signon does not contain a pin change request and should.
        TransactionNotAuthorized = 15508, // (ERROR) Current user is not authorized to perform this action on behalf of the <USERID>.
        CLIENTUIDError = 15510, // (ERROR) The CLIENTUID sent by the client was incorrect. User must register the Client UID.
        MFAError = 15511, // (ERROR) User should contact financial institution.
        AUTHTOKENRequired = 15512, // (ERROR) User needs to contact financial institution to obtain AUTHTOKEN. Client should send it in the next request.
        AUTHTOKENInvalid = 15513, // (ERROR) The AUTHTOKEN sent by the client was invalid.
        HTMLNotAllowed = 16500, // (ERROR) The server does not accept HTML formatting in the request.
        UnknownMailTo = 16501, // (ERROR) The server was unable to send mail to the specified Internet address.
        InvalidURL = 16502, // (ERROR) The server could not parse the URL.
        UnableToGetURL = 16503, // (ERROR) The server was unable to retrieve the information at this URL (e.g., an HTTP 400 or 500 series error).
    }

    public class OFX
    {
        public OFX() { }

        [XmlElement("SIGNONMSGSRSV1")]
        public SignOnResponseMessageSet SignOnMessageResponse { get; set; }

        [XmlElement("SIGNUPMSGSRSV1")]
        public SignUpResponseMessageSet SignUpMessageResponse { get; set; }

        [XmlElement("PROFMSGSRSV1")]
        public ProfileResponseMessageSet ProfileMessageSet { get; set; }

        public static OFX Deserialize(XDocument doc)
        {
            try
            {
                OFX ofx;
                XmlSerializer s = new XmlSerializer(typeof(OFX));
                using (XmlReader r = XmlReader.Create(new StringReader(doc.ToString())))
                {
                    ofx = (OFX)s.Deserialize(r);
                }
                return ofx;
            }
            catch
            {
                throw new OfxException("Error parsing OFX response", "Error", doc.ToString(), null);
            }
        }

    }

    public class MfaChallengeTransaction : TransactionWrapper
    {        
        [XmlArrayItem("MFACHALLENGE")]
        [XmlArray("MFACHALLENGERS")]
        public List<MfaChallenge> Challenges { get; set; }
    }


    public class MfaChallenge
    {
        /// <summary>
        /// Identifier for the challenge question. It should be unique for this challenge question but not unique for the user, session, etc. A-32. 
        /// </summary>
        [XmlElement("MFAPHRASEID")]
        public string PhraseId { get; set; }

        /// <summary>
        /// The textual challenge question. This should be as appropriate as possible for display to the user. A-64
        /// </summary>
        [XmlElement("MFAPHRASELABEL")]
        public string PhraseLabel { get; set; }
    }

    public class ProfileResponseMessageSet
    {
        public ProfileResponseMessageSet() { }

        [XmlElement("PROFTRNRS")]
        public ProfileMessageResponse ProfileMessageResponse { get; set; }
    }


    public class SignOnResponseMessageSet
    {
        public SignOnResponseMessageSet() { }

        [XmlElement("SONRS")]
        public SignOnResponse SignOnResponse { get; set; }

        [XmlElement("PINCHTRNRS")]
        public PinChangeResponseTransaction PinChangeResponseTransaction { get; set; }

        [XmlElement("MFACHALLENGETRNRS")]
        public MfaChallengeTransaction MfaChallengeTransaction { get; set; }
    }

    public class OfxSignOnInfoList
    {
        [XmlElement("SIGNONINFO")]
        public OfxSignOnInfo[] OfxSignOnInfo { get; set; }
    }

    public class OfxSignOnInfo
    {
        public OfxSignOnInfo() { }

        /// <summary>
        /// Identifies this realm
        /// </summary>
        [XmlElement("SIGNONREALMN")]
        public string SignOnRealm { get; set; }

        /// <summary>
        /// Minimum number of password characters, N-2
        /// </summary>
        [XmlElement("MIN")]
        public int MinimumLength { get; set; }

        /// <summary>
        /// Maximum number of password characters, N-2
        /// </summary>
        [XmlElement("MAX")]
        public int MaximumLength { get; set; }

        /// <summary>
        /// Type of characters allowed in password
        /// ALPHAONLY - Password may not contain numeric characters. The server would allow “abbc”, but not “1223” or “a122”.
        /// NUMERICONLY - Password may not contain alphabetic characters. The server would allow “1223”, but not “abbc” or “a122”.
        /// ALPHAORNUMERIC - Password may contain alphabetic or numeric characters (or both). The server would allow “abbc”, “1223”, or “a122”.
        /// ALPHAANDNUMERIC - Password must contain both alphabetic and numeric characters. The server would allow “a122”, but not “abbc” or “1223”.
        /// </summary>
        [XmlElement("CHARTYPE")]
        public string CharType { get; set; }

        /// <summary>
        /// Y if password is case-sensitive, Boolean
        /// </summary>
        [XmlElement("CASESEN")]
        public string CaseSensitive { get; set; }

        /// <summary>
        /// Y if special characters are allowed over and above those characters allowed by CHARTYPE and SPACES, Boolean
        /// </summary>
        [XmlElement("SPECIAL")]
        public string SpecialCharsAllowed { get; set; }

        /// <summary>
        /// Y if spaces are allowed over and above those characters allowed by CHARTYPE and SPECIAL, Boolean
        /// </summary>
        [XmlElement("SPACES")]
        public string SpacesAllowed { get; set; }

        /// <summary>
        /// Y if server supports <PINCHRQ> (PIN change requests), Boolean
        /// </summary>
        [XmlElement("PINCH")]
        public string PinChangeAllowed { get; set; }

        /// <summary>
        /// Y if server requires clients to change USERPASS as part of first signon. However, if MFACHALLENGEFIRST is also Y, this pin change request should be sent immediately after the session containing MFACHALLENGE authentication. Boolean
        /// </summary>
        [XmlElement("CHGPINFIRST")]
        public string PasswordChangeRequired { get; set; }

        /// <summary>
        /// Text prompt for user credential. If it is present, a third credential (USERCRED1) is required in addition to USERID and USERPASS. A-64
        /// </summary>
        [XmlElement("USERCRED1LABEL")]
        public string UserCredentialLabel1 { get; set; }

        /// <summary>
        /// Text prompt for user credential. If it is present, a fourth credential (USERCRED2) is required in addition to USERID, USERPASS and USERCRED1. If present, USERCRED1LABEL must also be present. A-64
        /// </summary>
        [XmlElement("USERCRED2LABEL")]
        public string UserCredentialLabel2 { get; set; }

        /// <summary>
        /// Y if CLIENTUID is required, Boolean
        /// </summary>
        [XmlElement("CLIENTUIDREQ")]
        public string ClientUidRequired { get; set; }

        /// <summary>
        /// Y if server requires clients to send AUTHTOKEN as part of the first signon, Boolean
        /// </summary>
        [XmlElement("AUTHTOKENFIRST")]
        public string AuthTokenRequired { get; set; }

        /// <summary>
        /// Text label for the AUTHTOKEN. Required if server supports AUTHTOKEN, A-64
        /// </summary>
        [XmlElement("AUTHTOKENLABEL")]
        public string AuthTokenLabel { get; set; }

        /// <summary>
        /// URL where AUTHTOKEN information is provided by the institution operating the OFX server. Required if server supports AUTHTOKEN, A-255
        /// </summary>
        [XmlElement("AUTHTOKENINFOURL")]
        public string AuthTokenInfoUrl { get; set; }

        /// <summary>
        /// Y if the server supports MFACHALLENGE functionality, Boolean
        /// </summary>
        [XmlElement("MFACHALLENGESUPT")]
        public string MFAChallengeSupported { get; set; }

        /// <summary>
        /// Y if the client is required to send MFACHALLENGERQ as part of the first signon, before sending any other requests, Boolean
        /// </summary>
        [XmlElement("MFACHALLENGEFIRST")]
        public string MFAChallengeRequired { get; set; }

    }

    public class ProfileMessageResponse : TransactionWrapper
    {
        [XmlElement("PROFRS")]
        public ProfileResponse OfxProfile { get; set; }
    }

    public class MessageSetCore
    {
        /// <summary>
        /// Version number of the message set, (for example, <VER>1 for version 1 of the message set), N-5
        /// </summary>
        [XmlElement("VER")]
        public string Version { get; set; }

        /// <summary>
        /// URL where messages in this set are to be sent, URL
        /// </summary>
        [XmlElement("URL")]
        public string Url { get; set; }

        /// <summary>
        /// Security level required for this message set;
        /// </summary>
        [XmlElement("OFXSEC")]
        public string SecurityLevel { get; set; }

        /// <summary>
        /// Y if transport-level security must be used, N if not used;
        /// </summary>
        [XmlElement("TRANSPSEC")]
        public string TransportLevelSecurity { get; set; }

        /// <summary>
        /// Signon realm to use with this message set, A-32
        /// </summary>
        [XmlElement("SIGNONREALM")]
        public string SignOnRealm { get; set; }

        /// <summary>
        /// Language supported, language.
        /// </summary>
        [XmlElement("LANGUAGE")]
        public string[] Language { get; set; }

        /// <summary>
        /// FULL for full synchronization capability
        /// LITE for lite synchronization capability
        /// </summary>
        [XmlElement("SYNCMODE")]
        public string SyncMode { get; set; }

        /// <summary>
        /// Y if server supports REFRESH within synchronizations
        /// </summary>
        [XmlElement("REFRESHSUPT")]
        public string RefreshSupported { get; set; }

        /// <summary>
        /// Y if server supports file-based error recovery
        /// </summary>
        [XmlElement("RESPFILEER")]
        public string FileBasedErrorRecoverySupported { get; set; }

        /// <summary>
        /// Service provider name
        /// </summary>
        [XmlElement("SPNAME")]
        public string SPNAME { get; set; }

        [XmlElement("INTU.TIMEOUT")]
        public string Timeout { get; set; }
    }

    public class SignOnMessageSetV1
    {
        [XmlElement("MSGSETCORE")]
        public MessageSetCore MessageSetCore { get; set; }
    }

    public class SignOnMessageSet
    {
        [XmlElement("SIGNONMSGSETV1")]
        public SignOnMessageSetV1 SignOnMessageSetV1 { get; set; }
    }

    public class ClientEnrollInfo
    {
        /// <summary>
        /// Y if account number is required as part of enrollment
        /// </summary>
        [XmlElement("ACCTREQUIRED")]
        public string AccountNumberRequired { get; set; }
    }

    public class OtherEnrollInfo
    {
        /// <summary>
        /// Message to consumer about what to do next (for example, a phone number)
        /// </summary>
        [XmlElement("MESSAGE")]
        public string Message { get; set; }
    }

    public class WebEnrollInfo
    {
        /// <summary>
        /// URL to start enrollment process
        /// </summary>
        [XmlElement("URL")]
        public string Url { get; set; }
    }

    // this is not the same as SignUpMessageSet
    public class SignUpMessageSet
    {
        [XmlElement("SIGNUPMSGSETV1")]
        public SignUpMessageSetV1 SignUpMessageSetV1 { get; set; }
    }

    // this is not the same as SignUpMessageSet
    public class SignUpMessageSetV1
    {
        [XmlElement("MSGSETCORE")]
        public MessageSetCore MessageSetCore { get; set; }

        /// <summary>
        /// Client-based enrollment supported
        /// </summary>
        [XmlElement("CLIENTENROLL")]
        public ClientEnrollInfo ClientEnrollInfo { get; set; }

        /// <summary>
        /// Some other enrollment process
        /// </summary>
        [XmlElement("OTHERENROLL")]
        public OtherEnrollInfo OtherEnrollInfo { get; set; }

        /// <summary>
        /// Web-based enrollment supported
        /// </summary>
        [XmlElement("WEBENROLL")]
        public WebEnrollInfo WebEnrollInfo { get; set; }

        /// <summary>
        /// Y if server supports client-based user information changes
        /// </summary>
        [XmlElement("CHGUSERINFO")]
        public string ChangeUserInfo { get; set; }

        /// <summary>
        /// Y if server can provide information on accounts with SVCSTATUS available, 
        /// N means client should expect to ask user for specific account information
        /// </summary>
        [XmlElement("AVAILACCTS")]
        public string AvailableAccounts { get; set; }

        /// <summary>
        /// Y if server allows clients to make service activation requests
        /// </summary>
        [XmlElement("CLIENTACTREQ")]
        public string ClientActivationAllowed { get; set; }
    }


    // this is not the same as SignUpMessageSet
    public class CreditCardMessageV1
    {
        [XmlElement("MSGSETCORE")]
        public MessageSetCore MessageSetCore { get; set; }

        /// <summary>
        /// Closing statement information available
        /// </summary>
        [XmlElement("CLOSINGAVAIL")]
        public string ClosingStatementInformationAvailable { get; set; }
    }

    // this is not the same as SignUpMessageSet
    public class CreditCardMessageSet
    {
        [XmlElement("CREDITCARDMSGSETV1")]
        public CreditCardMessageV1 CreditCardMessageV1 { get; set; }
    }

    public class ProfileMessageSetV1
    {
        [XmlElement("MSGSETCORE")]
        public MessageSetCore MessageSetCore { get; set; }
    }

    public class ProfileMessageSet
    {
        [XmlElement("PROFMSGSETV1")]
        public ProfileMessageSetV1 ProfileMessageSetV1 { get; set; }
    }

    public class EmailProfile
    {
        /// <summary>
        /// Supports generalized banking e-mail
        /// </summary>
        [XmlElement("CANEMAIL")]
        public string CanEmail { get; set; }

        /// <summary>
        /// Supports notification (of any kind)
        /// </summary>
        [XmlElement("CANNOTIFY")]
        public string CanNotify { get; set; }
    }

    public class BankMessageSetV1
    {
        [XmlElement("MSGSETCORE")]
        public MessageSetCore MessageSetCore { get; set; }

        [XmlElement("EMAILPROF")]
        public EmailProfile EmailProfile { get; set; }

        /// <summary>
        /// Closing statement information available
        /// </summary>
        [XmlElement("CLOSINGAVAIL")]
        public string ClosingStatementInformationAvailable { get; set; }
    }

    public class BankMessageSet
    {
        [XmlElement("BANKMSGSETV1")]
        public BankMessageSetV1 BankMessageSetV1 { get; set; }
    }

    public class MessageSetList
    {
        [XmlElement("SIGNONMSGSET")]
        public SignOnMessageSet SignOnMessageSet { get; set; }

        [XmlElement("SIGNUPMSGSET")]
        public SignUpMessageSet SignUpMessageSet { get; set; }

        [XmlElement("CREDITCARDMSGSET")]
        public CreditCardMessageSet CreditCardMessageSet { get; set; }

        [XmlElement("BANKMSGSET")]
        public BankMessageSet BankMessageSet { get; set; }

        [XmlElement("PROFMSGSET")]
        public ProfileMessageSet ProfileMessageSet { get; set; }
    }

    public class ProfileResponse
    {
        public ProfileResponse() { }

        [XmlElement("MSGSETLIST")]
        public MessageSetList MessageSetList { get; set; }

        [XmlElement("SIGNONINFOLIST")]
        public OfxSignOnInfoList OfxSignOnInfoList { get; set; }

        [XmlElement("DTPROFUP")]
        public string ProfileUpdateDate { get; set; }

        [XmlElement("FINAME")]
        public string FinancialInstitutionName { get; set; }

        [XmlElement("ADDR1")]
        public string Address1 { get; set; }

        [XmlElement("ADDR2")]
        public string Address2 { get; set; }

        [XmlElement("ADDR3")]
        public string Address3 { get; set; }

        [XmlElement("CITY")]
        public string City { get; set; }

        [XmlElement("STATE")]
        public string State { get; set; }

        [XmlElement("POSTALCODE")]
        public string PostalCode { get; set; }

        [XmlElement("COUNTRY")]
        public string Country { get; set; }

        /// <summary>
        /// Customer service telephone number,
        /// </summary>
        [XmlElement("CSPHONE")]
        public string CustomerServicePhone { get; set; }

        /// <summary>
        /// Technical support telephone number,
        /// </summary>
        [XmlElement("TSPHONE")]
        public string TechnicalSupportPhone { get; set; }

        /// <summary>
        /// Fax number
        /// </summary>
        [XmlElement("FAXPHONE")]
        public string FaxNumber { get; set; }

        /// <summary>
        /// URL for general information about FI
        /// </summary>
        [XmlElement("URL")]
        public string CompanyUrl { get; set; }

        /// <summary>
        /// E-mail address for FI
        /// </summary>
        [XmlElement("EMAIL")]
        public string Email { get; set; }
        
        /// <summary>
        /// Intuit extension
        /// </summary>
        [XmlElement("INTU.BROKERID")]
        public IntuitBrokerId IntuitBrokerId { get; set; }

    }

    
    //<INTU.BROKERID>
    //  dstsystems.com<ADDR1>816 Broadway</ADDR1><CITY>Kansas City</CITY><STATE>MO</STATE><POSTALCODE>64105</POSTALCODE><COUNTRY>USA</COUNTRY>
    //</INTU.BROKERID>
    public class IntuitBrokerId
    {
        [XmlText]
        public string Name { get; set; }

        [XmlElement("ADDR1")]
        public string Address1 { get; set; }

        [XmlElement("CITY")]
        public string City { get; set; }

        [XmlElement("STATE")]
        public string State { get; set; }

        [XmlElement("POSTALCODE")]
        public string PostalCode { get; set; }

        [XmlElement("COUNTRY")]
        public string Country { get; set; }
    }

    public class SignOnResponse
    {
        public SignOnResponse() { }

        [XmlElement("STATUS")]
        public OfxStatus OfxStatus { get; set; }

        [XmlElement("DTSERVER")]
        public string ServerDate { get; set; }

        [XmlElement("LANGUAGE")]
        public string Language { get; set; }

        [XmlElement("DTPROFUP")]
        public string ProfileUpdateDate { get; set; }

        [XmlElement("DTACCTUP")]
        public string AccountUpdateDate { get; set; }
        
        [XmlElement("USERKEY")]
        public string UserKey { get; set; }

        [XmlElement("TSKEYEXPIRE")]
        public string UserKeyExpireDate { get; set; }

        [XmlElement("FI")]
        public FinancialInstitution FinancialInstitution { get; set; }

        [XmlElement("SESSCOOKIE")]
        public string SessionCookie { get; set; }

        [XmlElement("ACCESSKEY")]
        public string AccessKey { get; set; }
    }

    public class FinancialInstitution
    {
        public FinancialInstitution() { }
        [XmlElement("ORG")]
        public string Organization { get; set; }

        [XmlElement("FID")]
        public string FID { get; set; }
    }

    public class OfxStatus
    {
        public OfxStatus() { }

        [XmlElement("CODE")]
        public int Code { get; set; }

        [XmlElement("SEVERITY")]
        public string Severity { get; set; }

        [XmlElement("MESSAGE")]
        public string Message { get; set; }
    }


    public class SignUpResponseMessageSet
    {
        public SignUpResponseMessageSet() { }

        [XmlElement("ACCTINFOTRNRS")]
        public AccountInfoSet AccountInfoSet { get; set; }
    }

    public class TransactionWrapper
    {
        [XmlElement("TRANUID")]
        public string TransactionId { get; set; }

        [XmlElement("STATUS")]
        public OfxStatus OfxStatus { get; set; }

        [XmlElement("CLTCOOKIE")]
        public string Cookie { get; set; }
    }

    public class AccountInfoSet : TransactionWrapper
    {
        public AccountInfoSet() { }

        [XmlArrayItem("ACCTINFO")]
        [XmlArray("ACCTINFORS")]
        public List<AccountInfoResponse> Accounts { get; set; }
    }

    public class AccountInfoResponse
    {
        public AccountInfoResponse() { }

        [XmlElement("DESC")]
        public string Description { get; set; }

        [XmlElement("BANKACCTINFO")]
        public BankAccountInfo BankAccountInfo { get; set; }

        [XmlElement("CCACCTINFO")]
        public CreditCardAccountInfo CreditCardAccountInfo { get; set; }

        [XmlElement("INVACCTINFO")]
        public InvestmentAccountInfo InvAccountInfo { get; set; }

    }

    public class BankAccountInfo
    {
        public BankAccountInfo() { }

        [XmlElement("BANKACCTFROM")]
        public BankAccountFrom BankAccountFrom { get; set; }

        [XmlElement("SUPTXDL")]
        public string SupportsDownload { get; set; }

        [XmlElement("XFERSRC")]
        public string TransferSourceEnabled { get; set; }

        [XmlElement("XFERDEST")]
        public string TransferDestinationEnabled { get; set; }

        [XmlElement("SVCSTATUS")]
        public string ActivationStatus { get; set; }
    }

    public class BankAccountFrom
    {
        public BankAccountFrom() { }

        [XmlElement("ACCTID")]
        public string AccountId { get; set; }

        [XmlElement("BANKID")]
        public string BankId { get; set; }

        [XmlElement("BRANCHID")]
        public string BranchId { get; set; }

        [XmlElement("ACCTTYPE")]
        public string AccountType { get; set; }
    }

    public class CreditCardAccountInfo
    {
        public CreditCardAccountInfo() { }

        [XmlElement("CCACCTFROM")]
        public CreditCardAccountFrom CreditCardAccountFrom { get; set; }

        [XmlElement("SUPTXDL")]
        public string SupportsDownload { get; set; }

        [XmlElement("XFERSRC")]
        public string TransferSourceEnabled { get; set; }

        [XmlElement("XFERDEST")]
        public string TransferDestinationEnabled { get; set; }

        [XmlElement("SVCSTATUS")]
        public string ActivationStatus { get; set; }
    }

    public class CreditCardAccountFrom
    {
        public CreditCardAccountFrom() { }

        [XmlElement("ACCTID")]
        public string AccountId { get; set; }
    }

    public class InvestmentAccountInfo
    {
        public InvestmentAccountInfo() { }

        [XmlElement("INVACCTFROM")]
        public InvestmentAccountFrom InvAccountFrom { get; set; }

        [XmlElement("USPRODUCTTYPE")]
        public string USProductType { get; set; }

        [XmlElement("CHECKING")]
        public string Checking { get; set; }

        [XmlElement("SVCSTATUS")]
        public string ActivationStatus { get; set; }

        [XmlElement("INVACCTTYPE")]
        public string AccountType { get; set; }

        [XmlElement("OPTIONLEVEL")]
        public string OptionLevel { get; set; }
    }

    public class InvestmentAccountFrom
    {
        public InvestmentAccountFrom() { }

        [XmlElement("BROKERID")]
        public string BrokerId { get; set; }

        [XmlElement("ACCTID")]
        public string AccountId { get; set; }
    }

    public class PinChangeResponseTransaction : TransactionWrapper
    {
        [XmlElement("PINCHRS")]
        public PinChangeResponse PinChangeResponse { get; set; }
    }

    public class PinChangeResponse
    {
        [XmlElement("USERID")]
        public string UserId { get; set; }

        [XmlElement("DTCHANGED")]
        public string DateChanged { get; set; }
    }


}
