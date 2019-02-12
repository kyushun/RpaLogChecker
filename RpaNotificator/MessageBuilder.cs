using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RpaNotificator
{
    class MessageBuilder
    {
        private static readonly string REPORT_MESSAGE_CONFIG = Directory.GetParent(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\ReportMessageConfig.txt";
        private static readonly string CONFIG_COMMENT_LINE = "//";
        private static readonly string CONFIG_LEVEL_FLAG = "#";
        private static readonly string CONFIG_END_LINE = "===";

        private int errorDuration = 0;
        private string log = "";

        public enum ReportLevel
        {
            SUCCESS,
            MISSING,
            ERROR
        }

        public MessageBuilder(int errorDuration = 0, string log = "")
        {
            this.errorDuration = errorDuration;
            this.log = log;
        }

        public string GetMessage(ReportLevel level)
        {
            string msg = GetReportMessageByFile(level.ToString());

            msg = msg.Replace("{time}", DateTime.Now.ToString("yyyy/MM/dd HH:mm"));
            msg = msg.Replace("{duration}", errorDuration.ToString());
            msg = msg.Replace("{log}", log);

            return msg;
        }

        private string GetReportMessageByFile(string level)
        {
            bool foundStartLine = false;
            string message = "";

            using (var fs = new FileStream(REPORT_MESSAGE_CONFIG, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var reader = new StreamReader(fs, Encoding.Default))
                {
                    while (reader.Peek() > -1)
                    {
                        string line = reader.ReadLine();

                        if (line.StartsWith(CONFIG_COMMENT_LINE))
                            continue;

                        if (line.Equals(CONFIG_LEVEL_FLAG + level))
                        {
                            foundStartLine = true;
                            continue;
                        }

                        if (foundStartLine)
                        {
                            if (line.Equals(CONFIG_END_LINE)) break;

                            message += line + Environment.NewLine;
                        }
                    }
                }
            }

            return message;
        }
    }
}
