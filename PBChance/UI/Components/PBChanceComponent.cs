using LiveSplit.Model;
using LiveSplit.UI.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LiveSplit.UI;
using System.Drawing;
using System.Windows.Forms;
using System.Xml;

namespace PBChance.UI.Components
{
    class PBChanceComponent : IComponent
    {
        protected InfoTextComponent InternalComponent { get; set; }
        protected PBChanceSettings Settings { get; set; }
        protected LiveSplitState State;
        protected Random rand;
        protected string category;

        string IComponent.ComponentName => "PB Chance";

        IDictionary<string, Action> IComponent.ContextMenuControls => null;
        float IComponent.HorizontalWidth => InternalComponent.HorizontalWidth;
        float IComponent.MinimumHeight => InternalComponent.MinimumHeight;
        float IComponent.MinimumWidth => InternalComponent.MinimumWidth;
        float IComponent.PaddingBottom => InternalComponent.PaddingBottom;
        float IComponent.PaddingLeft => InternalComponent.PaddingLeft;
        float IComponent.PaddingRight => InternalComponent.PaddingRight;
        float IComponent.PaddingTop => InternalComponent.PaddingTop;
        float IComponent.VerticalHeight => InternalComponent.VerticalHeight;

        //Split Data
        List<Time?>[] splits;
        private double[] runSurvivalChance;   //survivalChance[splitIndex]
        private double[] splitSurvivalChance; //survivalChance[splitIndex]
        int[] numberOfRunsSurvivedSegment;
        int[] numberOfRunsDeadBeforeSegment;
        private int runSampleCount;

        XmlNode IComponent.GetSettings(XmlDocument document)
        {
            return Settings.GetSettings(document);
        }

        Control IComponent.GetSettingsControl(LayoutMode mode)
        {
            Settings.Mode = mode;
            return Settings;
        }

        void IComponent.SetSettings(XmlNode settings)
        {
            Settings.SetSettings(settings);
        }

        public PBChanceComponent(LiveSplitState state)
        {
            State = state;
            InternalComponent = new InfoTextComponent("PB / Survival Chance                   .", "0.0%, 0.0%")
            {
                AlternateNameText = new string[]
                {
                    "PB , Survival Chance",
                    "PB,S%:"
                }
            };
            Settings = new PBChanceSettings();
            Settings.SettingChanged += OnSettingChanged;
            rand = new Random();
            category = State.Run.GameName + State.Run.CategoryName;

            state.OnSplit += OnSplit;
            state.OnReset += OnReset;
            state.OnSkipSplit += OnSkipSplit;
            state.OnUndoSplit += OnUndoSplit;
            state.OnStart += OnStart;
            state.RunManuallyModified += OnRunManuallyModified;

            UpdateComponent();
            RebuildSplitData();
            Recalculate();
        }

