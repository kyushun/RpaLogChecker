using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RpaNotificator
{
    static class Program
    {
        /// <summary>
        /// アプリケーションのメイン エントリ ポイントです。
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                Application.Run(new Form1());
            }
            catch(Exception ex)
            {
                Notificator.HangoutsChat chat = new Notificator.HangoutsChat(GetConfigValue("WebhookUrl"));
                chat.Send("RPA動作チェッカーがエラーを発生しました\r\n\r\n" + ex.ToString());
                MessageBox.Show(ex.ToString());
            }
        }

        public static string GetConfigValue(string key, string defaultValue = null)
        {
            return System.Configuration.ConfigurationManager.AppSettings[key] ?? defaultValue;
        }
    }
}
