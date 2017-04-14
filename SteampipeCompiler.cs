using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Application = UnityEngine.Application;

using Debug = UnityEngine.Debug;

public class SteampipeCompiler : EditorWindow
{
    [MenuItem("Build/Steampipe Compiler")]
    static void Init()
    {
        // Get existing open window or if none, make a new one:		
        var instance = CreateInstance<SteampipeCompiler>();
        instance.minSize = new Vector2(470, 169);
        instance.maxSize = new Vector2(472, 171);
        var r = instance.position;
        r.width = 325;
        r.height = 170;
        instance.position = r;
        instance.Show();
    }

    private static volatile string Version;
    private static bool steamPush = false;
    private static bool debug = false;
    private static bool useLauncher = true;
    private static bool drmfree = false;

    private static bool windows32, windows64, linux, osx;

    #region gui variables
    private static GUIStyle listStyle = new GUIStyle();

    private static GUIContent[] listBranch;
    private static WFTO.ComboBox comboBoxBranch;// = new ComboBox();
    #endregion
    private void OnGUI()
    {
        EditorGUI.LabelField(new Rect(5, 5, 50, 25), "Version");
        Version = EditorGUI.TextField(new Rect(60, 5, 140, 20), Version);
        EditorGUI.LabelField(new Rect(205, 5, 40, 25), "SteamPush");
        steamPush = EditorGUI.Toggle(new Rect(250, 5, 20, 25), steamPush);
        EditorGUI.LabelField(new Rect(265, 5, 40, 25), "Debug");
        debug = EditorGUI.Toggle(new Rect(310, 5, 20, 25), debug);
        EditorGUI.LabelField(new Rect(325, 5, 70, 25), "Launcher");
        useLauncher = EditorGUI.Toggle(new Rect(380, 5, 20, 25), useLauncher);
        EditorGUI.LabelField(new Rect(395, 5, 70, 25), "DRMFree");
        drmfree = EditorGUI.Toggle(new Rect(450, 5, 20, 25), drmfree);

        GUI.Label(new Rect(5, 30, 40, 25), "Target:");
        windows32 = GUI.Toggle(new Rect(5, 60, 100, 25), windows32, "Windows32");
        windows64 = GUI.Toggle(new Rect(5, 90, 100, 25), windows64, "Windows64");
        linux   = GUI.Toggle(new Rect(5, 120, 100, 25), linux, "Linux");
        osx     = GUI.Toggle(new Rect(5, 150, 100, 25), osx, "Osx");

        EditorGUI.LabelField(new Rect(160, 30, 50, 25), "Branch");
        if (comboBoxBranch == null)
        {
            listBranch = new[] { new GUIContent("Internal"), new GUIContent("Internal2"), new GUIContent("PatchTesting"), new GUIContent("Publicdebugbuild") };
            comboBoxBranch = new WFTO.ComboBox(new Rect(215, 30, 100, 20), listBranch[0], listBranch, "button", "box", listStyle);
        }
        comboBoxBranch.Show();


        GUI.enabled = !string.IsNullOrEmpty(Version);
        if (GUI.Button(new Rect(160, 120, 60, 20), "Build"))
        {
            if (drmfree) EnableDRMFree(); else ResetDRMFree();
            Args args = new Args
            {
                Version = Version,
                steampush = steamPush,
                Branch = listBranch[comboBoxBranch.SelectedItemIndex].text,
                debug = debug
            };
            
            if (windows64) args.Target.Add(Target.windows64);
            if (osx) args.Target.Add(Target.osx);
            if (linux) args.Target.Add(Target.linux);
            if (windows32) args.Target.Add(Target.windows32);

            DoBuild(args);
        }
        GUI.enabled = true;
    }

    static void EnableDRMFree() { DRMSet(Resources.Load<DRMFree>("prefabs/Launcher/DRMFree")); }
    static void ResetDRMFree() { DRMSet(Resources.Load<DRMSteam>("prefabs/Launcher/DRMSteam")); }

    static void DRMSet(SaveStatAchievementLAN implementation)
    {
        var wfto = Resources.Load<SteamWFTO>("prefabs/Launcher/Multiplayer");
        wfto.Implementation = implementation;
        EditorUtility.SetDirty(wfto);
        AssetDatabase.SaveAssets();
    }

