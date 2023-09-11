using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class RedirectionManager : MonoBehaviour
{
    const float INF = 100000;
    const float EPS = 1e-5f;
    public static readonly float MaxSamePosTime = 50;//the max time(in seconds) the avatar can stand on the same position, exceeds this value will make data invalid (stuck in one place)

    public enum RedirectorChoice { None, S2C, S2O, Zigzag, ThomasAPF, MessingerAPF, DynamicAPF, DeepLearning, PassiveHapticAPF, SeparateSpace };
    public enum ResetterChoice { None, TwoOneTurn, FreezeTurn, MR2C, R2G, SFR2G, SeparateSpace };

    [HideInInspector]
    public float gt; // translation gain
    [HideInInspector]
    public float curvature; // (1/curvature radius), positive for counter-clockwise(left), negative for clockwise(right)
    [HideInInspector]
    public float gr; // rotation gain
    [HideInInspector]
    public bool isRotating; // if the avatar is rotating
    [HideInInspector]
    public bool isWalking; // if the avatar is walking
    private const float MOVEMENT_THRESHOLD = 0.2f; // meters per second 
    private const float ROTATION_THRESHOLD = 15f; // degrees per second


    [Tooltip("The game object that is being physically tracked (probably user's head)")]
    public Transform headTransform;

    [Tooltip("Subtle Redirection Controller")]
    public RedirectorChoice redirectorChoice;

    [Tooltip("Overt Redirection Controller")]
    public ResetterChoice resetterChoice;

    // Experiment Variables
    [HideInInspector]
    public System.Type redirectorType = null;
    [HideInInspector]
    public System.Type resetterType = null;


    //record the time standing on the same position
    private float samePosTime;

    [HideInInspector]
    public GlobalConfiguration globalConfiguration;
    [HideInInspector]
    public VisualizationManager visualizationManager;

    //[HideInInspector]
    public Transform body;
    //[HideInInspector]
    public Transform trackingSpace;
    //[HideInInspector]
    public Transform simulatedHead;

    [HideInInspector]
    public Redirector redirector;
    [HideInInspector]
    public Resetter resetter;
    [HideInInspector]
    public TrailDrawer trailDrawer;
    //[HideInInspector]
    public MovementManager movementManager;
    //[HideInInspector]
    public SimulatedWalker simulatedWalker;
    [HideInInspector]
    public KeyboardController keyboardController;
    [HideInInspector]
    public HeadFollower bodyHeadFollower;

    [HideInInspector]
    public float priority;

    [HideInInspector]
    public Vector3 currPos, currPosReal, prevPos, prevPosReal;
    [HideInInspector]
    public Vector3 currDir, currDirReal, prevDir, prevDirReal;
    [HideInInspector]
    public Vector3 deltaPos;//the vector of the previous position to the current position
    [HideInInspector]
    public float deltaDir;//horizontal angle change in degrees (positive if rotate clockwise)
    [HideInInspector]
    public Transform targetWaypoint;

    [HideInInspector]
    public bool inReset = false;

    [HideInInspector]
    public int EndResetCountDown = 0;
    [HideInInspector]
    public bool resetSign;

    [HideInInspector]
    public float redirectionTime;//total time passed when using subtle redirection

    [HideInInspector]
    public float walkDist = 0;//walked virtual distance

    [HideInInspector]
    public bool touchWaypoint;

    [HideInInspector]
    public List<List<Vector2>> polygons;

    private NetworkManager networkManager;

    void Awake()
    {
        redirectorType = RedirectorChoiceToRedirector(redirectorChoice);
        resetterType = ResetterChoiceToResetter(resetterChoice);

        globalConfiguration = GetComponentInParent<GlobalConfiguration>();
        visualizationManager = GetComponent<VisualizationManager>();
        networkManager = globalConfiguration.GetComponentInChildren<NetworkManager>(true);

        body = transform.Find("Body");
        trackingSpace = transform.Find("TrackingSpace0");
        simulatedHead = GetSimulatedAvatarHead();

        movementManager = this.gameObject.GetComponent<MovementManager>();

        GetRedirector();
        GetResetter();

        trailDrawer = GetComponent<TrailDrawer>();
        simulatedWalker = simulatedHead.GetComponent<SimulatedWalker>();
        keyboardController = simulatedHead.GetComponent<KeyboardController>();

        bodyHeadFollower = body.GetComponent<HeadFollower>();

        SetReferenceForResetter();

        if (globalConfiguration.movementController != GlobalConfiguration.MovementController.HMD)
        {
            headTransform = simulatedHead;
        }
        else
        {
            // hide avatar body
            body.gameObject.SetActive(false);
        }

        // Resetter needs ResetTrigger to be initialized before initializing itself
        if (resetter != null)
            resetter.Initialize();

        samePosTime = 0;
        gt = curvature = gr = 1;
        isRotating = false;
        isWalking = false;
        touchWaypoint = false;
    }

    //modify these trhee functions when adding a new redirector
    public System.Type RedirectorChoiceToRedirector(RedirectorChoice redirectorChoice)
    {
        switch (redirectorChoice)
        {
            case RedirectorChoice.None:
                return typeof(NullRedirector);
            case RedirectorChoice.S2C:
                return typeof(S2CRedirector);
            case RedirectorChoice.S2O:
                return typeof(S2ORedirector);
            case RedirectorChoice.Zigzag:
                return typeof(ZigZagRedirector);
            case RedirectorChoice.ThomasAPF:
                return typeof(ThomasAPF_Redirector);
            case RedirectorChoice.MessingerAPF:
                return typeof(MessingerAPF_Redirector);
            case RedirectorChoice.DynamicAPF:
                return typeof(DynamicAPF_Redirector);
            case RedirectorChoice.DeepLearning:
                return typeof(DeepLearning_Redirector);
            case RedirectorChoice.PassiveHapticAPF:
                return typeof(PassiveHapticAPF_Redirector);
            case RedirectorChoice.SeparateSpace:
                return typeof(SeparateSpace_Redirector);
        }
        return typeof(NullRedirector);
    }
    public static RedirectorChoice RedirectorToRedirectorChoice(System.Type redirector)
    {
        if (redirector.Equals(typeof(NullRedirector)))
            return RedirectorChoice.None;
        else if (redirector.Equals(typeof(S2CRedirector)))
            return RedirectorChoice.S2C;
        else if (redirector.Equals(typeof(S2ORedirector)))
            return RedirectorChoice.S2O;
        else if (redirector.Equals(typeof(ZigZagRedirector)))
            return RedirectorChoice.Zigzag;
        else if (redirector.Equals(typeof(ThomasAPF_Redirector)))
            return RedirectorChoice.ThomasAPF;
        else if (redirector.Equals(typeof(MessingerAPF_Redirector)))
            return RedirectorChoice.MessingerAPF;
        else if (redirector.Equals(typeof(DynamicAPF_Redirector)))
            return RedirectorChoice.DynamicAPF;
        else if (redirector.Equals(typeof(DeepLearning_Redirector)))
            return RedirectorChoice.DeepLearning;
        else if (redirector.Equals(typeof(PassiveHapticAPF_Redirector)))
            return RedirectorChoice.PassiveHapticAPF;
        else if (redirector.Equals(typeof(SeparateSpace_Redirector)))
            return RedirectorChoice.SeparateSpace;
        return RedirectorChoice.None;
    }
    public static System.Type DecodeRedirector(string s)
    {
        switch (s.ToLower())
        {
            case "null":
                return typeof(NullRedirector);
            case "s2c":
                return typeof(S2CRedirector);
            case "s2o":
                return typeof(S2ORedirector);
            case "zigzag":
                return typeof(ZigZagRedirector);
            case "thomasapf":
                return typeof(ThomasAPF_Redirector);
            case "messingerapf":
                return typeof(MessingerAPF_Redirector);
            case "dynamicapf":
                return typeof(DynamicAPF_Redirector);
            case "deeplearning":
                return typeof(DeepLearning_Redirector);
            case "passivehapticapf":
                return typeof(PassiveHapticAPF_Redirector);
            case "separatespace":
                return typeof(SeparateSpace_Redirector);
            default:
                return typeof(NullRedirector);
        }
    }
    //modify these functions when adding a new resetter
    public static System.Type ResetterChoiceToResetter(ResetterChoice resetterChoice)
    {
        switch (resetterChoice)
        {
            case ResetterChoice.None:
                return typeof(NullResetter);
            case ResetterChoice.FreezeTurn:
                return typeof(FreezeTurnResetter);
            case ResetterChoice.TwoOneTurn:
                return typeof(TwoOneTurnResetter);
            case ResetterChoice.MR2C:
                return typeof(MR2C_Resetter);
            case ResetterChoice.R2G:
                return typeof(R2G_Resetter);
            case ResetterChoice.SFR2G:
                return typeof(SFR2G_Resetter);
            case ResetterChoice.SeparateSpace:
                return typeof(SeparateSpace_Resetter);
        }
        return typeof(NullResetter);
    }
    public static ResetterChoice ResetterToResetChoice(System.Type reset)
    {
        if (reset.Equals(typeof(NullResetter)))
            return ResetterChoice.None;
        else if (reset.Equals(typeof(FreezeTurnResetter)))
            return ResetterChoice.FreezeTurn;
        else if (reset.Equals(typeof(TwoOneTurnResetter)))
            return ResetterChoice.TwoOneTurn;
        else if (reset.Equals(typeof(MR2C_Resetter)))
            return ResetterChoice.MR2C;
        else if (reset.Equals(typeof(R2G_Resetter)))
            return ResetterChoice.R2G;
        else if (reset.Equals(typeof(SFR2G_Resetter)))
            return ResetterChoice.SFR2G;
        else if (reset.Equals(typeof(SeparateSpace_Resetter)))
            return ResetterChoice.SeparateSpace;
        return ResetterChoice.None;
    }
    public static System.Type DecodeResetter(string s)
    {
        switch (s.ToLower())
        {
            case "null":
                return typeof(NullResetter);
            case "freezeturn":
                return typeof(FreezeTurnResetter);
            case "twooneturn":
                return typeof(TwoOneTurnResetter);
            case "mr2c":
                return typeof(MR2C_Resetter);
            case "r2g":
                return typeof(R2G_Resetter);
            case "sfr2g":
                return typeof(SFR2G_Resetter);
            case "separatespace":
                return typeof(SeparateSpace_Resetter);
            default:
                return typeof(NullResetter);
        }
    }

    public Transform GetSimulatedAvatarHead()
    {
        return transform.Find("Simulated Avatar").Find("Head");
    }
    public void FixHeadTransform()
    {
        if (globalConfiguration.movementController == GlobalConfiguration.MovementController.HMD && globalConfiguration.networkingMode && movementManager.avatarId != networkManager.avatarId)
        {
            headTransform = simulatedHead;
        }
    }

    public bool IsDirSafe(Vector2 pos, Vector2 dir)
    {// if this direction is away from physical obstacles
        var spa = globalConfiguration.physicalSpaces[movementManager.physicalSpaceIndex].trackingSpace;
        var obs = globalConfiguration.physicalSpaces[movementManager.physicalSpaceIndex].obstaclePolygons;
        for (int i = 0; i < spa.Count; i++)
        {
            if (Utilities.PointLineDistance(pos, spa[i], spa[(i + 1) % spa.Count]) < globalConfiguration.RESET_TRIGGER_BUFFER + 0.05f)
            {
                var nearestPoint = Utilities.PointLineProjection(pos, spa[i], spa[(i + 1) % spa.Count]);
                if (Vector2.Dot(pos - nearestPoint, dir) <= 0 || Vector2.Angle(pos - nearestPoint, dir) >= 80)
                {
                    return false;
                }
            }
        }
        foreach (var obstacle in obs)
        {
            for (int i = 0; i < obstacle.Count; i++)
            {
                if (Utilities.PointLineDistance(pos, obstacle[i], obstacle[(i + 1) % obstacle.Count]) < globalConfiguration.RESET_TRIGGER_BUFFER + 0.05f)
                {
                    var nearestPoint = Utilities.PointLineProjection(pos, obstacle[i], obstacle[(i + 1) % obstacle.Count]);
                    if (Vector2.Dot(pos - nearestPoint, dir) <= 0 || Vector2.Angle(pos - nearestPoint, dir) >= 80)
                    {
                        return false;
                    }
                }
            }
        }
        return true;
    }

    public bool IfWaitTooLong()
    {
        return samePosTime > MaxSamePosTime;
    }

    public void Initialize()
    {
        samePosTime = 0;
        redirectionTime = 0;
        UpdatePreviousUserState();
        UpdateCurrentUserState();
        inReset = false;
        EndResetCountDown = 3;
    }
    public void UpdateRedirectionTime()
    {
        if (!inReset)
            redirectionTime += globalConfiguration.GetDeltaTime();
    }

    //make one step redirection: redirect or reset
    public void MakeOneStepRedirection()
    {
        FixHeadTransform();
        UpdateCurrentUserState();
        visualizationManager.realWaypoint.position = Utilities.GetRelativePosition(targetWaypoint.position, trackingSpace.transform);

        //invalidData
        if (movementManager.ifInvalid)
            return;
        //do not redirect other avatar's transform during networking mode
        if (globalConfiguration.networkingMode && movementManager.avatarId != networkManager.avatarId)
            return;

        if (currPos.Equals(prevPos))
        {
            //used in auto simulation mode and there are unfinished waypoints
            if (globalConfiguration.movementController == GlobalConfiguration.MovementController.AutoPilot && !movementManager.ifMissionComplete)
            {
                //accumulated time for standing on the same position
                samePosTime += 1.0f / globalConfiguration.targetFPS;
            }
        }
        else
        {
            samePosTime = 0;//clear accumulated time
        }

        CalculateStateChanges();

        if (globalConfiguration.synchronizedReset)
        {
            if (resetSign)
            {
                resetSign = false;
                OnResetTrigger();
            }
            if (inReset)
            {
                if (EndResetCountDown > 0)
                { // reset already finished 
                    bool othersInReset = false;
                    foreach (var us in globalConfiguration.redirectedAvatars)
                    {
                        if (us.GetComponent<RedirectionManager>().inReset && us.GetComponent<RedirectionManager>().EndResetCountDown == 0)
                        {
                            othersInReset = true;
                            break;
                        }
                    }
                    if (!othersInReset || redirectorChoice != RedirectorChoice.SeparateSpace)
                    { // end reset
                        inReset = false;
                        if (redirector != null)
                        {
                            redirector.ClearGains();
                            redirector.InjectRedirection();
                        }
                        EndResetCountDown = EndResetCountDown > 0 ? EndResetCountDown - 1 : 0;
                    }
                }
                else
                { // in reset
                    if (resetter != null)
                    {
                        resetter.InjectResetting();
                    }
                }
            }
            else
            {
                if (redirector != null)
                {
                    redirector.ClearGains();
                    redirector.InjectRedirection();
                }
                EndResetCountDown = EndResetCountDown > 0 ? EndResetCountDown - 1 : 0;
            }
        }
        else
        {
            if (resetter != null && !inReset && resetter.IsResetRequired() && EndResetCountDown == 0)
            {
                OnResetTrigger();
            }
            if (inReset)
            {
                if (resetter != null)
                {
                    resetter.InjectResetting();
                }
            }
            else
            {
                if (redirector != null)
                {
                    redirector.ClearGains();
                    redirector.InjectRedirection();
                }
                EndResetCountDown = EndResetCountDown > 0 ? EndResetCountDown - 1 : 0;
            }
        }

        UpdatePreviousUserState();
        UpdateBodyPose();
    }

    void UpdateBodyPose()
    {
        body.position = Utilities.FlattenedPos3D(headTransform.position);
        body.rotation = Quaternion.LookRotation(Utilities.FlattenedDir3D(headTransform.forward), Vector3.up);
    }

    void SetReferenceForRedirector()
    {
        if (redirector != null)
            redirector.redirectionManager = this;
    }

    void SetReferenceForResetter()
    {
        if (resetter != null)
            resetter.redirectionManager = this;

    }

    void SetReferenceForSimulationManager()
    {
        if (movementManager != null)
        {
            movementManager.redirectionManager = this;
        }
    }

    void GetRedirector()
    {
        redirector = this.gameObject.GetComponent<Redirector>();
        if (redirector == null)
            this.gameObject.AddComponent<NullRedirector>();
        redirector = this.gameObject.GetComponent<Redirector>();
    }

    void GetResetter()
    {
        resetter = this.gameObject.GetComponent<Resetter>();
        if (resetter == null)
            this.gameObject.AddComponent<NullResetter>();
        resetter = this.gameObject.GetComponent<Resetter>();
    }


    void GetTrailDrawer()
    {
        trailDrawer = this.gameObject.GetComponent<TrailDrawer>();
    }

    void GetSimulationManager()
    {
        movementManager = this.gameObject.GetComponent<MovementManager>();
    }

    void GetSimulatedWalker()
    {
        simulatedWalker = simulatedHead.GetComponent<SimulatedWalker>();
    }

    void GetKeyboardController()
    {
        keyboardController = simulatedHead.GetComponent<KeyboardController>();
    }

    void GetBodyHeadFollower()
    {
        bodyHeadFollower = body.GetComponent<HeadFollower>();
    }

    void GetBody()
    {
        body = transform.Find("Body");
    }

    void GetTrackedSpace()
    {
        trackingSpace = transform.Find("TrackingSpace0");
    }

    void GetSimulatedHead()
    {
        simulatedHead = transform.Find("Simulated User").Find("Head");
    }

    void GetTargetWaypoint()
    {
        targetWaypoint = transform.Find("Target Waypoint").gameObject.transform;
    }

    public void UpdateCurrentUserState()
    {
        currPos = Utilities.FlattenedPos3D(headTransform.position);//only consider head position
        currPosReal = GetPosReal(currPos);
        currDir = Utilities.FlattenedDir3D(headTransform.forward);
        currDirReal = GetDirReal(currDir);
        walkDist += (Utilities.FlattenedPos2D(currPos) - Utilities.FlattenedPos2D(prevPos)).magnitude;
    }

    void UpdatePreviousUserState()
    {
        prevPos = Utilities.FlattenedPos3D(headTransform.position);
        prevPosReal = GetPosReal(prevPos);
        prevDir = Utilities.FlattenedDir3D(headTransform.forward);
        prevDirReal = GetDirReal(prevDir);
    }
    public Vector3 GetPosReal(Vector3 pos)
    {
        return Utilities.GetRelativePosition(pos, trackingSpace.transform);
    }
    public Vector3 GetDirReal(Vector3 dir)
    {
        return Utilities.FlattenedDir3D(Utilities.GetRelativeDirection(dir, transform));
    }

    void CalculateStateChanges()
    {
        deltaPos = currPos - prevPos;
        deltaDir = Utilities.GetSignedAngle(prevDir, currDir);
        if (deltaPos.magnitude / GetDeltaTime() > MOVEMENT_THRESHOLD) //User is moving
        {
            isWalking = true;
        }
        else
        {
            isWalking = false;
        }
        if (Mathf.Abs(deltaDir) / GetDeltaTime() >= ROTATION_THRESHOLD)  //if User is rotating
        {
            isRotating = true;
        }
        else
        {
            isRotating = false;
        }
    }

    public void OnResetTrigger()
    {
        resetter.InitializeReset();
        inReset = true;

        //record one reset operation
        globalConfiguration.statisticsLogger.Event_Reset_Triggered(movementManager.avatarId);
    }

    public void OnResetEnd()
    {
        resetter.EndReset();
        EndResetCountDown = 2;
        if (!globalConfiguration.synchronizedReset)
        {
            inReset = false;
        }
    }

    public void RemoveRedirector()
    {
        redirector = gameObject.GetComponent<Redirector>();
        if (redirector != null)
            Destroy(redirector);
        redirector = null;
    }

    public void UpdateRedirector(System.Type redirectorType)
    {
        RemoveRedirector();
        redirector = (Redirector)gameObject.AddComponent(redirectorType);
        SetReferenceForRedirector();
    }

    public void RemoveResetter()
    {
        resetter = gameObject.GetComponent<Resetter>();
        if (resetter != null)
            Destroy(resetter);
        resetter = null;
    }

    public void UpdateResetter(System.Type resetterType)
    {
        RemoveResetter();
        if (resetterType != null)
        {
            resetter = (Resetter)gameObject.AddComponent(resetterType);
            SetReferenceForResetter();
            if (resetter != null)
                resetter.Initialize();
        }
    }
    public float GetDeltaTime()
    {
        return globalConfiguration.GetDeltaTime();
    }
}
