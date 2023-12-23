using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml;
public class StatisticsLogger : MonoBehaviour
{
    [Tooltip("Reset exceeds this value will make data invalid")]
    public int MaxResetCount = 1000; // reset exceeds this value will make data invalid
    private GlobalConfiguration globalConfiguration;
    public class ResultOfTrial
    {
        public int endState;
        public List<Dictionary<string, string>> result;
        public ResultOfTrial(int endState, List<Dictionary<string, string>> result)
        {
            this.endState = endState;
            this.result = result;
        }
        public string EndStateToString()
        {
            var experimentState = "";
            switch (endState)
            {
                case -1:
                    experimentState = "Invalid";
                    break;
                case 0:
                    experimentState = "Normal";
                    break;
                case 1:
                    experimentState = "Manually";
                    break;
                default:
                    experimentState = "Undefined";
                    break;
            }
            return experimentState;
        }
    }
    [HideInInspector]
    public List<ResultOfTrial> experimentResults = new List<ResultOfTrial>();//outside to inside: every trial->every avatar

    [Tooltip("If log sample variables")]
    public bool logSampleVariables = false;
    [Tooltip("How often we will gather data we have and log it in hertz, average 1 / samplingFrequency second records to one sample")]
    public float samplingFrequency = 10;

    [Header("Image")]
    [Tooltip("Side resolution of a square image(resolution * resolution)")]
    public int imageResolution;
    // [Tooltip("Side length represent the real world meters")]
    private float realSideLength;
    [Tooltip("Border thickness drawn in the image")]
    public int borderThickness;
    [Tooltip("Path thickness drawn in the image")]
    public int pathThickness;

    [Header("Screenshot")]
    [Tooltip("Factor by which to increase resolution")]
    [Range(1, 10)]
    public int superSize;

    private Color backgroundColor = Color.white;
    private Color trackingSpaceColor = Color.black;

    private Color obstacleColor;
    // The way this works is that we wait 1 / samplingFrequency time to transpire before we attempt to clean buffers and gather samples
    // And since we always get a buffer value right before collecting samples, we'll have at least 1 buffer value to get an average from
    // The only problem with this is that overall we'll be gathering less than the expected frequency since the "lateness" of sampling will accumulate

    // THE FOLLOWING PARAMETERS MUST BE SENSITIVE TO TIME SCALE

    // TEMPORARILY SETTING ALL TO PUBLIC FOR TESTING
    // avatar statistics
    public class AvatarStatistics
    {
        // Redirection Single Parameters
        public float sumOfInjectedTranslation = 0; // Overall amount of displacement (IN METERS) of redirection reference due to translation gain (always positive)
        public float sumOfInjectedRotationFromRotationGain = 0; // Overall amount of rotation (IN degrees) (around user) of redirection reference due to rotation gain (always positive)
        public float sumOfInjectedRotationFromCurvatureGain = 0; // Overall amount of rotation (IN degrees) (around user) of redirection reference due to curvature gain (always positive)
        public float maxTranslationGain = float.MinValue;
        public float minTranslationGain = float.MaxValue;
        public float maxRotationGain = float.MinValue;
        public float minRotationGain = float.MaxValue;
        public float maxCurvatureGain = float.MinValue;
        public float minCurvatureGain = float.MaxValue;

        // Reset Single Parameters
        public float resetCount = 0;
        public float sumOfVirtualDistanceTravelled = 0; // Based on user movement controller plus redirection movement
        public float sumOfRealDistanceTravelled = 0; // Based on user movement controller
        public float experimentBeginningTime = 0;//calculated according to getDeltaTime
        public float experimentEndingTime = 0;
        public float virtualWayDistance = 0;//virtual way length of waypoints
        public long executeBeginningTime = 0;//the real world time when experiment trial starts
        public long executeEndingTime = 0;//the real world time when experiment trial ends

        //For passive haptics
        public float positionError;//distance between the final physical avatar position and the  physical target position
        public float angleError;//angle between the final physical avatar direction and the  physical target direction

        // Reset Sample Parameters
        public List<float> virtualDistancesTravelledBetweenResets = new List<float>(); // this will be measured also from beginning to first reset, and from last reset to end (?)
        public float virtualDistanceTravelledSinceLastReset;
        public List<float> timeElapsedBetweenResets = new List<float>(); // this will be measured also from beginning to first reset, and from last reset to end (?)
        public float timeOfLastReset = 0;

