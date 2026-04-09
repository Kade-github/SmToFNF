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
            Console.WriteLine("SmToFnF.exe - Convert Stepmania files to and from Friday Night Funkin' files\n    Usage: SmToFnf.exe <input file/input directory>\n    If the input is a .sm file, it will be converted to FNF format. If the input is a directory containing -metadata.json and -chart.json files, it will be converted to .sm format.\n    The output files will be saved in the same directory as the input file/directory with the same name as the input file/directory.");
            Console.Read();
        }

        public static void ToFNF(string inputPath, string outputName, string ?outputPath)
        {
            
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
                
                if (d != "hard" && d != "medium" && d  != "easy" && d != "challenge" && d != "edit")
                {
                    Console.WriteLine($"Skipping difficulty {diff.name}. Not correct difficulty name.");
                    continue;
                }

                if (d == "medium")
                    d = "normal";

                d = d switch
                {
                    "challenge" => "erect",
                    "edit" => "nightmare",
                    _ => d
                };

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
                            fnfNote.k = "";
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
                    focusEvent.v = new Dictionary<string, int>();
                    focusEvent.v.Add("char", 1);
                    focusEvent.v.Add("duration", 4);
                
                    fnfSong.events.Add(focusEvent);
                }
                else
                {
                    // every 8 beats, switch camera focus ONLY if each player has notes.

                    int lastFocus = -1;
                    int currentFocus = -1;
                    
                    for(int i = 0; i < diff.notes.Last().beat; i++)
                    {
                        float beat = i;
                        
                        if (beat % 8 != 0)
                            continue;
                        
                        Event e = new Event();
                        e.e = "FocusCamera";
                        e.t = Timing.GetTimeFromBeat(beat, ref smFile);
                        
                        // check if each player has notes in the next 8 beats
                        List<SMNote> notesInSegment = diff.notes.FindAll(n => n.beat >= beat && n.beat < beat + 4);
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
                            e.v = new Dictionary<string, string>();
                            e.v.Add("char", currentFocus.ToString());
                            e.v.Add("ease", "CLASSIC");
                            
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
        }
        
        public static float getBeat(float s, List<SMTimingPoint> timingPoints)
        {
            float beat = 0;
            foreach (var timingPoint in timingPoints)
            {
                if (s < timingPoint.startTime)
                    break;
                beat = timingPoint.startBeat + (s - timingPoint.startTime) * (timingPoint.bpm / 60);
            }
            return beat;
        }
        
        public static float SnapBeatToGrid(float beat, int subdivisions = 192)
        {
            float snapped = MathF.Round(beat * subdivisions) / subdivisions;
            return snapped;
        }
        
        public static void ToSM(string inputPath, string outputName, string? outputPath)
        {
            var files = Array.Empty<string>();
            try
            {
                files = Directory.GetFiles(Path.GetDirectoryName(inputPath) ?? "", "*.json");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error reading directory: {e.Message}");
                Console.Read();
                return;
            }

            string? metadataPath = null;
            string? chartPath = null;
            foreach (var file in files)
            {
                if (file.EndsWith("-metadata.json", StringComparison.OrdinalIgnoreCase))
                    metadataPath = file;
                else if (file.EndsWith("-chart.json", StringComparison.OrdinalIgnoreCase))
                    chartPath = file;
            }
            
            if (metadataPath == null || chartPath == null)
            {
                Console.WriteLine("Could not find metadata or chart JSON files. Make sure they are in the same directory as the input file and have the correct naming convention.");
                Console.Read();
                return;
            }
            
            var metadataJson = File.ReadAllText(metadataPath);
            var chartJson = File.ReadAllText(chartPath);
            
            var metadata = JsonSerializer.Deserialize<FNFMetadata>(metadataJson);
            var chart = JsonSerializer.Deserialize<FNFSong>(chartJson);
            
            Console.WriteLine($"Read {metadata.songName} by {metadata.artist} with {metadata.playData.difficulties.Count} difficulties.");

            var smFile = new SMFile();
            smFile.metadata = new SMMetadata();
            smFile.metadata.title = metadata.songName;
            smFile.metadata.artist = metadata.artist;
            smFile.metadata.offset = -metadata.offsets.instrumental / 1000;
            smFile.metadata.sampleStart = metadata.playData.previewStart / 1000f;
            smFile.metadata.credit = metadata.charter;
            smFile.timingPoints = new List<SMTimingPoint>();
            foreach (var timeChange in metadata.timeChanges)
            {
                var timingPoint = new SMTimingPoint
                {
                    startBeat = timeChange.b,
                    bpm = timeChange.bpm,
                    startTime = timeChange.t / 1000f,
                    endBeat = Single.PositiveInfinity,
                    endTime = Single.PositiveInfinity
                };
                // Check if theres a time change behind this one, if so, set the end beat and time of the previous time change to this one
                if (smFile.timingPoints.Count > 0)
                {
                    var previous = smFile.timingPoints.Last();
                    previous.endBeat = timeChange.b;
                    previous.endTime = timeChange.t / 1000f;
                }
                
                smFile.timingPoints.Add(timingPoint);
            }
            
            smFile.difficulties = new List<SMDifficulty>();
            
            
            foreach (var difficulty in metadata.playData.difficulties)
            {
                var smDifficulty = new SMDifficulty
                {
                    name = difficulty,
                    charter = metadata.charter,
                    type = "dance-double",
                    notes = []
                };

                if (!chart.notes.ContainsKey(difficulty))
                {
                    Console.WriteLine($"Difficulty {difficulty} not found in chart JSON. Skipping.");
                    continue;
                }
                
                foreach (var note in chart.notes[difficulty])
                {
                    var smNote = new SMNote();
                    smNote.beat = SnapBeatToGrid(getBeat((float)note.t / 1000f, smFile.timingPoints));
                    smNote.lane = note.d;
                    smNote.type = SMNoteType.Tap;

                    if (note.l > 0)
                    {
                        smNote.type = SMNoteType.Head;
                        var tailNote = new SMNote();
                        tailNote.beat = SnapBeatToGrid(getBeat((float)(note.t + note.l) / 1000f, smFile.timingPoints));
                        tailNote.lane = note.d;
                        tailNote.type = SMNoteType.Tail;
                        smDifficulty.notes.Add(tailNote);
                    }

                    smDifficulty.notes.Add(smNote);
                }
                
                // sort notes by beat
                smDifficulty.notes = smDifficulty.notes.OrderBy(n => n.beat).ToList();
                
                // convert diff names

                switch (smDifficulty.name)
                {
                    case "normal":
                        smDifficulty.name = "medium";
                        break;
                    case "erect":
                        smDifficulty.name = "challenge";
                        break;
                    case "nightmare":
                        smDifficulty.name = "edit";
                        break;
                }

                smFile.difficulties.Add(smDifficulty);
            }

            outputName = metadata.songName;

            smFile.Save(outputName.ToLower() + ".sm");
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
            string? outputPath = null;
            var outputName = "";

            if (outputPath != null)
            {
                // sanatize it
                outputName = Path.GetFileNameWithoutExtension(outputPath);
                outputPath = Path.GetDirectoryName(outputPath);
            }
            
            if (inputPath.EndsWith(".sm", StringComparison.OrdinalIgnoreCase))
            {
                // SM to FNF
                ToFNF(inputPath, outputName, outputPath);
            }
            else
            {
                // FNF to SM
                ToSM(inputPath, outputName, outputPath);
            }

            Console.WriteLine("Completed!");
            
            Console.Read(); // Keep the console open
        }
    }
}