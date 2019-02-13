using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RpaNotificator
{
    static class Program
    {
        public static ArgumentMap args;
        /// <summary>
        /// アプリケーションのメイン エントリ ポイントです。
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Program.args = new ArgumentMap();
            Program.args.Init(args);

            try
            {
                Application.Run(new Form1());
            }
            catch(Exception ex)
            {
                int count = int.Parse(Program.args.GetOption("-r", "0"));
                if (count < 3)
                {
                    Notificator.HangoutsChat chat = new Notificator.HangoutsChat(GetConfigValue("WebhookUrl"));
                    MessageBuilder mb = new MessageBuilder(0, ex.ToString());
                    chat.Send(mb.GetMessage(MessageBuilder.ReportLevel.APPLICATION_ERROR));
                    System.Threading.Thread.Sleep(3000);
                    Restart(++count);
                }
                else
                {
                    Notificator.HangoutsChat chat = new Notificator.HangoutsChat(GetConfigValue("WebhookUrl"));
                    MessageBuilder mb = new MessageBuilder(0, ex.ToString());
                    chat.Send(mb.GetMessage(MessageBuilder.ReportLevel.APPLICATION_RESTARTING_MISSED));
                }
            }
        }

        public static string GetConfigValue(string key, string defaultValue = null)
        {
            return System.Configuration.ConfigurationManager.AppSettings[key] ?? defaultValue;
        }

        public static void Restart(int count)
        {
            ProcessStartInfo startInfo = Process.GetCurrentProcess().StartInfo;
            startInfo.FileName = Application.ExecutablePath;
            startInfo.Arguments = " -a -r " + count;
            Process.Start(startInfo);

            var exit = typeof(Application).GetMethod("ExitInternal",
                                System.Reflection.BindingFlags.NonPublic |
                                System.Reflection.BindingFlags.Static);
            exit.Invoke(null, null);
        }
    }
}