        // Sampling Paramers: These parameters are first read per frame/value update and stored in their buffer, and then 1/samplingFrequency time goes by, the values in the buffer will be averaged and logged to the list
        // The buffer variables for gains will be multiplied by time and at sampling time divided by time since last sample to get a proper average (since the functions aren't guaranteed to be called every frame)
        // Actually we can do this for all parameters just for true weighted average!
        public List<Vector2> userRealPositionSamples = new List<Vector2>();
        public List<Vector2> userRealPositionSamplesBuffer = new List<Vector2>();
        public List<Vector2> userVirtualPositionSamples = new List<Vector2>();
        public List<Vector2> userVirtualPositionSamplesBuffer = new List<Vector2>();
        public List<float> translationGainSamples = new List<float>();
        public List<float> translationGainSamplesBuffer = new List<float>();
        public List<float> injectedTranslationSamples = new List<float>();
        public List<float> injectedTranslationSamplesBuffer = new List<float>();
        public List<float> rotationGainSamples = new List<float>();
        public List<float> rotationGainSamplesBuffer = new List<float>();
        // NOTE: IN THE FUTURE, WE MIGHT WANT TO LOG THE INJECTED VALUES DIVIDED BY TIME, SO IT'S MORE CONSISTENT AND NO DEPENDENT ON THE FRAMERATE
        public List<float> injectedRotationFromRotationGainSamples = new List<float>();
        public List<float> injectedRotationFromRotationGainSamplesBuffer = new List<float>();
        public List<float> curvatureGainSamples = new List<float>();
        public List<float> curvatureGainSamplesBuffer = new List<float>();
        public List<float> injectedRotationFromCurvatureGainSamples = new List<float>();
        public List<float> injectedRotationFromCurvatureGainSamplesBuffer = new List<float>();
        public List<float> injectedRotationSamples = new List<float>();
        public List<float> injectedRotationSamplesBuffer = new List<float>();
        public List<float> distanceToNearestBoundarySamples = new List<float>();
        public List<float> distanceToNearestBoundarySamplesBuffer = new List<float>();
        public AvatarStatistics(float currentTime)
        {
            Initialize(currentTime);
        }
        public void Initialize(float currentTime)
        {
            sumOfInjectedTranslation = 0;
            sumOfInjectedRotationFromRotationGain = 0;
            sumOfInjectedRotationFromCurvatureGain = 0;
            maxTranslationGain = float.MinValue;
            minTranslationGain = float.MaxValue;
            maxRotationGain = float.MinValue;
            minRotationGain = float.MaxValue;
            maxCurvatureGain = float.MinValue;
            minCurvatureGain = float.MaxValue;

            resetCount = 0;
            sumOfVirtualDistanceTravelled = 0;
            sumOfRealDistanceTravelled = 0;
            experimentBeginningTime = currentTime;
            executeBeginningTime = DateTime.Now.Ticks;

            positionError = 0;
            angleError = 0;

            virtualDistancesTravelledBetweenResets = new List<float>();
            virtualDistanceTravelledSinceLastReset = 0;
            timeElapsedBetweenResets = new List<float>();
            timeOfLastReset = currentTime; // Technically a reset didn't happen here but we want to remember this time point                                                        

            userRealPositionSamples = new List<Vector2>();
            userRealPositionSamplesBuffer = new List<Vector2>();
            userVirtualPositionSamples = new List<Vector2>();
            userVirtualPositionSamplesBuffer = new List<Vector2>();
            translationGainSamples = new List<float>();
            translationGainSamplesBuffer = new List<float>();
            injectedTranslationSamples = new List<float>();
            injectedTranslationSamplesBuffer = new List<float>();
            rotationGainSamples = new List<float>();
            rotationGainSamplesBuffer = new List<float>();
            injectedRotationFromRotationGainSamples = new List<float>();
            injectedRotationFromRotationGainSamplesBuffer = new List<float>();
            curvatureGainSamples = new List<float>();
            curvatureGainSamplesBuffer = new List<float>();
            injectedRotationFromCurvatureGainSamples = new List<float>();
            injectedRotationFromCurvatureGainSamplesBuffer = new List<float>();
            injectedRotationSamples = new List<float>();
            injectedRotationSamplesBuffer = new List<float>();
            distanceToNearestBoundarySamples = new List<float>();
            distanceToNearestBoundarySamplesBuffer = new List<float>();
        }
    }
    //store statistic data of every avatar
    List<AvatarStatistics> avatarStatistics;

    List<float> samplingIntervals = new List<float>();
    float lastSamplingTime = 0;

    //the logging state
    enum LoggingState { not_started, logging, paused, complete };
    LoggingState state = LoggingState.not_started;

    void InitializeAllValues()
    {
        avatarStatistics = new List<AvatarStatistics>();
        for (int i = 0; i < globalConfiguration.redirectedAvatars.Count; i++)
        {
            avatarStatistics.Add(new AvatarStatistics(globalConfiguration.GetTime()));
        }
        samplingIntervals = new List<float>();
        lastSamplingTime = globalConfiguration.GetTime();
    }

    //initialize experiment Results
    public void InitializeExperimentResults()
    {
        experimentResults = new List<ResultOfTrial>();
    }

    // IMPORTANT! The gathering of values has to be in LateUpdate to make sure the "Time.deltaTime" that's used by the gain sampling functions is the same ones that are considered when dividing by time elapsed 
    // that we do when gatherin the samples from buffers. Otherwise it can be that we get the buffers from a deltaTime, then the same deltaTime is used later to calculate a buffer value for a gain, and then 
    // later on the division won't be fair!
    public void UpdateStats()
    {
        if (state == LoggingState.logging)
        {
            // Average and Log Sampled Values If It's Time To
            UpdateFrameBasedValues();
            if (globalConfiguration.GetTime() - lastSamplingTime > (1 / samplingFrequency))
            {
                GenerateSamplesFromBufferValuesAndClearBuffers();
                samplingIntervals.Add(globalConfiguration.GetTime() - lastSamplingTime);
                lastSamplingTime = globalConfiguration.GetTime();
            }
        }
    }

    public void BeginLogging()
    {
        if (state == LoggingState.not_started || state == LoggingState.complete)
        {
            state = LoggingState.logging;
            InitializeAllValues();
        }
    }

    // IF YOU PAUSE, YOU HAVE TO BE CAREFUL ABOUT TIME ELAPSED BETWEEN PAUSES!
    public void PauseLogging()
    {
        if (state == LoggingState.logging)
        {
            state = LoggingState.paused;
        }
    }

    public void ResumeLogging()
    {
        if (state == LoggingState.paused)
        {
            state = LoggingState.logging;
        }
    }

