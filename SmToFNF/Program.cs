using System;
using System.Text.Json;
using SMParser;
using SmToFnf.JsonStuff;

namespace SmToFnF
{
    internal class Program
    {
        public static void PrintArgs()
        {
            Console.WriteLine("SmToFnF.exe - Convert Stepmania files to Friday Night Funkin' files\n    Usage: SmToFnf.exe <input file> [output file]");
            Console.Read();
        }
        
        public static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                PrintArgs();
                return;
            }
            
            // set the current directory to the directory of the executable
            
            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

            var inputPath = args[0];
            var outputPath = args.Length > 1 ? args[1] : null;
            var outputName = "";

            if (outputPath != null)
            {
                // sanatize it
                outputName = Path.GetFileNameWithoutExtension(outputPath);
                outputPath = Path.GetDirectoryName(outputPath);
            }
            
            var smFile = new SMFile(inputPath);
            
            Timing.CalculateStartTimestamps(ref smFile);

            var fnfMetadata = new FNFMetadata();
            
            fnfMetadata.artist = smFile.metadata.artist;
            fnfMetadata.songName = smFile.metadata.title;
            
            if (fnfMetadata.songName == "")
                fnfMetadata.songName = Path.GetFileNameWithoutExtension(inputPath);
            
            fnfMetadata.version = "2.2.4";
            fnfMetadata.generatedBy = "SmToFnF";
            
            fnfMetadata.timeFormat = "ms";
            fnfMetadata.playData = new PlayData();
            fnfMetadata.playData.album = "Stepmania";
            fnfMetadata.playData.characters = new Characters();

            fnfMetadata.playData.characters.player = "bf";
            fnfMetadata.playData.characters.opponent = "dad";
            fnfMetadata.playData.characters.girlfriend = "gf";
            fnfMetadata.playData.characters.altInstrumentals = new List<string>();
            fnfMetadata.playData.difficulties = new List<string>();
            fnfMetadata.playData.noteStyle = "funkin";
            fnfMetadata.playData.previewStart = (int)(smFile.metadata.sampleStart * 1000);
            
            // time changes
            
            var timeChanges = new List<TimeChange>();
            foreach (var timingPoint in smFile.timingPoints)
            {
                var timeChange = new TimeChange();
                timeChange.t = Timing.GetTimeFromBeat(timingPoint.startBeat, ref smFile);
                timeChange.b = timingPoint.startBeat;
                timeChange.bpm = (int)timingPoint.bpm;
                timeChange.n = 4;
                timeChange.d = 4;
                timeChanges.Add(timeChange);
            }
            
            fnfMetadata.timeChanges = timeChanges;
            
            // offsets

            fnfMetadata.offsets = new Offsets();
            fnfMetadata.offsets.instrumental = (-smFile.metadata.offset * 1000);
            fnfMetadata.offsets.vocals = new Dictionary<string, float>();
            fnfMetadata.offsets.vocals.Add("dad", 0);
            fnfMetadata.offsets.vocals.Add("bf", 0);
            
            fnfMetadata.playData.songVariations = new List<string>();
            
            fnfMetadata.playData.ratings = new Dictionary<string, int>();
            
            // stage
            
            fnfMetadata.playData.stage = "mainStage";

            var fnfSong = new FNFSong();
            
            fnfSong.version = "2.0.0";
            fnfSong.generatedBy = "SmToFnF";
            fnfSong.scrollSpeed = new Dictionary<string, float>();
            fnfSong.notes = new Dictionary<string, List<Note>>();
            fnfSong.events = new List<Event>();

