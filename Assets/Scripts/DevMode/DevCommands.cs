#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Globalization;
using FORGE3D;
using UnityEngine;

/// <summary>
/// 개발자 모드 채팅창에 입력한 한 줄을 해석해 DevTuning에 반영하고, 채팅에 보여줄 응답 문장을 돌려준다.
/// 창 자체를 다루는 명령(pause, reset)은 DevConsole이 처리한다.
/// </summary>
public static class DevCommands
{
    private const int MaxEnemyCount = 30;

    public const string HelpText =
        "<b>명령어</b>  (대소문자 구분 없음, 소수 입력 가능)\n" +
        "enemy count <개수>        동시에 존재하는 적 수 (1~30)\n" +
        "enemy respawn <초>        적이 죽은 뒤 다시 나오기까지 시간\n" +
        "enemy hp <값>             적 선체 체력\n" +
        "enemy shield <값>         적 쉴드 체력\n" +
        "enemy speed <값>          적 최고 속도\n" +
        "enemy accel <값>          적 가속도\n" +
        "enemy orbit <값>          적이 플레이어를 도는 거리\n" +
        "enemy interval <초>       적 공격 간격\n" +
        "enemy range <값>          적 공격 사거리\n" +
        "enemy damage <값>         모든 무기의 적 공격력 (빔은 초당)\n" +
        "enemy damage <무기> <값>  특정 무기만 (예: enemy damage vulcan 30)\n" +
        "player speed <값>         플레이어 최고 속도\n" +
        "player accel <값>         플레이어 가속도\n" +
        "player hp <값>            플레이어 선체 최대 체력 (가득 채움)\n" +
        "player shield <값>        플레이어 쉴드 최대 체력 (가득 채움)\n" +
        "player heal               플레이어 체력/쉴드 즉시 회복\n" +
        "player god [on|off]       무적 (생략하면 전환)\n" +
        "credit <값>               크레딧 지급 (음수면 차감)\n" +
        "credit set <값>           크레딧을 그 값으로 설정\n" +
        "pause [on|off]            창이 열려 있는 동안 게임 정지 여부\n" +
        "reset                     바꾼 값을 모두 되돌리고 씬 다시 시작\n" +
        "clear                     채팅 지우기";

    /// <returns>채팅에 출력할 응답. 출력할 게 없으면 null.</returns>
    public static string Execute(string line)
    {
        string[] t = line.Trim().ToLowerInvariant().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        if (t.Length == 0)
            return null;

        switch (t[0])
        {
            case "help":
            case "?":
                return HelpText;
            case "enemy":
                return Enemy(t);
            case "player":
                return Player(t);
            case "credit":
                return Credit(t);
            default:
                return Err($"알 수 없는 명령어: {t[0]}  (help 입력)");
        }
    }

    private static string Enemy(string[] t)
    {
        if (t.Length < 3)
            return Err("사용법: enemy <항목> <값>   (help 입력)");

        if (t[1] == "damage")
            return EnemyDamage(t);

        if (!TryFloat(t[2], out float v))
            return Err($"숫자가 아닙니다: {t[2]}");

        switch (t[1])
        {
            case "count":
                return SetSpawner(ref DevTuning.EnemyCount, Mathf.Clamp(Mathf.Round(v), 1, MaxEnemyCount), "동시 개체수", s => s.MaxAliveCount = Mathf.RoundToInt(DevTuning.EnemyCount.Value));
            case "respawn":
                return SetSpawner(ref DevTuning.EnemyRespawnDelay, Mathf.Max(0f, v), "리스폰 대기(초)", s => s.RespawnDelay = DevTuning.EnemyRespawnDelay.Value);
            case "hp":
                return SetEnemy(ref DevTuning.EnemyHp, Mathf.Max(1f, v), "체력");
            case "shield":
                return SetEnemy(ref DevTuning.EnemyShield, Mathf.Max(1f, v), "쉴드");
            case "speed":
                return SetEnemy(ref DevTuning.EnemySpeed, Mathf.Max(0f, v), "최고 속도");
            case "accel":
                return SetEnemy(ref DevTuning.EnemyAcceleration, Mathf.Max(0f, v), "가속도");
            case "orbit":
                return SetEnemy(ref DevTuning.EnemyOrbitDistance, Mathf.Max(0f, v), "공전 거리");
            case "interval":
                return SetEnemy(ref DevTuning.EnemyFireInterval, Mathf.Max(0.05f, v), "공격 간격(초)");
            case "range":
                return SetEnemy(ref DevTuning.EnemyFireRange, Mathf.Max(0f, v), "사거리");
            default:
                return Err($"알 수 없는 항목: enemy {t[1]}  (help 입력)");
        }
    }

    private static string EnemyDamage(string[] t)
    {
        // enemy damage <값>  /  enemy damage <무기> <값>
        if (t.Length == 3)
        {
            if (!TryFloat(t[2], out float all))
                return Err($"숫자가 아닙니다: {t[2]}");

            DevTuning.EnemyDamageAll = Mathf.Max(0f, all);
            DevTuning.EnemyDamageByWeapon.Clear();
            return Ok($"적 공격력 = {F(DevTuning.EnemyDamageAll.Value)}  (모든 무기, 바로 적용)");
        }

        if (t.Length == 4)
        {
            if (!Enum.TryParse(t[2], true, out F3DFXType type) || !Enum.IsDefined(typeof(F3DFXType), type))
                return Err($"알 수 없는 무기: {t[2]}\n사용 가능: {string.Join(", ", Enum.GetNames(typeof(F3DFXType)))}");
            if (!TryFloat(t[3], out float v))
                return Err($"숫자가 아닙니다: {t[3]}");

            DevTuning.EnemyDamageByWeapon[type] = Mathf.Max(0f, v);
            return Ok($"적 {type} 공격력 = {F(Mathf.Max(0f, v))}  (바로 적용)");
        }

        return Err("사용법: enemy damage <값>   또는   enemy damage <무기> <값>");
    }

