using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using PathSeedChoice = GlobalConfiguration.PathSeedChoice;
using AvatarInfo = ExperimentSetup.AvatarInfo;

public class VisualizationManager : MonoBehaviour
{
    [HideInInspector]
    public GlobalConfiguration generalManager;

    [HideInInspector]
    public RedirectionManager redirectionManager;
    [HideInInspector]
    public MovementManager movementManager;
    //if this avatar is visible
    [HideInInspector]
    public bool ifVisible;
    [HideInInspector]
    public Camera cameraTopReal; // stay still relative to the physical space
    [HideInInspector]
    public HeadFollower headFollower;//headFollower
    private List<Transform> obstacleParents;
    private List<Transform> bufferParents;
    [HideInInspector]
    public List<GameObject> bufferRepresentations; // buffer gameobjects

    [HideInInspector]
    public List<GameObject> avatarBufferRepresentations; // gameobject of avatar Buffer, index represents the buffer of the avatar with avatarId
    [HideInInspector]
    public List<Transform> otherAvatarRepresentations;//Other avatars' representations

    private GlobalConfiguration globalConfiguration;
    [HideInInspector]
    public List<GameObject> allPlanes; // now we have more than one tracking space
    [HideInInspector]
    public Transform realWaypoint; // the waypoint in presentation in physical space

    [Header("target line")]
    public bool drawTargetLine;//jon: whether a line should be drawn between the avatar and its current target point
    public Color targetLineColor;
    public float targetLineWidth = 0.5f;
    [HideInInspector]
    public LineRenderer targetLine;

    void Awake()
    {
        ifVisible = true;
        generalManager = GetComponentInParent<GlobalConfiguration>();
        redirectionManager = GetComponent<RedirectionManager>();
        movementManager = GetComponent<MovementManager>();

        cameraTopReal = transform.Find("Real Top View Cam").GetComponent<Camera>();
        headFollower = transform.Find("Body").GetComponent<HeadFollower>();

        obstacleParents = new List<Transform>();
        bufferParents = new List<Transform>();

        bufferRepresentations = new List<GameObject>();
        avatarBufferRepresentations = new List<GameObject>();
        allPlanes = new List<GameObject>();

        if (drawTargetLine)
        {
            if (transform.Find("Target Line") == null)
            {
                GameObject obj = new GameObject("Target Line");
                obj.layer = LayerMask.NameToLayer("Virtual");
                targetLine = obj.AddComponent<LineRenderer>();
                obj.transform.parent = transform;
                Material lineMaterial = new Material(Shader.Find("Standard"));
                lineMaterial.color = targetLineColor;
                targetLine.material = lineMaterial;
                targetLine.widthMultiplier = targetLineWidth;
            }
            else
            {
                targetLine = transform.Find("Target Line").GetComponent<LineRenderer>();
            }
        }
        else
        {
            targetLine = null;
        }
    }

    public void SetRealTargetVisibility(bool visible)
    {
        realWaypoint.GetComponent<MeshRenderer>().enabled = visible;
    }

    public void SetVisibilityInVirtual(bool ifVisible)
    {
        this.ifVisible = ifVisible;
        foreach (var mr in GetComponentsInChildren<MeshRenderer>(true))
        {
            mr.enabled = false;
        }

        foreach (var mr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            mr.enabled = false;
        }
        foreach (var mr in transform.Find("Body").GetComponentsInChildren<MeshRenderer>(true))
        {
            mr.enabled = ifVisible;
        }

        foreach (var mr in transform.Find("Body").GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            mr.enabled = ifVisible;
        }

        if (targetLine != null)
            targetLine.enabled = false;
        //if waypoint visible
        if (redirectionManager.targetWaypoint != null)
        {
            if (generalManager.useCrystalWaypoint)
            {
                redirectionManager.targetWaypoint.gameObject.SetActive(ifVisible);
            }
            else
            {
                redirectionManager.targetWaypoint.GetComponent<MeshRenderer>().enabled = ifVisible;
            }
        }
        //if camera is working
        foreach (var cam in GetComponentsInChildren<Camera>())
        {
            cam.enabled = false;
        }
    }

    //set avatar's visibility (avatar, waypoint...)
    public void SetVisibility(bool ifVisible)
    {
        this.ifVisible = ifVisible;

        foreach (var mr in GetComponentsInChildren<MeshRenderer>(true))
        {
            mr.enabled = ifVisible;
        }

        foreach (var mr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            mr.enabled = ifVisible;
        }

        if (targetLine != null)
            targetLine.enabled = ifVisible; // target line

        //if waypoint visible
        if (redirectionManager.targetWaypoint != null)
        {
            if (generalManager.useCrystalWaypoint)
            {
                redirectionManager.targetWaypoint.gameObject.SetActive(ifVisible);
            }
            else
            {
                redirectionManager.targetWaypoint.GetComponent<MeshRenderer>().enabled = ifVisible;
            }
        }

        //if camera is working
        foreach (var cam in GetComponentsInChildren<Camera>())
        {
            cam.enabled = ifVisible;
        }
    }

