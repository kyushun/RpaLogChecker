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
        private static string LOG_REGEX = @"^(\d{4}/\d{2}/\d{2} \d{6}) 読込csv破損：(\d+?)\..*? - BIエラー画面表示：(\d+?)\..*?$";

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
        private int logUpdateInterval;

        private Notificator.HangoutsChat chat;
        private enum RPA_STATUS
        {
            SUCCESS,
            MISSING,
            ERROR
        }
        private static RPA_STATUS status = RPA_STATUS.SUCCESS;
        private bool IsFailed
        {
            get { return (status != RPA_STATUS.SUCCESS); }
        }
        private bool IsStatusChanged(RPA_STATUS newStatus)
        {
            return (status != newStatus);
        }
        public static int traialsCount = 0;
        public static int errorsCount = 0;
        public static int missingsCount = 0;

        public RpaChecker(Form1 form1, string logFileDir, string logFileName, string webhookUrl, bool normalReport, bool errorReport, int logUpdateInterval)
        {
            this.form1 = form1;
            this.logFileDir = logFileDir;
            this.logFileName = logFileName;
            this.webhookUrl = webhookUrl;
            this.normalReport = normalReport;
            this.errorReport = errorReport;
            this.logUpdateInterval = logUpdateInterval;

            chat = new Notificator.HangoutsChat(webhookUrl);
        }

        public async void Run()
        {
            string filePath = Path.Combine(logFileDir, logFileName);
            traialsCount++;

            // TODO
            // ファイルがなかった場合の処理は後で書く
            if (!File.Exists(filePath))
                return;
            
            DateTime lastUpdatedTime = File.GetLastWriteTime(filePath);
            DateTime nMinutesAgo = DateTime.Now.AddMinutes(-this.logUpdateInterval);

            string logs = GetLastLogs(1);
            bool hasError = false;

            Match match = Regex.Match(logs, LOG_REGEX);
            if (match.Success && match.Groups.Count > 2)
            {
                for (int i = 2; i < match.Groups.Count; i++)
                {
                    if (match.Groups[i].Value != "0")
                    {
                        hasError = true;
                        break;
                    }
                }
            }
            else
            {
                hasError = true;
            }


            if (hasError)
            {
                form1.AddLogFromAnotherThread("【エラー】書き込まれたログからエラーを検知しました");
                errorsCount++;
                RPA_STATUS newStatus = RPA_STATUS.ERROR;

                if (errorReport && IsStatusChanged(newStatus))
                {
                    MessageBuilder mb = new MessageBuilder(0, GetLastLogs(3));
                    chat.Send(mb.GetMessage(MessageBuilder.ReportLevel.ERROR), !IsFailed);
                }
                status = newStatus;
            }
            else
            {
                if (lastUpdatedTime <= nMinutesAgo)
                {
                    missingsCount++;
                    RPA_STATUS newStatus = RPA_STATUS.MISSING;

                    int diffMinutes = DiffTimesAsMinutes(lastUpdatedTime, nMinutesAgo);

                    form1.AddLogFromAnotherThread($"【警告】{diffMinutes}分間ログが書き込まれていません");
                    if (errorReport && IsStatusChanged(newStatus))
                    {
                        MessageBuilder mb = new MessageBuilder(diffMinutes, GetLastLogs(3));
                        chat.Send(mb.GetMessage(MessageBuilder.ReportLevel.MISSING), !IsFailed);
                    }
                    status = newStatus;
                }
                else
                {
                    if (IsFailed)
                    {
                        form1.AddLogFromAnotherThread("【復旧】復旧を確認しました");
                        if (errorReport)
                        {
                            MessageBuilder mb = new MessageBuilder(0, GetLastLogs(3));
                            chat.Send(mb.GetMessage(MessageBuilder.ReportLevel.RESTORING), false);
                        }
                    }
                    else
                    {
                        form1.AddLogFromAnotherThread("【正常】ログを確認しました");
                        if (normalReport)
                        {
                            MessageBuilder mb = new MessageBuilder(0, GetLastLogs(3));
                            chat.Send(mb.GetMessage(MessageBuilder.ReportLevel.SUCCESS));
                        }
                    }
                    status = RPA_STATUS.SUCCESS;
                }
            }            
        }

        private int DiffTimesAsMinutes(DateTime beforeTime, DateTime afterTime)
        {
            TimeSpan diff = afterTime - beforeTime;
            return (int)diff.TotalMinutes;
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
                            string stBuffer = sr.ReadLine();
                            
                            Match match = Regex.Match(stBuffer, LOG_REGEX);

                            if (match.Success && match.Groups.Count == 4)
                            {
                                var t = match.Groups;
                                try
                                {
                                    DateTime dt = DateTime.ParseExact(t[1].Value,
                                                    "yyyy/MM/dd HHmmss",
                                                    System.Globalization.DateTimeFormatInfo.InvariantInfo,
                                                    System.Globalization.DateTimeStyles.None);
                                    int _min = dt.Minute;
                                    int _sec = dt.Second;
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
                    rateMsg = $"🎉🎉本日の稼働率は{(rate * 100):F1}%でした🎉🎉";
                }
                else if (rate > 0.8)
                {
                    rateMsg = $"😑😑本日の稼働率は{(rate * 100):F1}%でした😑😑";
                }
                else if (rate > 0.6)
                {
                    rateMsg = $"🤕😷本日の稼働率は{(rate * 100):F1}%でした😷🤕";
                }
                else
                {
                    rateMsg = $"😱👿本日の稼働率は{(rate * 100):F1}%でした👿😱";
                }

                msg += $"試行回数　：{traialsCount}\r\n" +
                       $"エラー検出：{errorsCount}\r\n" +
                       $"ログ未取得：{missingsCount}\r\n\r\n" +
                       $"平均処理時間：{SecondsToMinutes((int)processTimesSec.Average())}\r\n" +
                       $"（Max：{SecondsToMinutes(processTimesSec.Max())}　Min：{SecondsToMinutes(processTimesSec.Min())}）\r\n\r\n" +
                       rateMsg;
            }
            chat.Send(msg);
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
            status = RPA_STATUS.SUCCESS;
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
    }
}
