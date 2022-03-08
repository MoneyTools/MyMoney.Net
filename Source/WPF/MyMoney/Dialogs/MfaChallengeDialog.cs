using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Xml.Linq;
using Walkabout.Data;
using Walkabout.Ofx;
using Walkabout.Utilities;

namespace Walkabout.Dialogs
{
    public class MfaChallengeDialog : PasswordWindow
    {
        public MfaChallengeDialog()
        {
            PasswordPrompt = null; // remove default field.
            this.Title = "Multi-Factor Authentication";

            // add instructions 
            RichTextBox box = this.IntroMessagePrompt;
            box.Document.Blocks.Clear();

            Paragraph p = new Paragraph();
            p.Inlines.Add(new Run("The following information is needed in addition to your user name and password " +
                "to help your financial institution ensure that your account information is not being accessed by an unauthorized user."));
            box.Document.Blocks.Add(p);

            box.Visibility = System.Windows.Visibility.Visible;
        }

        public void SetupQuestions(List<MfaChallenge> challenges)
        {
            foreach (MfaChallenge c in challenges)
            {
                this.AddUserDefinedField(c.PhraseId, GetPhrasePrompt(c));
            }
        }

        public void GetAnswers(List<MfaChallenge> challenges, List<MfaChallengeAnswer> answers)
        {
            foreach (MfaChallenge c in challenges)
            {
                string answer = this.GetUserDefinedField(c.PhraseId);
                answers.Add(new MfaChallengeAnswer() { Id = c.PhraseId, Answer = answer });
            }
        }

        XDocument phraseTable;

        private XDocument GetPhraseTable()
        {
            if (phraseTable == null)
            {
                phraseTable = ProcessHelper.GetEmbeddedResourceAsXml("Walkabout.Ofx.MfaPhrases.xml");
            }
            return phraseTable;
        }

        private string GetPhrasePrompt(MfaChallenge c)
        {
            if (!string.IsNullOrEmpty(c.PhraseLabel))
            {
                string[] words = c.PhraseLabel.Split(new char[] { ' ', '\r', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                return string.Join(" ", words);
            }

            // label-less phrase, hopefully we have the id right here.
            XDocument table = GetPhraseTable();
            foreach (XElement row in table.Root.Elements())
            {
                if ((string)row.Attribute("Id") == c.PhraseId)
                {
                    return (string)row.Attribute("Label");
                }
            }

            // we'll show this to the user so they can call the bank and find out what they
            // are supposed to respond with here...
            return "Your bank is asking an unknown question with id='" + c.PhraseId + "'";
        }


    }
}
