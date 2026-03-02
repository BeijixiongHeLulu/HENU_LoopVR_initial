using System;
using System.Collections;
using System.Collections.Generic;
using RoboRyanTron.Unite2017.Variables;
using UnityEngine;
using Object = System.Object;

public class ResetObjectPositionTrigger : MonoBehaviour
{

    [Tooltip("The point where the object will get respawned to after hitting an obstacle.")]
    [SerializeField] private GameObject respawnPointObstacleHit;
    
    [Tooltip("The point where the object will get respawned to exceeding the allowed number of trials.")]
    [SerializeField] private GameObject respawnPointTrialFailed;
  
    [SerializeField] private FloatVariable maxTrials;
    [SerializeField] private FloatVariable trialsDone;
   
    private Transform _resetPosition;

    [SerializeField] private FloatVariable timeToWait;

    

    private void Start()
    {
        _resetPosition = respawnPointObstacleHit.transform;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.GetComponent<CarController>())
        {
            if (trialsDone.Value >= maxTrials.Value)
            {
                TrainingHandler.Instance.GoToMainExperiment();
                _resetPosition = respawnPointTrialFailed.transform;
            }

            ResetCar(other.gameObject);
            trialsDone.ApplyChange(1);

            // --- [新增] 车辆被强制传送到原点，发出 Marker 12 ---
            if (SyncManager.Instance != null)
            {
                SyncManager.Instance.TriggerEvent(12);
            }

            StartCoroutine(TakeAwayControl(other));
        }
    }

    private void ResetCar(GameObject objectToReset)
    {
        // 1. 物理层深度清洗 (清理轮胎打滑和角速度残留)
        objectToReset.GetComponent<CarController>().TurnOffEngine();
        Rigidbody rb = objectToReset.GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero; // 加上这句，防止带着旋转惯性传送

        objectToReset.transform.SetPositionAndRotation(_resetPosition.position, _resetPosition.rotation);

        // 2. AI 脑部清洗
        AIController ai = objectToReset.GetComponent<AIController>();
        if (ai != null)
        {
            // 强迫 AI 重新扫描脚下最近的贝塞尔曲线点，而不是记忆里的那个点
            ai.SetLocalTargetAndCurveDetection();
        }

        // 3. 速度限制清洗 (请确保这里的数字是你刚起步时的期望速度)
        AimedSpeed aimed = objectToReset.GetComponent<AimedSpeed>();
        if (aimed != null)
        {
            aimed.SetRuleSpeed(50f / 3.6f); // 比如重置回 50km/h
        }
    }

    private IEnumerator TakeAwayControl(Collider other)
    {
        
        yield return new WaitForEndOfFrame();
        
        yield return new WaitForSecondsRealtime(timeToWait.Value);

        // other.gameObject.SetActive(true);
        other.gameObject.GetComponent<Rigidbody>().isKinematic = false;
        other.gameObject.GetComponent<CarController>().TurnOnEngine();
        
        //if (trialsDone.Value <= maxTrials.Value)
        //{
        //    //other.gameObject.SetActive(false);
        //
        //    
        //}

    }
}
