// Towards physically interactive virtual environments:reactive alignment with redirected walking
// https://www.jeraldthomas.com/static/publications/thomas2020towards.pdf

using UnityEngine;
using System.Collections;
using System.Collections.Generic;


public class PassiveHapticAPF_Redirector : APF_Redirector
{
    private const float CURVATURE_GAIN_CAP_DEGREES_PER_SECOND = 15;  // degrees per second
    private const float ROTATION_GAIN_CAP_DEGREES_PER_SECOND = 30;  // degrees per second

    //parameters used by the paper
    private const float Aa = 1;
    private const float Ba = 2;
    private const float Ao = 1;
    private const float Bo = -1;
    private bool alignmentState = false; // alignmentState == true: use attractive force and replusive force; alignmentState == false: only use repulsive force

    public override void InjectRedirection()
    {
        // var obstaclePolygons = globalConfiguration.obstaclePolygons;
        // var trackingSpacePoints = globalConfiguration.trackingSpacePoints;
        var physicalSpaces = redirectionManager.globalConfiguration.physicalSpaces;

        GetRepulsiveAndAttractiveForceAndNegativeGradient(physicalSpaces, out float rf, out Vector2 ng);
        ApplyRedirectionByNegativeGradient(ng);
    }

    //calculate attractive force and repulsive force and negative gradient
    public void GetRepulsiveAndAttractiveForceAndNegativeGradient(List<SingleSpace> physicalSpaces, out float rf, out Vector2 ng)
    {
        var nearestPosList = new List<Vector2>();
        var currPosReal = Utilities.FlattenedPos2D(redirectionManager.currPosReal);
        int userIndex = movementManager.physicalSpaceIndex; // we only consider the space where the current user's at
        SingleSpace space = physicalSpaces[userIndex];
        //contribution from borders
        for (int i = 0; i < space.trackingSpace.Count; i++)
        {
            var p = space.trackingSpace[i];
            var q = space.trackingSpace[(i + 1) % space.trackingSpace.Count];
            var nearestPos = Utilities.GetNearestPos(currPosReal, new List<Vector2> { p, q });
            //Debug.Log(p.ToString() + q.ToString() + nearestPos.ToString());
            var n = Utilities.RotateVector(q - p, -90).normalized;
            var d = currPosReal - nearestPos;
            if (Vector2.Dot(n, d.normalized) > 0)
            {
                nearestPosList.Add(nearestPos);
            }
        }

        //contribution from obstacles
        foreach (var obstacle in space.obstaclePolygons)
        {
            var nearestPos = Utilities.GetNearestPos(currPosReal, obstacle);
            nearestPosList.Add(nearestPos);
        }

        //contribution from other avatars
        foreach (var avatar in globalConfiguration.redirectedAvatars)
        {
            if (avatar.GetComponent<MovementManager>().physicalSpaceIndex != userIndex)
            {
                continue;
            }
            var avatarId = avatar.GetComponent<MovementManager>().avatarId;
            //ignore self
            if (avatarId == movementManager.avatarId)
                continue;
            var nearestPos = avatar.GetComponent<RedirectionManager>().currPosReal;
            nearestPosList.Add(Utilities.FlattenedPos2D(nearestPos));
        }
        UpdateAlignmentState();
        rf = 0;
        ng = Vector2.zero;

        ng = AttractiveNegtiveGradient(currPosReal) + ObstacleNegtiveGradient(currPosReal, nearestPosList);
        ng = ng.normalized;
        UpdateTotalForcePointer(ng);
    }
    private Vector2 AttractiveNegtiveGradient(Vector2 currPosReal)
    {
        if (!globalConfiguration.passiveHaptics || !alignmentState)
        {
            return Vector2.zero;
        }
        var physicalTargetPosReal = globalConfiguration.physicalTargetTransforms[movementManager.avatarId].position;
        var gDelta = Aa * Mathf.Pow((currPosReal - physicalTargetPosReal).magnitude, Ba) * (currPosReal - physicalTargetPosReal).normalized;
        return -gDelta;//NegtiveGradient
    }
    private Vector2 ObstacleNegtiveGradient(Vector2 currPosReal, List<Vector2> nearestPosList)
    {
        var ng = Vector2.zero;
        float rf = 0;//totalforce
        foreach (var obPos in nearestPosList)
        {
            rf += 1 / (currPosReal - obPos).magnitude;

            //get contribution from each obstacle
            var gDelta = -Ao * Mathf.Pow((currPosReal - obPos).magnitude, Bo) * (currPosReal - obPos).normalized;
            ng += -gDelta;//NegtiveGradient
        }
        return ng;
    }

