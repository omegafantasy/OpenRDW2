using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PathSeed = VirtualPathGenerator.PathSeed;
using AvatarInfo = ExperimentSetup.AvatarInfo;
using System.IO;
using System;
using TMPro;

//Store common parameters 
public class GlobalConfiguration : MonoBehaviour
{
    const float INF = 100000;
    const float EPS = 1e-5f;
    public List<SingleSpace> physicalSpaces;
    public SingleSpace virtualSpace;

    private float pathCircleRadius;
    private int pathCircleWaypointNum = 20;

    //square: size varied tracking space, size= squareSpaceWidth
    //others: shape varied tracking space, area= TARGET_AREA
    public enum TrackingSpaceChoice
    {
        //Trapezoid, Cross, L_shape
        FilePath, Square, Triangle, Rectangle, T_shape
    };
    public enum MovementController { Keyboard, AutoPilot, HMD };//Three input modes

    //Path mode
    //FilePath: constant walking speed
    //RealUserPath: real walking speed according to sample points    
    public enum PathSeedChoice { _90Turn, RandomTurn, StraightLine, Sawtooth, Circle, FigureEight, FilePath, RealUserPath, VEPath, ValidPath };

    public static float obstacleParentHeight = 0.0018f;
    public static float bufferParentHeight = 0.0016f;

    #region Experiment 

    [Header("Experiment")]

    [Tooltip("How avatar movement will be controlled")]
    public MovementController movementController = MovementController.HMD;
    [Tooltip("If all avatar reset together")]
    public bool synchronizedReset = true;

    [HideInInspector]
    public float timeStep = 0.5f;
    [HideInInspector]
    public int directionSamples = 30;
    [HideInInspector]
    public int timeStepBase;
    [HideInInspector]
    public int timeStepCountDown;

    [Tooltip("If run in backstage without visualization")]
    public bool runInBackstage = false;

    [Tooltip("If load configuration from commond txt file")]
    public bool loadFromTxt = false;

    [Tooltip("If load a directory which contains multiple command files for testing")]
    public bool multiCmdFiles = false;

    [Tooltip("If use a friendly panel when resetting")]
    public bool useResetPanel;
    [Tooltip("If use a crystal-like virtual waypoint")]
    public bool useCrystalWaypoint;

    [Tooltip("If use targetFPS, otherwise use system time")]
    public bool useSimulationTime;

    [Tooltip("If show in overview mode when every trial begins")]
    public bool overviewModeEveryTrial;

    [Tooltip("In Following mode, if show in first Person view, otherwise the third person view will be shown")]
    public bool firstPersonView;

    private bool firstPersonViewOldChoice;
    [Tooltip("Total virtual path length for each avatar")]
    [Range(50, 600)]
    public float pathLength = 400;//procedurally generated path length  
    [Tooltip("Default random seed for path generation")]
    public int DEFAULT_RANDOM_SEED = 23;

    [Tooltip("Translation gain (dilate)")]
    [Range(1, 3)]
    public float MAX_TRANS_GAIN = 1.26f;

    [Tooltip("Translation gain (compress)")]
    [Range(0.01f, 1)]
    public float MIN_TRANS_GAIN = 0.86f;

    [Tooltip("Rotation gain (dilate)")]
    [Range(1, 3)]
    public float MAX_ROT_GAIN = 1.49f;

    [Tooltip("Rotation gain (compress)")]
    [Range(0.01f, 1)]
    public float MIN_ROT_GAIN = 0.8f;

    [Tooltip("Radius applied by curvature gain")]
    [Range(1, 23)]
    public float CURVATURE_RADIUS = 7.5f;

    [Tooltip("Buffer width for triggering reset")]
    [SerializeField, Range(0f, 1f)]
    public float RESET_TRIGGER_BUFFER = 0.5f;

    [HideInInspector]
    public float simulatedTime = 0;//accumulated simulation time, clear after each trial

    [Tooltip("Target simulated frame rate in auto-pilot mode, the time 1/targetFPS will pass after each update")]
    public float targetFPS = 50;

    [Tooltip("Number of trails will be repeated")]
    [Range(1, 10)]
    public int trialsForRepeating;

    [HideInInspector]
    public List<ExperimentSetup> experimentSetups;//experimentSetup of one command file

    [HideInInspector]
    public List<List<ExperimentSetup>> experimentSetupsList;//experimentSetups of muliple command files

    private int trialsForCurrentExperiment = 5;

    [HideInInspector]
    public int experimentIterator = 0;//command group Id

    [HideInInspector]
    public int experimentSetupsListIterator = 0;//command file id
    private bool experimentComplete = false;//if experiment Completes

    [HideInInspector]
    public bool experimentInProgress = false;//if experiment is running

    //false indicates this is the first call of this trial
    [HideInInspector]
    public bool avatarIsWalking;

    //start time string of this program
    [HideInInspector]
    public string startTimeOfProgram;

    [Tooltip("APF force vector 3d model")]
    public GameObject negArrow;

    [HideInInspector]
    public bool readyToStart;//If all users are ready for the real walking experiment

    private bool firstTimePressR = true;//it is the first time to press key R

    #endregion

    #region Avatar

    [Header("Avatar")]
    [Tooltip("Avatar number")]
    [Range(1, 6)]
    public int avatarNum;

    [Tooltip("Pre-defined colors for avatars")]
    public Color[] avatarColors;

    [Tooltip("Which avatar prefab will be used for visualization")]
    public int avatarPrefabId;

    [Tooltip("Candidate Avatar Prefabs(models), you can drag your custom avatar here")]
    public GameObject[] avatarPrefabs;


    [Tooltip("Animator Controller controls the avatar's animation")]
    public RuntimeAnimatorController animatorController;

    public List<GameObject> redirectedAvatars;

    [Tooltip("Translation speed in meters per second.")]
    [Range(0.01f, 10)]
    public float translationSpeed = 1f;

    [Tooltip("Rotation speed in degrees per second.")]
    [Range(0.01f, 360)]
    public float rotationSpeed = 90f;

    [HideInInspector]
    public int currentShownAvatarId;//which avatar is shown currently, -1 indicates show all

    public List<GameObject> avatarRepresentations;//avatar representations in overview mode

    [HideInInspector]
    public List<int> avatarIdSortedFromHighPriorityToLow;//priority of avatar id (large first), update after customFunction executed

    #endregion

    #region Tracking Space
    [Header("Tracking Space")]

    [Tooltip("If the virtual world is visible")]
    public bool virtualWorldVisible = false;
    //check if this parameter changes at runtime
    private bool virtualWorldVisibleOldChoice;

    [Tooltip("If the tracking space is visible")]
    public bool trackingSpaceVisible = false;
    //check if this parameter changes at runtime
    private bool trackingSpaceVisibleOldChoice;


    [Tooltip("If buffers are visible")]
    public bool bufferVisible;
    //check if this parameter changes at runtime
    private bool bufferVisibleOldChoice;

    [Tooltip("If visualize obstacle in 3d")]
    public bool if3dObstacle;

    [Tooltip("Selected tracking space")]
    public TrackingSpaceChoice trackingSpaceChoice;

    [Tooltip("Obstacle setting")]
    [Range(0, 2)]
    public int obstacleType;

    [Tooltip("The height of obstacle if if3dObstacle is true")]
    [Range(0.01f, 1f)]
    public float obstacleHeight;

    [Tooltip("File path of customized tracking space")]
    public string trackingSpaceFilePath;

    [Tooltip("Side length if trackingSpace == Square")]
    public float squareWidth;

    public Color virtualObstacleColor;

    public Color obstacleColor;

    public Color bufferColor;

    [HideInInspector]
    public Transform virtualSpaceObject = null;

    // [HideInInspector]
    // public List<Vector2> trackingSpacePoints;//trackingSpace points，stored in counter-clockwise direction

    // [HideInInspector]
    // public List<List<Vector2>> obstaclePolygons;//obstacle points，stored in counter-clockwise direction

    public Material transparentMat;//material for transparent buffer

    public Material trackingSpacePlaneMat;//material for tracking space plane

    private List<GameObject> planesForAllAvatar;//plane gameobject in overview mode
    private List<GameObject> bufferRepresentations;//for buffer visualization

    public GameObject virtualWorld { get; private set; }//
    public bool useVECollision = false;
    #endregion

    #region Path

    [Header("Path")]
    [Tooltip("The first waypoint is startPoint or next point")]
    public bool firstWayPointIsStartPoint;

