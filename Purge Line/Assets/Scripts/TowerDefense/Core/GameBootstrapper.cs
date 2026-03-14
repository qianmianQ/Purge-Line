using UnityEngine;

/// <summary>
/// 游戏启动器（项目专属）：场景唯一入口
///
/// 使用方式：
///   场景中创建一个空 GameObject，挂载此组件即可。
///   无需在场景中手动放置 GameFramework 或 SystemManager，均由此脚本程序化创建。
/// </summary>
public class GameBootstrapper : MonoBehaviour
{
    private void Awake()
    {
        if(FindObjectOfType<GameFramework>() != null)
        {
            return;
        }
        DontDestroyOnLoad(gameObject);
        var fwGo = new GameObject("[GameFramework]")
        {
            transform =
            {
                parent = transform
            }
        };
        fwGo.AddComponent<GameFramework>();
    }
}
