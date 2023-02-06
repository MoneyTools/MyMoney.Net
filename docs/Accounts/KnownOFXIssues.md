# Known OFX Issues

If you have trouble getting Online Banking working with your bank you may be able to correct the problem by simply opening the Online Account properties and re-connecting.

You can also sometimes find updated OFX connection information here:
[https://wiki.gnucash.org/wiki/OFX_Direct_Connect_Bank_Settings](https://wiki.gnucash.org/wiki/OFX_Direct_Connect_Bank_Settings)

The following known errors are being returned from 74 OFX addresses.  The first 22 are because MyMoney does not yet have an implementation of the OFX TYPE1 security protocol.  Once this work is completed, we should be able to talk to these banks. 

The next 15 are refusing to connect over SSL, which indicates something wrong with the verification of the SSL certificate chain.  This might be resolvable with some work on installing OFX certificates.

The 10 "Bad Request" responses might be resolvable if we can figure out what kind of OFX request those banks really support.  Sending your OFX logs may help debug this.

The remaining errors from 14 banks may indicate those banks don't really support OFX, or have shut down their OFX servers.

**22: Did not get the expected response from your financial institution. Your bank requires** **security level 'TYPE1'** **which is not supported.**

    Achieva Credit Union
    Affinity Plus Federal Credit Union
    Canyon State Credit Union
    Credit Union 1
    Credit Union ONE
    Denver Community Federal Credit Union
    FAA Credit Union
    Hudson Valley FCU
    IBM Southeast Employees Federal Credit Union
    Insight CU
    JSC Federal Credit Union
    Mercer
    Missoula Federal Credit Union
    Morgan Keegan &Co, Inc.
    Robert W. Baird &Co.
    Sacramento Credit Union
    T. Rowe Price Retirement Plans
    UBS Financial Services Inc.
    Unity One Federal Credit Union
    USAgencies Credit Union
    Velocity Credit Union
    Whatcom Educational CU

**15: The underlying connection was closed: Could not establish trust relationship for the** **SSL/TLS secure channel** **.**

    121 Financial Credit Union
    Amplify Federal Credit Union
    Continental Federal Credit Union
    CPM Federal Credit Union
    Elevations CU
    Hawthorne Credit Union
    Navy Army Federal Credit Union
    Picatinny Federal Credit Union
    Yakima Valley Credit Union
    Bank One
    Chase
    First USA
    FirstBank of Colorado
    Northern Trust – Banking
    Northern Trust – Investments

The result of these errors are harder to debug because the error message is so non-descript.  Some of the errors may indicate that the bank actually does not talk OFX at all, or they have shut down that gateway, like the “Bank of America (California)” case below.

**10: The remote server returned an error: (400) Bad Request.**

    Abbott Laboratories Employee CU
    Andrews Federal Credit Union
    Century Federal Credit Union
    Charlotte State Bank – DC
    Fremont Bank
    Janney Montgomery Scott LLC
    Las Colinas FCU
    Prudential Retirement
    Sterne Agee
    T. Rowe Price

**3: The remote server returned an error: (404) Not Found.**

    Air Academy FCU
    Allegiance Community Bank
    Citadel Federal Credit Union

**2: Error parsing OFX response**

    American Express Brokerage
    Educational Employees CU

**1: Bank of America has completed a planned systems update for accounts opened in California. Please visit** [www.bankofamerica.com/Financialmanagement](http://www.bankofamerica.com/Financialmanagement)** for detailed instructions on how to update your settings and avoid service interruptions.**

    Bank of America (California)

**1: The remote server returned an error: (406) Not Acceptable.**

    Bank of Stockton – OLD

**2: General Error**

    BB& Banking and Bill Payment
    Xceed Financial Credit Union

**2: The remote server returned an error: (500) Internal Server Error.**

    Capitol Federal Savings Bank
    Technology Credit Union

**4: The underlying connection was closed: An unexpected error occurred on a send.**

    Dominion Credit Union
    Fort Stewart Georgia Federal Credit Union
    Silver State Schools CU
    Southeastern CU

**3: The remote server returned an error: (503) Server Unavailable.**

    First Clearing, LLC
    Wachovia Bank
    Wachovia Sec-Wells Fargo Advisors

**3: Error returned from server**

    First National Bank of St. Louis
    Florida Gulf Bank
    ING Direct

**1: We're sorry for the inconvenience. This function is currently not available. Try the function again or call us at 1-888-464-3232 to speak with a Direct Associate. (Error Code: INVALID_PERSONAL_CUSTOMER) (27-Oct-2012 23:11)**

    ING DIRECT Canada

**2: The operation has timed out**

    Premier Bank Rochester
    Private Bank of Buckhead

**1: The underlying connection was closed: The connection was closed unexpectedly.**

    PSECU

**1: The server does not support the PROFMSGSRQV1 request, or a request was made against an invalid organization. Please verify the Organization name and FID and try again. [ref: d43f244c-291c-486d-8d96-8f291689d6ae]**

    Simmons First National Bank

**2: The remote server returned an error: (501) Not Implemented.**

    The Bank of Miami, N.A
    Western National Bank

**1: This server does not support the requested InstitutionID**

    TIB Bank