    [Tooltip("If align the next waypoint to the initial direction")]
    public bool alignToInitialForward;

    [Tooltip("If draw real trails")]
    public bool drawRealTrail;

    [Tooltip("If draw virtual trails")]
    public bool drawVirtualTrail;

    [Tooltip("Maximum distance requirement to trigger next waypoint")]
    public float distanceToWaypointThreshold = 0.05f;

    [Tooltip("how long the visualization of the trail should be reserved, -1 indicate show permanently")]
    public int trailVisualTime = 8;

    [Tooltip("the color of real trail")]
    public Color realTrailColor;

    [Tooltip("the color of virtual trail")]
    public Color virtualTrailColor;

    private List<List<TrailDrawer.Vertice>> realTrailPoints;//real trail in overview mode, realTrailPoints[i][j] indicates avatar i's number j point    
    private List<List<TrailDrawer.Vertice>> virtualSpaceTrailPoints;
    private List<GameObject> realTrailList;//trail obj in overview mode
    private List<GameObject> virtualSpaceTrailList;

    #endregion

    #region Analysis

    [Header("Analysis")]
    [Tooltip("If export the image of simulation paths")]
    public bool exportImage;


    [HideInInspector]
    public StatisticsLogger statisticsLogger;

    #endregion


    #region Passive Haptics
    [System.Serializable]
    public class PhysicalTargetTransform
    {
        public Vector2 position;
        public Vector2 forward;
    }
    [Header("Passive Haptics")]
    public bool passiveHaptics;//if use passive haptics mode    
    public List<PhysicalTargetTransform> physicalTargetTransforms;//real target position and direction corresponding to each avatar

    [HideInInspector]
    public List<GameObject> physicalTargets;//real target representation(green arrow)

    #endregion

    #region Networking
    [Header("Networking")]
    [Tooltip("If use network for synchronization")]
    public bool networkingMode;

    private NetworkManager networkManager;

    #endregion

    #region Camera        
    [HideInInspector]
    public Camera cameraVirtualTopForAllAvatars;//virtualCam for all avatars
    [HideInInspector]
    public TextMeshPro signText;
    #endregion


    #region Others

    [HideInInspector]
    public UserInterfaceManager userInterfaceManager;

    #endregion

    const int AVATAR_LAYER = 8;//jon: the layer for avatar objects, used in VE collision

    // redirection & reset phase for separatespace redirector
    public void SeparateSpaceDecision()
    {
        List<RedirectionManager> managers = new List<RedirectionManager>();
        List<SeparateSpace_Redirector> redirectors = new List<SeparateSpace_Redirector>();
        List<int> resetAvatars = new List<int>();
        List<int> safeAvatars = new List<int>();
        int n = avatarNum;
        for (int i = 0; i < n; i++)
        {
            managers.Add(redirectedAvatars[i].GetComponent<RedirectionManager>());
            redirectors.Add((SeparateSpace_Redirector)(managers[i].redirector));
        }

        bool triggerSyncReset = false;
        for (int i = 0; i < n; i++)
        {
            if (managers[i].resetter != null && !managers[i].inReset && managers[i].resetter.IsResetRequired() && managers[i].EndResetCountDown == 0)
            {
                triggerSyncReset = true;
                break;
            }
        }
        if (triggerSyncReset)
        { // when sync reset
            for (int i = 0; i < n; i++)
            {
                if (redirectors[i].resetTimeRange.Item2 <= redirectors[i].timeToWayPoint)
                {// max<=T
                    resetAvatars.Add(i);
                }
                else
                {
                    safeAvatars.Add(i);
                }
            }
            if (resetAvatars.Count == 0)
            {//no need to reset
                foreach (var rd in redirectors)
                {
                    rd.ResetAndGuideToSafePos();
                }
            }
            else
            {//need to reset
                float timeToReset = 100000f;
                foreach (var id in resetAvatars)
                {
                    timeToReset = redirectors[id].resetTimeRange.Item2 < timeToReset ? redirectors[id].resetTimeRange.Item2 : timeToReset;
                }

                foreach (var id in resetAvatars)
                {
                    if (redirectors[id].resetTimeRange.Item2 == timeToReset)
                    {
                        redirectors[id].ResetAndGuideToFurthest();
                    }
                    else
                    {
                        redirectors[id].ResetAndGuideToMaxTimePos(timeToReset);
                    }

                }
                foreach (var id in safeAvatars)
                {
                    redirectors[id].ResetAndGuideToMaxTimePos(timeToReset);
                }
            }
        }
        else
        { // when someone hit a waypoint
            for (int i = 0; i < n; i++)
            {
                if (redirectors[i].collisionTimeRange.Item2 <= redirectors[i].timeToWayPoint)
                {// max<=T
                    resetAvatars.Add(i);
                }
                else
                {
                    safeAvatars.Add(i);
                }
            }
            if (resetAvatars.Count == 0)
            {//no need to reset
                foreach (var rd in redirectors)
                {
                    rd.GuideToSafePos();
                }
            }
            else
            {//need to reset
                float timeToCollide = 100000f;
                foreach (var id in resetAvatars)
                {
                    timeToCollide = redirectors[id].collisionTimeRange.Item2 < timeToCollide ? redirectors[id].collisionTimeRange.Item2 : timeToCollide;
                }

                foreach (var id in resetAvatars)
                {
                    if (redirectors[id].collisionTimeRange.Item2 == timeToCollide)
                    {
                        redirectors[id].GuideToFurthest();
                    }
                    else
                    {
                        redirectors[id].GuideToMaxTimePos(timeToCollide);
                    }
                }
                foreach (var id in safeAvatars)
                {
                    redirectors[id].GuideToMaxTimePos(timeToCollide);
                }
            }
        }
    }

    private void Awake()
    {
        startTimeOfProgram = Utilities.GetTimeString();
        statisticsLogger = GetComponent<StatisticsLogger>();

        userInterfaceManager = GetComponent<UserInterfaceManager>();
        cameraVirtualTopForAllAvatars = transform.Find("Virtual Top View Cam For All Avatars").GetComponent<Camera>();
        signText = cameraVirtualTopForAllAvatars.transform.GetChild(0).gameObject.GetComponent<TextMeshPro>();
        cameraVirtualTopForAllAvatars.transform.GetChild(0).gameObject.SetActive(true);
        virtualWorld = transform.Find("Virtual World").gameObject;

        if (movementController == MovementController.AutoPilot)
        {
            if (runInBackstage)
            {
                useSimulationTime = true;
            }
        }
        else
        {
            useSimulationTime = false;
            runInBackstage = false;
        }

        Initialize();

        // Initialization
        experimentIterator = 0;
        trialsForCurrentExperiment = trialsForRepeating;

        firstPersonViewOldChoice = firstPersonView;
        virtualWorldVisibleOldChoice = virtualWorldVisible;
        trackingSpaceVisibleOldChoice = trackingSpaceVisible;
        bufferVisibleOldChoice = bufferVisible;


        //networking
        networkManager = GetComponentInChildren<NetworkManager>(true);
        networkManager.gameObject.SetActive(networkingMode);
        if (networkingMode)
        {
            avatarNum = 1;
        }
        pathCircleRadius = pathLength / 2 / Mathf.PI;
        timeStepBase = (int)(timeStep * targetFPS);
        timeStepCountDown = 0;
    }
    public List<Vector2> RotateWaypoints(List<Vector2> waypoints, float angle)
    {
        var rot = angle;
        var newWaypoints = new List<Vector2>();
        foreach (var p in waypoints)
        {
            var newP = Utilities.RotateVector(p, rot);
            newWaypoints.Add(newP);
        }
        return newWaypoints;
    }

    // public List<Vector2> RandomRotateWaypoints(List<Vector2> waypoints)
    // {
    //     var rot = UnityEngine.Random.Range(0f, 360f);
    //     return RotateWaypoints(waypoints, rot);
    // }

