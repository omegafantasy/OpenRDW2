// Dynamic Artificial Potential Fields for Multi-User Redirected Walking
// https://ieeexplore.ieee.org/abstract/document/9089569

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class DynamicAPF_Redirector : APF_Redirector
{
    //The physical space boundary or obstacle boundary is divided into small segments with a growth of no more than targetSegLength long for accumulation
    private static readonly float targetSegLength = 1;

    //Constant parameters used in the formula
    // private static readonly float C = 0.00897f;
    // private static readonly float lamda = 2.656f;
    private static readonly float gamma = 1.5f;

    private const float CURVATURE_GAIN_CAP_DEGREES_PER_SECOND = 15;  // degrees per second
    private const float ROTATION_GAIN_CAP_DEGREES_PER_SECOND = 30;  // degrees per second

    //Unit: degree, the maximum steering value in the proximity based steering rate scaling policy
    private const float M = 15;

    private const float baseRate = 1.5f;//degrees of rotation when the user is standing still
    private const float scaleMultiplier = 2.5f;
    private const float angRateScaleDilate = 1.3f;
    private const float angRateScaleCompress = 0.85f;
    private const float V = 0.1f;//velocity larger than V: use curvature gain; otherwise use rotation gain

    private static readonly float c = 0.25f;
    private static readonly float a1 = 1f;
    private static readonly float a2 = 0.02f;
    private static readonly float b1 = 2f;
    private static readonly float b2 = 1f;

    private Vector2 currPosReal;
    private Vector2 spaceCenterPoint;

    public override void InjectRedirection()
    {
        currPosReal = Utilities.FlattenedPos2D(redirectionManager.currPosReal);
        // var obstaclePolygons = globalConfiguration.obstaclePolygons;
        // var trackingSpacePoints = globalConfiguration.trackingSpacePoints;
        var physicalSpaces = globalConfiguration.physicalSpaces;
        var userTransforms = globalConfiguration.GetAvatarTransforms();

        //calculate total force by the formulas given by the paper
        var forceT = GetTotalForce(physicalSpaces, userTransforms);
        var gravitation = GetPrimarySteeringTargetDir(physicalSpaces, userTransforms) * 0.5f * forceT.magnitude;
        var totalForce = (forceT + gravitation).normalized;

        forceT = forceT.normalized;
        totalForce = totalForce.normalized;

        //update the total force pointer
        UpdateTotalForcePointer(totalForce);

        //apply redirection by the calculated force vector
        ApplyRedirectionByForce(totalForce, physicalSpaces);

    }

    //calculate the total force by the corresponding paper
    public Vector2 GetTotalForce(List<SingleSpace> physicalSpaces, List<Transform> userTransforms)
    {
        var t = Vector2.zero;
        var w = Vector2.zero;
        var u = Vector2.zero;
        var avatar = Vector2.zero;
        int userIndex = movementManager.physicalSpaceIndex; // we only consider the space where the current user's at
        float userPriority = redirectionManager.priority;
        SingleSpace space = physicalSpaces[userIndex];
        for (int i = 0; i < space.trackingSpace.Count; i++)
            w += GetW_Force(space.trackingSpace[i], space.trackingSpace[(i + 1) % space.trackingSpace.Count]);
        foreach (var ob in space.obstaclePolygons)
        {
            for (int i = 0; i < ob.Count; i++)
            {
                //vertices is in counter-clockwise order, need to swap the positions
                w += GetW_Force(ob[(i + 1) % ob.Count], ob[i]);
            }
        }
        foreach (var user in userTransforms)
        {
            if (user.GetComponent<MovementManager>().physicalSpaceIndex == userIndex)
            {
                u += GetU_Force(user);
                if (user.GetComponent<RedirectionManager>().priority > userPriority)
                {
                    avatar += GetAvatar_Force(user);
                }
            }
        }

        t = w + u + avatar;
        return t;
    }

    //Obtain the force contributed by each edge of the obstacle and physical space. By default, turn 90°counterclockwise to face trackingspace
    public Vector2 GetW_Force(Vector2 p, Vector2 q)
    {
        var wForce = Vector2.zero;

        var nearestPos = Utilities.GetNearestPos(currPosReal, new List<Vector2> { p, q });
        //normal
        var n = Utilities.RotateVector(q - p, -90).normalized;
        var d = currPosReal - nearestPos;
        if (Vector2.Dot(n, d.normalized) > 0)
            wForce = 1 / (currPosReal - nearestPos).magnitude * (currPosReal - nearestPos).normalized;
        else
            wForce = Vector2.zero;

        return wForce;
    }

    //get the force from other user
    public Vector2 GetU_Force(Transform user)
    {
        //ignore self
        if (user == transform)
            return Vector2.zero;
        var rm = user.GetComponent<RedirectionManager>();

        //get real position and direction
        var otherUserPos = Utilities.FlattenedPos2D(rm.currPosReal);
        var otherUserDir = Utilities.FlattenedDir2D(rm.currDirReal);

        //get local user's position and direction        
        var currPos = Utilities.FlattenedPos2D(redirectionManager.currPosReal);
        var currDir = Utilities.FlattenedDir2D(redirectionManager.currDirReal);

        var theta1 = Vector2.Angle(otherUserPos - currPos, currDir);
        var theta2 = Vector2.Angle(currPos - otherUserPos, otherUserDir);
        var k = Mathf.Clamp01((Mathf.Cos(theta1 * Mathf.Deg2Rad) + Mathf.Cos(theta2 * Mathf.Deg2Rad)) / 2);

        var d = currPos - otherUserPos;
        return k * d.normalized * 1 / Mathf.Pow(d.magnitude, gamma);
    }

    public Vector2 GetAvatar_Force(Transform user)
    {
        var rm = user.GetComponent<RedirectionManager>();

        if (user == transform)
            return Vector2.zero;

        //get real position and direction of other user
        var otherUserPos = Utilities.FlattenedPos2D(rm.currPosReal);
        var otherUserDir = Utilities.FlattenedDir2D(rm.currDirReal);

        var avatarPos = otherUserPos + otherUserDir.normalized;
        var avatarDir = otherUserDir;

        //get real position and direction of current user
        var currPos = Utilities.FlattenedPos2D(redirectionManager.currPosReal);
        var currDir = Utilities.FlattenedDir2D(redirectionManager.currDirReal);

        var theta1 = Vector2.Angle(avatarPos - currPos, currDir);
        var theta2 = Vector2.Angle(currPos - avatarPos, avatarDir);
        var k = Mathf.Clamp01((Mathf.Cos(theta1 * Mathf.Deg2Rad) + Mathf.Cos(theta2 * Mathf.Deg2Rad)) / 2);

        var d = currPos - avatarPos;
        return c * k * d.normalized * 1 / Mathf.Pow(d.magnitude, gamma);
    }

    //Get primary steering target direction calculated by paper
    public Vector2 GetPrimarySteeringTargetDir(List<SingleSpace> physicalSpaces, List<Transform> userTransforms)
    {
        //get real position and direction of current user
        var currPos = Utilities.FlattenedPos2D(redirectionManager.currPosReal);
        var currDir = Utilities.FlattenedDir2D(redirectionManager.currDirReal);

        //lock potentialArea
        var footPoint1 = Vector2.zero;
        var footPoint2 = Vector2.zero;
        var boundary1 = new List<Vector2>();
        var boundary2 = new List<Vector2>();
        int userIndex = movementManager.physicalSpaceIndex; // we only consider the space where the current user's at
        SingleSpace space = physicalSpaces[userIndex];
        for (int i = 0; i < space.trackingSpace.Count; i++)
        {
            boundary1 = new List<Vector2> { space.trackingSpace[i], space.trackingSpace[(i + 1) % space.trackingSpace.Count] };
            boundary2 = new List<Vector2> { space.trackingSpace[(i + 1) % space.trackingSpace.Count], space.trackingSpace[(i + 2) % space.trackingSpace.Count] };
            footPoint1 = Utilities.GetNearestPos(currPos, boundary1);
            footPoint2 = Utilities.GetNearestPos(currPos, boundary2);
            if (Vector2.Dot(footPoint1 - currPos, currDir) >= 0 && Vector2.Dot(footPoint2 - currPos, currDir) >= 0)
            {
                break;
            }
        }

        //calculate primaryTarget
        var xDir = (footPoint1 - currPos).normalized;
        var yDir = (footPoint2 - currPos).normalized;
        var primaryTarget = currPos;
        float maxsum = 0;
        for (int i = 0; i < (int)(footPoint1 - currPos).magnitude; i++)
        {
            for (int j = 0; j < (int)(footPoint2 - currPos).magnitude; j++)
            {
                var target = currPos + xDir / 2 + yDir / 2 + xDir * i + yDir * j;
                if (Utilities.PointLineRelation(target, boundary1[0], boundary1[1]) == 2 || Utilities.PointLineRelation(target, boundary2[0], boundary2[1]) == 2)
                { // ensure the target is in the tracking space
                    continue;
                }
                float sum = 0;
                foreach (var user in userTransforms)
                {
                    if (user != transform && user.GetComponent<MovementManager>().physicalSpaceIndex == userIndex)
                    {
                        var rm = user.GetComponent<RedirectionManager>();
                        var otherUserPos = Utilities.FlattenedPos2D(rm.currPosReal);
                        sum += (otherUserPos - target).magnitude;
                    }
                }
                if (sum > maxsum)
                {
                    primaryTarget = target;
                    maxsum = sum;
                }
            }
        }


        //lock selectionArea

        List<Vector2> selectPoints = new List<Vector2>();

        for (int i = 0; i < space.trackingSpace.Count; i++)
            selectPoints.Add(space.trackingSpace[i]);
        foreach (var user in userTransforms)
        {
            if (user != transform && user.GetComponent<MovementManager>().physicalSpaceIndex == userIndex)
            {
                var rm = user.GetComponent<RedirectionManager>();
                var otherUserPos = Utilities.FlattenedPos2D(rm.currPosReal);
                selectPoints.Add(otherUserPos);
            }
        }
        selectPoints.Sort((v1, v2) =>
        {
            return v1[0].CompareTo(v2[0]);
        });


        float maxS = 0;
        float RIGHT = float.PositiveInfinity;
        float LEFT = float.NegativeInfinity;
        float UP = float.PositiveInfinity;
        float DOWN = float.NegativeInfinity;
        for (int left = 0; left < selectPoints.Count - 1; left++)
        {
            for (int right = left + 1; right < selectPoints.Count; right++)
            {
                float leftX = selectPoints[left][0] - primaryTarget[0];
                float rightX = selectPoints[right][0] - primaryTarget[0];
                if (leftX <= 0 && 0 <= rightX)
                {
                    float upY = float.PositiveInfinity;
                    float downY = float.NegativeInfinity;
                    bool findup = false;
                    bool finddown = false;
                    for (int i = left; i <= right; i++)
                    {
                        float dis = selectPoints[i][1] - primaryTarget[1];
                        if (dis >= 0 && dis < upY)
                        {
                            upY = dis;
                            findup = true;
                        }
                        if (dis < 0 && dis > downY)
                        {
                            downY = dis;
                            finddown = true;
                        }
                    }
                    if (findup && finddown && (upY - downY) * (rightX - leftX) >= maxS)
                    {
                        maxS = (upY - downY) * (rightX - leftX);
                        RIGHT = rightX;
                        LEFT = leftX;
                        UP = upY;
                        DOWN = downY;
                    }
                }
            }
        }

        //calculate steeringTarget
        var steeringTarget = primaryTarget;

        xDir = new Vector2(0, 1);
        yDir = new Vector2(1, 0);
        Vector2 startPoint = primaryTarget + LEFT * xDir + DOWN * yDir;
        maxsum = float.NegativeInfinity;
        if (spaceCenterPoint == null)
        {
            globalConfiguration.GetTrackingSpaceBoundingbox(out float minX, out float maxX, out float minY, out float maxY, userIndex);
            spaceCenterPoint = new Vector2((minX + maxX) / 2, (minY + maxY) / 2);
        }
        for (int i = 0; i < (int)(RIGHT - LEFT); i++)
        {
            for (int j = 0; j < (int)(UP - DOWN); j++)
            {
                var target = startPoint + xDir / 2 + yDir / 2 + xDir * i + yDir * j;
                float D1 = Utilities.GetNearestDistToTrackingSpace(space.trackingSpace, target);
                float D2 = (target - startPoint - spaceCenterPoint).magnitude;
                float sum = b1 * D1 - b2 * D2;
                if (sum > maxsum)
                {
                    steeringTarget = target;
                    maxsum = sum;
                }
            }
        }

        return (steeringTarget - currPos).normalized;
    }

    //use ThomasAPF for Redirection
    public void ApplyRedirectionByForce(Vector2 force, List<SingleSpace> physicalSpaces)
    {
        var desiredFacingDirection = Utilities.UnFlatten(force);//total force vector in physical space
        int desiredSteeringDirection = (-1) * (int)Mathf.Sign(Utilities.GetSignedAngle(redirectionManager.currDirReal, desiredFacingDirection));
        if (Vector2.Dot(force, Utilities.FlattenedDir2D(redirectionManager.currDirReal)) < 0)
        {
            SetTranslationGain(globalConfiguration.MAX_TRANS_GAIN);
        }
        else
        {
            SetTranslationGain(1);
        }

        if (redirectionManager.deltaDir * desiredSteeringDirection < 0)
        {//rotate away total force vector
            SetRotationGain(globalConfiguration.MIN_ROT_GAIN);
        }
        else
        {//rotate towards total force vector
            SetRotationGain(globalConfiguration.MAX_ROT_GAIN);
        }
        SetCurvature(desiredSteeringDirection * 1 / globalConfiguration.CURVATURE_RADIUS);

        ApplyGains();
    }


    //calculate px as priority by the paper    
    public override void GetPriority()
    {
        var physicalSpaces = globalConfiguration.physicalSpaces;
        var userTransforms = globalConfiguration.GetAvatarTransforms();

        //calculate the total force needed by the priority        
        var t = GetPriorityForce(physicalSpaces, userTransforms);

        //large number means large priority        
        var dir = Utilities.FlattenedDir2D(redirectionManager.currDirReal);
        redirectionManager.priority = -(a1 * t.magnitude + a2 * Vector2.Angle(t, dir));
    }

    //get total force needed by the priority according to the paper    
    public Vector2 GetPriorityForce(List<SingleSpace> physicalSpaces, List<Transform> userTransforms)
    {
        var t = Vector2.zero;
        var w = Vector2.zero;
        var u = Vector2.zero;
        int userIndex = movementManager.physicalSpaceIndex; // we only consider the space where the current user's at
        SingleSpace space = physicalSpaces[userIndex];
        for (int i = 0; i < space.trackingSpace.Count; i++)
            w += GetW_Force(space.trackingSpace[i], space.trackingSpace[(i + 1) % space.trackingSpace.Count]);
        foreach (var ob in space.obstaclePolygons)
        {
            for (int i = 0; i < ob.Count; i++)
            {
                //swap the positions because vertices of the obstacle is in counterclockwise order                
                w += GetW_Force(ob[(i + 1) % ob.Count], ob[i]);
            }
        }
        foreach (var user in userTransforms)
        {
            if (user.GetComponent<MovementManager>().physicalSpaceIndex == userIndex)
            {
                u += GetU_Force(user);
            }
        }

        t = w + u;
        return t;
    }
}


