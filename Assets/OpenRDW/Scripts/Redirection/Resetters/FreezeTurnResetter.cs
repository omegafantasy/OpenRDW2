using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// This type of reset injects a 180 rotation. It will actually stop the virtual movement when user is turning. 
public class FreezeTurnResetter : Resetter
{
    float requiredRotateAngle = 0;
    System.Random rand;

    void Awake()
    {
        rand = new System.Random();
    }
    public override bool IsResetRequired()
    {
        return IfCollisionHappens();
    }

    public override void InitializeReset()
    {
        requiredRotateAngle = 180;
        targetPos = DecideResetPosition(Utilities.FlattenedPos2D(redirectionManager.currPosReal));
        targetDir = -Utilities.FlattenedDir2D(redirectionManager.currDirReal);
        if (globalConfiguration.useResetPanel)
        {
            SetPanel();
        }
        else
        {
            SetHUD(1);
        }
    }
    public override void InjectResetting()
    {
        InjectRotation(-redirectionManager.deltaDir);
        InjectTranslation(-redirectionManager.deltaPos);
        if (globalConfiguration.useResetPanel)
        {
            UpdatePanel();
        }
        else
        {
            if (requiredRotateAngle == 0)
            { // meet the rotation requirement
                redirectionManager.OnResetEnd();
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
        redirectionManager.simulatedWalker.RotateInPlace(rotateAngle);
    }
}