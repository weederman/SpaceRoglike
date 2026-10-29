using System.Collections;
using UnityEngine;

/// <summary>
/// 상황 배경음악. 그리드(플레이어 주변 gridRadius 안)에 적이 없으면 평온한 곡(Common_Sound) 중 하나를,
/// 적이 나타나면 전투 곡(Combat_Sound) 중 하나를 무작위로 틀고 서서히 바꿔 낀다. 곡이 끝나면 같은 종류에서 다른 곡을 고른다.
/// 적이 사라져도 바로 바꾸지 않고 calmDelay초 동안 없을 때 평온한 곡으로 돌아간다.
/// 스토리 배경음악(술집 등)이 나오는 동안에는 물러나 있다. 게임 씬이 시작되면 자동으로 생긴다.
/// 곡 목록: Assets/Resources/Audio/MusicPlaylist.asset
/// </summary>
public class MusicDirector : MonoBehaviour
{
    private const float GridRadius = 1500f;      // EVE의 "그리드"처럼 플레이어 주변을 보는 거리
    private const float CheckInterval = 0.5f;
    private const float CalmDelay = 6f;
    private const float FadeSeconds = 2.5f;

    private static MusicDirector _instance;

    private MusicPlaylist _playlist;
    private AudioSource[] _sources;
    private int _active;
    private bool _combat;
    private bool _wantCombat;
    private bool _started;
    private float _lastEnemySeen = float.NegativeInfinity;
    private float _nextCheck;
    private Coroutine _fade;
    private Transform _player;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null)
            return;

        var go = new GameObject("MusicDirector");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<MusicDirector>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
    }

    private void Awake()
    {
        _playlist = Resources.Load<MusicPlaylist>(MusicPlaylist.ResourcePath);
        _sources = new AudioSource[2];
        for (int i = 0; i < 2; i++)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            _sources[i] = source;
        }
    }

    private void Update()
    {
        if (_playlist == null)
            return;

        // 스토리 배경음악(술집 등)이 나오는 동안에는 물러난다
        if (GameAudio.IsStoryMusicPlaying)
        {
            if (_started)
                FadeTo(null, 0f);
            _started = false;
            return;
        }

        if (Time.unscaledTime >= _nextCheck)
        {
            _nextCheck = Time.unscaledTime + CheckInterval;
            if (EnemyOnGrid())
                _lastEnemySeen = Time.unscaledTime;

            _wantCombat = Time.unscaledTime - _lastEnemySeen < CalmDelay;
        }

        // 처음 시작, 상황이 바뀜, 또는 곡이 끝남 → 새 곡
        AudioSource current = _sources[_active];
        bool finished = _started && _fade == null && !current.isPlaying;
        if (!_started || _wantCombat != _combat || finished)
        {
            _combat = _wantCombat;
            _started = true;
            AudioClip[] list = _combat ? _playlist.combat : _playlist.calm;
            AudioClip clip = Pick(list, current.clip);
            if (clip != null)
                FadeTo(clip, _combat ? _playlist.combatVolume : _playlist.calmVolume);
        }
    }

    /// <summary>플레이어 주변(그리드)에 살아 있는 적 함선이 있는지. 정거장에 도킹해 있으면 없음.</summary>
    private bool EnemyOnGrid()
    {
        if (StationDocking.IsDocked)
            return false;

        if (_player == null)
        {
            var ship = FindObjectOfType<SpaceshipController>();
            if (ship == null)
                return false;
            _player = ship.transform;
        }

        HullHealth playerHull = _player.GetComponentInChildren<HullHealth>();
        float radiusSqr = GridRadius * GridRadius;
        foreach (HullHealth hull in HullHealth.All)
        {
            if (hull == playerHull || !hull.IsActive || FriendlyShip.Is(hull))
                continue;

            Vector3 d = hull.transform.position - _player.position;
            d.y = 0f;
            if (d.sqrMagnitude <= radiusSqr)
                return true;
        }

        return false;
    }

    private static AudioClip Pick(AudioClip[] list, AudioClip avoid)
    {
        if (list == null || list.Length == 0)
            return null;

        AudioClip clip = list[Random.Range(0, list.Length)];
        if (clip == avoid && list.Length > 1)
            clip = list[(System.Array.IndexOf(list, clip) + 1) % list.Length];
        return clip;
    }

    private void FadeTo(AudioClip clip, float volume)
    {
        if (_fade != null)
            StopCoroutine(_fade);
        _fade = StartCoroutine(Crossfade(clip, volume));
    }

    private IEnumerator Crossfade(AudioClip clip, float volume)
    {
        AudioSource from = _sources[_active];
        AudioSource to = null;
        if (clip != null)
        {
            _active = 1 - _active;
            to = _sources[_active];
            to.clip = clip;
            to.volume = 0f;
            to.Play();
        }

        float fromVolume = from.volume;
        for (float t = 0f; t < FadeSeconds; t += Time.unscaledDeltaTime)
        {
            float k = t / FadeSeconds;
            if (from != to) from.volume = fromVolume * (1f - k);
            if (to != null) to.volume = volume * k;
            yield return null;
        }

        if (from != to)
        {
            from.volume = 0f;
            from.Stop();
        }

        if (to != null)
            to.volume = volume;

        _fade = null;
    }
}
