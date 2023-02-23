using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SeparateSpace_Resetter : Resetter
{
    const float INF = 100000;
    float requiredRotateSteerAngle = 0; // steering angle，rotate the physical plane and avatar together
    float requiredRotateAngle = 0; // normal rotation angle, only rotate avatar
    float rotateDir; // rotation direction, positive if rotate clockwise
    float speedRatio;
    public Vector2 resetDir;
    public bool useResetDir;

    private void Awake(){
        useResetDir = false;
    }

    public override bool IsResetRequired()
    {
        return IfCollisionHappens();
    }

    public override void InitializeReset()
    {
        var rm = redirectionManager;
        var currPosReal = Utilities.FlattenedPos2D(rm.currPosReal);
        targetPos = DecideResetPosition(currPosReal);
        var currDir = Utilities.FlattenedDir2D(rm.currDirReal);
        if (useResetDir)
        {
            targetDir = resetDir;
        }
        else
        {
            targetDir = -currDir;
        }

        var angle2Waypoint = Vector2.SignedAngle(Utilities.FlattenedDir2D(redirectionManager.currDir), Utilities.FlattenedDir2D(redirectionManager.targetWaypoint.position - redirectionManager.currPos));

        var targetRealRotation = 360 - Vector2.Angle(targetDir, currDir); // required rotation angle in real world

        rotateDir = -(int)Mathf.Sign(Utilities.GetSignedAngle(rm.currDirReal, Utilities.UnFlatten(targetDir)));

        requiredRotateSteerAngle = 360 - targetRealRotation - rotateDir * angle2Waypoint;
        if (requiredRotateSteerAngle < 0)
        {
            requiredRotateSteerAngle += 360;
        }

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