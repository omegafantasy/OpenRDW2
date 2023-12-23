using UnityEngine;
using System.Collections;

public abstract class SteerToRedirector : Redirector
{
    // Testing Parameters
    bool useBearingThresholdBasedRotationDampeningTimofey = true;
    bool dontUseDampening = false;

    // User Experience Improvement Parameters
    private const float MOVEMENT_THRESHOLD = 0.2f; // meters per second
    private const float ROTATION_THRESHOLD = 1.5f; // degrees per second
    private const float CURVATURE_GAIN_CAP_DEGREES_PER_SECOND = 15;  // degrees per second
    private const float ROTATION_GAIN_CAP_DEGREES_PER_SECOND = 30;  // degrees per second
    private const float DISTANCE_THRESHOLD_FOR_DAMPENING = 1.25f; // Distance threshold to apply dampening (meters)
    private const float BEARING_THRESHOLD_FOR_DAMPENING = 45f; // Bearing threshold to apply dampening (degrees) MAHDI: WHERE DID THIS VALUE COME FROM?
    private const float SMOOTHING_FACTOR = 0.125f; // Smoothing factor for redirection rotations

    // Reference Parameters
    protected Transform currentTarget; //Where the participant  is currently directed?
    protected GameObject tmpTarget;
    protected Transform spaceCenter;

    // State Parameters
    protected bool noTmpTarget = true;

    // Auxiliary Parameters
    private float rotationFromCurvatureGain; //Proposed curvature gain based on user speed
    private float rotationFromRotationGain; //Proposed rotation gain based on head's yaw
    private float lastRotationApplied = 0f;


    public abstract void PickRedirectionTarget();

    public override void InjectRedirection()
    {
        PickRedirectionTarget();

        // Get Required Data
        Vector3 deltaPos = redirectionManager.deltaPos;
        float deltaDir = redirectionManager.deltaDir;

        //Compute desired facing vector for redirection
        Vector3 desiredFacingDirection = Utilities.FlattenedPos3D(currentTarget.position) - redirectionManager.currPos;
        int desiredSteeringDirection = (-1) * (int)Mathf.Sign(Utilities.GetSignedAngle(redirectionManager.currDir, desiredFacingDirection)); // We have to steer to the opposite direction so when the user counters this steering, she steers in right direction

        //Compute proposed rotation gain
        rotationFromRotationGain = 0;
        //Determine if we need to rotate with or against the user
        if (deltaDir * desiredSteeringDirection < 0)
        {
            //Rotating against the user
            rotationFromRotationGain = Mathf.Abs(deltaDir * redirectionManager.globalConfiguration.MIN_ROT_GAIN - 1);
        }
        else
        {
            //Rotating with the user
            rotationFromRotationGain = Mathf.Abs(deltaDir * redirectionManager.globalConfiguration.MAX_ROT_GAIN - 1);
        }
        SetCurvature(desiredSteeringDirection * 1 / globalConfiguration.CURVATURE_RADIUS);
        SetTranslationGain(1);

        float rotationProposed = desiredSteeringDirection * rotationFromRotationGain;

        if (!dontUseDampening)
        {
            //DAMPENING METHODS
            // MAHDI: Sinusiodally scaling the rotation when the bearing is near zero
            float bearingToTarget = Vector3.Angle(redirectionManager.currDir, desiredFacingDirection);
            if (useBearingThresholdBasedRotationDampeningTimofey)
            {
                // TIMOFEY
                if (bearingToTarget <= BEARING_THRESHOLD_FOR_DAMPENING)
                    rotationProposed *= Mathf.Sin(Mathf.Deg2Rad * 90 * bearingToTarget / BEARING_THRESHOLD_FOR_DAMPENING);
            }
            else
            {
                // MAHDI
                // The algorithm first is explained to be similar to above but at the end it is explained like this. Also the BEARING_THRESHOLD_FOR_DAMPENING value was never mentioned which make me want to use the following even more.
                rotationProposed *= Mathf.Sin(Mathf.Deg2Rad * bearingToTarget);
            }


            // MAHDI: Linearly scaling the rotation when the distance is near zero
            if (desiredFacingDirection.magnitude <= DISTANCE_THRESHOLD_FOR_DAMPENING)
            {
                rotationProposed *= desiredFacingDirection.magnitude / DISTANCE_THRESHOLD_FOR_DAMPENING;
            }

        }

        // Implement additional rotation with smoothing
        float finalRotation = (1.0f - SMOOTHING_FACTOR) * lastRotationApplied + SMOOTHING_FACTOR * rotationProposed;
        //float finalRotation = rotationProposed;
        lastRotationApplied = finalRotation;

        SetRotationGain(1 + finalRotation / Mathf.Max(0.001f, redirectionManager.deltaDir));

        ApplyGains();
    }
}
