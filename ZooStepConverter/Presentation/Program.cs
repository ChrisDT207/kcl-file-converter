using System;
using System.IO;
using System.Net;
using System.Windows.Forms;
using ZooStepConverter.Presentation.Forms;

namespace ZooStepConverter.Presentation
{
    /// <summary>
    /// Presentation Layer: Application entry point.
    /// </summary>
    public static class Program
    {
        [STAThread]
        public static void Main(string[] cmdArgs)
        {
            // .NET Framework may default to old TLS; Zoo needs TLS 1.2+.
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string start = "";
            if (cmdArgs.Length > 0 && Directory.Exists(cmdArgs[0])) start = cmdArgs[0];
            Application.Run(new ConverterForm(start));
        }
    }
}

