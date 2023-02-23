using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using PathSeedChoice = GlobalConfiguration.PathSeedChoice;
using AvatarInfo = ExperimentSetup.AvatarInfo;

public class MovementManager : MonoBehaviour
{
    [HideInInspector]
    public int randomSeed; // random seed choice for path generation
    [HideInInspector]
    public int physicalSpaceIndex; // indicate which physical space the avatar's at
    [HideInInspector]
    public int avatarId;//start from 0

    [HideInInspector]
    public GlobalConfiguration generalManager;

    [HideInInspector]
    public RedirectionManager redirectionManager;
    [HideInInspector]
    public VisualizationManager visualizationManager;

    [Tooltip("Custom waypoints file path")]
    [SerializeField]
    private string waypointsFilePath;

    [Tooltip("Sampling time intervals between waypoints, used when Path Seed Choice == Real User Path")]
    [SerializeField]
    private string samplingIntervalsFilePath;

    [HideInInspector]
    public List<Vector2> waypoints;//waypoints for simulation/collection

    [HideInInspector]
    public List<float> samplingIntervals;//sampling rate read from record files

    [Tooltip("The path seed used for generating waypoints")]
    [SerializeField]
    public PathSeedChoice pathSeedChoice;

    [Tooltip("Path bound to a given VE")]
    public VEPath vePath;

    [HideInInspector]
    public InitialPose physicalInitPose;
    [HideInInspector]
    public InitialPose virtualInitPose;

    [HideInInspector]
    public int waypointIterator = 0;

    [HideInInspector]
    public float accumulatedWaypointTime;//only take effect when pathChoice == realUserPath

    [HideInInspector]
    public SimulatedWalker simulatedWalker;// represent the user's position
    [HideInInspector]
    public bool ifInvalid;//if this avatar becomes invalid (stay at the same position for a long time, exceeds the given time)
    [HideInInspector]
    public bool ifMissionComplete;//If finish this given path
    [HideInInspector]
    public Transform[] vePathWaypoints; //jon: waypoints in VEPath, using transform instead of Vector2
    private GameObject crystal;

    private void Awake()
    {
        generalManager = GetComponentInParent<GlobalConfiguration>();
        redirectionManager = GetComponent<RedirectionManager>();
        visualizationManager = GetComponent<VisualizationManager>();
        simulatedWalker = transform.Find("Simulated Avatar").Find("Head").GetComponent<SimulatedWalker>();
        crystal = Resources.Load("Crystalsv01") as GameObject;
    }

    //one step movement
    public void MakeOneStepMovement()
    {
        //skip invalid data
        if (ifInvalid)
            return;

        UpdateSimulatedWaypointIfRequired();
        simulatedWalker.UpdateSimulatedWalker();
        if (IfInvalidData())
        {
            Debug.LogError(string.Format("InvalidData! experimentIterator = {0}, userid = {1}", generalManager.experimentIterator, avatarId));
            ifInvalid = true;
        }
    }

    //get new waypoints
    public void InitializeWaypointsPattern(int randomSeed)
    {
        if (pathSeedChoice == PathSeedChoice.VEPath)//jon: new option, using waypoints defined in VEPath
        {
            vePathWaypoints = vePath.pathWaypoints;
        }
        else
        {
            generalManager.GenerateWaypoints(randomSeed, pathSeedChoice, virtualInitPose, waypointsFilePath, samplingIntervalsFilePath, out waypoints, out samplingIntervals);
        }
    }

    //check if need to update waypoint
    void UpdateSimulatedWaypointIfRequired()
    {
        //experiment is not in progress
        if (!generalManager.experimentInProgress)
            return;

        if (pathSeedChoice == PathSeedChoice.RealUserPath)
        {
            var redirectionTime = redirectionManager.redirectionTime;
            var samplingInterval = GetSamplingIntervalByWaypointIterator(waypointIterator);
            while (!ifMissionComplete && waypointIterator < waypoints.Count && redirectionTime > accumulatedWaypointTime + samplingInterval)
            {
                accumulatedWaypointTime += samplingInterval;
                UpdateWaypoint();
                samplingInterval = GetSamplingIntervalByWaypointIterator(waypointIterator);
            }
        }
        else//jon: Okay with newly added VEPath
        {
            if ((redirectionManager.currPos - Utilities.FlattenedPos3D(redirectionManager.targetWaypoint.position)).magnitude < generalManager.distanceToWaypointThreshold)
            {
                UpdateWaypoint();
            }
        }

    }
    //0 index represents the start point, so the corresponding sampling Interval equals to 0
    public float GetSamplingIntervalByWaypointIterator(int waypointIterator)
    {
        return waypointIterator == 0 ? 0 : samplingIntervals[waypointIterator];
    }

    //check if this data becomes invalid, break the trial if invalid, (invalid: reset exceeds the upper limit, stuck in a same position for too long)
    public bool IfInvalidData()
    {
        return generalManager.statisticsLogger.IfResetCountExceedLimit(avatarId) || redirectionManager.IfWaitTooLong();
    }


