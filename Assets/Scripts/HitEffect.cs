using UnityEngine;

public class HitEffect : MonoBehaviour
{
    public static HitEffect Instance;
    public GameObject hitPrefab;

    private void Awake()
    {
        Instance = this;
    }

    public void Spawn(Vector3 pos)
    {
        if (hitPrefab == null) return;

        GameObject fx = Instantiate(hitPrefab, pos, Quaternion.identity);

        fx.transform.localScale = Vector3.one * 1.5f;

        Destroy(fx, 0.5f);
    }
}