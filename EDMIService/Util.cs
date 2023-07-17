using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReportApi
{
    class Util
    {
        public static void Logging(string function, string log)
        {
            string exe = Process.GetCurrentProcess().MainModule.FileName;
            string path = Path.GetDirectoryName(exe);

            if (function == "") { function = "EDMI API"; }
            string logFormat = DateTime.Now.ToString("yyyy-MMM-dd HH:mm:ss") + "    ";
            char spc = ' ';

            int lnt = 25 - function.Length;

            for (int i = 0; i < lnt; i++)
            {
                function += spc;
            }
            logFormat += function + log;

            System.IO.StreamWriter sw;
            if (Environment.UserInteractive) { Console.WriteLine(logFormat); }

            string Fpath = path + @"\" + DateTime.Now.ToString("yyyy-MM-dd") + "_log.txt";

            try
            {
                if (!System.IO.File.Exists(Fpath))
                {
                    System.IO.File.Create(Fpath);
                }

                sw = System.IO.File.AppendText(Fpath);
                sw.WriteLine(logFormat);
                sw.Close();
            }
            catch { }
        }
    }
}
