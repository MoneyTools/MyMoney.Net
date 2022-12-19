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
            _mfaPhraseId = id;
            _mfaPhraseLabel = label;
            _mfaPhraseAnswer = answer;
        }

        public string PhraseId
        {
            get => _mfaPhraseId;
            set
            {
                if (value != _mfaPhraseId)
                {
                    _mfaPhraseId = value;
                    OnPropertyChanged("PhraseId");
                }
            }
        }

        public string PhraseLabel
        {
            get => _mfaPhraseLabel;
            set
            {
                if (value != _mfaPhraseLabel)
                {
                    _mfaPhraseLabel = value;
                    OnPropertyChanged("PhraseLabel");
                }
            }
        }

        public string PhraseAnswer
        {
            get => _mfaPhraseAnswer;
            set
            {
                if (value != _mfaPhraseAnswer)
                {
                    _mfaPhraseAnswer = value;
                    OnPropertyChanged("PhraseAnswer");
                }
            }
        }
    }
}