    public static void ReleaseWindows() { BuildWindows("../Build/Release/Windows", "../CustomPlugins/GameLogic/bin/Windows/Release/Win32", BuildTarget.StandaloneWindows); }
    public static void DebugWindows() { BuildWindows("../Build/Debug/Windows", "../CustomPlugins/GameLogic/bin/Windows/Debug/Win32", BuildTarget.StandaloneWindows, BuildOptions.Development|BuildOptions.ConnectWithProfiler | BuildOptions.AllowDebugging); }
    public static void ReleaseWindows64() { BuildWindows("../Build/Release/Windows x64", "../CustomPlugins/GameLogic/bin/Windows/Release/Win64", BuildTarget.StandaloneWindows64); }
    public static void DebugWindows64() { BuildWindows("../Build/Debug/Windows x64", "../CustomPlugins/GameLogic/bin/Windows/Debug/Win64", BuildTarget.StandaloneWindows64, BuildOptions.Development | BuildOptions.AllowDebugging | BuildOptions.ConnectWithProfiler); }
    private static void BuildWindows(string buildPath, string pluginPath, BuildTarget tgt, BuildOptions opts = BuildOptions.None) {
        string executableName;
        if (useLauncher)
            executableName = "WFTOGame";
        else
            executableName = "WFTO";


        /****** PRE BUILD ******/
        UniversalPreBuild(buildPath, tgt == BuildTarget.StandaloneWindows64 ? "Windows x64" : "Windows x32");

        /******   BUILD    ******/
        Compile(buildPath, opts, tgt, executableName + ".exe");

        /****** POST BUILD ******/
        UniversalPostBuild(buildPath, executableName + "_Data");

        //Inject MPMap
        const string mapsLocation = "Assets/GameData/Maps";
        DirectoryCopy(mapsLocation, buildPath + "/GameData/Maps", false);

        // Copy over Game Launcher:
        if(useLauncher) {
            SafeCopy("../WFTOLauncher/WFTOLauncher/bin/Release/WFTOLauncher.exe", buildPath + "/WFTO.exe");
            SafeCopy("../WFTOLauncher/WFTOLauncher/bin/Release/Open.Nat.dll", buildPath + "/Open.Nat.dll");
        }
    
    }


	public static void ReleaseLinux() {BuildLinux("../Build/Release/Linux", "../CustomPlugins/GameLogic/bin/Linux/Release/64bit");}
    public static void DebugLinux() { BuildLinux("../Build/Debug/Linux", "../CustomPlugins/GameLogic/bin/Linux/Debug/64bit", BuildOptions.Development | BuildOptions.AllowDebugging | BuildOptions.ConnectWithProfiler); }
    public static void BuildLinux(string buildPath, string pluginPath, BuildOptions opts = BuildOptions.None)
    {
        string executableName;
        if (useLauncher)
            executableName = "WFTOGame";
        else
            executableName = "WFTO";

        string dataPathName = executableName + "_Data";

        /****** PRE BUILD ******/
        UniversalPreBuild(buildPath, "Linux");

        /******   BUILD    ******/
        Compile(buildPath, opts, BuildTarget.StandaloneLinux64, executableName + ".x86_64");

        /****** POST BUILD ******/
        UniversalPostBuild(buildPath, dataPathName);
        
        //Inject MPMap
        const string mapsLocation = "Assets/GameData/Maps";
        DirectoryCopy(mapsLocation, buildPath + "/" + dataPathName + "/GameData/Maps", false);

        //enforce lowercase uiresources
        if (Directory.Exists(buildPath + "/" + dataPathName + "/UIResources"))
        {
            Directory.Move(buildPath + "/" + dataPathName + "/UIResources", buildPath + "/" + dataPathName + "/templol");
            Directory.Move(buildPath + "/" + dataPathName + "/templol", buildPath + "/" + dataPathName + "/uiresources");
        }

        // Ensure that the CUI bash script has unix line-endings:
        Execute(buildPath + "/" + dataPathName + "/CoherentUI_Host/linux", "dos2unix.exe CoherentUI_Host");
    }


