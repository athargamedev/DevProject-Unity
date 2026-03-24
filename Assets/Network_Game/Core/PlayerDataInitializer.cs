using UnityEngine;

namespace Network_Game.Core
{
    /// <summary>
    /// Ensures PlayerDataManager is available in the scene.
    /// Add to your bootstrap or main scene GameObject.
    /// </summary>
    public sealed class PlayerDataInitializer : MonoBehaviour
    {
        [SerializeField]
        private bool m_DontDestroyOnLoad = true;

        private void Awake()
        {
            // Ensure singleton exists
            if (PlayerDataManager.Instance == null)
            {
                var go = new GameObject("PlayerDataManager");
                go.AddComponent<PlayerDataManager>();
                
                if (m_DontDestroyOnLoad)
                {
                    DontDestroyOnLoad(go);
                }
                
                Debug.Log("[PlayerData] Manager initialized");
            }

            Destroy(this); // Self-destruct after ensuring manager exists
        }
    }
}
