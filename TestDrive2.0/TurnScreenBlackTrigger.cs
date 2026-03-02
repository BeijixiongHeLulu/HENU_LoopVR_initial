using System;
using System.Collections;
using System.Collections.Generic;
using RoboRyanTron.Unite2017.Variables;
using UnityEngine;

public class TurnScreenBlackTrigger : MonoBehaviour
{
    [SerializeField] private FloatVariable timeToWait;
    
    [SerializeField] private FloatVariable maxTrials;
    [SerializeField] private FloatVariable trialsDone;

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<CarController>())
        {
            StartCoroutine(TurnScreenBlack());

            // ==========================================================
            // --- [新增] 车辆触碰碰撞区，发送碰撞 Marker 13 ---
            // ==========================================================
            if (SyncManager.Instance != null)
            {
                // 抓取撞车瞬间的速度 (m/s 转 km/h)
                float crashSpeed = 0f;
                Rigidbody carRb = other.GetComponent<Rigidbody>();
                if (carRb != null)
                {
                    crashSpeed = carRb.velocity.magnitude * 3.6f;
                }

                // 发送 Marker 13，并附带撞车速度
                SyncManager.Instance.TriggerEvent(13, crashSpeed, 0f, 0f);
            }
        }
    }

    private IEnumerator TurnScreenBlack()
    {
        if (trialsDone.Value <= maxTrials.Value)
        {
            CameraManager.Instance.FadeOut();
            yield return new WaitForSecondsRealtime(timeToWait.Value);
            CameraManager.Instance.FadeIn();
        }

        else
        {
            CameraManager.Instance.FadeOut();
            yield return new WaitForSecondsRealtime(timeToWait.Value);
            CameraManager.Instance.FadeIn();
        }
    }
}
