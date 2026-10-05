using UnityEngine;
using Zenject;

public class DebugProjectContextLoad : MonoBehaviour
{
    void Start()
    {
        Debug.Log("========== DEBUG ==========");

        var projectContext = ProjectContext.Instance;
        if (projectContext != null)
        {
            Debug.Log($"✅ ProjectContext loaded: {projectContext.name}");

            var container = projectContext.Container;
            Debug.Log($"Container has AudioDatabase: {container.HasBinding<AudioDatabase>()}");
        }
        else
        {
            Debug.LogError("❌ ProjectContext.Instance is NULL!");
            Debug.LogError("Check: Prefab must be named 'ProjectContext' in Resources folder");
        }

        Debug.Log("==========================");
    }
}