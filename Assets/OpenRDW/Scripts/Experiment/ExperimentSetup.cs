using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PathSeedChoice = GlobalConfiguration.PathSeedChoice;
using TrackingSpaceChoice = GlobalConfiguration.TrackingSpaceChoice;

public class ExperimentSetup
{
    // store info of every avatar
    public class AvatarInfo
    {
        public int randomSeed; // random seed choice for path generation
        public int physicalSpaceIndex; // newly added, to indicate which physical space the avatar's at
        public System.Type redirector;
        public System.Type resetter;
        public PathSeedChoice pathSeedChoice;
        public List<Vector2> waypoints;
        public List<float> samplingIntervals;
        public string vePathName;//jon: newly added, which VE Path to use when pathSeedChoice==PathSeedChoice.VEPath
        public string waypointsFilePath;//waypoints file
        public string samplingIntervalsFilePath;//read sampling intervals from filePath, same line number with waypoints file
        public InitialPose physicalInitPose;
        public InitialPose virtualInitPose;
        public AvatarInfo(System.Type redirector, System.Type resetter, PathSeedChoice pathSeedChoice, List<Vector2> waypoints, string waypointsFilePath,
            List<float> samplingIntervals, string samplingIntervalsFilePath, string vePathName, InitialPose physicalInitPose, InitialPose virtualInitPose, int physicalSpaceIndex, int randomSeed)
        {
            this.redirector = redirector;
            this.resetter = resetter;
            this.pathSeedChoice = pathSeedChoice;
            this.waypoints = waypoints;
            this.waypointsFilePath = waypointsFilePath;
            this.samplingIntervals = samplingIntervals;
            this.samplingIntervalsFilePath = samplingIntervalsFilePath;
            this.vePathName = vePathName;
            this.virtualInitPose = virtualInitPose;
            this.physicalInitPose = physicalInitPose;
            this.physicalSpaceIndex = physicalSpaceIndex;
            this.randomSeed = randomSeed;
        }
        //clone an avatar info
        public AvatarInfo Copy()
        {
            return new AvatarInfo(redirector, resetter, pathSeedChoice, waypoints, waypointsFilePath, samplingIntervals, samplingIntervalsFilePath, vePathName, physicalInitPose,virtualInitPose, physicalSpaceIndex, randomSeed);
        }
    }
    public List<AvatarInfo> avatars;
    public TrackingSpaceChoice trackingSpaceChoice;
    public string trackingSpaceFilePath;
    public float squareWidth;
    public int obstacleType;
    public List<SingleSpace> physicalSpaces;
    public SingleSpace virtualSpace;
    public float pathLength;
    public ExperimentSetup(List<AvatarInfo> avatars, List<SingleSpace> physicalSpaces, SingleSpace virtualSpace, TrackingSpaceChoice trackingSpaceChoice, string trackingSpaceFilePath, float squareWidth,
         int obstacleType, float pathLength)
    {
        this.avatars = avatars;
        this.physicalSpaces = physicalSpaces;
        this.virtualSpace = virtualSpace;
        this.trackingSpaceChoice = trackingSpaceChoice;
        this.trackingSpaceFilePath = trackingSpaceFilePath;
        this.squareWidth = squareWidth;
        this.obstacleType = obstacleType;
        this.pathLength = pathLength;
    }
}