    // Experiment Descriptors are given and 
    public void EndLogging()
    {
        if (state == LoggingState.logging)
        {
            Event_Experiment_Ended();
            state = LoggingState.complete;
        }
    }

    // Experiment Descriptors are given and we add the logged data as a full experiment result bundle
    // data of every avatar
    public ResultOfTrial GetExperimentResultForSummaryStatistics(int endState, List<Dictionary<string, string>> experimentDescriptors)
    {
        var experimentResults = new List<Dictionary<string, string>>();
        for (int i = 0; i < globalConfiguration.redirectedAvatars.Count; i++)
        {
            var experimentDescriptor = experimentDescriptors[i];
            var er = new Dictionary<string, string>(experimentDescriptor);
            experimentResults.Add(er);

            var us = avatarStatistics[i];

            er["reset_count"] = us.resetCount.ToString();
            er["virtual_way_distance"] = us.virtualWayDistance.ToString();

            er["virtual_distance_between_resets_average"] = GetAverage(us.virtualDistancesTravelledBetweenResets).ToString();
            er["time_elapsed_between_resets_average"] = GetAverage(us.timeElapsedBetweenResets).ToString();

            er["sum_injected_translation(IN METERS)"] = us.sumOfInjectedTranslation.ToString();
            er["sum_injected_rotation_g_r(IN DEGREES)"] = us.sumOfInjectedRotationFromRotationGain.ToString();
            er["sum_injected_rotation_g_c(IN DEGREES)"] = us.sumOfInjectedRotationFromCurvatureGain.ToString();
            er["sum_real_distance_travelled(IN METERS)"] = us.sumOfRealDistanceTravelled.ToString();
            er["sum_virtual_distance_travelled(IN METERS)"] = us.sumOfVirtualDistanceTravelled.ToString();
            er["min_g_t"] = us.minTranslationGain < float.MaxValue ? us.minTranslationGain.ToString() : "N/A";
            er["max_g_t"] = us.maxTranslationGain > float.MinValue ? us.maxTranslationGain.ToString() : "N/A";
            er["min_g_r"] = us.minRotationGain < float.MaxValue ? us.minRotationGain.ToString() : "N/A";
            er["max_g_r"] = us.maxRotationGain > float.MinValue ? us.maxRotationGain.ToString() : "N/A";
            er["min_g_c"] = us.minCurvatureGain < float.MaxValue ? us.minCurvatureGain.ToString() : "N/A";
            er["max_g_c"] = us.maxCurvatureGain > float.MinValue ? us.maxCurvatureGain.ToString() : "N/A";
            er["experiment_duration"] = (us.experimentEndingTime - us.experimentBeginningTime).ToString();
            er["execute_duration"] = (((double)us.executeEndingTime - us.executeBeginningTime) / 1e7).ToString();
            er["g_t_average"] = GetAverageOfAbsoluteValues(us.translationGainSamples).ToString();
            er["injected_translation_average"] = GetAverage(us.injectedTranslationSamples).ToString();
            er["g_r_average"] = GetAverageOfAbsoluteValues(us.rotationGainSamples).ToString();
            er["injected_rotation_from_rotation_gain_average"] = GetAverage(us.injectedRotationFromRotationGainSamples).ToString();
            er["g_c_average"] = GetAverageOfAbsoluteValues(us.curvatureGainSamples).ToString();
            er["injected_rotation_from_curvature_gain_average"] = GetAverage(us.injectedRotationFromCurvatureGainSamples).ToString();
            er["injected_rotation_average"] = GetAverage(us.injectedRotationSamples).ToString();
            var gtChange = new List<float>();
            var gcChange = new List<float>();
            for (int j = 0; j < us.translationGainSamples.Count - 1; j++)
            {
                gtChange.Add(Mathf.Abs(us.translationGainSamples[j + 1] - us.translationGainSamples[j]));
                gcChange.Add(Mathf.Abs(us.curvatureGainSamples[j + 1] - us.curvatureGainSamples[j]));
            }
            er["g_t_change_max"] = gtChange.Count > 0 ? (gtChange.Max() * samplingFrequency).ToString() : "0";
            er["g_c_change_max"] = gcChange.Count > 0 ? (gcChange.Max() * samplingFrequency).ToString() : "0";
            er["g_t_change_average"] = gtChange.Count > 0 ? (GetAverage(gtChange) * samplingFrequency).ToString() : "0";
            er["g_c_change_average"] = gcChange.Count > 0 ? (GetAverage(gcChange) * samplingFrequency).ToString() : "0";

            er["real_position_average"] = GetAverage(us.userRealPositionSamples).ToString();
            er["virtual_position_average"] = GetAverage(us.userVirtualPositionSamples).ToString();
            er["distance_to_boundary_average"] = GetAverage(us.distanceToNearestBoundarySamples).ToString();
            er["average_sampling_interval"] = GetAverage(samplingIntervals).ToString();

            //if passive haptics mode
            if (globalConfiguration.passiveHaptics)
            {
                er["positionError"] = us.positionError.ToString();
                er["angleError"] = us.angleError.ToString();
            }
        }
        return new ResultOfTrial(endState, experimentResults);
    }