    //get next waypoint
    public void UpdateWaypoint()
    {
        if (pathSeedChoice == PathSeedChoice.VEPath)//jon: new option, using waypoints defined in VEPath
        {
            if (waypointIterator == vePathWaypoints.Length - 1)
            {
                ifMissionComplete = true;
            }
            else
            {
                waypointIterator++;
                redirectionManager.targetWaypoint = vePathWaypoints[waypointIterator];
            }
        }
        else if (waypointIterator == waypoints.Count - 1)
        {
            ifMissionComplete = true;
        }
        else
        {
            waypointIterator++;
            redirectionManager.touchWaypoint = true;
            redirectionManager.targetWaypoint.position = new Vector3(waypoints[waypointIterator].x, redirectionManager.targetWaypoint.position.y, waypoints[waypointIterator].y);
        }
    }

    //align the recorded waypoints to the given point and direction,     
    public List<Vector2> GetRealWaypoints(List<Vector2> preWaypoints, Vector2 initialPosition, Vector2 initialForward, out float sumOfDistances, out float sumOfRotations)
    {
        sumOfDistances = 0;
        sumOfRotations = 0;
        var recordedWaypoints = preWaypoints;
        var deltaPos = initialPosition - VirtualPathGenerator.defaultStartPoint;
        //var deltaPos = Vector2.zero;
        var newWaypoints = new List<Vector2>();
        var pos = initialPosition;
        var forward = initialForward;
        foreach (var p in recordedWaypoints)
        {
            var newPos = p + deltaPos;
            newWaypoints.Add(newPos);

            sumOfDistances += (newPos - pos).magnitude;
            sumOfRotations += Vector2.Angle(forward, newPos - pos);

            forward = (newPos - pos).normalized;
            pos = newPos;
        }

        //align waypoint[1] to the init direction, rotate other waypoints
        if (generalManager.alignToInitialForward)
        {
            var virtualDir = Vector2.up;
            if (generalManager.firstWayPointIsStartPoint)
            {
                virtualDir = newWaypoints[1] - initialPosition;
            }
            else
            {
                virtualDir = newWaypoints[0] - initialPosition;
            }
            var rotAngle = Utilities.GetSignedAngle(Utilities.UnFlatten(virtualDir), Utilities.UnFlatten(initialForward));
            for (int i = 0; i < newWaypoints.Count; i++)
            {
                var vec = Utilities.RotateVector(newWaypoints[i] - initialPosition, rotAngle);
                newWaypoints[i] = initialPosition + vec;
            }
        }
        return newWaypoints;
    }

    //Get Current avatar's info(waypoint,redirector,resetter)
    public AvatarInfo GetCurrentAvatarInfo()
    {
        var rm = redirectionManager;
        string vePathName = vePath == null ? null : vePath.gameObject.name;
        return new AvatarInfo(rm.redirectorType, rm.resetterType, pathSeedChoice, waypoints, waypointsFilePath, samplingIntervals, samplingIntervalsFilePath, vePathName, physicalInitPose, virtualInitPose, physicalSpaceIndex, randomSeed);
    }

    void InstantiateSimulationPrefab()
    {
        if (redirectionManager.targetWaypoint != null)
        {
            Destroy(redirectionManager.targetWaypoint.gameObject);
        }
        Transform waypoint;
        if (generalManager.useCrystalWaypoint)
        {
            waypoint = GameObject.Instantiate(crystal).transform;
            waypoint.localScale = 0.15f * Vector3.one;
        }
        else
        {
            waypoint = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            Destroy(waypoint.GetComponent<SphereCollider>());
            waypoint.GetComponent<Renderer>().material.color = generalManager.avatarColors[avatarId];
            waypoint.GetComponent<Renderer>().material.SetColor("_EmissionColor", generalManager.avatarColors[avatarId] * 0.4f);
            waypoint.localScale = 0.4f * Vector3.one;
        }

        waypoint.gameObject.layer = LayerMask.NameToLayer("Waypoint");
        redirectionManager.targetWaypoint = waypoint;
        waypoint.name = "Simulated Waypoint";
        waypoint.position = 1.2f * Vector3.up + 1000 * Vector3.forward;
        waypoint.parent = transform.parent;


        if (visualizationManager.realWaypoint != null)
        {
            Destroy(visualizationManager.realWaypoint.gameObject);
        }
        Transform newWaypoint = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
        Destroy(newWaypoint.GetComponent<SphereCollider>());
        visualizationManager.realWaypoint = newWaypoint;
        newWaypoint.gameObject.layer = LayerMask.NameToLayer("Physical");
        newWaypoint.name = "Real Waypoint";
        newWaypoint.position = 1.2f * Vector3.up + 1000 * Vector3.forward;
        newWaypoint.localScale = 0.4f * Vector3.one;
        newWaypoint.parent = transform.parent;
        newWaypoint.GetComponent<Renderer>().material.color = generalManager.avatarColors[avatarId];
        newWaypoint.GetComponent<Renderer>().material.SetColor("_EmissionColor", generalManager.avatarColors[avatarId] * 0.4f);
    }

