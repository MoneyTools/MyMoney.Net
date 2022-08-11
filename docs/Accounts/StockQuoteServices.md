# Stock Quote Services

Daily stock prices can be downloaded from a configured stock quote service.  Select **Stock Quote Account**  from the Online Menu to configure your service.
When this information is downloaded you will see full stock quote information available in the [Stock Chart](../Charts/StockChart.md).


![](../Images/Stock%20Quote%20Services.png)

MyMoney can download from **iexcloud.io**  and/or from **alphavantage.co** .  Both of these services require a registered account and will provide an API Key which you enter here.  Depending on your plan you will also have limits on the number of requests per minute or per day or per month.  Enter those numbers here and MyMoney will ensure those limits are not exceeded.  Just leave the value at 0 if there is no limit.  MyMoney supports using both services at once since iexcloud.io is best at downloading a bunch of stock quotes and alphavantage.co is best at downloading history.

During the download you will see some progress information in the status bar on the bottom right.  If the download is blocked on the quota limits this progress bar may pause for a moment.  If you reach daily or monthly limits the download will print an error message explaining why the stock quotes are not updating right now.

[https://iexcloud.io/](https://iexcloud.io/)
To setup a free account on iexcloud.io, click their Get Started button, create a new account and select the "Start" account which is free up to 500,000 messages per month, which should be plenty.  MyMoney will only make one call for each 100 stocks you own each time you launch it.  You will get a verification email and when it arrives click the link which will take you back to iexcloud.io.  Click the "[API Tokens](https://iexcloud.io/console/tokens)" tab and you will see two tokens, a Secret and a Publishable one.  Save these in a safe place, and copy the "publishable" token to the API Key field in the MyMoney Stock Quote Service Dialog.

[https://www.alphavantage.co/](https://www.alphavantage.co/)
Click "Get Your Free API Key Today" button right on the home page, and enter your name and email, then click "get free api key".  The same page will give you a message saying "Your API key is: " with a number that looks like this: "RTY28311JXWBEYOP" and so copy that to the API key field in the MyMoney Stock Quote Service Dialog.  This account has a limit of 5 API requests per minute and MyMoney will send one request per stock that you own, so it will hit this limit quickly.  MyMoney will pause for a minute and continue.  If you want to unblock faster downloads, you can go to a [https://www.alphavantage.co/premium/](https://www.alphavantage.co/premium/) membership.

AlphaVantage.co supports downloading 20 year stock price history and MyMoney will use this feature to fill in any missing UnitPrice information.  These stock quote histories are stored in the "**StockQuote** " folder next to your money database in an easy to use XML format.

### Cache

The stock quote information is cached in a folder named `StockQuotes`
next to your *.mmdb money database file.  There is also a special
`DownloadLog.xml` index file in this folder which keeps tabs on how
recently the stock quotes were updated and so on.  These cached files
are used to efficiently compute the historical market value for your
[investment accounts](InvestmentAccounts.md).