        private void RebuildSplitData()
        {
            // Create the lists of all split times
            splits = new List<Time?>[State.Run.Count];
            for (int i = 0; i < State.Run.Count; i++)
            {
                splits[i] = new List<Time?>();
            }

            // Find the range of attempts to gather times from
            int lastAttempt = State.Run.AttemptHistory.Count;
            int runCount = State.Run.AttemptHistory.Count;
            if (!Settings.IgnoreRunCount)
            {
                runCount = State.Run.AttemptCount;
                if (runCount > State.Run.AttemptHistory.Count)
                {
                    runCount = State.Run.AttemptHistory.Count;
                }
            }
            int firstAttempt = lastAttempt / 2;
            if (Settings.UseFixedAttempts)
            {
                // Fixed number of attempts
                firstAttempt = lastAttempt - Settings.AttemptCount;

                if (firstAttempt < State.Run.GetMinSegmentHistoryIndex())
                {
                    firstAttempt = State.Run.GetMinSegmentHistoryIndex();
                }
            }
            else
            {
                // Percentage of attempts
                firstAttempt = lastAttempt - runCount * Settings.AttemptCount / 100;
                if (firstAttempt < State.Run.GetMinSegmentHistoryIndex())
                {
                    firstAttempt = State.Run.GetMinSegmentHistoryIndex();
                }
            }

            runSampleCount = lastAttempt - firstAttempt;
            if (runSampleCount >= 0)
            {
                splitSurvivalChance = new double[State.Run.Count]; //State.Run.Count = # Splits in a run
                runSurvivalChance = new double[State.Run.Count];
                numberOfRunsSurvivedSegment = new int[State.Run.Count];
                numberOfRunsDeadBeforeSegment = new int[State.Run.Count];


                // Gather split times
                for (int a = firstAttempt; a < lastAttempt; a++)
                {
                    int lastSegment = -1;

                    // Get split times from a single attempt
                    for (int segment = 0; segment < State.Run.Count; segment++)
                    {
                        if (State.Run[segment].SegmentHistory == null || State.Run[segment].SegmentHistory.Count == 0)
                        {
                            InternalComponent.InformationValue = "-";
                            return;
                        }

                        if (State.Run[segment].SegmentHistory.ContainsKey(a) && State.Run[segment].SegmentHistory[a][State.CurrentTimingMethod] > TimeSpan.Zero)
                        {
                            splits[segment].Add(State.Run[segment].SegmentHistory[a]);
                            lastSegment = segment;
                            numberOfRunsSurvivedSegment[segment]++;
                        }
                    }

                    if (lastSegment < State.Run.Count - 1) //If run dies early
                    {
                        if (lastSegment == -1)
                        {
                            runSampleCount -= 1;
                        }
                        else
                        {                                     //TODO: Look at this after you've had coffee. Should this be +1 or +2
                            for (int deadSegments = lastSegment + 2; deadSegments < State.Run.Count; deadSegments++)
                            {
                                numberOfRunsDeadBeforeSegment[deadSegments]++;
                            }
                        }

                        // Run didn't finish, add "reset" for the last known split
                        splits[lastSegment + 1].Add(null);
                    }
                }

                //Calculate split survival chance
                for (int segment = 0; segment < State.Run.Count; segment++)
                {                                                                                                             //numberOfRunsDeadBeforeSegment is 1 too low?
                    int deadBeforeCurrentSegment = numberOfRunsDeadBeforeSegment[segment];
                    int deadBeforeNextSegment;
                    if (segment + 1 < State.Run.Count)
                    {
                        deadBeforeNextSegment = numberOfRunsDeadBeforeSegment[segment + 1];
                    }
                    else
                    {
                        deadBeforeNextSegment = numberOfRunsDeadBeforeSegment[segment];
                    }
                    /*
                    numberOfRunsSurvivedSegment[State.CurrentSplitIndex]
                    if (State.CurrentSplitIndex < numberOfRunsDeadBeforeSegment.Length - 1 && State.CurrentSplitIndex >= 0)
                        {
                            int deadBeforeCurrentSegment = numberOfRunsDeadBeforeSegment[State.CurrentSplitIndex];
                            int deadBeforeNextSegment = numberOfRunsDeadBeforeSegment[State.CurrentSplitIndex + 1];
                            int diedHere = deadBeforeNextSegment - deadBeforeCurrentSegment;*/

                    int diedHere = runSampleCount - deadBeforeCurrentSegment - numberOfRunsSurvivedSegment[segment];

                    splitSurvivalChance[segment] = (double)(numberOfRunsSurvivedSegment[segment]) / (double)(diedHere+numberOfRunsSurvivedSegment[segment]); //seems like it's off by one on counting dead runs, which is why the split is 90% instead of 100%
                    //runSampleCount - deadBeforeCurrentSegment - numberOfRunsSurvivedSegment[segment];
                    //splitSurvivalChance[segment] = (double)(numberOfRunsSurvivedSegment[segment]) / (double)(runSampleCount - deadBeforeCurrentSegment);
                }

                //RUN SURVIVAL CHANCE
                // Run survival chance = product of chance to survive current and all subsequent splits
                for (int segment = 0; segment < State.Run.Count; segment++)
                {
                    runSurvivalChance[segment] = splitSurvivalChance[segment];
                    for (int futureSegments = segment + 1; futureSegments < State.Run.Count; futureSegments++)
                    {
                        runSurvivalChance[segment] = runSurvivalChance[segment] * splitSurvivalChance[futureSegments];
                    }
                }
            }
        }

        private void UpdateComponent()
        {
            if (Settings.DebugMode)
            {
                InternalComponent.InformationName = "RSS,CSSC,DBS,RSH,RDH:"; //Run sample size, current split survival chance, #runs survived here, # runs died here;
            }
            else
            {
                InternalComponent.InformationName = "PB / Survival Chance                   .\n";
            }
        }

        private void OnRunManuallyModified(object sender, EventArgs e)
        {
            RebuildSplitData();
            Recalculate();
        }

        private void OnSettingChanged(object sender, EventArgs e)
        {
            UpdateComponent();
            RebuildSplitData();
            Recalculate();
        }

        private void OnStart(object sender, EventArgs e)
        {
            RebuildSplitData();
            Recalculate();
        }

        protected void OnUndoSplit(object sender, EventArgs e)
        {
            Recalculate();
        }

        protected void OnSkipSplit(object sender, EventArgs e)
        {
            Recalculate();
        }

        protected void OnReset(object sender, TimerPhase value)
        {
            Recalculate();
        }

        protected void OnSplit(object sender, EventArgs e)
        {
            Recalculate();
        }