    // Start is called before the first frame update
    void Start()
    {
        //Load from command txt file
        if (loadFromTxt)
        {
            userInterfaceManager.GetCommandFilePaths();
            //generate experimentSetups according to command txt
            GenerateExperimentSetupsByCommandFiles();
        }
        else
        {
            //generate experimentSetups according to UI settings
            GenerateExperimentSetupsByUI();
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (movementController == MovementController.AutoPilot)
        {
            //omit rendering process
            if (runInBackstage)
            {
                //disable trail drawing
                drawRealTrail = false;
                drawVirtualTrail = false;
                RunInBackstage();
            }
            else
            {//render frames                
                MakeOneStepCycle();
            }
        }
        else
        {
            if (movementController == MovementController.Keyboard)
                MakeOneStepCycle();
            else if (movementController == MovementController.HMD)
            {
                //press R key to start logging
                if (readyToStart)
                    MakeOneStepCycle();
            }
        }

        //change person view at runtime
        if (firstPersonViewOldChoice != firstPersonView)
        {
            firstPersonViewOldChoice = firstPersonView;
            SwitchPersonView(firstPersonView);
        }

        //change virtual world visibility at runtime
        if (virtualWorldVisibleOldChoice != virtualWorldVisible)
        {
            virtualWorldVisibleOldChoice = virtualWorldVisible;
            virtualWorld.SetActive(virtualWorldVisible);
        }

        //change virtual world visibility at runtime
        if (trackingSpaceVisibleOldChoice != trackingSpaceVisible)
        {
            trackingSpaceVisibleOldChoice = trackingSpaceVisible;
            ChangeTrackingSpaceVisibility(trackingSpaceVisible);
        }

        //change buffer visibility at runtime
        if (bufferVisibleOldChoice != bufferVisible)
        {
            bufferVisibleOldChoice = bufferVisible;
            ChangeBufferVisibility(bufferVisible);
        }

        //keycode relative
        for (var i = KeyCode.Alpha0; i <= KeyCode.Alpha9; i++)
            if (Input.GetKeyDown(i))
            {
                ShowOnlyOneAvatar(i - KeyCode.Alpha0);
            }
        if (Input.GetKeyDown(KeyCode.BackQuote))
        {
            ShowAllAvatars();
        }
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ShowAllAvatarsInVirtual();
        }
        //press key Q to stop Experiment manually
        if (Input.GetKeyDown(KeyCode.Q))
        {
            EndExperiment(1);
        }
        //press Key S to take a snap shot
        if (Input.GetKeyDown(KeyCode.P))
        {
            Utilities.CaptureScreenShot(statisticsLogger.SCREENSHOTS_DERECTORY + Utilities.GetTimeStringForFileName() + ".png", statisticsLogger.superSize);
        }
        //press key R to confirm ready
        if (Input.GetKeyDown(KeyCode.R))
        {
            readyToStart = true;

            //networkingMode, update target avatar number
            if (networkingMode && !loadFromTxt && firstTimePressR)
                GenerateExperimentSetupsByUI();//regenerate experiment setups

            firstTimePressR = false;
        }
        if (Input.GetKeyDown(KeyCode.T))
        {
            firstPersonView = !firstPersonView;
        }
        UpdateUI();
    }
    public void UpdateUI()
    {
        userInterfaceManager.SetActivePanelExperimentComplete(experimentComplete);
    }
    //switch to first person view or third person view
    public void SwitchPersonView(bool ifFirstPersonView)
    {
        foreach (var avatar in redirectedAvatars)
        {
            var vm = avatar.GetComponent<VisualizationManager>();
            vm.SwitchPersonView(ifFirstPersonView);
        }
    }
    public void ChangeTrackingSpaceVisibility(bool ifVisible)
    {
        trackingSpaceVisible = ifVisible;
        foreach (var avatar in redirectedAvatars)
        {
            var vm = avatar.GetComponent<VisualizationManager>();
            vm.ChangeTrackingSpaceVisibility(ifVisible);
        }
    }
    public void ChangeBufferVisibility(bool ifVisible)
    {
        bufferVisible = ifVisible;
        foreach (var avatar in redirectedAvatars)
        {
            var vm = avatar.GetComponent<VisualizationManager>();
            vm.SetBufferVisibility(bufferVisible);
        }
        foreach (var br in bufferRepresentations)
        {
            br.SetActive(ifVisible);
        }
    }
    public void UpdateSimulatedTime()
    {
        simulatedTime += 1.0f / targetFPS;
        foreach (var avatar in redirectedAvatars)
        {
            var rm = avatar.GetComponent<RedirectionManager>();
            rm.UpdateRedirectionTime();
        }
    }
    //Switch to folloing mode, show the avatar with avatarId
    public void ShowOnlyOneAvatar(int avatarId)
    {
        if (avatarId == -1)
            ShowAllAvatars();

        if (avatarId < 0 || avatarId >= redirectedAvatars.Count)
        {
            Debug.Log("avatarId out of range!");
            return;
        }

        currentShownAvatarId = avatarId;

        for (int i = 0; i < redirectedAvatars.Count; i++)
        {
            ShowAvatar(i, i == avatarId);
        }

        ChangeToAllAvatarView(false);
        if (virtualSpaceTrailList != null)
            foreach (var t in virtualSpaceTrailList)
                t.SetActive(false);
        if (virtualSpaceObject != null)
        {
            virtualSpaceObject.gameObject.SetActive(false);
        }

        signText.enabled = false;
    }
    public void ShowAllAvatarsInVirtual()
    {
        for (int i = 0; i < redirectedAvatars.Count; i++)
        {
            redirectedAvatars[i].GetComponent<VisualizationManager>().SetVisibilityInVirtual(true);
            redirectedAvatars[i].GetComponent<VisualizationManager>().SetRealTargetVisibility(false);
        }
        ChangeToAllAvatarView(false);
        cameraVirtualTopForAllAvatars.enabled = true;
        if (virtualSpaceObject != null)
        {
            virtualSpaceObject.gameObject.SetActive(true);
        }
        signText.text = "Virtual Space";
        signText.enabled = true;
    }

    [ContextMenu("ChangeToOverviewMode")]
    //Switch to overview mode    
    public void ShowAllAvatars()
    {
        //Debug.Log("ShowAllAvatars");
        currentShownAvatarId = -1;
        for (int i = 0; i < redirectedAvatars.Count; i++)
        {
            ShowAvatar(i, false);
        }
        ChangeToAllAvatarView(true);
        virtualSpaceObject.gameObject.SetActive(false);
        signText.text = "Physical Space";
        signText.enabled = true;
    }

    //If change to overview mode    
    public void ChangeToAllAvatarView(bool bl)
    {
        cameraVirtualTopForAllAvatars.enabled = bl;
        foreach (var plane in planesForAllAvatar)
        {
            plane.SetActive(bl);
        }

        if (realTrailList != null)
            foreach (var t in realTrailList)
                t.SetActive(bl);
        if (virtualSpaceTrailList != null)
            foreach (var t in virtualSpaceTrailList)
                t.SetActive(!bl);
        if (avatarRepresentations != null)
            foreach (var r in avatarRepresentations)
                r.SetActive(bl);
    }
    //if show this avatar
    public void ShowAvatar(int avatarId, bool ifShow)
    {
        var av = redirectedAvatars[avatarId];
        av.GetComponent<VisualizationManager>().SetVisibility(ifShow);
        av.GetComponent<VisualizationManager>().SetRealTargetVisibility(true);
    }
    //Customizable function, called before every cycle
    public void CustomFunctionCalledInEveryStep()
    {
        avatarIdSortedFromHighPriorityToLow = new List<int>();
        for (int i = 0; i < redirectedAvatars.Count; i++)
        {
            avatarIdSortedFromHighPriorityToLow.Add(i);
            var r = redirectedAvatars[i].GetComponent<RedirectionManager>().redirector;
            if (r != null)
                r.GetPriority();
        }
        //large priority first
        avatarIdSortedFromHighPriorityToLow.Sort(delegate (int x, int y)
        {
            return redirectedAvatars[y].GetComponent<RedirectionManager>().priority.CompareTo(redirectedAvatars[x].GetComponent<RedirectionManager>().priority);
        });
    }

