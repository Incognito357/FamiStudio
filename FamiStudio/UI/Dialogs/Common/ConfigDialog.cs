﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace FamiStudio
{
    class ConfigDialog
    {
        enum ConfigSection
        {
            UserInterface,
            Sound,
            MIDI,
            Max
        };

        readonly string[] ConfigSectionNames =
        {
            "Interface",
            "Sound",
            "MIDI",
            ""
        };

        public enum TimeFormat
        {
            PatternFrame,
            MinuteSecondsMilliseconds,
            Max
        }

        readonly string[] TimeFormatStrings =
        {
            "Pattern:Frame",
            "MM:SS:mmm"
        };

        public string[] FollowSequencerStrings =
        {
            "None",
            "Jump",
            "Continuous"
        };

        private PropertyPage[] pages = new PropertyPage[(int)ConfigSection.Max];
        private MultiPropertyDialog dialog;

        public unsafe ConfigDialog(Rectangle mainWinRect)
        {
#if FAMISTUDIO_LINUX
            int width  = 500;
#else
            int width  = 480;
#endif
            int height = 300;
            int x = mainWinRect.Left + (mainWinRect.Width  - width)  / 2;
            int y = mainWinRect.Top  + (mainWinRect.Height - height) / 2;

            this.dialog = new MultiPropertyDialog(x, y, width, height);

            for (int i = 0; i < (int)ConfigSection.Max; i++)
            {
                var section = (ConfigSection)i;
                var page = dialog.AddPropertyPage(ConfigSectionNames[i], "Config" + section.ToString());
                CreatePropertyPage(page, section);
            }
        }

        private PropertyPage CreatePropertyPage(PropertyPage page, ConfigSection section)
        {
            switch (section)
            {
                case ConfigSection.UserInterface:
                {
#if FAMISTUDIO_WINDOWS
                    var scalingValues = new[] { "System", "100%", "150%", "200%" };
#elif FAMISTUDIO_MACOS
                    var scalingValues = new[] { "System", "100%", "200%" };
#else
                    var scalingValues = new[] { "System" };
#endif
                    var scalingIndex = Settings.DpiScaling == 0 ? 0 : Array.IndexOf(scalingValues, $"{Settings.DpiScaling}%");
                    var timeFormatIndex = Settings.TimeFormat < (int)TimeFormat.Max ? Settings.TimeFormat : 0;
                    var followSequencerIndex = Settings.FollowSequencer <= 0 ? 0 : Settings.FollowSequencer % FollowSequencerStrings.Length;

                    page.AddStringList("Scaling (Requires restart):", scalingValues, scalingValues[scalingIndex]); // 0
                    page.AddStringList("Time Format:", TimeFormatStrings, TimeFormatStrings[timeFormatIndex]); // 1
                    page.AddBoolean("Check for updates:", Settings.CheckUpdates); // 2
                    page.AddBoolean("Trackpad controls:", Settings.TrackPadControls); // 3
#if FAMISTUDIO_MACOS
                    page.AddBoolean("Reverse trackpad direction:", Settings.ReverseTrackPad); // 4
                    page.SetPropertyEnabled(4, Settings.TrackPadControls);
                    page.PropertyChanged += Page_PropertyChanged;
                    page.AddStringList("Follow Sequencer:", FollowSequencerStrings, FollowSequencerStrings[followSequencerIndex]); // 5
#else
                    page.AddStringList("Follow Sequencer:", FollowSequencerStrings, FollowSequencerStrings[followSequencerIndex]); // 4
#endif
#if FAMISTUDIO_LINUX
                    page.SetPropertyEnabled(0, false);
#endif

                    break;
                }
                case ConfigSection.Sound:
                {
                    page.AddIntegerRange("Stop instruments after (sec):", Settings.InstrumentStopTime, 0, 10); // 0
                    page.AddBoolean("Prevent popping on square channels:", Settings.SquareSmoothVibrato); // 1
                    break;
                }
                case ConfigSection.MIDI:
                {
                    int midiDeviceCount = Midi.InputCount;
                    var midiDevices = new List<string>();
                    for (int i = 0; i < midiDeviceCount; i++)
                    {
                        var name = Midi.GetDeviceName(i);
                        if (!string.IsNullOrEmpty(name))
                            midiDevices.Add(name);
                    }

                    var midiDevice = "";

                    if (!string.IsNullOrEmpty(Settings.MidiDevice) && midiDevices.Contains(Settings.MidiDevice))
                        midiDevice = Settings.MidiDevice;
                    else if (midiDevices.Count > 0)
                        midiDevice = midiDevices[0];

                    page.AddStringList("Device :", midiDevices.ToArray(), midiDevice); // 0
                    break;
                }
            }

            page.Build();
            pages[(int)section] = page;

            return page;
        }

#if FAMISTUDIO_MACOS
        private void Page_PropertyChanged(PropertyPage props, int idx, object value)
        {
            if (props == pages[(int)ConfigSection.UserInterface] && idx == 3)
            {
                props.SetPropertyEnabled(4, (bool)value);
            }
        }
#endif

        public DialogResult ShowDialog()
        {
            var dialogResult = dialog.ShowDialog();

            if (dialogResult == DialogResult.OK)
            {
                // UI
                var pageUI = pages[(int)ConfigSection.UserInterface];
                var pageSound = pages[(int)ConfigSection.Sound];
                var scalingString = pageUI.GetPropertyValue<string>(0);
                var timeFormatString = pageUI.GetPropertyValue<string>(1);

                Settings.DpiScaling = scalingString == "System" ? 0 : int.Parse(scalingString.Substring(0, 3));
                Settings.TimeFormat = Array.IndexOf(TimeFormatStrings, timeFormatString);
                Settings.CheckUpdates = pageUI.GetPropertyValue<bool>(2);
                Settings.TrackPadControls = pageUI.GetPropertyValue<bool>(3);
#if FAMISTUDIO_MACOS
                Settings.ReverseTrackPad = pageUI.GetPropertyValue<bool>(4);
                var followSequencerString = pageUI.GetPropertyValue<string>(5);
#else
                var followSequencerString = pageUI.GetPropertyValue<string>(4);
#endif
                Settings.FollowSequencer = Array.IndexOf(FollowSequencerStrings, followSequencerString);

                // Sound
                Settings.InstrumentStopTime = pageSound.GetPropertyValue<int>(0);
                Settings.SquareSmoothVibrato = pageSound.GetPropertyValue<bool>(1);

                // MIDI
                var pageMIDI = pages[(int)ConfigSection.MIDI];

                Settings.MidiDevice = pageMIDI.GetPropertyValue<string>(0);

                Settings.Save();
            }

            return dialogResult;
        }
    }
}
