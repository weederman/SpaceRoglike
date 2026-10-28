using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 항성계 맵(StarSystemData)을 무작위로 한 번 만들어 Assets/Resources/StarSystem.asset에 저장한다.
/// 만든 뒤에는 고정된 맵으로 쓰고, 다시 만들고 싶을 때만 메뉴를 실행한다(시드가 같으면 같은 맵이 나온다).
///  - 행성 궤도 10개, 그 사이사이에 소행성 벨트 9개 (안쪽부터 행성 → 벨트 → 행성 …)
///  - 정거장: 행성 궤도마다 하나, 궤도 위 무작위 각도 (행성과는 떨어진 곳)
///  - 워프 비콘: 벨트마다 레벨 1~5가 하나씩, 무작위 각도 (서로 떨어지게)
///  - 모항(튜토리얼 정거장)은 3번째 행성 궤도의 정거장, 튜토리얼 비콘은 그 양옆 벨트의 레벨 1 비콘 중 가까운 것
/// </summary>
public static class StarSystemGenerator
{
    private const string AssetPath = "Assets/Resources/StarSystem.asset";
    private const string PlanetFolder = "Assets/2D Space Kit/Background Elements/Planets/";
    private const string StarSpritePath = "Assets/2D Space Kit/Background Elements/Stars/Main Sequence.png";

    private const int PlanetCount = 10;
    private const int BeaconsPerBelt = StarSystemData.MaxLevel;
    private const int DefaultSeed = 46;

    private const float FirstOrbitRadius = 3000f;
    private const float OrbitSpacing = 2000f;   // 궤도(행성/벨트) 사이 간격
    private const float OrbitJitter = 250f;
    private const float BeltWidth = 900f;
    private const float MinBeaconSeparation = 40f;  // 같은 벨트의 비콘끼리 최소 각도
    private const float MinStationToPlanet = 40f;   // 정거장과 행성 사이 최소 각도
    private const int HomePlanetIndex = 2;          // 모항: 3번째 행성 궤도

    // 안쪽(뜨겁고 작은 암석 행성) → 바깥쪽(가스·얼음 행성) 순서의 후보. (이름, 배경 크기 배율)
    private static readonly (string sprite, float scale)[][] PlanetTiers =
    {
        new[] { ("(Small) Rocky 1", 0.7f), ("Barren 2", 0.9f), ("Desert 1", 1f), ("Desert 2", 1f), ("Desert 3", 1f) },
        new[] { ("Earthlike 1", 1f), ("Earthlike 2", 1f), ("(Small) Earthlike 1", 0.75f), ("Thick Atmo 1", 1.1f), ("Thick Atmo 2", 1.1f), ("Primordial 1", 1f) },
        new[] { ("Gas 1", 1.6f), ("Gas 2", 1.6f), ("Gas 3", 1.6f), ("Ice 1", 1.1f) },
    };

