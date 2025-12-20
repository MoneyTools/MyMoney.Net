using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Security.Policy;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Xml.Linq;
using Walkabout.Configuration;
using Walkabout.Controls;
using Walkabout.Data;
using Walkabout.Sgml;
using Walkabout.StockQuotes;
using Walkabout.Utilities;

namespace Walkabout.StockQuotes
{
    public enum CurrencyCode
    {
        AED, // United Arab Emirates Dirham
        AFN, // Afghan Afghani
        ALL, // Albanian Lek
        AMD, // Armenian Dram
        ANG, // Dutch Guilders
        AOA, // Angolan Kwanza
        ARS, // Argentine Peso
        AUD, // Australian Dollar
        AWG, // Aruban Florin
        AZN, // Azerbaijani Manat
        BAM, // Bosnia-Herzegovina Convertible Mark
        BBD, // Barbadian Dollar
        BDT, // Bangladeshi Taka
        BGN, // Bulgarian Lev
        BHD, // Bahraini Dinar
        BIF, // Burundian Franc
        BMD, // Bermudian Dollar
        BND, // Bruneian Dollar
        BOB, // Bolivian Boliviano
        BRL, // Brazilian Real
        BRX, // Brazilian PTAX
        BSD, // Bahamian Dollar
        BTN, // Bhutanese Ngultrum
        BWP, // Botswanan Pula
        BYN, // Belarusian Ruble
        BZD, // Belizean Dollar
        CAD, // Canadian Dollar
        CDF, // Congolese Franc
        CHF, // Swiss Franc
        CLF, // Chilean Unit of Account UF
        CLP, // Chilean Peso
        CNH, // Chinese Yuan Offshore
        CNY, // Chinese Yuan
        COP, // Colombian Peso
        COU, // Unidad de Valor Real (Colombia)
        CRC, // Costa Rican Colon
        CUP, // Cuban Peso
        CVE, // Cape Verdean Escudo
        CZK, // Czech Republic Koruna
        DJF, // Djiboutian Franc
        DKK, // Danish Krone
        DOP, // Dominican Peso
        DZD, // Algerian Dinar
        EGP, // Egyptian Pound
        ERN, // Eritrean Nakfa
        ETB, // Ethiopian Birr
        EUR, // Euro
        FJD, // Fijian Dollar
        FKP, // Falkland Islands Pound
        GBP, // British Pound Sterling
        GEL, // Georgian Lari
        GHS, // Ghanaian Cedi
        GIP, // Gibraltar Pound
        GMD, // Gambian Dalasi
        GNF, // Guinean Franc
        GTQ, // Guatemalan Quetzal
        GYD, // Guyanaese Dollar
        HKD, // Hong Kong Dollar
        HNL, // Honduran Lempira
        HRK, // Croatian Kuna
        HTG, // Haitian Gourde
        HUF, // Hungarian Forint
        HUX, // Hungarian Forint Official Rate
        IDR, // Indonesian Rupiah
        ILS, // Israeli New Sheqel
        INR, // Indian Rupee
        IQD, // Iraqi Dinar
        IRR, // Iranian Rial
        ISK, // Icelandic Krona
        JMD, // Jamaican Dollar
        JOD, // Jordanian Dinar
        JPY, // Japanese Yen
        KES, // Kenyan Shilling
        KGS, // Kyrgystani Som
        KHR, // Cambodian Riel
        KMF, // Comorian Franc
        KPW, // North Korean Won
        KRW, // South Korean Won
        KWD, // Kuwaiti Dinar
        KYD, // Caymanian Dollar
        KZT, // Kazakhstani Tenge
        LAK, // Laotian Kip
        LBP, // Lebanese Pound
        LKR, // Sri Lankan Rupee
        LRD, // Liberian Dollar
        LSL, // Lesotho Maloti
        LYD, // Libyan Dinar
        MAD, // Moroccan Dirham
        MDL, // Moldovan Leu
        MGA, // Malagasy Ariary
        MKD, // Macedonian Denar
        MMK, // Myanma Kyat
        MNT, // Mongolian Tugrik
        MOP, // Macanese Pataca
        MRU, // Mauritanian Ouguiya
        MUR, // Mauritian Rupee
        MVR, // Maldivian Rufiyaa
        MWK, // Malawian Kwacha
        MXN, // Mexican Peso
        MXV, // Mexican Unidad de Inversion
        MYR, // Malaysian Ringgit
        MZN, // Mozambican Metical
        NAD, // Namibian Dollar
        NGN, // Nigerian Naira
        NIO, // Nicaraguan Cordoba
        NOK, // Norwegian Krone
        NPR, // Nepalese Rupee
        NZD, // New Zealand Dollar
        OMR, // Omani Rial
        PAB, // Panamanian Balboa
        PEN, // Peruvian Nuevo Sol
        PGK, // Papua New Guinean Kina
        PHP, // Philippine Peso
        PKR, // Pakistani Rupee
        PLN, // Polish Zloty
        PYG, // Paraguayan Guarani
        QAR, // Qatari Rial
        RON, // Romanian Leu
        RSD, // Serbian Dinar
        RUB, // Russian Ruble
        RWF, // Rwandan Franc
        SAR, // Saudi Arabian Riyal
        SBD, // Solomon Islands Dollar
        SCR, // Seychellois Rupee
        SDG, // Sudanese Pound
        SEK, // Swedish Krona
        SGD, // Singapore Dollar
        SHP, // Saint Helena Pound
        SLL, // Sierra Leonean Leone
        SOS, // Somali Shilling
        SRD, // Surinamese Dollar
        SSP, // South Sudanese Pound
        STN, // Sao Tomean Dobra
        SVC, // Salvadoran Colon
        SYP, // Syrian Pound
        SZL, // Swazi Emalangeni
        THB, // Thai Baht
        TJS, // Tajikistani Somoni
        TMT, // Turkmenistani Manat
        TND, // Tunisian Dinar
        TOP, // Tongan Pa'anga
        TRY, // Turkish Lira
        TTD, // Trinidad and Tobago Dollar
        TWD, // Taiwan New Dollar
        TZS, // Tanzanian Shilling
        UAH, // Ukrainian Hryvnia
        UGX, // Ugandan Shilling
        USD, // United States Dollar
        UYU, // Uruguayan Peso
        UZS, // Uzbekistan Som
        VES, // Venezuelan Bolivar
        VND, // Vietnamese Dong
        VUV, // Ni-Vanuatu Vatu
        WST, // Samoan Tala
        XAF, // CFA Franc BEAC
        XCD, // East Caribbean Dollar
        XDR, // Special Drawing Rights
        XOF, // CFA Franc BCEAO
        XPF, // CFP Franc
        YER, // Yemeni Rial
        ZAR, // South African Rand
        ZMW, // Zambian Kwacha
    }

