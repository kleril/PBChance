﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using LiveSplit.UI;

namespace PBChance.UI.Components
{
    public partial class PBChanceSettings : UserControl
    {
        public Boolean UsePercentOfAttempts { get; set; }
        public Boolean UseFixedAttempts { get; set; }
        public int AttemptCount { get; set; }
        public bool DisplayOdds { get; set; }
        public bool IgnoreRunCount { get; set; }
        public bool DebugMode { get; set; }

        public event EventHandler SettingChanged;

        public PBChanceSettings()
        {
            InitializeComponent();

            UsePercentOfAttempts = true;
            UseFixedAttempts = false;
            DebugMode = false;
            AttemptCount = 50;

            PercentOfAttempts.DataBindings.Add("Checked", this, "UsePercentOfAttempts", true, DataSourceUpdateMode.OnPropertyChanged).BindingComplete += OnSettingChanged;
            FixedAttempts.DataBindings.Add("Checked", this, "UseFixedAttempts", true, DataSourceUpdateMode.OnPropertyChanged).BindingComplete += OnSettingChanged;
            AttemptCountBox.DataBindings.Add("Value", this, "AttemptCount", true, DataSourceUpdateMode.OnPropertyChanged).BindingComplete += OnSettingChanged;
            DisplayOddsCheckbox.DataBindings.Add("Checked", this, "DisplayOdds", true, DataSourceUpdateMode.OnPropertyChanged).BindingComplete += OnSettingChanged;
            IgnoreRunCountBox.DataBindings.Add("Checked", this, "IgnoreRunCount", true, DataSourceUpdateMode.OnPropertyChanged).BindingComplete += OnSettingChanged;
            debugCheckBox.DataBindings.Add("Checked", this, "DebugMode", true, DataSourceUpdateMode.OnPropertyChanged).BindingComplete += OnSettingChanged;
        }

        private void OnSettingChanged(object sender, BindingCompleteEventArgs e)
        {
            SettingChanged?.Invoke(this, e);
        }

        public LayoutMode Mode { get; internal set; }

        internal XmlNode GetSettings(XmlDocument document)
        {
            var parent = document.CreateElement("Settings");
            CreateSettingsNode(document, parent);
            return parent;
        }

        private int CreateSettingsNode(XmlDocument document, XmlElement parent)
        {
            return SettingsHelper.CreateSetting(document, parent, "Version", "0.1") ^
                SettingsHelper.CreateSetting(document, parent, "AttemptCount", AttemptCount) ^
                SettingsHelper.CreateSetting(document, parent, "UsePercentOfAttempts", UsePercentOfAttempts) ^
                SettingsHelper.CreateSetting(document, parent, "UseFixedAttempts", UseFixedAttempts) ^
                SettingsHelper.CreateSetting(document, parent, "DisplayOdds", DisplayOdds) ^
                SettingsHelper.CreateSetting(document, parent, "IgnoreRunCount", IgnoreRunCount) ^ 
                SettingsHelper.CreateSetting(document, parent, "DebugMode", DebugMode);
        }

        internal void SetSettings(XmlNode settings)
        {
            AttemptCount = SettingsHelper.ParseInt(settings["AttemptCount"]);
            UsePercentOfAttempts = SettingsHelper.ParseBool(settings["UsePercentOfAttempts"]);
            UseFixedAttempts = SettingsHelper.ParseBool(settings["UseFixedAttempts"]);
            DisplayOdds = SettingsHelper.ParseBool(settings["DisplayOdds"]);
            IgnoreRunCount = SettingsHelper.ParseBool(settings["IgnoreRunCount"]);
            DebugMode = SettingsHelper.ParseBool(settings["DebugMode"]);
        }
    }
}
