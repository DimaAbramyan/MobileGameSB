using UnityEngine;

public class AudioServiceHost : MonoBehaviour
{
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }
}