﻿using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

#if FAMISTUDIO_WINDOWS
using AudioStream = FamiStudio.XAudio2Stream;
#else
using AudioStream = FamiStudio.PortAudioStream;
#endif

namespace FamiStudio
{
    public enum LoopMode
    {
        None,
        Song,
        Pattern,
        Max
    };

    public class BasePlayer
    {
        protected const int SampleRate = 44100;

        // NSTC: 734 = ceil(SampleRate / FrameRate) = ceil(44100 / 60.0988).
        // PAL:  882 = ceil(SampleRate / FrameRate) = ceil(44100 / 50.0070).
        protected const int BufferSize = 882 * sizeof(short);
        protected const int NumAudioBuffers = 3;

        protected int apuIndex;
        protected NesApu.DmcReadDelegate dmcCallback;
        protected int tempoCounter = 0;
        protected int playPattern = 0;
        protected int playNote = 0;
        protected int speed = 6;
        protected int palFramePattern = 0;
        protected bool famitrackerTempo = true;
        protected bool palMode = false;
        protected bool firstFrame = false;
        protected Song song;
        protected ChannelState[] channelStates;
        protected LoopMode loopMode = LoopMode.Song;
        protected int channelMask = 0xffff;
        protected int playPosition = 0;

        protected BasePlayer(int apu)
        {
            apuIndex = apu;
            dmcCallback = new NesApu.DmcReadDelegate(NesApu.DmcReadCallback);
        }

        public virtual void Shutdown()
        {
        }

        public int ChannelMask
        {
            get { return channelMask; }
            set { channelMask = value; }
        }

        public LoopMode Loop
        {
            get { return loopMode; }
            set { loopMode = value; }
        }

        public int CurrentFrame
        {
            get { return Math.Max(0, playPosition); }
            set { playPosition = value; }
        }

        public int GetNumFramesToRun()
        {
            if (famitrackerTempo)
            {
                return 1;
            }
            else
            {
                if (tempoCounter <= 0)
                {
                    tempoCounter = 11;
                    palFramePattern = 0x104 << 1;
                }
                tempoCounter--;
                palFramePattern >>= 1;

                return palMode && ((palFramePattern & 1) != 0) ? 2 : 1;
            }
        }

        public bool UpdateTempo(int speed, int tempo)
        {
            if (famitrackerTempo)
            {
                // Tempo/speed logic straight from Famitracker.
                var tempoDecrement = (tempo * 24) / speed;
                var tempoRemainder = (tempo * 24) % speed;

                if (tempoCounter <= 0)
                {
                    int ticksPerSec = palMode ? 50 : 60;
                    tempoCounter += (60 * ticksPerSec) - tempoRemainder;
                }
                tempoCounter -= tempoDecrement;

                return tempoCounter <= 0;
            }
            else
            {
                return true;
            }
        }

        public void UpdateTempo()
        {

        }

        public bool BeginPlaySong(Song s, bool pal, int startNote)
        {
            song = s;
            famitrackerTempo = song.Project.TempoMode == Project.TempoFamiTracker;
            speed = song.FamitrackerSpeed;
            palMode = pal;
            playPosition = startNote;
            playPattern = 0;
            playNote = 0;
            tempoCounter = 0;
            firstFrame = true;
            channelStates = CreateChannelStates(song.Project, apuIndex, song.Project.ExpansionNumChannels, palMode);

            NesApu.InitAndReset(apuIndex, SampleRate, palMode, GetNesApuExpansionAudio(song.Project), song.Project.ExpansionNumChannels, dmcCallback);

            if (startNote != 0)
            {
                NesApu.StartSeeking(apuIndex);
#if DEBUG
                NesApu.seeking = true;
#endif

                while (song.GetPatternStartNote(playPattern) + playNote < startNote)
                {
                    foreach (var channel in channelStates)
                    {
                        channel.Advance(song, playPattern, playNote);
                        channel.ProcessEffects(song, playPattern, playNote,ref speed);
                        channel.UpdateEnvelopes();
                        channel.UpdateAPU();
                    }

                    if (!AdvanceSong(song.Length, loopMode))
                        return false;
                }

                NesApu.StopSeeking(apuIndex);
#if DEBUG
                NesApu.seeking = false;
#endif
            }

            return true;
        }

        public bool PlaySongFrame()
        {
            int numFrames = GetNumFramesToRun();

            for (int i = 0; i < numFrames; i++)
            {
                if (firstFrame || UpdateTempo(speed, song.FamitrackerTempo))
                //if (UpdateFamistudioTempo(6, ref tempoCounter, ref numFrames))
                {
                    // Advance to next note.
                    if (!firstFrame && !AdvanceSong(song.Length, loopMode))
                        return false;

                    foreach (var channel in channelStates)
                    {
                        channel.Advance(song, playPattern, playNote);
                        channel.ProcessEffects(song, playPattern, playNote, ref speed);
                    }

                    playPosition = song.GetPatternStartNote(playPattern) + playNote;
                    firstFrame = false;
                }

                // Update envelopes + APU registers.
                foreach (var channel in channelStates)
                {
                    channel.UpdateEnvelopes();
                    channel.UpdateAPU();
                }
            }

            // Mute.
            for (int i = 0; i < channelStates.Length; i++)
            {
                NesApu.EnableChannel(apuIndex, i, (channelMask & (1 << i)));
            }

            EndFrame();

            return true;
        }

