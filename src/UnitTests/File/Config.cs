﻿using DuetAPI.Commands;
using DuetControlServer.FileExecution;
using NUnit.Framework;
using System;
using System.IO;

namespace DuetUnitTest.File
{
    [TestFixture]
    public class Config
    {
        [Test]
        public void ProcessConfig()
        {
            string filePath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "File/GCodes/config.g");
            MacroFile macro = new MacroFile(filePath, DuetAPI.CodeChannel.Daemon, null);

            do
            {
                Code code = macro.ReadCode();
                Console.WriteLine(code);
            } while (!macro.IsFinished);

            // End
        }
    }
}