    public void UpdateAlignmentState()
    {
        alignmentState = false;

        //position and direction in physical tracking space
        var currPosReal = Utilities.FlattenedPos2D(redirectionManager.currPosReal);
        var currDirReal = Utilities.FlattenedDir2D(redirectionManager.currDirReal);
        var gc = globalConfiguration;
        Vector2 objVirtualPos;
        if (movementManager.pathSeedChoice == GlobalConfiguration.PathSeedChoice.VEPath)//jon: newly added waypoints defined in VEPath
        {
            objVirtualPos = Utilities.FlattenedPos2D(movementManager.vePathWaypoints[movementManager.vePathWaypoints.Length - 1].position);
        }
        else
        {
            objVirtualPos = movementManager.waypoints[movementManager.waypoints.Count - 1];
        }
        var objPhysicalPos = gc.physicalTargetTransforms[movementManager.avatarId].position;

        //the virtual distance from the user to the alignment target
        var Dv = (objVirtualPos - Utilities.FlattenedPos2D(redirectionManager.currPos)).magnitude;

        //the physical distance from the user to the alignment target
        var Dp = (objPhysicalPos - currPosReal).magnitude;

        var gt = gc.MIN_TRANS_GAIN + 1;
        var Gt = gc.MAX_TRANS_GAIN + 1;
        //the physical rotational oﬀset
        var phiP = Vector2.Angle(currDirReal, objPhysicalPos - currPosReal) * Mathf.Deg2Rad;
        if (gt * Dp < Dv && Dv < Gt * Dp)
        {
            if (phiP < Mathf.Asin((Dp * 1 / gc.CURVATURE_RADIUS) / 2))
            {
                alignmentState = true;
                //Debug.Log("alignmentState = true");
            }
        }
    }

    public void ApplyRedirectionByNegativeGradient(Vector2 ng)
    {
        //calculate translation
        if (Vector2.Dot(ng, Utilities.FlattenedDir2D(redirectionManager.currDirReal)) < 0)
        {
            SetTranslationGain(globalConfiguration.MAX_TRANS_GAIN);
        }
        else
        {
            SetTranslationGain(1);
        }


        var desiredFacingDirection = Utilities.UnFlatten(ng);//vector of negtive gradient in physical space
        int desiredSteeringDirection = (-1) * (int)Mathf.Sign(Utilities.GetSignedAngle(redirectionManager.currDirReal, desiredFacingDirection));

        if (redirectionManager.deltaDir * desiredSteeringDirection < 0)
        {//rotate away from negtive gradient
            SetRotationGain(globalConfiguration.MIN_ROT_GAIN);
            //g_r = desiredSteeringDirection * Mathf.Min(Mathf.Abs(deltaDir * globalConfiguration.MIN_ROT_GAIN), maxRotationFromRotationGain);
        }
        else
        {//rotate towards negtive gradient
            SetRotationGain(globalConfiguration.MAX_ROT_GAIN);
            //g_r = desiredSteeringDirection * Mathf.Min(Mathf.Abs(deltaDir * globalConfiguration.MAX_ROT_GAIN), maxRotationFromRotationGain);
        }
        SetCurvature(desiredSteeringDirection * 1 / globalConfiguration.CURVATURE_RADIUS);

        ApplyGains();
    }
}