    public void GetExperimentResultsForSampledVariables(out List<Dictionary<string, List<float>>> oneDimensionalSamples, out List<Dictionary<string, List<Vector2>>> twoDimensionalSamples)
    {
        //store every avatar's info
        oneDimensionalSamples = new List<Dictionary<string, List<float>>>();
        twoDimensionalSamples = new List<Dictionary<string, List<Vector2>>>();
        for (int i = 0; i < avatarStatistics.Count; i++)
        {
            var us = avatarStatistics[i];
            var oneDimensionalSample = new Dictionary<string, List<float>>();
            var twoDimensionalSample = new Dictionary<string, List<Vector2>>();

            oneDimensionalSample.Add("distances_to_boundary", us.distanceToNearestBoundarySamples);
            oneDimensionalSample.Add("g_t", us.translationGainSamples);
            oneDimensionalSample.Add("injected_translations", us.injectedTranslationSamples);
            oneDimensionalSample.Add("g_r", us.rotationGainSamples);
            oneDimensionalSample.Add("injected_rotations_from_rotation_gain", us.injectedRotationFromRotationGainSamples);
            oneDimensionalSample.Add("g_c", us.curvatureGainSamples);
            oneDimensionalSample.Add("injected_rotations_from_curvature_gain", us.injectedRotationFromCurvatureGainSamples);
            oneDimensionalSample.Add("injected_rotations", us.injectedRotationSamples);
            oneDimensionalSample.Add("virtual_distances_between_resets", us.virtualDistancesTravelledBetweenResets);
            oneDimensionalSample.Add("time_elapsed_between_resets", us.timeElapsedBetweenResets);
            oneDimensionalSample.Add("sampling_intervals", samplingIntervals);

            twoDimensionalSample.Add("user_real_positions", us.userRealPositionSamples);
            twoDimensionalSample.Add("user_virtual_positions", us.userVirtualPositionSamples);

            oneDimensionalSamples.Add(oneDimensionalSample);
            twoDimensionalSamples.Add(twoDimensionalSample);
        }
    }

    public void Event_User_Translated(AvatarStatistics us, Vector3 deltaPosition2D)
    {
        if (state == LoggingState.logging)
        {
            us.sumOfVirtualDistanceTravelled += deltaPosition2D.magnitude;
            us.sumOfRealDistanceTravelled += deltaPosition2D.magnitude;
            us.virtualDistanceTravelledSinceLastReset += deltaPosition2D.magnitude;
        }
    }

    public void Event_User_Rotated(float rotationInDegrees)
    {
        if (state == LoggingState.logging)
        {

        }
    }

    public void Event_Translation_Gain(int userId, float g_t, Vector3 translationApplied)
    {
        if (state == LoggingState.logging)
        {
            var us = avatarStatistics[userId];
            us.sumOfInjectedTranslation += translationApplied.magnitude;
            us.maxTranslationGain = Mathf.Max(us.maxTranslationGain, g_t);
            us.minTranslationGain = Mathf.Min(us.minTranslationGain, g_t);
            us.sumOfVirtualDistanceTravelled += Mathf.Sign(g_t - 1) * translationApplied.magnitude; // if gain is positive, redirection reference moves with the user, thus increasing the virtual displacement, and if negative, decreases
            us.virtualDistanceTravelledSinceLastReset += Mathf.Sign(g_t - 1) * translationApplied.magnitude;
            us.translationGainSamplesBuffer.Add(g_t);
            us.injectedTranslationSamplesBuffer.Add(translationApplied.magnitude);
        }
    }

    public void Event_Rotation_Gain(int userId, float g_r, float rotationApplied)
    {
        if (state == LoggingState.logging)
        {
            var us = avatarStatistics[userId];
            us.sumOfInjectedRotationFromRotationGain += Mathf.Abs(rotationApplied);
            us.maxRotationGain = Mathf.Max(us.maxRotationGain, g_r);
            us.minRotationGain = Mathf.Min(us.minRotationGain, g_r);

            us.rotationGainSamplesBuffer.Add(g_r);
            us.injectedRotationFromRotationGainSamplesBuffer.Add(Mathf.Abs(rotationApplied));
            us.injectedRotationSamplesBuffer.Add(Mathf.Abs(rotationApplied));
        }
    }

    public void Event_Curvature_Gain(int userId, float g_c, float rotationApplied)
    {
        if (state == LoggingState.logging)
        {
            var us = avatarStatistics[userId];
            us.sumOfInjectedRotationFromCurvatureGain += Mathf.Abs(rotationApplied);
            us.maxCurvatureGain = Mathf.Max(us.maxCurvatureGain, g_c);
            us.minCurvatureGain = Mathf.Min(us.minCurvatureGain, g_c);

            us.curvatureGainSamplesBuffer.Add(g_c);
            us.injectedRotationFromCurvatureGainSamplesBuffer.Add(Mathf.Abs(rotationApplied));
            us.injectedRotationSamplesBuffer.Add(Mathf.Abs(rotationApplied));
        }
    }

    public void Event_Reset_Triggered(int userId)
    {
        if (state == LoggingState.logging)
        {
            var us = avatarStatistics[userId];
            us.resetCount++;
            us.virtualDistancesTravelledBetweenResets.Add(us.virtualDistanceTravelledSinceLastReset);
            us.virtualDistanceTravelledSinceLastReset = 0;
            us.timeElapsedBetweenResets.Add(globalConfiguration.GetTime() - us.timeOfLastReset);
            us.timeOfLastReset = globalConfiguration.GetTime(); // Technically a reset didn't happen here but we want to remember this time point
        }
    }

    public void Event_Update_PassiveHaptics_Results(int userId, float positionError, float angleError)
    {
        if (state == LoggingState.logging)
        {
            var us = avatarStatistics[userId];
            us.positionError = positionError;
            us.angleError = angleError;
        }
    }

