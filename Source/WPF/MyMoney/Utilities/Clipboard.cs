
namespace Walkabout.Utilities
{
    public interface IClipboardClient
    {
        bool CanCut { get; }
        void Cut();

        bool CanCopy { get; }
        void Copy();

        bool CanPaste { get; }
        void Paste();

        bool CanDelete { get; }
        void Delete();
    }

}
