using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VECollisionController : MonoBehaviour
{

    public GameObject followedTarget;
    public GlobalConfiguration globalConfiguration;
    public RedirectionManager redirectionManager;
    public float distanceMultiplier = 2f;

    private Vector3 normal;
    private float verticalDis;
    private bool isInside;
    // Start is called before the first frame update
    void Start()
    {
        globalConfiguration = GameObject.FindObjectOfType<GlobalConfiguration>();
    }

    // Update is called once per frame
    void Update()
    {
        if (followedTarget && !followedTarget.activeInHierarchy)
        {
            Destroy(this.gameObject);
        }
        if (followedTarget)
        {
            this.transform.position = followedTarget.transform.position;//jon: we move the collider to follow the avatar, to avoid some physical problems caused by bouding the collider directly to the avatar
            if (isInside)
            {
                verticalDis = Vector3.Dot(redirectionManager.deltaPos, normal);
                globalConfiguration.virtualWorld.transform.position = globalConfiguration.virtualWorld.transform.position + normal * verticalDis* distanceMultiplier;
            }
        
        }


    }

    private void OnCollisionEnter(Collision collision)
    {
        Transform trans = collision.transform;
        while (trans.parent != null)
        {
            if (trans.parent.gameObject == globalConfiguration.virtualWorld)
            {
                normal = collision.contacts[0].normal;
                verticalDis = Vector3.Dot(redirectionManager.deltaPos, normal);
                globalConfiguration.virtualWorld.transform.position = globalConfiguration.virtualWorld.transform.position + normal * verticalDis;
                isInside = true;
                break;
            }
            else
            {
                trans = trans.parent;
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        Transform trans = collision.transform;
        while (trans.parent != null)
        {
            if (trans.parent.gameObject == globalConfiguration.virtualWorld)
            {
                isInside = false;
                break;
            }
            else
            {
                trans = trans.parent;
            }
        }
    }
}