	public static void ReleaseOSX() {BuildOSX("../Build/Release/OSX", "");}
    public static void DebugOSX() { BuildOSX("../Build/Debug/OSX", "", BuildOptions.Development | BuildOptions.AllowDebugging | BuildOptions.ConnectWithProfiler); }
    public static void BuildOSX(string buildPath, string pluginPath, BuildOptions opts = BuildOptions.None)
    {
        const string executableFile = "WFTO.app";

        /****** PRE BUILD ******/
        UniversalPreBuild(buildPath, "OSX");

        /******   BUILD    ******/
        Compile(buildPath, opts, BuildTarget.StandaloneOSXIntel64, executableFile);

        /****** POST BUILD ******/
        UniversalPostBuild(buildPath, "WFTO.app/Contents");
        
        //Inject MPMap
        const string mapsLocation = "Assets/GameData/Maps";
        DirectoryCopy(mapsLocation, buildPath + "/WFTO.app/Contents/GameData/Maps", false);

        //enforce lowercase uiresources
        if (Directory.Exists(buildPath + "/WFTO.app/Contents/UIResources"))
        {
            Directory.Move(buildPath + "/WFTO.app/Contents/UIResources", buildPath + "/WFTO.app/templol");
            Directory.Move(buildPath + "/WFTO.app/templol", buildPath + "/WFTO.app/Contents/uiresources");
        }
    }


    private static void SafeCopy(string from, string to)
    {
        if (File.Exists(to)) File.Delete(to);
        File.Copy(from, to);
    }
    /// <summary>
    /// post build stuff that should be done for every platform (so don't copy plugins)
    /// </summary>
    /// <param name="buildPath">build directory</param>
    /// <param name="data">WFTO_Data or WFTO.app for OSX rebels ?</param>
    private static void UniversalPostBuild(string buildPath, string data)
    {
        UpdateVersionString(buildPath, data);
        InjectSettings(buildPath, data);
        InjectTranslations(buildPath, data);
        //InjectMPMaps(buildPath, data);
        //InjectGameData(buildPath, data);
    }

    private static void UniversalPreBuild(string buildPath, string tgt)
    {
        var files = Directory.GetFiles(buildPath);
        for (int i = 0; i < files.Length; i++)
        {
            var file = files[i];
            if (file.Contains("gitignore")) continue;
            File.Delete(file);
        }
        var directories = Directory.GetDirectories(buildPath);
        for (int i = 0; i < directories.Length; i++)
            Directory.Delete(directories[i],true);

       
    }

    /// <summary>
    /// in the main menu's top right, every compilation should have a new time string on it
    /// </summary>
    /// <param name="buildPath">where to look for WFTO_Data/uiresources/menu/menu.html</param>
    private static void UpdateVersionString(string buildPath, string data)
    {
        // update version file (used to include version into logs)
        try
        {
            if (!Directory.Exists(buildPath + "/" + data))
                Directory.CreateDirectory(buildPath + "/" + data);
            File.WriteAllLines(buildPath + "/" + data + "/wftoversion.dat", new string[] { Version });
        }
        catch (Exception e)
        {
            Debug.LogError("failed write wftoversion.dat, see exception below.");
            Debug.LogException(e);
        }
        // update version in main menu
        var mainMenuPath = buildPath + "/" + data + "/uiresources/wftoUI/menu/menu.html";
        if (!File.Exists(mainMenuPath))
        {
            Utils.Trace( "Non-Fatal Build Error! Couldn't find {0}, so can't update time string!", mainMenuPath);
            return;
        }
        var lines = File.ReadAllLines(mainMenuPath);
        if (lines.Length == 0)
        {
            Utils.Trace( "Non-Fatal Build Error! Couldn't read from {0}, so can't update time string!", mainMenuPath);
            return;
        }
        for (int i = 0; i < lines.Length-1; i++)
        {
            if (lines[i].Trim() == "<!--place timestring on next line-->")
            {
                lines[i + 1] =
                    "<span style=\"position: absolute; color: #FFFFFF; font-family: 'Palatino Linotype'\">" + Version + "</span>";
                File.WriteAllLines(mainMenuPath, lines);
                return;
            }
        }

       
    }
    
