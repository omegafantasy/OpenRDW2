using UnityEngine;
using System.Collections;
using System.Collections.Generic;


/// <summary>
/// This type of reset injects a 180 rotation. It will show a prompt to the user once at the full rotation is applied and the user is roughly looking at the original direction.
/// The method is simply doubling the rotation amount. No smoothing is applied. No specific rotation is enforced this way.
/// </summary>
public class TwoOneTurnResetter : Resetter
{

    float overallInjectedRotation;

    float requiredRotateAngle = 0;

    public override bool IsResetRequired()
    {
        return IfCollisionHappens();
    }

    public override void InitializeReset()
    {
        //rotate by redirectionManager
        overallInjectedRotation = 0;

        //rotate by simulatedWalker
        requiredRotateAngle = 180;

        targetPos = DecideResetPosition(Utilities.FlattenedPos2D(redirectionManager.currPosReal));
        targetDir = -Utilities.FlattenedDir2D(redirectionManager.currDirReal);
        if (globalConfiguration.useResetPanel)
        {
            SetPanel();
        }
        else
        {
            SetHUD(1); // rotate clockwise by default
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
            if (Mathf.Abs(overallInjectedRotation) < 180)
            {
                float remainingRotation = redirectionManager.deltaDir > 0 ? 180 - overallInjectedRotation : -180 - overallInjectedRotation; // The idea is that we're gonna keep going in this direction till we reach objective
                if (Mathf.Abs(remainingRotation) < Mathf.Abs(redirectionManager.deltaDir) || requiredRotateAngle == 0)
                {
                    InjectRotation(remainingRotation);
                    redirectionManager.OnResetEnd();
                    overallInjectedRotation += remainingRotation;
                }
                else
                {
                    InjectRotation(redirectionManager.deltaDir);
                    overallInjectedRotation += redirectionManager.deltaDir;
                }
            }
        }
    }

    //end reset
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

        //finish rotating
        if (rotateAngle >= requiredRotateAngle)
        {
            rotateAngle = requiredRotateAngle;
            requiredRotateAngle = 0;
        }
        else
        {
            requiredRotateAngle -= rotateAngle;
        }
        redirectionManager.simulatedWalker.RotateInPlace(rotateAngle);
    }

}
