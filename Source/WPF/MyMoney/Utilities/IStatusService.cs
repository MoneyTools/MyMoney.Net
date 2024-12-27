
namespace Walkabout.Utilities
{
    public interface IStatusService
    {
        void ShowOutput(string text);

        void ShowMessage(string text);

        void ShowProgress(int min, int max, int value);

        void ShowProgress(string message, int min, int max, int value);

    }
}