    private static void InjectSettings(string buildPath, string data)
    {
        const string settingLocation = "Assets/Settings.ini";
        File.Copy(settingLocation, buildPath + "/" + data + "/Settings.ini");
    }

    private static void InjectTranslations(string buildPath, string data)
    {
        const string translationFile = "GameText.csv";
        const string translationLocation = "Assets/Translation/" + translationFile;
        string targetDir = buildPath + "/" + data + "/Translation";
        if (!Directory.Exists(targetDir))
            Directory.CreateDirectory(targetDir);
        string ctargetDir = buildPath + "/" + data + "/Translation/Community";
        if (!Directory.Exists(ctargetDir))
            Directory.CreateDirectory(ctargetDir);

        File.Copy(translationLocation, targetDir + "/" + translationFile);
    }

    private static void InjectGameData(string buildPath, string data)
    {
        //DirectoryCopy("Assets/GameData", buildPath + "/" + data + "/GameData", true, ".meta");
        /*const string dataFile = "VeinsOfEvilSetup.csv";
        const string dataLocation = "Assets/GameData/" + dataFile;
        string targetDir = buildPath + "/" + data + "/GameData";
        if (!Directory.Exists(targetDir))
            Directory.CreateDirectory(targetDir);
        File.Copy(dataLocation, targetDir + "/" + dataFile);*/
    }

    private static void InjectMPMaps(string buildPath, string data)
    {
        const string mapsLocation = "Assets/GameData/Maps";
        DirectoryCopy(mapsLocation, buildPath + /*"/" + data +*/ "/GameData/Maps", false);
    }

