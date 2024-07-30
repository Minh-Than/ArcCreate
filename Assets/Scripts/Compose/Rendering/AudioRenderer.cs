using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArcCreate.Gameplay.Data;
using ArcCreate.SceneTransition;
using NAudio.Wave;
using UnityEngine;

namespace ArcCreate.Compose.Rendering
{
    public class AudioRenderer
    {
        private const string V = "Writing to ";
        private readonly AudioClip songAudio;
        private readonly AudioClip tapAudio;
        private readonly AudioClip arcAudio;
        private readonly AudioClip renderStartAudio;
        private readonly AudioClip gameplayLoadCompleteAudio;
        private readonly Dictionary<string, AudioClip> sfxAudio;
        private readonly int startTiming;
        private readonly int endTiming;
        private readonly int audioOffset;
        private readonly bool showTransition;
        private readonly RenderSetting settings;
        private readonly TransitionSequence transitionSequence;

        public AudioRenderer(
            int startTiming,
            int endTiming,
            int audioOffset,
            AudioClip songAudio,
            AudioClip tapAudio,
            AudioClip arcAudio,
            AudioClip renderStartAudio,
            AudioClip gameplayLoadCompleteAudio,
            Dictionary<string, AudioClip> sfxAudio,
            bool showTransition,
            RenderSetting settings,
            TransitionSequence transitionSequence)
        {
            this.startTiming = startTiming;
            this.endTiming = endTiming;
            this.audioOffset = audioOffset;
            this.songAudio = songAudio;
            this.tapAudio = tapAudio;
            this.arcAudio = arcAudio;
            this.renderStartAudio = renderStartAudio;
            this.gameplayLoadCompleteAudio = gameplayLoadCompleteAudio;
            this.sfxAudio = sfxAudio;
            this.showTransition = showTransition;
            this.transitionSequence = transitionSequence;
            this.settings = settings;
        }

        public List<string> SfxAudioList => sfxAudio.Keys.ToList();

