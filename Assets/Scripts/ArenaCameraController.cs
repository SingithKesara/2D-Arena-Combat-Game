using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class ArenaCameraController : MonoBehaviour
{
    [Header("Players")]
    public Transform player1;
    public Transform player2;
    public bool autoFindPlayers = true;

    [Header("Follow")]
    public float followSmooth = 6f;
    public float verticalOffset = 0.45f;

    [Header("Zoom")]
    public float zoomSmooth = 5f;
    public float minOrthographicSize = 4.7f;
    public float maxOrthographicSize = 6.8f;
    public float horizontalPadding = 2.0f;
    public float verticalPadding = 1.6f;

    [Header("Arena Camera Limits")]
    public bool useCameraLimits = true;
    public float minCenterX = -1.4f;
    public float maxCenterX = 1.4f;
    public float minCenterY = -0.2f;
    public float maxCenterY = 1.5f;

    [Header("Fall / Death Tracking")]
    public bool ignoreDeadPlayers = true;
    public bool ignorePlayersBelowLimit = true;
    public float ignoreBelowY = -6.5f;

    [Header("Start")]
    public bool snapOnStart = true;

    private Camera _cam;
    private Vector3 _lastGoodTarget;
    private bool _hasSnapped;

    private readonly List<Vector3> _trackedPositions = new List<Vector3>();

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        _cam.orthographic = true;

        if (autoFindPlayers)
            AutoFindPlayers();
    }

    private void Start()
    {
        if (autoFindPlayers)
            AutoFindPlayers();

        UpdateCamera(true);
    }

    private void LateUpdate()
    {
        if (autoFindPlayers && (player1 == null || player2 == null))
            AutoFindPlayers();

        UpdateCamera(false);
    }

    private void AutoFindPlayers()
    {
        PlayerController[] players = Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude);

        foreach (PlayerController pc in players)
        {
            if (pc == null) continue;

            if (pc.playerIndex == 1)
                player1 = pc.transform;
            else if (pc.playerIndex == 2)
                player2 = pc.transform;
        }
    }

    private void UpdateCamera(bool forceSnap)
    {
        _trackedPositions.Clear();

        if (IsTrackable(player1))
            _trackedPositions.Add(player1.position);

        if (IsTrackable(player2))
            _trackedPositions.Add(player2.position);

        if (_trackedPositions.Count == 0)
        {
            if (_lastGoodTarget == Vector3.zero)
                _lastGoodTarget = new Vector3(0f, 0.3f, transform.position.z);

            MoveCamera(_lastGoodTarget, minOrthographicSize, forceSnap);
            return;
        }

        Bounds bounds = new Bounds(_trackedPositions[0], Vector3.zero);

        for (int i = 1; i < _trackedPositions.Count; i++)
            bounds.Encapsulate(_trackedPositions[i]);

        Vector3 targetPosition = bounds.center;
        targetPosition.y += verticalOffset;
        targetPosition.z = transform.position.z;

        if (useCameraLimits)
        {
            targetPosition.x = Mathf.Clamp(targetPosition.x, minCenterX, maxCenterX);
            targetPosition.y = Mathf.Clamp(targetPosition.y, minCenterY, maxCenterY);
        }

        float desiredSize = CalculateRequiredZoom(bounds);

        _lastGoodTarget = targetPosition;

        MoveCamera(targetPosition, desiredSize, forceSnap || (snapOnStart && !_hasSnapped));
        _hasSnapped = true;
    }

    private bool IsTrackable(Transform target)
    {
        if (target == null) return false;
        if (!target.gameObject.activeInHierarchy) return false;

        PlayerController pc = target.GetComponent<PlayerController>();

        if (ignoreDeadPlayers && pc != null && pc.isDead)
            return false;

        if (ignorePlayersBelowLimit && target.position.y < ignoreBelowY)
            return false;

        return true;
    }

    private float CalculateRequiredZoom(Bounds bounds)
    {
        float aspect = Mathf.Max(_cam.aspect, 0.01f);

        float sizeByHeight = (bounds.size.y * 0.5f) + verticalPadding;
        float sizeByWidth = (bounds.size.x * 0.5f / aspect) + horizontalPadding;

        float requiredSize = Mathf.Max(sizeByHeight, sizeByWidth, minOrthographicSize);
        return Mathf.Clamp(requiredSize, minOrthographicSize, maxOrthographicSize);
    }

    private void MoveCamera(Vector3 targetPosition, float targetSize, bool forceSnap)
    {
        if (forceSnap)
        {
            transform.position = targetPosition;
            _cam.orthographicSize = targetSize;
            return;
        }

        float followT = 1f - Mathf.Exp(-followSmooth * Time.deltaTime);
        float zoomT = 1f - Mathf.Exp(-zoomSmooth * Time.deltaTime);

        transform.position = Vector3.Lerp(transform.position, targetPosition, followT);
        _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, targetSize, zoomT);
    }

    private void OnDrawGizmosSelected()
    {
        if (!useCameraLimits) return;

        Gizmos.color = Color.cyan;

        Vector3 bottomLeft = new Vector3(minCenterX, minCenterY, 0f);
        Vector3 topLeft = new Vector3(minCenterX, maxCenterY, 0f);
        Vector3 topRight = new Vector3(maxCenterX, maxCenterY, 0f);
        Vector3 bottomRight = new Vector3(maxCenterX, minCenterY, 0f);

        Gizmos.DrawLine(bottomLeft, topLeft);
        Gizmos.DrawLine(topLeft, topRight);
        Gizmos.DrawLine(topRight, bottomRight);
        Gizmos.DrawLine(bottomRight, bottomLeft);
    }
}