    private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs, string excludeExt = "")
    {
        // Get the subdirectories for the specified directory.
        DirectoryInfo dir = new DirectoryInfo(sourceDirName);
        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException(
                "Source directory does not exist or could not be found: "
                + sourceDirName);
        }

        DirectoryInfo[] dirs = dir.GetDirectories();
       
        // If the destination directory doesn't exist, create it. 
        if (!Directory.Exists(destDirName))
        {
            Directory.CreateDirectory(destDirName);
        }

        // Get the files in the directory and copy them to the new location.
        FileInfo[] files = dir.GetFiles();
        if (!string.IsNullOrEmpty(excludeExt))
            files = files.Where(name => !name.FullName.EndsWith(excludeExt)).ToArray();

        foreach (FileInfo file in files)
        {
            string temppath = Path.Combine(destDirName, file.Name);
            file.CopyTo(temppath, false);
        }

        // If copying subdirectories, copy them and their contents to new location. 
        if (copySubDirs)
        {
            foreach (DirectoryInfo subdir in dirs)
            {
                string temppath = Path.Combine(destDirName, subdir.Name);
                DirectoryCopy(subdir.FullName, temppath, true, excludeExt);
            }
        }
    }


    private static string[] levelNames = null;
    private static void Compile(string buildPath, BuildOptions buildOptions, BuildTarget buildTarget, String executableFile)
    {
        string[] scenes = null;
		// Grab the scenes from the build settings:
        if (levelNames == null) scenes = (from scene in EditorBuildSettings.scenes where scene.enabled select scene.path).ToArray();
        else scenes = (from scene in EditorBuildSettings.scenes where scene.enabled && isPresent(levelNames, scene.path) select scene.path).ToArray();
        // Manually set Run In Background to false: 
        // Application.runInBackground = false; // seems this prevents next loading step to be triggered when unfocused making loading times longer for many users, as they tend to do something else while the game loads
        // Build player:
        BuildPipeline.BuildPlayer(scenes, buildPath + "/"+executableFile, buildTarget, buildOptions);

    }

    private static bool isPresent(string[] sceneNames, string path)
    {
        for (int i = 0; i < sceneNames.Length; i++)
            if (path.ToLowerInvariant().Contains(sceneNames[i].ToLowerInvariant()))
                return true;
        return false;
    }

    public const int ErrorNonSpecifiedVersion = 10;
    public const int ErrorNonSpecifiedTarget = 11;
    public const int ErrorBuildingTarget = 12;

    //Command line usage:
    // - version:[version string]
    // - target:[windows|linux|osx]
    // - branch:[PatchTesting|Internal]
    // - debug:[true|false]
    // - scenes:[scenes name separated with # eg: mudbox#coolmap# etc]
    public static void Build()
    {
        //Get the arguments from the command line
        Args args = new Args(Environment.GetCommandLineArgs(), true);
        DoBuild(args);
    }

    public static void DoBuild(Args args)
    {
        levelNames = null;
        Debug.Log("Start build");

        //Check we have a version
        if (string.IsNullOrEmpty(args.Version))
        {
            Debug.LogError("ErrorNonSpecifiedVersion");
            if(args.console) EditorApplication.Exit(ErrorNonSpecifiedVersion);
            return;
        }

        //If no target is set, return an error
        if (args.Target.Count == 0)
        {
            Debug.LogError("ErrorNonSpecifiedTarget");
            if (args.console) EditorApplication.Exit(ErrorNonSpecifiedTarget);
            return;
        }

        if (args.steampush)
            NetworkServer.SendSlackMessage("Start building version:" + args.Version + " on branch:" + args.Branch);

        //Get the version
        Version = args.Version;

        //Get the scenes names restriction for this build
        levelNames = args.sceneNames;

        buildFinished = false;
        pushThread = new Thread(PushUpdate);
        pushThread.Start();

        var time = DateTime.Now;
        //Try to build the target, throw an error if it fails
        foreach (var tgt in args.Target)
        {
            Debug.Log("Build " + tgt);
            if (args.steampush)
                NetworkServer.SendSlackMessage("Building :" + tgt);

            if (BuildTgt(tgt, args))
            {
                Debug.Log("Build " + tgt + " Done");
                if (args.steampush)
                {
                    NetworkServer.SendSlackMessage("Building " + tgt + " done, pushing to Steam");
                    lock (steamPushQueue)
                    {
                        steamPushQueue.Enqueue(new KeyValuePair<Target, KeyValuePair<string, bool>>(tgt, new KeyValuePair<string, bool>(args.Branch, args.debug)));
                        Debug.Log("Will push " + tgt);
                    }
                }
            }
            else Debug.Log("FAILED to Build " + tgt);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Resources.UnloadUnusedAssets();
        }

        levelNames = null;

        buildFinished = true;
        pushThread.Join(1000 * 60 * 30);
        //while (pushThread.IsAlive && steamPushQueue.Count > 1) { }
        if(args.steampush) Debug.Log("Push Finished");
        ResetDRMFree();
        if (args.steampush)
            NetworkServer.SendSlackMessage(string.Format("Building done ! It took {0:0.0} minutes", (DateTime.Now - time).TotalMinutes));
        if (args.console) EditorApplication.Exit(0);
        
    }


    private static void PushUpdate()
    {
        while (!IsThreadFinished())
        {
            lock (steamPushQueue)
            {
                if (steamPushQueue.Count > 0 && SteamPushProcess == null)
                {
                    var item = steamPushQueue.Dequeue();
                    //workingOn = item;
                    SteamPush(item.Key, item.Value.Key, item.Value.Value);
                }
            }
            if (SteamPushProcess != null)
            {
                SteamPushProcess.WaitForExit(1000 * 60 * 30);
                SteamPushProcess = null;
            }
            //Thread.Sleep(100);
        }
        
        pushThread.Interrupt();
    }
    private static bool IsThreadFinished()
    {
        lock (steamPushQueue)
            return buildFinished && SteamPushProcess == null && steamPushQueue.Count == 0;
    }

    static Thread pushThread;
    static volatile bool buildFinished;
    private static bool BuildTgt(Target tgt, Args args)
    {
        try 
        { 
            Builds[tgt](args.debug);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("ErrorBuildingTarget:" + e);
            if (args.console) EditorApplication.Exit(ErrorBuildingTarget);
            return false;
        }
    }

    private const string vdfFolderPath = "../Build/steampipe/ContentBuilder/scripts/";
    private const string vdfPath = vdfFolderPath + "app_build_230190.vdf";
    private static void SteamPush(Target target, string branch, bool debug)
    {
        if (SteamPushProcess != null)
        {
            Debug.LogError("Another push is happening. Failed for target " + target); 
            return;
        }
        //Is file available ? No other steam push ongoing ?
        FileStream fileOpen = null;
        try { fileOpen = File.Open(vdfPath, FileMode.Open, FileAccess.ReadWrite); }
        catch (Exception) { Debug.LogError("Couldn't open steam vdf file. Either user or another push is happening. Failed for target " + target); return; }
        finally { fileOpen.Close(); }
        //fileOpen.Close();
        string defaultDescription = "War For The Overworld";
        string[] lines = null;
        string line = "";
        string depotFile = null;

        string search = target.ToString().ToLowerInvariant();
        lines = File.ReadAllLines(vdfPath); 
        for (int i = 0; i < lines.Length; i++)
        {
            line = lines[i];
            string lowerLine = line.ToLowerInvariant();
            if (lowerLine.Contains("setlive"))
            {
                lines[i] = "	\"setlive\"		\""+branch+"\"		// branch to set live after successful build, none if empty";
            }
            else if (lowerLine.Trim().StartsWith("\"desc\""))
            {
                // Append version number and build target to description field in vdf file
                int descEnd = line.LastIndexOf("//", StringComparison.InvariantCulture);
                if (descEnd < 0) descEnd = line.Length - 1;
                int descStart = line.IndexOf("\"" + defaultDescription);
                lines[i] = line.Substring(0, descStart + 1 + defaultDescription.Length) + " " + Version + " (" + search + ")\" " + line.Substring(descEnd);
            }
            else if (lowerLine.Contains(search))
            {
                //extract the depot file
                //extract the depot file
                depotFile = lowerLine.Replace("//", "").Trim();
                depotFile = depotFile.Split(' ')[1];
                depotFile = depotFile.Trim(char.Parse("\""));
                if (line.StartsWith("//"))
                    lines[i] = line.Substring(2);
            }
            else if (lowerLine.Contains("linux") || lowerLine.Contains("windows32") || lowerLine.Contains("windows64") || lowerLine.Contains("osx"))
                if (!line.StartsWith("//"))
                    lines[i] = "//" + line;
        }
        File.WriteAllLines(vdfPath, lines);

        //Now depending on debug we need to change the depot files too
        if (depotFile != null && File.Exists(vdfFolderPath + depotFile))
        {
            lines = File.ReadAllLines(vdfFolderPath + depotFile);
            for (int l = 0; l < lines.Length; l++)
            {
                line = lines[l];
                if (!debug && line.Contains("Debug"))
                    lines[l] = line.Replace("Debug", "Release");
                if (debug && line.Contains("Release"))
                    lines[l] = line.Replace("Release", "Debug");
            }
            File.WriteAllLines(vdfFolderPath + depotFile, lines);
        }
        SteamPushProcess = Execute(@"..\Build\steampipe\ContentBuilder","run_build.bat");
        //builder\steamcmd.exe +login [User] [Pwd] +run_app_build ..\scripts\app_build_230190.vdf +quit
    }

    private static Process Execute(string workingdirectory, string batfile)
    {
        Process process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c "+batfile,
                WorkingDirectory = workingdirectory,
            }
        };
        process.Start();
        return process;
    }

    //private static KeyValuePair<Target, KeyValuePair<string, bool>> workingOn;
    static readonly Queue<KeyValuePair<Target, KeyValuePair<string, bool>>> steamPushQueue = new Queue<KeyValuePair<Target, KeyValuePair<string, bool>>>();
    static Process SteamPushProcess;
   
    //Simple helper to define a build line given a target/debug input
    static Dictionary<Target, Action<bool>> Builds = new Dictionary<Target, Action<bool>>
    {
        {Target.linux,     (dbg) => { if (dbg) DebugLinux();     else ReleaseLinux(); } },
        {Target.osx,       (dbg) => { if (dbg) DebugOSX();       else ReleaseOSX(); } },
        {Target.windows32, (dbg) => { if (dbg) DebugWindows();   else ReleaseWindows(); } },
        {Target.windows64, (dbg) => { if (dbg) DebugWindows64(); else ReleaseWindows64(); } },
    };

    //Hold all the arguments possible from command line
    public class Args
    {
        public string Version = "";
        public string Branch = "";
        public List<Target> Target = new List<Target>();
        public bool debug = false;
        public bool steampush = false;
        public string[] sceneNames = null;
        public bool console;
        public Args(){}
        public Args(string[] args, bool console = false)
        {
            this.console = console;
            for (int i = 0; i < args.Length; i++)
            {
                if (!args[i].Contains(":")) continue;
                string[] thisArgs = args[i].Split(':');
                string key = thisArgs[0].ToLower();
                if (key == "version"){Version = thisArgs[1];}
                else if (key == "target")
                {
                    try{Target.Add((Target) Enum.Parse(typeof (Target), thisArgs[1], true));}
                    catch{}
                }
                else if (key == "branch") { Branch = thisArgs[1]; }
                else if (key == "debug")    {bool.TryParse(thisArgs[1], out debug);}
                else if (key == "steampush"){bool.TryParse(thisArgs[1], out steampush);}
                else if (key == "scenes")   {sceneNames = thisArgs[1].Split('#');}
            }
        }
    }
    
    //Represent the different target to build   
    public enum Target
    {
        none,
        windows32,
        windows64,
        linux,
        osx,
    }
}
namespace WFTO
{
    public class ComboBox
    {
        private static bool forceToUnShow = false;
        private static int useControlID = -1;
        private bool isClickedComboButton = false;
        private int selectedItemIndex = 0;

