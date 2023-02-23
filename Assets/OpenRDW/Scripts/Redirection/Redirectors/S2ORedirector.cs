﻿using UnityEngine;
using System.Collections;

public class S2ORedirector : SteerToRedirector
{
    private const float S2O_TARGET_GENERATION_ANGLE_IN_DEGREES = 60;

    public override void PickRedirectionTarget()
    {
        if (spaceCenter == null)
        {
            var spaceCenterObject = new GameObject("S2O CenterObject");
            spaceCenter = spaceCenterObject.transform;
            spaceCenter.parent = redirectionManager.trackingSpace;
            globalConfiguration.GetTrackingSpaceBoundingbox(out float minX, out float maxX, out float minY, out float maxY, movementManager.physicalSpaceIndex);
            spaceCenter.position = Utilities.GetInverseRelativePosition(new Vector3((minX + maxX) / 2, 0, (minY + maxY) / 2), redirectionManager.trackingSpace);
        }
        //use smaller radius when the tracking space is small
        var S2O_TARGET_RADIUS = 7.5f;//Target orbit radius for Steer-to-Orbit algorithm (meters)
        var trackingSpaceSize = redirectionManager.globalConfiguration.GetTrackingSpaceBoundingboxSize(movementManager.physicalSpaceIndex);
        if (trackingSpaceSize.x <= 30 || trackingSpaceSize.y <= 30)
        {
            //S2O_TARGET_RADIUS = 2f;
            S2O_TARGET_RADIUS = Mathf.Min(trackingSpaceSize.x, trackingSpaceSize.y) / 4;
            //Debug.Log("S2O_TARGET_RADIUS: " + S2O_TARGET_RADIUS);
        }

        Vector3 userToCenter = spaceCenter.position - redirectionManager.currPos;

        //Compute steering target for S2O
        if (noTmpTarget)
        {
            tmpTarget = new GameObject("S2O Target");
            tmpTarget.transform.parent = transform;
            //Debug.Log(tmpTarget.transform.position);
            currentTarget = tmpTarget.transform;
            noTmpTarget = false;
        }

        //Step One: Compute angles for direction from center to potential targets
        float alpha;
        //Where is user relative to desired orbit?
        if (userToCenter.magnitude < S2O_TARGET_RADIUS) //Inside the orbit
        {
            alpha = S2O_TARGET_GENERATION_ANGLE_IN_DEGREES;
        }
        else
        {
            //Use tangents of desired orbit
            alpha = Mathf.Acos(S2O_TARGET_RADIUS / userToCenter.magnitude) * Mathf.Rad2Deg;
        }
        //Step Two: Find directions to two petential target positions
        Vector3 dir1 = Quaternion.Euler(0, alpha, 0) * -userToCenter.normalized;
        Vector3 targetPosition1 = spaceCenter.position + S2O_TARGET_RADIUS * dir1;
        Vector3 dir2 = Quaternion.Euler(0, -alpha, 0) * -userToCenter.normalized;
        Vector3 targetPosition2 = spaceCenter.position + S2O_TARGET_RADIUS * dir2;

        //Step Three: Evaluate difference in direction
        // We don't care about angle sign here
        float angle1 = Vector3.Angle(redirectionManager.currDir, targetPosition1 - redirectionManager.currPos);
        float angle2 = Vector3.Angle(redirectionManager.currDir, targetPosition2 - redirectionManager.currPos);

        currentTarget.transform.position = (angle1 <= angle2) ? targetPosition1 : targetPosition2;
    }


}
