﻿using System;
using System.IO;

namespace Credfeto.Gallery.Storage;

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
        RotateWithRetry(current: file, file + ".1");
    }

    private static bool Rotate(string current, string previous)
    {
        Console.WriteLine(format: "Moving {0} to {1}", arg0: current, arg1: previous);

        if (!File.Exists(current))
        {
            return true;
        }

        FileHelpers.DeleteFile(previous);

        try
        {
            File.Move(sourceFileName: current, destFileName: previous);

            return true;
        }
        catch (Exception exception)
        {
            Console.WriteLine(format: "ERROR: Failed to move file (FAST): {0}", arg0: exception.Message);

            return SlowMove(current: current, previous: previous);
        }
    }

    private static void RotateWithRetry(string current, string previous)
    {
        const int maxRetries = 5;

        for (int retry = 0; retry < maxRetries; ++retry)
        {
            if (Rotate(current: current, previous: previous))
            {
                return;
            }
        }
    }

    private static bool SlowMove(string current, string previous)
    {
        try
        {
            File.Copy(sourceFileName: current, destFileName: previous);
            FileHelpers.DeleteFile(current);

            return true;
        }
        catch (Exception exception)
        {
            Console.WriteLine(format: "ERROR: Failed to move file (SLOW): {0}", arg0: exception.Message);

            return false;
        }
    }
}