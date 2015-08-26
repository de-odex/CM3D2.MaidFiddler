﻿using System.Drawing;
using System.IO;
using System.Linq;
using CM3D2.MaidFiddler.Hook;
using param;

namespace CM3D2.MaidFiddler.Plugin.Gui
{
    public partial class MaidFiddlerGUI
    {
        private void InitHookCallbacks()
        {
            MaidStatusChangeHooks.StatusChanged += OnStatusChanged;
            MaidStatusChangeHooks.ThumbnailChanged += OnMaidThumbnailChanged;
            MaidStatusChangeHooks.StatusChangedID += OnStatusChanged;
            MaidStatusChangeHooks.ClassUpdated += OnClassUpdated;
            MaidStatusChangeHooks.NewProperty += OnPropertyHasChanged;
            MaidStatusChangeHooks.PropertyRemoved += OnPropertyHasChanged;
            MaidStatusChangeHooks.CheckWorkEnabled += OnWorkEnabledCheck;
            MaidStatusChangeHooks.ProcessNoonWorkData += PostProcessNoonWorkData;
            MaidStatusChangeHooks.ProcessNightWorkData += PostProcessNightWorkData;
            MaidStatusChangeHooks.StatusUpdated += OnStatusUpdated;
            MaidStatusChangeHooks.FeaturePropensityUpdated += OnFeaturePropensityUpdated;
            MaidStatusChangeHooks.CommandUpdate += OnCommandUpdate;

            PlayerStatusChangeHooks.PlayerValueChanged += OnPlayerValueChanged;

            ValueLimitHooks.ToggleValueLimit += OnValueRound;
        }

        private void OnClassUpdated(HookEventArgs args)
        {
            Debugger.WriteLine("Updating maid and/or yotogi class info.");
            MaidInfo maid = SelectedMaid;
            if (maid == null)
            {
                Debugger.WriteLine(LogLevel.Warning, "Maid is NULL!");
                return;
            }

            if (maid.Maid != args.CallerMaid)
            {
                Debugger.WriteLine(LogLevel.Warning, "Caller maid is not the selected one! Aborting...");
                return;
            }

            if (valueUpdateQueue.ContainsKey(args.Tag))
            {
                Debugger.WriteLine(LogLevel.Error, "Tag already in update queue! Aborting...");
                return;
            }
            switch (args.Tag)
            {
                case MaidChangeType.MaidClassType:
                    valueUpdateQueue.Add(args.Tag, () => maid.UpdateMaidClass());
                    break;
                case MaidChangeType.YotogiClassType:
                    valueUpdateQueue.Add(args.Tag, () => maid.UpdateYotogiClass());
                    break;
                case MaidChangeType.MaidAndYotogiClass:
                    valueUpdateQueue.Add(
                    args.Tag,
                    () =>
                    {
                        maid.UpdateMaidBonusValues();
                        maid.UpdateMaidClass();
                        maid.UpdateYotogiClass();
                    });
                    break;
            }
        }

        private void OnCommandUpdate(CommandUpdateEventArgs args)
        {
            if (!allYotogiCommandsVisible)
                return;
            for (int i = 0; i < args.Commands[args.PlayerState].Length; i++)
            {
                Yotogi.SkillData.Command.Data data = args.Commands[args.PlayerState][i];
                args.CommandFactory.AddCommand(data);
            }
        }

        private void OnFeaturePropensityUpdated(UpdateFeaturePropensityEventArgs args)
        {
            MaidInfo maid = SelectedMaid;
            if (maid == null)
                return;

            if (args.CallerMaid != maid.Maid)
                return;

            if (args.UpdateFeature)
            {
                Debugger.WriteLine(LogLevel.Info, "Updating all features!");
                for (Feature e = Feature.Null + 1; e < Feature.Max; e++)
                    maid.UpdateMiscStatus(MaidChangeType.Feature, (int) e);
            }
            else if (args.UpdatePropensity)
            {
                Debugger.WriteLine(LogLevel.Info, "Updating all propensities!");
                for (Propensity e = Propensity.Null + 1; e < Propensity.Max; e++)
                    maid.UpdateMiscStatus(MaidChangeType.Propensity, (int) e);
            }
        }

        private void OnMaidThumbnailChanged(ThumbnailEventArgs args)
        {
            if (!IsMaidLoaded(args.Maid))
                return;

            Image img;
            using (MemoryStream stream = new MemoryStream(args.Maid.GetThumIcon().EncodeToPNG()))
            {
                img = Image.FromStream(stream);
            }

            if (!maidThumbnails.ContainsKey(args.Maid.Param.status.guid))
                maidThumbnails.Add(args.Maid.Param.status.guid, img);
            else
            {
                maidThumbnails[args.Maid.Param.status.guid].Dispose();
                maidThumbnails.Remove(args.Maid.Param.status.guid);
                maidThumbnails.Add(args.Maid.Param.status.guid, img);
            }

            listBox1.Invalidate();
        }

        private void OnPlayerValueChanged(PlayerValueChangeEventArgs args)
        {
            if (Player.Player == null)
                return;

            if (!playerValueUpdateQueue.ContainsKey(args.Tag))
                playerValueUpdateQueue.Add(args.Tag, () => Player.UpdateField(args.Tag));
        }