        public bool AdvanceSong(int songLength, LoopMode loopMode)
        {
            if (++playNote >= song.GetPatternLength(playPattern))
            {
                playNote = 0;
                if (loopMode != LoopMode.Pattern)
                    playPattern++;
            }

            if (playPattern >= songLength)
            {
                if (loopMode == LoopMode.None)
                {
                    if (song.LoopPoint >= 0)
                    {
                        playPattern = song.LoopPoint;
                        playNote = 0;
                    }
                    else
                    {
                        return false;
                    }
                }
                else if (loopMode == LoopMode.Song)
                {
                    playPattern = Math.Max(0, song.LoopPoint);
                    playNote = 0;
                }
            }

            return true;
        }

        private ChannelState CreateChannelState(int apuIdx, int channelType, int expNumChannels, bool pal)
        {
            switch (channelType)
            {
                case Channel.Square1:
                case Channel.Square2:
                    return new ChannelStateSquare(apuIdx, channelType, pal);
                case Channel.Triangle:
                    return new ChannelStateTriangle(apuIdx, channelType, pal);
                case Channel.Noise:
                    return new ChannelStateNoise(apuIdx, channelType, pal);
                case Channel.Dpcm:
                    return new ChannelStateDpcm(apuIdx, channelType, pal);
                case Channel.Vrc6Square1:
                case Channel.Vrc6Square2:
                    return new ChannelStateVrc6Square(apuIdx, channelType);
                case Channel.Vrc6Saw:
                    return new ChannelStateVrc6Saw(apuIdx, channelType);
                case Channel.Vrc7Fm1:
                case Channel.Vrc7Fm2:
                case Channel.Vrc7Fm3:
                case Channel.Vrc7Fm4:
                case Channel.Vrc7Fm5:
                case Channel.Vrc7Fm6:
                    return new ChannelStateVrc7(apuIdx, channelType);
                case Channel.FdsWave:
                    return new ChannelStateFds(apuIdx, channelType);
                case Channel.Mmc5Square1:
                case Channel.Mmc5Square2:
                    return new ChannelStateMmc5Square(apuIdx, channelType);
                case Channel.N163Wave1:
                case Channel.N163Wave2:
                case Channel.N163Wave3:
                case Channel.N163Wave4:
                case Channel.N163Wave5:
                case Channel.N163Wave6:
                case Channel.N163Wave7:
                case Channel.N163Wave8:
                    return new ChannelStateN163(apuIdx, channelType, expNumChannels, pal);
                case Channel.S5BSquare1:
                case Channel.S5BSquare2:
                case Channel.S5BSquare3:
                    return new ChannelStateS5B(apuIdx, channelType, pal);
            }

            Debug.Assert(false);
            return null;
        }

        public ChannelState[] CreateChannelStates(Project project, int apuIdx, int expNumChannels, bool pal)
        {
            var channelCount = project.GetActiveChannelCount();
            var states = new ChannelState[channelCount];

            int idx = 0;
            for (int i = 0; i < Channel.Count; i++)
            {
                if (project.IsChannelActive(i))
                    states[idx++] = CreateChannelState(apuIdx, i, expNumChannels, pal);
            }

            return states;
        }
        
        public int GetNesApuExpansionAudio(Project project)
        {
            switch (project.ExpansionAudio)
            {
                case Project.ExpansionNone:
                    return NesApu.APU_EXPANSION_NONE;
                case Project.ExpansionVrc6:
                    return NesApu.APU_EXPANSION_VRC6;
                case Project.ExpansionVrc7:
                    return NesApu.APU_EXPANSION_VRC7;
                case Project.ExpansionFds:
                    return NesApu.APU_EXPANSION_FDS;
                case Project.ExpansionMmc5:
                    return NesApu.APU_EXPANSION_MMC5;
                case Project.ExpansionN163:
                    return NesApu.APU_EXPANSION_NAMCO;
                case Project.ExpansionS5B:
                    return NesApu.APU_EXPANSION_SUNSOFT;
            }

            Debug.Assert(false);
            return 0;
        }

        protected virtual unsafe short[] EndFrame()
        {
            NesApu.EndFrame(apuIndex);

            int numTotalSamples = NesApu.SamplesAvailable(apuIndex);
            short[] samples = new short[numTotalSamples];

            fixed (short* ptr = &samples[0])
            {
                NesApu.ReadSamples(apuIndex, new IntPtr(ptr), numTotalSamples);
            }

            return samples;
        }
    };
}