    public void SwitchPersonView(bool ifFirstPersonView)
    {
        redirectionManager.simulatedHead.Find("1st Person View").gameObject.SetActive(ifFirstPersonView);
        redirectionManager.simulatedHead.Find("3rd Person View").gameObject.SetActive(!ifFirstPersonView);
    }
    public void ChangeTrackingSpaceVisibility(bool ifVisible)
    {
        if (allPlanes != null)
        {
            foreach (var trackingSpace in allPlanes)
            {
                trackingSpace.SetActive(ifVisible);
            }
        }
    }
    public void ChangeColor(Color newColor)
    {
        transform.Find("Body").GetComponent<HeadFollower>().ChangeColor(newColor);
    }

    public void DestroyAll()
    {
        foreach (var plane in allPlanes)
        {
            Destroy(plane);
        }
        foreach (var otherAvatar in otherAvatarRepresentations)
        {
            Destroy(otherAvatar.gameObject);
        }
    }

    public void InitializeOtherAvatarRepresentations()
    {
        //initialize other avatars' representations
        avatarBufferRepresentations = new List<GameObject>();
        otherAvatarRepresentations = new List<Transform>();
        for (int i = 0; i < generalManager.redirectedAvatars.Count; i++)
        {
            var representation = generalManager.CreateAvatar(transform, i, true);

            otherAvatarRepresentations.Add(representation.transform);
            var avatarColor = generalManager.avatarColors[i];
            foreach (var mr in representation.GetComponentsInChildren<MeshRenderer>())
            {
                mr.material = new Material(Shader.Find("Standard"));
                mr.material.color = avatarColor;
            }
            foreach (var mr in representation.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                mr.material = new Material(Shader.Find("Standard"));
                mr.material.color = avatarColor;
            }

            //visualize buffer
            var physicalSpaceIndex = generalManager.redirectedAvatars[i].GetComponent<MovementManager>().physicalSpaceIndex;
            var bufferMesh = TrackingSpaceGenerator.GenerateBufferMesh(new List<Vector2> { Vector2.zero }, false, generalManager.RESET_TRIGGER_BUFFER);
            var obj = AddAvatarBufferMesh(bufferMesh, bufferParents[physicalSpaceIndex], representation.transform);
            //hide
            if (i == movementManager.avatarId)
            {
                representation.SetActive(false);
                obj.SetActive(false);
            }
            avatarBufferRepresentations.Add(obj);
        }
    }

    public void GenerateVirtualSpaceMesh(SingleSpace virtualSpace)
    {
        // plane
        var trackingSpaceMesh = TrackingSpaceGenerator.GeneratePolygonMesh(virtualSpace.trackingSpace);
        var virtualPlane = new GameObject("VirtualPlane");
        virtualPlane.transform.SetParent(transform.parent);
        virtualPlane.transform.localPosition = Vector3.zero;
        virtualPlane.transform.rotation = Quaternion.identity;
        virtualPlane.AddComponent<MeshFilter>().mesh = trackingSpaceMesh;
        var planeMr = virtualPlane.AddComponent<MeshRenderer>();
        planeMr.material = new Material(generalManager.trackingSpacePlaneMat);

        // obstacle
        var obstacleParent = new GameObject().transform;
        obstacleParent.SetParent(virtualPlane.transform);
        obstacleParent.name = "VirtualObstacle";
        obstacleParent.localPosition = new Vector3(0, GlobalConfiguration.obstacleParentHeight, 0);
        obstacleParent.rotation = Quaternion.identity;
        TrackingSpaceGenerator.GenerateObstacleMesh(virtualSpace.obstaclePolygons, obstacleParent, generalManager.virtualObstacleColor, generalManager.if3dObstacle, generalManager.obstacleHeight);
    }

