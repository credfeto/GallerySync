using System;
using Alphaleonis.Win32.Filesystem;

namespace StorageHelpers
{
    public static class ExtensionMethods
    {
        public static void RotateLastGenerations(string file)
        {
            FileHelpers.DeleteFile(file + ".9");
            RotateWithRetry(file + ".8", file + ".9");
            RotateWithRetry(file + ".7", file + ".8");
            RotateWithRetry(file + ".6", file + ".7");
            RotateWithRetry(file + ".5", file + ".6");
            RotateWithRetry(file + ".4", file + ".5");
            RotateWithRetry(file + ".3", file + ".4");
            RotateWithRetry(file + ".2", file + ".3");
            RotateWithRetry(file + ".1", file + ".2");
            RotateWithRetry(file + ".0", file + ".1");
            RotateWithRetry(file, file + ".1");
        }


        private static bool Rotate(string current, string previous)
        {
            Console.WriteLine("Moving {0} to {1}", current, previous);
            if (!File.Exists(current))
                return true;

            FileHelpers.DeleteFile(previous);

            try
            {
                File.Move(current, previous);
                return true;
            }
            catch (Exception exception)
            {
                Console.WriteLine("ERROR: Failed to move file (FAST): {0}", exception.Message);
                return SlowMove(current, previous);
            }
        }

        private static void RotateWithRetry(string current, string previous)
        {
            const int maxRetries = 5;

            for (var retry = 0; retry < maxRetries; ++retry)
                if (Rotate(current, previous))
                    return;
        }

        private static bool SlowMove(string current, string previous)
        {
            try
            {
                File.Copy(current, previous);
                FileHelpers.DeleteFile(current);
                return true;
            }
            catch (Exception exception)
            {
                Console.WriteLine("ERROR: Failed to move file (SLOW): {0}", exception.Message);
                return false;
            }
        }
    }
}