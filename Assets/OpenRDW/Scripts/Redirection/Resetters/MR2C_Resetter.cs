using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// Proposed by: A general reactive algorithm for redirected walking using artificial potential functions
// https://www.jeraldthomas.com/static/publications/thomas2019general.pdf
// MR2C means modified reset-to-center
public class MR2C_Resetter : Resetter
{
    float requiredRotateSteerAngle = 0; // steering angle，rotate the physical plane and avatar together

    float requiredRotateAngle = 0; // normal rotation angle, only rotate avatar

    float rotateDir; // rotation direction, positive if rotate clockwise

    float speedRatio;
    protected Transform spaceCenter;
    public override bool IsResetRequired()
    {
        return IfCollisionHappens();
    }

    public override async void InitializeReset()
    {
        if (spaceCenter == null)
        {
            var spaceCenterObject = new GameObject("MR2C CenterObject");
            spaceCenter = spaceCenterObject.transform;
            spaceCenter.parent = redirectionManager.trackingSpace;
            globalConfiguration.GetTrackingSpaceBoundingbox(out float minX, out float maxX, out float minY, out float maxY, movementManager.physicalSpaceIndex);
            spaceCenter.position = Utilities.GetInverseRelativePosition(new Vector3((minX + maxX) / 2, 0, (minY + maxY) / 2), redirectionManager.trackingSpace);
        }
        var centerPos = Utilities.FlattenedPos2D(Utilities.GetRelativePosition(spaceCenter.position, redirectionManager.trackingSpace));
        var currPos = Utilities.FlattenedPos2D(redirectionManager.currPosReal);
        targetPos = DecideResetPosition(currPos);
        targetDir = (centerPos - currPos).normalized;

        var nearestDistAndPos = Utilities.GetNearestDistAndPosToObstacleAndTrackingSpace(globalConfiguration.physicalSpaces, movementManager.physicalSpaceIndex, currPos);
        var obstaclePos = Vector2.zero;
        if (nearestDistAndPos.Item1 > globalConfiguration.RESET_TRIGGER_BUFFER)
        { // reset not triggered by obstacles or tracking space
            obstaclePos = Utilities.GetNearestAvtarPos(globalConfiguration.redirectedAvatars, movementManager.avatarId, movementManager.physicalSpaceIndex, currPos);
        }
        else
        {
            obstaclePos = nearestDistAndPos.Item2;
        }
        var normal = (currPos - obstaclePos).normalized;
        if (Vector2.Dot(normal, targetDir) <= 0)
        { // choose a reasonable direction instead
            if (Utilities.GetSignedAngle(normal, targetDir) < 0)
            {
                targetDir = Utilities.RotateVector(normal, -70);
            }
            else
            {
                targetDir = Utilities.RotateVector(normal, 70);
            }
        }

        var currDir = Utilities.FlattenedDir2D(redirectionManager.currDirReal);
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
}