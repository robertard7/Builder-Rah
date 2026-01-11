#nullable enable
using System;
using System.IO;
using RahCli;

namespace RahBuilder.Tests.Helpers;

public static class CliRunner
{
    public static (int ExitCode, string StdOut, string StdErr) Run(params string[] args)
    {
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();
        Console.SetOut(outWriter);
        Console.SetError(errWriter);
        try
        {
            var code = Program.Main(args);
            return (code, outWriter.ToString(), errWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }
}
