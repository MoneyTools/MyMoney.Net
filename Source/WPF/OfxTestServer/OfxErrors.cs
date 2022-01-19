using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OfxTestServer
{
    public enum OfxErrors
    {
        None = 0,
        ClientUptoDate = 1, //  Based on the client timestamp, the client has the latest information. The response does not supply any additional information.
        GeneralError = 2000, //  Error other than those specified by the remaining error codes.
        InvalidAccount = 2001,
        GeneralAccountError = 2002, //  Account error not specified by the remaining error codes
        AccountNotFound = 2003, //  The specified account number does not correspond to one of the user’s accounts.
        AccountClosed = 2004, //  The specified account number corresponds to an account that has been closed.
        AccountNotAuthorized = 2005, //  The user is not authorized to perform this action on the account, or the server does not allow this type of action to be performed on the account
        SourceAccountNotFound = 2006, //  The specified account number does not correspond to one of the user’s accounts.
        SourceAccountClosed = 2007, //  The specified account number corresponds to an account that has been closed
        SourceAccountNnotAuthorized = 2008, //  The user is not authorized to perform this action on the account, or the server does not allow this type of action to be performed on the account
        DestinationAccountNotFound = 2009, //  The specified account number does not correspond to one of the user’s accounts
        DestinationAccountClosed = 2010, //  The specified account number corresponds to an account that has been closed
        DestinationAccountNotAuthorized = 2011, //  The user is not authorized to perform this action on the account, or the server does not allow this type of action to be performed on the account
        InvalidAmount = 2012, //  The specified amount is not valid for this action; for example, the user specified a negative payment amount
        DateTooSoon = 2014, //  The server cannot process the requested action by the date specified by the user
        DateTooFarInFuture = 2015, //  The server cannot accept requests for an action that far in the future
        TransactionAlreadyCommitted = 2016, //  Transaction has entered the processing loop and cannot be modified/cancelled using OFX. The transaction may still be cancelled or modified using other means (for example, a phone call to Customer Service).
        AlreadyCanceled = 2017, //  The transaction cannot be canceled or modified because it has already been canceled.
        UnknownServerId = 2018, //  The specified server ID does not exist or no longer exists
        DuplicateRequest = 2019, //  A request with this <TRNUID> has already been received and processed
        InvalidDate = 2020, //  The specified datetime stamp cannot be parsed; for instance, the datetime stamp specifies 25:00 hours
        UnsupportedVersion = 2021, //  The server does not support the requested version. The version of the message set specified by the client is not supported by this server.
        InvalidTan = 2022, //  The server was unable to validate the TAN sent in the request
        UnknownFITID = 2023, //  The specified FITID/BILLID does not exist or no longer exists
        BranchIdMissing = 2025, //  A <BRANCHID> value must be provided in the <BANKACCTFROM> aggregate for this country system, but this field is missing
        BankNameDoesntMatchBank = 2026, //  The value of <BANKNAME> in the <EXTBANKACCTTO> aggregate is inconsistent with the value of <BANKID> in the <BANKACCTTO> aggregate.
        InvalidateDateRange = 2027, //  Response for non-overlapping dates, date ranges in the future, et cetera
        RequestedElementUnknown = 2028, //  One or more elements of the request were not recognized by the server or the server (as noted in the FI Profile) does not support the elements. The server executed the element transactions it understood and supported. For example, the request file included private tags in a <PMTRQ> but the server was able to execute the rest of the request.
        MFAChallengeAuthenticationRequired = 3000, //  User credentials are correct, but further authentication required. Client should send <MFACHALLENGERQ> in next request.
        MFAChallengeInformationIsInvalid = 3001, //  User or client information sent in MFACHALLENGEA contains invalid information
        RejectIfMissingInvalidWithoutToken = 6500, //  This error code may appear in the <SYNCERROR> element of an <xxxSYNCRS> wrapper (in <PRESDLVMSGSRSV1> and V2 message set responses) or the <CODE> contained in any embedded transaction wrappers within a sync response. The corresponding sync request wrapper included <REJECTIFMISSING>Y with <REFRESH>Y or <TOKENONLY>Y, which is illegal
        EmbeddedTransactionsInRequestFailed = 6501, //  <REJECTIFMISSING>Y and embedded transactions appeared in the request sync wrapper and the provided <TOKEN> was out of date. This code should be used in the <SYNCERROR> of the response sync wrapper.
        UnableToProcessEmbeddedTransactionDueToOutOfDate = 6502, //  Used in response transaction wrapper for embedded transactions when <SYNCERROR>6501 appears in the surrounding sync wrapper.
        StopCheckInProcess = 10000, //  Stop check is already in process
        TooManyChecksToProcess = 10500, //  The stop-payment request <STPCHKRQ> specifies too many checks
        InvalidPayee = 10501, //  Payee error not specified by the remaining error codes
        InvalidPayeeAddress = 10502, //  Some portion of the payee’s address is incorrect or unknown
        InvalidPayeeAccountNumber = 10503, //  The account number <PAYACCT> of the requested payee is invalid        
        InsufficientFunds = 10504, //  The server cannot process the request because the specified account does not have enough funds.
        CannotModifyElement = 10505, //  The server does not allow modifications to one or more values in a modification request.
        CannotModifySourceAccount = 10506, //  Reserved for future use.
        CannotModifyDestinationAccount = 10507, //  Reserved for future use.
        InvalidFrequency = 10508, //  The specified frequency <FREQ> does not match one of the accepted frequencies for recurring transactions.
        ModelAlreadyCanceled = 10509, //  The server has already canceled the specified recurring model.
        InvalidPayeeID = 10510, //  The specified payee ID does not exist or no longer exists.
        InvalidPayeeCity = 10511, //  The specified city is incorrect or unknown.
        InvalidPayeeState = 10512 , //  The specified state is incorrect or unknown.
        InvalidPayeePostalCode = 10513, //  The specified postal code is incorrect or unknown.
        TransactionAlreadyProcessed = 10514, //  Transaction has already been sent or date due is past
        PayeeNotModifiableByClient = 10515, //  The server does not allow clients to change payee information.
        WireBeneficiaryInvalid = 10516, //  The specified wire beneficiary does not exist or no longer exists.
        InvalidPayeeName = 10517, //  The server does not recognize the specified payee name.
        UnknownModelID = 10518, //  The specified model ID does not exist or no longer exists.
        InvalidPayeeListID = 10519, //  The specified payee list ID does not exist or no longer exists.
        TableTypeNotFound = 10600, //  The specified table type is not recognized or does not exist.
        InvestmentTransactionDownloadNotSupported = 12250, //  The server does not support investment transaction download.
        InvestmentPositionDownloadNotSupported = 12251, //  The server does not support investment position download.
        InvestmentPositionsForSpecifiedDateNotAvailable = 12252, //  The server does not support investment positions for the specified date.
        InvestmentOpenOrderDownloadNotSupported = 12253, //  The server does not support open order download.
        InvestmentBalancesDownloadNotSupported = 12254, //  The server does not support investment balances download.
        Error401kNotAvailableForThisAccount = 12255, //  (ERROR) 401(k) information requested from a non-401(k) account.
        OneOrMoreSecuritiesNotFound = 12500, //  (ERROR) The server could not find the requested securities.
        UserIDAndPasswordWillBeSentOutOfBand = 13000, //  (INFO) The server will send the user ID and password via postal mail, e-mail, or another means. The accompanying message will provide details.
        UnableToEnrollUser = 13500, //  (ERROR) The server could not enroll the user.
        UserAlreadyEnrolled = 13501, //  (ERROR) The server has already enrolled the user.
        InvalidService = 13502, //  (ERROR) The server does not support the service <SVC> specified in the service-activation request.
        CannotChangeUserInformation = 13503, //  (ERROR) The server does not support the <CHGUSERINFORQ> request.
        FIMissingOrInvalidInSONRQ = 13504, //  (ERROR) The FI requires the client to provide the <FI> aggregate in the <SONRQ> request, but either none was provided, or the one provided was invalid.
        Form1099NotAvailable = 14500, //  (ERROR) 1099 forms are not yet available for the tax year requested.
        Form1099NotAvailableForUserID = 14501, //  (ERROR) This user does not have any 1099 forms available.
        W2formsNotAvailable = 14600, //  (ERROR) W2 forms are not yet available for the tax year requested.
        W2FormsNotAvailableForUserID = 14601, //  (ERROR) The user does not have any W2 forms available.
        Form1098NotAvailable = 14700, //  (ERROR) 1098 forms are not yet available for the tax year requested.
        Form1098NotAvailableForUserID = 14701, //  (ERROR) The user does not have any 1098 forms available.
        MustChangeUSERPASS = 15000, //  (INFO) The user must change his or her <USERPASS> number as part of the next OFX request.
        SignonInvalid = 15500, //  (ERROR) The user cannot signon because he or she entered an invalid user ID or password.
        CustomerAccountAlreadyInUse = 15501, //  (ERROR) The server allows only one connection at a time, and another user is already signed on. Please try again later.
        UserPasslockout = 15502, //  (ERROR) The server has received too many failed signon attempts for this user. Please call the FI’s technical support number.
        CouldNotChangeUSERPASS = 15503, //  (ERROR) The server does not support the <PINCHRQ> request.
        CouldNotProvideRandomData = 15504, //  (ERROR) The server could not generate random data as requested by the <CHALLENGERQ>.
        CountrySystemNotSupported = 15505, //  (ERROR) The server does not support the country specified in the <COUNTRY> field of the <SONRQ> aggregate.
        EmptySignonNotSupported = 15506, //  (ERROR) The server does not support signons not accompanied by some other transaction.
        SignonInvalidWithoutSupportingPinChangeRequest = 15507, //  (ERROR) The OFX block associated with the signon does not contain a pin change request and should.
        TransactionNotAuthorized = 15508, //  (ERROR) Current user is not authorized to perform this action on behalf of the <USERID>.
        CLIENTUIDError = 15510, //  (ERROR) The CLIENTUID sent by the client was incorrect. User must register the Client UID.
        MFAError = 15511, //  (ERROR) User should contact financial institution.
        AUTHTOKENRequired = 15512, //  (ERROR) User needs to contact financial institution to obtain AUTHTOKEN. Client should send it in the next request.
        AUTHTOKENInvalid = 15513, //  (ERROR) The AUTHTOKEN sent by the client was invalid.
        HTMLNotAllowed = 16500, //  (ERROR) The server does not accept HTML formatting in the request.
        UnknownMailTo = 16501, //  (ERROR) The server was unable to send mail to the specified Internet address.
        InvalidURL = 16502, //  (ERROR) The server could not parse the URL.
        UnableToGetURL = 16503, //  (ERROR) The server was unable to retrieve the information at this URL (e.g., an HTTP 400 or 500 series error).
    }
}
