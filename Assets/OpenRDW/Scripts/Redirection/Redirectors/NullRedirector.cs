using UnityEngine;
using System.Collections;

public class NullRedirector : Redirector
{
    public override void InjectRedirection()
    {
        SetTranslationGain(1);
        SetRotationGain(1);
        SetCurvature(0);

        ApplyGains();
    }
}