        protected void Recalculate()
        {
            // Get the current Personal Best, if it exists
            Time pb = State.Run.Last().PersonalBestSplitTime;
            
            if(pb[State.CurrentTimingMethod] == TimeSpan.Zero || runSampleCount <= 0)
            {
                // No personal best, so any run will PB
                InternalComponent.InformationValue = "100%, ?%";
                return;
            }

            // Calculate probability of PB
            int success = 0;
            for (int i = 0; i < 10000; i++)
            {
                // Get current time as a baseline
                Time test = State.CurrentTime;
                if (test[State.CurrentTimingMethod] < TimeSpan.Zero)
                {
                    test[State.CurrentTimingMethod] = TimeSpan.Zero;
                }

                // Add random split times for each remaining segment
                for (int segment = 0; segment < State.Run.Count; segment++)
                {
                    if (segment < State.CurrentSplitIndex)
                    {
                        continue;
                    }

                    if(splits[segment].Count == 0)
                    {
                        // This split contains no split times, so we cannot calculate a probability
                        InternalComponent.InformationValue = "-";
                        return;
                    }

                    int attempt = rand.Next(splits[segment].Count-1);
                    Time? split = splits[segment][attempt];
                    if (split == null)
                    {
                        // Split is a reset, so count it as a failure
                        test += pb;
                        break;
                    }
                    else
                    {
                        // Add the split time
                        test += split.Value;
                    }
                }

                if (test[State.CurrentTimingMethod] < pb[State.CurrentTimingMethod])
                {
                    success++;
                }
            }

            double prob = success / 10000.0;

            string probString = Math.Round(prob * 100.0, 2).ToString();
            string survivalString;
            try
            {
                if (State.CurrentSplitIndex < runSurvivalChance.Length && State.CurrentSplitIndex >= 0)
                    {survivalString = Math.Round(100 * runSurvivalChance[State.CurrentSplitIndex], 2).ToString();}
                else
                    {survivalString = "N/A"; }
            }
            catch (Exception e)
            {
                survivalString = "err";
            }
            string text = probString + "," + survivalString + "%";
            if (Settings.DisplayOdds && prob > 0)
            {
                text += " (1 in " + Math.Round(1 / prob, 2).ToString() + ")";
            }

            if (Settings.DebugMode)
            {
                if (State.CurrentSplitIndex < splitSurvivalChance.Length && State.CurrentSplitIndex >= 0)
                { survivalString = Math.Round(100.00 * splitSurvivalChance[State.CurrentSplitIndex], 2).ToString(); }
                else
                { survivalString = "N/A"; }

                string survivalCountString;
                if (State.CurrentSplitIndex < numberOfRunsSurvivedSegment.Length && State.CurrentSplitIndex >= 0)
                { survivalCountString = numberOfRunsSurvivedSegment[State.CurrentSplitIndex].ToString(); }
                else
                { survivalCountString = "N/A"; }

                string dbsString;
                if (State.CurrentSplitIndex < numberOfRunsSurvivedSegment.Length && State.CurrentSplitIndex >= 0)
                { dbsString = numberOfRunsDeadBeforeSegment[State.CurrentSplitIndex].ToString(); }
                else
                { dbsString = "N/A"; }

                string deathCountString;
                if (State.CurrentSplitIndex < numberOfRunsDeadBeforeSegment.Length && State.CurrentSplitIndex >= 0)
                {
                    int deadBeforeCurrentSegment = numberOfRunsDeadBeforeSegment[State.CurrentSplitIndex];
                    int diedHere = runSampleCount - deadBeforeCurrentSegment - numberOfRunsSurvivedSegment[State.CurrentSplitIndex];

                    deathCountString = diedHere.ToString();
                }
                else
                { deathCountString = "N/A"; }


                //InternalComponent.InformationName = "RSS,CSSC, RSH, RDH:"; //Run sample size, current split survival chance, #runs survived here, # runs died here;
                text = runSampleCount + "," + survivalString + "," + dbsString + "," + survivalCountString + "," + deathCountString;
            }
            InternalComponent.InformationValue = text;
        }

        void IComponent.DrawHorizontal(Graphics g, LiveSplitState state, float height, Region clipRegion)
        {
            PrepareDraw(state, LayoutMode.Horizontal);
            InternalComponent.DrawHorizontal(g, state, height, clipRegion);
        }

        void IComponent.DrawVertical(Graphics g, LiveSplitState state, float width, Region clipRegion)
        {
            InternalComponent.PrepareDraw(state, LayoutMode.Vertical);
            PrepareDraw(state, LayoutMode.Vertical);
            InternalComponent.DrawVertical(g, state, width, clipRegion);
        }

        void PrepareDraw(LiveSplitState state, LayoutMode mode)
        {
            InternalComponent.NameLabel.ForeColor = state.LayoutSettings.TextColor;
            InternalComponent.ValueLabel.ForeColor = state.LayoutSettings.TextColor;
            InternalComponent.PrepareDraw(state, mode);
        }

        void IComponent.Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
        {
            string newCategory = State.Run.GameName + State.Run.CategoryName;
            if (newCategory != category)
            {
                Recalculate();
                category = newCategory;
            }
            
            InternalComponent.Update(invalidator, state, width, height, mode);
        }

        void IDisposable.Dispose()
        {
            
        }
    }
}
