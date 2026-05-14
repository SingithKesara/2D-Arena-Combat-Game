using System.Collections;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance;

    public Vector3 CurrentOffset { get; private set; }

    private Coroutine _shakeRoutine;

    private void Awake()
    {
        Instance = this;
        CurrentOffset = Vector3.zero;
    }

    public void Shake(float duration, float strength)
    {
        if (_shakeRoutine != null)
            StopCoroutine(_shakeRoutine);

        _shakeRoutine = StartCoroutine(ShakeRoutine(duration, strength));
    }

    private IEnumerator ShakeRoutine(float duration, float strength)
    {
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;

            Vector2 random = Random.insideUnitCircle * strength;
            CurrentOffset = new Vector3(random.x, random.y, 0f);

            yield return null;
        }

        CurrentOffset = Vector3.zero;
        _shakeRoutine = null;
    }
}