    //record real and virtual paths for logging
    void UpdateFrameBasedValues()
    {
        for (int i = 0; i < avatarStatistics.Count; i++)
        {
            var userGameobject = globalConfiguration.redirectedAvatars[i];
            var rm = userGameobject.GetComponent<RedirectionManager>();
            var mm = userGameobject.GetComponent<MovementManager>();
            var us = avatarStatistics[i];
            // Now we are letting the developer determine the movement manually in update, and we pull the info from redirector
            Event_User_Rotated(rm.deltaDir);
            Event_User_Translated(us, Utilities.FlattenedPos2D(rm.deltaPos));

            us.userRealPositionSamplesBuffer.Add(Utilities.FlattenedPos2D(rm.currPosReal));
            us.userVirtualPositionSamplesBuffer.Add(Utilities.FlattenedPos2D(rm.currPos));
            us.distanceToNearestBoundarySamplesBuffer.Add(Utilities.GetNearestDistAndPosToObstacleAndTrackingSpace(globalConfiguration.physicalSpaces, mm.physicalSpaceIndex, Utilities.FlattenedPos2D(rm.currPosReal)).Item1);
        }
    }

    // get sample from buffer and clear buffer
    void GenerateSamplesFromBufferValuesAndClearBuffers()
    {
        //Debug.Log("GenerateSamplesFromBufferValuesAndClearBuffers");
        for (int i = 0; i < avatarStatistics.Count; i++)
        {
            var us = avatarStatistics[i];
            GetSampleFromBuffer(ref us.userRealPositionSamples, ref us.userRealPositionSamplesBuffer);
            GetSampleFromBuffer(ref us.userVirtualPositionSamples, ref us.userVirtualPositionSamplesBuffer);
            GetSampleFromBuffer(ref us.translationGainSamples, ref us.translationGainSamplesBuffer, globalConfiguration.redirectedAvatars[i].GetComponent<RedirectionManager>().gt);
            GetSampleFromBuffer(ref us.injectedTranslationSamples, ref us.injectedTranslationSamplesBuffer);
            GetSampleFromBuffer(ref us.rotationGainSamples, ref us.rotationGainSamplesBuffer, globalConfiguration.redirectedAvatars[i].GetComponent<RedirectionManager>().gr);
            GetSampleFromBuffer(ref us.injectedRotationFromRotationGainSamples, ref us.injectedRotationFromRotationGainSamplesBuffer);
            GetSampleFromBuffer(ref us.curvatureGainSamples, ref us.curvatureGainSamplesBuffer, globalConfiguration.redirectedAvatars[i].GetComponent<RedirectionManager>().curvature);
            GetSampleFromBuffer(ref us.injectedRotationFromCurvatureGainSamples, ref us.injectedRotationFromCurvatureGainSamplesBuffer);
            GetSampleFromBuffer(ref us.injectedRotationSamples, ref us.injectedRotationSamplesBuffer);
            GetSampleFromBuffer(ref us.distanceToNearestBoundarySamples, ref us.distanceToNearestBoundarySamplesBuffer);
        }
    }

    void GetSampleFromBuffer(ref List<float> samples, ref List<float> buffer, float defaultVal = 0, bool verbose = false)
    {
        float sampleValue = 0;
        foreach (float bufferValue in buffer)
        {
            sampleValue += bufferValue;
        }
        //samples.Add(sampleValue / (redirectionManager.GetTime() - lastSamplingTime));
        // OPTIONALLY WE CAN NOT LOG ANYTHING AT ALL IN THIS CASE!
        samples.Add(buffer.Count != 0 ? sampleValue / buffer.Count : defaultVal);
        if (verbose)
        {
            print("sampleValue: " + sampleValue);
            print("samplingInterval: " + (globalConfiguration.GetTime() - lastSamplingTime));
        }
        buffer.Clear();
    }

    void GetSampleFromBuffer(ref List<Vector2> samples, ref List<Vector2> buffer)
    {
        Vector2 sampleValue = Vector2.zero;
        foreach (Vector2 bufferValue in buffer)
        {
            sampleValue += bufferValue;
        }
        //samples.Add(sampleValue / (redirectionManager.GetTime() - lastSamplingTime));
        samples.Add(sampleValue / buffer.Count);
        buffer.Clear();
    }

    void Event_Experiment_Ended()
    {
        for (int i = 0; i < avatarStatistics.Count; i++)
        {
            var us = avatarStatistics[i];
            us.virtualDistancesTravelledBetweenResets.Add(us.virtualDistanceTravelledSinceLastReset);
            us.timeElapsedBetweenResets.Add(globalConfiguration.GetTime() - us.timeOfLastReset);
            us.experimentEndingTime = globalConfiguration.GetTime();
            us.executeEndingTime = DateTime.Now.Ticks;

            us.virtualWayDistance = 0;
            MovementManager movementManager = globalConfiguration.redirectedAvatars[i].GetComponent<MovementManager>();
            if (movementManager.pathSeedChoice == GlobalConfiguration.PathSeedChoice.VEPath)//jon: new option, using waypoints defined in VEPath
            {
                var veWaypoints = movementManager.vePathWaypoints;
                for (int j = 0; j < veWaypoints.Length - 1; j++)
                    us.virtualWayDistance += (veWaypoints[j + 1].position - veWaypoints[j].position).magnitude;
            }
            else
            {
                var waypoints = movementManager.waypoints;
                for (int j = 0; j < waypoints.Count - 1; j++)
                    us.virtualWayDistance += (waypoints[j + 1] - waypoints[j]).magnitude;
            }
        }
        //Debug.Log("********redirectionManager.GetTime():"+ redirectionManager.GetTime());
    }