            foreach (SMDifficulty diff in smFile.difficulties)
            {
                string d = diff.name.ToLower();
                
                if (d != "hard" && d != "normal" && d  != "easy" && d != "erect")
                {
                    Console.WriteLine($"Skipping difficulty {diff.name}. Not correct difficulty name.");
                    continue;
                }
                
                fnfSong.scrollSpeed.Add(d, 1.6f);
                fnfMetadata.playData.difficulties.Add(d);
                fnfMetadata.playData.ratings.Add(d, 1);

                List<Note> notes = new List<Note>();
                bool isDouble = false;

                foreach (var note in diff.notes)
                {
                    if (note.type == SMNoteType.Tail)
                    {
                        // find last in the same column for tails
                        int lane = note.lane;

                        if (isDouble)
                        {
                            if (lane >= 4)
                                lane -= 4;
                            else
                                lane += 4;
                        }
                        
                        var lastNote = notes.LastOrDefault(n => n.d == lane);
                        if (lastNote == null)
                        {
                            Console.WriteLine($"Skipping tail note at {note.beat} beat on lane {note.lane}. No head note found.");
                            continue;
                        }
                        lastNote.l = Timing.GetTimeFromBeat(note.beat, ref smFile) - lastNote.t;
                        continue;
                    }
                    var fnfNote = new Note();
                    fnfNote.t = Timing.GetTimeFromBeat(note.beat, ref smFile);
                    if (note.lane >= 4 && !isDouble)
                    {
                        // loop over notes and += 4
                        foreach (var n in notes)
                        {
                            n.d += 4;
                        }
                        isDouble = true;
                    }

                    if (!isDouble)
                        fnfNote.d = note.lane;
                    else
                    {
                        if (note.lane >= 4) // player
                            fnfNote.d = note.lane - 4;
                        else
                            fnfNote.d = note.lane + 4;
                    }

                    switch (note.type)
                    {
                        case SMNoteType.Fake:
                            fnfNote.k = "fake";
                            break;
                        case SMNoteType.Mine:
                            fnfNote.k = "mine";
                            break;
                        default:
                            fnfNote.k = "normal";
                            break;
                    }
                    fnfNote.l = 0;
                    
                    notes.Add(fnfNote);
                }
                
                // events

                if (!isDouble)
                {
                    // focus camera on bf
                
                    var focusEvent = new Event();
                    focusEvent.t = 0;
                    focusEvent.e = "FocusCamera";
                    focusEvent.v = 1;
                
                    fnfSong.events.Add(focusEvent);
                }
                else
                {
                    // every 4 beats, switch camera focus ONLY if each player has notes.

                    int lengthOfFourthBeats = (int)(diff.notes.Last().beat / 4);

                    int lastFocus = -1;
                    int currentFocus = -1;
                    
                    for(int i = 0; i < lengthOfFourthBeats; i++)
                    {
                        float beat = 4 * i;
                        
                        Event e = new Event();
                        e.e = "FocusCamera";
                        e.t = Timing.GetTimeFromBeat(beat, ref smFile);
                        
                        // check if each player has notes in the next 8 beats
                        List<SMNote> notesInSegment = diff.notes.FindAll(n => n.beat >= beat && n.beat < beat + 8);
                        if (notesInSegment.Count == 0)
                            continue;

                        SMNote? firstBFNote = notesInSegment.FirstOrDefault(n => n.lane >= 4);
                        SMNote? firstOPNote = notesInSegment.FirstOrDefault(n => n.lane < 4);
                        
                        if (firstOPNote.HasValue && firstBFNote.HasValue)
                        {
                            // compare the beats of the first notes
                            
                            if (firstBFNote.Value.beat < firstOPNote.Value.beat)
                            {
                                // switch focus to bf
                                currentFocus = 1;
                            }
                            else
                            {
                                // switch focus to opponent
                                currentFocus = 0;
                            }
                        }
                        else if (firstOPNote.HasValue)
                        {
                            // switch focus to opponent
                            currentFocus = 0;
                        }
                        else if (firstBFNote.HasValue)
                        {
                            // switch focus to bf
                            currentFocus = 1;
                        }
                        else
                        {
                            e.v = 0;
                            currentFocus = 0;
                        }

                        if (currentFocus != lastFocus)
                        {
                            e.v = new Dictionary<string, int>();
                            e.v.Add("char", currentFocus);
                            e.v.Add("duration", 4);
                            
                            lastFocus = currentFocus;
                            fnfSong.events.Add(e);
                        }
                    }


                }
                
                fnfSong.notes.Add(d, notes);
            }

            if (fnfMetadata.playData.difficulties.Count == 0)
            {
                Console.WriteLine("No valid difficulties found. Exiting.");
                Console.Read();
                return;
            }

            string charters = smFile.difficulties[0].charter;
            if (charters == "")
                charters = "Unknown";
            
            for (int i = 1; i < smFile.difficulties.Count; i++)
            {
                if (smFile.difficulties[i].charter != "")
                    charters += ", " + smFile.difficulties[i].charter;
            }
            
            fnfMetadata.charter = charters;
            
            // Serialize the metadata
            
            var metadataJson = JsonSerializer.Serialize(fnfMetadata, new JsonSerializerOptions() {WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase});
            var songJson = JsonSerializer.Serialize(fnfSong, new JsonSerializerOptions() {WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase});

            // Write the JSON to file
            if (outputPath != null)
            {
                File.WriteAllText(outputPath + "/" + outputName.ToLower() + "-metadata", metadataJson);
                File.WriteAllText(outputPath + "/" + outputName.ToLower() + "-chart.json", songJson);
            }
            else
            {
                outputName = Path.GetFileNameWithoutExtension(inputPath);
                
                File.WriteAllText(outputName.ToLower() + "-metadata.json", metadataJson);
                File.WriteAllText(outputName.ToLower() + "-chart.json", songJson);
            }

            Console.WriteLine("Completed!");
            
            Console.Read(); // Keep the console open
        }
    }
}