    //need to reload data for each trial
    public void LoadData(int avatarId, AvatarInfo avatar)
    {
        var rm = redirectionManager;
        var vm = visualizationManager;
        this.avatarId = avatarId;
        randomSeed = avatar.randomSeed;
        physicalSpaceIndex = avatar.physicalSpaceIndex;
        rm.redirectorType = avatar.redirector;
        rm.resetterType = avatar.resetter;
        pathSeedChoice = avatar.pathSeedChoice;
        waypoints = avatar.waypoints;
        waypointsFilePath = avatar.waypointsFilePath;
        samplingIntervals = avatar.samplingIntervals;
        samplingIntervalsFilePath = avatar.samplingIntervalsFilePath;
        if (generalManager.movementController.Equals(GlobalConfiguration.MovementController.HMD))
        {
            //HMD mode, set as current position and direction
            virtualInitPose = new InitialPose(Utilities.FlattenedPos2D(rm.headTransform.position), Utilities.FlattenedDir2D(rm.headTransform.forward));
            physicalInitPose = virtualInitPose;
        }
        else
        {
            //auto simulation and keyboard controll mode, apply initial positions and directions
            virtualInitPose = avatar.virtualInitPose;
            physicalInitPose = avatar.physicalInitPose;
        }

        ifInvalid = false;
        ifMissionComplete = false;

        //Set virtual path

        if (pathSeedChoice == PathSeedChoice.VEPath)//jon: newly added VE Path
        {
            vePath = GameObject.Find(avatar.vePathName).GetComponent<VEPath>();
            if (vePath)
            {
                vePathWaypoints = vePath.pathWaypoints;
            }
        }
        else
        {
            waypoints = GetRealWaypoints(waypoints, virtualInitPose.initialPosition, virtualInitPose.initialForward, out float sumOfDistances, out float sumOfRotations);
        }
        //Set priority, large priority call early
        rm.priority = this.avatarId;

        InstantiateSimulationPrefab();
        // Set First Waypoint Position and Enable It
        if (pathSeedChoice == PathSeedChoice.VEPath)//jon: when using VEPath
        {
            rm.targetWaypoint = vePathWaypoints[0];
        }
        else
        {
            rm.targetWaypoint.position = new Vector3(waypoints[0].x, rm.targetWaypoint.position.y, waypoints[0].y);
        }
        waypointIterator = 0;
        accumulatedWaypointTime = 0;

        // Enabling/Disabling Redirectors
        rm.redirectorChoice = RedirectionManager.RedirectorToRedirectorChoice(rm.redirectorType);
        rm.resetterChoice = RedirectionManager.ResetterToResetChoice(rm.resetterType);
        rm.UpdateRedirector(rm.redirectorType);
        rm.UpdateResetter(rm.resetterType);
        rm.redirector.globalConfiguration = generalManager;
        rm.redirector.movementManager = this;
        rm.resetter.globalConfiguration = generalManager;
        rm.resetter.movementManager = this;
        rm.polygons = new List<List<Vector2>>();
        rm.polygons.Add(generalManager.physicalSpaces[physicalSpaceIndex].trackingSpace);
        foreach (var obs in generalManager.physicalSpaces[physicalSpaceIndex].obstaclePolygons)
            rm.polygons.Add(obs);
        if (rm.redirectorChoice == RedirectionManager.RedirectorChoice.SeparateSpace)
        {
            ((SeparateSpace_Redirector)rm.redirector).InitializePositionSquares();
        }

        // Stop Trail Drawing and Delete Virtual Path
        rm.trailDrawer.enabled = false;

        // Setup Trail Drawing
        rm.trailDrawer.enabled = true;
        // Enable Waypoint
        rm.targetWaypoint.gameObject.SetActive(true);
        vm.realWaypoint.gameObject.SetActive(true);

        // Resetting User and World Positions and Orientations
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
        // ESSENTIAL BUG FOUND: If you set the user first and then the redirection recipient, then the user will be moved, so you have to make sure to do it afterwards!
        //Debug.Log("Target User Position: " + setup.initialPose.initialPosition.ToString("f4"));

        rm.headTransform.position = Utilities.UnFlatten(virtualInitPose.initialPosition, rm.headTransform.position.y);
        rm.headTransform.rotation = Quaternion.LookRotation(Utilities.UnFlatten(virtualInitPose.initialForward), Vector3.up);

        rm.Initialize();//initialize when restart a experiment 
        vm.DestroyAll();
        vm.GenerateTrackingSpaceMesh(generalManager.physicalSpaces);
        rm.trackingSpace.position = Utilities.UnFlatten(virtualInitPose.initialPosition - physicalInitPose.initialPosition);
        rm.trackingSpace.eulerAngles = new Vector3(0, Mathf.Atan2(virtualInitPose.initialForward.y, virtualInitPose.initialForward.x) - Mathf.Atan2(physicalInitPose.initialForward.y, physicalInitPose.initialForward.x), 0);

        rm.trailDrawer.BeginTrailDrawing();
    }
}

