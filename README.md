
MyMoney.Net is a rich client .NET 4.0 application for managing your personal finances. It is
written entirely in C# and is designed for programmers who want easy access to their data and who
want to quickly and easily add their own features. Your data will not be locked up in some
proprietary format, it is yours to do with as you like.

## Install

You can simply click the [ClickOnce
Installer](http://lovettsoftware.com/Downloads/MyMoney/publish.htm) to install the app, it requires
.NET 4.0.

You can also install using the new [MSIX
Installer](http://lovettsoftware.com/Downloads/MyMoney.Net/index.html) for Windows 10.

## Help

Learn  how to use the app by reading our [Helpful Wiki](https://github.com/clovett/MyMoney.Net/wiki).

![](https://github.com/clovett/MyMoney.Net/wiki/Images/Home1.png)

## Licenses

This program is provided with [MIT license](https://opensource.org/licenses/MIT). This program uses
System.Windows.Controls.DataVisualization.Charting which is (c) Copyright Microsoft Corporation and
is subject to the Microsoft Public License (Ms-PL). This program optionally uses Stock Quote
Services from [AlphaVantage](https://www.alphavantage.co/) and [IEX
Trading](https://iextrading.com/) which are subject to their respective licenses. It also uses
Currency Rates from
[restfulwebservices.net](http://www.restfulwebservices.net/ServiceContracts/2008/01/ICurrencyService/GetCo).
It uses online Banking information (OFX) from [http://www.ofxhome.com/](http://www.ofxhome.com/)
and implements the [OFX 1.0 and 2.0 specifications](http://www.ofx.net/). It uses
[SQLite](http://sqlite.org/copyright.html) to store your financial transactions in a local file on
your computer. It also has an implementation of the [Canny edge
detection](https://en.wikipedia.org/wiki/Canny_edge_detector) algorithm used in getting quick image
boundaries from your scanner, especially useful for receipts. It also contains a documentation
generator that uses the [Microsoft OneNote API](http://dev.onenote.com/) to generate HTML and from
HTML it generates Markdown format for the github Wiki.

Lastly the app is built entirely in C#, using .NET 4.0 and Visual Studio, and WPF. UnitTests are written and executed from a DGML test Model using
[DgmlTestModeling](http://www.lovettsoftware.com/Downloads/DgmlTestModel/Readme.htm).

--Enjoy!