    public void GenerateTrackingSpaceMesh(List<SingleSpace> physicalSpaces)
    {
        allPlanes = new List<GameObject>();
        obstacleParents = new List<Transform>();
        bufferParents = new List<Transform>();
        bufferRepresentations = new List<GameObject>();

        for (int i = 0; i < physicalSpaces.Count; i++)
        {
            var space = physicalSpaces[i];

            // plane
            var trackingSpaceMesh = TrackingSpaceGenerator.GeneratePolygonMesh(space.trackingSpace);
            var newTrackingSpace = new GameObject("Plane" + allPlanes.Count);
            if (movementManager.physicalSpaceIndex == allPlanes.Count)
            {
                redirectionManager.trackingSpace = newTrackingSpace.transform;
            }
            newTrackingSpace.transform.SetParent(transform);
            newTrackingSpace.transform.localPosition = Vector3.zero;
            newTrackingSpace.transform.rotation = Quaternion.identity;
            newTrackingSpace.AddComponent<MeshFilter>().mesh = trackingSpaceMesh;
            var planeMr = newTrackingSpace.AddComponent<MeshRenderer>();
            planeMr.material = new Material(generalManager.trackingSpacePlaneMat);
            allPlanes.Add(newTrackingSpace);

            // obstacle
            var obstacleParent = new GameObject().transform;
            obstacleParent.SetParent(allPlanes[i].transform);
            obstacleParent.name = "ObstacleParent";
            obstacleParent.localPosition = new Vector3(0, GlobalConfiguration.obstacleParentHeight, 0);
            obstacleParent.rotation = Quaternion.identity;
            obstacleParents.Add(obstacleParent);
            TrackingSpaceGenerator.GenerateObstacleMesh(space.obstaclePolygons, obstacleParent, generalManager.obstacleColor, generalManager.if3dObstacle, generalManager.obstacleHeight);

            // buffer
            var bufferParent = new GameObject().transform;
            bufferParent.SetParent(allPlanes[i].transform);
            bufferParent.name = "BufferParent";
            bufferParent.localPosition = new Vector3(0, GlobalConfiguration.bufferParentHeight, 0);
            bufferParent.rotation = Quaternion.identity;
            bufferParents.Add(bufferParent);

            var trackingSpaceBufferMesh = TrackingSpaceGenerator.GenerateBufferMesh(space.trackingSpace, true, generalManager.RESET_TRIGGER_BUFFER);
            AddBufferMesh(trackingSpaceBufferMesh, bufferParent);
            foreach (var obstaclePoints in space.obstaclePolygons)
            {
                var obstacleBufferMesh = TrackingSpaceGenerator.GenerateBufferMesh(obstaclePoints, false, generalManager.RESET_TRIGGER_BUFFER);
                AddBufferMesh(obstacleBufferMesh, bufferParent);
            }
        }
    }

    public GameObject AddBufferMesh(Mesh bufferMesh, Transform bufferParent)
    {
        var obj = new GameObject("bufferMesh" + bufferRepresentations.Count);
        obj.transform.SetParent(bufferParent);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.rotation = Quaternion.identity;

        obj.AddComponent<MeshFilter>().mesh = bufferMesh;
        var mr = obj.AddComponent<MeshRenderer>();
        mr.material = new Material(generalManager.transparentMat);
        mr.material.color = generalManager.bufferColor;

        bufferRepresentations.Add(obj);
        return obj;
    }
    public GameObject AddAvatarBufferMesh(Mesh bufferMesh, Transform bufferParent, Transform followedObj)
    {
        var obj = AddBufferMesh(bufferMesh, bufferParent);
        var hf = obj.AddComponent<HorizontalFollower>();
        hf.followedObj = followedObj;
        return obj;
    }
    //visualization relative, update other avatar representations...
    public void UpdateVisualizations()
    {
        //update avatar
        headFollower.UpdateManually();
        //update trail   
        redirectionManager.trailDrawer.UpdateManually();
        for (int i = 0; i < otherAvatarRepresentations.Count; i++)
        {
            if (i == movementManager.avatarId)
                continue;
            var us = generalManager.redirectedAvatars[i];
            var rm = us.GetComponent<RedirectionManager>();
            otherAvatarRepresentations[i].localPosition = rm.currPosReal;
            otherAvatarRepresentations[i].localRotation = Quaternion.LookRotation(rm.currDirReal, Vector3.up);
        }

        if (drawTargetLine)
        {
            targetLine.SetPosition(0, headFollower.transform.position + new Vector3(0, 0.01f, 0));
            targetLine.SetPosition(1, redirectionManager.targetWaypoint.position + new Vector3(0, 0.01f, 0));
        }
    }

    public void SetBufferVisibility(bool ifVisible)
    {
        for (int i = 0; i < bufferRepresentations.Count; i++)
        {
            bufferRepresentations[i].SetActive(ifVisible);
        }
        avatarBufferRepresentations[movementManager.avatarId].SetActive(false);
    }

    public void Initialize(int avatarId)
    {
        InitializeOtherAvatarRepresentations();
        headFollower.CreateAvatarViualization();
        var avatarColors = generalManager.avatarColors;
        ChangeColor(avatarColors[avatarId % avatarColors.Length]);
    }
}
