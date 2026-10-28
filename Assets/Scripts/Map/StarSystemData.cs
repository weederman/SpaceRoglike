using System;
using UnityEngine;

/// <summary>
/// 항성계 맵 데이터(단일 출처). 항성을 중심으로 한 동심원 궤도들과, 그 위의 행성·정거장·워프 비콘을 담는다.
///  - 궤도: 행성 궤도와 소행성 벨트가 안쪽부터 번갈아 놓인다.
///  - 행성 궤도마다 행성(멀리 보이는 배경) 하나와 우주정거장 하나.
///  - 소행성 벨트마다 레벨 1~5의 워프 비콘 하나씩(총 5개). 레벨이 높을수록 광물 가치·해적 위험이 높다.
/// 위치는 "항성계 좌표"(항성 = 원점, XZ 평면)로 저장한다. 게임 월드 좌표로 바꾸는 일은 StarSystem이 한다.
/// 각도는 다른 연출 코드와 같이 위쪽(+Z, 북쪽) = 0도, 시계 방향.
/// 에디터 메뉴 SpaceRoglike/항성계 다시 생성 으로 만들며, 한 번 만든 뒤에는 고정된 맵으로 쓴다(값은 인스펙터에서 고칠 수 있다).
/// 위치: Assets/Resources/StarSystem.asset
/// </summary>
[CreateAssetMenu(fileName = "StarSystem", menuName = "SpaceRoglike/Star System Data")]
public class StarSystemData : ScriptableObject
{
    public const string ResourcePath = "StarSystem";
    public const int MaxLevel = 5;

    public enum OrbitKind
    {
        Planet,
        Belt
    }

    [Serializable]
    public class Orbit
    {
        public string name;
        public OrbitKind kind;
        [Min(1f)] public float radius;

        [Header("행성 궤도")]
        [Tooltip("행성이 놓인 각도 (배경으로 멀리 보이는 행성)")]
        public float planetAngle;
        public Sprite planetSprite;
        [Tooltip("배경에 보이는 행성의 크기 배율 (가스 행성은 크게)")]
        [Min(0.1f)] public float planetScale = 1f;

        [Header("소행성 벨트")]
        [Tooltip("항성계 지도에 그리는 벨트의 폭")]
        [Min(0f)] public float beltWidth;
    }

    [Serializable]
    public class Station
    {
        public string name;
        [Tooltip("orbits 안의 번호 (행성 궤도)")]
        public int orbit;
        public float angle;
    }

    [Serializable]
    public class Beacon
    {
        public string name;
        [Tooltip("orbits 안의 번호 (소행성 벨트)")]
        public int orbit;
        public float angle;
        [Range(1, MaxLevel)] public int level = 1;
    }

    public string systemName = "K-A46";
    public int seed;

    [Header("항성 (배경)")]
    public Sprite starSprite;

    public Orbit[] orbits = new Orbit[0];
    public Station[] stations = new Station[0];
    public Beacon[] beacons = new Beacon[0];

    [Header("튜토리얼 연결")]
    [Tooltip("씬에 놓인 모항 정거장(튜토리얼 정거장)에 해당하는 stations 번호")]
    public int homeStation;
    [Tooltip("튜토리얼 첫 워프(장면 3)가 도착하는 beacons 번호 (레벨 1)")]
    public int tutorialBeacon;

    /// <summary>궤도 위 한 점의 항성계 좌표 (XZ)</summary>
    public static Vector2 OnOrbit(float radius, float angleDegrees)
    {
        float rad = angleDegrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * radius;
    }

    public Vector2 StationPosition(int index)
    {
        Station s = stations[index];
        return OnOrbit(orbits[s.orbit].radius, s.angle);
    }

    public Vector2 BeaconPosition(int index)
    {
        Beacon b = beacons[index];
        return OnOrbit(orbits[b.orbit].radius, b.angle);
    }

    public Vector2 PlanetPosition(int orbitIndex)
    {
        Orbit o = orbits[orbitIndex];
        return OnOrbit(o.radius, o.planetAngle);
    }

    public float OuterRadius
    {
        get
        {
            float r = 0f;
            foreach (Orbit o in orbits)
                r = Mathf.Max(r, o.radius + o.beltWidth * 0.5f);
            return r;
        }
    }

    /// <summary>레벨별 표시 색 (1 = 초록 … 5 = 빨강)</summary>
    public static Color LevelColor(int level)
    {
        float t = Mathf.InverseLerp(1f, MaxLevel, level);
        return Color.HSVToRGB(Mathf.Lerp(0.36f, 0f, t), 0.75f, 1f);
    }
}
