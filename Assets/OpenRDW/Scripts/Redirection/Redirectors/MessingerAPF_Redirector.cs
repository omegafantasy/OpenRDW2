// extended version, suitable for Nonconvex tracking space
// Effects of tracking area shape and size on artificial potential field redirected walking
// https://www.cs.purdue.edu/cgvlab/courses/490590VR/notes/VRLocomotion/MultiuserRedirectedWalking/TrackingAreaShapeSizeEffects2019.pdf

// original version
// Multi-user redirected walking and resetting using artificial potential fields
// https://www.cs.purdue.edu/cgvlab/courses/490590VR/notes/VRLocomotion/MultiuserRedirectedWalking/APFRedirectedWalking2019.pdf

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MessingerAPF_Redirector : APF_Redirector
{
    private static readonly float targetSegLength = 1;//split edges of boundaries/obstacles into small segments with length equal to targetSegLength

    //constant parameters used by the paper
    private static readonly float C = 0.00897f;
    private static readonly float lamda = 2.656f;
    private static readonly float gamma = 3.091f;

    private const float CURVATURE_GAIN_CAP_DEGREES_PER_SECOND = 15;  // degrees per second
    private const float ROTATION_GAIN_CAP_DEGREES_PER_SECOND = 30;  // degrees per second

    private const float M = 15;//unit:degree, the maximum Steering rate in proximity-Based Steering Rate Scaling strategy

    private const float baseRate = 1.5f;//the rotation rate when the avatar is standing still
    private const float scaleMultiplier = 2.5f;
    private const float angRateScaleDilate = 1.3f;
    private const float angRateScaleCompress = 0.85f;

    public override void InjectRedirection()
    {
        var physicalSpaces = redirectionManager.globalConfiguration.physicalSpaces;

        // var obstaclePolygons = redirectionManager.globalConfiguration.obstaclePolygons;
        // var trackingSpacePoints = redirectionManager.globalConfiguration.trackingSpacePoints;
        var userTransforms = redirectionManager.globalConfiguration.GetAvatarTransforms();

        //calculate total force by the paper
        var forceT = GetTotalForce(physicalSpaces, userTransforms);
        forceT = forceT.normalized;

        UpdateTotalForcePointer(forceT);

        //apply redirection according to the total force
        InjectRedirectionByForce(forceT, physicalSpaces);

    }
    //calculate the total force by the paper
    public Vector2 GetTotalForce(List<SingleSpace> physicalSpaces, List<Transform> userTransforms)
    {
        var t = Vector2.zero;
        var w = Vector2.zero;
        var u = Vector2.zero;
        int userIndex = movementManager.physicalSpaceIndex; // we only consider the space where the current user's at
        SingleSpace space = physicalSpaces[userIndex];
        for (int i = 0; i < space.trackingSpace.Count; i++)
            w += GetW_Force(space.trackingSpace[i], space.trackingSpace[(i + 1) % space.trackingSpace.Count]);
        foreach (var ob in space.obstaclePolygons)
            for (int i = 0; i < ob.Count; i++)
            {
                //swap positions because the vertices order is in counter-clockwise
                w += GetW_Force(ob[(i + 1) % ob.Count], ob[i]);
            }
        foreach (var user in userTransforms)
            if (user.GetComponent<MovementManager>().physicalSpaceIndex == userIndex)
            {
                u += GetU_Force(user);
            }
        t = w + u;
        return t;
    }
    // get force contributed by every edge of the obstacle or border
    public Vector2 GetW_Force(Vector2 p, Vector2 q)
    {
        var wForce = Vector2.zero;
        //split long edge to short segments then accumulate
        var length = (p - q).magnitude;
        var segNum = (int)(length / targetSegLength);
        if (segNum * targetSegLength < length)
            segNum++;
        var segLength = length / segNum;
        var unitVec = (q - p).normalized;
        for (int i = 1; i <= segNum; i++)
        {
            var tmpP = p + unitVec * (i - 1) * segLength;
            var tmpQ = p + unitVec * i * segLength;
            wForce += GetW_ForceEverySeg(tmpP, tmpQ);
        }
        return wForce;
    }

    //get force contributed by a segment
    public Vector2 GetW_ForceEverySeg(Vector2 p, Vector2 q)
    {
        //get center point
        var c = (p + q) / 2;

        var currPos = Utilities.FlattenedPos2D(redirectionManager.currPosReal);
        var d = currPos - c;
        //normal towards walkable side
        var n = Utilities.RotateVector(q - p, -90).normalized;

        if (Vector2.Dot(n, d.normalized) > 0)
            return C * (q - p).magnitude * d.normalized * 1 / Mathf.Pow(d.magnitude, lamda);
        else
            return Vector2.zero;
    }
    //get forces from other avatars
    public Vector2 GetU_Force(Transform user)
    {
        //ignore self
        if (user == transform)
            return Vector2.zero;
        var otherRm = user.GetComponent<RedirectionManager>();
        //get other avatars' positions and directions
        var otherUserPos = Utilities.FlattenedPos2D(otherRm.currPosReal);
        var otherUserDir = Utilities.FlattenedDir2D(otherRm.currDirReal);

        //get local avatar position and direction
        var currPos = Utilities.FlattenedPos2D(redirectionManager.currPosReal);
        var currDir = Utilities.FlattenedDir2D(redirectionManager.currDirReal);

        var theta1 = Vector2.Angle(otherUserPos - currPos, currDir);
        var theta2 = Vector2.Angle(currPos - otherUserPos, otherUserDir);
        var k = Mathf.Clamp01((Mathf.Cos(theta1 * Mathf.Deg2Rad) + Mathf.Cos(theta2 * Mathf.Deg2Rad)) / 2);

        var d = currPos - otherUserPos;
        return k * d.normalized * 1 / Mathf.Pow(d.magnitude, gamma);
    }

    //do redirection by MessingerAPF
    public void InjectRedirectionByForce(Vector2 force, List<SingleSpace> physicalSpaces)
    {
        var desiredFacingDirection = Utilities.UnFlatten(force);//total force vector in physical space
        int desiredSteeringDirection = (-1) * (int)Mathf.Sign(Utilities.GetSignedAngle(redirectionManager.currDirReal, desiredFacingDirection));

        //calculate walking speed
        var v = redirectionManager.deltaPos.magnitude / globalConfiguration.GetDeltaTime();
        float movingRate = 0;

        movingRate = 360 * v / (2 * Mathf.PI * globalConfiguration.CURVATURE_RADIUS);
        //only consider static obstacles
        var distToObstacle = Utilities.GetNearestDistAndPosToObstacleAndTrackingSpace(physicalSpaces, movementManager.physicalSpaceIndex, Utilities.FlattenedPos2D(redirectionManager.currPosReal)).Item1;

        //distance smaller than curvature radius，use Proximity-Based Steering Rate Scaling strategy
        if (distToObstacle < globalConfiguration.CURVATURE_RADIUS)
        {
            var h = movingRate;
            var m = distToObstacle;
            var t = 1 - m / globalConfiguration.CURVATURE_RADIUS;
            var appliedSteeringRate = (1 - t) * h + t * M;
            movingRate = appliedSteeringRate;//calculate steering rate of curvature gain
        }
        SetCurvature(desiredSteeringDirection * movingRate * redirectionManager.GetDeltaTime() / Mathf.Rad2Deg / Mathf.Max(0.001f, redirectionManager.deltaPos.magnitude)); // WARNING: this could result in a curvature above imperceptible levels

        if (redirectionManager.deltaDir * desiredSteeringDirection < 0)
        {//rotate away total force vector
            SetRotationGain(globalConfiguration.MIN_ROT_GAIN);
            //g_r = desiredSteeringDirection * Mathf.Max(baseRate * deltaTime, Mathf.Min(Mathf.Abs(deltaDir * redirectionManager.globalConfiguration.MIN_ROT_GAIN), maxRotationFromRotationGain));
        }
        else
        {//rotate towards total force vector
            SetRotationGain(globalConfiguration.MAX_ROT_GAIN);
            //g_r = desiredSteeringDirection * Mathf.Max(baseRate * deltaTime, Mathf.Min(Mathf.Abs(deltaDir * redirectionManager.globalConfiguration.MAX_ROT_GAIN), maxRotationFromRotationGain));
        }
        SetTranslationGain(1);

        ApplyGains();
    }
}