using UnityEngine;
using UnityEngine.AI;
using System.Reflection;
using System.Collections.Generic;

public class NPC_AmbulanceDrive : NPC_WaypointDrive
{
    [Header("模式控制")]
    public bool isEmergency = false;
    public float emergencySpeed = 15.0f;
    public float detectRadius = 55f;

    [Header("自動判斷左右路口")]
    public Transform leftIntersectionCenter;
    public Transform rightIntersectionCenter;

    private bool emergencyOnLeftIntersection = true;
    private string emergencyEWState = "G";
    private string emergencyNSState = "R";

    [Header("緊急模式自動恢復")]
    public float emergencyPassDistance = 25f;
    private Transform currentEmergencyIntersection;
    private bool hasEmergencyIntersection = false;

    [Header("自動導航邏輯")]
    public bool useSmartNavigation = true;
    public int nodesToGoStraight = 1;
    private int nodesPassed = 0;

    [Header("特效組件")]
    public GameObject sirenLights;
    public AudioSource sirenAudio;

    private bool lastEmergencyState;

    public bool ambulanceWaitingAtRedLight;
    public bool ambulanceFullyStopped;

    protected override void Start()
    {
        base.Start();
        lastEmergencyState = isEmergency;

        if (leftIntersectionCenter == null)
        {
            GameObject obj = GameObject.Find("LeftIntersectionCenter");
            if (obj != null)
                leftIntersectionCenter = obj.transform;
        }

        if (rightIntersectionCenter == null)
        {
            GameObject obj = GameObject.Find("RightIntersectionCenter");
            if (obj != null)
                rightIntersectionCenter = obj.transform;
        }

        UnityEngine.Debug.Log(
            "Auto Find Centers：Left=" +
            (leftIntersectionCenter != null) +
            " Right=" +
            (rightIntersectionCenter != null)
        );
    }

    protected override void Update()
    {
        if (agent == null)
            return;

        HandleStateChange();
        HandleEffects();
        CheckEmergencyPassedIntersection();

        if (useSmartNavigation)
            SmartNavigation();
        else
            base.Update();
    }

    private void SmartNavigation()
    {
        bool shouldStop = false;

        if (!isEmergency)
        {
            if (targetNode != null && targetNode.isStopLine && targetNode.currentIsRed)
            {
                isWaitingAtRedLight = true;
                float dist = Vector3.Distance(transform.position, targetNode.transform.position);

                if (dist < 4.0f)
                {
                    shouldStop = true;
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                    agent.ResetPath();
                    isFullyStopped = true;
                    return;
                }
                else if (dist <= 15f)
                {
                    agent.speed = Mathf.Lerp(0, originalSpeed, dist / 15f);
                    agent.isStopped = false;
                    return;
                }
            }
            else
            {
                if (isWaitingAtRedLight)
                {
                    isWaitingAtRedLight = false;
                    isFullyStopped = false;
                    agent.isStopped = false;
                }
            }

            CheckForwardCollision();

            if (agent.isStopped)
                shouldStop = true;
        }
        else
        {
            agent.isStopped = false;
            NotifyNearbyCars();
            CheckForwardCollisionCustom(4.0f);

            if (agent.isStopped)
                shouldStop = true;
        }

        if (shouldStop)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
        else
        {
            agent.isStopped = false;
        }

        if (!isWaitingAtRedLight && !agent.pathPending && agent.remainingDistance < 2.5f)
        {
            nodesPassed++;

            if (targetNode == null)
                return;

            TrafficNode nextNode = null;
            List<TrafficNode> choices = targetNode.nextNodes;

            if (choices != null && choices.Count > 0)
            {
                if (nodesPassed <= nodesToGoStraight || choices.Count == 1)
                    nextNode = FindBestForwardNode(choices);
                else
                    nextNode = FindMostLeftNode(choices);
            }

            if (nextNode != null)
            {
                targetNode = nextNode;
                agent.SetDestination(targetNode.transform.position);
            }
        }
    }

    private void NotifyNearbyCars()
    {
        IntersectionV2X[] intersections = FindObjectsOfType<IntersectionV2X>();

        foreach (var brain in intersections)
        {
            if (brain == null)
                continue;

            if (Vector3.Distance(transform.position, brain.transform.position) > 70f)
                continue;

            brain.AmbulanceApproach(this);

            AutoSetEmergencyDirection();

            currentEmergencyIntersection = brain.transform;
            hasEmergencyIntersection = true;

            ArduinoTrafficLightAutoSync sync =
                FindObjectOfType<ArduinoTrafficLightAutoSync>();

            if (sync != null)
            {
                if (emergencyOnLeftIntersection)
                    sync.LeftEmergency(emergencyEWState, emergencyNSState);
                else
                    sync.RightEmergency(emergencyEWState, emergencyNSState);
            }
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, detectRadius);

        foreach (var hit in hits)
        {
            GameObject vehicleRoot = hit.transform.root.gameObject;

            if (!vehicleRoot.CompareTag("Car") || vehicleRoot == this.gameObject)
                continue;

            NPC_WaypointDrive npc = vehicleRoot.GetComponent<NPC_WaypointDrive>();

            if (npc != null)
                npc.YieldForAmbulance(this);
        }
    }

