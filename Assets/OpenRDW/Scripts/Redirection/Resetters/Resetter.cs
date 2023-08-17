using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public abstract class Resetter : MonoBehaviour
{
    private static float toleranceAngleError = 1;//Allowable angular error to prevent jamming
    [HideInInspector]
    public GlobalConfiguration globalConfiguration;
    [HideInInspector]
    public RedirectionManager redirectionManager;

    [HideInInspector]
    public MovementManager movementManager;
    [HideInInspector]
    public VisualizationManager visualizationManager;

    //spin in place hint
    public Transform prefabHUD = null;

    public Transform instanceHUD;

    private GameObject resetPanel; // a user-friendly panel
    private Transform arrow;
    private Transform arrowMap;
    private Text resetMsg; // the hint message for a better reset
    private Texture orangePanelTexture;
    [HideInInspector]
    public Vector2 targetPos; // the target position we want user to be at when the reset ends
    [HideInInspector]
    public Vector2 targetDir; // the target direction we want user to face when the reset ends

    private void Awake()
    {
        redirectionManager = GetComponent<RedirectionManager>();
        movementManager = GetComponent<MovementManager>();
        targetDir = new Vector2(1, 0);
        targetPos = Vector2.zero;
    }

    // Manually update arrow's position and direction and perform the endreset test
    public void UpdatePanel()
    {
        if (orangePanelTexture == null)
        {
            orangePanelTexture = Resources.Load<Texture>("OPR_Round");
        }
        if (resetPanel == null)
        {
            Transform root = transform.Find("Simulated Avatar").Find("Head");
            resetPanel = root.Find("ResetPanel").gameObject;
            resetPanel.active = true;
            resetPanel.transform.parent = redirectionManager.headTransform;
            resetMsg = resetPanel.transform.Find("Text").GetComponent<Text>();
            arrow = resetPanel.transform.Find("Arrow").transform;
            arrowMap = resetPanel.transform.Find("ArrMap").transform;
            arrowMap.GetComponent<RawImage>().texture = orangePanelTexture;
            resetMsg.text = "Please align the black arrow\nwith the white arrow";
            resetPanel.transform.localPosition = new Vector3(0, 0, 100000f);
            resetPanel.transform.localEulerAngles = new Vector3(0, 0, 0);
        }
        if (redirectionManager.inReset)
        {
            resetPanel.transform.localPosition = new Vector3(0, 0, 0.5f);
            Vector2 realPos = Utilities.FlattenedPos2D(redirectionManager.currPosReal);
            Vector2 realDir = Utilities.FlattenedDir2D(redirectionManager.currDirReal);
            Vector2 distanceVector = realPos - targetPos;
            arrow.localPosition = new Vector3((distanceVector.x * targetDir.y - distanceVector.y * targetDir.x) * 60f, 50f + (distanceVector.x * targetDir.x + distanceVector.y * targetDir.y) * 60f, 0);
            float deltaAngle = Utilities.GetSignedAngle(realDir, targetDir);
            arrow.localRotation = Quaternion.Euler(0, 0, deltaAngle);
            if (distanceVector.magnitude < 0.2f &&
                arrow.localRotation.eulerAngles.magnitude < 8.0f)
            { // both position and direction satisfy the requirements, end reset
                redirectionManager.OnResetEnd();
            }
        }
    }

    /// <summary>
    /// Function called when reset trigger is signalled, to see if resetter believes resetting is necessary.
    /// </summary>
    /// <returns></returns>
    public abstract bool IsResetRequired();

    public abstract void InitializeReset();

    public abstract void InjectResetting();

    public abstract void EndReset();

    //manipulation when update every reset
    public abstract void SimulatedWalkerUpdate();


    //rotate physical plane clockwise
    public void InjectRotation(float rotationInDegrees)
    {
        transform.RotateAround(Utilities.FlattenedPos3D(redirectionManager.headTransform.position), Vector3.up, rotationInDegrees);
        GetComponentInChildren<KeyboardController>().SetLastRotation(rotationInDegrees);
    }

    // translate
    protected void InjectTranslation(Vector3 translation)
    {
        if (translation.magnitude > 0)
        {
            transform.Translate(translation, Space.World);
        }
    }

    public void Initialize()
    {
        if (globalConfiguration == null)
        {
            globalConfiguration = GetComponentInParent<GlobalConfiguration>();
        }
        if (visualizationManager == null)
        {
            visualizationManager = GetComponent<VisualizationManager>();
        }
    }

    public float GetDistanceToCenter()
    {
        return redirectionManager.currPosReal.magnitude;
    }

    public bool IfCollisionHappens()
    {
        var realPos = new Vector2(redirectionManager.currPosReal.x, redirectionManager.currPosReal.z);
        var realDir = new Vector2(redirectionManager.currDirReal.x, redirectionManager.currDirReal.z).normalized;
        var userGameobjects = globalConfiguration.redirectedAvatars;

        bool ifCollisionHappens = false;
        foreach (var space in globalConfiguration.physicalSpaces)
        {
            for (int i = 0; i < space.trackingSpace.Count; i++)
            {
                var p = space.trackingSpace[i];
                var q = space.trackingSpace[(i + 1) % space.trackingSpace.Count];

                //judge vertices of polygons
                if (IfCollideWithPoint(realPos, realDir, p))
                {
                    ifCollisionHappens = true;
                    break;
                }

                //judge edge collision
                if (Vector3.Cross(q - p, realPos - p).magnitude / (q - p).magnitude <= redirectionManager.globalConfiguration.RESET_TRIGGER_BUFFER//distance
                    && Vector2.Dot(q - p, realPos - p) >= 0 && Vector2.Dot(p - q, realPos - q) >= 0//range
                    )
                {
                    //if collide with border
                    if (Mathf.Abs(Cross(q - p, realDir)) > 1e-3 && Mathf.Sign(Cross(q - p, realDir)) != Mathf.Sign(Cross(q - p, realPos - p)))
                    {
                        ifCollisionHappens = true;
                        break;
                    }
                }
            }
            for (int i = 0; i < space.obstaclePolygons.Count; i++)
            {
                for (int j = 0; j < space.obstaclePolygons[i].Count; j++)
                {
                    var p = space.obstaclePolygons[i][j];
                    var q = space.obstaclePolygons[i][(j + 1) % space.obstaclePolygons[i].Count];

                    //judge vertices of polygons
                    if (IfCollideWithPoint(realPos, realDir, p))
                    {
                        ifCollisionHappens = true;
                        break;
                    }

                    //judge edge collision
                    if (Vector3.Cross(q - p, realPos - p).magnitude / (q - p).magnitude <= redirectionManager.globalConfiguration.RESET_TRIGGER_BUFFER//distance
                        && Vector2.Dot(q - p, realPos - p) >= 0 && Vector2.Dot(p - q, realPos - q) >= 0//range
                        )
                    {
                        //if collide with border
                        if (Mathf.Abs(Cross(q - p, realDir)) > 1e-3 && Mathf.Sign(Cross(q - p, realDir)) != Mathf.Sign(Cross(q - p, realPos - p)))
                        {
                            ifCollisionHappens = true;
                            break;
                        }
                    }
                }
            }
        }

        if (!ifCollisionHappens)
        {//if collide with other avatars
            foreach (var us in userGameobjects)
            {
                //ignore self
                if (us.Equals(gameObject))
                    continue;
                //collide with other avatars
                if (IfCollideWithPoint(realPos, realDir, Utilities.FlattenedPos2D(us.GetComponent<RedirectionManager>().currPosReal)))
                {
                    ifCollisionHappens = true;
                    break;
                }
            }
        }

        return ifCollisionHappens;
    }

    //if collide with vertices
    public bool IfCollideWithPoint(Vector2 realPos, Vector2 realDir, Vector2 obstaclePoint)
    {
        //judge point, if the avatar will walks into a circle obstacle
        var pointAngle = Vector2.Angle(obstaclePoint - realPos, realDir);
        return (obstaclePoint - realPos).magnitude <= globalConfiguration.RESET_TRIGGER_BUFFER && pointAngle < 90 - toleranceAngleError;
    }
    private float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }


    // initialize spin in place hint, rotateDir==1:rotate clockwise, otherwise, rotate counter clockwise
    public void SetHUD(int rotateDir)
    {
        if (prefabHUD == null)
            prefabHUD = Resources.Load<Transform>("Resetter HUD");

        if (visualizationManager.ifVisible)
        {
            instanceHUD = Instantiate(prefabHUD);
            instanceHUD.parent = redirectionManager.headTransform;
            instanceHUD.localPosition = instanceHUD.position;
            instanceHUD.localRotation = instanceHUD.rotation;

            //rotate clockwise
            if (rotateDir == 1)
            {
                instanceHUD.GetComponent<TextMesh>().text = "Spin in Place\n→";
            }
            else
            {
                instanceHUD.GetComponent<TextMesh>().text = "Spin in Place\n←";
            }
        }
    }

    // set the  user-friendly hint panel
    public void SetPanel()
    {
        if (resetPanel != null)
        {
            resetPanel.transform.localPosition = new Vector3(0, 0, 0.5f);
        }
    }

    // we don't actually destroy the panel, instead we only move it to a faraway place
    public void DestroyPanel()
    {
        if (resetPanel != null)
        {
            resetPanel.transform.localPosition = new Vector3(0, 0, 100000f);
        }
    }

    // decide the actual reset position, which doesn't need to be the same with user's current position
    // a safer position could reduce possible resets in a live-user experiment
    public Vector2 DecideResetPosition(Vector2 currPosReal)
    {
        return currPosReal;
    }

    // destroy HUD object
    public void DestroyHUD()
    {
        if (instanceHUD != null)
            Destroy(instanceHUD.gameObject);
    }
}