        private Rect rect;
        private GUIContent buttonContent;
        private GUIContent[] listContent;
        private string buttonStyle;
        private string boxStyle;
        private GUIStyle listStyle;

        public ComboBox(Rect rect, GUIContent buttonContent, GUIContent[] listContent, GUIStyle listStyle)
        {
            this.rect = rect;
            this.buttonContent = buttonContent;
            this.listContent = listContent;
            this.buttonStyle = "button";
            this.boxStyle = "box";
            this.listStyle = listStyle;
        }

        public ComboBox(Rect rect, GUIContent buttonContent, GUIContent[] listContent, string buttonStyle, string boxStyle, GUIStyle listStyle)
        {
            this.rect = rect;
            this.buttonContent = buttonContent;
            this.listContent = listContent;
            this.buttonStyle = buttonStyle;
            this.boxStyle = boxStyle;
            this.listStyle = listStyle;
        }

        public int Show()
        {
            if (forceToUnShow)
            {
                forceToUnShow = false;
                isClickedComboButton = false;
            }

            bool done = false;
            int controlID = GUIUtility.GetControlID(FocusType.Passive);

            switch (Event.current.GetTypeForControl(controlID))
            {
                case EventType.mouseUp:
                    {
                        if (isClickedComboButton)
                        {
                            done = true;
                        }
                    }
                    break;
            }

            if (GUI.Button(rect, buttonContent, buttonStyle))
            {
                if (useControlID == -1)
                {
                    useControlID = controlID;
                    isClickedComboButton = false;
                }

                if (useControlID != controlID)
                {
                    forceToUnShow = true;
                    useControlID = controlID;
                }
                isClickedComboButton = true;
            }

            if (isClickedComboButton)
            {
                Rect listRect = new Rect(rect.x, rect.y + listStyle.CalcHeight(listContent[0], 1.0f) + 10,
                          rect.width, listStyle.CalcHeight(listContent[0], 1.0f) * listContent.Length);

                GUI.Box(listRect, "", boxStyle);
                int newSelectedItemIndex = GUI.SelectionGrid(listRect, selectedItemIndex, listContent, 1, listStyle);
                if (newSelectedItemIndex != selectedItemIndex)
                {
                    selectedItemIndex = newSelectedItemIndex;
                    buttonContent = listContent[selectedItemIndex];
                }
            }

            if (done)
                isClickedComboButton = false;

            return selectedItemIndex;
        }

        public int SelectedItemIndex
        {
            get
            {
                return selectedItemIndex;
            }
            set
            {
                selectedItemIndex = value;
            }
        }
    }
}