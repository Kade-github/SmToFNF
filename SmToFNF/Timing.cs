using System.Collections.Generic;
using SMParser;

namespace SmToFnF;

public class Timing
{
    public static List<int> startTimestamps = new List<int>();
    
    public static void CalculateStartTimestamps(ref SMFile file)
    {
        startTimestamps.Clear();
        startTimestamps.Add(0);
        
        if (file.timingPoints.Count <= 1)
            return;
        
        foreach (var timingPoint in file.timingPoints.Skip(1)) // Skip the first timing point
        {
            var previous = file.timingPoints[file.timingPoints.IndexOf(timingPoint) - 1];
            
            var time = (timingPoint.startBeat - previous.startBeat) / (previous.bpm / 60) * 1000;
            startTimestamps.Add(startTimestamps.Last() + (int)time);
        }
    }
    
    public static int GetTimeFromBeat(float beat, ref SMFile file)
    {
        int i = 0;
        foreach (var timingPoint in file.timingPoints)
        {
            float endBeat = float.MaxValue;
            if (i + 1 < file.timingPoints.Count)
                endBeat = file.timingPoints[i + 1].startBeat;
            
            if (timingPoint.startBeat <= beat && endBeat > beat)
            {
                int b = (int)((beat - timingPoint.startBeat) / (timingPoint.bpm / 60) * 1000);
                return startTimestamps[i] + b;
            }
            i++;
        }

        return -1;
    }
}