using System.Collections.Generic;

namespace SmToFnf.JsonStuff
{
    public class Characters
    {
        public string player { get; set; }
        public string girlfriend { get; set; }
        public string opponent { get; set; }
        public List<string> altInstrumentals { get; set; }
    }

    public class PlayData
    {
        public Dictionary<string, int> ratings { get; set; }
        public List<string> songVariations { get; set; }
        public List<string> difficulties { get; set; }
        public Characters characters { get; set; }
        public string stage { get; set; }
        public string noteStyle { get; set; }
        public string album { get; set; }
        public int previewStart { get; set; }
    }

    public class Offsets
    {
        public float instrumental { get; set; }
        public Dictionary<string, float> vocals { get; set; }
    }

    public class FNFMetadata
    {
        public string version { get; set; }
        public string songName { get; set; }
        public string artist { get; set; }
        public string charter { get; set; }
        public string timeFormat { get; set; }
        public Offsets offsets { get; set; }
        public List<TimeChange> timeChanges { get; set; }
        public PlayData playData { get; set; }
        public string generatedBy { get; set; }
    }

    public class TimeChange
    {
        public int t { get; set; }
        public float b { get; set; }
        public int bpm { get; set; }
        public int n { get; set; }
        public int d { get; set; }
    }


}