        public void CreateAudio()
        {
            // Get list of notes
            Dictionary<string, List<Note>> sfxSound = new Dictionary<string, List<Note>>();

            int startRange = startTiming - audioOffset;
            int endRange = endTiming - audioOffset;

            Gameplay.IChartControl chart = Services.Gameplay.Chart;
            IEnumerable<Tap> taps = chart.GetAll<Tap>().Where((n) => n.Timing >= startRange && n.Timing <= endRange && !n.NoInput);
            IEnumerable<Hold> holds = chart.GetAll<Hold>().Where((n) => n.Timing >= startRange && n.Timing <= endRange && !n.NoInput);
            IEnumerable<Arc> arcs = chart.GetAll<Arc>().Where((n) => n.Timing >= startRange && n.Timing <= endRange && n.Timing < n.EndTiming && !n.IsTrace && !n.NoInput);
            IEnumerable<ArcTap> arctaps = chart.GetAll<ArcTap>().Where((n) => n.Timing >= startRange && n.Timing <= endRange && !n.NoInput);

            HashSet<int> tapSoundTimings = new HashSet<int>(taps.Select(n => n.Timing).Union(holds.Select(h => h.Timing)));
            HashSet<int> arcSoundTimings = new HashSet<int>(arcs.Select(a => a.Timing));

            foreach (ArcTap at in arctaps)
            {
                if (sfxAudio.ContainsKey(at.Sfx))
                {
                    if (sfxSound.ContainsKey(at.Sfx))
                    {
                        sfxSound[at.Sfx].Add(at);
                    }
                    else if (!string.IsNullOrEmpty(at.Sfx) && at.Sfx != "none")
                    {
                        sfxSound.Add(at.Sfx, new List<Note> { at });
                    }
                }
                else
                {
                    arcSoundTimings.Add(at.Timing);
                }
            }

            Debug.Log("Creating tapsound audio for " + (tapSoundTimings.Count + arcSoundTimings.Count + sfxSound.Count) + " notes.");

            int channels = tapAudio.channels;
            int frequency = tapAudio.frequency;
            if (arcAudio.channels != channels || renderStartAudio.channels != channels || gameplayLoadCompleteAudio.channels != channels
            || arcAudio.frequency != frequency || renderStartAudio.frequency != frequency || gameplayLoadCompleteAudio.frequency != frequency)
            {
                throw new Exception("Internal audio error: Internal sfx wav have differing frequency and channels count");
            }

            // Get raw byte array from audio clips
            float[] tap = GetSamples(tapAudio);
            float[] arc = GetSamples(arcAudio);
            Dictionary<string, float[]> sfx = new Dictionary<string, float[]>();
            foreach (KeyValuePair<string, AudioClip> sa in sfxAudio)
            {
                sfx.Add(sa.Key, GetSamples(sa.Value));
            }

            float[] renderStart = GetSamples(renderStartAudio);
            float[] gameplayLoadComplete = GetSamples(gameplayLoadCompleteAudio);
            float effectVolume = settings.EffectVolume;
            if (Settings.InputMode.Value != (int)InputMode.Auto && Settings.InputMode.Value != (int)InputMode.AutoController)
            {
                effectVolume = 0;
            }

            // Combine
            float audioLength = (endTiming - startTiming) / 1000f;
            if (showTransition)
            {
                audioLength += transitionSequence.FullSequenceSeconds;
            }

            float[] samples = new float[(int)(audioLength * frequency) * channels];
            Debug.Log("Prepared samples array of size " + samples.Length);

            Dictionary<string, float[]> sfxSamples = new Dictionary<string, float[]>();
            foreach (KeyValuePair<string, AudioClip> s in sfxAudio)
            {
                AudioClip clip = s.Value;
                if (clip.channels != channels || clip.frequency != frequency)
                {
                    throw new Exception(I18n.S("Compose.Exception.Render.Audio.IncompatibleSfx", new Dictionary<string, object>
                    {
                        { "Sfx", s.Key },
                        { "ExpectedChannels", channels },
                        { "ExpectedFrequency", frequency },
                        { "Channels", clip.channels },
                        { "Frequency", clip.frequency },
                    }));
                }

                sfxSamples.Add(s.Key, new float[(int)(audioLength * s.Value.frequency) * s.Value.channels]);
                Debug.Log($"Prepared {s.Key} sfx samples array of size: {sfxSamples[s.Key].Length}");
            }

            if (showTransition)
            {
                for (int i = 0; i < renderStart.Length; i++)
                {
                    samples[i] += renderStart[i];
                }

                int shutterOpenOffset =
                    (int)((transitionSequence.ShowDurationSeconds + transitionSequence.WaitDurationSeconds) * frequency) * channels;
                for (int i = 0; i < gameplayLoadComplete.Length; i++)
                {
                    samples[i + shutterOpenOffset] += gameplayLoadComplete[i];
                }
            }

            foreach (int t in tapSoundTimings)
            {
                int start = TimingToSampleIndex(t, channels, frequency);
                for (int i = 0; i < tap.Length; i++)
                {
                    if (start + i < samples.Length)
                    {
                        samples[start + i] += tap[i] * effectVolume;
                    }
                }
            }

            foreach (int t in arcSoundTimings)
            {
                int start = TimingToSampleIndex(t, channels, frequency);
                for (int i = 0; i < arc.Length; i++)
                {
                    if (start + i < samples.Length)
                    {
                        samples[start + i] += arc[i] * effectVolume;
                    }
                }
            }

            foreach (KeyValuePair<string, List<Note>> sound in sfxSound)
            {
                if (!sfxAudio.TryGetValue(sound.Key, out AudioClip clip))
                {
                    clip = arcAudio;
                }

                foreach (Note n in sound.Value)
                {
                    int start = TimingToSampleIndex(n.Timing, clip.channels, clip.frequency);
                    for (int i = 0; i < sfx[sound.Key].Length; i++)
                    {
                        if (start + i < sfxSamples[sound.Key].Length)
                        {
                            sfxSamples[sound.Key][start + i] += sfx[sound.Key][i];
                        }
                    }
                }
            }

            /* Normal Note */
            byte[] byteData = new byte[samples.Length * 2];
            Debug.Log("Converting to raw bytes");

            for (int i = 0; i < samples.Length; i++)
            {
                int intData = (short)(samples[i] * short.MaxValue);
                byte[] sample = BitConverter.GetBytes(intData);
                byteData[i * 2] = sample[0];
                byteData[(i * 2) + 1] = sample[1];
            }

            // Write the sfx wav file
            Debug.Log(V + GetPath("sfx.wav"));
            MemoryStream memoryStream = new MemoryStream(byteData);
            WaveFormat format = new WaveFormat(frequency, 16, channels);
            IWaveProvider wave = new RawSourceWaveStream(memoryStream, format);
            WaveFileWriter.CreateWaveFile(GetPath("sfx.wav"), wave);

            /* Sfx Sky Note */
            foreach (KeyValuePair<string, float[]> sfxsample in sfxSamples)
            {
                byteData = new byte[sfxsample.Value.Length * 2];
                Debug.Log($"Converting to raw bytes (sfx: {sfxsample.Key})");

                for (int i = 0; i < sfxsample.Value.Length; i++)
                {
                    int intData = (short)(sfxsample.Value[i] * short.MaxValue);
                    byte[] sample = BitConverter.GetBytes(intData);
                    byteData[i * 2] = sample[0];
                    byteData[(i * 2) + 1] = sample[1];
                }

                // Write the sfx wav file
                Debug.Log("Writing to " + GetPath($"sfx_{sfxsample.Key}.wav"));
                memoryStream = new MemoryStream(byteData);
                format = new WaveFormat(sfxAudio[sfxsample.Key].frequency, 16, sfxAudio[sfxsample.Key].channels);
                wave = new RawSourceWaveStream(memoryStream, format);
                WaveFileWriter.CreateWaveFile(GetPath($"sfx_{sfxsample.Key}.wav"), wave);
            }

            // Get song data
            Debug.Log("Cutting track from " + startTiming + " to " + endTiming);
            float[] songSamples = GetSongSamples(songAudio);
            byte[] songBytes = new byte[songSamples.Length * 2];
            float songVolume = settings.MusicVolume;

            Debug.Log("Converting to raw bytes");
            for (int i = 0; i < songSamples.Length; i++)
            {
                int intData = (short)(songSamples[i] * songVolume * short.MaxValue);
                byte[] sample = BitConverter.GetBytes(intData);
                songBytes[i * 2] = sample[0];
                songBytes[(i * 2) + 1] = sample[1];
            }

            // Write song file
            Debug.Log("Writing to " + GetPath("song.wav"));
            memoryStream = new MemoryStream(songBytes);
            format = new WaveFormat(songAudio.frequency, 16, songAudio.channels);
            wave = new RawSourceWaveStream(memoryStream, format);
            WaveFileWriter.CreateWaveFile(GetPath("song.wav"), wave);
        }

