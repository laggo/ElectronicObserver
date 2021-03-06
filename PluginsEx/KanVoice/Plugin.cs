﻿using ElectronicObserver.Window.Plugins;
using ElectronicObserver;
using ElectronicObserver.Data;
using ElectronicObserver.Notifier;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.CSharp;
using Fiddler;
using System.Xml;
using System.Text.RegularExpressions;

using WeifenLuo.WinFormsUI.Docking;

namespace KanVoice
{

    public class VoicePlugin : ServerPlugin
    {
        public static VoiceSubtitle form = new VoiceSubtitle();
        public static ElectronicObserver.Window.FormMain Main = null;
        VoiceObserverPlugin VoiceObserverPlugin = new VoiceObserverPlugin();

        const string Title = "舰娘语音字幕";

        public override string MenuTitle
        {
            get { return Title; }
        }

        public override string Version
        {
            get { return "1.0.0.2"; }
        }

        public override bool RunService(ElectronicObserver.Window.FormMain main)
        {
            Main = main;
            Main.SubForms.Add(form);
            for (int i = 0; i < Main.MainMenuStrip.Items.Count; i++)
            {
                if (Main.MainMenuStrip.Items[i].Name == "StripMenu_View")
                {
                    var aa = (ToolStripMenuItem)Main.MainMenuStrip.Items[i];
                    aa.DropDownItems.Add(Title).Click += Plugin_Click;
                }
            }
            VoiceObserverPlugin.Data.Init();
            LoadConfig();
            //ElectronicObserver.Utility.Configuration.Instance.AddObserverPlugin(VoiceObserverPlugin);
            try
            {
                ElectronicObserver.Utility.KanVoice.OnGetVoiceText += KanVoice_OnGetVoiceText;
            }
            catch
            {
                //没有这个事件钩子 ,74主程序版本不够
            }
            return true;
        }

        string KanVoice_OnGetVoiceText(int shipID, int voiceID)
        {
            return VoiceObserverPlugin.Data.GetVoice(shipID, voiceID);
        }
        void Plugin_Click(object sender, EventArgs e)
        {
            try
            {
                form.Show(Main.MainPanel);
            }
            catch
            {
                form = new VoiceSubtitle();
                form.Show(Main.MainPanel);
            }
        }

        public static void LoadConfig()
        {
            try
            {
                if (System.IO.File.Exists(VoiceData.ConfigFile))
                {
                    XmlDocument doc = new XmlDocument();
                    doc.Load(VoiceData.ConfigFile);
                    var Root = doc.DocumentElement;

                    string UseThirdBuffer = Root.GetAttribute("UseThirdBuffer");
                    if (UseThirdBuffer == "True")
                    {
                        VoiceData.UseThirdBuffer = true;
                        form.UseThird.Checked = true;
                    }
                    else
                    {
                        VoiceData.UseThirdBuffer = false;
                    }

                    string subtitleDisplayArea = Root.GetAttribute("SubtitleDisplayArea");
                    SubtitleDisplayArea Area;
                    if (Enum.TryParse(subtitleDisplayArea, out Area))
                    {
                        VoiceData.subtitleDisplayArea = Area;
                    }
                    ((ToolStripMenuItem)(form.MenuArea.DropDownItems[(int)Area])).Checked = true;

                    string IgnoreBlankSubtitles = Root.GetAttribute("IgnoreBlankSubtitles");
                    if (IgnoreBlankSubtitles == "True")
                    {
                        VoiceData.IgnoreBlankSubtitles = true;
                        form.IgnoreItem.Checked = true;
                    }
                    else
                    {
                        VoiceData.IgnoreBlankSubtitles = false;
                    }

                    string MaxLines = Root.GetAttribute("MaxLines");
                    int max;
                    if (int.TryParse(MaxLines, out max))
                    {
                        VoiceSubtitle.MaxLines = max;
                    }
                    if (VoiceSubtitle.MaxLines <= form.SetMax.DropDownItems.Count)
                        ((ToolStripMenuItem)form.SetMax.DropDownItems[VoiceSubtitle.MaxLines - 1]).Checked = true;
                }
            }
            catch
            {
            }
        }

        public static void SaveConfig()
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                if (!System.IO.File.Exists(VoiceData.ConfigFile))
                {
                    XmlElement xmlelem = doc.CreateElement("Config");
                    doc.AppendChild(xmlelem);
                }
                else
                {
                    doc.Load(VoiceData.ConfigFile);
                }
                var Root = doc.DocumentElement;
                Root.RemoveAll();
                Root.SetAttribute("UseThirdBuffer", VoiceData.UseThirdBuffer.ToString());
                Root.SetAttribute("IgnoreBlankSubtitles", VoiceData.IgnoreBlankSubtitles.ToString());
                Root.SetAttribute("SubtitleDisplayArea", VoiceData.subtitleDisplayArea.ToString());
                Root.SetAttribute("MaxLines", VoiceSubtitle.MaxLines.ToString());
                doc.Save(VoiceData.ConfigFile);
            }
            catch
            {
            }
        }
    }


    public class VoiceObserverPlugin : ObserverPlugin
    {
        const string Title = "舰娘语音字幕";
        public static VoiceData Data = new VoiceData();
        public override string MenuTitle
        {
            get { return Title; }
        }

        public override string Version
        {
            get { return "1.0.0.2"; }
        }

        public override bool OnBeforeRequest(Session oSession)
        {
            if (VoiceData.UseThirdBuffer)
            {
                Regex reg = new Regex("sound/kc(.*?)/(.*?).mp3");
                Match match = reg.Match(oSession.fullUrl);
                if (match.Success && match.Groups.Count == 3)
                {
                    oSession.bBufferResponse = true;
                }
            }
            return false;
        }

        public override bool OnAfterSessionComplete(Session oSession)
        {
            return false;
        }

        public override bool OnBeforeResponse(Session oSession)
        {
            try
            {
                Regex reg = new Regex("sound/kc(.*?)/(.*?).mp3");
                Match match = reg.Match(oSession.fullUrl);
                if (match.Success && match.Groups.Count == 3)
                {
                    if (VoiceData.UseThirdBuffer)
                    {
                        oSession.oResponse.headers["Pragma"] = "no-cache";
                        oSession.oResponse.headers["Cache-Control"] = "no-cache";
                    }
                    string voice = Data.GetVoice(match.Groups[1].Value, int.Parse(match.Groups[2].Value));
                    if (voice != null)
                    {
                        if (VoiceData.subtitleDisplayArea == SubtitleDisplayArea.DockForm || VoiceData.subtitleDisplayArea == SubtitleDisplayArea.Both)
                        {
                            VoicePlugin.form.AddText(voice);
                        }
                        if (VoiceData.subtitleDisplayArea == SubtitleDisplayArea.StatusBar || VoiceData.subtitleDisplayArea == SubtitleDisplayArea.Both)
                        {
                            try
                            {
                                var stripStatus = VoicePlugin.Main.Controls.Find("StripStatus", false)[0] as StatusStrip;
                                stripStatus.Items[0].Text = voice;
                            }
                            catch
                            {//状态栏可能移动位置了
                            }
                        }
                    }
                    return false;
                }
            }
            catch
            {
                ElectronicObserver.Utility.Logger.Add(3, string.Format("{0}", "语音字幕插件 出错"));
            }
            return false;
        }
    }
}
