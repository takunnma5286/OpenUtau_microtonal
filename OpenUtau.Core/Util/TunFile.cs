using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace OpenUtau.Core.Util {
    /*
     * Ported/Adapted from AnaMark Tuning Library
     * 
     * MIT License
     * 
     * Copyright (C) 2009 Mark Henning, Germany, http://www.mark-henning.de
     * 
     */

    public static class TunFile {
        public static MicrotonalConfig Load(string filePath) {
            var config = new MicrotonalConfig();
            config.TuningMap = new double[128];

            // Initialize with standard 12-ET just in case
            for (int i = 0; i < 128; i++) {
                config.TuningMap[i] = 440.0 * Math.Pow(2, (i - 69) / 12.0);
            }

            double baseFreq = 440.0;
            bool insideExactTuning = false;

            var lines = File.ReadAllLines(filePath);
            foreach (var line in lines) {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";")) {
                    continue;
                }

                if (trimmed.StartsWith("[") && trimmed.EndsWith("]")) {
                    string section = trimmed.Substring(1, trimmed.Length - 2);
                    insideExactTuning = section.Equals("Exact Tuning", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (insideExactTuning) {
                    var parts = trimmed.Split('=');
                    if (parts.Length == 2) {
                        string key = parts[0].Trim();
                        string value = parts[1].Trim();

                        if (key.Equals("BaseFreq", StringComparison.OrdinalIgnoreCase)) {
                            if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double freq)) {
                                baseFreq = freq;
                                config.ConcertPitch = baseFreq; // Update config base pitch
                            }
                        } else if (key.StartsWith("Note ")) {
                            string noteIndexStr = key.Substring(5).Trim();
                            if (int.TryParse(noteIndexStr, out int noteIndex) &&
                                double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double cents)) {
                                if (noteIndex >= 0 && noteIndex < 128) {
                                    // Formula: Freq = BaseFreq * 2^(Cents/1200)
                                    config.TuningMap[noteIndex] = baseFreq * Math.Pow(2, cents / 1200.0);
                                }
                            }
                        }
                    }
                }
            }

            return config;
        }
    }
}
