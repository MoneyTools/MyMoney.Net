using System;
using System.IO;


namespace Walkabout.Utilities
{

    public static class Sounds
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2211:NonConstantFieldsShouldNotBeVisible")]
        public static bool PlaySounds = true;

        public static void PlaySound(string name)
        {
            if (PlaySounds)
            {

                const int SND_FILENAME = 0x20000;
                const int SND_ASYNC = 0x1;

                string path = Path.Combine(Path.Combine(Path.GetTempPath(), "MyMoney"), name);
                if (!File.Exists(path))
                {
                    // extract the resource
                    if (!ProcessHelper.ExtractEmbeddedResourceAsFile(name, path))
                    {
                        return;
                    }
                }

                NativeMethods.PlaySound(path, IntPtr.Zero, SND_FILENAME | SND_ASYNC);
            }
        }



    }
}
