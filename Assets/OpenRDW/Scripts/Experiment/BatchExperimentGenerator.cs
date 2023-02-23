using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Read a file and easily generate a batch of experiments
/// </summary>
public class BatchExperimentGenerator : MonoBehaviour
{
    public string filepath;
    public string savepath;
    public string saveName;
    public string result;
    public bool useRandomSeedSet; // if we choose randomseeds from randomSeedSet; otherwise we use random randomseeds
    public List<int> randomSeedSet; // the set of all the randomseeds
    public bool sameRandomSeedForEachUser; // if different users use the same randomseed for path generation
    public string pathChoice; // the waypoint generation mode for the experiment
    public string waypointDirPath;
    public List<float> pathLengthSet; // the set of all the pathLengths
    public string trackingSpaceDirPath; // we only use filepath, every trackingspace file in this directory would be experimented
    public int trialsCount; // the number of repeated experiments
    public List<string> redirectorSet; // every redirector would be experimented
    public List<string> resetterSet; // the resetter corresponds with the redirector

    public async void readFileAndGenerate()
    {
        // init
        randomSeedSet = new List<int>();
        redirectorSet = new List<string>();
        resetterSet = new List<string>();
        pathLengthSet = new List<float>();
        useRandomSeedSet = false;
        sameRandomSeedForEachUser = false;
        pathChoice = "";
        waypointDirPath = "";
        trackingSpaceDirPath = "";
        trialsCount = 1;

        // read
        if (!File.Exists(filepath))
        {
            Debug.LogError("BatchFile does not exist!");
            return;
        }
        try
        {
            var content = File.ReadAllLines(filepath);
            int lineId = 0;
            foreach (var line in content)
            {
                lineId++;
                if (line.Trim().Length == 0)
                    continue;
                var split = line.Split('=');
                for (int i = 0; i < split.Length; i++)
                {
                    split[i] = split[i].Trim();
                }
                switch (split[0].ToLower())
                {
                    case "userandomseedset":
                        useRandomSeedSet = bool.Parse(split[1]);
                        break;
                    case "randomseedset":
                        var seeds = split[1].Split(',');
                        foreach (var seed in seeds)
                        {
                            randomSeedSet.Add(int.Parse(seed));
                        }
                        break;
                    case "samerandomseedforeachuser":
                        sameRandomSeedForEachUser = bool.Parse(split[1]);
                        break;
                    case "pathchoice":
                        pathChoice = split[1];
                        break;
                    case "waypointdirpath":
                        waypointDirPath = split[1];
                        break;
                    case "pathlengthset":
                        var lengths = split[1].Split(',');
                        foreach (var length in lengths)
                        {
                            pathLengthSet.Add(float.Parse(length));
                        }
                        break;
                    case "trackingspacedirpath":
                        trackingSpaceDirPath = split[1];
                        break;
                    case "trialscount":
                        trialsCount = int.Parse(split[1]);
                        break;
                    case "redirectorset":
                        var redirectors = split[1].Split(',');
                        foreach (var redirector in redirectors)
                        {
                            redirectorSet.Add(redirector);
                        }
                        break;
                    case "resetterset":
                        var resetters = split[1].Split(',');
                        foreach (var resetter in resetters)
                        {
                            resetterSet.Add(resetter);
                        }
                        break;
                    default:
                        break;
                }
            }
        }
        catch
        {
            Debug.LogError("Read error");
            return;
        }

        // check
        if (redirectorSet.Count != resetterSet.Count)
        {
            Debug.LogError("RedirectorSet and ResetterSet should have exactly same number of elements");
            return;
        }
        if (useRandomSeedSet && randomSeedSet.Count == 0)
        {
            Debug.LogError("At least one randomSeed is needed");
            return;
        }
        if (pathLengthSet.Count == 0)
        {
            pathLengthSet.Add(400); // default length
        }
        if (trackingSpaceDirPath == null || trackingSpaceDirPath.Length == 0)
        {
            Debug.LogError("We use filepath to generate tracking space, so trackingSpaceDirPath is needed");
            return;
        }
        if (pathChoice.ToLower() == "filepath" && (waypointDirPath == null || waypointDirPath.Length == 0))
        {
            Debug.LogError("WaypointDirPath is needed if use filepath to generate waypoints");
            return;
        }

        // generate
        System.Random rand = new System.Random();
        result = "";
        var trackingSpaceFiles = new List<string>(Directory.GetFiles(trackingSpaceDirPath));
        var waypointFiles = new List<string>();
        if (pathChoice.ToLower() == "filepath")
        {
            waypointFiles = new List<string>(Directory.GetFiles(waypointDirPath));
        }
        for (int h = 0; h < trackingSpaceFiles.Count; h++)
        {
            var trackingSpaceFile = trackingSpaceFiles[h];
            var avatarnum = int.Parse(File.ReadAllLines(trackingSpaceFile)[0]);
            var pathLength = pathLengthSet[h % pathLengthSet.Count];
            for (int i = 0; i < redirectorSet.Count; i++)
            {
                var redirector = redirectorSet[i];
                var resetter = resetterSet[i];
                for (int j = 0; j < trialsCount; j++)
                {
                    var seeds = new List<int>();
                    if (sameRandomSeedForEachUser)
                    {
                        var seed = rand.Next();
                        if (useRandomSeedSet)
                        {
                            seed = randomSeedSet[(i * trialsCount + j) % randomSeedSet.Count];
                        }
                        for (int k = 0; k < avatarnum; k++)
                        {
                            seeds.Add(seed);
                        }
                    }
                    else
                    {
                        for (int k = 0; k < avatarnum; k++)
                        {
                            var seed = rand.Next();
                            if (useRandomSeedSet)
                            {
                                seed = randomSeedSet[((i * trialsCount + j) * avatarnum + k) % randomSeedSet.Count];
                            }
                            seeds.Add(seed);
                        }
                    }

                    appendLine("redirector = " + redirector);
                    appendLine("resetter = " + resetter);
                    appendLine("pathSeedChoice = " + pathChoice);
                    if (pathChoice.ToLower() == "filepath")
                    {
                        appendLine("waypointsFilepath = " + waypointFiles[(i * trialsCount + j) % trialsCount]);
                    }
                    appendLine("trackingSpaceChoice = filepath");
                    appendLine("trackingSpaceFilepath = " + trackingSpaceFile);
                    if (pathChoice.ToLower() != "filepath")
                    {
                        appendLine("pathLength = " + pathLength);
                    }
                    for (int k = 0; k < avatarnum; k++)
                    {
                        if (pathChoice.ToLower() != "filepath")
                        {
                            appendLine("randomSeed = " + seeds[k]);
                        }
                        appendLine("newUser");
                    }
                    appendLine("end");
                    appendLine("");
                }
            }
        }

        // save
        if (saveName == null || saveName.Length == 0)
        {
            saveName = "batch";
        }
        var outfile = File.Create(savepath + "/" + saveName + ".txt");
        var byteArray = System.Text.Encoding.UTF8.GetBytes(result);
        outfile.Write(byteArray, 0, byteArray.Length);
        outfile.Close();
        Debug.Log("File generation succeed!");
    }

    public void appendLine(string str)
    {
        result += str + "\n";
    }
}