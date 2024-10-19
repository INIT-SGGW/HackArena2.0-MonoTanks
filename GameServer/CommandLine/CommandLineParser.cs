﻿using System.Reflection;
using System.Text.RegularExpressions;
using CommandLine;
using CommandLine.Text;
using Serilog.Core;

namespace GameServer;

/// <summary>
/// Represents the command line parser.
/// </summary>
internal static partial class CommandLineParser
{
    private static Logger log = default!;

    /// <summary>
    /// Parses the command line arguments.
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    /// <param name="log">The log.</param>
    /// <returns>
    /// The parsed options or <see langword="null"/> if the options are invalid.
    /// </returns>
    public static CommandLineOptions? Parse(string[] args, Logger log)
    {
        CommandLineParser.log = log;

        var parser = new Parser(with =>
        {
            with.HelpWriter = null;
        });

        var parserResult = parser.ParseArguments<CommandLineOptions>(args);

        bool areOptionsValid = true;
        _ = parserResult.WithParsed((opts) =>
        {
#if DEBUG
            if (!opts.SkipValidation && !RunOptions(opts))
#else
            if (!RunOptions(opts))
#endif
            {
                areOptionsValid = false;
                var helpText = GenerateHelpText(parserResult);
                Console.WriteLine(helpText);
            }
        });

        if (!areOptionsValid)
        {
            return null;
        }

        _ = parserResult.WithNotParsed(
            errs =>
            {
                foreach (var err in errs)
                {
                    log.Error("{err}", err);
                }

                var helpText = GenerateHelpText(parserResult);
                Console.WriteLine(helpText);
            });

        return parserResult.Value;
    }

    [GeneratedRegex(@"^\*$|^localhost$|^host.docker.internal$|^(\d{1,3}\.){3}\d{1,3}$")]
    private static partial Regex HostRegex();

    private static bool RunOptions(CommandLineOptions opts)
    {
        if (!opts.SkipHostRegexValidation && !HostRegex().IsMatch(opts.Host))
        {
            log.Error("Invalid host. Must be a valid IP address or 'localhost'.");
            return false;
        }

        if (opts.Port is < 1 or > 65535)
        {
            log.Error("Invalid port. Must be between 1 and 65535.");
            return false;
        }

        if (!opts.SandboxMode && opts.NumberOfPlayers is < 2 or > 4)
        {
            log.Error("Invalid number of players. Must be between 2 and 4.");
            return false;
        }

        if (opts.BroadcastInterval <= 0)
        {
            log.Error("Invalid broadcast interval. Must be at least 1.");
            return false;
        }

        if (opts.SandboxMode && opts.SaveReplay)
        {
            log.Error("Cannot save replay in sandbox mode.");
            return false;
        }

        if (!opts.SaveReplay && opts.ReplayFilepath is not null)
        {
            log.Warning(
                "Argument \"--replay-filepath\" provided without \"-r\" or \"--save-replay\", ignoring.");
        }

        if (!opts.SaveReplay && opts.OverwriteReplayFile)
        {
            log.Warning(
                "Argument \"--overwrite-record-file\" provided without \"-r\" or \"--save-replay\", ignoring.");
        }

        if (opts.SaveReplay && opts.ReplayFilepath is not null)
        {
            string replayPath = Path.GetFullPath(opts.ReplayFilepath);

            if (!opts.OverwriteReplayFile && File.Exists(replayPath))
            {
                log.Error($"Record file '{replayPath}' already exists. Use --overwrite-record-file to overwrite.");
                return false;
            }

#if HACKATHON
            var pathWithoutExtension = Path.GetFileNameWithoutExtension(replayPath);
            if (pathWithoutExtension.ToLower().EndsWith("_results"))
            {
                log.Error("The record file cannot end with '_results'.");
                return false;
            }
#endif

            try
            {
                _ = Directory.CreateDirectory(Path.GetDirectoryName(replayPath)!);
            }
            catch (Exception ex)
            {
                log.Error("Failed to create directory for record file '{replayPath}': {message}", replayPath, ex.Message);
                return false;
            }

            try
            {
                File.Create(replayPath).Dispose();
            }
            catch (Exception ex)
            {
                log.Error("Failed to create record file '{replayPath}': {message}", replayPath, ex.Message);
                return false;
            }
        }

        return true;
    }

    private static HelpText GenerateHelpText(ParserResult<CommandLineOptions> parserResult)
    {
        return HelpText.AutoBuild(
            parserResult,
            h =>
            {
                h.AutoVersion = false;

                h.Heading = Assembly
                    .GetEntryAssembly()!
                    .GetCustomAttribute<AssemblyTitleAttribute>()?
                    .Title;

                h.Copyright = "Copyright 2024" + Assembly
                    .GetEntryAssembly()!
                    .GetCustomAttribute<AssemblyCompanyAttribute>()?
                    .Company;

                var descrption = Assembly
                    .GetEntryAssembly()!
                    .GetCustomAttribute<AssemblyDescriptionAttribute>()?
                    .Description;
                _ = h.AddPreOptionsLine($"\n{descrption}");

                var assemblyName = Assembly
                    .GetEntryAssembly()!
                    .GetName()
                    .Name;
                _ = h.AddPostOptionsLine($"Usage: {assemblyName}.exe -- [options]");

                return h;
            },
            e => e);
    }
}
