﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CM3D2.MaidFiddler.Hook;
using CM3D2.MaidFiddler.Plugin.Utils;

namespace CM3D2.MaidFiddler.Plugin.Gui
{
    public partial class MaidFiddlerGUI : Form
    {
        private bool initialized;

        public MaidFiddlerGUI(MaidFiddler plugin)
        {
            Plugin = plugin;
            InitializeComponent();
            Font uiFont = new Font(FontFamily.GenericSansSerif, 8.25F);
            Font = uiFont;
            foreach (Control control in Controls)
            {
                control.Font = uiFont;
            }
            if(!plugin.CFGOpenOnStartup)
                Opacity = 0.0;
            Text = $"CM3D2 Maid Fiddler {MaidFiddler.VERSION}";
            Translation.AddTranslationAction("TITLE_TEXT", s => Text = $"CM3D2 Maid Fiddler {MaidFiddler.VERSION} {s}");
            try
            {
                Player = new PlayerInfo(this);
                removeValueLimit = false;
                Resources.InitThumbnail();
                InitMenuText();
                InitMaidInfoTab();
                InitMaidStatsTab();
                InitClassesTab();
                InitWorkTab();
                InitYotogiSkillTab();
                InitMiscTab();
                InitGameTab();
                ControlsEnabled = false;
                Player.UpdateAll();

                InitMaids();

                playerValueUpdateQueue = new Dictionary<PlayerChangeType, Action>();

                Shown += OnShown;
                FormClosing += OnFormClosing;
                VisibleChanged += OnVisibleChanged;

                listBox1.DrawMode = DrawMode.OwnerDrawFixed;
                listBox1.DrawItem += DrawListBox;
                listBox1.SelectedValueChanged += OnSelectedValueChanged;

                InitHookCallbacks();

                Translation.ApplyTranslation();
            }
            catch (Exception e)
            {
                FiddlerUtils.ThrowErrorMessage(e, "Failed to initalize core components", plugin);
            }
        }

        public MaidFiddler Plugin { get; }

        private void OnShown(object sender, EventArgs e)
        {
            Visible = Plugin.CFGOpenOnStartup;
            if(!Plugin.CFGOpenOnStartup)
                Opacity = 1.0;
        }

        public void DoIfVisible(Action action)
        {
            if (Visible)
                action();
        }

        private void DrawListBox(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (listBox1.Items.Count == 0)
                return;
            MaidInfo m = listBox1.Items[e.Index] as MaidInfo;
            if (m == null)
                return;

            Image maidThumb;
            maidThumbnails.TryGetValue(m.Maid.Param.status.guid, out maidThumb);

            if (maidThumb == null && Resources.DefaultThumbnail == null)
                e.Graphics.FillRectangle(Brushes.BlueViolet, e.Bounds.X, e.Bounds.Y, e.Bounds.Height, e.Bounds.Height);
            else
            {
                e.Graphics.DrawImage(
                maidThumb ?? Resources.DefaultThumbnail,
                e.Bounds.X,
                e.Bounds.Y,
                e.Bounds.Height,
                e.Bounds.Height);
            }
            string name = Plugin.UseJapaneseNameStyle
                          ? $"{m.Maid.Param.status.last_name} {m.Maid.Param.status.first_name}"
                          : $"{m.Maid.Param.status.first_name} {m.Maid.Param.status.last_name}";
            e.Graphics.DrawString(
            name,
            e.Font,
            Brushes.Black,
            e.Bounds.X + e.Bounds.Height + 5,
            e.Bounds.Y + (e.Bounds.Height - e.Font.Height) / 2,
            StringFormat.GenericDefault);

            e.DrawFocusRectangle();
        }

        private void InitMenuText()
        {
            foreach (ToolStripDropDownItem item in menuStrip1.Items)
            {
                LoadMenuText(item);
                Translation.AddTranslationAction(item.Text, s => item.Text = s);
            }
        }

        private void LoadMenuText(ToolStripDropDownItem item)
        {
            foreach (ToolStripItem toolStripItem in item.DropDownItems)
            {
                ToolStripDropDownItem downItem = toolStripItem as ToolStripDropDownItem;
                if (downItem != null)
                    LoadMenuText(downItem);
                Translation.AddTranslationAction(toolStripItem.Text, s => toolStripItem.Text = s);
            }
        }

        private void OnFormClosing(object sender, FormClosingEventArgs formClosingEventArgs)
        {
            Debugger.WriteLine(
            $"Closing GUI. Destroying the GUI: {destroyGUI}. Running on application thread: {!InvokeRequired}");
            formClosingEventArgs.Cancel = !destroyGUI;
            if (!destroyGUI)
                Hide();
            else
                Application.ExitThread();
        }

        private void OnSelectedValueChanged(object sender, EventArgs e)
        {
            Debugger.WriteLine("Changed selected maid!");
            currentQueue = -1;
            valueUpdateQueue[0].Clear();
            valueUpdateQueue[1].Clear();
            currentQueue = 0;
            MaidInfo maid = SelectedMaid;
            if (maid == null)
            {
                Debugger.WriteLine(LogLevel.Error, "MAID IS NULL");
                ClearAllFields();
                ControlsEnabled = false;
                return;
            }
            Debugger.WriteLine(
            LogLevel.Info,
            $"New maid: {maid.Maid.Param.status.first_name} {maid.Maid.Param.status.last_name}");
            ControlsEnabled = true;
            maid.UpdateAll();
        }

        private void OnVisibleChanged(object sender, EventArgs eventArgs)
        {
            Debugger.Assert(
            () =>
            {
                if (!initialized && Visible && !IsHandleCreated)
                {
                    Debugger.WriteLine(LogLevel.Info, "No handle! Creating one...");
                    CreateControl();
                    initialized = true;
                }
                if (!Visible)
                    return;
                UpdateMaids(GameMain.Instance.CharacterMgr.GetStockMaidList().ToList());
                Player.UpdateAll();
            },
            $"Failed to {(Visible ? "restore" : "hide")} the Maid Fiddler window");
        }

        private void UpdateList()
        {
            Debugger.Assert(
            () =>
            {
                listBox1.ClearSelected();
                ClearAllFields();
                listBox1.BeginUpdate();
                listBox1.Items.Clear();
                if (loadedMaids.Count > 0)
                    listBox1.Items.AddRange(loadedMaids.Select(m => m.Value as object).ToArray());
                listBox1.EndUpdate();
                listBox1.Invalidate();
            },
            "Failed to update maid GUI list");
        }

        private void OpenLangMenu(object sender, EventArgs e)
        {
            Debugger.WriteLine(LogLevel.Info, "Opening language select menu...");
            TranslationSelectionGUI tsGui = new TranslationSelectionGUI(Plugin);
            tsGui.ShowDialog(this);
            tsGui.Dispose();
        }

        private void OpenAboutMenu(object sender, EventArgs e)
        {
            AboutGUI aboutGui = new AboutGUI();
            aboutGui.ShowDialog(this);
            aboutGui.Dispose();
        }

        private void OpenSettings(object sender, EventArgs e)
        {
            SettingsGUI settings = new SettingsGUI(Plugin);
            settings.ShowDialog(this);
            settings.Dispose();
            listBox1.Refresh();
        }

        private delegate void UpdateInternal(List<Maid> newMaids);
    }
}