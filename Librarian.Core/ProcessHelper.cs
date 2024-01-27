using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Librarian.Util;

namespace Librarian
{
    public static class ProcessHelper
    {
        public static async Task<(int ExitCode, string Output, string Error)> RunProcessAsync(string binary, params string[] arguments)
        {
            using Process process = new()
            {
                StartInfo = new ProcessStartInfo(binary)
                {
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                },
                EnableRaisingEvents = true,
            };
            arguments.ForEach(process.StartInfo.ArgumentList.Add);

            StringBuilder processOut = new();
            StringBuilder processErr = new();

            process.OutputDataReceived += (sender, args) => processOut.AppendLine(args.Data);
            process.ErrorDataReceived += (sender, args) => processErr.AppendLine(args.Data);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            return (process.ExitCode, processOut.ToString(), processErr.ToString());
        }
    }
}
