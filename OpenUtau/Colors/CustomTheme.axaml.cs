using System;
using System.IO;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using OpenUtau.Core;

namespace OpenUtau.Colors;
public class CustomTheme {
    public static ThemeYaml Default;

    static CustomTheme() {
        Load();
        if (Default == null) {
            Default = new ThemeYaml();
        }
    }

    public static void Load() {
        if (File.Exists(PathManager.Inst.ThemeFilePath)) {
            Default = Yaml.DefaultDeserializer.Deserialize<ThemeYaml>(File.ReadAllText(PathManager.Inst.ThemeFilePath,
                Encoding.UTF8));
        } else {
            Save();
        }
    }

    public static void Save() {
        PathManager path = new PathManager();
        Default = new ThemeYaml();
        Directory.CreateDirectory(path.DataPath);
        File.WriteAllText(path.ThemeFilePath, Yaml.DefaultSerializer.Serialize(Default), Encoding.UTF8);
    }

    public static void ApplyTheme(Avalonia.Controls.ResourceDictionary resDict) {
        Load();
        resDict["IsDarkMode"] = Default.IsDarkMode;
        resDict["BackgroundColor"] = Color.Parse($"{Default.BackgroundColor}");
        resDict["BackgroundColorPointerOver"] = Color.Parse($"{Default.BackgroundColorPointerOver}");
        resDict["BackgroundColorPressed"] = Color.Parse($"{Default.BackgroundColorPressed}");
        resDict["BackgroundColorDisabled"] = Color.Parse($"{Default.BackgroundColorDisabled}");

        resDict["ForegroundColor"] = Color.Parse($"{Default.ForegroundColor}");
        resDict["ForegroundColorPointerOver"] = Color.Parse($"{Default.ForegroundColorPointerOver}");
        resDict["ForegroundColorPressed"] = Color.Parse($"{Default.ForegroundColorPressed}");
        resDict["ForegroundColorDisabled"] = Color.Parse($"{Default.ForegroundColorDisabled}");

        resDict["BorderColor"] = Color.Parse($"{Default.BorderColor}");
        resDict["BorderColorPointerOver"] = Color.Parse($"{Default.BorderColorPointerOver}");

        resDict["SystemAccentColor"] = Color.Parse($"{Default.SystemAccentColor}");
        resDict["SystemAccentColorLight1"] = Color.Parse($"{Default.SystemAccentColorLight1}");
        resDict["SystemAccentColorDark1"] = Color.Parse($"{Default.SystemAccentColorDark1}");

        resDict["NeutralAccentColor"] = Color.Parse($"{Default.NeutralAccentColor}");
        resDict["NeutralAccentColorPointerOver"] = Color.Parse($"{Default.NeutralAccentColorPointerOver}");
        resDict["AccentColor1"] = Color.Parse($"{Default.AccentColor1}");
        resDict["AccentColor2"] = Color.Parse($"{Default.AccentColor2}");
        resDict["AccentColor3"] = Color.Parse($"{Default.AccentColor3}");

        resDict["TickLineColor"] = Color.Parse($"{Default.TickLineColor}");
        resDict["BarNumberColor"] = Color.Parse($"{Default.BarNumberColor}");
        resDict["FinalPitchColor"] = Color.Parse($"{Default.FinalPitchColor}");
        resDict["TrackBackgroundAltColor"] = Color.Parse($"{Default.TrackBackgroundAltColor}");

        resDict["WhiteKeyColorLeft"] = Color.Parse($"{Default.WhiteKeyColorLeft}");
        resDict["WhiteKeyColorRight"] = Color.Parse($"{Default.WhiteKeyColorRight}");
        resDict["WhiteKeyNameColor"] = Color.Parse($"{Default.WhiteKeyNameColor}");

        resDict["CenterKeyColorLeft"] = Color.Parse($"{Default.CenterKeyColorLeft}");
        resDict["CenterKeyColorRight"] = Color.Parse($"{Default.CenterKeyColorRight}");
        resDict["CenterKeyNameColor"] = Color.Parse($"{Default.CenterKeyNameColor}");

        resDict["BlackKeyColorLeft"] = Color.Parse($"{Default.BlackKeyColorLeft}");
        resDict["BlackKeyColorRight"] = Color.Parse($"{Default.BlackKeyColorRight}");
        resDict["BlackKeyNameColor"] = Color.Parse($"{Default.BlackKeyNameColor}");
    }

    [Serializable]
    public class ThemeYaml {
        public string Name = "Custom YAML";

        public bool IsDarkMode = false;
        public string BackgroundColor = "#FFFFFF";
        public string BackgroundColorPointerOver = "#F0F0F0";
        public string BackgroundColorPressed = "#E0E0E0";
        public string BackgroundColorDisabled = "#D0D0D0";

        public string ForegroundColor = "#000000";
        public string ForegroundColorPointerOver = "#000000";
        public string ForegroundColorPressed = "#202020";
        public string ForegroundColorDisabled = "#808080";

        public string BorderColor = "#707070";
        public string BorderColorPointerOver = "#B0B0B0";

        public string SystemAccentColor = "#4EA6EA";
        public string SystemAccentColorLight1 = "#90CAF9";
        public string SystemAccentColorDark1 = "#1E88E5";

        public string NeutralAccentColor = "#ADA1B3";
        public string NeutralAccentColorPointerOver = "#948A99";
        public string AccentColor1 = "#4EA6EA";
        public string AccentColor2 = "#FF679D";
        public string AccentColor3 = "#E62E6E";

        public string TickLineColor = "#AFA3B5";
        public string BarNumberColor = "#AFA3B5";
        public string FinalPitchColor = "#C0C0C0";
        public string TrackBackgroundAltColor = "#F0F0F0";

        public string WhiteKeyColorLeft = "Transparent";
        public string WhiteKeyColorRight = "Transparent";
        public string WhiteKeyNameColor = "#FF347c";

        public string CenterKeyColorLeft = "#FFDDE6";
        public string CenterKeyColorRight = "#FFCEDC";
        public string CenterKeyNameColor = "#FF347C";

        public string BlackKeyColorLeft = "#FF71A3";
        public string BlackKeyColorRight = "#FF347C";
        public string BlackKeyNameColor = "#FFFFFF";
    }
}