        private int TimingToSampleIndex(int timing, int channels, int frequency)
        {
            int offset = audioOffset;
            if (showTransition)
            {
                offset += transitionSequence.FullSequenceMs;
            }

            int samples = (int)((timing - startTiming + offset) / 1000f * frequency);
            return samples * channels;
        }

        private float[] GetSamples(AudioClip clip)
        {
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            return samples;
        }

        private float[] GetSongSamples(AudioClip clip)
        {
            int start = startTiming;
            int offset = showTransition ? transitionSequence.FullSequenceMs : 0;
            if (start < 0)
            {
                offset += -start;
                start = 0;
            }

            float audioLength = (endTiming - start) / 1000f;
            audioLength += offset / 1000f;

            float[] samples = new float[(int)(audioLength * clip.frequency) * clip.channels];

            int readOffset = (int)(start / 1000f * clip.frequency);

            clip.GetData(samples, readOffset);

            // shift forward
            if (offset > 0)
            {
                int shiftIndex = (int)(offset / 1000f * clip.frequency) * clip.channels;
                for (int i = samples.Length - 1; i >= shiftIndex; i--)
                {
                    samples[i] = samples[i - shiftIndex];
                }

                for (int i = 0; i < shiftIndex; i++)
                {
                    samples[i] = 0;
                }
            }

            return samples;
        }

        private string GetPath(string fileName = "")
        {
            string path = Path.Combine(Path.GetDirectoryName(Services.Project.CurrentProject.Path), ".rendering", fileName);
            if (!Directory.Exists(Path.GetDirectoryName(path)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            }

            return path;
        }
    }
}