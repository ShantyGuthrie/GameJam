using UnityEngine;

public class BossNivel2 : MonoBehaviour
{
    [Header("Ciclo del boss")]
    [SerializeField] private bool iniciarAlComenzarNivel = false;
    [SerializeField] private int aparicionesMaximas = 3;
    [SerializeField] private float duracionAparicion = 1.2f;
    [SerializeField] private float duracionActivo = 5f;
    [SerializeField] private float tiempoEntreApariciones = 15f;

    [Header("Persecucion")]
    [SerializeField] private float velocidadSeguimiento = 3f;
    [SerializeField] private float distanciaMinimaAlPlayer = 0.75f;
    [SerializeField] private Vector2 offsetAparicionDesdePlayer = new Vector2(1.2f, 0.6f);
    [SerializeField] private LayerMask capaBloqueoBoss;
    [SerializeField] private float radioColisionBoss = 0.35f;
    [SerializeField] private float margenFrenteObstaculo = 0.08f;

    [Header("Debuff al Player")]
    [SerializeField, Range(0.1f, 1f)] private float multiplicadorMovimientoPlayer = 0.65f;
    [SerializeField, Range(0.1f, 1f)] private float multiplicadorSaltoPlayer = 0.7f;
    [SerializeField] private float intervaloDanio = 1f;
    [SerializeField] private float radioEfecto = 1.2f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSourceSfx;
    [SerializeField] private AudioClip sonidoAparicionBoss;

    private SpriteRenderer[] _sprites;
    private Collider2D[] _colliders;
    private PlayerController _player;
    private bool _bossActivo;
    private bool _apareciendo;
    private bool _cicloFinalizado;
    private int _aparicionesRealizadas;
    private float _tiempoInicioAparicion;
    private float _tiempoFinActivo;
    private float _tiempoProximaAparicion;
    private float _proximoDanio;

    private void Awake()
    {
        _sprites = GetComponentsInChildren<SpriteRenderer>(true);
        _colliders = GetComponentsInChildren<Collider2D>(true);
        _player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        SetColisionActiva(false);
        SetVisualActiva(false); // Arranca invisible en juego.

        if (audioSourceSfx == null)
            audioSourceSfx = GetComponent<AudioSource>();
        if (audioSourceSfx == null)
            audioSourceSfx = gameObject.AddComponent<AudioSource>();
        audioSourceSfx.playOnAwake = false;
        audioSourceSfx.loop = false;
        audioSourceSfx.spatialBlend = 0f;
    }

    private void Start()
    {
        if (iniciarAlComenzarNivel)
            ActivarBoss();
        else
            _tiempoProximaAparicion = Time.time + Mathf.Max(0.1f, tiempoEntreApariciones);
    }

    private void Update()
    {
        if (_cicloFinalizado)
            return;

        if (_apareciendo)
        {
            float progreso = Mathf.Clamp01((Time.time - _tiempoInicioAparicion) / Mathf.Max(0.01f, duracionAparicion));
            SetAlpha(progreso);
            if (progreso >= 1f)
                _apareciendo = false;
        }

        if (_bossActivo)
        {
            SeguirPlayer();
            AplicarDebuffYDanioSiCorresponde();
            if (Time.time >= _tiempoFinActivo)
                DesactivarBoss();
            return;
        }

        if (Time.time >= _tiempoProximaAparicion)
            ActivarBoss();
    }

    public void ActivarBoss()
    {
        if (_player == null)
            _player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);

        if (_player == null)
            return;

        if (aparicionesMaximas > 0 && _aparicionesRealizadas >= aparicionesMaximas)
        {
            _cicloFinalizado = true;
            return;
        }