        private void OnPropertyHasChanged(StatusChangedEventArgs args)
        {
            MaidInfo maid = SelectedMaid;
            if (maid == null)
                return;

            if (maid.Maid != args.CallerMaid)
                return;

            if (valueUpdateQueue.ContainsKey(args.Tag))
            {
                Debugger.WriteLine(LogLevel.Warning, "Tag already in update queue! Aborting...");
                return;
            }
            switch (args.Tag)
            {
                case MaidChangeType.NewGetWork:
                case MaidChangeType.Work:
                    valueUpdateQueue.Add(args.Tag, () => maid.UpdateHasWork(args.ID));
                    break;
                case MaidChangeType.NewGetSkill:
                case MaidChangeType.Skill:
                    valueUpdateQueue.Add(args.Tag, () => maid.UpdateHasSkill(args.ID));
                    break;
            }
        }

        private void OnStatusChanged(StatusChangedEventArgs args)
        {
            Debugger.WriteLine($"Changed status for property {EnumHelper.GetName(args.Tag)}");
            MaidInfo maid = SelectedMaid;
            if (maid == null)
            {
                Debugger.WriteLine(LogLevel.Warning, "Maid is NULL!");
                return;
            }

            if (maid.Maid != args.CallerMaid)
            {
                Debugger.WriteLine(LogLevel.Warning, "Caller maid is not the selected one! Aborting...");
                return;
            }

            if (!valueUpdateQueue.ContainsKey(args.Tag))
                valueUpdateQueue.Add(args.Tag, () => maid.UpdateField(args.Tag, args.ID, args.Value));
            else
                Debugger.WriteLine(LogLevel.Warning, "Tag already in update queue! Aborting...");
        }

        private void OnStatusChanged(StatusEventArgs args)
        {
            Debugger.WriteLine($"Changed status for property {EnumHelper.GetName(args.Tag)}");
            Debugger.WriteLine(
            $"Called maid: {args.CallerMaid.Param.status.first_name} {args.CallerMaid.Param.status.last_name}");
            MaidInfo selectedMaid = SelectedMaid;
            if (selectedMaid == null)
            {
                Debugger.WriteLine(LogLevel.Warning, "No maid selected! Aborting...");
                return;
            }

            if (selectedMaid.Maid != args.CallerMaid)
            {
                Debugger.WriteLine(LogLevel.Warning, "Selected maids are different!");
                return;
            }

            if (selectedMaid.IsLocked(args.Tag))
            {
                Debugger.WriteLine(LogLevel.Info, "Value locked! Aborting changes...");
                args.BlockAssignment = true;
                return;
            }

            if (!valueUpdateQueue.ContainsKey(args.Tag))
            {
                Debugger.WriteLine(LogLevel.Info, "Adding to update queue");
                valueUpdateQueue.Add(args.Tag, () => selectedMaid.UpdateField(args.Tag));
            }
            else
                Debugger.WriteLine(LogLevel.Warning, "Already in update queue!");
        }

        private void OnStatusUpdated(StatusUpdateEventArgs args)
        {
            MaidInfo maid = SelectedMaid;
            if (maid == null)
                return;

            if (args.CallerMaid != maid.Maid)
                return;

            maid.UpdateMiscStatus(args.Tag, args.EnumVal, args.Value);
        }

        private void OnValueRound(ValueLimitEventArgs args)
        {
            args.RemoveLimit = removeValueLimit;
        }

        private void OnWorkEnabledCheck(WorkEventArgs args)
        {
            if (args.CallerMaid == null)
                return;
            if (!IsMaidLoaded(args.CallerMaid))
                return;

            switch (args.Tag)
            {
                case MaidChangeType.NoonWorkId:
                    args.ForceEnabled = GetMaidInfo(args.CallerMaid).IsNoonWorkForceEnabled(args.ID);
                    break;
                case MaidChangeType.NightWorkId:
                    args.ForceEnabled = GetMaidInfo(args.CallerMaid).IsNightWorkForceEnabled(args.ID);
                    break;
            }
        }

        private void PostProcessNightWorkData(PostProcessNightEventArgs args)
        {
            Maid m = args.ScheduleScene.slot[args.SlotID].maid;
            if (m == null || !IsMaidLoaded(m))
                return;

            MaidInfo maid = GetMaidInfo(m);
            foreach (
            NightTaskCtrl.NightTaskButton nightTaskButton in
            args.List.Where(nightTaskButton => maid.IsNightWorkForceEnabled(int.Parse(nightTaskButton.id))))
            {
                Debugger.WriteLine($"Forcing update on night work #{nightTaskButton.id}");
                nightTaskButton.enableTask = true;
            }
        }

        private void PostProcessNoonWorkData(PostProcessNoonEventArgs args)
        {
            Maid m = args.ScheduleScene.slot[args.SlotID].maid;
            if (m == null || !IsMaidLoaded(m))
                return;

            MaidInfo maid = GetMaidInfo(m);
            foreach (
            DaytimeTaskCtrl.DaytimeTaskButton daytimeTaskButton in
            args.List.Where(daytimeTaskButton => maid.IsNoonWorkForceEnabled(int.Parse(daytimeTaskButton.id))))
            {
                Debugger.WriteLine($"Forcing update on noon work #{daytimeTaskButton.id}");
                daytimeTaskButton.enableTask = true;
            }
        }
    }
}