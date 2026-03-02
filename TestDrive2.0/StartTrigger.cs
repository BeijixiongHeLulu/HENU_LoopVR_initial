using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StartTrigger : MonoBehaviour
{
    [SerializeField] private int aimedSpeed;
    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<CarController>())
        {
            StartCoroutine(Triggered(other));
        }
    }

    private IEnumerator Triggered(Collider other)
    {
        other.gameObject.GetComponent<CarController>().TurnOffEngine();
        yield return new WaitForSecondsRealtime(3);
        other.gameObject.GetComponent<CarController>().TurnOnEngine();

        other.gameObject.GetComponent<AimedSpeed>().SetRuleSpeed(aimedSpeed / 3.6f);
        other.gameObject.GetComponent<AIController>().SetLocalTargetAndCurveDetection();
        other.gameObject.GetComponent<ControlSwitch>().SwitchControl(false);
        GetComponent<BoxCollider>().enabled = false;

        // --- [新增] 实验路段/回合正式开始，发出 Marker 4 ---
        if (SyncManager.Instance != null)
        {
            SyncManager.Instance.TriggerEvent(4);
        }
    }

}