    public class ExchangeRateService : IDisposable
    {
        private static readonly string name = "fastforex.io";
        private static string endPoint = "https://api.fastforex.io/";
        private string query = "fetch-all?from=USD";
        private MyMoney myMoney;
        private CancellationTokenSource cancelSource = null;
        private Task downloadTask = null;
        private bool _firstError = true;
        private DateTime lastDownload = DateTime.MinValue;
        private Dictionary<string, decimal> ratesCache = new Dictionary<string, decimal>();
        OnlineServiceSettings settings;
        IServiceProvider provider;

        public ExchangeRateService(OnlineServiceSettings settings, string logPath, IServiceProvider provider)
        {
            this.settings = settings;
            this.provider = provider;
            if (string.IsNullOrEmpty(settings.ServiceType))
            {
                settings.ServiceType = "ExchangeRate";
            }
            settings.Address = endPoint; 
        }

        public static OnlineServiceSettings GetDefaultSettings()
        {
            return new OnlineServiceSettings()
            {
                Name = name,
                ServiceType = "ExchangeRate",
                Address = endPoint,
                ApiKey = "",
                ApiRequestsPerMinuteLimit = 0,
                ApiRequestsPerDayLimit = 0,
                ApiRequestsPerMonthLimit = 1000000
            };
        }

        public static bool IsMySettings(OnlineServiceSettings settings)
        {
            return settings.Name == name;
        }

