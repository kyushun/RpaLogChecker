using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RpaNotificator.Notificator
{
    class HangoutsChat
    {
        private string THREAD_NAME_REGEX = "\"thread\":{\"name\":\"(.*?)\"}";
        private string webhookUrl = "";
        private static string pastThreadName = null;

        public HangoutsChat(string webhookUrl)
        {
            this.webhookUrl = webhookUrl;
        }

        // Slack に Incoming WebHooks で投稿する
        // https://webbibouroku.com/Blog/Article/slack-incoming-webhooks
        public void Send(string msg, bool newThread = true)
        {
            // ポストするデータをJSON形式で作成
            var data = JsonConvert.SerializeObject(new
            {
                text = msg,
                thread = new
                {
                    name = newThread ? null : pastThreadName
                }
            });

            // クライアントを設定
            var webClient = new WebClient();
            webClient.Headers.Add(HttpRequestHeader.ContentType, "application/json;charset=UTF-8");
            webClient.Encoding = Encoding.UTF8;

            // Webhook URL にデータをアップロード
            string response = webClient.UploadString(this.webhookUrl, data);
            response = response.Replace("\r", "").Replace("\n", "").Replace(" ", "");
            
            Match match = Regex.Match(response, THREAD_NAME_REGEX);
            if (match.Success && match.Groups.Count == 2)
            {
                pastThreadName = match.Groups[1].Value;
            }
            else
            {
                pastThreadName = null;
            }
        }
    }
}