    Vector2 GetTimeWeightedSampleAverage(List<Vector2> sampleArray, List<float> sampleDurationArray)
    {
        Vector2 valueSum = Vector2.zero;
        float timeSum = 0;
        for (int i = 0; i < sampleArray.Count; i++)
        {
            valueSum += sampleArray[i] * sampleDurationArray[i];
            timeSum += sampleDurationArray[i];
        }
        return sampleArray.Count != 0 ? valueSum / timeSum : Vector2.zero;
    }

    // utils
    float GetAverage(List<float> array)
    {
        float sum = 0;
        foreach (float value in array)
        {
            sum += value;
        }
        return array.Count != 0 ? sum / array.Count : 0;
    }

    float GetAverageOfAbsoluteValues(List<float> array)
    {
        float sum = 0;
        foreach (float value in array)
        {
            sum += Mathf.Abs(value);
        }
        return array.Count != 0 ? sum / array.Count : 0;
    }

    Vector2 GetAverage(List<Vector2> array)
    {
        Vector2 sum = Vector2.zero;
        foreach (Vector2 value in array)
        {
            sum += value;
        }
        return sum / array.Count;
    }

    // We're not providing a time-based version of this at this time
    float GetMedian(List<float> array)
    {
        if (array.Count == 0)
        {
            Debug.LogError("Empty Array");
            return 0;
        }
        List<float> sortedArray = array.OrderBy(item => item).ToList<float>();
        if (sortedArray.Count % 2 == 1)
            return sortedArray[(int)(0.5f * sortedArray.Count)];
        else
            return 0.5f * (sortedArray[(int)(0.5f * sortedArray.Count)] + sortedArray[(int)(0.5f * sortedArray.Count) - 1]);
    }

    ////////////// LOGGING TO FILE
    [HideInInspector]
    public string RESULT_DIRECTORY;
    [HideInInspector]
    public string RESULT_WITH_TIME_DIRECTORY;//save results with time mark
    [HideInInspector]
    public string SUMMARY_STATISTICS_DIRECTORY;
    [HideInInspector]
    public string SAMPLED_METRICS_DIRECTORY;
    [HideInInspector]
    public string GRAPH_DERECTORY;
    [HideInInspector]
    public string TMP_DERECTORY;
    [HideInInspector]
    public string SCREENSHOTS_DERECTORY;


    XmlWriter xmlWriter;
    [HideInInspector]
    public string SUMMARY_STATISTICS_XML_FILENAME = "SimulationResults";
    const string XML_ROOT = "Experiments";
    const string XML_ELEMENT = "Experiment";

    StreamWriter csvWriter;

    private Texture2D texRealPathGraph;//tex for simulation real path logging
    private Texture2D texVirtualPathGraph;//tex for simulation virtual path logging

    void Awake()
    {
        RESULT_DIRECTORY = "Experiment Results/";
        RESULT_WITH_TIME_DIRECTORY = Utilities.GetTimeStringForFileName() + "/";
        SUMMARY_STATISTICS_DIRECTORY = "Summary Statistics/";
        SAMPLED_METRICS_DIRECTORY = "Sampled Metrics/";
        GRAPH_DERECTORY = "Graphs/";
        TMP_DERECTORY = "Tmp/";
        SCREENSHOTS_DERECTORY = "Screenshots/";

        texRealPathGraph = new Texture2D(imageResolution, imageResolution);
        texVirtualPathGraph = new Texture2D(imageResolution, imageResolution);

        globalConfiguration = GetComponent<GlobalConfiguration>();
        RESULT_DIRECTORY = Utilities.GetProjectPath() + RESULT_DIRECTORY;
        RESULT_WITH_TIME_DIRECTORY = RESULT_DIRECTORY + RESULT_WITH_TIME_DIRECTORY;
        SUMMARY_STATISTICS_DIRECTORY = RESULT_WITH_TIME_DIRECTORY + SUMMARY_STATISTICS_DIRECTORY;
        SAMPLED_METRICS_DIRECTORY = RESULT_WITH_TIME_DIRECTORY + SAMPLED_METRICS_DIRECTORY;
        GRAPH_DERECTORY = RESULT_WITH_TIME_DIRECTORY + GRAPH_DERECTORY;
        TMP_DERECTORY = RESULT_WITH_TIME_DIRECTORY + TMP_DERECTORY;
        SCREENSHOTS_DERECTORY = RESULT_WITH_TIME_DIRECTORY + SCREENSHOTS_DERECTORY;

        //create relative directories        
        Utilities.CreateDirectoryIfNeeded(RESULT_DIRECTORY);
        Utilities.CreateDirectoryIfNeeded(RESULT_WITH_TIME_DIRECTORY);
        Utilities.CreateDirectoryIfNeeded(SUMMARY_STATISTICS_DIRECTORY);
        Utilities.CreateDirectoryIfNeeded(SAMPLED_METRICS_DIRECTORY);
        Utilities.CreateDirectoryIfNeeded(GRAPH_DERECTORY);
        Utilities.CreateDirectoryIfNeeded(TMP_DERECTORY);
        Utilities.CreateDirectoryIfNeeded(SCREENSHOTS_DERECTORY);

        obstacleColor = globalConfiguration.obstacleColor;
    }
    public string Get_TMP_DERECTORY()
    {
        return TMP_DERECTORY;
    }
    // Writes all summary statistics for a batch of experiments
    public void LogExperimentSummaryStatisticsResults(List<Dictionary<string, string>> experimentResults)
    {
        // Settings
        XmlWriterSettings settings = new XmlWriterSettings();
        settings.Indent = true;
        settings.IndentChars = ("\t");
        settings.CloseOutput = true;

        // Create XML File
        xmlWriter = XmlWriter.Create(SUMMARY_STATISTICS_DIRECTORY + SUMMARY_STATISTICS_XML_FILENAME + ".xml", settings);
        xmlWriter.Settings.Indent = true;
        xmlWriter.WriteStartDocument();
        xmlWriter.WriteStartElement(XML_ROOT);

        // HACK: If there's only one element, Excel won't show the lablels, so we're duplicating for now
        if (experimentResults.Count == 1)
        {
            experimentResults.Add(experimentResults[0]);
        }

        foreach (Dictionary<string, string> experimentResult in experimentResults)
        {
            xmlWriter.WriteStartElement(XML_ELEMENT);
            foreach (KeyValuePair<string, string> entry in experimentResult)
            {
                xmlWriter.WriteElementString(entry.Key, entry.Value);
            }
            xmlWriter.WriteEndElement();
        }

        xmlWriter.WriteEndElement();
        xmlWriter.WriteEndDocument();
        xmlWriter.Flush();
        xmlWriter.Close();
    }

