using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SmToFnf.JsonStuff
{
    public class Note
    {
        public double t { get; set; }

        public int d { get; set; }

        public double l { get; set; }

        public string k { get; set; }
    }
    
    public class Event
    {
        public double t { get; set; }
        public string e { get; set; }
        public dynamic v { get; set; }
    }
    
    public class FNFSong
    {
        public string version { get; set; }
        public Dictionary<string, float> scrollSpeed { get; set; }
        public Dictionary<string, List<Note>> notes { get; set; }
        public List<Event> events { get; set; }
        public string generatedBy { get; set; }
    }

}