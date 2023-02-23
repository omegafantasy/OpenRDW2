using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InitialPose
{
    public Vector2 initialPosition;
    public Vector2 initialForward;
    public InitialPose(Vector2 initialPosition, Vector2 initialForward)
    {
        this.initialPosition = initialPosition;
        this.initialForward = initialForward.normalized;
    }
    public InitialPose(bool isRandom) // For Creating Random Configuration or just default of center/up
    {
        this.initialPosition = Vector2.zero;
        this.initialForward = Vector2.up;
    }
    public static InitialPose GetDefaultInitialPose() {
        return new InitialPose(Vector2.zero, Vector2.up);
    }
    public static InitialPose Copy(InitialPose initialPose) {
        return new InitialPose(initialPose.initialPosition, initialPose.initialForward);
    }
}
