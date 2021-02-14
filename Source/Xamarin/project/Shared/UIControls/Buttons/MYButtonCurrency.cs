using Xamarin.Forms;

namespace XMoney
{
    public class XButtonCurrency : XButton
    {
        private decimal value = 0;

        public XButtonCurrency()
        {
            BackgroundColor = Color.DarkGray;
            TextColor = Color.FromHex("#bbFFFFFF");
        }

        public decimal Value
        {
            get
            {
                return this.value;
            }

            set
            {
                this.value = value;
                this.Text = value.ToString("C");
                BackgroundColor = MyColors.GetCurrencyColor(this.value);
            }
        }
    }
}