    //large cycle
    public void MakeOneStepCycle()
    {
        //experiment is finished
        if (experimentIterator >= experimentSetups.Count)
            return;
        if (networkingMode && !readyToStart)
            return;

        UpdateSimulatedTime();
        MakeOneStepMovement();
        MakeOneStepRedirection();

        for (var id = 0; id < redirectedAvatars.Count; id++)
        {
            var mm = redirectedAvatars[id].GetComponent<MovementManager>();
            var rm = redirectedAvatars[id].GetComponent<RedirectionManager>();
            var vm = redirectedAvatars[id].GetComponent<VisualizationManager>();
            vm.UpdateVisualizations();

            //draw real trails in overview mode
            if (drawRealTrail)
                TrailDrawer.UpdateTrailPoints(realTrailPoints[id], rm.trackingSpace.transform, realTrailList[id].GetComponent<MeshFilter>().mesh,
                    Utilities.FlattenedPos3D(rm.headTransform.position, TrailDrawer.PATH_HEIGHT), trailVisualTime);
            if (drawVirtualTrail)
                TrailDrawer.UpdateTrailPoints(virtualSpaceTrailPoints[id], transform, virtualSpaceTrailList[id].GetComponent<MeshFilter>().mesh,
                Utilities.FlattenedPos3D(rm.headTransform.position, TrailDrawer.PATH_HEIGHT), trailVisualTime);

            var prePos = avatarRepresentations[id].transform.position;
            avatarRepresentations[id].transform.localPosition = new Vector3(rm.currPosReal.x, avatarRepresentations[id].transform.localPosition.y, rm.currPosReal.z);
            avatarRepresentations[id].transform.localRotation = Quaternion.LookRotation(rm.currDirReal, Vector3.up);
        }

        //if this experiment trial finished
        bool ifExperimentEnd = true;
        for (int i = 0; i < redirectedAvatars.Count; i++)
        {
            var us = redirectedAvatars[i];
            if (!us.GetComponent<MovementManager>().ifMissionComplete)
            {
                ifExperimentEnd = false;
                break;
            }
        }

        bool ifInvalidAvatarExist = false;
        for (int i = 0; i < redirectedAvatars.Count; i++)
        {
            var us = redirectedAvatars[i];
            if (us.GetComponent<MovementManager>().ifInvalid)
            {
                ifInvalidAvatarExist = true;
                Debug.Log("ifInvalid: " + i);
                break;
            }
        }

        if (ifInvalidAvatarExist)
        {
            EndExperiment(-1);
        }
        else if (ifExperimentEnd)
        {
            EndExperiment(0);
        }
    }
    [ContextMenu("EndExperiment")]
    public void EndExperimentMenu()
    {
        EndExperiment(1);
    }
    public void MakeOneStepMovement()
    {
        if (!experimentInProgress && experimentIterator < experimentSetups.Count)
        {
            StartNextExperiment();
        }

        if (experimentInProgress && !avatarIsWalking)
        {
            avatarIsWalking = true;
            // Start Logging
            statisticsLogger.BeginLogging();
        }

        CustomFunctionCalledInEveryStep();

        for (int i = 0; i < avatarIdSortedFromHighPriorityToLow.Count; i++)
        {
            redirectedAvatars[avatarIdSortedFromHighPriorityToLow[i]].GetComponent<MovementManager>().MakeOneStepMovement();
        }
    }

    public void Initialize()
    {
        simulatedTime = 0;
    }

    //make one step redirection
    public void MakeOneStepRedirection()
    {
        bool touchWaypoint = false;
        bool triggerSyncReset = false;
        bool inSyncReset = false;
        for (int i = 0; i < avatarNum; i++)
        {
            RedirectionManager remanager = redirectedAvatars[i].GetComponent<RedirectionManager>();
            if (remanager.touchWaypoint)
            {
                touchWaypoint = true;
                remanager.touchWaypoint = false;
            }
            if (remanager.resetter != null && remanager.inReset)
            {
                inSyncReset = true;
            }
            else if (remanager.resetter != null && !remanager.inReset && remanager.resetter.IsResetRequired() && remanager.EndResetCountDown == 0)
            {
                triggerSyncReset = true;
                for (int j = 0; j < avatarNum; j++)
                {
                    redirectedAvatars[j].GetComponent<RedirectionManager>().resetSign = true;
                }
                Debug.Log(i);
                break;
            }
        }
        if (redirectedAvatars[0].GetComponent<RedirectionManager>().redirectorChoice == RedirectionManager.RedirectorChoice.SeparateSpace)
        {
            if (touchWaypoint || triggerSyncReset)
            {
                for (int i = 0; i < avatarNum; i++)
                {
                    RedirectionManager rm = redirectedAvatars[i].GetComponent<RedirectionManager>();
                    SeparateSpace_Redirector rd = (SeparateSpace_Redirector)(rm.redirector);
                    rd.collisionParams = rd.GetCollisionParams(new Vector2(rm.currPosReal.x, rm.currPosReal.z), Utilities.FlattenedPos2D(Utilities.GetRelativePosition(rm.targetWaypoint.position, rm.trackingSpace.transform) - rm.currPosReal));
                    rd.resetTimeRange = rd.GetResetTimeRange(new Vector2(rm.currPosReal.x, rm.currPosReal.z));
                    rd.collisionTimeRange = rd.GetTimeRange(rd.collisionParams);
                    rd.timeToWayPoint = rd.GetTimeToWayPoint();
                }
                SeparateSpaceDecision();
            }
        }
        for (int i = 0; i < avatarIdSortedFromHighPriorityToLow.Count; i++)
        {
            redirectedAvatars[avatarIdSortedFromHighPriorityToLow[i]].GetComponent<RedirectionManager>().MakeOneStepRedirection();
        }

        //log data
        statisticsLogger.UpdateStats();
    }
    private void GenerateExperimentSetupsByCommandFiles()
    {
        experimentSetupsList = new List<List<ExperimentSetup>>();
        foreach (var cf in userInterfaceManager.commandFiles)
        {
            GenerateAllExperimentSetupsByCommand(out List<ExperimentSetup> expTmp, File.ReadAllLines(cf));
            experimentSetupsList.Add(expTmp);
        }
        if (experimentSetupsList.Count > 0)
            experimentSetups = experimentSetupsList[0];
        else
            experimentSetups = new List<ExperimentSetup>();
    }
    private void GenerateExperimentSetupsByUI()
    {
        experimentSetupsList = new List<List<ExperimentSetup>>();
        // Here we generate the corresponding experiments
        experimentSetups = new List<ExperimentSetup>();

        for (; redirectedAvatars.Count < avatarNum;)
        {
            var newAvatar = CreateNewRedirectedAvatar(redirectedAvatars.Count);
            redirectedAvatars.Add(newAvatar);
        }

        GenerateTrackingSpace(redirectedAvatars.Count, out physicalSpaces, out virtualSpace);

        var avatarList = new List<AvatarInfo>();
        int physicalSpaceIndex = 0;
        int avatarIndex = 0;
        for (int i = 0; i < redirectedAvatars.Count; i++)
        {
            var ra = redirectedAvatars[i];
            var mm = ra.GetComponent<MovementManager>();
            mm.physicalInitPose = physicalSpaces[physicalSpaceIndex].initialPoses[avatarIndex];
            mm.physicalSpaceIndex = physicalSpaceIndex;
            if (virtualSpace != null)
            {
                mm.virtualInitPose = virtualSpace.initialPoses[i];
            }
            else
            {
                mm.virtualInitPose = mm.physicalInitPose;
            }

            mm.InitializeWaypointsPattern(DEFAULT_RANDOM_SEED);
            mm.randomSeed = DEFAULT_RANDOM_SEED;
            var avatarInfo = mm.GetCurrentAvatarInfo();
            avatarList.Add(avatarInfo);
            avatarIndex++;
            if (avatarIndex >= physicalSpaces[physicalSpaceIndex].initialPoses.Count)
            {
                physicalSpaceIndex++;
                avatarIndex = 0;
            }
        }
        for (int i = 0; i < trialsForCurrentExperiment; i++)
        {
            experimentSetups.Add(new ExperimentSetup(
                avatarList, physicalSpaces, virtualSpace, trackingSpaceChoice, trackingSpaceFilePath, squareWidth
                , obstacleType, pathLength));
        }
        experimentSetupsList.Add(experimentSetups);
    }

    private InitialPose DecodeInitialPose(string s)
    {
        var split = s.Split(',');
        return new InitialPose(new Vector2(float.Parse(split[0]), float.Parse(split[1])), new Vector2(float.Parse(split[2]), float.Parse(split[3])));
    }
    public static PathSeedChoice DecodePathSeedChoice(string s)
    {
        switch (s.ToLower())
        {
            case "90turn":
                return PathSeedChoice._90Turn;
            case "randomturn":
                return PathSeedChoice.RandomTurn;
            case "straightline":
                return PathSeedChoice.StraightLine;
            case "sawtooth":
                return PathSeedChoice.Sawtooth;
            case "circle":
                return PathSeedChoice.Circle;
            case "figureeight":
                return PathSeedChoice.FigureEight;
            case "filepath":
                return PathSeedChoice.FilePath;
            case "realuserpath":
                return PathSeedChoice.RealUserPath;
            default:
                return PathSeedChoice.Sawtooth;
        }
    }