        _aparicionesRealizadas++;
        transform.position = PosicionAparicionDesdePlayer();
        _bossActivo = true;
        _apareciendo = true;
        _tiempoInicioAparicion = Time.time;
        _tiempoFinActivo = Time.time + Mathf.Max(0.1f, duracionActivo);
        _proximoDanio = Time.time;
        SetColisionActiva(true);
        SetVisualActiva(true);
        SetAlpha(0f);
        ReproducirSonidoAparicion();
    }

    private void IntentarDanio(PlayerController player)
    {
        if (player == null || Time.time < _proximoDanio)
            return;

        _proximoDanio = Time.time + Mathf.Max(0.1f, intervaloDanio);
        player.RecibirDanio(PlayerController.FuenteDanio.Boss);
    }

    private void DesactivarBoss()
    {
        _bossActivo = false;
        _apareciendo = false;
        _tiempoProximaAparicion = Time.time + Mathf.Max(0.1f, tiempoEntreApariciones);
        SetColisionActiva(false);
        SetVisualActiva(false);

        if (aparicionesMaximas > 0 && _aparicionesRealizadas >= aparicionesMaximas)
            _cicloFinalizado = true;
    }

    private void SeguirPlayer()
    {
        if (_player == null || !_player.gameObject.activeInHierarchy)
            return;

        Vector3 destino = _player.transform.position;
        Vector3 delta = destino - transform.position;
        delta.z = 0f;
        if (delta.magnitude <= distanciaMinimaAlPlayer)
            return;

        Vector2 origen = transform.position;
        Vector2 direccion = ((Vector2)destino - origen).normalized;
        float distanciaPaso = Mathf.Max(0f, velocidadSeguimiento) * Time.deltaTime;

        RaycastHit2D hit = Physics2D.CircleCast(
            origen,
            Mathf.Max(0.01f, radioColisionBoss),
            direccion,
            distanciaPaso,
            capaBloqueoBoss);

        if (hit.collider == null)
        {
            transform.position = Vector2.MoveTowards(origen, destino, distanciaPaso);
            return;
        }

        float distanciaPermitida = Mathf.Max(0f, hit.distance - Mathf.Max(0f, margenFrenteObstaculo));
        if (distanciaPermitida > 0.001f)
            transform.position = origen + direccion * Mathf.Min(distanciaPaso, distanciaPermitida);
    }

    private void AplicarDebuffYDanioSiCorresponde()
    {
        if (_player == null || !_player.gameObject.activeInHierarchy)
            return;

        if (!PlayerEnAlcanceDelBoss())
            return;

        _player.AplicarDebuffBoss(multiplicadorMovimientoPlayer, multiplicadorSaltoPlayer);
        IntentarDanio(_player);
    }

    private bool PlayerEnAlcanceDelBoss()
    {
        if (_player == null)
            return false;

        float r = Mathf.Max(0.05f, radioEfecto);
        if (Vector2.Distance(transform.position, _player.transform.position) <= r)
            return true;

        if (_colliders == null || _colliders.Length == 0)
            return false;

        Collider2D[] playerCols = _player.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < _colliders.Length; i++)
        {
            Collider2D bc = _colliders[i];
            if (bc == null || !bc.enabled)
                continue;

            for (int j = 0; j < playerCols.Length; j++)
            {
                Collider2D pc = playerCols[j];
                if (pc == null || !pc.enabled)
                    continue;

                ColliderDistance2D d = Physics2D.Distance(bc, pc);
                if (d.isOverlapped || d.distance <= 0.02f)
                    return true;
            }
        }

        return false;
    }

    private void SetColisionActiva(bool activa)
    {
        if (_colliders == null)
            return;

        for (int i = 0; i < _colliders.Length; i++)
            _colliders[i].enabled = activa;
    }

    private Vector3 PosicionAparicionDesdePlayer()
    {
        if (_player == null)
            return transform.position;

        Vector3 posPlayer = _player.transform.position;
        Vector2[] offsets = new Vector2[]
        {
            offsetAparicionDesdePlayer,
            new Vector2(-offsetAparicionDesdePlayer.x, offsetAparicionDesdePlayer.y),
            new Vector2(offsetAparicionDesdePlayer.x, -offsetAparicionDesdePlayer.y),
            new Vector2(-offsetAparicionDesdePlayer.x, -offsetAparicionDesdePlayer.y),
            new Vector2(0f, offsetAparicionDesdePlayer.y),
            new Vector2(0f, -offsetAparicionDesdePlayer.y)
        };

        float radioChequeo = Mathf.Max(0.01f, radioColisionBoss);
        for (int i = 0; i < offsets.Length; i++)
        {
            Vector2 candidata = (Vector2)posPlayer + offsets[i];
            Collider2D bloqueado = Physics2D.OverlapCircle(candidata, radioChequeo, capaBloqueoBoss);
            if (bloqueado == null)
                return new Vector3(candidata.x, candidata.y, transform.position.z);
        }

        return new Vector3(posPlayer.x, posPlayer.y + 1f, transform.position.z);
    }

    private void SetAlpha(float alpha)
    {
        if (_sprites == null)
            return;

        float alphaFinal = Mathf.Clamp01(alpha);
        for (int i = 0; i < _sprites.Length; i++)
        {
            Color c = _sprites[i].color;
            c.a = alphaFinal;
            _sprites[i].color = c;
        }
    }

    private void SetVisualActiva(bool activa)
    {
        if (_sprites == null)
            return;

        for (int i = 0; i < _sprites.Length; i++)
            _sprites[i].enabled = activa;

        if (!activa)
            SetAlpha(0f);
    }

    private void ReproducirSonidoAparicion()
    {
        if (audioSourceSfx == null || sonidoAparicionBoss == null)
            return;

        audioSourceSfx.PlayOneShot(sonidoAparicionBoss);
    }
}
