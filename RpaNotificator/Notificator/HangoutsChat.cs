using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace RpaNotificator.Notificator
{
    class HangoutsChat
    {
        private string webhookUrl = "";

        public HangoutsChat(string webhookUrl)
        {
            this.webhookUrl = webhookUrl;
        }

        // Slack に Incoming WebHooks で投稿する
        // https://webbibouroku.com/Blog/Article/slack-incoming-webhooks
        public void Send(string msg)
        {
            // ポストするデータをJSON形式で作成
            var data = JsonConvert.SerializeObject(new
            {
                text = msg
            });

            // クライアントを設定
            var webClient = new WebClient();
            webClient.Headers.Add(HttpRequestHeader.ContentType, "application/json;charset=UTF-8");
            webClient.Encoding = Encoding.UTF8;

            // Webhook URL にデータをアップロード
            webClient.UploadString(this.webhookUrl, data);
        }
    }
}