    //get TrackingSpaceChoice from txt
    private TrackingSpaceChoice DecodeTrackingSpaceChoice(string s)
    {
        switch (s.ToLower())
        {
            case "triangle":
                return TrackingSpaceChoice.Triangle;
            case "square":
                return TrackingSpaceChoice.Square;
            case "rectangle":
                return TrackingSpaceChoice.Rectangle;
            case "t_shape":
                return TrackingSpaceChoice.T_shape;
            case "filepath":
                return TrackingSpaceChoice.FilePath;
            default:
                return TrackingSpaceChoice.Square;
        }
    }
    private void GenerateAllExperimentSetupsByCommand(out List<ExperimentSetup> experimentSetups, string[] commands)
    {
        experimentSetups = new List<ExperimentSetup>();
        AvatarInfo avatar = new AvatarInfo(typeof(NullRedirector), typeof(NullResetter), PathSeedChoice.StraightLine, null, null, null, null, null, null, null, 0, DEFAULT_RANDOM_SEED);
        var avatarList = new List<AvatarInfo>();
        trackingSpaceChoice = TrackingSpaceChoice.Square;
        obstacleType = 0;

        foreach (var line in commands)
        {
            if (line.Trim().Length == 0)
                continue;
            var split = line.Split('=');
            for (int i = 0; i < split.Length; i++)
            {
                split[i] = split[i].Trim();
            }
            switch (split[0].ToLower())
            {
                case "newuser":
                    AddAvatarToAvatarListWhenDealingCommand(ref avatarList, ref avatar);//add a new user
                    break;
                case "randomseed":
                    avatar.randomSeed = int.Parse(split[1]);
                    break;
                case "redirector":
                    avatar.redirector = RedirectionManager.DecodeRedirector(split[1]);
                    break;
                case "resetter":
                    avatar.resetter = RedirectionManager.DecodeResetter(split[1]);
                    break;
                case "pathseedchoice":
                    avatar.pathSeedChoice = DecodePathSeedChoice(split[1]);
                    break;
                case "waypointsfilepath":
                    avatar.waypointsFilePath = split[1];//first waypoint should align with the initial avatar position
                    break;
                case "samplingintervalsfilepath":
                    avatar.samplingIntervalsFilePath = split[1];
                    break;
                case "vepathname"://jon: newly added VEPath name, when pathSeedChoice==PathSeedChoice.VEPath
                    avatar.vePathName = split[1];
                    break;
                case "trackingspacechoice":
                    trackingSpaceChoice = DecodeTrackingSpaceChoice(split[1]);
                    break;
                case "obstacletype":
                    obstacleType = int.Parse(split[1]);
                    break;
                case "squarewidth":
                    squareWidth = float.Parse(split[1]);
                    break;
                case "trackingspacefilepath":
                    trackingSpaceFilePath = split[1];
                    break;
                case "physicalinitpose":
                    avatar.physicalInitPose = DecodeInitialPose(split[1]);
                    break;
                case "virtualinitpose":
                    avatar.virtualInitPose = DecodeInitialPose(split[1]);
                    break;

                //general parameters
                case "max_trans_gain":
                    MAX_TRANS_GAIN = float.Parse(split[1]);
                    break;
                case "min_trans_gain":
                    MIN_TRANS_GAIN = float.Parse(split[1]);
                    break;
                case "max_rot_gain":
                    MAX_ROT_GAIN = float.Parse(split[1]);
                    break;
                case "min_rot_gain":
                    MIN_ROT_GAIN = float.Parse(split[1]);
                    break;
                case "curvature_radius":
                    CURVATURE_RADIUS = float.Parse(split[1]);
                    break;
                case "reset_trigger_buffer":
                    RESET_TRIGGER_BUFFER = float.Parse(split[1]);
                    break;
                case "pathlength":
                    pathLength = float.Parse(split[1]);
                    break;

                //handle experiment setup
                case "end":
                    // AddAvatarToAvatarListWhenDealingCommand(ref avatarList, ref avatar);
                    // GenerateWaypoints(avatar.randomSeed, avatar.pathSeedChoice, avatar.virtualInitPose, avatar.waypointsFilePath, avatar.samplingIntervalsFilePath, out avatar.waypoints, out avatar.samplingIntervals);
                    GenerateTrackingSpace(avatarList.Count, out physicalSpaces, out virtualSpace);
                    int physicalSpaceIndex = 0;
                    int avatarIndex = 0;
                    for (int i = 0; i < avatarList.Count; i++)
                    {
                        avatarList[i].physicalInitPose = physicalSpaces[physicalSpaceIndex].initialPoses[avatarIndex];
                        avatarList[i].virtualInitPose = virtualSpace.initialPoses[i];
                        avatarList[i].physicalSpaceIndex = physicalSpaceIndex;
                        GenerateWaypoints(avatarList[i].randomSeed, avatarList[i].pathSeedChoice, avatarList[i].virtualInitPose,
                            avatarList[i].waypointsFilePath, avatarList[i].samplingIntervalsFilePath, out avatarList[i].waypoints, out avatarList[i].samplingIntervals);
                        avatarIndex++;
                        if (avatarIndex >= physicalSpaces[physicalSpaceIndex].initialPoses.Count)
                        {
                            physicalSpaceIndex++;
                            avatarIndex = 0;
                        }
                    }
                    experimentSetups.Add(new ExperimentSetup(avatarList, physicalSpaces, virtualSpace, trackingSpaceChoice, trackingSpaceFilePath, squareWidth, obstacleType, pathLength));

                    avatarList = new List<AvatarInfo>();//initialize for next trial setup
                    break;
                default:
                    Debug.LogError("Invalid command line: " + line);
                    break;
            }
        }
    }
    public void GenerateWaypoints(int randomSeed, PathSeedChoice pathSeedChoice, InitialPose initPose, string waypointsFilePath, string samplingIntervalsFilePath, out List<Vector2> waypoints, out List<float> samplingIntervals)
    {
        if (pathSeedChoice == PathSeedChoice.FilePath)
        {
            waypoints = LoadWaypointsFromFile(waypointsFilePath);
            samplingIntervals = null;
        }
        else if (pathSeedChoice == PathSeedChoice.RealUserPath)
        {
            waypoints = LoadWaypointsFromFile(waypointsFilePath);
            if (File.Exists(samplingIntervalsFilePath))
                samplingIntervals = LoadSamplingIntervalsFromFile(samplingIntervalsFilePath);
            else
            {
                Debug.LogError("sampleIntervalsFile does not exist: " + samplingIntervalsFilePath);
                samplingIntervals = null;
            }
        }
        else
        {
            waypoints = new List<Vector2>();
            float sumOfDistances, sumOfRotations;
            pathCircleRadius = pathLength / 2 / Mathf.PI;
            switch (pathSeedChoice)
            {
                case PathSeedChoice._90Turn:
                    waypoints = VirtualPathGenerator.GenerateInitialPathByPathSeed(randomSeed, PathSeed.GetPathSeed90Turn(), pathLength, out sumOfDistances, out sumOfRotations);
                    break;
                case PathSeedChoice.RandomTurn:
                    waypoints = VirtualPathGenerator.GenerateInitialPathByPathSeed(randomSeed, PathSeed.GetPathSeedRandomTurn(), pathLength, out sumOfDistances, out sumOfRotations);
                    break;
                case PathSeedChoice.StraightLine:
                    waypoints = VirtualPathGenerator.GenerateInitialPathByPathSeed(randomSeed, PathSeed.GetPathSeedStraightLine(), pathLength, out sumOfDistances, out sumOfRotations);
                    break;
                case PathSeedChoice.Sawtooth:
                    waypoints = VirtualPathGenerator.GenerateInitialPathByPathSeed(randomSeed, PathSeed.GetPathSeedSawtooth(), pathLength, out sumOfDistances, out sumOfRotations);
                    break;
                case PathSeedChoice.Circle:
                    waypoints = VirtualPathGenerator.GenerateCirclePath(randomSeed, pathCircleRadius, pathCircleWaypointNum, out sumOfDistances, out sumOfRotations);
                    break;
                case PathSeedChoice.FigureEight:
                    waypoints = VirtualPathGenerator.GenerateCirclePath(randomSeed, pathCircleRadius / 2, pathCircleWaypointNum, out sumOfDistances, out sumOfRotations, true);
                    break;
                case PathSeedChoice.ValidPath:
                    waypoints = VirtualPathGenerator.GenerateValidVirtualPath(randomSeed, PathSeed.GetPathSeedRandomTurn(), pathLength, initPose, virtualSpace, out sumOfDistances, out sumOfRotations);
                    break;
                default:
                    waypoints = VirtualPathGenerator.GenerateInitialPathByPathSeed(randomSeed, PathSeed.GetPathSeedRandomTurn(), pathLength, out sumOfDistances, out sumOfRotations);
                    break;
            }
            samplingIntervals = null;
        }
    }

