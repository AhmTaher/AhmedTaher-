﻿using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using GitCredentialManager.UI.Commands;
using GitCredentialManager.UI.Controls;

namespace GitCredentialManager.UI
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            // Set the session id (sid) for the helper process, to be
            // used when TRACE2 tracing is enabled.
            ProcessManager.CreateSid();
            using (var context = new CommandContext())
            using (var app = new HelperApplication(context))
            {
                // Initialize TRACE2 system
                context.Trace2.Initialize(DateTimeOffset.UtcNow);

                context.Trace2.Start(context.ApplicationPath, args, Thread.CurrentThread.Name);

                // Write the start and version events
                if (args.Length == 0)
                {
                    await Gui.ShowWindow(() => new TesterWindow(), IntPtr.Zero);
                    return;
                }

                app.RegisterCommand(new CredentialsCommandImpl(context));
                app.RegisterCommand(new OAuthCommandImpl(context));
                app.RegisterCommand(new DeviceCodeCommandImpl(context));

                int exitCode = app.RunAsync(args)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();

                context.Trace2.Stop(exitCode, Thread.CurrentThread.Name);
                Environment.Exit(exitCode);
            }
        }
    }
}
