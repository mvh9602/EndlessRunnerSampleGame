﻿using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using FMOD.Studio;

public class PatrollingObstacle : Obstacle
{
	static int s_SpeedRatioHash = Animator.StringToHash("SpeedRatio");
	static int s_DeadHash = Animator.StringToHash("Dead"); 

	[Tooltip("Minimum time to cross all lanes.")]
    public float minTime = 2f;
    [Tooltip("Maximum time to cross all lanes.")]
    public float maxTime = 5f;
	[Tooltip("Leave empty if no animation")]
	public Animator animator;

	public AudioClip[] patrollingSound;
    public string patrollingEventPath;
    public string distanceParamName;

    private EventInstance patrollingEventRef;
    private PARAMETER_ID distanceParamID;
    private PLAYBACK_STATE patrollPlayback;
    //private ParameterInstance distanceParamRef;
    private GameObject player;

	protected TrackSegment m_Segement;

	protected Vector3 m_OriginalPosition = Vector3.zero;
	protected float m_MaxSpeed;
	protected float m_CurrentPos;

	protected AudioSource m_Audio;
    private bool m_isMoving = false;

    protected const float k_LaneOffsetToFullWidth = 2f;

	public override IEnumerator Spawn(TrackSegment segment, float t)
	{
		Vector3 position;
		Quaternion rotation;
		segment.GetPointAt(t, out position, out rotation);
	    AsyncOperationHandle op = Addressables.InstantiateAsync(gameObject.name, position, rotation);
	    yield return op;
	    if (op.Result == null || !(op.Result is GameObject))
	    {
	        Debug.LogWarning(string.Format("Unable to load obstacle {0}.", gameObject.name));
	        yield break;
	    }
        GameObject obj = op.Result as GameObject;

        obj.transform.SetParent(segment.objectRoot, true);

        PatrollingObstacle po = obj.GetComponent<PatrollingObstacle>();
        po.m_Segement = segment;

        //TODO : remove that hack related to #issue7
        Vector3 oldPos = obj.transform.position;
        obj.transform.position += Vector3.back;
        obj.transform.position = oldPos;

        po.Setup();
    }

    public override void Setup()
	{
		m_Audio = GetComponent<AudioSource>();
		if(m_Audio != null && patrollingSound != null && patrollingSound.Length > 0)
		{
			m_Audio.loop = true;
			m_Audio.clip = patrollingSound[Random.Range(0,patrollingSound.Length)];
			m_Audio.Play();
		}

        patrollingEventRef = FMODUnity.RuntimeManager.CreateInstance(patrollingEventPath);
        //patrollingEventRef.getParameter(distanceParamPath, out distanceParamRef);
        //patrollingEventRef.start();
        //patrollingEventRef.release();

        // This is annoying
        /*
        EventDescription patrollingEventDesc;
        patrollingEventRef.getDescription(out patrollingEventDesc);
        PARAMETER_DESCRIPTION distanceParamDesc;
        patrollingEventDesc.getParameterDescriptionByName(distanceParamName, out distanceParamDesc);
        distanceParamID = distanceParamDesc.id;
        */

        player = GameObject.FindGameObjectWithTag("Player");

        m_OriginalPosition = transform.localPosition + transform.right * m_Segement.manager.laneOffset;
		transform.localPosition = m_OriginalPosition;

		float actualTime = Random.Range(minTime, maxTime);

        //time 2, becaus ethe animation is a back & forth, so we need the speed needed to do 4 lanes offset in the given time
        m_MaxSpeed = (m_Segement.manager.laneOffset * k_LaneOffsetToFullWidth * 2) / actualTime;

		if (animator != null)
		{
			AnimationClip clip = animator.GetCurrentAnimatorClipInfo(0)[0].clip;
            animator.SetFloat(s_SpeedRatioHash, clip.length / actualTime);
		}

	    m_isMoving = true;
	}

	public override void Impacted()
	{
	    m_isMoving = false;
        patrollingEventRef.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        base.Impacted();

		if (animator != null)
		{
			animator.SetTrigger(s_DeadHash);
		}
	}

	void Update()
	{
		if (!m_isMoving)
			return;
        patrollingEventRef.getPlaybackState(out patrollPlayback);
        float distanceToPlayer = Vector3.Magnitude(transform.position - player.transform.position);
        if(distanceToPlayer < 10f && distanceToPlayer > -10f)
        {
            if(patrollPlayback == PLAYBACK_STATE.STOPPED)
            {
                patrollingEventRef.start();
                patrollingEventRef.release();
            }
            //patrollingEventRef.setParameterByID(distanceParamID, 1f - (distanceToPlayer / 5));
            patrollingEventRef.setParameterByName(distanceParamName, 0.5f + 0.5f *(Mathf.Abs(distanceToPlayer) / 10f));
        }
        else
        {
            //patrollingEventRef.setParameterByID(distanceParamID, 0f);
            patrollingEventRef.setParameterByName(distanceParamName, 0f);
        }

		m_CurrentPos += Time.deltaTime * m_MaxSpeed;

        transform.localPosition = m_OriginalPosition - transform.right * Mathf.PingPong(m_CurrentPos, m_Segement.manager.laneOffset * k_LaneOffsetToFullWidth);
	}
}