    private void AutoSetEmergencyDirection()
    {
        if (leftIntersectionCenter == null || rightIntersectionCenter == null)
        {
            UnityEngine.Debug.LogWarning("找不到 LeftIntersectionCenter 或 RightIntersectionCenter");
            return;
        }

        float leftDist = Vector3.Distance(transform.position, leftIntersectionCenter.position);
        float rightDist = Vector3.Distance(transform.position, rightIntersectionCenter.position);

        emergencyOnLeftIntersection = leftDist < rightDist;

        float x = Mathf.Abs(transform.forward.x);
        float z = Mathf.Abs(transform.forward.z);

        if (x > z)
        {
            emergencyEWState = "G";
            emergencyNSState = "R";
        }
        else
        {
            emergencyEWState = "R";
            emergencyNSState = "G";
        }

        UnityEngine.Debug.Log(
            "自動判斷：左路口=" + emergencyOnLeftIntersection +
            " 左距離=" + leftDist.ToString("F1") +
            " 右距離=" + rightDist.ToString("F1") +
            " EW=" + emergencyEWState +
            " NS=" + emergencyNSState
        );
    }

    private void CheckEmergencyPassedIntersection()
    {
        if (!isEmergency)
            return;

        if (!hasEmergencyIntersection || currentEmergencyIntersection == null)
            return;

        float dist = Vector3.Distance(transform.position, currentEmergencyIntersection.position);

        Vector3 dirToIntersection =
            (currentEmergencyIntersection.position - transform.position).normalized;

        float dot = Vector3.Dot(transform.forward, dirToIntersection);

        if (dist > emergencyPassDistance && dot < 0f)
        {
            isEmergency = false;
            hasEmergencyIntersection = false;
            currentEmergencyIntersection = null;

            ArduinoTrafficLightAutoSync sync =
                FindObjectOfType<ArduinoTrafficLightAutoSync>();

            if (sync != null)
                sync.ClearAllEmergency();
        }
    }

    private TrafficNode FindMostLeftNode(List<TrafficNode> choices)
    {
        TrafficNode bestNode = choices[0];
        float minX = float.MaxValue;

        foreach (var node in choices)
        {
            Vector3 relativePos = transform.InverseTransformPoint(node.transform.position);

            if (relativePos.x < minX)
            {
                minX = relativePos.x;
                bestNode = node;
            }
        }

        return bestNode;
    }

    private TrafficNode FindBestForwardNode(List<TrafficNode> choices)
    {
        TrafficNode bestNode = choices[0];
        float maxDot = -2.0f;

        foreach (var node in choices)
        {
            Vector3 dirToNode =
                (node.transform.position - transform.position).normalized;

            float dot = Vector3.Dot(transform.forward, dirToNode);

            if (dot > maxDot)
            {
                maxDot = dot;
                bestNode = node;
            }
        }

        return bestNode;
    }

    private void HandleStateChange()
    {
        if (isEmergency == lastEmergencyState)
            return;

        agent.ResetPath();

        ArduinoTrafficLightAutoSync sync =
            FindObjectOfType<ArduinoTrafficLightAutoSync>();

        if (!isEmergency)
        {
            agent.speed = originalSpeed;
            agent.acceleration = 8f;
            agent.angularSpeed = 120f;

            isWaitingAtRedLight = false;
            isFullyStopped = false;
            agent.isStopped = false;

            hasEmergencyIntersection = false;
            currentEmergencyIntersection = null;

            if (sync != null)
                sync.ClearAllEmergency();
        }
        else
        {
            agent.speed = emergencySpeed;
            agent.acceleration = 40f;
            agent.angularSpeed = 1000f;

            isWaitingAtRedLight = false;
            isFullyStopped = false;
            agent.isStopped = false;
        }

        if (targetNode != null)
            agent.SetDestination(targetNode.transform.position);

        lastEmergencyState = isEmergency;
    }

    protected void CheckForwardCollisionCustom(float dist)
    {
        RaycastHit hit;

        if (Physics.Raycast(transform.TransformPoint(sensorOffset), transform.forward, out hit, dist))
        {
            if (hit.collider.CompareTag("Car"))
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                return;
            }
        }

        agent.isStopped = false;
    }

    private void HandleEffects()
    {
        if (sirenLights != null && sirenLights.activeSelf != isEmergency)
            sirenLights.SetActive(isEmergency);

        if (isEmergency && sirenLights != null)
        {
            LightManager manager = sirenLights.GetComponent<LightManager>();

            if (manager != null)
            {
                FieldInfo field = typeof(LightManager).GetField(
                    "sirenMode",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                if (field != null)
                    field.SetValue(manager, 2);

                Light[] lights =
                    sirenLights.GetComponentsInChildren<Light>(true);

                foreach (Light l in lights)
                    l.enabled = true;
            }
        }

        if (sirenAudio != null)
        {
            if (isEmergency && !sirenAudio.isPlaying)
                sirenAudio.Play();
            else if (!isEmergency && sirenAudio.isPlaying)
                sirenAudio.Stop();
        }
    }
}