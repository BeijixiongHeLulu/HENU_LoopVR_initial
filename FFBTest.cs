using UnityEngine;

public class FFBTest : MonoBehaviour
{
    void Start()
    {
        // false 代表我们不忽略其他控制器的干扰，强行扫描
        LogitechGSDK.LogiSteeringInitialize(false);

        bool foundAny = false;
        // 扫描 0 到 3 号所有底层槽位
        for (int i = 0; i < 4; i++)
        {
            if (LogitechGSDK.LogiIsConnected(i))
            {
                Debug.Log($"【底层扫描】在索引 [{i}] 处抓到了设备！是否支持力反馈: {LogitechGSDK.LogiHasForceFeedback(i)}");
                foundAny = true;
            }
        }

        if (!foundAny)
        {
            Debug.LogError("【致命错误】0到3号索引全部为空！DLL 彻底瞎了，根本没看到你的方向盘！");
        }
    }

    void Update()
    {
        LogitechGSDK.LogiUpdate();

        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("【暴力测试】向所有槽位下发向右的巨力！");
            // 不管三七二十一，给所有 4 个槽位全部下发力反馈
            for (int i = 0; i < 4; i++)
            {
                LogitechGSDK.LogiPlaySpringForce(i, 100, 100, 100);
            }
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            for (int i = 0; i < 4; i++) LogitechGSDK.LogiStopSpringForce(i);
        }
    }

    void OnApplicationQuit()
    {
        LogitechGSDK.LogiSteeringShutdown();
    }
}