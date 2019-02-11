using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RpaNotificator
{
    class AppConfig
    {
        private Configuration config;
        public string LogFileDirectory
        {
            get
            {
                return ConfigurationManager.AppSettings["LogFileDirectory"];
            }
            set
            {
                config.AppSettings.Settings["LogFileDirectory"].Value = value;
            }
        }
        public string LogFileName
        {
            get
            {
                return ConfigurationManager.AppSettings["LogFileName"];
            }
            set
            {
                config.AppSettings.Settings["LogFileName"].Value = value;
            }
        }
        public string WebhookUrl
        {
            get
            {
                return ConfigurationManager.AppSettings["WebhookUrl"];
            }
            set
            {
                config.AppSettings.Settings["WebhookUrl"].Value = value;
            }
        }
        public bool ReportNormalLog
        {
            get
            {
                return "1".Equals(ConfigurationManager.AppSettings["ReportNormalLog"]);
            }
            set
            {
                config.AppSettings.Settings["ReportNormalLog"].Value = (value == true) ? "1" : "0";
            }
        }
        public bool ReportErrorLog
        {
            get
            {
                return "1".Equals(ConfigurationManager.AppSettings["ReportErrorLog"]);
            }
            set
            {
                config.AppSettings.Settings["ReportErrorLog"].Value = (value == true) ? "1" : "0";
            }
        }
        public int RefreshInterval
        {
            get
            {
                int.TryParse(ConfigurationManager.AppSettings["RefreshInterval"], out int interval);
                return interval;
            }
            set
            {
                config.AppSettings.Settings["RefreshInterval"].Value = value.ToString();
            }
        }
        public int LogUpdateInterval
        {
            get
            {
                int.TryParse(ConfigurationManager.AppSettings["LogUpdateInterval"], out int interval);
                return interval;
            }
            set
            {
                config.AppSettings.Settings["LogUpdateInterval"].Value = value.ToString();
            }
        }

        public AppConfig()
        {
            config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            if (config.AppSettings.Settings.Count < 1)
            {
                throw new SettingsPropertyNotFoundException();
            }
        }

        public void Save()
        {
            config.Save();
        }
    }
}
