using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class WorkerAgent : MonoBehaviour
{
    Transform _reporterTr;
    //Transform _player;
    NavMeshAgent _agent;
    Vector3 _destination;
    Animator _anim;
    float _safetyDistance = 4f;
    bool _EndReporting = false;

    public enum State
    {
        Idle,
        Run,
        Tablet
    }

    State state = State.Idle;

    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _anim = GetComponent<Animator>();
        _reporterTr = transform;
        //_player = GameObject.FindWithTag("Player").transform;
        //StartCoroutine(CheckAndMovePlayerTr(_player.transform));
        //StartCoroutine(CheckAnimator());
    }

    public IEnumerator CheckAndMovePlayerTr(Transform Destination)
    {
        while (!_EndReporting)
        {
            //Debug.Log(state);
            yield return new WaitForSeconds(0.3f);
            float _distance = Vector3.Distance(Destination.position, _reporterTr.position);
            _destination = new Vector3(Destination.position.x, Destination.position.y, Destination.position.z - _safetyDistance);
            if (Mathf.RoundToInt(_distance) > _safetyDistance)
            {
                state = State.Run;
                _agent.isStopped = false;
                _agent.destination = _destination;
                transform.LookAt(Destination); 
            }
            else if (Mathf.RoundToInt(_distance) <= _safetyDistance)
            {
                state = State.Tablet;
                _agent.isStopped = true;
                //transform.LookAt(Destination);
            }
        }
    }

    public IEnumerator CheckAnimator()
    {
        while (!_EndReporting)
        {
            yield return new WaitForSeconds(0.3f);

            switch (state)
            {
                case State.Idle:
                    _anim.SetBool("IsRunning", false);
                    _anim.SetBool("UsingTablet", false);
                    break;
                case State.Run:
                    _anim.SetBool("IsRunning", true);
                    _anim.SetBool("UsingTablet", false);
                    break;
                case State.Tablet:
                    _anim.SetBool("UsingTablet", true);
                    _anim.SetBool("IsRunning", false);
                    break;
            }
        }
    }
}