    private static readonly string[] Roman = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };

    [MenuItem("SpaceRoglike/항성계 다시 생성 (Resources/StarSystem)")]
    private static void GenerateFromMenu()
    {
        var existing = AssetDatabase.LoadAssetAtPath<StarSystemData>(AssetPath);
        if (existing != null &&
            !EditorUtility.DisplayDialog("항성계 다시 생성", "이미 있는 항성계 맵을 새로 만들어 덮어씁니다.\n(정거장·비콘 위치가 모두 바뀝니다)", "덮어쓰기", "취소"))
            return;

        Generate(existing != null ? existing.seed : DefaultSeed);
    }

    /// <summary>시드로 항성계를 만들어 에셋에 저장하고 돌려준다. 같은 시드면 같은 맵.</summary>
    public static StarSystemData Generate(int seed)
    {
        var rng = new System.Random(seed);
        var data = AssetDatabase.LoadAssetAtPath<StarSystemData>(AssetPath);
        bool created = data == null;
        if (created)
            data = ScriptableObject.CreateInstance<StarSystemData>();

        data.seed = seed;
        data.starSprite = AssetDatabase.LoadAssetAtPath<Sprite>(StarSpritePath);

        List<(string sprite, float scale)> planetLooks = PickPlanetLooks(rng);

        // 궤도: 짝수 번째는 행성, 홀수 번째는 벨트 (행성 사이사이에 벨트)
        int orbitCount = PlanetCount * 2 - 1;
        var orbits = new StarSystemData.Orbit[orbitCount];
        int planetNo = 0;
        int beltNo = 0;
        for (int i = 0; i < orbitCount; i++)
        {
            var o = new StarSystemData.Orbit
            {
                radius = FirstOrbitRadius + i * OrbitSpacing + Range(rng, -OrbitJitter, OrbitJitter),
            };

            if (i % 2 == 0)
            {
                o.kind = StarSystemData.OrbitKind.Planet;
                o.name = $"{data.systemName} {Roman[planetNo]}";
                o.planetAngle = Range(rng, 0f, 360f);
                o.planetSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlanetFolder + planetLooks[planetNo].sprite + ".png");
                o.planetScale = planetLooks[planetNo].scale;
                planetNo++;
            }
            else
            {
                beltNo++;
                o.kind = StarSystemData.OrbitKind.Belt;
                o.name = $"제{beltNo} 소행성대";
                o.beltWidth = BeltWidth;
            }

            orbits[i] = o;
        }

        data.orbits = orbits;

        // 정거장: 행성 궤도마다 하나
        var stations = new List<StarSystemData.Station>();
        for (int i = 0; i < orbitCount; i++)
        {
            if (orbits[i].kind != StarSystemData.OrbitKind.Planet)
                continue;

            float angle;
            do
            {
                angle = Range(rng, 0f, 360f);
            } while (Mathf.Abs(Mathf.DeltaAngle(angle, orbits[i].planetAngle)) < MinStationToPlanet);

            stations.Add(new StarSystemData.Station { name = $"{orbits[i].name} 정거장", orbit = i, angle = angle });
        }

        data.stations = stations.ToArray();

        // 워프 비콘: 벨트마다 레벨 1~5 하나씩
        var beacons = new List<StarSystemData.Beacon>();
        for (int i = 0; i < orbitCount; i++)
        {
            if (orbits[i].kind != StarSystemData.OrbitKind.Belt)
                continue;

            var angles = new List<float>();
            while (angles.Count < BeaconsPerBelt)
            {
                float a = Range(rng, 0f, 360f);
                if (angles.TrueForAll(other => Mathf.Abs(Mathf.DeltaAngle(a, other)) >= MinBeaconSeparation))
                    angles.Add(a);
            }

            var levels = new List<int>();
            for (int level = 1; level <= BeaconsPerBelt; level++)
                levels.Add(level);
            Shuffle(levels, rng);

            for (int k = 0; k < BeaconsPerBelt; k++)
            {
                beacons.Add(new StarSystemData.Beacon
                {
                    name = $"{orbits[i].name} 비콘 {k + 1}",
                    orbit = i,
                    angle = angles[k],
                    level = levels[k],
                });
            }
        }

        data.beacons = beacons.ToArray();

        // 튜토리얼 연결: 모항 정거장과, 그 양옆 벨트의 레벨 1 비콘 중 가까운 것
        data.homeStation = Mathf.Clamp(HomePlanetIndex, 0, data.stations.Length - 1);
        Vector2 home = data.StationPosition(data.homeStation);
        int homeOrbit = data.stations[data.homeStation].orbit;
        float best = float.PositiveInfinity;
        for (int b = 0; b < data.beacons.Length; b++)
        {
            StarSystemData.Beacon beacon = data.beacons[b];
            if (beacon.level != 1 || Mathf.Abs(beacon.orbit - homeOrbit) != 1)
                continue;

            float d = Vector2.Distance(home, data.BeaconPosition(b));
            if (d < best)
            {
                best = d;
                data.tutorialBeacon = b;
            }
        }

        if (created)
            AssetDatabase.CreateAsset(data, AssetPath);

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        Debug.Log($"[StarSystem] 항성계 '{data.systemName}' 생성 (시드 {seed}): 궤도 {orbits.Length}, 정거장 {data.stations.Length}, 비콘 {data.beacons.Length} → {AssetPath}");
        return data;
    }

    /// <summary>행성 10개의 모습: 안쪽 3개는 암석, 가운데 3개는 대기 행성, 바깥 4개는 가스·얼음 행성 (구간 안에서 무작위, 겹치지 않게)</summary>
    private static List<(string, float)> PickPlanetLooks(System.Random rng)
    {
        int[] perTier = { 3, 3, 4 };
        var result = new List<(string, float)>();
        for (int t = 0; t < PlanetTiers.Length; t++)
        {
            var pool = new List<(string, float)>(PlanetTiers[t]);
            Shuffle(pool, rng);
            for (int k = 0; k < perTier[t]; k++)
                result.Add(pool[k % pool.Count]);
        }

        return result;
    }

    private static float Range(System.Random rng, float min, float max)
    {
        return Mathf.Lerp(min, max, (float)rng.NextDouble());
    }

    private static void Shuffle<T>(IList<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