    //export results of every trial to csv
    public void LogExperimentSummaryStatisticsResultsSCSV(List<ResultOfTrial> experimentResults, string resultDir, string resultFileName)
    {
        if (!Directory.Exists(resultDir))
            Directory.CreateDirectory(resultDir);
        csvWriter = new StreamWriter(resultDir + resultFileName + ".csv");
        csvWriter.WriteLine("sep=;");
        //csvWriter.WriteLine("");
        for (int experimentTrialId = 0; experimentTrialId < experimentResults.Count; experimentTrialId++)
        {
            var experimentResult = experimentResults[experimentTrialId].result;
            var endState = experimentResults[experimentTrialId].EndStateToString();
            var firstLine = string.Format("TrialId = {0};EndState = {1}", experimentTrialId, endState);
            csvWriter.WriteLine(firstLine);
            if (experimentResult.Count > 0)
            {
                // Set up the headers
                csvWriter.Write("experiment_start_time;");
                foreach (string header in experimentResult[0].Keys)
                {
                    csvWriter.Write(header + ";");
                }
                csvWriter.WriteLine();
                // Write Values            
                foreach (var experimentResultPerUser in experimentResult)
                {
                    csvWriter.Write(globalConfiguration.startTimeOfProgram + ";");
                    foreach (string value in experimentResultPerUser.Values)
                    {
                        csvWriter.Write(value + ";");
                    }
                    csvWriter.WriteLine();
                }
            }
            if (experimentTrialId < experimentResults.Count - 1)
                csvWriter.WriteLine();
        }
        csvWriter.Flush();
        csvWriter.Close();
    }

    //export crucial results to csv
    public void LogLightVersionCSV(List<ResultOfTrial> experimentResults, string resultDir, string resultFileName)
    {
        if (!Directory.Exists(resultDir))
            Directory.CreateDirectory(resultDir);
        csvWriter = new StreamWriter(resultDir + resultFileName + ".csv");
        string[] headers = { "trial", "endState", "user", "redirector", "resetCount", "virtualDistance", "gtChangeMax", "gcChangeMax", "gtChangeAvg", "gcChangeAvg" };
        for (int i = 0; i < headers.Length; i++)
        {
            if (i < headers.Length - 1)
            {
                csvWriter.Write(headers[i] + ",");
            }
            else
            {
                csvWriter.Write(headers[i]);
            }
        }
        csvWriter.WriteLine();
        for (int experimentTrialId = 0; experimentTrialId < experimentResults.Count; experimentTrialId++)
        {
            var experimentResult = experimentResults[experimentTrialId].result;
            var endState = experimentResults[experimentTrialId].EndStateToString();

            if (experimentResult.Count > 0)
            { // Write Values
                int userIndex = 0;
                foreach (var experimentResultPerUser in experimentResult)
                {
                    csvWriter.Write(experimentTrialId + ",");
                    csvWriter.Write(endState + ",");
                    csvWriter.Write(userIndex + ",");
                    csvWriter.Write(experimentResultPerUser["redirector"] + ",");
                    csvWriter.Write(experimentResultPerUser["reset_count"] + ",");
                    csvWriter.Write(experimentResultPerUser["sum_virtual_distance_travelled(IN METERS)"] + ",");
                    csvWriter.Write(experimentResultPerUser["g_t_change_max"] + ",");
                    csvWriter.Write(experimentResultPerUser["g_c_change_max"] + ",");
                    csvWriter.Write(experimentResultPerUser["g_t_change_average"] + ",");
                    csvWriter.Write(experimentResultPerUser["g_c_change_average"]);
                    csvWriter.WriteLine();
                    userIndex++;
                }
            }
        }
        csvWriter.Flush();
        csvWriter.Close();
    }

