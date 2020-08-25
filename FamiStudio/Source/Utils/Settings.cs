﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FamiStudio
{
    static class Settings
    {
        // User Interface section
        public static int DpiScaling = 0;
        public static int TimeFormat = 0;
        public static bool CheckUpdates = true;
        public static int FollowSequencer = 0;
        public static bool TrackPadControls = false;
        public static bool ReverseTrackPad = false;

        // Audio section
        public static int InstrumentStopTime = 2;
        public static bool SquareSmoothVibrato = true;

        // MIDI section
        public static string MidiDevice = "";

        // Last used folders
        public static string LastFileFolder = "";
        public static string LastInstrumentFolder  = "";
        public static string LastSampleFolder = "";

        public static void Load()
        {
            var ini = new IniFile();
            ini.Load(GetConfigFileName());

            DpiScaling = ini.GetInt("UI", "DpiScaling", 0);
            TimeFormat = ini.GetInt("UI", "TimeFormat", 0);
            CheckUpdates = ini.GetBool("UI", "CheckUpdates", true);
            TrackPadControls = ini.GetBool("UI", "TrackPadControls", false);
            ReverseTrackPad = ini.GetBool("UI", "ReverseTrackPad", false);
            FollowSequencer = ini.GetInt("UI", "FollowSequencer", 0);
            InstrumentStopTime = ini.GetInt("Audio", "InstrumentStopTime", 2);
            MidiDevice = ini.GetString("MIDI", "Device", "");
            SquareSmoothVibrato = ini.GetBool("Audio", "SquareSmoothVibrato", true);
            LastFileFolder = ini.GetString("Folders", "LastFileFolder", "");
            LastInstrumentFolder = ini.GetString("Folders", "LastInstrumentFolder", "");
            LastSampleFolder = ini.GetString("Folders", "LastSampleFolder", "");

            if (DpiScaling != 100 && DpiScaling != 150 && DpiScaling != 200)
                DpiScaling = 0;

            InstrumentStopTime = Utils.Clamp(InstrumentStopTime, 0, 10);

            if (MidiDevice == null)
                MidiDevice = "";
            if (!Directory.Exists(LastFileFolder))
                LastFileFolder = "";
            if (!Directory.Exists(LastInstrumentFolder))
                LastInstrumentFolder = "";
            if (!Directory.Exists(LastSampleFolder))
                LastSampleFolder = "";
        }

        public static void Save()
        {
            var ini = new IniFile();

            ini.SetInt("UI", "DpiScaling", DpiScaling);
            ini.SetInt("UI", "TimeFormat", TimeFormat);
            ini.SetBool("UI", "CheckUpdates", CheckUpdates);
            ini.SetBool("UI", "TrackPadControls", TrackPadControls);
            ini.SetBool("UI", "ReverseTrackPad", ReverseTrackPad);
            ini.SetInt("UI", "FollowSequencer", FollowSequencer);
            ini.SetInt("Audio", "InstrumentStopTime", InstrumentStopTime);
            ini.SetBool("Audio", "SquareSmoothVibrato", SquareSmoothVibrato);
            ini.SetString("MIDI", "Device", MidiDevice);
            ini.SetString("Folders", "LastFileFolder", LastFileFolder);
            ini.SetString("Folders", "LastInstrumentFolder", LastInstrumentFolder);
            ini.SetString("Folders", "LastSampleFolder", LastSampleFolder);

            Directory.CreateDirectory(GetConfigFilePath());

            ini.Save(GetConfigFileName());
        }

        private static string GetConfigFilePath()
        {
#if FAMISTUDIO_WINDOWS
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FamiStudio");
#elif FAMISTUDIO_LINUX
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config/FamiStudio");
#else
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library/Application Support/FamiStudio");
#endif
        }

        private static string GetConfigFileName()
        {
            return Path.Combine(GetConfigFilePath(), "FamiStudio.ini");
        }
    }
}