        public void LogError(string msg)
        {
            if (this.provider != null)
            {
                Paragraph p = new Paragraph();
                p.Inlines.Add(msg);
                OutputPane output = (OutputPane)this.provider.GetService(typeof(OutputPane));
                output.AppendParagraph(p);
                if (this._firstError)
                {
                    this._firstError = false;
                    output.Show();
                }
            }
        }


        public void Dispose()
        {
            if (cancelSource != null)
            {
                cancelSource.Cancel();
                cancelSource = null;
            }
            downloadTask = null;
        }

        public MyMoney MyMoney
        {
            get { return this.myMoney; }
            set { this.myMoney = value; }
        }

        public bool NeedsUpdate         
        {
            get
            {
                return (DateTime.Now.Date - this.lastDownload).TotalDays >= 1;
            }
        }

        internal void UpdateRates()
        {
            if (this.myMoney != null)
            {
                var apiKey = this.settings.ApiKey?.Trim();
                if (this.downloadTask == null && !string.IsNullOrEmpty(apiKey))
                {
                    this.cancelSource = new CancellationTokenSource();
                    this.downloadTask = Task.Run(this.GetRates, cancelSource.Token);
                }
            }
        }

        private async Task GetRates()
        {
            var token = this.cancelSource.Token;            
            if (!token.IsCancellationRequested)
            {
                Exception error = null;
                try
                {
                    await this.GetExchangeRates(token);
                } 
                catch (Exception ex)
                {
                    error = ex;
                }
                UiDispatcher.Invoke(() =>
                {
                    this.UpdateCurrencyInfo();
                    if (error != null)
                    {
                        this.LogError("Error updating exchange rates from fastforex.io: " + error.Message);
                    }
                });
            }

            this.downloadTask = null;
        }

        private void UpdateCurrencyInfo()
        {
            lock (this.ratesCache)
            {
                foreach (var kvp in ratesCache)
                {
                    var code = kvp.Key;
                    var ratio = kvp.Value; // ratio to USD.
                    Currency found = this.myMoney.Currencies.FindCurrency(code);
                    if (found != null && ratio != 0)
                    {
                        found.Ratio = 1 / ratio;
                    }
                }
            }
        }

        public async Task<string> TestApiKeyAsync(OnlineServiceSettings settings)
        {
            try
            {
                this.settings = settings;
                var source = new CancellationTokenSource();
                await this.GetExchangeRates(source.Token);
            } 
            catch (Exception ex)
            {
                return ex.Message;
            }
            return null;
        }

        private async Task GetExchangeRates(CancellationToken token)
        {
            HttpClient client = new HttpClient();
            // Set http header X-API-Key to our api key
            var apiKey = this.settings.ApiKey?.Trim();
            client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
            var response = await client.GetAsync(this.settings.Address + this.query, token);            
            if (response != null)
            {
                if (response.IsSuccessStatusCode)
                {
                    this.lastDownload = DateTime.Now.Date;
                    var json = await response.Content.ReadAsStringAsync(token);
                    var result = Newtonsoft.Json.JsonConvert.DeserializeObject<FastForexResponse>(json);
                    if (result != null && result.results != null)
                    {
                        // USD to XXX
                        lock (ratesCache)
                        {
                            foreach (var kvp in result.results)
                            {
                                ratesCache[kvp.Key] = kvp.Value;
                            }
                        }
                    }
                } else
                {
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        /// <summary>
        /// Call this method when user edits a new currency to lookup the rate for it from our daily cache
        /// </summary>
        internal Currency CreateOrUpdate(string code)
        {
            if (this.ratesCache != null)
            {
                lock (this.ratesCache)
                {
                    if (this.ratesCache.TryGetValue(code, out decimal ratio))
                    {
                        Currency found = this.myMoney.Currencies.FindCurrency(code);
                        if (found == null)
                        {
                            found = new Currency();
                            found.Symbol = code.ToString();
                            this.myMoney.Currencies.AddCurrency(found);
                        }
                        if (ratio != 0)
                        {
                            found.Ratio = 1 / ratio;
                        }
                        return found;
                    }
                }
            }
            return null;
        }
    }

    internal class FastForexResponse
    {
        public string @base { get; set; }
        public Dictionary<string, decimal> results { get; set; }
        public string updated { get; set; }
        public long ms { get; set; }
    }
}