    //save path, boundaries, obstacles as a image
    public void LogExperimentPathPictures(int experimentSetupId)
    {
        var experimentSetups = globalConfiguration.experimentSetups;

        var experimentSetup = experimentSetups[experimentSetupId];

        //set background to white
        Utilities.SetTextureToSingleColor(texRealPathGraph, backgroundColor);

        var trackingSpaces = new List<List<Vector2>>();
        var obstaclePolygons = new List<List<Vector2>>();
        float minx = float.MaxValue, miny = float.MaxValue, maxx = float.MinValue, maxy = float.MinValue;
        foreach (var space in globalConfiguration.physicalSpaces)
        {
            trackingSpaces.Add(space.trackingSpace);
            foreach (var point in space.trackingSpace)
            {
                minx = Mathf.Min(minx, point.x);
                miny = Mathf.Min(miny, point.y);
                maxx = Mathf.Max(maxx, point.x);
                maxy = Mathf.Max(maxy, point.y);
            }
            foreach (var obstacle in space.obstaclePolygons)
            {
                obstaclePolygons.Add(obstacle);
            }
        }
        realSideLength = Mathf.Max(maxx - minx, maxy - miny);
        Vector2 centerp = new Vector2((minx + maxx) / 2, (miny + maxy) / 2);
        foreach (var trackingSpacePoints in trackingSpaces)
        {
            for (int i = 0; i < trackingSpacePoints.Count; i++)
                Utilities.DrawLine(texRealPathGraph, trackingSpacePoints[i] - centerp, trackingSpacePoints[(i + 1) % trackingSpacePoints.Count] - centerp, realSideLength, borderThickness, trackingSpaceColor);
        }

        foreach (var obstaclePolygon in obstaclePolygons)
            Utilities.DrawPolygon(texRealPathGraph, obstaclePolygon, realSideLength, borderThickness, obstacleColor, centerp);
        //for (int i = 0; i < obstaclePolygon.Count; i++)
        //    Utilities.DrawLine(tex, obstaclePolygon[i], obstaclePolygon[(i + 1) % obstaclePolygon.Count], sideLength, borderThickness, obstacleColor);
        for (int uId = 0; uId < avatarStatistics.Count; uId++)
        {
            var color = globalConfiguration.avatarColors[uId];
            var realPosList = avatarStatistics[uId].userRealPositionSamples;
            var beginWeight = 0.1f;
            var deltaWeight = (1 - beginWeight) / realPosList.Count;

            for (int i = 0; i < realPosList.Count - 1; i++)
            {
                var w = (beginWeight + deltaWeight * i);
                //Debug.Log("realPosList[i]:" + realPosList[i].ToString("f3"));
                Utilities.DrawLine(texRealPathGraph, realPosList[i] - centerp, realPosList[i + 1] - centerp, realSideLength, pathThickness, w * color + (1 - w) * backgroundColor, (w + deltaWeight) * color + (1 - w - deltaWeight) * backgroundColor);
            }
        }

        texRealPathGraph.Apply();

        //Export as png file
        Utilities.ExportTexture2dToPng(GRAPH_DERECTORY + string.Format("{0}_{1}_realPath.png", experimentSetupId, Utilities.GetTimeStringForFileName()), texRealPathGraph);

    }

    public void LogOneDimensionalExperimentSamples(string experimentSamplesDirectory, string measuredMetric, List<float> values)
    {
        Utilities.CreateDirectoryIfNeeded(experimentSamplesDirectory);
        csvWriter = new StreamWriter(experimentSamplesDirectory + measuredMetric + ".csv");
        foreach (float value in values)
        {
            csvWriter.WriteLine(value);
        }
        csvWriter.Flush();
        csvWriter.Close();
    }

    public void LogTwoDimensionalExperimentSamples(string experimentSamplesDirectory, string measuredMetric, List<Vector2> values)
    {
        Utilities.CreateDirectoryIfNeeded(experimentSamplesDirectory);
        csvWriter = new StreamWriter(experimentSamplesDirectory + measuredMetric + ".csv");
        foreach (Vector2 value in values)
        {
            csvWriter.WriteLine(value.x + ", " + value.y);
        }
        csvWriter.Flush();
        csvWriter.Close();
    }

    //save results to local
    public void LogAllExperimentSamples(string experimentDecriptorString, List<Dictionary<string, List<float>>> oneDimensionalSamplesMaps, List<Dictionary<string, List<Vector2>>> twoDimensionalSamplesMaps)
    {
        globalConfiguration.GetResultDirAndFileName(SAMPLED_METRICS_DIRECTORY, out string resultDir, out string fileName);
        Utilities.CreateDirectoryIfNeeded(resultDir);
        resultDir += fileName + "/";
        Utilities.CreateDirectoryIfNeeded(resultDir);
        string experimentSamplesDirectory = resultDir + experimentDecriptorString + "/";
        Utilities.CreateDirectoryIfNeeded(experimentSamplesDirectory);
        Debug.Log("experimentSamplesDirectory: " + experimentSamplesDirectory);

        for (var i = 0; i < oneDimensionalSamplesMaps.Count; i++)
        {
            var oneDimensionalSamplesMap = oneDimensionalSamplesMaps[i];
            foreach (KeyValuePair<string, List<float>> oneDimensionalSamples in oneDimensionalSamplesMap)
            {
                LogOneDimensionalExperimentSamples(experimentSamplesDirectory + "userId_" + i + "/", oneDimensionalSamples.Key, oneDimensionalSamples.Value);
            }
        }

        for (var i = 0; i < twoDimensionalSamplesMaps.Count; i++)
        {
            var twoDimensionalSamplesMap = twoDimensionalSamplesMaps[i];
            foreach (KeyValuePair<string, List<Vector2>> twoDimensionalSamples in twoDimensionalSamplesMap)
            {
                LogTwoDimensionalExperimentSamples(experimentSamplesDirectory + "userId_" + i + "/", twoDimensionalSamples.Key, twoDimensionalSamples.Value);
            }
        }
    }
    public bool IfResetCountExceedLimit(int id)
    {
        return avatarStatistics[id].resetCount > MaxResetCount;
    }
}
