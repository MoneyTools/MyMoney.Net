using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using Walkabout.Interfaces.Reports;
using Walkabout.Reports;
using Walkabout.Utilities;
using Walkabout.Views;

namespace Walkabout.Setup
{
    internal class ChangeInfoFormatter : Report
    {
        private string previousVersion;
        private XDocument doc;
        private FlowDocumentView view;
        private bool installButton;

        public event EventHandler InstallButtonClick;

        private void OnInstallButtonClick()
        {
            if (InstallButtonClick != null)
            {
                InstallButtonClick(this, EventArgs.Empty);
            }
        }

        public ChangeInfoFormatter(FlowDocumentView view, bool addInstallButton, string previousVersion, XDocument doc)
        {
            this.previousVersion = previousVersion;
            this.doc = doc;
            this.view = view;
            this.installButton = addInstallButton;
        }

        /// <summary>
        /// return true if the given 'version' is the same or older than the 'previousVersion'
        /// </summary>
        /// <param name="previousVersion"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        public bool IsSameOrOlder(string previousVersion, string version)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(previousVersion))
                {
                    return false;
                }
                Version v1 = Version.Parse(version);
                Version v2 = Version.Parse(previousVersion);
                if (v1 <= v2)
                {
                    return true;
                }
            }
            catch
            {
                return version == previousVersion;
            }
            return false;
        }

        public bool HasLatestVersion()
        {
            if (this.doc == null)
            {
                return true;
            }

            foreach (XElement change in this.doc.Root.Elements("change"))
            {
                string version = (string)change.Attribute("version");
                if (version == this.previousVersion)
                {
                    return true;
                }
                return this.IsSameOrOlder(this.previousVersion, version);
            }
            return false;
        }

        public override Task Generate(IReportWriter writer)
        {
            if (this.doc == null)
            {
                return Task.CompletedTask;
            }

            var document = this.view.DocumentViewer.Document;

            bool found = false;
            bool first = true;

            foreach (XElement change in this.doc.Root.Elements("change"))
            {
                string version = (string)change.Attribute("version");
                if (string.IsNullOrEmpty(version))
                {
                    continue;
                }

                bool match = this.IsSameOrOlder(this.previousVersion, version);

                if (!found && match)
                {
                    writer.WriteHeading("The following changes were already installed");
                    found = true;
                }

                if (first && !found)
                {
                    writer.WriteHeading("The following changes are now available");
                    first = false;
                }

                string date = (string)change.Attribute("date");

                writer.WriteSubHeading(version + "    " + date);

                foreach (string line in change.Value.Split('\r', '\n'))
                {
                    string trimmed = line.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        string brush = found ? "ListItemForegroundBrush" : "ListItemSelectedForegroundBrush";
                        FlowDocumentReportWriter fwriter = (FlowDocumentReportWriter)writer;
                        Paragraph p = fwriter.WriteParagraph(trimmed, FontStyles.Normal, FontWeights.Normal,
                            AppTheme.Instance.GetThemedBrush(brush), null);
                        p.TextIndent = 20;
                        p.Margin = new Thickness(0);
                        p.Padding = new Thickness(0);
                    }
                }
            }

            if (this.installButton && !this.HasLatestVersion())
            {
                document.Blocks.InsertAfter(document.Blocks.FirstBlock, new BlockUIContainer(this.CreateInstallButton()));
            }
            return Task.CompletedTask;
        }

        private Button CreateInstallButton()
        {
            Button button = new Button();
            button.HorizontalAlignment = HorizontalAlignment.Left;
            button.Margin = new Thickness(10);
            button.ToolTip = "Click here to install the new version";

            StackPanel panel = new StackPanel()
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                Margin = new Thickness(2)
            };

            Image img = new Image();
            BitmapFrame frame = BitmapFrame.Create(new Uri("pack://application:,,,/MyMoney;component/Icons/setup.ico"));
            img.Source = frame;
            img.Width = frame.Width;
            img.Height = frame.Height;
            panel.Children.Add(img);

            panel.Children.Add(new TextBlock()
            {
                Text = "Install",
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            });

            button.Content = panel;
            button.Click += new RoutedEventHandler(this.OnInstallLatest);
            return button;
        }

        private void OnInstallLatest(object sender, RoutedEventArgs e)
        {
            this.OnInstallButtonClick();
        }

    }
}