    //load waypoints from txt
    public List<Vector2> LoadWaypointsFromFile(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogError("waypointsFilePath does not exist! :" + path);
            return null;
        }

        var re = new List<Vector2>();
        try
        {
            var content = File.ReadAllLines(path);
            int lineId = 0;
            var firstPoint = Vector2.zero;

            foreach (var line in content)
            {
                lineId++;//start from one

                string[] split;
                if (line.Contains(","))
                    split = line.Split(',');
                else
                    split = line.Split(new char[] { ',', ' ' });

                if (split.Length != 2)
                {
                    Debug.LogError("Input Waypoint File Error in Line: " + lineId);
                    break;
                }

                var x = float.Parse(split[0]);
                var y = float.Parse(split[1]);

                if (lineId == 1)
                {
                    firstPoint = new Vector2(x, y);
                }

                //if first wayPoint is start point，if true, update other points
                if (firstWayPointIsStartPoint)
                {
                    x -= firstPoint.x;
                    y -= firstPoint.y;
                }
                re.Add(new Vector2(x, y));
            }
        }
        catch
        {
            return re;
        }
        return re;
    }
    public List<float> LoadSamplingIntervalsFromFile(string sampleIntervalsFilePath)
    {
        var re = new List<float>();
        var lines = File.ReadAllLines(sampleIntervalsFilePath);
        foreach (var line in lines)
            re.Add(float.Parse(line));
        return re;
    }
    //add avatar to avatarList
    public void AddAvatarToAvatarListWhenDealingCommand(ref List<AvatarInfo> avatarList, ref AvatarInfo avatar)
    {
        // GenerateWaypoints(avatar.randomSeed, avatar.pathSeedChoice, avatar.virtualInitPose, avatar.waypointsFilePath, avatar.samplingIntervalsFilePath, out avatar.waypoints, out avatar.samplingIntervals);
        avatarList.Add(avatar);
        //change info based on the recent user
        avatar = avatar.Copy();
        //null indicates using the position returned by trackingSpace
        avatar.physicalInitPose = null;
        avatar.virtualInitPose = null;
    }

    //which avatar for visualization    
    public GameObject GetUserSelectedAvatarPrefab()
    {
        return avatarPrefabs[avatarPrefabId];
    }

    public GameObject CreateNewRedirectedAvatar(int avatarId)
    {
        var av0 = redirectedAvatars[0];
        var newAvatar = Instantiate(av0, av0.transform.position, av0.transform.rotation, av0.transform.parent);
        newAvatar.name = av0.name + "_" + avatarId;
        if (newAvatar.transform.Find("Body").childCount != 0)
        {
            Destroy(newAvatar.transform.Find("Body").GetChild(0).gameObject);
        }
        int planeIndex = 0;
        while (newAvatar.transform.Find("Plane" + planeIndex) != null)
        {
            Destroy(newAvatar.transform.Find("Plane" + planeIndex).gameObject);
            planeIndex++;
        }
        return newAvatar;
    }
    void StartNextExperiment()
    {
        Debug.Log(string.Format("---------- EXPERIMENT STARTED ----------"));
        Debug.Log(string.Format("trial:{0}/{1}, cmd file:{2}/{3}", experimentIterator + 1, experimentSetups.Count, experimentSetupsListIterator + 1, experimentSetupsList.Count));

        //get current experimentSetup
        ExperimentSetup setup = experimentSetups[experimentIterator];

        physicalSpaces = setup.physicalSpaces;
        trackingSpaceChoice = setup.trackingSpaceChoice;
        squareWidth = setup.squareWidth;
        obstacleType = setup.obstacleType;

        //get avatar configurations
        var avatarList = setup.avatars;

        //ensure the num of redirectedAvatars equals to the num in experiment setting
        while (redirectedAvatars.Count > avatarList.Count)
        {
            Destroy(redirectedAvatars[redirectedAvatars.Count - 1]);
            redirectedAvatars.RemoveAt(redirectedAvatars.Count - 1);
        }
        while (redirectedAvatars.Count < avatarList.Count)
        {
            var newAvatar = CreateNewRedirectedAvatar(redirectedAvatars.Count);
            redirectedAvatars.Add(newAvatar);
        }

        avatarNum = avatarList.Count;

        for (int id = 0; id < avatarList.Count; id++)
        {
            var mm = redirectedAvatars[id].GetComponent<MovementManager>();
            mm.LoadData(id, avatarList[id]); // reload data from experimentSetups
        }
        for (int id = 0; id < avatarList.Count; id++)
        {
            var vm = redirectedAvatars[id].GetComponent<VisualizationManager>();
            vm.Initialize(id);
            if (id == 0 && virtualSpace != null)
            {
                vm.GenerateVirtualSpaceMesh(virtualSpace);
                virtualSpaceObject = transform.Find("VirtualPlane");
            }
        }

        Initialize();

        GenerateTrackingSpaceMeshForAllAvatarView(physicalSpaces);

        if (realTrailList != null)
        {
            foreach (var t in realTrailList)
                Destroy(t);
        }
        if (avatarRepresentations != null)
        {
            foreach (var r in avatarRepresentations)
                Destroy(r);
        }
        realTrailList = new List<GameObject>();
        virtualSpaceTrailList = new List<GameObject>();
        avatarRepresentations = new List<GameObject>();
        realTrailPoints = new List<List<TrailDrawer.Vertice>>();
        virtualSpaceTrailPoints = new List<List<TrailDrawer.Vertice>>();

        for (int id = 0; id < avatarList.Count; id++)
        {
            var rTrail = TrailDrawer.GetNewTrailObj("realTrail" + id, avatarColors[id], transform);
            var vsTrail = TrailDrawer.GetNewTrailObj("virtualSpaceTrail" + id, avatarColors[id], transform);
            realTrailList.Add(rTrail);
            virtualSpaceTrailList.Add(vsTrail);
            realTrailPoints.Add(new List<TrailDrawer.Vertice>());
            virtualSpaceTrailPoints.Add(new List<TrailDrawer.Vertice>());

            GameObject avatarRepresentation;
            avatarRepresentation = CreateAvatar(transform, id, false);

            var animatorTmp = avatarRepresentation.GetComponent<Animator>();

            var mat = new Material(Shader.Find("Standard"));
            mat.color = avatarColors[id];
            foreach (var mr in avatarRepresentation.GetComponentsInChildren<MeshRenderer>())
            {
                mr.material = mat;
            }
            foreach (var mr in avatarRepresentation.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                mr.material = mat;
            }
            avatarRepresentations.Add(avatarRepresentation);
        }

        experimentInProgress = true;

        SwitchPersonView(firstPersonView);

        virtualWorld.SetActive(virtualWorldVisible);

        ChangeTrackingSpaceVisibility(trackingSpaceVisible);

        ChangeBufferVisibility(bufferVisible);

        if (passiveHaptics)
        {

            if (physicalTargets != null)
            {
                foreach (var obj in physicalTargets)
                    Destroy(obj);
            }
            physicalTargets = new List<GameObject>();
            if (redirectedAvatars.Count > physicalTargetTransforms.Count)
            {
                Debug.LogError("There are fewer physical targets than avatars");
            }
            else
            {
                for (int i = 0; i < redirectedAvatars.Count; i++)
                {
                    var physicalObjTransform = physicalTargetTransforms[i];
                    var physicalObj = Instantiate(negArrow);
                    physicalObj.transform.SetParent(redirectedAvatars[i].transform);
                    physicalObj.transform.localPosition = Utilities.UnFlatten(physicalObjTransform.position);
                    physicalObj.transform.forward = Utilities.UnFlatten(physicalObjTransform.forward).normalized;
                    foreach (var mr in physicalObj.GetComponentsInChildren<MeshRenderer>())
                    {
                        mr.material = new Material(Shader.Find("Standard"));
                        mr.material.color = Color.green;
                    }
                    physicalTargets.Add(physicalObj);
                }
            }
        }

        //follow avatar 0 by default
        var defaultId = 0;
        if (networkingMode)
        {
            defaultId = networkManager.avatarId;
        }

        redirectedAvatars[defaultId].transform.Find("[CameraRig]").gameObject.SetActive(movementController == MovementController.HMD);

        if (overviewModeEveryTrial)
        {
            ShowAllAvatars();
        }
        else
        {
            ShowOnlyOneAvatar(defaultId);
        }
    }

    //physical plane in overviewmode    
    public void GenerateTrackingSpaceMeshForAllAvatarView(List<SingleSpace> physicalSpaces)
    {
        // destroy old gameobjects
        if (planesForAllAvatar != null)
        {
            foreach (var plane in planesForAllAvatar)
            {
                Destroy(plane);
            }
        }

        // create new gameobjects
        planesForAllAvatar = new List<GameObject>();
        bufferRepresentations = new List<GameObject>();
        for (int i = 0; i < physicalSpaces.Count; i++)
        {
            var space = physicalSpaces[i];

            // plane
            var plane = new GameObject("PlaneForAllAvatar" + planesForAllAvatar.Count);
            plane.transform.SetParent(transform);
            var trackingSpaceMesh = TrackingSpaceGenerator.GeneratePolygonMesh(space.trackingSpace);
            plane.AddComponent<MeshFilter>().mesh = trackingSpaceMesh;
            var planeMr = plane.AddComponent<MeshRenderer>();
            planeMr.material = new Material(trackingSpacePlaneMat);
            planesForAllAvatar.Add(plane);

            // obstacle
            var obstacleParent = new GameObject().transform;
            obstacleParent.SetParent(planesForAllAvatar[i].transform);
            obstacleParent.name = "ObstacleParent";
            obstacleParent.localPosition = new Vector3(0, obstacleParentHeight, 0);
            obstacleParent.rotation = Quaternion.identity;
            TrackingSpaceGenerator.GenerateObstacleMesh(space.obstaclePolygons, obstacleParent, obstacleColor, if3dObstacle, obstacleHeight);

            // buffer
            var bufferParent = new GameObject().transform;
            bufferParent.SetParent(planesForAllAvatar[i].transform);
            bufferParent.name = "BufferParent";
            bufferParent.localPosition = Vector3.zero;
            bufferParent.rotation = Quaternion.identity;

            var trackingSpaceBufferMesh = TrackingSpaceGenerator.GenerateBufferMesh(space.trackingSpace, true, RESET_TRIGGER_BUFFER);
            AddBufferMesh(trackingSpaceBufferMesh, bufferParent);
            foreach (var obstaclePoints in space.obstaclePolygons)
            {
                var obstacleBufferMesh = TrackingSpaceGenerator.GenerateBufferMesh(obstaclePoints, false, RESET_TRIGGER_BUFFER);
                AddBufferMesh(obstacleBufferMesh, bufferParent);
            }
        }
    }

    public GameObject AddBufferMesh(Mesh bufferMesh, Transform bufferParent)
    {
        var obj = new GameObject("bufferMesh" + bufferRepresentations.Count);
        obj.transform.SetParent(bufferParent);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.rotation = Quaternion.identity;

        obj.AddComponent<MeshFilter>().mesh = bufferMesh;
        var mr = obj.AddComponent<MeshRenderer>();
        mr.material = new Material(transparentMat);
        mr.material.color = bufferColor;

        bufferRepresentations.Add(obj);
        return obj;
    }

    //endState of experiment, 0 indicates normal end, -1 indicates invalid data, 1 indicates end manually
    void EndExperiment(int endState)
    {
        if (experimentIterator >= experimentSetups.Count)
            return;

        //HMD mode, press key R to be ready for the next trial
        if (movementController == MovementController.HMD)
            readyToStart = false;

        ExperimentSetup setup = experimentSetups[experimentIterator];

        var avatarList = setup.avatars;
        for (int id = 0; id < avatarList.Count; id++)
        {
            var vm = redirectedAvatars[id].GetComponent<VisualizationManager>();
            var rm = redirectedAvatars[id].GetComponent<RedirectionManager>();

            // Disable Waypoint
            rm.targetWaypoint.gameObject.SetActive(false);
            vm.realWaypoint.gameObject.SetActive(false);
            // Disabling Redirectors
            rm.RemoveRedirector();
            rm.RemoveResetter();
        }

        if (passiveHaptics)
        {
            if (physicalTargets == null || physicalTargets.Count < redirectedAvatars.Count)
            {
                Debug.LogError("physicalTargets == null || physicalTargets.Count < redirectedAvatars.Count!");
            }
            else
            {
                for (int id = 0; id < redirectedAvatars.Count; id++)
                {
                    var posReal = Utilities.FlattenedPos2D(redirectedAvatars[id].GetComponent<RedirectionManager>().currPosReal);
                    var dirReal = Utilities.FlattenedPos2D(redirectedAvatars[id].GetComponent<RedirectionManager>().currDirReal);
                    var dist = (physicalTargetTransforms[id].position - posReal).magnitude;
                    var angle = Vector2.Angle(physicalTargetTransforms[id].forward, dirReal);
                    statisticsLogger.Event_Update_PassiveHaptics_Results(id, dist, angle);
                }
            }
        }

        avatarIsWalking = false;

        // Stop Logging
        statisticsLogger.EndLogging();

        // Gather Summary Statistics
        statisticsLogger.experimentResults.Add(statisticsLogger.GetExperimentResultForSummaryStatistics(endState, GetExperimentDescriptor(setup)));

        // Log Sampled Metrics
        if (statisticsLogger.logSampleVariables)
        {
            List<Dictionary<string, List<float>>> oneDimensionalSamples;
            List<Dictionary<string, List<Vector2>>> twoDimensionalSamples;
            statisticsLogger.GetExperimentResultsForSampledVariables(out oneDimensionalSamples, out twoDimensionalSamples);
            statisticsLogger.LogAllExperimentSamples(TrialIdToString(experimentIterator), oneDimensionalSamples, twoDimensionalSamples);
        }

        //save images
        if (exportImage)
            statisticsLogger.LogExperimentPathPictures(experimentIterator);

        //create temporary files to indicate the stage of the experiment
        File.WriteAllText(statisticsLogger.Get_TMP_DERECTORY() + "/" + experimentSetupsListIterator + "-" + experimentSetupsList.Count + " " + experimentIterator + "-" + experimentSetups.Count + ".txt", "");

        // Prepared for new experiment
        experimentIterator++;
        experimentInProgress = false;

        // Log All Summary Statistics To File
        if (experimentIterator == experimentSetups.Count)
        {
            GetResultDirAndFileName(statisticsLogger.SUMMARY_STATISTICS_DIRECTORY, out string resultDir, out string fileName);
            Debug.Log(string.Format("Save data to resultDir:{0}, fileName:{1}", resultDir, fileName));
            statisticsLogger.LogExperimentSummaryStatisticsResultsSCSV(statisticsLogger.experimentResults, resultDir, fileName);
            statisticsLogger.LogLightVersionCSV(statisticsLogger.experimentResults, resultDir, fileName + "_light");

            //initialize experiment results 
            statisticsLogger.InitializeExperimentResults();

            experimentSetupsListIterator++;
            if (experimentSetupsListIterator >= experimentSetupsList.Count)
            {
                Debug.Log("Last Experiment Complete, experimentSetups.Count == " + experimentSetups.Count);
                experimentComplete = true;//all trials end
            }
            else
            {//handle next experiment setup
                experimentIterator = 0;
                experimentSetups = experimentSetupsList[experimentSetupsListIterator];
            }
        }
    }
    public void GetResultDirAndFileName(string defaultDir, out string resultDir, out string fileName)
    {
        resultDir = defaultDir;
        fileName = "Result";
        if (loadFromTxt)
        {
            //path of command file 
            var commandFilePath = userInterfaceManager.commandFiles[experimentSetupsListIterator];
            var split = commandFilePath.Split(new char[] { '/', '\\' });
            if (multiCmdFiles)
                resultDir += split[split.Length - 2] + "/";
            fileName = split[split.Length - 1].Split('.')[0];
        }
    }

    public void GenerateTrackingSpace(int avatarNum, out List<SingleSpace> physicalSpaces, out SingleSpace virtualSpace)
    {
        //generate TrackingSpace by choice
        List<SingleSpace> rePhysicalSpaces = new List<SingleSpace>();
        SingleSpace reVirtualSpace = null;
        switch (trackingSpaceChoice)
        {
            case TrackingSpaceChoice.Rectangle:
                TrackingSpaceGenerator.GenerateRectangleTrackingSpace(obstacleType, out rePhysicalSpaces);
                break;
            case TrackingSpaceChoice.Triangle:
                TrackingSpaceGenerator.GenerateTriangleTrackingSpace(obstacleType, out rePhysicalSpaces);
                break;
            case TrackingSpaceChoice.T_shape:
                TrackingSpaceGenerator.GenerateT_ShapeTrackingSpace(obstacleType: obstacleType, out rePhysicalSpaces);
                break;
            case TrackingSpaceChoice.FilePath:
                TrackingSpaceGenerator.LoadTrackingSpacePointsFromFile(trackingSpaceFilePath, out rePhysicalSpaces, out reVirtualSpace);
                break;
            case TrackingSpaceChoice.Square:
                TrackingSpaceGenerator.GenerateRectangleTrackingSpace(obstacleType, out rePhysicalSpaces, squareWidth, squareWidth);
                break;
            default:
                TrackingSpaceGenerator.GenerateRectangleTrackingSpace(0, out rePhysicalSpaces, 10, 10);
                break;
        }
        physicalSpaces = rePhysicalSpaces;
        virtualSpace = reVirtualSpace;
    }
    List<Dictionary<string, string>> GetExperimentDescriptor(ExperimentSetup setup)
    {
        var descriptorList = new List<Dictionary<string, string>>();
        for (int i = 0; i < setup.avatars.Count; i++)
        {
            var descriptor = new Dictionary<string, string>();
            descriptorList.Add(descriptor);
            descriptor["trackingSpace"] = setup.trackingSpaceChoice.ToString();
            if (setup.trackingSpaceChoice == TrackingSpaceChoice.FilePath)
            {
                descriptor["trackingSpaceFilePath"] = setup.trackingSpaceFilePath;
            }
            else
            {
                if (setup.trackingSpaceChoice.Equals(TrackingSpaceChoice.Square))
                {
                    descriptor["squareWidth"] = setup.squareWidth.ToString();
                }
                descriptor["obstacleType"] = setup.obstacleType.ToString();
            }
            descriptor["pathSeedChoice"] = setup.avatars[i].pathSeedChoice.ToString();
            if (setup.avatars[i].pathSeedChoice == PathSeedChoice.FilePath)
            {
                descriptor["waypointsFilePath"] = setup.avatars[i].waypointsFilePath;
            }
            else
            {
                descriptor["randomSeed"] = setup.avatars[i].randomSeed.ToString();
                descriptor["pathLength"] = setup.pathLength.ToString();
            }
            descriptor["redirector"] = RedirectionManager.RedirectorToRedirectorChoice(setup.avatars[i].redirector).ToString();
            descriptor["resetter"] = RedirectionManager.ResetterToResetChoice(setup.avatars[i].resetter).ToString();
        }
        return descriptorList;
    }

    string TrialIdToString(int trialId)
    {
        return "trialId_" + trialId;
    }

    public void RunInBackstage()
    {
        while ((experimentInProgress || experimentIterator < experimentSetups.Count))
        {
            MakeOneStepCycle();
        }
    }

    //get time passed from the previous frame
    public float GetDeltaTime()
    {
        if (useSimulationTime)
            return 1.0f / targetFPS;
        else
            return Time.deltaTime;
    }

    public float GetTime()
    {
        if (useSimulationTime)
            return simulatedTime;
        else
            return Time.time;
    }

    //get transforms of all avatars
    public List<Transform> GetAvatarTransforms()
    {
        var re = new List<Transform>();
        foreach (var avatar in redirectedAvatars)
        {
            re.Add(avatar.transform);
        }
        return re;
    }

    public void GetTrackingSpaceBoundingbox(out float minX, out float maxX, out float minY, out float maxY, int physicalSpaceIndex)
    {
        minX = float.MaxValue;
        maxX = float.MinValue;
        minY = float.MaxValue;
        maxY = float.MinValue;
        foreach (var p in physicalSpaces[physicalSpaceIndex].trackingSpace)
        {
            minX = Mathf.Min(minX, p.x);
            maxX = Mathf.Max(maxX, p.x);
            minY = Mathf.Min(minY, p.y);
            maxY = Mathf.Max(maxY, p.y);
        }
    }
    public Vector2 GetTrackingSpaceBoundingboxSize(int physicalSpaceIndex)
    {
        GetTrackingSpaceBoundingbox(out float minX, out float maxX, out float minY, out float maxY, physicalSpaceIndex);
        return new Vector2(maxX - minX, maxY - minY);
    }

    //get specified avatarPrefab
    public GameObject GetAvatarPrefab()
    {
        return avatarPrefabs[avatarPrefabId];
    }

    //create an avatar, return the root object, trans: the parent transform of avatarRoot
    public GameObject CreateAvatar(Transform trans, int avatarId, bool isOtherAvatarRepresentation)
    {
        if (isOtherAvatarRepresentation)
        {
            var otherAvatarRoot = new GameObject("avatarRoot");
            otherAvatarRoot.transform.SetParent(trans);
            otherAvatarRoot.transform.localPosition = Vector3.zero;
            otherAvatarRoot.transform.localRotation = Quaternion.identity;
            otherAvatarRoot.transform.localScale = Vector3.one;

            //user selected avatar prefab
            var otherAvatar = Instantiate(GetUserSelectedAvatarPrefab(), otherAvatarRoot.transform);
            //init transform
            otherAvatar.transform.localScale = Vector3.one;
            otherAvatar.transform.localPosition = Vector3.zero;
            otherAvatar.transform.localRotation = Quaternion.identity;

            if (otherAvatar.GetComponent<Animator>() == null)
                otherAvatar.AddComponent<Animator>();

            var otherAnimator = otherAvatar.GetComponent<Animator>();

            otherAnimator.applyRootMotion = false;

            otherAnimator.runtimeAnimatorController = Instantiate(animatorController);

            otherAnimator.speed = 0;

            var otherAvatarAnimatorController = otherAvatarRoot.gameObject.AddComponent<AvatarAnimatorController>();
            otherAvatarAnimatorController.avatarId = avatarId;

            return otherAvatarRoot;// other avatar, no need for collision
        }


        var avatarRoot = new GameObject("avatarRoot");
        avatarRoot.transform.SetParent(trans);
        avatarRoot.transform.localPosition = Vector3.zero;
        avatarRoot.transform.localRotation = Quaternion.identity;
        avatarRoot.transform.localScale = Vector3.one;

        //user selected avatar prefab
        var avatar = Instantiate(GetUserSelectedAvatarPrefab(), avatarRoot.transform);
        //init transform
        avatar.transform.localScale = Vector3.one;
        avatar.transform.localPosition = Vector3.zero;
        avatar.transform.localRotation = Quaternion.identity;

        if (avatar.GetComponent<Animator>() == null)
            avatar.AddComponent<Animator>();

        var animator = avatar.GetComponent<Animator>();

        animator.applyRootMotion = false;

        animator.runtimeAnimatorController = Instantiate(animatorController);

        animator.speed = 0;

        var avatarAnimatorController = avatarRoot.gameObject.AddComponent<AvatarAnimatorController>();
        avatarAnimatorController.avatarId = avatarId;

        if (useVECollision)//jon: only add avatar collider when useVECollision is toggled
        {
            var avatarCollider = new GameObject("AvatarCollider" + avatarId);
            avatarCollider.layer = AVATAR_LAYER;
            avatarCollider.transform.parent = transform;
            var rigidbody = avatarCollider.gameObject.AddComponent<Rigidbody>();
            rigidbody.useGravity = false;
            rigidbody.constraints = RigidbodyConstraints.FreezeRotation;

            var collider = avatarCollider.gameObject.AddComponent<CapsuleCollider>();
            collider.height = 1.8f;
            collider.radius = 0.3f;
            collider.center = new Vector3(0f, 0.9f, 0f);

            var veCollisionManager = avatarCollider.AddComponent<VECollisionController>();
            veCollisionManager.followedTarget = avatar;
            veCollisionManager.redirectionManager = redirectedAvatars[avatarId].GetComponent<RedirectionManager>();
        }


        return avatarRoot;
    }
}
