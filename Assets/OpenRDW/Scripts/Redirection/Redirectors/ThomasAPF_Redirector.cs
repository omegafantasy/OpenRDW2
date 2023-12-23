// A general reactive algorithm for redirected walking using artificial potential functions
// https://www.jeraldthomas.com/static/publications/thomas2019general.pdf

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ThomasAPF_Redirector : APF_Redirector
{
    private const float CURVATURE_GAIN_CAP_DEGREES_PER_SECOND = 15;  // degrees per second
    private const float ROTATION_GAIN_CAP_DEGREES_PER_SECOND = 30;  // degrees per second

    public override void InjectRedirection()
    {
        // var obstaclePolygons = globalConfiguration.obstaclePolygons;
        // var trackingSpacePoints = globalConfiguration.trackingSpacePoints;
        var physicalSpaces = globalConfiguration.physicalSpaces;

        //get repulsive force and negative gradient
        GetRepulsiveForceAndNegativeGradient(physicalSpaces, out float rf, out Vector2 ng);
        ApplyRedirectionByNegativeGradient(ng);
    }

    public void GetRepulsiveForceAndNegativeGradient(List<SingleSpace> physicalSpaces, out float rf, out Vector2 ng)
    {
        var nearestPosList = new List<Vector2>();
        var currPosReal = Utilities.FlattenedPos2D(redirectionManager.currPosReal);
        int userIndex = movementManager.physicalSpaceIndex; // we only consider the space where the current user's at
        SingleSpace space = physicalSpaces[userIndex];

        //physical borders' contributions
        for (int i = 0; i < space.trackingSpace.Count; i++)
        {
            var p = space.trackingSpace[i];
            var q = space.trackingSpace[(i + 1) % space.trackingSpace.Count];
            var nearestPos = Utilities.GetNearestPos(currPosReal, new List<Vector2> { p, q });
            var n = Utilities.RotateVector(q - p, -90).normalized;
            var d = currPosReal - nearestPos;
            if (Vector2.Dot(n, d.normalized) > 0)
            {
                nearestPosList.Add(nearestPos);
            }
        }

        //obstacle contribution
        foreach (var obstacle in space.obstaclePolygons)
        {
            var nearestPos = Utilities.GetNearestPos(currPosReal, obstacle);
            nearestPosList.Add(nearestPos);
        }

        //consider avatar as point obstacles
        foreach (var user in globalConfiguration.redirectedAvatars)
        {
            if (user.GetComponent<MovementManager>().physicalSpaceIndex != userIndex)
            {
                continue;
            }
            var uId = user.GetComponent<MovementManager>().avatarId;
            //ignore self
            if (uId == movementManager.avatarId)
                continue;
            var nearestPos = user.GetComponent<RedirectionManager>().currPosReal;
            nearestPosList.Add(Utilities.FlattenedPos2D(nearestPos));
        }

        rf = 0;
        ng = Vector2.zero;
        foreach (var obPos in nearestPosList)
        {
            rf += 1 / (currPosReal - obPos).magnitude;

            //get gradient contributions
            var gDelta = -1 / (currPosReal - obPos).magnitude * (currPosReal - obPos).normalized;

            ng += -gDelta;//negtive gradient
        }
        ng = ng.normalized;
        UpdateTotalForcePointer(ng);
    }

    //apply redirection by negtive gradient
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