    private static string Player(string[] t)
    {
        if (t.Length < 2)
            return Err("사용법: player <항목> [값]   (help 입력)");

        if (t[1] == "heal")
        {
            return DevTuning.HealPlayer()
                ? Ok("플레이어 체력/쉴드를 가득 채웠습니다.")
                : Err(PlayerNotFound);
        }

        if (t[1] == "god")
            return PlayerGod(t);

        if (t.Length < 3)
            return Err($"사용법: player {t[1]} <값>");

        if (!TryFloat(t[2], out float v))
            return Err($"숫자가 아닙니다: {t[2]}");

        string label;
        switch (t[1])
        {
            case "speed":
                v = Mathf.Max(0f, v);
                DevTuning.PlayerSpeed = v;
                label = "최고 속도";
                break;
            case "accel":
                v = Mathf.Max(0f, v);
                DevTuning.PlayerAcceleration = v;
                label = "가속도";
                break;
            case "hp":
                v = Mathf.Max(1f, v);
                DevTuning.PlayerHp = v;
                label = "체력";
                break;
            case "shield":
                v = Mathf.Max(1f, v);
                DevTuning.PlayerShield = v;
                label = "쉴드";
                break;
            default:
                return Err($"알 수 없는 항목: player {t[1]}  (help 입력)");
        }

        return DevTuning.ApplyToPlayer()
            ? Ok($"플레이어 {label} = {F(v)}")
            : Err($"{PlayerNotFound} 값({F(v)})은 저장했고 다음에 적용됩니다.");
    }

    private static string PlayerGod(string[] t)
    {
        if (t.Length > 2 && t[2] != "on" && t[2] != "off")
            return Err("사용법: player god [on|off]");

        DevTuning.PlayerGod = t.Length > 2 ? t[2] == "on" : !DevTuning.PlayerGod;

        if (!DevTuning.ApplyToPlayer())
            return Err($"{PlayerNotFound} 설정은 저장했고 다음에 적용됩니다.");

        return Ok(DevTuning.PlayerGod ? "무적 켜짐 — 플레이어가 데미지를 받지 않습니다." : "무적 꺼짐");
    }

    private static string Credit(string[] t)
    {
        var wallet = CreditWallet.Instance;
        if (wallet == null)
            return Err("CreditWallet을 찾지 못했습니다.");

        bool set = t.Length == 3 && t[1] == "set";
        if (t.Length != (set ? 3 : 2))
            return Err("사용법: credit <값>   또는   credit set <값>");

        if (!TryFloat(t[set ? 2 : 1], out float v))
            return Err($"숫자가 아닙니다: {t[set ? 2 : 1]}");

        int amount = Mathf.RoundToInt(v);

        if (set)
        {
            wallet.SetAmount(amount);
        }
        else if (amount >= 0)
        {
            wallet.Add(amount);
        }
        else if (!wallet.TrySpend(-amount))
        {
            // 가진 것보다 많이 빼려 하면 0으로 맞춘다
            wallet.SetAmount(0);
        }

        return Ok($"크레딧 = {wallet.Amount}");
    }

    private const string PlayerNotFound = "플레이어를 찾지 못했습니다.";

    /// <summary>값을 저장하고, 살아있는 모든 적에게 적용한다. 이후 스폰되는 적에게도 적용된다.</summary>
    private static string SetEnemy(ref float? field, float value, string label)
    {
        field = value;

        int applied = 0;
        var spawner = UnityEngine.Object.FindObjectOfType<EnemySpawner>();
        if (spawner != null)
        {
            for (int i = 0; i < spawner.AliveEnemies.Count; i++)
            {
                DevTuning.ApplyToEnemy(spawner.AliveEnemies[i]);
                applied++;
            }
        }

        return Ok($"적 {label} = {F(value)}  (살아있는 적 {applied}기 + 이후 스폰되는 적)");
    }

    private static string SetSpawner(ref float? field, float value, string label, Action<EnemySpawner> apply)
    {
        field = value;

        var spawner = UnityEngine.Object.FindObjectOfType<EnemySpawner>();
        if (spawner == null)
            return Err($"EnemySpawner를 찾지 못했습니다. 값({F(value)})은 저장했고 다음 씬 로드 때 적용됩니다.");

        apply(spawner);
        return Ok($"적 {label} = {F(value)}");
    }

    private static bool TryFloat(string s, out float v)
    {
        return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v)
            && !float.IsNaN(v) && !float.IsInfinity(v);
    }

    public static string F(float v)
    {
        return v.ToString("0.##", CultureInfo.InvariantCulture);
    }

    public static string Ok(string msg) => $"<color=#7CFC9A>{msg}</color>";
    public static string Err(string msg) => $"<color=#FF7B7B>{msg}</color>";
}
#endif
