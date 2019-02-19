using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RpaNotificator
{
    class AppConfig
    {
        [Newtonsoft.Json.JsonIgnore]
        private static readonly string CONFIG_FILE_PATH = Directory.GetParent(Assembly.GetExecutingAssembly().Location) + @"\config.json";
        public string LogFileDirectory { get; set; } = "";
        public string LogFileName { get; set; } = "";
        public string WebhookUrl { get; set; } = "";
        public TimeSchedule[] Schedules { get; set; }

        public class TimeSchedule
        {
            public DateTime StartTime { get; set; } = DateTime.Now;
            public DateTime EndTime { get; set; } = DateTime.Now;
            public int RefreshInterval { get; set; } = 1;
            public int ErrorJudgementInterval { get; set; } = 1;
            public bool SuccessReport { get; set; } = false;
            public bool ErrorReport { get; set; } = false;
        }

        public static AppConfig Load()
        {
            string json = "";
            try
            {
                using (StreamReader sr = new StreamReader(CONFIG_FILE_PATH, Encoding.UTF8))
                {
                    json = sr.ReadToEnd();
                }
            }
            catch(Exception ex)
            {
                throw new FileNotFoundException(ex.ToString());
            }

            AppConfig config;
            try
            {
                config = JsonConvert.DeserializeObject<AppConfig>(json);
            }
            catch(Exception ex)
            {
                throw new ConfigurationErrorsException(ex.ToString());
            }
            return config;
        }

        public void Save()
        {
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            using (StreamWriter sw = new StreamWriter(CONFIG_FILE_PATH, false, Encoding.UTF8))
            {
                sw.Write(json);
            }
        }
    }
}
