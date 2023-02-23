using UnityEngine;
using System.Collections;

public abstract class Redirector : MonoBehaviour
{
    [HideInInspector]
    public GlobalConfiguration globalConfiguration;

    [HideInInspector]
    public RedirectionManager redirectionManager;

    [HideInInspector]
    public MovementManager movementManager;
    [HideInInspector]
    public VisualizationManager visualizationManager;

    void Awake()
    {
        globalConfiguration = GetComponentInParent<GlobalConfiguration>();
        redirectionManager = GetComponent<RedirectionManager>();
        movementManager = GetComponent<MovementManager>();
        visualizationManager = GetComponent<VisualizationManager>();
    }

    /// <summary>
    /// Applies redirection based on the algorithm.
    /// </summary>
    public abstract void InjectRedirection();

    protected void SetTranslationGain(float gt)
    {
        gt = Mathf.Max(gt, globalConfiguration.MIN_TRANS_GAIN);
        gt = Mathf.Min(gt, globalConfiguration.MAX_TRANS_GAIN);
        redirectionManager.gt = gt;
        var translation = redirectionManager.deltaPos * (gt - 1);
        transform.Translate(translation, Space.World);
        globalConfiguration.statisticsLogger.Event_Translation_Gain(movementManager.avatarId, gt, translation);
    }
    protected void SetRotationGain(float gr)
    {
        if (redirectionManager.isRotating)
        {
            gr = Mathf.Max(gr, globalConfiguration.MIN_ROT_GAIN);
            gr = Mathf.Min(gr, globalConfiguration.MAX_ROT_GAIN);
            redirectionManager.gr = gr;
            var rotationInDegrees = redirectionManager.deltaDir * (gr - 1);
            transform.RotateAround(Utilities.FlattenedPos3D(redirectionManager.headTransform.position), Vector3.up, rotationInDegrees);
            GetComponentInChildren<KeyboardController>().SetLastRotation(rotationInDegrees);
            globalConfiguration.statisticsLogger.Event_Rotation_Gain(movementManager.avatarId, gr, rotationInDegrees);
        }
        else
        {
            redirectionManager.gr = 1;
        }
    }
    protected void SetCurvature(float curvature)// positive means turning left
    {
        if (redirectionManager.isWalking)
        {
            curvature = Mathf.Max(curvature, -1 / globalConfiguration.CURVATURE_RADIUS);
            curvature = Mathf.Min(curvature, 1 / globalConfiguration.CURVATURE_RADIUS);
            redirectionManager.curvature = curvature;
            var rotationInDegrees = Mathf.Rad2Deg * redirectionManager.deltaPos.magnitude * curvature;
            transform.RotateAround(Utilities.FlattenedPos3D(redirectionManager.headTransform.position), Vector3.up, rotationInDegrees);
            GetComponentInChildren<KeyboardController>().SetLastRotation(rotationInDegrees);
            globalConfiguration.statisticsLogger.Event_Curvature_Gain(movementManager.avatarId, curvature, rotationInDegrees);
        }
        else
        {
            redirectionManager.curvature = 0;
        }
    }

    public virtual void GetPriority()
    { }
}
