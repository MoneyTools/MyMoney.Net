using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using Walkabout.Data;
using Walkabout.Interfaces.Reports;
using Walkabout.Reports;
using Walkabout.Utilities;
using Walkabout.Views;

namespace Walkabout.Setup
{
    internal class ChangeInfoReport
        : Report
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

        public ChangeInfoReport()
        {
        }

        ~ChangeInfoReport()
        {
            Debug.WriteLine("ChangeInfoReport disposed!");
        }


        public string PreviousVersion
        {
            get => previousVersion; 
            set => previousVersion = value;
        }

        public bool InstallButton
        {
            get => installButton;
            set => installButton = value;
        }
        public XDocument Document
        {
            get => doc;
            set => doc = value;
        }

        public override void OnSiteChanged()
        {
            this.view = (FlowDocumentView)this.ServiceProvider.GetService(typeof(FlowDocumentView));
        }

        public class ChangeInfoReportState : IReportState
        {
            public string PreviousVersion { get; set; }
            public bool InstallButton { get; set; }
            public XDocument Document { get; set; }
            public ChangeInfoReportState() { }

            public Type GetReportType() {  
                return typeof(ChangeInfoReport); 
            }
        }

        public override IReportState GetState()
        {
            return new ChangeInfoReportState()
            {
                PreviousVersion = this.previousVersion,
                InstallButton = this.installButton,
                Document = this.doc
            };
        }

        public override void ApplyState(IReportState state)
        {
            if (state is ChangeInfoReportState cs)
            {
                this.previousVersion = cs.PreviousVersion;
                this.installButton = cs.InstallButton;
                this.doc = cs.Document;
            }
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
                        if (writer is FlowDocumentReportWriter fwriter)
                        {
                            string brush = found ? "ListItemForegroundBrush" : "ListItemSelectedForegroundBrush";
                            Paragraph p = fwriter.WriteParagraph(trimmed, FontStyles.Normal, FontWeights.Normal,
                                AppTheme.Instance.GetThemedBrush(brush), null);
                            p.TextIndent = 20;
                            p.Margin = new Thickness(0);
                            p.Padding = new Thickness(0);
                        }
                        else
                        {
                            writer.WriteParagraph(trimmed);
                        }
                    }
                }
            }

            if (this.installButton && !this.HasLatestVersion() && writer is FlowDocumentReportWriter)
            {
                var document = this.view.DocumentViewer.Document;
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
