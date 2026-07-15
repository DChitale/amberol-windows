using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace AmberolWpf.Helpers
{
    public class LrcWord
    {
        public TimeSpan Time { get; set; }
        public string Text { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class LrcLine
    {
        public TimeSpan Time { get; set; }
        public string Text { get; set; }
        public List<LrcWord> Words { get; set; } = new List<LrcWord>();
        public bool HasWordSync => Words != null && Words.Count > 0;
    }

    public class LrcParser
    {
        private static readonly Regex LrcTimeRegex = new Regex(@"\[(\d+):(\d+)(?:\.(\d+))?\]", RegexOptions.Compiled);
        private static readonly Regex LrcWordTimeRegex = new Regex(@"<(\d+):(\d+)(?:\.(\d+))?>", RegexOptions.Compiled);

        public static List<LrcLine> Parse(string lrcText)
        {
            var lines = new List<LrcLine>();
            if (string.IsNullOrEmpty(lrcText)) return lines;

            using (var reader = new StringReader(lrcText))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    // Match all timestamps at the start of the line
                    var matches = LrcTimeRegex.Matches(line);
                    if (matches.Count == 0) continue;

                    // The lyric text is everything after the last timestamp closing bracket
                    int lastMatchEnd = 0;
                    foreach (Match match in matches)
                    {
                        if (match.Index + match.Length > lastMatchEnd)
                        {
                            lastMatchEnd = match.Index + match.Length;
                        }
                    }

                    string text = line.Substring(lastMatchEnd).Trim();
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    // Parse each timestamp and add a lyric line for it
                    foreach (Match match in matches)
                    {
                        try
                        {
                            int minutes = int.Parse(match.Groups[1].Value);
                            int seconds = int.Parse(match.Groups[2].Value);
                            int milliseconds = 0;

                            if (match.Groups[3].Success)
                            {
                                string msStr = match.Groups[3].Value;
                                if (msStr.Length == 2) // centiseconds
                                {
                                    milliseconds = int.Parse(msStr) * 10;
                                }
                                else if (msStr.Length == 3) // milliseconds
                                {
                                    milliseconds = int.Parse(msStr);
                                }
                                else
                                {
                                    milliseconds = int.Parse(msStr.PadRight(3, '0').Substring(0, 3));
                                }
                            }

                            var time = new TimeSpan(0, 0, minutes, seconds, milliseconds);
                            
                            // Parse word-by-word sync if present
                            var words = ParseWords(text, time);
                            string cleanText = text;
                            if (words.Count > 0)
                            {
                                cleanText = LrcWordTimeRegex.Replace(text, "").Replace("  ", " ").Trim();
                            }

                            lines.Add(new LrcLine 
                            { 
                                Time = time, 
                                Text = cleanText,
                                Words = words
                            });
                        }
                        catch
                        {
                            // Skip malformed timestamp
                        }
                    }
                }
            }

            // Sort lines by time
            lines.Sort((a, b) => a.Time.CompareTo(b.Time));

            // Calculate word durations
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (!line.HasWordSync) continue;

                for (int j = 0; j < line.Words.Count; j++)
                {
                    var word = line.Words[j];
                    if (j < line.Words.Count - 1)
                    {
                        word.Duration = line.Words[j + 1].Time - word.Time;
                    }
                    else
                    {
                        // Last word in the line.
                        // Duration is time until next line, capped at 1.5 seconds.
                        TimeSpan nextLineTime = (i < lines.Count - 1) ? lines[i + 1].Time : (line.Time + TimeSpan.FromSeconds(2));
                        TimeSpan diff = nextLineTime - word.Time;
                        if (diff < TimeSpan.Zero) diff = TimeSpan.FromSeconds(0.5);
                        if (diff.TotalSeconds > 1.5) diff = TimeSpan.FromSeconds(0.8);
                        word.Duration = diff;
                    }

                    if (word.Duration <= TimeSpan.Zero)
                    {
                        word.Duration = TimeSpan.FromSeconds(0.2);
                    }

                    // Cap very long word durations to avoid weird infinite sweeps
                    if (word.Duration.TotalSeconds > 2.0)
                    {
                        word.Duration = TimeSpan.FromSeconds(0.5);
                    }
                }
            }

            return lines;
        }

        public static List<LrcWord> ParseWords(string lineText, TimeSpan lineStartTime)
        {
            var words = new List<LrcWord>();
            var matches = LrcWordTimeRegex.Matches(lineText);
            if (matches.Count == 0) return words;

            int lastIdx = 0;
            TimeSpan currentWordTime = lineStartTime;

            for (int i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                // Text before this match belongs to the previous timestamp
                string textSegment = lineText.Substring(lastIdx, match.Index - lastIdx).Trim();
                if (!string.IsNullOrEmpty(textSegment))
                {
                    var parts = textSegment.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        words.Add(new LrcWord { Time = currentWordTime, Text = part });
                    }
                }

                // Parse the next timestamp
                currentWordTime = ParseTagTime(match, lineStartTime);
                lastIdx = match.Index + match.Length;
            }

            // Text after the last match belongs to the last timestamp
            if (lastIdx < lineText.Length)
            {
                string textSegment = lineText.Substring(lastIdx).Trim();
                if (!string.IsNullOrEmpty(textSegment))
                {
                    var parts = textSegment.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        words.Add(new LrcWord { Time = currentWordTime, Text = part });
                    }
                }
            }

            return words;
        }

        private static TimeSpan ParseTagTime(Match match, TimeSpan fallback)
        {
            try
            {
                int minutes = int.Parse(match.Groups[1].Value);
                int seconds = int.Parse(match.Groups[2].Value);
                int milliseconds = 0;

                if (match.Groups[3].Success)
                {
                    string msStr = match.Groups[3].Value;
                    if (msStr.Length == 2)
                    {
                        milliseconds = int.Parse(msStr) * 10;
                    }
                    else if (msStr.Length == 3)
                    {
                        milliseconds = int.Parse(msStr);
                    }
                    else
                    {
                        milliseconds = int.Parse(msStr.PadRight(3, '0').Substring(0, 3));
                    }
                }
                return new TimeSpan(0, 0, minutes, seconds, milliseconds);
            }
            catch
            {
                return fallback;
            }
        }

        public static int GetActiveLineIndex(List<LrcLine> lines, TimeSpan currentTime)
        {
            if (lines == null || lines.Count == 0) return -1;
            if (currentTime < lines[0].Time) return -1;

            for (int i = 0; i < lines.Count; i++)
            {
                if (i == lines.Count - 1)
                {
                    return i;
                }

                if (currentTime >= lines[i].Time && currentTime < lines[i + 1].Time)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
