using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Documents;
using Walkabout.Reports;
using Walkabout.Utilities;
using System.Xml.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using Walkabout.Controls;
using System.Windows;
using Walkabout.Views;
using Walkabout.Interfaces.Reports;
using System.Windows.Media.Imaging;

namespace Walkabout.Setup
{
    internal class ChangeInfoFormatter : IReport
    {
        string previousVersion;
        XDocument doc;
        FlowDocumentView view;
        bool installButton;

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
            if (doc == null) return true;
            foreach (XElement change in doc.Root.Elements("change"))
            {
                string version = (string)change.Attribute("version");
                if (version == previousVersion)
                {
                    return true;
                }
                return IsSameOrOlder(previousVersion, version);
            }
            return false;
        }

        public void Export(string filename)
        {
            throw new NotImplementedException();
        }

        public void Generate(IReportWriter writer) 
        {
            if (doc == null) return;

            var document = view.DocumentViewer.Document;

            bool found = false;
            bool first = true;
           
            foreach (XElement change in doc.Root.Elements("change"))
            {
                string version = (string)change.Attribute("version");
                if (string.IsNullOrEmpty(version))
                {
                    continue;
                }

                bool match = IsSameOrOlder(previousVersion, version);
                
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

            if (installButton && !HasLatestVersion())
            {
                document.Blocks.InsertAfter(document.Blocks.FirstBlock, new BlockUIContainer(CreateInstallButton()));
            }
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
            button.Click += new RoutedEventHandler(OnInstallLatest);
            return button;
        }

        void OnInstallLatest(object sender, RoutedEventArgs e)
        {
            OnInstallButtonClick();
        }

    }
}
