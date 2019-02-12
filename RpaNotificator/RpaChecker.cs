using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RpaNotificator
{
    class RpaChecker
    {
        private Form1 form1;
        private string logFileDir;
        private string _logFileName;
        private string logFileName
        {
            get
            {
                return DateTime.Now.ToString(_logFileName);
            }
            set
            {
                this._logFileName = value;
            }
        }
        private string webhookUrl;
        private bool normalReport;
        private bool errorReport;
        private int refreshInterval;
        private int logUpdateInterval;

        private bool fileNotFound = false;
        public static int traialsCount = 0;
        public static int errorsCount = 0;
        public static int missingsCount = 0;

        public RpaChecker(Form1 form1, string logFileDir, string logFileName, string webhookUrl, bool normalReport, bool errorReport, int refreshInterval, int logUpdateInterval)
        {
            this.form1 = form1;
            this.logFileDir = logFileDir;
            this.logFileName = logFileName;
            this.webhookUrl = webhookUrl;
            this.normalReport = normalReport;
            this.errorReport = errorReport;
            this.refreshInterval = refreshInterval;
            this.logUpdateInterval = logUpdateInterval;
        }

        public async void Run()
        {
            string filePath = Path.Combine(logFileDir, logFileName);
            traialsCount++;

            // TODO
            // ファイルがなかった場合の処理は後で書く
            if (!File.Exists(filePath))
                return;

            ///
            /// 更新日時の比較
            ///
            // 更新日時の取得
            DateTime lastUpdatedTime = File.GetLastWriteTime(filePath);
            // n（ログエラー判定間隔）分前の日時
            DateTime nMinutesAgo = DateTime.Now.AddMinutes(-this.logUpdateInterval);
            

            // n: RPAの更新間隔（min）
            // 最終更新から n 分以上経過している場合
            if (lastUpdatedTime <= nMinutesAgo)
            {
                missingsCount++;
                int diffMinutes = DiffTimesAsMinutes(lastUpdatedTime, nMinutesAgo);

                form1.AddLogFromAnotherThread($"【警告】{diffMinutes}分間ログが書き込まれていません");
                if (errorReport)
                {
                    string msg = GetWarningNotificationMsg(diffMinutes);
                    SendNotification(msg);
                }
            }
            else
            {
                string logs = GetLastLogs(1);
                Match match = Regex.Match(logs, @"\d{14}…エラー画面：(\d).0回閉じて、次へスキップ");

                if (!match.Success || match.Groups.Count < 2 || match.Groups[1].Value != "0")
                {
                    form1.AddLogFromAnotherThread("【エラー】書き込まれたログからエラーを検知しました");
                    errorsCount++;
                    if (errorReport)
                    {
                        string msg = GetErrorNotificationMsg();
                        SendNotification(msg);
                    }
                }
                else
                {
                    form1.AddLogFromAnotherThread("【正常】ログを確認しました");
                    if (normalReport)
                    {
                        string msg = GetSuccessNotificationMsg();
                        SendNotification(msg);
                    }
                }
            }
        }

        private int DiffTimesAsMinutes(DateTime beforeTime, DateTime afterTime)
        {
            TimeSpan diff = afterTime - beforeTime;
            return (int)diff.TotalMinutes;
        }

        private string GetSuccessNotificationMsg()
        {
            return $"{DateTime.Now.ToString("yyyy/MM/dd hh:mm")} ロボパットの正常稼働を確認しました。";
        }

        private string GetErrorNotificationMsg()
        {
            string logs = GetLastLogs(1);
            return $"【エラー】{DateTime.Now.ToString("yyyy/MM/dd hh:mm")}\r\n" +
                    "エラーを検知しました。" +
                    $"```{logs}```";
        }

        private string GetWarningNotificationMsg(int duration = 0)
        {
            string logs = GetLastLogs();
            return $"*【警告】*{DateTime.Now.ToString("yyyy/MM/dd hh:mm")}\r\n*" +
                    $"ログが{duration}分間書き込まれていない*ことを検出しました。\r\n" +
                    $"・BIツールにアクセスし、社内用の各ワークスペースの「データセット」更新時間を確認ください。" +
                    $"・BIツール更新が直近10分以内で更新されていない場合（10分以上更新が停止している場合）は、ロボパット2号機を再稼働ください。" +
                    $"```{logs}```";
        }

        public void SendTest(string msg)
        {
            SendNotification(msg);
        }

        public void SendFinalReport()
        {
            form1.AddLogFromAnotherThread("【レポート】最終レポートを送信");
            string msg = $"【{DateTime.Now.ToString("yyyy年MM月dd日")}　最終レポート】\r\n";
            if (!File.Exists(Path.Combine(logFileDir, logFileName)))
            {
                msg += "ログファイルを取得できませんでした。";
            }
            else if (traialsCount <= 0)
            {
                msg += "試行回数が0回のため取得できませんでした。";
            }
            else
            {
                List<int> processTimesSec = new List<int>();
                using (var fs = new FileStream(Path.Combine(logFileDir, logFileName), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var sr = new StreamReader(fs, Encoding.GetEncoding("SHIFT-JIS")))
                    {
                        while (sr.Peek() >= 0)
                        {
                            // ファイルを 1 行ずつ読み込む
                            string stBuffer = sr.ReadLine();

                            // 正規表現で時間抜き出し
                            Match match = Regex.Match(stBuffer, @"\d{4}\d{2}\d{2}\d{2}(\d{2})(\d{2})…エラー画面：(\d).0回閉じて、次へスキップ");

                            if (match.Success && match.Groups.Count == 4 || match.Groups[3].Value == "0")
                            {
                                var t = match.Groups;
                                try
                                {
                                    // 差分時間の取り出し
                                    int _min = int.Parse(t[1].Value);
                                    int _sec = int.Parse(t[2].Value);
                                    int _min1Digit = (_min - (int)(_min / 10) * 10);
                                    int minDiff = 0;

                                    if (_min1Digit >= 5)
                                    {
                                        minDiff = (_min1Digit - 5) * 60 + _sec;
                                    }
                                    else
                                    {
                                        minDiff = _min1Digit * 60 + _sec;
                                    }

                                    processTimesSec.Add(minDiff);
                                }
                                catch (ArgumentOutOfRangeException ignored)
                                {
                                    break;
                                }
                            }
                        }
                    }
                }

                double rate = 1.0 - ((double)errorsCount + (double)missingsCount) / (double)traialsCount;
                string rateMsg = "";

                if (rate >= 0.975)
                {
                    rateMsg = $"🎉🎉本日の稼働率は{rate * 100}%でした🎉🎉";
                }
                else if (rate > 0.8)
                {
                    rateMsg = $"😑😑本日の稼働率は{rate * 100}%でした😑😑";
                }
                else if (rate > 0.6)
                {
                    rateMsg = $"🤕😷本日の稼働率は{rate * 100}%でした😷🤕";
                }
                else
                {
                    rateMsg = $"😱👿本日の稼働率は{(rate * 100):F1}%でした👿😱";
                }

                msg += $"試行回数　：{traialsCount}\r\n" +
                       $"エラー検出：{errorsCount}\r\n" +
                       $"ログ未取得：{missingsCount}\r\n\r\n" +
                       $"平均処理時間：{SecondsToMinutes((int)processTimesSec.Average())}\r\n" +
                       $"最長処理時間：{SecondsToMinutes(processTimesSec.Max())}\r\n" +
                       $"最短処理時間：{SecondsToMinutes(processTimesSec.Min())}\r\n\r\n" +
                       rateMsg;
            }
            SendNotification(msg);
            ResetCount();
        }

        private string SecondsToMinutes(int sec)
        {
            if (sec < 60) return sec.ToString() + "秒";
            int _min = (int)(sec / 60);
            int _sec = sec - _min * 60;

            return $"{_min}分{_sec}秒";
        }

        public void ResetCount()
        {
            traialsCount = 0;
            errorsCount = 0;
            missingsCount = 0;
        }

        // 【.NET】テキストファイルの末尾からn行を読み込む
        // https://qiita.com/yaju/items/cdc261a7e228a914d754
        public string GetLastLogs(int lines = 3, string encoding = "SHIFT-JIS")
        {
            int BUFFER_SIZE = 32;       // バッファーサイズ(あえて小さく設定)
            int offset = 0;
            int loc = 0;
            int foundCount = 0;
            var buffer = new byte[BUFFER_SIZE];
            bool isFirst = true;
            bool isFound = false;

            // ファイル共有モードで開く
            using (var fs = new FileStream(Path.Combine(logFileDir, logFileName), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                // 検索ブロック位置の繰り返し
                for (int i = 0; ; i++)
                {
                    // ブロック開始位置に移動
                    offset = Math.Min((int)fs.Length, (i + 1) * BUFFER_SIZE);
                    loc = 0;
                    if (fs.Length <= i * BUFFER_SIZE)
                    {
                        // ファイルの先頭まで達した場合
                        if (foundCount > 0 || fs.Length > 0) break;

                        // 行が未存在
                        //throw new ArgumentOutOfRangeException("NOT FOUND DATA");
                        return "NOT FOUND LOG DATA";
                    }

                    fs.Seek(-offset, SeekOrigin.End);

                    // ブロックの読み込み
                    int readLength = offset - BUFFER_SIZE * i;
                    for (int j = 0; j < readLength; j += fs.Read(buffer, j, readLength - j)) ;

                    // ブロック内の改行コードの検索
                    for (int k = readLength - 1; k >= 0; k--)
                    {
                        if (buffer[k] == 0x0A)
                        {
                            if (isFirst && k == readLength - 1) continue;
                            if (++foundCount == lines)
                            {
                                // 所定の行数が見つかった場合
                                loc = k + 1;
                                isFound = true;
                                break;
                            }
                        }
                    }
                    isFirst = false;
                    if (isFound) break;
                }

                // 見つかった場合
                fs.Seek(-offset + loc, SeekOrigin.End);

                using (var sr = new StreamReader(fs, Encoding.GetEncoding(encoding)))
                {
                    return sr.ReadToEnd();
                }
            }
        }

        // Slack に Incoming WebHooks で投稿する
        // https://webbibouroku.com/Blog/Article/slack-incoming-webhooks
        private void SendNotification(string msg)
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
