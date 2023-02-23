using UnityEngine;
using System.Collections;

public class S2CRedirector : SteerToRedirector
{
    // Testing Parameters
    bool dontUseTempTargetInS2C = false;

    private const float S2C_BEARING_ANGLE_THRESHOLD_IN_DEGREE = 160;
    private const float S2C_TEMP_TARGET_DISTANCE = 4;

    public override void PickRedirectionTarget()
    {
        if (spaceCenter == null)
        {
            var spaceCenterObject = new GameObject("S2C CenterObject");
            spaceCenter = spaceCenterObject.transform;
            spaceCenter.parent = redirectionManager.trackingSpace;
            globalConfiguration.GetTrackingSpaceBoundingbox(out float minX, out float maxX, out float minY, out float maxY, movementManager.physicalSpaceIndex);
            spaceCenter.position = Utilities.GetInverseRelativePosition(new Vector3((minX + maxX) / 2, 0, (minY + maxY) / 2), redirectionManager.trackingSpace);
        }

        Vector3 userToCenter = Utilities.GetRelativePosition(spaceCenter.position, redirectionManager.trackingSpace) - redirectionManager.currPosReal;

        //Compute steering target for S2C
        float bearingToCenter = Vector3.Angle(userToCenter, redirectionManager.currDirReal);//unsigned angle
        float directionToCenter = Mathf.Sign(Utilities.GetSignedAngle(redirectionManager.currDirReal, userToCenter));//signed angle
        //Debug.Log(bearingToCenter);
        if (bearingToCenter >= S2C_BEARING_ANGLE_THRESHOLD_IN_DEGREE && !dontUseTempTargetInS2C)
        {
            //Generate temporary target
            if (noTmpTarget)
            {
                tmpTarget = new GameObject("S2C Temp Target");
                tmpTarget.transform.parent = transform;
                tmpTarget.transform.position = Utilities.GetInverseRelativePosition(redirectionManager.currPosReal + S2C_TEMP_TARGET_DISTANCE * (Quaternion.Euler(0, directionToCenter * 90, 0) * redirectionManager.currDirReal.normalized), redirectionManager.trackingSpace);
                noTmpTarget = false;
            }
            currentTarget = tmpTarget.transform;
        }
        else
        {
            currentTarget = spaceCenter;
            if (!noTmpTarget)
            {
                Destroy(tmpTarget);
                noTmpTarget = true;
            }
        }
    }

}
