using UnityEngine;
using System.Collections;
using System.Collections.Generic;


// align to the vector calculated by artificial potential fileds, rotate to the side of the larger angle
// R2G means reset-to-gradient 
public class R2G_Resetter : Resetter
{

    float requiredRotateSteerAngle = 0; // steering angle，rotate the physical plane and avatar together

    float requiredRotateAngle = 0; // normal rotation angle, only rotate avatar

    float rotateDir; // rotation direction, positive if rotate clockwise

    float speedRatio;

    APF_Redirector redirector;

    public override bool IsResetRequired()
    {
        return IfCollisionHappens();
    }

    public override void InitializeReset()
    {
        var redirectorTmp = redirectionManager.redirector;
        var currPos = Utilities.FlattenedPos2D(redirectionManager.currPosReal);
        targetPos = DecideResetPosition(currPos);
        if (redirectorTmp.GetType().IsSubclassOf(typeof(APF_Redirector)))
        {
            redirector = (APF_Redirector)redirectorTmp;
            targetDir = redirector.totalForce;
        }
        else
        {
            targetDir = getGradientForceByThomasAPF(currPos);
            // Debug.Log("RedirectorType: " + redirectorTmp.GetType());
            // Debug.LogError("non-APF redirector can't use R2G_resetter");
        }
        var currDir = Utilities.FlattenedDir2D(redirectionManager.currDirReal);

        var nearestDistAndPos = Utilities.GetNearestDistAndPosToObstacleAndTrackingSpace(globalConfiguration.physicalSpaces, movementManager.physicalSpaceIndex, currPos);
        var obstaclePos = Vector2.zero;
        if (nearestDistAndPos.Item1 > globalConfiguration.RESET_TRIGGER_BUFFER && globalConfiguration.physicalSpaces.Count > 1)
        { // separate spaces
        }
        else
        {
            if (nearestDistAndPos.Item1 > globalConfiguration.RESET_TRIGGER_BUFFER)
            {
                // reset not triggered by obstacles or tracking space
                obstaclePos = Utilities.GetNearestAvtarPos(globalConfiguration.redirectedAvatars, movementManager.avatarId, movementManager.physicalSpaceIndex, currPos);
            }
            else
            {
                obstaclePos = nearestDistAndPos.Item2;
            }
            var normal = (currPos - obstaclePos).normalized;
            if (Vector2.Dot(normal, targetDir) <= 0)
            { // choose a reasonable direction instead
                targetDir = normal;
            }
        }

        var targetRealRotation = 360 - Vector2.Angle(targetDir, currDir); // required rotation angle in real world

        rotateDir = -(int)Mathf.Sign(Utilities.GetSignedAngle(redirectionManager.currDirReal, Utilities.UnFlatten(targetDir)));

        requiredRotateSteerAngle = 360 - targetRealRotation;

        requiredRotateAngle = targetRealRotation;

        speedRatio = requiredRotateSteerAngle / requiredRotateAngle;

        if (globalConfiguration.useResetPanel)
        {
            SetPanel();
        }
        else
        {
            SetHUD((int)rotateDir);
        }
    }

    public override void InjectResetting()
    {
        if (globalConfiguration.useResetPanel)
        { // use freeze-turn to ensure the virtual postion and direction unchanged
            InjectRotation(-redirectionManager.deltaDir);
            InjectTranslation(-redirectionManager.deltaPos);
            UpdatePanel();
        }
        else
        {
            var steerRotation = speedRatio * redirectionManager.deltaDir;
            if (Mathf.Abs(requiredRotateSteerAngle) <= Mathf.Abs(steerRotation) || requiredRotateAngle == 0)
            { // meet the rotation requirement
                InjectRotation(requiredRotateSteerAngle);

                // reset end
                redirectionManager.OnResetEnd();
                requiredRotateSteerAngle = 0;
            }
            else
            { // rotate the rotation calculated by ratio
                InjectRotation(steerRotation);
                requiredRotateSteerAngle -= Mathf.Abs(steerRotation);
            }
        }
    }

    public override void EndReset()
    {
        if (globalConfiguration.useResetPanel)
        {
            DestroyPanel();
        }
        else
        {
            DestroyHUD();
        }
    }

    public override void SimulatedWalkerUpdate()
    {
        // Act is if there's some dummy target a meter away from you requiring you to rotate        
        var rotateAngle = redirectionManager.GetDeltaTime() * redirectionManager.globalConfiguration.rotationSpeed;
        // finish specified rotation
        if (rotateAngle >= requiredRotateAngle)
        {
            rotateAngle = requiredRotateAngle;
            // Avoid accuracy error
            requiredRotateAngle = 0;
        }
        else
        {
            requiredRotateAngle -= rotateAngle;
        }
        redirectionManager.simulatedWalker.RotateInPlace(rotateAngle * rotateDir);
    }

    // a basic method to get apf force
    // we can use other methods if needed
    public Vector2 getGradientForceByThomasAPF(Vector2 currPosReal)
    {
        var nearestPosList = new List<Vector2>();
        int userIndex = movementManager.physicalSpaceIndex; // we only consider the space where the current user's at
        SingleSpace space = globalConfiguration.physicalSpaces[userIndex];

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

        var ng = Vector2.zero;
        foreach (var obPos in nearestPosList)
        {
            //get gradient contributions
            var gDelta = -1 / (currPosReal - obPos).magnitude * (currPosReal - obPos).normalized;

            ng += -gDelta;//negtive gradient
        }
        ng = ng.normalized;
        return ng;
    }
}
