﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Bonsai.IO
{
    [DefaultProperty("FileName")]
    [Description("Starts a new system process with the specified file name and optional command-line arguments.")]
    public class StartProcess : Source<int>
    {
        [Description("The name of the application to start.")]
        [FileNameFilter("Executable files|*.exe|All Files|*.*")]
        [Editor("Bonsai.Design.OpenFileNameEditor, Bonsai.Design", "System.Drawing.Design.UITypeEditor, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
        public string FileName { get; set; }

        [Description("The optional set of command-line arguments to use when starting the application.")]
        public string Arguments { get; set; }

        public IObservable<int> Generate<TSource>(IObservable<TSource> source)
        {
            return source.SelectMany(input => Generate());
        }

        public override IObservable<int> Generate()
        {
            return Observable.StartAsync(cancellationToken =>
            {
                return Task.Factory.StartNew(() =>
                {
                    using (var process = new Process())
                    using (var exitSignal = new ManualResetEvent(false))
                    {
                        process.StartInfo.FileName = FileName;
                        process.StartInfo.Arguments = Arguments;
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.RedirectStandardError = true;
                        process.StartInfo.RedirectStandardOutput = true;
                        process.ErrorDataReceived += (sender, e) => Console.Error.WriteLine(e.Data);
                        process.OutputDataReceived += (sender, e) => Console.WriteLine(e.Data);
                        process.Exited += (sender, e) => exitSignal.Set();
                        process.EnableRaisingEvents = true;
                        process.Start();
                        process.BeginErrorReadLine();
                        process.BeginOutputReadLine();
                        using (var cancellation = cancellationToken.Register(() => exitSignal.Set()))
                        {
                            exitSignal.WaitOne();
                            return process.ExitCode;
                        }
                    }
                },
                cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
            });
        }
    }
}