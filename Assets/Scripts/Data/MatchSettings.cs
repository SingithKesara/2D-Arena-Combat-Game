using UnityEngine;

[CreateAssetMenu(fileName = "MatchSettings", menuName = "Arena Combat/Match Settings", order = 1)]
public class MatchSettings : ScriptableObject
{
    [Header("Match")]
    public int roundsToWin = 2;
    public float matchTimeSec = 99f;

    [Header("Round Pacing")]
    public float roundIntroDelay = 1.0f;
    public float fightTextDelay = 0.7f;
    public float roundEndDelay = 2.0f;

    [Header("Spawn")]
    public Vector2 spawnP1Position = new Vector2(-2.8f, -1.10f);
    public Vector2 spawnP2Position = new Vector2(2.8f, -1.10f);

    [Header("Arena")]
    public float deathZoneY = -8.5f;
}
