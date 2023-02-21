using System.Windows.Automation;

namespace Walkabout.Tests.Wrappers
{
    internal class CurrencyViewWrapper : GridViewWrapper
    {
        public CurrencyViewWrapper(MainWindowWrapper window, AutomationElement control) : base(window, control)
        {
            this.Columns = new GridViewColumnWrappers(new[]
            {
                new GridViewColumnWrapper("Symbol", "Symbol", "ComboBox"),
                new GridViewColumnWrapper("Name", "Name", "ComboBox"),
                new GridViewColumnWrapper("Culture Code", "CultureCode", "ComboBox"),
                new GridViewColumnWrapper("Ratio", "Ratio", "TextBox"),
                new GridViewColumnWrapper("LastRatio", "LastRatio", "TextBox")
            });
        }

        public bool HasCurrencies
        {
            get
            {
                foreach (AutomationElement e in this.Control.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem)))
                {
                    if (e.Current.Name == "Walkabout.Data.Currency")
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public override GridViewRowWrapper WrapRow(AutomationElement e, int index)
        {
            return new CurrencyViewRowWrapper(this, e, index);
        }
    }

    internal class CurrencyViewRowWrapper : GridViewRowWrapper
    {
        public CurrencyViewRowWrapper(GridViewWrapper grid, AutomationElement e, int index) : base(grid, e, index)
        {
        }

        internal string GetSymbol()
        {
            var cell = this.GetCell("Symbol");
            return cell.GetValue();
        }

        internal void SetSymbol(string symbol)
        {
            var cell = this.GetCell("Symbol");
            cell.SetValue(symbol);
        }

        internal string GetName()
        {
            var cell = this.GetCell("Name");
            return cell.GetValue();
        }

        internal void SetName(string symbol)
        {
            var cell = this.GetCell("Name");
            cell.SetValue(symbol);
        }

        internal string GetCultureCode()
        {
            var cell = this.GetCell("CultureCode");
            return cell.GetValue();
        }

        internal void SetCultureCode(string code)
        {
            var cell = this.GetCell("CultureCode");
            cell.SetValue(code);
        }

        internal decimal GetRatio()
        {
            return this.GetDecimalColumn("Ratio");
        }

        internal void SetRatio(decimal value)
        {
            this.SetDecimalColumn("Ratio", value);
        }

    }
}
