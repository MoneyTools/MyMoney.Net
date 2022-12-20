using System.ComponentModel;

namespace OfxTestServer
{
    public class MFAChallenge : INotifyPropertyChanged
    {
        private string _mfaPhraseId;
        private string _mfaPhraseLabel;
        private string _mfaPhraseAnswer;

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        public MFAChallenge(string id, string label, string answer)
        {
            this._mfaPhraseId = id;
            this._mfaPhraseLabel = label;
            this._mfaPhraseAnswer = answer;
        }

        public string PhraseId
        {
            get => this._mfaPhraseId;
            set
            {
                if (value != this._mfaPhraseId)
                {
                    this._mfaPhraseId = value;
                    this.OnPropertyChanged("PhraseId");
                }
            }
        }

        public string PhraseLabel
        {
            get => this._mfaPhraseLabel;
            set
            {
                if (value != this._mfaPhraseLabel)
                {
                    this._mfaPhraseLabel = value;
                    this.OnPropertyChanged("PhraseLabel");
                }
            }
        }

        public string PhraseAnswer
        {
            get => this._mfaPhraseAnswer;
            set
            {
                if (value != this._mfaPhraseAnswer)
                {
                    this._mfaPhraseAnswer = value;
                    this.OnPropertyChanged("PhraseAnswer");
                }
            }
        }
    }
}
